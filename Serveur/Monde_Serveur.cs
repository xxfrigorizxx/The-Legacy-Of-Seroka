using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FileAccess = Godot.FileAccess;

/// <summary>Détient les chunks serveur (données voxel), la génération, la simulation d'eau. Aucun MeshInstance3D.</summary>
public partial class Monde_Serveur : Node
{
	[Export] public int TailleChunk = 16;
	[Export] public int HauteurMax = 720;  // Montagnes jusqu'à 700
	[Export] public int SeedTerrain = 19847;
	[Export] public int RayonMondeChunks = 1000;
	[Export] public int RenderDistance = 200;

	/// <summary>Matériel du terrain pour les débris (BlocChutant). Assigné par Gestionnaire_Monde.</summary>
	public Material MaterielTerrain;

	/// <summary>Fuseau horaire de la dimension en heures. Monde 1 = 0, Monde 2 = +6, Monde 3 = +12, Monde 4 = +18.</summary>
	[Export] public double FuseauHoraireHeures = 0.0;

	private Dictionary<Vector2I, Chunk_Serveur> _chunks = new Dictionary<Vector2I, Chunk_Serveur>();
	private Queue<Vector3I> _fileEau = new Queue<Vector3I>();
	private HashSet<Vector3I> _eauActive = new HashSet<Vector3I>();
	private float _tempsEcoulement;
	private const float TICK_EAU = 0.05f;
	private const int MaxEauParTick = 32;
	private static readonly Vector3I[] DirEauHoriz = { new Vector3I(1, 0, 0), new Vector3I(-1, 0, 0), new Vector3I(0, 0, -1), new Vector3I(0, 0, 1) };
	private static readonly Vector3I[] DirVoisins = { new Vector3I(0, 1, 0), new Vector3I(1, 0, 0), new Vector3I(-1, 0, 0), new Vector3I(0, 0, -1), new Vector3I(0, 0, 1) };
	private static readonly Vector3I[] DirReveil = { new Vector3I(0, 1, 0), new Vector3I(0, -1, 0), new Vector3I(1, 0, 0), new Vector3I(-1, 0, 0), new Vector3I(0, 0, 1), new Vector3I(0, 0, -1) };

	private Node _parentPourBlocsChutants;
	private Node _parentPourArbres;
	private Action<Vector2I, List<int>> _onChunkModifie;
	private Action<Vector2I, DonneesChunk> _onEnvoyerChunk;
	private Action<Vector2I, Dictionary<Vector3I, byte>> _onFloreModifie;
	private Action<Vector3I, byte> _onVoxelModifie;
	private Action<Vector2I> _onOrdonnerDestructionChunk;
	private Func<Vector3> _obtenirPositionJoueur;

	private List<Vector2I> _chunksEnAttenteEnvoi = new List<Vector2I>();
	private Queue<ColisChunk> _fileEnvoiReseau = new Queue<ColisChunk>();
	private HashSet<Vector2I> _chunksEnCoursGeneration = new HashSet<Vector2I>();
	private int _chunksEnGenerationActive;
	private static readonly int MaxThreadsGeneration = 4;
	[Export] public int MultiplicateurCharge = 8; // 16 → 8 pour test (génération /2)
	private int LancerMaxTaches => MaxThreadsGeneration * MultiplicateurCharge;
	/// <summary>Budget anti micro-freeze : limite de chunks workers intégrés par frame.</summary>
	private const int MaxIntegrationsWorkersParTick = 2;
	/// <summary>Budget anti micro-freeze : limite de demandes chunks traitées par frame.</summary>
	private const int MaxDemandesChunksParTick = 3;
	/// <summary>Budget anti micro-freeze : limite de chargements disque synchrones par frame.</summary>
	private const int MaxChargesDisqueParTick = 1;
	private const int MaxChunksEnvoiParTick = 8;
	private bool _modificationEnCours;
	private readonly object _verrouGeneration = new object();
	private ConcurrentQueue<(Vector2I coord, Chunk_Serveur chunk, DonneesChunk donnees)> _chunksGeneres = new ConcurrentQueue<(Vector2I, Chunk_Serveur, DonneesChunk)>();

	private struct ColisChunk
	{
		public Vector2I Coord;
		public DonneesChunk Donnees;
	}

	/// <summary>Pierres chargées depuis disque → instanciation goutte-à-goutte (quand chunk dessiné à l'écran).</summary>
	private Queue<(Vector3 pos, int id, int indexCache, int indexChimique)> _filePierresAInstancier = new Queue<(Vector3, int, int, int)>();
	/// <summary>Chambre de stase : roches par coord de chunk. Aucune poussière avant que la croûte (chunk) soit scellée — libérées seulement à l'envoi du chunk.</summary>
	private Dictionary<Vector2I, List<(Vector3 pos, int id, int indexCache, int indexChimique)>> _rochesEnStase = new Dictionary<Vector2I, List<(Vector3, int, int, int)>>();
	/// <summary>Micro-dosage : au plus 3 cailloux par frame pour éviter pics CPU / sync BVH Jolt (AddChild lourd).</summary>
	private const int MaxPierresParFrame = 3;

	/// <summary>Pools de roches par taille (ID 10–14). Limite 50 par catégorie. Plus loin du joueur → formes plus cassées (2e moitié du cache).</summary>
	private Dictionary<int, List<RigidBody3D>> _poolsRochesParTaille = new Dictionary<int, List<RigidBody3D>>();
	private const int TaillePoolParType = 50;
	/// <summary>En deçà de cette distance au niveau d'eau (Y=103) : formes douces. Au-delà (hautes montagnes ou profondeur) : formes plus cassées.</summary>
	private const float SeuilDistanceEauFormesCassées = 25f;

	private float _tempsDepuisVerifDecharge;
	private const float IntervalleEvaluationTectonique = 0.5f;
	/// <summary>Tapis roulant décharge : au plus N chunks sauvegardés/déchargés par frame (évite lag).</summary>
	private const int MaxChunksDechargeParTick = 2;
	private List<Vector2I> _chunksEnAttenteDecharge = new List<Vector2I>();

	public void Initialiser(Node parentPourBlocsChutants, Node parentPourArbres, Action<Vector2I, List<int>> onChunkModifie, Action<Vector2I, DonneesChunk> onEnvoyerChunk = null, Action<Vector2I, Dictionary<Vector3I, byte>> onFloreModifie = null, Action<Vector3I, byte> onVoxelModifie = null, Action<Vector2I> onOrdonnerDestructionChunk = null, Func<Vector3> obtenirPositionJoueur = null)
	{
		_parentPourBlocsChutants = parentPourBlocsChutants;
		_parentPourArbres = parentPourArbres;
		_onChunkModifie = onChunkModifie;
		_onEnvoyerChunk = onEnvoyerChunk;
		_onFloreModifie = onFloreModifie;
		_onVoxelModifie = onVoxelModifie;
		_onOrdonnerDestructionChunk = onOrdonnerDestructionChunk;
		_obtenirPositionJoueur = obtenirPositionJoueur;
		string nom = GameState.Instance?.NomMondeActuel ?? "MonMonde";
		DirAccess.MakeDirRecursiveAbsolute($"user://saves/{nom}/chunks");
		GD.Print($"ZERO-K : Dossier chunks actif = user://saves/{nom}/chunks/ (lecture ET écriture)");
		CreerPoolsRochesParTaille();
	}

	/// <summary>Sauvegarde d'urgence : sauvegarde tous les chunks chargés (robuste même si un drapeau EstModifie a été raté).</summary>
	public void SauvegarderMondeEntier()
	{
		GD.Print("ZERO-K : Lancement du Râle d'Agonie. Sauvegarde des Chunks modifiés...");
		int chunksSauves = 0;
		foreach (var kvp in _chunks)
		{
			Vector2I coord = kvp.Key;
			Chunk_Serveur chunk = kvp.Value;
			chunk.SauvegarderChunkSurDisque();
			SauvegarderFloreChunk(coord, chunk);
			SauvegarderPierresChunk(coord);
			SauvegarderArbresChunk(coord);
			chunksSauves++;
		}
		GD.Print($"ZERO-K : Râle d'Agonie terminé. {chunksSauves} Chunks gravés sur le disque.");
	}

