using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public partial class Monde_Client : Node3D
{
	private int CompterBacklog()
	{
		int pendingMaths;
		lock (_lockFileAttenteMaths)
			pendingMaths = _fileAttenteMathsData.Count;
		return pendingMaths
			+ _fileIntegrationMainThread.Count
			+ _fileAttenteSolidification.Count
			+ _fileFloreDifferee.Count
			+ Thread.VolatileRead(ref _chunksEnCoursDeCalcul);
	}

	/// <summary>Empreinte légère du paquet serveur : détecte un renvoi identique sans comparer tout le voxel.</summary>
	private static ulong CalculerEmpreinteDonneesChunk(DonneesChunk d)
	{
		if (d == null) return 0;
		var hc = new HashCode();
		hc.Add(d.CoordChunk.X);
		hc.Add(d.CoordChunk.Y);
		hc.Add(d.CoordChunkY);
		hc.Add(d.TailleChunk);
		hc.Add(d.HauteurMax);
		hc.Add(d.EstVideIntegral);
		if (d.MaterialsFlat != null)
		{
			hc.Add(d.MaterialsFlat.Length);
			int pas = Mathf.Max(1, d.MaterialsFlat.Length / 48);
			for (int i = 0; i < d.MaterialsFlat.Length; i += pas)
				hc.Add(d.MaterialsFlat[i]);
		}
		if (d.DensitiesQuantifiees != null)
		{
			hc.Add(d.DensitiesQuantifiees.Length);
			int pas = Mathf.Max(1, d.DensitiesQuantifiees.Length / 32);
			for (int i = 0; i < d.DensitiesQuantifiees.Length; i += pas)
				hc.Add(d.DensitiesQuantifiees[i]);
		}
		else if (d.DensitiesFlat != null)
		{
			hc.Add(d.DensitiesFlat.Length);
			int pas = Mathf.Max(1, d.DensitiesFlat.Length / 32);
			for (int i = 0; i < d.DensitiesFlat.Length; i += pas)
				hc.Add(d.DensitiesFlat[i]);
		}
		return unchecked((ulong)(uint)hc.ToHashCode());
	}

	/// <summary>Évite que les files de streaming grossissent sans borne pendant une longue marche (lag croissant).</summary>
	private void EpurerBacklogsChunkLointains(Vector3 positionObservation)
	{
		Vector2I obs = Gestionnaire_Monde.WorldToChunkCoord(positionObservation, TailleChunk);
		int rayonGarder = Mathf.Max(RayonDormancePhysique + 1, _rayonRequetesActuel + 3);
		float gardeCarre = rayonGarder * rayonGarder;

		lock (_lockFileAttenteMaths)
		{
			while (_fileAttenteMathsData.Count > MaxFileAttenteMathsChunks)
			{
				int pire = -1;
				float pireD = -1f;
				for (int i = 0; i < _fileAttenteMathsData.Count; i++)
				{
					Vector2I c = _fileAttenteMathsData[i].data.Coordonnees;
					float d = DistanceCarreeChunk(obs, c);
					if (d > pireD)
					{
						pireD = d;
						pire = i;
					}
				}
				if (pire < 0 || pireD <= gardeCarre)
					break;
				_fileAttenteMathsData.RemoveAt(pire);
			}
		}

		int surplusIntegration = _fileIntegrationMainThread.Count - MaxFileIntegrationEnAttente;
		for (int n = 0; n < surplusIntegration && _fileIntegrationMainThread.TryDequeue(out _); n++) { }
	}

	private static float DistanceCarreeChunk(Vector2I obs, Vector2I chunk)
	{
		int dx = chunk.X - obs.X;
		int dz = chunk.Y - obs.Y;
		return dx * dx + dz * dz;
	}

	/// <summary>
	/// Mode « Sauver les FPS » : réduit le débit lointain mais ne doit pas bloquer la génération de chunks
	/// (nouveau monde, sol manquant, file/radar en retard, tranches profondeur absentes).
	/// </summary>
	private bool EstStreamingChunksPrioritaire(bool enChargement, bool garantirProcheJoueur)
	{
		if (!ModeSurvieFpsAgressif)
			return enChargement || garantirProcheJoueur;
		if (enChargement || garantirProcheJoueur)
			return true;
		if (_timerGraceStreamingBootstrap > 0f)
			return true;
		if (_chunksACharger.Count > 0)
			return true;
		if (_rayonRequetesActuel + 2 < RayonChargementChunksActif())
			return true;
		if (CompterBacklog() > SeuilBacklogBas)
			return true;
		if (ModeProfondeurTranchesActif() && EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef))
		{
			Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(joueurRef.GlobalPosition, TailleChunk);
			int cy = CoordYDepuisMondeY((int)Mathf.Floor(joueurRef.GlobalPosition.Y));
			int demi = ConstantesProfondeurVerticale.DemiFenetreTranches;
			for (int dy = -demi; dy <= demi; dy++)
			{
				if (!TryGetChunkDataPourCoordY(c, cy + dy, out var data) || data == null
					|| !data.VisualInstanceRID.IsValid)
					return true;
			}
		}
		return false;
	}

	/// <summary>Vrai si le streaming peut prendre un peu plus de marge (FPS/backlog stables, voir <see cref="AjusterFenetreRequetes"/>).</summary>
	private bool StreamingPeutElargirTranquillement()
	{
		return ActiverElargissementRadarSiFpsStable
			&& ModeSurvieFpsAgressif
			&& _accumulateurFpsStablePourRadar >= SecondesFpsStablesPourElargirRadar;
	}

	/// <summary>Plafond de <see cref="_rayonRequetesActuel"/> sous urgence FPS, proportionnel à la cible (jamais au-dessus de <paramref name="rayonDetail"/>).</summary>
	private int CalculerCapUrgenceRayonRequetes(int niveauUrgence, int rayonDetail)
	{
		if (rayonDetail <= 0)
			return 0;
		int minAbsolu = Mathf.Max(1, RayonDormancePhysique + 1);
		float frac = niveauUrgence >= 3
			? Mathf.Clamp(FractionRayonMaxUrgenceExtreme, 0.15f, 0.95f)
			: niveauUrgence >= 2
				? Mathf.Clamp(FractionRayonMaxUrgenceCritique, 0.15f, 0.95f)
				: Mathf.Clamp(FractionRayonMaxUrgenceForte, 0.15f, 0.95f);
		int depuisFrac = Mathf.Max(minAbsolu, Mathf.RoundToInt(rayonDetail * frac));
		return Mathf.Clamp(Mathf.Min(rayonDetail, depuisFrac), minAbsolu, rayonDetail);
	}

	private void AjusterFenetreRequetes(float dt)
	{
		if (_timerGraceStreamingReglageUtilisateur > 0f)
			_timerGraceStreamingReglageUtilisateur = Mathf.Max(0f, _timerGraceStreamingReglageUtilisateur - dt);
		int rayonDetail = RayonChargementChunksActif();
		int minRayonRequetes = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
		// Plafond valide pour Clamp : si RenderDistance est basse, rayonDetail peut être < RayonInitialRequetesChunks.
		int plafondFenetreRequetes = Mathf.Max(minRayonRequetes, rayonDetail);
		int backlog = CompterBacklog();
		bool calmePourAccumRadar = ActiverElargissementRadarSiFpsStable && ModeSurvieFpsAgressif
			&& _niveauUrgencePerf == 0
			&& !_gateStreamingGele
			&& _timerFreinSpike <= 0f
			&& _fpsMoyenneAuto >= SeuilFpsMoyenPourElargirRadar
			&& backlog <= SeuilBacklogBas
			&& _timerGraceStreamingReglageUtilisateur <= 0f;
		if (calmePourAccumRadar)
			_accumulateurFpsStablePourRadar = Mathf.Min(_accumulateurFpsStablePourRadar + dt, SecondesFpsStablesPourElargirRadar + 4f);
		else
			_accumulateurFpsStablePourRadar = Mathf.Max(0f, _accumulateurFpsStablePourRadar - dt * 2.5f);
		// Hors « Sauver les FPS » : pas de réduction automatique du rayon ni throttling par backlog sur cette fenêtre.
		if (!ModeSurvieFpsAgressif)
		{
			_rayonRequetesActuel = plafondFenetreRequetes;
			return;
		}
		if (_rayonRequetesActuel <= 0) _rayonRequetesActuel = minRayonRequetes;
		_rayonRequetesActuel = Mathf.Clamp(_rayonRequetesActuel, minRayonRequetes, plafondFenetreRequetes);

		_timerExpansionRequetes -= dt;
		_timerProgressionForceeRayon -= dt;
		if (backlog >= SeuilBacklogHaut)
		{
			_rayonRequetesActuel = Mathf.Max(Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks), _rayonRequetesActuel - 1);
			_timerExpansionRequetes = Mathf.Max(0.1f, IntervalleExpansionRequetesSec * 0.6f);
		}
		else if (_timerExpansionRequetes <= 0f && backlog <= SeuilBacklogBas)
		{
			int gap = Mathf.Max(0, rayonDetail - _rayonRequetesActuel);
			int pas = gap > 40 ? 2 : 1;
			if (gap > 0 && _niveauUrgencePerf == 0 && !_gateStreamingGele && _timerFreinSpike <= 0f)
			{
				int div = Mathf.Max(1, DiviseurGapPourPasExpansion);
				int pasGap = Mathf.Clamp(gap / div, 1, Mathf.Max(1, PasExpansionMaxSiGapLarge));
				pas = Mathf.Max(pas, pasGap);
			}
			if (_timerGraceStreamingReglageUtilisateur > 0f)
				pas = Mathf.Max(pas, Mathf.Clamp(gap / 6, 2, 5));
			if (StreamingPeutElargirTranquillement() && PasExpansionRequetesSupplementaireSiCalme > 0)
				pas = Mathf.Min(gap, pas + PasExpansionRequetesSupplementaireSiCalme);
			_rayonRequetesActuel = Mathf.Min(rayonDetail, _rayonRequetesActuel + pas);
			_timerExpansionRequetes = Mathf.Max(0.1f, IntervalleExpansionRequetesSec);
		}
		if (_timerGraceStreamingReglageUtilisateur <= 0f)
		{
			if (_niveauUrgencePerf >= 3)
			{
				int capUrgence = CalculerCapUrgenceRayonRequetes(3, rayonDetail);
				_rayonRequetesActuel = Mathf.Min(_rayonRequetesActuel, capUrgence);
				_timerExpansionRequetes = Mathf.Max(_timerExpansionRequetes, 0.32f);
				_timerProgressionForceeRayon = Mathf.Max(_timerProgressionForceeRayon, 0.60f);
			}
			else if (_niveauUrgencePerf >= 2)
			{
				int capUrgence = CalculerCapUrgenceRayonRequetes(2, rayonDetail);
				_rayonRequetesActuel = Mathf.Min(_rayonRequetesActuel, capUrgence);
				_timerExpansionRequetes = Mathf.Max(_timerExpansionRequetes, 0.25f);
				_timerProgressionForceeRayon = Mathf.Max(_timerProgressionForceeRayon, 0.45f);
			}
			else if (_niveauUrgencePerf == 1)
			{
				int capUrgence = CalculerCapUrgenceRayonRequetes(1, rayonDetail);
				_rayonRequetesActuel = Mathf.Min(_rayonRequetesActuel, capUrgence);
				_timerExpansionRequetes = Mathf.Max(_timerExpansionRequetes, 0.16f);
			}
		}

		// Même sous charge, le rayon avance lentement pour éviter un "blocage complet" du chargement lointain.
		if (_niveauUrgencePerf <= 0 && _timerProgressionForceeRayon <= 0f && _rayonRequetesActuel < rayonDetail)
		{
			_rayonRequetesActuel = Mathf.Min(rayonDetail, _rayonRequetesActuel + 1);
			_timerProgressionForceeRayon = Mathf.Max(0.5f, IntervalleProgressionForceeRayonSec);
		}
	}

	private Vector3I ExtraireCleFloreLaPlusProche(List<Vector3I> liste, Vector3 positionObservation)
	{
		if (liste.Count == 0) return Vector3I.Zero;
		Vector2 posObsV2 = new Vector2(positionObservation.X / TailleChunk, positionObservation.Z / TailleChunk);
		int best = 0;
		float bestD = float.MaxValue;
		for (int i = 0; i < liste.Count; i++)
		{
			Vector2 c = new Vector2(liste[i].X, liste[i].Z);
			float d = c.DistanceSquaredTo(posObsV2);
			if (d < bestD) { bestD = d; best = i; }
		}
		Vector3I v = liste[best];
		liste.RemoveAt(best);
		return v;
	}

	/// <summary>Minage près de Y=100,200… : inclure la tranche voisine déjà en RAM pour éviter mesh/collision désynchronisés.</summary>
	private void ExpandirTranchesVoisinesPourRemeshMinage(HashSet<Vector3I> chunks)
	{
		if (chunks == null || chunks.Count == 0) return;
		_voisinsRemeshMinageTemp.Clear();
		_voisinsRemeshMinageTemp.AddRange(chunks);
		foreach (Vector3I c in _voisinsRemeshMinageTemp)
		{
			var dessous = new Vector3I(c.X, c.Y - 1, c.Z);
			if (TryGetChunkDataPourCoordY(new Vector2I(c.X, c.Z), c.Y - 1, out _))
				chunks.Add(dessous);
			var dessus = new Vector3I(c.X, c.Y + 1, c.Z);
			if (TryGetChunkDataPourCoordY(new Vector2I(c.X, c.Z), c.Y + 1, out _))
				chunks.Add(dessus);
		}
	}

	private void ExecuterReconstructionPrioritaire(Vector3I coord)
	{
		ChunkData data = null;
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			if (!_chunksDataAbysse3D.TryGetValue(coord, out data))
				return;
		}
		else if (!TryGetChunkDataPourCoordY(new Vector2I(coord.X, coord.Z), coord.Y, out data))
			return;
		if (data.DensitiesFlat == null || data.MaterialsFlat == null) return;
		data.EmpreinteDonneesServeur = 0;
		var payloads = Chunk_Client.ReconstruirePayloadsDepuisData(data, TryEchantillonnerVoxelProfondeur);
		if (payloads != null && payloads.Count > 0)
			IntegrerChunkDataRIDs(data, payloads, recoudreVoisinsVertical: false);
		RestaurerCollisionImmediateSiSousJoueur(data);
	}

	/// <summary>Collision synchrone si le chunk est dans la zone prioritaire joueur (spawn / marche / minage).</summary>
	private void SolidifierCollisionPrioritaireSiProcheJoueur(ChunkData data)
	{
		if (data == null || data._meshRef == null || !EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef))
			return;
		Vector3 pos = joueurRef.GlobalPosition;
		Vector3 velXZ = new Vector3(joueurRef.Velocity.X, 0f, joueurRef.Velocity.Z);
		if (DoitSolidifierALIntegration(data, pos, velXZ))
		{
			EssayerSolidifierCorridorAIntegration(data, pos, velXZ);
			return;
		}
		Vector2I cJoueur = Gestionnaire_Monde.WorldToChunkCoord(pos, TailleChunk);
		int dx = Mathf.Abs(data.Coordonnees.X - cJoueur.X);
		int dz = Mathf.Abs(data.Coordonnees.Y - cJoueur.Y);
		bool solProchePret = ChunkSousPiedsAPret();
		int rayonXZ = (dx == 0 && dz == 0) ? 0 : (solProchePret ? 1 : Mathf.Max(2, RayonGrilleMinSpawnPret));
		if (dx > rayonXZ || dz > rayonXZ)
			return;
		if (ModeProfondeurTranchesActif())
		{
			int cyJoueur = CoordYDepuisMondeY((int)Mathf.Floor(pos.Y));
			int dySlice = data.CoordChunkY - cyJoueur;
			if (Mathf.Abs(dySlice) > ConstantesProfondeurVerticale.DemiFenetreTranches)
				return;
			int yBaseTranche = data.CoordChunkY * data.HauteurMax;
			int yPieds = (int)Mathf.Floor(pos.Y);
			if (yPieds < yBaseTranche - 2 || yPieds > yBaseTranche + data.HauteurMax + 2)
				return;
			if (!solProchePret)
				rayonXZ = Mathf.Max(rayonXZ, RayonGrilleMinSpawnPret);
		}
		World3D world = GetWorld3D();
		if (world == null)
			return;
		if (data.PhysicsBodyRID.IsValid && !data.Dormant)
		{
			PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, world.Space);
			data.EstEnFileSolidification = false;
			return;
		}
		RetirerDeFileSolidification(data);
		_setSolidificationUrgente.Remove(data);
		data.EstEnFileSolidification = false;
		AssurerCorpsPhysiqueChunk(data);
		if (data.PhysicsBodyRID.IsValid)
			PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, world.Space);
	}

	/// <summary>Après minage : évite de traverser le sol pendant la file de solidification (collision synchrone sous les pieds).</summary>
	private void RestaurerCollisionImmediateSiSousJoueur(ChunkData data)
		=> SolidifierCollisionPrioritaireSiProcheJoueur(data);

	private float DistanceCarreeAuJoueur(Vector2I chunk, Vector3 posObservation)
	{
		Vector2I obs = Gestionnaire_Monde.WorldToChunkCoord(posObservation, TailleChunk);
		int dx = chunk.X - obs.X, dz = chunk.Y - obs.Y;
		return dx * dx + dz * dz;
	}

	private void PurgerChunksObsolètesDeLaFile(Vector3 positionObservation)
	{
		// Aligner la purge sur la distance utilisateur (panneau), pas sur une fenêtre de requêtes plus petite.
		int rayonPurgeChunks = RayonChargementChunksActif();
		float rayonMaxCarre = (rayonPurgeChunks + 2) * (rayonPurgeChunks + 2);
		for (int i = _chunksACharger.Count - 1; i >= 0; i--)
		{
			float d2 = DistanceCarreeAuJoueur(_chunksACharger[i], positionObservation);
			if (d2 > rayonMaxCarre)
				_chunksACharger.RemoveAt(i);
		}
		int plafondFile = _dimensionReseauActive == (int)DimensionJeu.Abysse
			? MaxFileDemandesChunksAbysse
			: MaxChunksAChargerAlpha;
		if (_chunksACharger.Count > plafondFile)
		{
			Vector2I obs = Gestionnaire_Monde.WorldToChunkCoord(positionObservation, TailleChunk);
			while (_chunksACharger.Count > plafondFile)
			{
				int pire = -1;
				float pireD = -1f;
				for (int i = 0; i < _chunksACharger.Count; i++)
				{
					float d = DistanceCarreeChunk(obs, _chunksACharger[i]);
					if (d > pireD)
					{
						pireD = d;
						pire = i;
					}
				}
				if (pire < 0)
					break;
				_chunksACharger.RemoveAt(pire);
			}
		}
	}

	/// <summary>Sénescence : retire de la mémoire les chunks au-delà du rayon + hystérésis. Libère les RIDs (RenderingServer/PhysicsServer3D).</summary>
	private void NettoyerChunksObsoles(Vector3 positionObservation)
	{
		int rayonDetail = RayonChargementChunksActif();
		// Seuil unique : libère la mémoire de façon homogène (l’ancien écart avant/arrière gardait trop longtemps l’arc avant).
		float seuilEvictionCarree = (rayonDetail + 2) * (rayonDetail + 2);
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			// IMPORTANT : en Abysse, le lifecycle est piloté par la map 3D.
			// Toute libération doit passer par RetirerChunkDataAbysse pour éviter un RID libéré côté cache 2D.
			PurgerChunksAbysseHorsFenetre(positionObservation, _joueur?.GlobalPosition ?? positionObservation);
			if (_cooldownDiagCoherenceAbysse <= 0f)
			{
				VerifierCoherenceCachesAbysse();
				_cooldownDiagCoherenceAbysse = IntervalleDiagCoherenceAbysseSec;
			}
			return;
		}
		if (ModeProfondeurTranchesActif())
		{
			Vector2I obs2D = Gestionnaire_Monde.WorldToChunkCoord(positionObservation, TailleChunk);
			int coordYCourant = CoordYDepuisMondeY((int)Mathf.Floor(positionObservation.Y));
			var clesProfondesARetirer = new List<Vector3I>();
			foreach (var kv in _chunksDataProfondeur3D)
			{
				int dx = kv.Key.X - obs2D.X;
				int dz = kv.Key.Z - obs2D.Y;
				float dist2 = dx * dx + dz * dz;
				int dy = Mathf.Abs(kv.Key.Y - coordYCourant);
				int demiFenetre = DemiFenetreTranchesStreamingActif();
				if (dist2 > seuilEvictionCarree || dy > demiFenetre)
					clesProfondesARetirer.Add(kv.Key);
			}
			for (int i = 0; i < clesProfondesARetirer.Count; i++)
			{
				Vector3I cle = clesProfondesARetirer[i];
				if (!_chunksDataProfondeur3D.TryGetValue(cle, out var data) || data == null)
					continue;
				_chunksDataProfondeur3D.Remove(cle);
				RetirerDeFileSolidification(data);
				_setSolidificationUrgente.Remove(data);
				Vector3I cleFlore = CleFlorePourChunkData(data);
				_setFloreDifferee.Remove(cleFlore);
				_fileFloreDifferee.Remove(cleFlore);
				_frameEnqueueFlore.Remove(cleFlore);
				RetirerTravauxEnAttentePourChunk(data);
				data.LibérerRids();
				data.LibererDonneesVoxel();
			}
		}
		_chunksATuerTemp.Clear();
		foreach (var kv in _chunksData)
		{
			float dist2 = DistanceCarreeAuJoueur(kv.Key, positionObservation);
			if (dist2 > seuilEvictionCarree)
				_chunksATuerTemp.Add(kv.Key);
		}
		foreach (Vector2I coord in _chunksATuerTemp)
		{
			if (_chunksData.TryGetValue(coord, out var data))
			{
				_chunksData.Remove(coord);
				RetirerDeFileSolidification(data);
				_setSolidificationUrgente.Remove(data);
				Vector3I cleFlore = CleFlorePourChunkData(data);
				_setFloreDifferee.Remove(cleFlore);
				_fileFloreDifferee.Remove(cleFlore);
				_frameEnqueueFlore.Remove(cleFlore);
				RetirerTravauxEnAttentePourChunk(data);
				data.LibérerRids();
				data.LibererDonneesVoxel();
				NettoyerRegistreReconstruction(coord);
			}
		}
	}

	private void RetirerTravauxEnAttentePourChunk(ChunkData data)
	{
		if (data == null) return;
		lock (_lockFileAttenteMaths)
			_fileAttenteMathsData.RemoveAll(entree => ReferenceEquals(entree.data, data));
	}

	private void VerifierCoherenceCachesAbysse()
	{
		if (!OS.IsDebugBuild() || _dimensionReseauActive != (int)DimensionJeu.Abysse)
			return;
		if (_chunksDataAbysse3D.Count == 0)
			return;
		foreach (var kv in _chunksData)
		{
			ChunkData data = kv.Value;
			if (data == null)
				continue;
			Vector3I cle = new Vector3I(data.Coordonnees.X, data.CoordChunkY, data.Coordonnees.Y);
			if (_chunksDataAbysse3D.TryGetValue(cle, out var data3D) && ReferenceEquals(data3D, data))
				continue;
			GD.PrintErr($"ZERO-K ABYSSE DIAG : cache 2D incohérent sur {data.Coordonnees} -> coucheY={data.CoordChunkY} absente du cache 3D.");
		}
	}

	private void JournaliserDiagnosticCollisionAbysse(Vector3 positionObservation)
	{
		if (!EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef))
			return;
		Vector3 pos = joueurRef.GlobalPosition;
		Vector2I chunk = Gestionnaire_Monde.WorldToChunkCoord(pos, TailleChunk);
		bool chunkSousPiedsPret = ChunkSousPiedsAPret();
		bool collisionCroixPrete = AbyssePretPourDeplacement(pos);
		bool collisionLocalePrete = AbysseCollisionLocaleActive(pos);
		_coordYCollisionAbysseTravail.Clear();
		RemplirCoordYCollisionAutourPointMonde(pos.Y, _coordYCollisionAbysseTravail);
		var etats = new List<string>(4);
		foreach (int coordY in _coordYCollisionAbysseTravail)
		{
			Vector3I cle = new Vector3I(chunk.X, coordY, chunk.Y);
			bool present = _chunksDataAbysse3D.TryGetValue(cle, out var data) && data != null;
			bool ridActif = present && data.PhysicsBodyRID.IsValid && !data.Dormant && !data.EstEnFileSolidification;
			etats.Add($"{coordY}:{(present ? "present" : "absent")}/{(ridActif ? "actif" : "inactif")}");
		}
		string resumeY = etats.Count > 0 ? string.Join(", ", etats) : "aucune";
		GD.Print($"ZERO-K ABYSSE DIAG CLIENT: pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1}) chunk={chunk} sousPieds={chunkSousPiedsPret} croix={collisionCroixPrete} local={collisionLocalePrete} y={resumeY} chunks3D={_chunksDataAbysse3D.Count} obsY={positionObservation.Y:F1}");
	}

	/// <summary>Tri radial de la file de requêtes (plus proche en tête).</summary>
	private void TrierFileChunksAChargerParDistance(Vector3 positionObservation)
	{
		if (_chunksACharger.Count <= 1) return;
		float ox = positionObservation.X / (float)TailleChunk;
		float oy = positionObservation.Z / (float)TailleChunk;
		_chunksACharger.Sort((a, b) =>
		{
			float da = (a.X - ox) * (a.X - ox) + (a.Y - oy) * (a.Y - oy);
			float db = (b.X - ox) * (b.X - ox) + (b.Y - oy) * (b.Y - oy);
			return da.CompareTo(db);
		});
	}

	private void RetirerChunksDeLaFile(HashSet<Vector2I> aRetirer)
	{
		if (aRetirer == null || aRetirer.Count == 0 || _chunksACharger.Count == 0) return;
		for (int i = _chunksACharger.Count - 1; i >= 0; i--)
			if (aRetirer.Contains(_chunksACharger[i]))
				_chunksACharger.RemoveAt(i);
	}

	/// <summary>
	/// Extraction radiale stricte : toujours le chunk le plus proche (file triée par le radar).
	/// Pas de priorité « là où je regarde » : sinon les montagnes lointaines passent avant les trous proches.
	/// </summary>
	private Vector2I ExtraireChunkLePlusProche(List<Vector2I> liste, Vector3 positionObservation, Vector3 directionObservation)
	{
		if (liste.Count == 0) return Vector2I.Zero;
		Vector2 posObsV2 = new Vector2(positionObservation.X / (float)TailleChunk, positionObservation.Z / (float)TailleChunk);
		Vector2I chunkCible = liste[0];
		float distMin = float.MaxValue;
		int indexASupprimer = 0;
		int count = liste.Count;
		int fenetre = Mathf.Clamp(FenetreSelectionRequetes, 8, 512);
		int scan = Mathf.Min(count, fenetre);
		// Début de file = plus proche (tri radar + InsertRange prioritaire) : on scanne depuis l'index 0.
		for (int i = 0; i < scan; i++)
		{
			Vector2I c = liste[i];
			float dx = c.X - posObsV2.X;
			float dz = c.Y - posObsV2.Y;
			float dist = dx * dx + dz * dz;
			if (dist < distMin)
			{
				distMin = dist;
				chunkCible = c;
				indexASupprimer = i;
			}
		}
		liste.RemoveAt(indexASupprimer);
		return chunkCible;
	}

	private int ExtraireIndexSolidificationProche(Vector2I coordObservation)
	{
		int count = _fileAttenteSolidification.Count;
		if (count <= 1) return 0;
		int fenetre = Mathf.Clamp(FenetreSelectionSolidification, 4, 256);
		int scan = Mathf.Min(count, fenetre);
		if (_curseurSelectionSolidification >= count) _curseurSelectionSolidification = 0;
		int idxBest = _curseurSelectionSolidification;
		int dBest = int.MaxValue;
		for (int n = 0; n < scan; n++)
		{
			int idx = (_curseurSelectionSolidification + n) % count;
			ChunkData c = _fileAttenteSolidification[idx];
			if (c == null) continue;
			int ddx = c.Coordonnees.X - coordObservation.X;
			int ddz = c.Coordonnees.Y - coordObservation.Y;
			int d2 = ddx * ddx + ddz * ddz;
			if (d2 < dBest)
			{
				dBest = d2;
				idxBest = idx;
			}
		}
		_curseurSelectionSolidification = (_curseurSelectionSolidification + 1) % count;
		return idxBest;
	}

	/// <summary>Retire de la file urgente le chunk le plus proche des pieds (tranche Y incluse en profondeur).</summary>
	private bool PreleverSolidificationUrgenteProche(Vector3 positionJoueur, out ChunkData data)
	{
		data = null;
		int count = _fileAttenteSolidificationUrgente.Count;
		if (count == 0) return false;
		Vector2I cJoueur = Gestionnaire_Monde.WorldToChunkCoord(positionJoueur, TailleChunk);
		int cyJoueur = ModeProfondeurTranchesActif()
			? CoordYDepuisMondeY((int)Mathf.Floor(positionJoueur.Y))
			: 0;
		int fenetre = Mathf.Min(count, 48);
		int bestIdx = count - 1;
		int bestScore = int.MaxValue;
		for (int n = 0; n < fenetre; n++)
		{
			int idx = count - 1 - n;
			ChunkData c = _fileAttenteSolidificationUrgente[idx];
			if (c == null) continue;
			int ddx = c.Coordonnees.X - cJoueur.X;
			int ddz = c.Coordonnees.Y - cJoueur.Y;
			int score = ddx * ddx + ddz * ddz;
			if (ModeProfondeurTranchesActif())
				score += Mathf.Abs(c.CoordChunkY - cyJoueur) * 12;
			if (score < bestScore)
			{
				bestScore = score;
				bestIdx = idx;
			}
		}
		data = _fileAttenteSolidificationUrgente[bestIdx];
		_fileAttenteSolidificationUrgente.RemoveAt(bestIdx);
		_setSolidificationUrgente.Remove(data);
		return data != null;
	}

	private void DeclencherReconstructionSection((int cx, int coordY, int cz, int section) cible)
	{
		var coord = new Vector2I(cible.cx, cible.cz);
		if (!_chunksData.TryGetValue(coord, out _)) return;
		// AAA : pas de reconstruction par section ; on pourrait re-demander le chunk.
	}
}
