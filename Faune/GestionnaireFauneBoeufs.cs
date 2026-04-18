using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class GestionnaireFauneBoeufs : Node3D
{
	// Legacy: scene unique (utilisee si SceneVache est vide).
	[Export] public PackedScene SceneBoeuf;
	[Export] public PackedScene SceneVache;
	[Export] public PackedScene SceneTaureau;
	[Export(PropertyHint.Range, "0,1,0.01")] public float RatioMales = 0.45f;
	[Export] public int IdMatierePlaine = 1;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ChanceSpawnPlaine = 0.38f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ChanceSpawnHorsPlaine = 0.05f;
	[Export] public int NombreBoeufs = 6;
	[Export] public int TailleTroupeauMin = 5;
	[Export] public int TailleTroupeauMax = 6;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ChanceTroupeauParChunkID1 = 0.14f;
	[Export] public int RayonEvaluationChunksAutourJoueur = 8;
	[Export] public float RayonSpawnMin = 24f;
	[Export] public float RayonSpawnMax = 120f;
	[Export] public int EssaisParBoeuf = 52;
	[Export] public float DistanceMinEntreBoeufs = 10f;
	[Export] public float HauteurRaycast = 420f;
	[Export] public bool AutoriserSpawnPartout = true;
	[Export] public float IntervalleVerificationSpawn = 1.25f;
	[Export] public bool ExigerChunksVoisinsChargesPourSpawn = true;
	[Export] public int MargeSecuriteChunksSpawn = 2;
	[Export] public bool ValiderSolAutourDuPointSpawn = true;
	[Export] public float RayonValidationSolSpawn = 1.2f;
	[Export] public bool GarantirPremierTroupeau = true;
	[Export(PropertyHint.Range, "8,256,1")] public int BudgetChunksEvaluesParCycle = 64;
	[ExportGroup("Diagnostic performance")]
	[Export] public bool ActiverProfilagePerfFaune = false;
	[Export(PropertyHint.Range, "0.2,10,0.1")] public float IntervalleLogProfilageFauneSec = 2.0f;

	private Gestionnaire_Monde _gestionnaireMonde;
	private CharacterBody3D _joueur;
	private readonly List<BoeufSauvage> _boeufs = new List<BoeufSauvage>();
	private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
	private readonly HashSet<Vector2I> _chunksEvaluesSpawnFaune = new HashSet<Vector2I>();
	private float _cooldownVerification;
	private bool _premierTroupeauForce;
	private bool _fauneChargeeDepuisSauvegarde;
	private int _curseurEvaluationChunks;
	private float _cooldownDrainProfilage;
	private const int VersionPersistanceFaune = 1;

	public override void _Ready()
	{
		_rng.Randomize();
		Node parent = GetParent();
		_gestionnaireMonde = parent?.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
		_joueur = parent?.GetNodeOrNull<CharacterBody3D>("Joueur");
	}

	public override void _Process(double delta)
	{
		if (ResoudreSceneFemelle() == null && SceneTaureau == null) return;
		if (_gestionnaireMonde == null || _joueur == null) return;
		if (!_gestionnaireMonde.EstSpawnPret()) return;
		if (!_gestionnaireMonde.EstAlignementSpawnTermine()) return;
		ulong debutFrameUs = ActiverProfilagePerfFaune ? PerfBudgetMonitor.Begin() : 0UL;
		_cooldownDrainProfilage += (float)delta;
		if (!_fauneChargeeDepuisSauvegarde)
		{
			ChargerFauneMonde();
			_fauneChargeeDepuisSauvegarde = true;
		}

		_cooldownVerification -= (float)delta;
		if (_cooldownVerification > 0f)
		{
			if (ActiverProfilagePerfFaune)
				PerfBudgetMonitor.End("Faune/GestionnaireFrame", debutFrameUs);
			return;
		}
		// GATE FPS STRICT : aucun nouveau spawn de troupeau si le streaming global est gelé (FPS < seuil).
		// On ne touche PAS aux bovins existants, on empêche juste d'en ajouter qui feraient pire.
		float fpsActuel = (float)Engine.GetFramesPerSecond();
		if (fpsActuel > 1f && fpsActuel < 58f)
		{
			_cooldownVerification = 0.25f; // réessaie bientôt, pas de log ni spawn
			if (ActiverProfilagePerfFaune)
				PerfBudgetMonitor.End("Faune/GestionnaireFrame", debutFrameUs);
			return;
		}
		_cooldownVerification = IntervalleVerificationSpawn;
		ulong debutEvalUs = ActiverProfilagePerfFaune ? PerfBudgetMonitor.Begin() : 0UL;
		EvaluerChunksEtSpawnerTroupeaux();
		if (ActiverProfilagePerfFaune)
		{
			PerfBudgetMonitor.End("Faune/GestionnaireEvaluationChunks", debutEvalUs);
			PerfBudgetMonitor.End("Faune/GestionnaireFrame", debutFrameUs);
			if (_cooldownDrainProfilage >= Mathf.Max(0.2f, IntervalleLogProfilageFauneSec))
			{
				_cooldownDrainProfilage = 0f;
				PerfBudgetMonitor.FlushSiEchu("Faune", IntervalleLogProfilageFauneSec);
			}
		}
	}

	public IReadOnlyList<BoeufSauvage> ObtenirBoeufsActifs()
	{
		_boeufs.RemoveAll(b => !IsInstanceValid(b) || !b.IsInsideTree());
		return _boeufs;
	}

	private static string ObtenirCheminFichierFaune()
	{
		string nomMonde = GameState.Instance?.NomMondeActuel ?? "MonMonde";
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{nomMonde}");
		Directory.CreateDirectory(dossier);
		return Path.Combine(dossier, "faune_boeufs.dat");
	}

	public void SauvegarderFauneMonde()
	{
		try
		{
			_boeufs.RemoveAll(b => !IsInstanceValid(b) || !b.IsInsideTree());
			string chemin = ObtenirCheminFichierFaune();
			using var w = new BinaryWriter(File.Open(chemin, FileMode.Create));
			w.Write(VersionPersistanceFaune);
			w.Write(_boeufs.Count);
			foreach (BoeufSauvage b in _boeufs)
			{
				bool estFemelle = b is VacheSauvage;
				w.Write(estFemelle);
				var profil = b.ExtraireProfilPersistant();
				w.Write(Json.Stringify(profil));
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K Faune : Erreur sauvegarde faune : {ex.Message}");
		}
	}

	public void ChargerFauneMonde()
	{
		try
		{
			if (_fauneChargeeDepuisSauvegarde)
				return;
			string chemin = ObtenirCheminFichierFaune();
			if (!File.Exists(chemin))
			{
				_fauneChargeeDepuisSauvegarde = true;
				return;
			}

			foreach (BoeufSauvage b in _boeufs)
			{
				if (IsInstanceValid(b))
					b.QueueFree();
			}
			_boeufs.Clear();

			using var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
			int version = r.ReadInt32();
			if (version < 1 || version > VersionPersistanceFaune)
				return;
			int count = Mathf.Max(0, r.ReadInt32());
			for (int i = 0; i < count; i++)
			{
				bool estFemelle = r.ReadBoolean();
				string jsonProfil = r.ReadString();
				Variant v = Json.ParseString(jsonProfil);
				if (v.VariantType != Variant.Type.Dictionary)
					continue;
				var dict = v.AsGodotDictionary();

				PackedScene scene = estFemelle ? ResoudreSceneFemelle() : SceneTaureau;
				if (scene == null)
					scene = ResoudreSceneFemelle() ?? SceneTaureau;
				if (scene == null)
					continue;

				Node inst = scene.Instantiate();
				if (inst is not BoeufSauvage boeuf)
				{
					inst?.QueueFree();
					continue;
				}
				AddChild(boeuf);
				Vector3 pos = Vector3.Zero;
				if (dict.TryGetValue("x", out Variant x) && dict.TryGetValue("y", out Variant y) && dict.TryGetValue("z", out Variant z))
					pos = new Vector3(x.AsSingle(), y.AsSingle(), z.AsSingle());
				boeuf.GlobalPosition = pos;
				boeuf.Configurer(_gestionnaireMonde, _joueur, _gestionnaireMonde.SeedTerrain, pos);
				boeuf.AppliquerProfilPersistant(dict);
				_boeufs.Add(boeuf);
			}
			_fauneChargeeDepuisSauvegarde = true;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K Faune : Erreur chargement faune : {ex.Message}");
		}
	}

	/// <summary>
	/// Lance le "dé" UNE seule fois par chunk chargé: si le chunk est ID1 et gagne le tirage,
	/// on y spawn un troupeau mixte de 5-6 bovidés (vaches + boeufs).
	/// </summary>
	private void EvaluerChunksEtSpawnerTroupeaux()
	{
		_boeufs.RemoveAll(b => !IsInstanceValid(b));
		Vector2I chunkJoueur = Gestionnaire_Monde.WorldToChunkCoord(_joueur.GlobalPosition, _gestionnaireMonde.TailleChunk);
		int rayon = Mathf.Max(1, RayonEvaluationChunksAutourJoueur);
		if (_gestionnaireMonde.RenderDistance > 0)
			rayon = Mathf.Min(rayon, Mathf.Max(1, _gestionnaireMonde.RenderDistance - 1));
		int cote = rayon * 2 + 1;
		int totalCases = Math.Max(1, cote * cote);
		if (_curseurEvaluationChunks >= totalCases)
			_curseurEvaluationChunks = 0;
		int budget = Mathf.Clamp(BudgetChunksEvaluesParCycle, 8, 256);
		Vector2I? chunkPlaineProche = null;

		int evals = 0;
		int troupeauxSpawnesCeTick = 0;
		const int MaxTroupeauxSpawnesParTick = 1; // Anti-burst : un seul troupeau par évaluation évite les rafales au passage d'une grande plaine.
		while (evals < budget)
		{
			int index = (_curseurEvaluationChunks + evals) % totalCases;
			int dx = (index % cote) - rayon;
			int dz = (index / cote) - rayon;
			evals++;
			Vector2I c = new Vector2I(chunkJoueur.X + dx, chunkJoueur.Y + dz);
			if (_chunksEvaluesSpawnFaune.Contains(c))
				continue;
			if (!_gestionnaireMonde.ChunkEstCharge(c))
				continue; // Le dé n'est pas lancé avant la génération/chargement du chunk.

			_chunksEvaluesSpawnFaune.Add(c);
			if (!ChunkSemblePlaineID1(c))
				continue;
			if (chunkPlaineProche == null)
				chunkPlaineProche = c;
			if (!TirageTroupeauReussi(c))
				continue;
			if (troupeauxSpawnesCeTick >= MaxTroupeauxSpawnesParTick)
				continue; // On reprendra au prochain tick (curseur avance), sans re-tirer ce chunk.

			SpawnerTroupeauDansChunk(c);
			troupeauxSpawnesCeTick++;
		}
		_curseurEvaluationChunks = (_curseurEvaluationChunks + evals) % totalCases;

		// Sécurité UX: éviter le cas "je cherche partout et je ne vois rien".
		if (GarantirPremierTroupeau && !_premierTroupeauForce && _boeufs.Count == 0 && chunkPlaineProche.HasValue)
		{
			SpawnerTroupeauDansChunk(chunkPlaineProche.Value);
			_premierTroupeauForce = true;
		}
	}

	private PackedScene ResoudreSceneFemelle() => SceneVache ?? SceneBoeuf;

	private void CompterSexes(out int nbFemelles, out int nbMales)
	{
		nbFemelles = 0;
		nbMales = 0;
		foreach (BoeufSauvage b in _boeufs)
		{
			if (!IsInstanceValid(b)) continue;
			if (b is VacheSauvage) nbFemelles++;
			else nbMales++;
		}
	}

	private PackedScene ChoisirSceneSpawn(int nbFemelles, int nbMales)
	{
		PackedScene sceneFemelle = ResoudreSceneFemelle();
		PackedScene sceneMale = SceneTaureau;
		if (sceneFemelle != null && sceneMale != null)
		{
			// Garantie un couple male/femelle quand possible.
			if (nbFemelles == 0) return sceneFemelle;
			if (nbMales == 0) return sceneMale;

			int totalActuel = nbFemelles + nbMales;
			int cibleMales = Mathf.Clamp(Mathf.RoundToInt((totalActuel + 1) * RatioMales), 1, totalActuel + 1);
			return nbMales < cibleMales ? sceneMale : sceneFemelle;
		}

		if (sceneFemelle != null) return sceneFemelle;
		return sceneMale;
	}

	private bool ChunkSemblePlaineID1(Vector2I chunk)
	{
		int tc = Mathf.Max(1, _gestionnaireMonde.TailleChunk);
		float cx = chunk.X * tc + tc * 0.5f;
		float cz = chunk.Y * tc + tc * 0.5f;
		int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(Mathf.FloorToInt(cx), Mathf.FloorToInt(cz), _gestionnaireMonde.SeedTerrain);
		Vector3 centreChunkSol = new Vector3(cx, h + 0.25f, cz);
		int id = _gestionnaireMonde.ObtenirMatiereExacte(centreChunkSol);
		return id == IdMatierePlaine;
	}

	private bool TirageTroupeauReussi(Vector2I chunk)
	{
		float roll = Hash01(chunk, 9137);
		return roll <= Mathf.Clamp(ChanceTroupeauParChunkID1, 0f, 1f);
	}

	private int DeterminerTailleTroupeau(Vector2I chunk)
	{
		int min = Mathf.Max(1, Mathf.Min(TailleTroupeauMin, TailleTroupeauMax));
		int max = Mathf.Max(min, Mathf.Max(TailleTroupeauMin, TailleTroupeauMax));
		int delta = max - min + 1;
		int idx = Mathf.Clamp((int)Mathf.Floor(Hash01(chunk, 1777) * delta), 0, delta - 1);
		return min + idx;
	}

	private void SpawnerTroupeauDansChunk(Vector2I chunk)
	{
		int tc = Mathf.Max(1, _gestionnaireMonde.TailleChunk);
		Vector3 ancre = new Vector3(chunk.X * tc + tc * 0.5f, _joueur.GlobalPosition.Y, chunk.Y * tc + tc * 0.5f);
		int nbFemelles = 0;
		int nbMales = 0;
		int taille = DeterminerTailleTroupeau(chunk);
		int spawnsReussis = 0;
		int tentativesGlobales = 0;
		int budgetTentatives = Mathf.Max(taille * 8, taille + 2);

		while (spawnsReussis < taille && tentativesGlobales < budgetTentatives)
		{
			PackedScene sceneSpawn = ChoisirSceneSpawn(nbFemelles, nbMales);
			if (sceneSpawn == null)
				break;
			bool modeRelache = tentativesGlobales >= Mathf.Max(3, taille);
			if (!TrouverPointSpawnDansChunk(chunk, tentativesGlobales, out Vector3 pointSol, modeRelache))
			{
				tentativesGlobales++;
				continue;
			}

			Node instance = sceneSpawn.Instantiate();
			if (instance is not BoeufSauvage boeuf)
			{
				instance.QueueFree();
				tentativesGlobales++;
				continue;
			}

			AddChild(boeuf);
			boeuf.GlobalPosition = pointSol + Vector3.Up * 0.2f;
			boeuf.Configurer(_gestionnaireMonde, _joueur, _gestionnaireMonde.SeedTerrain, ancre);
			_boeufs.Add(boeuf);
			if (boeuf is VacheSauvage) nbFemelles++;
			else nbMales++;
			spawnsReussis++;
			tentativesGlobales++;
		}
	}

	private bool TrouverPointSpawnDansChunk(Vector2I chunk, int indexDansTroupeau, out Vector3 pointSol, bool modeRelache = false)
	{
		pointSol = Vector3.Zero;
		int seed = _gestionnaireMonde.SeedTerrain;
		int tc = Mathf.Max(1, _gestionnaireMonde.TailleChunk);
		float marge = 1.2f;
		float minX = chunk.X * tc + marge;
		float maxX = (chunk.X + 1) * tc - marge;
		float minZ = chunk.Y * tc + marge;
		float maxZ = (chunk.Y + 1) * tc - marge;

		for (int essai = 0; essai < EssaisParBoeuf; essai++)
		{
			float rx = Hash01(chunk, indexDansTroupeau * 2003 + essai * 73 + 11);
			float rz = Hash01(chunk, indexDansTroupeau * 1999 + essai * 79 + 17);
			float x = Mathf.Lerp(minX, maxX, rx);
			float z = Mathf.Lerp(minZ, maxZ, rz);
			int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(Mathf.FloorToInt(x), Mathf.FloorToInt(z), seed);
			Vector3 testRaycast = new Vector3(x, h + HauteurRaycast, z);

			if (!EssayerTrouverSolParRaycast(testRaycast, out Vector3 hit))
				continue;
			if (!modeRelache && ExigerChunksVoisinsChargesPourSpawn && !ChunksAutourSontCharges(hit))
				continue;
			if (!modeRelache && ValiderSolAutourDuPointSpawn && !ValiderSolAutourDuPoint(hit))
				continue;
			if (TropProcheAutreBoeuf(hit))
				continue;
			int idMatiere = _gestionnaireMonde.ObtenirMatiereExacte(hit + Vector3.Up * 0.2f);
			if (idMatiere != IdMatierePlaine)
				continue;

			pointSol = hit;
			return true;
		}
		return false;
	}

	private float Hash01(Vector2I chunk, int salt)
	{
		unchecked
		{
			uint x = (uint)(chunk.X * 73856093);
			uint z = (uint)(chunk.Y * 19349663);
			uint s = (uint)(_gestionnaireMonde.SeedTerrain * 83492791 + salt * 2654435761u);
			uint h = x ^ z ^ s;
			h ^= h >> 16;
			h *= 0x7feb352d;
			h ^= h >> 15;
			h *= 0x846ca68b;
			h ^= h >> 16;
			return (h & 0x00FFFFFF) / 16777215f;
		}
	}

	private bool TrouverPointSpawn(Vector3 centre, out Vector3 pointSol)
	{
		pointSol = Vector3.Zero;
		int seed = _gestionnaireMonde.SeedTerrain;
		float rayonMaxSecurise = CalculerRayonSpawnMaxSecurise();
		if (rayonMaxSecurise < Mathf.Max(4f, RayonSpawnMin))
			rayonMaxSecurise = Mathf.Max(4f, RayonSpawnMin);

		for (int essai = 0; essai < EssaisParBoeuf; essai++)
		{
			float angle = _rng.RandfRange(0f, Mathf.Tau);
			// Biais vers le proche: plus fiable quand les chunks lointains ne sont pas encore collidables.
			float t = _rng.Randf();
			float distance = Mathf.Lerp(RayonSpawnMin, rayonMaxSecurise, t * t);
			Vector3 candidat = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
			if (ExigerChunksVoisinsChargesPourSpawn && !ChunksAutourSontCharges(candidat))
				continue;

			int x = Mathf.FloorToInt(candidat.X);
			int z = Mathf.FloorToInt(candidat.Z);
			int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z, seed);
			if (!AutoriserSpawnPartout)
			{
				if (h < 103 || h > 230) continue;
				if (PenteTropForte(x, z, h, seed)) continue;
			}

			Vector3 testRaycast = new Vector3(x + 0.5f, h + HauteurRaycast, z + 0.5f);
			if (!EssayerTrouverSolParRaycast(testRaycast, out Vector3 hit))
				continue;
			if (ValiderSolAutourDuPointSpawn && !ValiderSolAutourDuPoint(hit))
				continue;

			if (hit.DistanceTo(_joueur.GlobalPosition) < RayonSpawnMin * 0.55f) continue;
			if (TropProcheAutreBoeuf(hit)) continue;
			if (!ValiderChanceBiomeSpawn(hit)) continue;

			pointSol = hit;
			return true;
		}

		return false;
	}

	private bool ValiderChanceBiomeSpawn(Vector3 pointSol)
	{
		int idMatiere = _gestionnaireMonde.ObtenirMatiereExacte(pointSol + Vector3.Up * 0.15f);
		bool estPlaine = idMatiere == IdMatierePlaine;
		float chance = estPlaine ? ChanceSpawnPlaine : ChanceSpawnHorsPlaine;
		chance = Mathf.Clamp(chance, 0f, 1f);
		return _rng.Randf() <= chance;
	}

	private bool PenteTropForte(int x, int z, int h, int seed)
	{
		int hE = Generateur_Voxel.ObtenirHauteurTerrainMonde(x + 8, z, seed);
		int hW = Generateur_Voxel.ObtenirHauteurTerrainMonde(x - 8, z, seed);
		int hN = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z - 8, seed);
		int hS = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z + 8, seed);
		int pente = Mathf.Abs(h - hE) + Mathf.Abs(h - hW) + Mathf.Abs(h - hN) + Mathf.Abs(h - hS);
		return pente > 38;
	}

	private bool TropProcheAutreBoeuf(Vector3 point)
	{
		float min2 = DistanceMinEntreBoeufs * DistanceMinEntreBoeufs;
		foreach (BoeufSauvage b in _boeufs)
		{
			if (!IsInstanceValid(b)) continue;
			Vector3 d = b.GlobalPosition - point;
			d.Y = 0f;
			if (d.LengthSquared() < min2) return true;
		}

		return false;
	}

	private bool EssayerTrouverSolParRaycast(Vector3 positionApprox, out Vector3 pointSol)
	{
		pointSol = Vector3.Zero;
		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null) return false;

		Vector3 debut = positionApprox;
		Vector3 fin = positionApprox + Vector3.Down * (HauteurRaycast * 2f);
		var query = PhysicsRayQueryParameters3D.Create(debut, fin);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		var hit = world.DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0 || !hit.ContainsKey("position")) return false;
		pointSol = (Vector3)hit["position"];
		return true;
	}

	private float CalculerRayonSpawnMaxSecurise()
	{
		float rayonDemande = Mathf.Max(RayonSpawnMin + 1f, RayonSpawnMax);
		if (_gestionnaireMonde == null)
			return rayonDemande;

		int tailleChunk = Mathf.Max(1, _gestionnaireMonde.TailleChunk);
		int render = Mathf.Max(1, _gestionnaireMonde.RenderDistance);
		int marge = Mathf.Clamp(MargeSecuriteChunksSpawn, 0, render - 1);
		float rayonChargement = Mathf.Max(tailleChunk, (render - marge) * tailleChunk);
		return Mathf.Min(rayonDemande, rayonChargement);
	}

	private bool ChunksAutourSontCharges(Vector3 point)
	{
		if (_gestionnaireMonde == null || !ExigerChunksVoisinsChargesPourSpawn)
			return true;

		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(point, _gestionnaireMonde.TailleChunk);
		if (!_gestionnaireMonde.ChunkEstCharge(c)) return false;
		if (!_gestionnaireMonde.ChunkEstCharge(new Vector2I(c.X - 1, c.Y))) return false;
		if (!_gestionnaireMonde.ChunkEstCharge(new Vector2I(c.X + 1, c.Y))) return false;
		if (!_gestionnaireMonde.ChunkEstCharge(new Vector2I(c.X, c.Y - 1))) return false;
		if (!_gestionnaireMonde.ChunkEstCharge(new Vector2I(c.X, c.Y + 1))) return false;
		return true;
	}

	private bool ValiderSolAutourDuPoint(Vector3 pointSol)
	{
		if (!ValiderSolAutourDuPointSpawn)
			return true;
		if (!EssayerTrouverSolParRaycast(pointSol + Vector3.Up * 2.0f, out _))
			return false;

		Vector3[] offsets =
		{
			new Vector3(RayonValidationSolSpawn, 0f, 0f),
			new Vector3(-RayonValidationSolSpawn, 0f, 0f),
			new Vector3(0f, 0f, RayonValidationSolSpawn),
			new Vector3(0f, 0f, -RayonValidationSolSpawn),
		};
		foreach (Vector3 off in offsets)
		{
			if (!EssayerTrouverSolParRaycast(pointSol + off + Vector3.Up * 2.0f, out Vector3 voisin))
				return false;
			if (Mathf.Abs(voisin.Y - pointSol.Y) > 6.0f)
				return false;
		}
		return true;
	}
}