	public override void _ExitTree()
	{
		SauvegarderMondeEntier();
	}

	public override void _Notification(int what)
	{
		// Utilisation stricte de Node.NotificationWMCloseRequest (WM en majuscules)
		if (what == Node.NotificationWMCloseRequest)
		{
			SauvegarderMondeEntier();
			GetTree().Quit();
		}
	}

	/// <summary>Enregistre une demande de chunk. Tri par proximité du joueur (Préemption Absolue).</summary>
	public void EnregistrerDemandeChunk(Vector2I coord)
	{
		if (!_chunksEnAttenteEnvoi.Contains(coord))
			_chunksEnAttenteEnvoi.Add(coord);
	}

	public override void _PhysicsProcess(double delta)
	{
		bool hadModifications = _modificationEnCours;
		_modificationEnCours = false;

		// Récupérer les chunks générés par les workers (Main Thread uniquement)
		// SÉGRÉGATION : ne JAMAIS écraser un chunk chargé depuis le disque avec un chunk procédural.
		int integrationsWorkers = 0;
		while (integrationsWorkers < MaxIntegrationsWorkersParTick && _chunksGeneres.TryDequeue(out var result))
		{
			_chunksEnCoursGeneration.Remove(result.coord);
			_chunksEnGenerationActive--;
			if (_chunks.TryGetValue(result.coord, out var existant) && existant.EstChargeDepuisDisque)
				continue; // Chunk déjà ressuscité du disque — ignorer le résultat procédural.
			if (!_chunks.ContainsKey(result.coord))
				_chunks[result.coord] = result.chunk;
			SpawnerArbresChunk(result.coord, result.chunk);
			// Envoi client uniquement APRÈS stase remplie, sinon LibererRochesChunk trouve une liste vide
			DeclencherEnsemencement(result.coord, result.chunk, TailleChunk, (coord, ch) =>
				_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = coord, Donnees = ch.ObtenirDonneesPourClient() }));
			integrationsWorkers++;
		}

		// Manufacture parallèle : purge des obsolètes puis extraction radiale
		if (!hadModifications)
		{
			Vector3 posObs = _obtenirPositionJoueur?.Invoke() ?? Vector3.Zero;
			float rayonMaxCarrePurge = (RenderDistance + 1) * (RenderDistance + 1);
			_chunksEnAttenteEnvoi.RemoveAll(c =>
			{
				float d2 = DistanceCarreeAuJoueur(c, posObs);
				return d2 > rayonMaxCarrePurge;
			});
			Vector3 posObservation = posObs;
			int demandesTraitees = 0;
			int chargesDisque = 0;
			while (_chunksEnAttenteEnvoi.Count > 0 && _chunksEnGenerationActive < LancerMaxTaches && demandesTraitees < MaxDemandesChunksParTick)
			{
				demandesTraitees++;
				Vector2I chunkCible = ExtraireChunkLePlusProche(_chunksEnAttenteEnvoi, posObservation);

				float distCarree = DistanceCarreeAuJoueur(chunkCible, posObservation);
				float rayonMaxCarre = (RenderDistance + 1) * (RenderDistance + 1);
				if (distCarree > rayonMaxCarre)
					continue;

				if (_chunks.TryGetValue(chunkCible, out var existant))
				{
					_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = chunkCible, Donnees = existant.ObtenirDonneesPourClient() });
					continue;
				}

				Chunk_Serveur chunkActuel = null;

				// BRANCHE 1 : RÉSURRECTION PURE — AUCUN appel de génération. Le chunk part directement au Mesh.
				if (FichierChunkExiste(chunkCible))
				{
					if (chargesDisque >= MaxChargesDisqueParTick)
					{
						// On refile la demande pour la frame suivante afin d'éviter un pic I/O + désérialisation.
						_chunksEnAttenteEnvoi.Add(chunkCible);
						continue;
					}
					chunkActuel = ChargerChunkDepuisDisque(chunkCible);
					chargesDisque++;
					// RÈGLE D'ARCHITECTURE : GenererTerrainDeBase, GenererCoucheSurface, GenererEau, GenererArbres
					// ne sont JAMAIS appelés ici. Le chunk chargé est final.
				}

				// BRANCHE 2 : CRÉATION PROCÉDURALE — TOUTES les passes (terrain, surface, eau) UNIQUEMENT ici.
				if (chunkActuel == null)
				{
					lock (_verrouGeneration)
					{
						if (!_chunksEnCoursGeneration.Add(chunkCible))
							continue;
						_chunksEnGenerationActive++;
					}
					Vector2I coord = chunkCible;
					Task.Run(() =>
					{
						var chunk = CreerChunkServeur(coord);
						// TOUTES les passes : GenererTerrainDeBase, GenererCoucheSurface, GenererEau — encapsulées dans GenererDonneesVoxel.
						chunk.GenererDonneesVoxel();
						var donnees = chunk.ObtenirDonneesPourClient();
						_chunksGeneres.Enqueue((coord, chunk, donnees));
					});
					continue;
				}

				// BRANCHE COMMUNE : Chunk ressuscité. Pierres + Arbres. Spawn quand chunk demandé (visible écran).
				_chunks[chunkCible] = chunkActuel;
				SpawnerArbresChunk(chunkCible, chunkActuel);
				if (!ChargerEtSpawnerPierresChunk(chunkCible))
				{
					// Attendre que l'ensemencement asynchrone finisse AVANT d'envoyer le chunk au réseau
					DeclencherEnsemencement(chunkCible, chunkActuel, TailleChunk, (coord, ch) =>
						_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = coord, Donnees = ch.ObtenirDonneesPourClient() }));
				}
				else
				{
					// Si chargé depuis le disque, on envoie directement
					_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = chunkCible, Donnees = chunkActuel.ObtenirDonneesPourClient() });
				}
			}
		}

		// Tapis roulant : 1 envoi au client par Tick (60 TPS)
		int envoisCeTick = 0;
		while (_fileEnvoiReseau.Count > 0 && envoisCeTick < MaxChunksEnvoiParTick)
		{
			ColisChunk colis = _fileEnvoiReseau.Dequeue();
			_onEnvoyerChunk?.Invoke(colis.Coord, colis.Donnees);
			// Verrou chronologique : la croûte est scellée (chunk envoyé) → on libère les roches de ce chunk vers la file de micro-dosage.
			LibererRochesChunk(colis.Coord);
			envoisCeTick++;
		}

		// Réveil des pierres dormantes : quand joueur dans 2 chunks, le terrain est chargé → on dégèle
		ReveillerPierresDansRayon();

		// Goutte-à-goutte : pierres chargées depuis disque, instanciées quand chunk dessiné à l'écran
		int nPierres = 0;
		while (nPierres < MaxPierresParFrame && _filePierresAInstancier.Count > 0)
		{
			var (pos, id, idx, chim) = _filePierresAInstancier.Dequeue();
			// Plus la roche est loin du niveau d'eau (Y=103), plus elle peut prendre une forme cassée (2e moitié du cache)
			if (idx < 0)
			{
				float distEau = Mathf.Abs(pos.Y - NIVEAU_EAU);
				bool formesCassées = distEau > SeuilDistanceEauFormesCassées;
				idx = formesCassées ? -2 : -1;
			}
			GenererItemPhysique(pos, id, idx, chim);
			nPierres++;
		}

		_tempsEcoulement += (float)delta;
		if (_tempsEcoulement < TICK_EAU) return;
		_tempsEcoulement = 0;

		_tempsDepuisVerifDecharge += (float)delta;
		if (_tempsDepuisVerifDecharge >= IntervalleEvaluationTectonique)
		{
			_tempsDepuisVerifDecharge = 0f;
			EvaluerDechargementChunks();
		}

		// Tapis roulant décharge : N chunks par frame (sauvegarde + décharge progressifs)
		ProcesserDechargeProgressive();

		int n = Math.Min(_fileEau.Count, MaxEauParTick);
		for (int i = 0; i < n; i++)
		{
			Vector3I pos = _fileEau.Dequeue();
			_eauActive.Remove(pos);
			if (!EstVoxelEau(pos)) continue;

			Vector3I posBas = pos + new Vector3I(0, -1, 0);
			if (posBas.Y < 0) { DefinirVoxel(pos, 0); continue; }

			if (EstVoxelAir(posBas))
			{
				DefinirVoxel(posBas, 4);
				DefinirVoxel(pos, 0);
				ActiverEau(posBas);
				ReveillerVoisins(pos);
				continue;
			}

			bool aPression = EstVoxelEau(pos + new Vector3I(0, 1, 0));
			foreach (var d in DirEauHoriz)
			{
				Vector3I pc = pos + d, pcb = pc + new Vector3I(0, -1, 0);
				if (!EstVoxelAir(pc)) continue;
				bool auBord = EstVoxelAir(pcb);
				if (aPression || auBord)
				{
					DefinirVoxel(pc, 4);
					DefinirVoxel(pos, 0);
					ActiverEau(pc);
					ReveillerVoisins(pos);
					break;
				}
			}
		}
	}

	private void ActiverEau(Vector3I pos)
	{
		if (_eauActive.Add(pos)) _fileEau.Enqueue(pos);
	}

	private void ReveillerVoisins(Vector3I pos)
	{
		foreach (var d in DirVoisins)
			if (EstVoxelEau(pos + d)) ActiverEau(pos + d);
	}

	public void ReveillerEauAdjacente(Vector3 pointGlobal)
	{
		int gx = Mathf.FloorToInt(pointGlobal.X), gy = Mathf.FloorToInt(pointGlobal.Y), gz = Mathf.FloorToInt(pointGlobal.Z);
		var basePos = new Vector3I(gx, gy, gz);
		foreach (var d in DirReveil)
			if (EstVoxelEau(basePos + d)) ActiverEau(basePos + d);
	}

	public bool ChunkEstCharge(Vector2I coord) => _chunks.ContainsKey(coord);

	public Chunk_Serveur ObtenirOuCreerChunk(Vector2I coord)
	{
		if (_chunks.TryGetValue(coord, out var c)) return c;

		Chunk_Serveur chunkActuel = null;
		// BRANCHE 1 : RÉSURRECTION — AUCUNE génération.
		if (FichierChunkExiste(coord))
			chunkActuel = ChargerChunkDepuisDisque(coord);
		// BRANCHE 2 : CRÉATION PROCÉDURALE — TOUTES les passes ici.
		if (chunkActuel == null)
		{
			chunkActuel = CreerChunkServeur(coord);
			chunkActuel.GenererDonneesVoxel(); // GenererTerrainDeBase, Surface, Eau — UNIQUEMENT pour chunks ex nihilo.
		}
		_chunks[coord] = chunkActuel;
		return chunkActuel;
	}

	private static bool FichierChunkExiste(Vector2I coord)
	{
		return File.Exists(ProjectSettings.GlobalizePath(DonneesChunk.ObtenirCheminChunk(coord)));
	}

	private static string ObtenirCheminSauvegarde(Vector2I coord) => DonneesChunk.ObtenirCheminChunk(coord);

	/// <summary>Délègue au chunk la sauvegarde binaire. NE sauvegarde QUE si EstModifie.</summary>
	private void SauvegarderChunkSurDisque(Vector2I coord, Chunk_Serveur chunk)
	{
		chunk.SauvegarderChunkSurDisque();
	}

	/// <summary>Résurrection : chargement binaire via BinaryReader. Si fichier absent ou corrompu → régénération procédurale.</summary>
	private Chunk_Serveur ChargerChunkDepuisDisque(Vector2I coord)
	{
		GD.Print($"ZERO-K DIAG : Tentative chargement Chunk {coord}...");
		string cheminGodot = ObtenirCheminSauvegarde(coord);
		string cheminAbsolu = ProjectSettings.GlobalizePath(cheminGodot);
		if (!File.Exists(cheminAbsolu))
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} — fichier inexistant.");
			return null;
		}
		int voxelCount = (TailleChunk + 1) * (HauteurMax + 1) * (TailleChunk + 1);
		int tailleAttendue = voxelCount * 9;
		byte[] donneesVoxels;
		try
		{
			using (var reader = new BinaryReader(File.Open(cheminAbsolu, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)))
			{
				byte version = reader.ReadByte();
				if (version != 1)
				{
					GD.PrintErr($"ZERO-K REJET : Chunk {coord} — version {version} non supportée.");
					return null;
				}
				int tailleLu = reader.ReadInt32();
				if (tailleLu != tailleAttendue)
				{
					GD.PrintErr($"ZERO-K REJET : Chunk {coord} corrompu (taille {tailleLu} ≠ {tailleAttendue}). Régénération forcée.");
					return null;
				}
				donneesVoxels = reader.ReadBytes(tailleLu);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} — erreur lecture : {ex.Message}");
			return null;
		}
		if (donneesVoxels == null || donneesVoxels.Length != tailleAttendue)
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} refusé ! Taille lue : {donneesVoxels?.Length ?? 0} | Attendue : {tailleAttendue}.");
			return null;
		}
		GD.Print($"ZERO-K SUCCÈS : Chunk {coord} chargé depuis le disque ({donneesVoxels.Length} bytes).");
		var chunk = CreerChunkServeur(coord);
		if (!chunk.AppliquerTableauBytes(donneesVoxels))
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} — AppliquerTableauBytes a échoué. Régénération forcée.");
			return null;
		}
		ChargerFloreChunk(coord, chunk);
		ChargerArbresChunk(coord, chunk);
		return chunk;
	}

	/// <summary>Sauvegarde l’inventaire flore du chunk (herbe/buissons retirés ou repoussés).</summary>
	private void SauvegarderFloreChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (chunk == null) return;
		string nom = GameState.Instance?.NomMondeActuel ?? "MonMonde";
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{nom}/chunks/");
		Directory.CreateDirectory(dossier);
		string chemin = Path.Combine(dossier, $"chunk_{coord.X}_{coord.Y}_flore.bin");
		try
		{
			using (var w = new BinaryWriter(File.Open(chemin, FileMode.Create)))
			{
				w.Write(0x5A4B3346); // ZK3F
				w.Write(chunk.InventaireFlore.Count);
				foreach (var kv in chunk.InventaireFlore)
				{
					w.Write(kv.Key.X);
					w.Write(kv.Key.Y);
					w.Write(kv.Key.Z);
					w.Write(kv.Value);
				}
			}
		}
		catch (Exception ex) { GD.PrintErr($"ZERO-K : Erreur sauvegarde flore chunk {coord} : {ex.Message}"); }
	}

	/// <summary>Charge l’inventaire flore; fallback procédural si fichier absent.</summary>
	private void ChargerFloreChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (chunk == null) return;
		string nom = GameState.Instance?.NomMondeActuel ?? "MonMonde";
		string chemin = ProjectSettings.GlobalizePath($"user://saves/{nom}/chunks/chunk_{coord.X}_{coord.Y}_flore.bin");
		if (!File.Exists(chemin))
		{
			chunk.RegenererInventaireFloreDepuisSurface();
			return;
		}
		try
		{
			chunk.InventaireFlore.Clear();
			using (var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)))
			{
				int magic = r.ReadInt32();
				if (magic != 0x5A4B3346)
				{
					chunk.RegenererInventaireFloreDepuisSurface();
					return;
				}
				int count = r.ReadInt32();
				for (int i = 0; i < count; i++)
				{
					var pos = new Vector3I(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
					byte etat = r.ReadByte();
					chunk.InventaireFlore[pos] = etat;
				}
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Erreur chargement flore chunk {coord} : {ex.Message}");
			chunk.RegenererInventaireFloreDepuisSurface();
		}
	}

	private Chunk_Serveur CreerChunkServeur(Vector2I coord)
	{
		var chunk = new Chunk_Serveur(
			coord.X, coord.Y, TailleChunk, HauteurMax, SeedTerrain,
			(pos, mat) => { SpawnBlocChutant(pos, mat); },
			ChunkEstCharge,
			ReveillerEauAdjacente
		);
		chunk.SetOnVoxelModifie((pos, id) => _onVoxelModifie?.Invoke(pos, id));
		chunk.SetOnFlorePurgée((c, inventaire) => _onFloreModifie?.Invoke(c, inventaire));
		return chunk;
	}

	private void SpawnBlocChutant(Vector3 pos, byte mat)
	{
		if (_parentPourBlocsChutants == null) return;
		var matTerrain = MaterielTerrain ?? GD.Load<Material>("res://Manteau_Planetaire.tres");
		var bloc = BlocChutant.Creer(pos, mat, matTerrain);
		_parentPourBlocsChutants.AddChild(bloc);
		// Fibres (fauchage) : léger décalage vers le haut pour éviter d’être coincées dans le sol / la collision.
		Vector3 posPose = mat == 15 ? pos + new Vector3(0f, 0.12f, 0f) : pos;
		bloc.GlobalPosition = posPose;
	}

	/// <summary>Spawn branches et bûches qui tombent au sol quand un arbre est coupé.</summary>
	public void SpawnDebrisArbre(Vector3 baseArbre, int ageEnJours, uint seed)
	{
		if (_parentPourBlocsChutants == null) return;
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(Mathf.Abs(baseArbre.X) * 73856 + Mathf.Abs(baseArbre.Z) * 19349 + seed);
		int nbBranches = Mathf.Clamp(ageEnJours * 2 + (int)(rng.Randf() * 4), 2, 12);
		int nbBuches = Mathf.Clamp(ageEnJours / 2 + (int)(rng.Randf() * 2), 1, 6);
		float offsetRayon = 0.8f + ageEnJours * 0.1f;
		for (int i = 0; i < nbBranches; i++)
		{
			float angle = (float)i / nbBranches * Mathf.Tau + rng.Randf() * 0.5f;
			float r = offsetRayon * (0.5f + rng.Randf() * 0.5f);
			Vector3 pos = baseArbre + new Vector3(Mathf.Cos(angle) * r, 0.5f + rng.Randf() * 0.3f, Mathf.Sin(angle) * r);
			SpawnBlocChutant(pos, BlocChutant.ID_BRANCHE);
		}
		for (int i = 0; i < nbBuches; i++)
		{
			float angle = (float)i / nbBuches * Mathf.Tau + rng.Randf() * 0.8f;
			float r = offsetRayon * (0.3f + rng.Randf() * 0.4f);
			Vector3 pos = baseArbre + new Vector3(Mathf.Cos(angle) * r, 0.6f + rng.Randf() * 0.4f, Mathf.Sin(angle) * r);
			SpawnBlocChutant(pos, BlocChutant.ID_BOIS);
		}
	}

	private const float NIVEAU_EAU = 103f;  // +1 m
	private const float DECALAGE_SPAWN_VERTICAL = 1.2f; // Légèrement au-dessus du terrain à la génération, tombe quand réveillé
	/// <summary>Rayon en chunks : objets dynamiques gelés se réveillent dans cette zone autour du joueur.</summary>
	private const int RAYON_ACTIVATION_PIERRES_CHUNKS = 5;

	/// <summary>Délai de synchronisation : attend 2 frames physiques, puis enfile sur le tapis roulant (ordre spatial logique). Si onStasePrete est fourni (chunk procédural), on enqueue l'envoi client seulement après la stase → évite LibererRochesChunk à vide.</summary>
	private async void DeclencherEnsemencement(Vector2I chunkCoord, Chunk_Serveur chunk, float tailleChunk, Action<Vector2I, Chunk_Serveur> onStasePrete = null)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		var positionsFiltrees = CollecterPositionsEnsemencement(chunkCoord, chunk, tailleChunk);
		var aEnfiler = new List<(Vector3 pos, int id, int indexCache, int indexChimique)>();
		foreach (var p in positionsFiltrees)
			aEnfiler.Add((p.pos, p.idMat, p.idxMorph, p.taille));
		MettreRochesEnStase(chunkCoord, aEnfiler);
		onStasePrete?.Invoke(chunkCoord, chunk);
	}

	/// <summary>Pré-crée les pools par matière rocheuse (40–49).</summary>
	private void CreerPoolsRochesParTaille()
	{
		if (_parentPourBlocsChutants == null) return;
		int n = 0;
		for (int id = ItemPhysique.IdRocheMatiereMin; id <= ItemPhysique.IdRocheMatiereMax; id++)
		{
			_poolsRochesParTaille[id] = new List<RigidBody3D>();
			for (int i = 0; i < TaillePoolParType; i++)
			{
				var rb = CreerNouvelleRoche(id, 0, 2);
				_poolsRochesParTaille[id].Add(rb);
			}
			n++;
		}
		GD.Print($"ZERO-K : Pools roches par matière créés ({n} x {TaillePoolParType}).");
	}

	/// <summary>Collecte positions, ID matière (40–49), morph (-1 = tirage), taille (0–4).</summary>
	private List<(Vector3 pos, int idMat, int idxMorph, int taille)> CollecterPositionsEnsemencement(Vector2I chunkCoord, Chunk_Serveur chunk, float tailleChunk)
	{
		var liste = new List<(Vector3 pos, int idMat, int idxMorph, int taille)>();
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(chunkCoord.X * 73856093 + chunkCoord.Y * 19349663 + SeedTerrain);

		for (float x = 0; x < tailleChunk; x += 3f)
		{
			for (float z = 0; z < tailleChunk; z += 3f)
			{
				if (rng.Randf() > 0.02f) continue;
				int lx = Mathf.Clamp(Mathf.FloorToInt(x), 0, (int)tailleChunk);
				int lz = Mathf.Clamp(Mathf.FloorToInt(z), 0, (int)tailleChunk);
				var (ySurface, idMatiere) = chunk.ObtenirSurfaceEtMateriau(lx, lz);
				if (ySurface < 0) continue;

				Vector3 pointImpact = new Vector3(
					chunkCoord.X * tailleChunk + x + 0.5f,
					ySurface + 0.5f,
					chunkCoord.Y * tailleChunk + z + 0.5f
				);
				Vector3 pointDeSpawnSecurise = pointImpact + new Vector3(0, DECALAGE_SPAWN_VERTICAL, 0);

				if (idMatiere == 3 && pointImpact.Y < NIVEAU_EAU)
				{
					liste.Add((pointDeSpawnSecurise, ItemPhysique.IdRocheMatiereMin + ItemPhysique.IndexChimiqueSilex, -1, 1));
					continue;
				}

				int tailleSpawn = 0;
				float proba = rng.Randf();
				if (idMatiere == 1 || idMatiere == 3) tailleSpawn = 1;
				else if (idMatiere == 7 || idMatiere == 8) tailleSpawn = (proba > 0.4f) ? 1 : 2;
				else if (idMatiere == 5 || idMatiere == 6) tailleSpawn = (proba > 0.5f) ? 1 : 2;
				else if (idMatiere == 2)
				{
					if (proba < 0.40f) tailleSpawn = 1;
					else if (proba < 0.70f) tailleSpawn = 2;
					else if (proba < 0.90f) tailleSpawn = 3;
					else tailleSpawn = 4;
				}

				if (tailleSpawn != 0)
				{
					int chimIdx = rng.RandiRange(0, ItemPhysique.TableGeologique.Length - 1);
					liste.Add((pointDeSpawnSecurise, ItemPhysique.IdRocheMatiereMin + chimIdx, -1, tailleSpawn));
				}
			}
		}
		return liste;
	}

	/// <summary>Chambre de stase : les roches attendent leur sol. Pas de spawn tant que le chunk n'est pas scellé (envoyé).</summary>
	private void MettreRochesEnStase(Vector2I coordChunk, List<(Vector3 pos, int id, int indexCache, int indexChimique)> pierres)
	{
		if (pierres.Count == 0) return;
		pierres.Sort((a, b) =>
		{
			int cmpX = a.pos.X.CompareTo(b.pos.X);
			if (cmpX != 0) return cmpX;
			int cmpZ = a.pos.Z.CompareTo(b.pos.Z);
			if (cmpZ != 0) return cmpZ;
			return a.pos.Y.CompareTo(b.pos.Y);
		});
		_rochesEnStase[coordChunk] = pierres;
	}

	/// <summary>Signal de fondation : chunk scellé (envoyé au client) → on transfère ses roches vers la file de micro-dosage (3 par frame).</summary>
	private void LibererRochesChunk(Vector2I coordChunk)
	{
		if (!_rochesEnStase.TryGetValue(coordChunk, out var liste)) return;
		foreach (var p in liste)
			_filePierresAInstancier.Enqueue(p);
		_rochesEnStase.Remove(coordChunk);
	}

	/// <summary>Enfile cailloux et silex sur le tapis roulant en ordre spatial logique (X, Z, Y) : terrain cohérent.</summary>
	private void EnfilerPierresSurTapisRoulant(List<(Vector3 pos, int id, int indexCache, int indexChimique)> pierres)
	{
		if (pierres.Count == 0) return;
		pierres.Sort((a, b) =>
		{
			int cmpX = a.pos.X.CompareTo(b.pos.X);
			if (cmpX != 0) return cmpX;
			int cmpZ = a.pos.Z.CompareTo(b.pos.Z);
			if (cmpZ != 0) return cmpZ;
			return a.pos.Y.CompareTo(b.pos.Y);
		});
		foreach (var p in pierres)
			_filePierresAInstancier.Enqueue((p.pos, p.id, p.indexCache, p.indexChimique));
	}

	/// <summary>Roches matière 40–49 : <paramref name="indexCache"/> = morph (-1/-2 = tirage), <paramref name="indexChimique"/> = <see cref="ItemPhysique.IndexTailleRoche"/> (0–4).</summary>
	private void GenererItemPhysique(Vector3 position, int idObjet, int indexCache = -1, int indexChimique = -1)
	{
		if (_parentPourBlocsChutants == null) return;
		ItemPhysique rb = null;
		if (_poolsRochesParTaille.TryGetValue(idObjet, out var pool) && pool.Count > 0)
		{
			rb = pool[pool.Count - 1] as ItemPhysique;
			pool.RemoveAt(pool.Count - 1);
			if (rb != null)
			{
				rb.ID_Objet = idObjet;
				if (indexCache == -2)
					rb.IndexCacheMemoire = GD.RandRange(2, 3);
				else if (indexCache < 0)
					rb.IndexCacheMemoire = GD.RandRange(0, 3);
				else
					rb.IndexCacheMemoire = Mathf.Clamp(indexCache, 0, 3);
				rb.IndexTailleRoche = indexChimique >= 0 ? Mathf.Clamp(indexChimique, 0, 4) : 2;
				rb.IndexChimique = ItemPhysique.IndexChimiqueDepuisIdRoche(idObjet);
				rb.ReappliquerApparence();
				rb.Freeze = true; // Stase : ReveillerPierresDansRayon dégèle à 2 chunks (terrain solide)
			}
		}
		else
			rb = CreerNouvelleRoche(idObjet, indexCache, indexChimique);
		try
		{
			_parentPourBlocsChutants.AddChild(rb);
			rb.GlobalPosition = position;
			rb.Freeze = true; // Dormance : gravité seulement à 2 chunks du joueur (évite chute dans le vide)
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K CRASH ÉVITÉ : Objet physique échoué à l'instanciation. {ex.Message}");
			rb?.QueueFree();
		}
	}

	/// <summary>Crée une roche neuve (ItemPhysique = RigidBody3D racine). N'est pas ajoutée au parent.</summary>
	private ItemPhysique CreerNouvelleRoche(int idObjet, int indexCache, int indexTailleOuChim)
	{
		int morph;
		if (indexCache == -2)
			morph = GD.RandRange(2, 3);
		else if (indexCache < 0)
			morph = GD.RandRange(0, 3);
		else
			morph = Mathf.Clamp(indexCache, 0, 3);
		int taille = indexTailleOuChim >= 0 ? Mathf.Clamp(indexTailleOuChim, 0, 4) : 2;
		float rayon = ItemPhysique.RayonBaseRochesJoueur(taille);
		var item = new ItemPhysique
		{
			ID_Objet = idObjet,
			IndexCacheMemoire = morph,
			IndexTailleRoche = taille,
			IndexChimique = ItemPhysique.IndexChimiqueDepuisIdRoche(idObjet),
			Name = "ItemPhysique",
			// Morphologie appliquée sur le MeshInstance3D dans ItemPhysique._Ready (Jolt : pas d’échelle non uniforme sur RigidBody3D).
			Scale = Vector3.One
		};
		item.Mass = 1.0f;
		// Friction / amortissement : ItemPhysique._Ready → AppliquerPhysiqueRochePortee (évite conflit avec matériau 0,6).
		item.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = rayon, Height = rayon * 2f } });
		item.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = rayon } });
		return item;
	}

	/// <summary>Rayon en unités : pierres gelées se réveillent quand joueur entre (2 chunks = terrain chargé).</summary>
	private float RayonActivationPierres => RAYON_ACTIVATION_PIERRES_CHUNKS * TailleChunk;

	/// <summary>Réveille les objets dynamiques dans le rayon, endort les lointains (charge CPU réduite côté serveur).</summary>
	private void ReveillerPierresDansRayon()
	{
		if (_parentPourBlocsChutants == null || _obtenirPositionJoueur == null) return;
		Vector3 posJoueur = _obtenirPositionJoueur();
		float rayonCarre = RayonActivationPierres * RayonActivationPierres;
		foreach (Node child in _parentPourBlocsChutants.GetChildren())
		{
			if (child is not RigidBody3D rb) continue;
			int id = 0;
			if (rb is ItemPhysique item)
				id = item.ID_Objet;
			else if (rb.HasMeta("ID_Matiere"))
				id = rb.GetMeta("ID_Matiere").AsInt32();
			if (!TryGetPositionMonde(rb, out Vector3 posRb)) continue;
			float distCarre = posRb.DistanceSquaredTo(posJoueur);
			if (distCarre <= rayonCarre)
			{
				if (id != 200)
				{
					rb.Freeze = false; // Réveiller : gravité + collisions
					rb.Sleeping = false;
				}
			}
			else
			{
				rb.LinearVelocity = Vector3.Zero;
				rb.AngularVelocity = Vector3.Zero;
				rb.Sleeping = true;
				if (id != 200)
					rb.Freeze = true;
			}
		}
	}

	/// <summary>Évite l’erreur Godot <c>!is_inside_tree()</c> sur GlobalPosition (ex. sauvegarde pendant <c>_ExitTree</c>).</summary>
	private static bool TryGetPositionMonde(Node3D node, out Vector3 worldPos)
	{
		worldPos = default;
		if (node == null) return false;
		if (node.IsInsideTree())
		{
			worldPos = node.GlobalPosition;
			return true;
		}
		if (node.GetParent() is Node3D parent && parent.IsInsideTree())
		{
			worldPos = parent.GlobalTransform * node.Position;
			return true;
		}
		return false;
	}

	/// <summary>Sauvegarde les roches matière (40–49) : morph dans index, taille dans chimique (octet).</summary>
	private void SauvegarderPierresChunk(Vector2I coord)
	{
		if (_parentPourBlocsChutants == null) return;
		float xMin = coord.X * TailleChunk;
		float xMax = (coord.X + 1) * TailleChunk;
		float zMin = coord.Y * TailleChunk;
		float zMax = (coord.Y + 1) * TailleChunk;
		var pierres = new List<(Vector3 pos, int id, int index, int chimique)>();
		foreach (Node child in _parentPourBlocsChutants.GetChildren())
		{
			var item = child as ItemPhysique ?? child.GetNodeOrNull<ItemPhysique>("ItemPhysique");
			if (item == null) continue;
			if (item.EstEclatFracture) continue; // Éclats de fracture : pas sauvegardés (créés à l'instant, supprimés quand chunk déchargé).
			int id = item.ID_Objet;
			if (!ItemPhysique.EstIdRocheMatiere(id)) continue;
			if (child is not Node3D n3 || !TryGetPositionMonde(n3, out Vector3 pos)) continue;
			if (pos.X >= xMin && pos.X < xMax && pos.Z >= zMin && pos.Z < zMax)
				pierres.Add((pos, id, Mathf.Clamp(item.IndexCacheMemoire, 0, 3), Mathf.Clamp(item.IndexTailleRoche, 0, 4)));
		}
		string nom = GameState.Instance?.NomMondeActuel ?? "MonMonde";
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{nom}/chunks/");
		Directory.CreateDirectory(dossier);
		string chemin = Path.Combine(dossier, $"chunk_{coord.X}_{coord.Y}_items.bin");
		try
		{
			using (var w = new BinaryWriter(File.Open(chemin, FileMode.Create)))
			{
				w.Write(0x5A4B324A); // Magic v3 = IndexCacheMemoire + IndexChimique
				w.Write(pierres.Count);
				foreach (var (pos, id, index, chimique) in pierres)
				{
					w.Write(pos.X); w.Write(pos.Y); w.Write(pos.Z);
					w.Write((byte)id);
					w.Write((byte)index);
					w.Write((byte)chimique);
				}
			}
		}
		catch (Exception ex) { GD.PrintErr($"ZERO-K : Erreur sauvegarde pierres chunk {coord} : {ex.Message}"); }
	}

	/// <summary>Charge et enfile les pierres sur le tapis roulant (ordre spatial logique X,Z,Y). v1/v2/v3.</summary>
	private bool ChargerEtSpawnerPierresChunk(Vector2I coord)
	{
		if (_parentPourBlocsChutants == null) return false;
		string nom = GameState.Instance?.NomMondeActuel ?? "MonMonde";
		string chemin = ProjectSettings.GlobalizePath($"user://saves/{nom}/chunks/chunk_{coord.X}_{coord.Y}_items.bin");
		if (!File.Exists(chemin)) return false;
		try
		{
			var pierres = new List<(Vector3 pos, int id, int indexCache, int indexChimique)>();
			using (var stream = File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
			using (var r = new BinaryReader(stream))
			{
				int magicOrCount = r.ReadInt32();
				bool formatV3 = (magicOrCount == 0x5A4B324A);
				bool formatV2 = (magicOrCount == 0x5A4B3249) || formatV3;
				int count = formatV2 || formatV3 ? r.ReadInt32() : magicOrCount;
				for (int i = 0; i < count; i++)
				{
					float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle();
					int id = r.ReadByte();
					int indexCache = formatV2 || formatV3 ? r.ReadByte() : -1;
					int indexChimique = formatV3 ? r.ReadByte() : -1;
					if (id >= 10 && id <= 14)
					{
						int chim = Mathf.Clamp(indexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
						if (id == 11) chim = ItemPhysique.IndexChimiqueSilex;
						int tailleMigr = id switch { 10 => 1, 11 => 1, 12 => 2, 13 => 3, 14 => 4, _ => 2 };
						id = ItemPhysique.IdRocheMatiereMin + chim;
						indexChimique = tailleMigr;
						if (indexCache >= 0) indexCache %= 4;
					}
					if (ItemPhysique.EstIdRocheMatiere(id))
						pierres.Add((new Vector3(x, y, z), id, indexCache, indexChimique));
				}
			}
			MettreRochesEnStase(coord, pierres);
			return true;
		}
		catch (Exception ex) { GD.PrintErr($"ZERO-K : Erreur chargement pierres chunk {coord} : {ex.Message}"); return false; }
	}

	/// <summary>Sauvegarde les ArbreVivant dans ce chunk. Fichier chunk_X_Y_arbres.bin.</summary>
	private void SauvegarderArbresChunk(Vector2I coord)
	{
		if (_parentPourArbres == null) return;
		float xMin = coord.X * TailleChunk;
		float xMax = (coord.X + 1) * TailleChunk;
		float zMin = coord.Y * TailleChunk;
		float zMax = (coord.Y + 1) * TailleChunk;
		var arbres = new List<(Vector3 pos, int age)>();
		foreach (Node n in _parentPourArbres.GetChildren())
		{
			if (n is not ArbreVivant arbre) continue;
			if (!TryGetPositionMonde(arbre, out Vector3 p)) continue;
			if (p.X >= xMin && p.X < xMax && p.Z >= zMin && p.Z < zMax)
				arbres.Add((p, arbre.AgeEnJours));
		}
		string nom = GameState.Instance?.NomMondeActuel ?? "MonMonde";
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{nom}/chunks/");
		Directory.CreateDirectory(dossier);
		string chemin = Path.Combine(dossier, $"chunk_{coord.X}_{coord.Y}_arbres.bin");
		try
		{
			using (var w = new BinaryWriter(File.Open(chemin, FileMode.Create)))
			{
				w.Write(0x5A4B3251); // MAGIC V2 = sauvegarde temps
				int jourActuel = GameState.Instance != null ? GameState.Instance.JourAbsolu : 0;
				w.Write(jourActuel);
				w.Write(arbres.Count);
				foreach (var (pos, age) in arbres)
				{
					w.Write((int)pos.X); w.Write((int)pos.Y); w.Write((int)pos.Z);
					w.Write(age); // Âge brut (int, croissance infinie)
				}
			}
		}
		catch (Exception ex) { GD.PrintErr($"ZERO-K : Erreur sauvegarde arbres chunk {coord} : {ex.Message}"); }
	}

	/// <summary>Spawn les ArbreVivant 3D pour ce chunk (procédural ou chargé).</summary>
	private void SpawnerArbresChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (_parentPourArbres == null || chunk.InventaireArbres.Count == 0) return;
		foreach (var kv in chunk.InventaireArbres)
		{
			// Base collée au sol (Y - 0.5 pour éviter troncs flottants)
			Vector3 pos = new Vector3(kv.Key.X + 0.5f, kv.Key.Y - 0.5f, kv.Key.Z + 0.5f);
			int age = Mathf.Max(1, kv.Value.Stage + 1);
			var arbre = new ArbreVivant
			{
				AgeEnJours = age,
				ResistanceActuelle = ArbreVivant.ResistanceMaxPourAge(age),
				Seed = kv.Value.Seed
			};
			_parentPourArbres.AddChild(arbre);
			arbre.GlobalPosition = pos;
		}
	}

	/// <summary>Charge et spawn les arbres depuis disque. Rattrape la croissance du temps passé hors-ligne.</summary>
	private void ChargerArbresChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (_parentPourArbres == null) return;
		string nom = GameState.Instance?.NomMondeActuel ?? "MonMonde";
		string chemin = ProjectSettings.GlobalizePath($"user://saves/{nom}/chunks/chunk_{coord.X}_{coord.Y}_arbres.bin");
		if (!File.Exists(chemin)) return;
		try
		{
			using (var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)))
			{
				int magic = r.ReadInt32();
				int jourDeSauvegarde = 0;
				if (magic == 0x5A4B3251) // V2 avec temps
					jourDeSauvegarde = r.ReadInt32();
				else if (magic != 0x5A4B3250)
					return; // Format inconnu

				int jourActuel = GameState.Instance != null ? GameState.Instance.JourAbsolu : 0;
				int joursEcoules = Mathf.Max(0, jourActuel - jourDeSauvegarde);
				int count = r.ReadInt32();

				for (int i = 0; i < count; i++)
				{
					int gx = r.ReadInt32(), gy = r.ReadInt32(), gz = r.ReadInt32();
					int ageSauvegarde;
					if (magic == 0x5A4B3251)
						ageSauvegarde = r.ReadInt32();
					else
					{
						byte stage = r.ReadByte();
						r.ReadUInt32(); // seed (legacy)
						ageSauvegarde = stage + 1; // Ancien format Stage 0-4 → age 1-5
					}

					Vector3 pos = new Vector3(gx + 0.5f, gy - 0.5f, gz + 0.5f);
					uint seedArbre = (uint)((gx * 73856093) ^ (gz * 19349663));
					int ageCharge = Mathf.Max(1, ageSauvegarde);
					var arbre = new ArbreVivant
					{
						AgeEnJours = ageCharge,
						ResistanceActuelle = ArbreVivant.ResistanceMaxPourAge(ageCharge),
						Seed = seedArbre
					};
					_parentPourArbres.AddChild(arbre);
					arbre.GlobalPosition = pos;
					if (joursEcoules > 0)
						arbre.RattraperCroissance(joursEcoules, pos);
				}
			}
		}
		catch (Exception ex) { GD.PrintErr($"ZERO-K : Erreur chargement arbres chunk {coord} : {ex.Message}"); }
	}

	/// <summary>Retire du monde les ArbreVivant dont la position est dans le chunk (décharge).</summary>
	private void RetirerArbresChunk(Vector2I coord)
	{
		if (_parentPourArbres == null) return;
		float xMin = coord.X * TailleChunk;
		float xMax = (coord.X + 1) * TailleChunk;
		float zMin = coord.Y * TailleChunk;
		float zMax = (coord.Y + 1) * TailleChunk;
		var aRetirer = new List<Node>();
		foreach (Node n in _parentPourArbres.GetChildren())
		{
			if (n is not ArbreVivant) continue;
			if (n is not Node3D n3 || !TryGetPositionMonde(n3, out Vector3 p)) continue;
			if (p.X >= xMin && p.X < xMax && p.Z >= zMin && p.Z < zMax)
				aRetirer.Add(n);
		}
		foreach (var n in aRetirer)
		{
			_parentPourArbres.RemoveChild(n);
			n.QueueFree();
		}
	}

	/// <summary>Retire du monde les pierres/silex dont la position est dans le chunk ; remet dans le pool de la taille si possible.</summary>
	private void RetirerPierresChunk(Vector2I coord)
	{
		if (_parentPourBlocsChutants == null) return;
		float xMin = coord.X * TailleChunk;
		float xMax = (coord.X + 1) * TailleChunk;
		float zMin = coord.Y * TailleChunk;
		float zMax = (coord.Y + 1) * TailleChunk;
		var aRetirer = new List<Node>();
		foreach (Node child in _parentPourBlocsChutants.GetChildren())
		{
			var item = child as ItemPhysique ?? child.GetNodeOrNull<ItemPhysique>("ItemPhysique");
			if (item == null) continue;
			if (!ItemPhysique.EstIdRocheMatiere(item.ID_Objet)) continue;
			if (child is not Node3D n3p || !TryGetPositionMonde(n3p, out Vector3 pos)) continue;
			if (pos.X >= xMin && pos.X < xMax && pos.Z >= zMin && pos.Z < zMax)
				aRetirer.Add(child);
		}
		foreach (var n in aRetirer)
		{
			var item = n as ItemPhysique ?? n.GetNodeOrNull<ItemPhysique>("ItemPhysique");
			int id = item?.ID_Objet ?? 0;
			_parentPourBlocsChutants.RemoveChild(n);
			// Les éclats de fracture sont créés à l'instant, jamais remis au pool (sinon roches infinies).
			if (item != null && item.EstEclatFracture)
			{
				n.QueueFree();
				continue;
			}
			if (n is RigidBody3D rb && ItemPhysique.EstIdRocheMatiere(id) && _poolsRochesParTaille.TryGetValue(id, out var pool) && pool.Count < TaillePoolParType)
			{
				rb.Freeze = true; // En pool = figé pour réutilisation ; dégelé à la sortie (GenererItemPhysique)
				pool.Add(rb);
			}
			else
				n.QueueFree();
		}
	}

	/// <summary>Croissance des arbres 3D : VieillirUnJour sur chaque ArbreVivant. Appelé au changement de jour (minuit).</summary>
	public void FairePousserArbresDuJour()
	{
		if (_parentPourArbres == null) return;
		foreach (Node n in _parentPourArbres.GetChildren())
		{
			if (n is ArbreVivant arbre)
				arbre.VieillirUnJour();
		}
		GD.Print("ZERO-K : Croissance des arbres du jour appliquée.");
	}

	public void AppliquerDestructionGlobale(Vector3 pointImpact, float rayon, float forceDegats = 5.0f, int peerDemandeur = -1)
	{
		_modificationEnCours = true;
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;

		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				Vector2I coord = new Vector2I(cx, cz);
				var chunk = ObtenirOuCreerChunk(coord);
				chunk.DetruireVoxel(pointImpact, rayon, forceDegats);
			}
	}

	public void AppliquerFauchageGlobal(Vector3 pointImpact, float rayon)
	{
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;

		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
				chunk.FaucherFlore(pointImpact, rayon);
			}
	}

	public void AppliquerCreationGlobale(Vector3 pointImpact, Vector3 normale, float rayon, int idMatiere = 1)
	{
		_modificationEnCours = true;
		Vector3 pointCible = pointImpact + (normale * 0.1f); // Réduit pour éviter les blocs flottants
		byte matiere = (byte)Mathf.Clamp(idMatiere, 0, 255);
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X - rayon, pointCible.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X + rayon, pointCible.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X, pointCible.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X, pointCible.Z + rayon, TailleChunk).Y;

		for (int cx = cxMin; cx <= cxMax; cx++)
		{
			for (int cz = czMin; cz <= czMax; cz++)
			{
				Vector2I coord = new Vector2I(cx, cz);
				var chunk = ObtenirOuCreerChunk(coord);
				chunk.CreerMatiere(pointCible, rayon, matiere);
			}
		}
	}

	public DonneesChunk ObtenirDonneesChunkPourClient(Vector2I coord)
	{
		var chunk = ObtenirOuCreerChunk(coord);
		return chunk.ObtenirDonneesPourClient();
	}

	private (Chunk_Serveur chunk, Vector3I local)? ObtenirChunkEtLocal(Vector3I pos)
	{
		if (pos.Y < 0 || pos.Y > HauteurMax) return null;
		Gestionnaire_Monde.WorldToChunkAndLocal(pos.X, pos.Z, TailleChunk, out Vector2I c, out int lx, out int lz);
		Vector2I coord = new Vector2I(c.X, c.Y);
		if (!_chunks.TryGetValue(coord, out var ch)) return null;
		if (lx < 0 || lx > TailleChunk || lz < 0 || lz > TailleChunk) return null;
		return (ch, new Vector3I(lx, pos.Y, lz));
	}

	private bool EstVoxelEau(Vector3I pos)
	{
		var r = ObtenirChunkEtLocal(pos);
		return r.HasValue && r.Value.chunk.EstVoxelEau(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
	}

	/// <summary>Vérifie un petit voisinage 3³ : bûche/bâton peuvent chevaucher plusieurs voxels.</summary>
	public bool EstPointDansEau(Vector3 positionGlobale)
	{
		int gx = Mathf.FloorToInt(positionGlobale.X);
		int gy = Mathf.FloorToInt(positionGlobale.Y);
		int gz = Mathf.FloorToInt(positionGlobale.Z);
		for (int dx = -1; dx <= 1; dx++)
			for (int dy = -1; dy <= 1; dy++)
				for (int dz = -1; dz <= 1; dz++)
					if (EstVoxelEau(new Vector3I(gx + dx, gy + dy, gz + dz)))
						return true;
		return false;
	}

	private bool EstVoxelAir(Vector3I pos)
	{
		var r = ObtenirChunkEtLocal(pos);
		return r.HasValue && r.Value.chunk.EstVoxelAir(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
	}

	private void DefinirVoxel(Vector3I pos, byte id)
	{
		var r = ObtenirChunkEtLocal(pos);
		if (!r.HasValue) return;
		if (id == 4) r.Value.chunk.DefinirVoxelEau(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
		else if (id == 0) r.Value.chunk.DefinirVoxelAir(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
		_onVoxelModifie?.Invoke(pos, id);
	}

	/// <summary>Réplique la modification sur le padding des chunks voisins (évite déchirures quand chunk envoyé plus tard).</summary>
	public void RepliquerPaddingVoisins(Vector3I posGlobal, byte id)
	{
		Gestionnaire_Monde.WorldToChunkAndLocal(posGlobal.X, posGlobal.Z, TailleChunk, out Vector2I c, out int localX, out int localZ);
		int cx = c.X;
		int cz = c.Y;

		if (localX == 0 && _chunks.TryGetValue(new Vector2I(cx - 1, cz), out var vx))
			vx.SetVoxelLocal(TailleChunk, posGlobal.Y, localZ, id);
		if (localX == TailleChunk - 1 && _chunks.TryGetValue(new Vector2I(cx + 1, cz), out var vxp))
			vxp.SetVoxelLocal(0, posGlobal.Y, localZ, id);
		if (localZ == 0 && _chunks.TryGetValue(new Vector2I(cx, cz - 1), out var vz))
			vz.SetVoxelLocal(localX, posGlobal.Y, TailleChunk, id);
		if (localZ == TailleChunk - 1 && _chunks.TryGetValue(new Vector2I(cx, cz + 1), out var vzp))
			vzp.SetVoxelLocal(localX, posGlobal.Y, 0, id);
		if (localX == 0 && localZ == 0 && _chunks.TryGetValue(new Vector2I(cx - 1, cz - 1), out var vxz))
			vxz.SetVoxelLocal(TailleChunk, posGlobal.Y, TailleChunk, id);
		if (localX == TailleChunk - 1 && localZ == 0 && _chunks.TryGetValue(new Vector2I(cx + 1, cz - 1), out var vxpz))
			vxpz.SetVoxelLocal(0, posGlobal.Y, TailleChunk, id);
		if (localX == 0 && localZ == TailleChunk - 1 && _chunks.TryGetValue(new Vector2I(cx - 1, cz + 1), out var vxzp))
			vxzp.SetVoxelLocal(TailleChunk, posGlobal.Y, 0, id);
		if (localX == TailleChunk - 1 && localZ == TailleChunk - 1 && _chunks.TryGetValue(new Vector2I(cx + 1, cz + 1), out var vxpzp))
			vxpzp.SetVoxelLocal(0, posGlobal.Y, 0, id);
	}

	private void DemanderMiseAJourMesh(Vector3I pos)
	{
		var r = ObtenirChunkEtLocal(pos);
		if (!r.HasValue) return;
		Gestionnaire_Monde.WorldToChunkAndLocal(pos.X, pos.Z, TailleChunk, out Vector2I c, out int lx, out int lz);
		int cx = c.X;
		int cz = c.Y;
		int sec = Mathf.Clamp(Mathf.FloorToInt(pos.Y / 16f), 0, 44);  // 45 sections (0-44) pour HauteurMax 720
		_onChunkModifie?.Invoke(new Vector2I(cx, cz), new List<int> { sec });
		if (lx == 0) _onChunkModifie?.Invoke(new Vector2I(cx - 1, cz), new List<int> { sec });
		if (lx == TailleChunk - 1) _onChunkModifie?.Invoke(new Vector2I(cx + 1, cz), new List<int> { sec });
		if (lz == 0) _onChunkModifie?.Invoke(new Vector2I(cx, cz - 1), new List<int> { sec });
		if (lz == TailleChunk - 1) _onChunkModifie?.Invoke(new Vector2I(cx, cz + 1), new List<int> { sec });
	}

	public static int ObtenirHauteurTerrainMonde(int worldX, int worldZ, int seed)
	{
		return Generateur_Voxel.ObtenirHauteurTerrainMonde(worldX, worldZ, seed);
	}

	/// <summary>Oracle géologique : sonde les 8 coins du cube Marching Cubes pour isoler la matière solide (évite fallback gazon quand on lit l'air).</summary>
	public int ObtenirMatiereExacte(Vector3 positionGlobale)
	{
		int gx = Mathf.FloorToInt(positionGlobale.X);
		int gy = Mathf.FloorToInt(positionGlobale.Y);
		int gz = Mathf.FloorToInt(positionGlobale.Z);

		int matiereTrouvee = 1;
		bool trouveSolide = false;

		for (int dx = 0; dx <= 1; dx++)
		{
			for (int dy = 0; dy <= 1; dy++)
			{
				for (int dz = 0; dz <= 1; dz++)
				{
					var r = ObtenirChunkEtLocal(new Vector3I(gx + dx, gy + dy, gz + dz));
					if (r.HasValue && r.Value.chunk.EstVoxelSolide(r.Value.local.X, r.Value.local.Y, r.Value.local.Z))
					{
						byte mat = r.Value.chunk.ObtenirMatiereAtLocal(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
						if (mat > 0)
						{
							matiereTrouvee = mat;
							trouveSolide = true;
							if (mat != 1) return mat;
						}
					}
				}
			}
		}
		return trouveSolide ? matiereTrouvee : 1;
	}

	private float DistanceCarreeAuJoueur(Vector2I chunk, Vector3 posObservation)
	{
		Vector2I obs = Gestionnaire_Monde.WorldToChunkCoord(posObservation, TailleChunk);
		int dx = chunk.X - obs.X, dz = chunk.Y - obs.Y;
		return dx * dx + dz * dz;
	}

	/// <summary>Extraction radiale : le chunk à distance minimale de l'épicentre. DistanceSquaredTo évite la racine carrée.</summary>
	private Vector2I ExtraireChunkLePlusProche(List<Vector2I> liste, Vector3 positionObservation)
	{
		if (liste.Count == 0) return Vector2I.Zero;
		Vector2 posObsV2 = new Vector2(positionObservation.X / (float)TailleChunk, positionObservation.Z / (float)TailleChunk);
		Vector2I chunkCible = liste[0];
		float distanceMin = float.MaxValue;
		int indexASupprimer = 0;
		for (int i = 0; i < liste.Count; i++)
		{
			Vector2 posChunk = new Vector2(liste[i].X, liste[i].Y);
			float dist = posObsV2.DistanceSquaredTo(posChunk);
			if (dist < distanceMin)
			{
				distanceMin = dist;
				chunkCible = liste[i];
				indexASupprimer = i;
			}
		}
		liste.RemoveAt(indexASupprimer);
		return chunkCible;
	}

	private void EvaluerDechargementChunks()
	{
		if (_obtenirPositionJoueur == null || _onOrdonnerDestructionChunk == null) return;
		Vector3 posJoueur = _obtenirPositionJoueur();
		Vector2I cj = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
		int cjX = cj.X;
		int cjZ = cj.Y;

		var aDecharger = new List<Vector2I>();
		foreach (var kv in _chunks)
		{
			int dx = Mathf.Abs(kv.Key.X - cjX);
			int dz = Mathf.Abs(kv.Key.Y - cjZ);
			if (dx > RenderDistance || dz > RenderDistance)
				aDecharger.Add(kv.Key);
		}
		// Enfiler sur le tapis roulant : le déchargement sera fait progressivement par ProcesserDechargeProgressive
		_chunksEnAttenteDecharge = aDecharger;
	}

	/// <summary>Traite au plus MaxChunksDechargeParTick chunks : sauvegarde (voxels + pierres) puis décharge (retrait pierres, Remove chunk, notif client).</summary>
	private void ProcesserDechargeProgressive()
	{
		if (_chunksEnAttenteDecharge.Count == 0 || _onOrdonnerDestructionChunk == null) return;
		int traites = 0;
		while (traites < MaxChunksDechargeParTick && _chunksEnAttenteDecharge.Count > 0)
		{
			Vector2I coord = _chunksEnAttenteDecharge[0];
			_chunksEnAttenteDecharge.RemoveAt(0);
			if (_chunks.TryGetValue(coord, out var chunk))
			{
				chunk.SauvegarderChunkSurDisque();
				SauvegarderFloreChunk(coord, chunk);
				SauvegarderPierresChunk(coord);
				SauvegarderArbresChunk(coord);
				RetirerPierresChunk(coord);
				RetirerArbresChunk(coord);
				_chunks.Remove(coord);
				_onOrdonnerDestructionChunk(coord);
				traites++;
			}
		}
	}
}