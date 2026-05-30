using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public partial class Monde_Client : Node3D
{
	/// <summary>Alpha / Beta / Omega / Delta : portail « vers APISARA » au XZ monde (0,0) — la collision de ce disque doit rester active pour <see cref="Portail.AlignerPortailSurSurface"/>.</summary>
	public bool EstDimensionActiveeAvecPortailNexusAuChunkOrigine()
	{
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse) return false;
		return ConstantesDimensions.EssayerObtenirInfo(_dimensionReseauActive, out var info) && info.EstAlphaLike;
	}

	private bool ChunkEstDansDisquePhysiquePortailVersApisara(Vector2I coordChunkXZ)
	{
		int dx0 = Mathf.Abs(coordChunkXZ.X);
		int dz0 = Mathf.Abs(coordChunkXZ.Y);
		return dx0 <= RayonDormancePhysique && dz0 <= RayonDormancePhysique;
	}

	private void DemanderChunk(Vector2I coord)
	{
		if (_networkManager != null)
		{
			Vector3 obs = ObtenirPositionObservation();
			ulong frame = Engine.GetProcessFrames();
			if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
			{
				_coordYActifsAbysseTravail.Clear();
				RemplirCoordYActifsAbysse(obs, _coordYActifsAbysseTravail);
				_coordYActifsAbysseListeTravail.Clear();
				var setCoordYNorm = new HashSet<int>();
				foreach (int coordY in _coordYActifsAbysseTravail)
					setCoordYNorm.Add(NormaliserCoordYAbysse(coordY));
				foreach (int coordYNorm in setCoordYNorm)
					_coordYActifsAbysseListeTravail.Add(coordYNorm);
				int coordYCentre = CoordYStageAbysseDepuisYMonde(obs.Y);
				float vitesseY = _joueur?.Velocity.Y ?? 0f;
				_coordYActifsAbysseListeTravail.Sort((a, b) =>
				{
					int da = Mathf.Abs(a - coordYCentre);
					int db = Mathf.Abs(b - coordYCentre);
					if (da != db) return da.CompareTo(db);
					// Priorité profondeur: en descente, charger d'abord dessous; en montée, dessus.
					if (vitesseY < -0.25f) return a.CompareTo(b);
					if (vitesseY > 0.25f) return b.CompareTo(a);
					return a.CompareTo(b);
				});
				foreach (int coordYActif in _coordYActifsAbysseListeTravail)
				{
					if (ChunkDisponiblePourY(coord, coordYActif))
						continue;
					var cle = new Vector3I(coord.X, coordYActif, coord.Y);
					if (_demandesAbysseFrameDerniereEmission.TryGetValue(cle, out ulong derniereFrame)
						&& frame - derniereFrame < 8)
						continue;
					_demandesAbysseFrameDerniereEmission[cle] = frame;
					_networkManager.EnvoyerDemandeChunkDimensionAuServeur(coord, coordYActif, _dimensionReseauActive, obs);
				}
				// Purge incrémentale de l'anti-spam pour éviter une map de demandes infinie.
				if (_demandesAbysseFrameDerniereEmission.Count > 0 && frame % 45UL == 0UL)
				{
					_clesDemandesAbysseExpireesTemp.Clear();
					foreach (var kv in _demandesAbysseFrameDerniereEmission)
					{
						if (frame - kv.Value > 360UL)
							_clesDemandesAbysseExpireesTemp.Add(kv.Key);
					}
					for (int i = 0; i < _clesDemandesAbysseExpireesTemp.Count; i++)
						_demandesAbysseFrameDerniereEmission.Remove(_clesDemandesAbysseExpireesTemp[i]);
				}
			}
			else
			{
				int coordY = Mathf.FloorToInt(obs.Y / Mathf.Max(1f, HauteurMax));
				_networkManager.EnvoyerDemandeChunkDimensionAuServeur(coord, coordY, _dimensionReseauActive, obs);
			}
			return;
		}
		_enregistrerDemandeChunk?.Invoke(coord);
	}

	private bool ChunkDisponiblePourY(Vector2I coord, int coordY)
	{
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
			return _chunksDataAbysse3D.ContainsKey(new Vector3I(coord.X, NormaliserCoordYAbysse(coordY), coord.Y));
		if (!_chunksData.TryGetValue(coord, out var data) || data == null)
			return false;
		return data.CoordChunkY == coordY;
	}

	private bool TryGetChunkDataPourCoordY(Vector2I coord, int coordY, out ChunkData data)
	{
		data = null;
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
			return _chunksDataAbysse3D.TryGetValue(new Vector3I(coord.X, NormaliserCoordYAbysse(coordY), coord.Y), out data);
		if (!_chunksData.TryGetValue(coord, out data) || data == null)
			return false;
		return data.CoordChunkY == coordY;
	}

	private bool ChunkDisponiblePourObservation(Vector2I coord, Vector3 observation)
	{
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			// Règle critique anti-chute: la disponibilité pour le déplacement doit cibler
			// le stage courant (pas "n'importe quel stage voisin").
			int coordYStageCourant = CoordYStageAbysseDepuisYMonde(observation.Y);
			return ChunkDisponiblePourY(coord, coordYStageCourant);
		}

		int coordYLocal = CoordYDepuisMondeY((int)Mathf.Floor(observation.Y));
		return ChunkDisponiblePourY(coord, coordYLocal);
	}

	private int CoordYDepuisMondeY(int yMonde)
	{
		int h = Mathf.Max(1, HauteurMax);
		return Mathf.FloorToInt(yMonde / (float)h);
	}

	private int NormaliserCoordYAbysse(int coordY)
	{
		int indexStage = ConstantesDimensionAbysse.ObtenirIndexStageDepuisCoordYChunk(coordY, HauteurMax);
		return ConstantesDimensionAbysse.ObtenirCoordYChunkRepresentatifDuStage(indexStage, HauteurMax);
	}

	private int CoordYStageAbysseDepuisYMonde(float yMonde)
	{
		int indexStage = ConstantesDimensionAbysse.ObtenirIndexStageDepuisYMonde(yMonde);
		return ConstantesDimensionAbysse.ObtenirCoordYChunkRepresentatifDuStage(indexStage, HauteurMax);
	}

	private int LocalYDepuisMondeY(int yMonde)
	{
		int h = Mathf.Max(1, HauteurMax);
		int local = yMonde % h;
		if (local < 0) local += h;
		return local;
	}

	private bool EstVideAbysseAttendu(Vector3 observation)
	{
		if (_dimensionReseauActive != (int)DimensionJeu.Abysse)
			return false;
		if (ConstantesDimensionAbysse.EstDansTrouNoirXZ(observation.X, observation.Z))
			return true;
		if (JoueurEnModeVolCreatif())
			return true;
		return false;
	}

	private void RemplirCoordYCollisionAutourPointMonde(float yMonde, HashSet<int> sortie)
	{
		if (sortie == null)
			return;
		sortie.Clear();
		int stageCourant = ObtenirIndexPalierAbysse(yMonde);
		sortie.Add(ConstantesDimensionAbysse.ObtenirCoordYChunkRepresentatifDuStage(stageCourant, HauteurMax));
		// Quand on frôle une frontière d'étage, on autorise aussi le voisin immédiat.
		float taillePalier = Mathf.Max(1f, ConstantesDimensionAbysse.TaillePalierMetres);
		float yDansStage = yMonde - (stageCourant * taillePalier);
		if (yDansStage <= 8f)
			sortie.Add(ConstantesDimensionAbysse.ObtenirCoordYChunkRepresentatifDuStage(stageCourant - 1, HauteurMax));
		if ((taillePalier - yDansStage) <= 8f)
			sortie.Add(ConstantesDimensionAbysse.ObtenirCoordYChunkRepresentatifDuStage(stageCourant + 1, HauteurMax));
	}

	private int ObtenirIndexPalierAbysse(float yMonde)
	{
		return ConstantesDimensionAbysse.ObtenirIndexStageDepuisYMonde(yMonde);
	}

	private void ObtenirPlageCoordYPalierAbysse(int indexPalier, out int coordYMin, out int coordYMax)
	{
		ConstantesDimensionAbysse.ObtenirPlageCoordYChunkDuStage(indexPalier, HauteurMax, out coordYMin, out coordYMax);
	}

	private void AjouterCoordYPalierAbysse(int indexPalier, HashSet<int> sortie)
	{
		sortie.Add(ConstantesDimensionAbysse.ObtenirCoordYChunkRepresentatifDuStage(indexPalier, HauteurMax));
	}

	private void RemplirCoordYActifsAbysse(Vector3 observation, HashSet<int> sortie)
	{
		if (sortie == null)
			return;
		sortie.Clear();
		int palierCourant = ObtenirIndexPalierAbysse(observation.Y);
		// Mode 2D par étages: fenêtre fixe courant ±N.
		AjouterCoordYPalierAbysse(palierCourant, sortie);
		int demiFenetre = Mathf.Max(0, ConstantesDimensionAbysse.ObtenirDemiFenetrePaliersActifs(observation.X, observation.Z));
		for (int i = 1; i <= demiFenetre; i++)
		{
			AjouterCoordYPalierAbysse(palierCourant - i, sortie);
			AjouterCoordYPalierAbysse(palierCourant + i, sortie);
		}
	}

	private void RemplirCoordYPrioritairesAbysse(Vector3 observation, HashSet<int> sortie)
	{
		if (sortie == null)
			return;
		RemplirCoordYActifsAbysse(observation, sortie);
		_coordYCollisionAbysseTravail.Clear();
		RemplirCoordYCollisionAutourPointMonde(observation.Y, _coordYCollisionAbysseTravail);
		foreach (int yCollision in _coordYCollisionAbysseTravail)
			sortie.Add(yCollision);
	}

	private bool EstCoordYDansFenetrePaliersAbysse(int coordY, Vector3 observation)
	{
		int palierChunk = ConstantesDimensionAbysse.ObtenirIndexStageDepuisCoordYChunk(coordY, HauteurMax);
		int palierObservation = ObtenirIndexPalierAbysse(observation.Y);
		int ecart = Mathf.Abs(palierChunk - palierObservation);
		int demiFenetre = ConstantesDimensionAbysse.ObtenirDemiFenetrePaliersActifs(observation.X, observation.Z);
		return ecart <= Mathf.Max(0, demiFenetre);
	}

	private ChunkData TrouverCoucheAbysseColonne(Vector2I coord)
	{
		foreach (var kv in _chunksDataAbysse3D)
		{
			if (kv.Key.X == coord.X && kv.Key.Z == coord.Y)
				return kv.Value;
		}
		return null;
	}

	private void RetirerChunkDataAbysse(Vector3I cle, ChunkData data)
	{
		_chunksDataAbysse3D.Remove(cle);
		Vector3I cleFlore = CleFlorePourChunkData(data);
		_setFloreDifferee.Remove(cleFlore);
		_fileFloreDifferee.Remove(cleFlore);
		_frameEnqueueFlore.Remove(cleFlore);
		RetirerDeFileSolidification(data);
		_setSolidificationUrgente.Remove(data);
		_setSolidificationNormale.Remove(data);
		lock (_lockFileAttenteMaths)
			_fileAttenteMathsData.RemoveAll(entree => ReferenceEquals(entree.data, data));
		if (_chunksData.TryGetValue(data.Coordonnees, out var courant2D) && ReferenceEquals(courant2D, data))
		{
			ChunkData remplacement = TrouverCoucheAbysseColonne(data.Coordonnees);
			if (remplacement != null)
				_chunksData[data.Coordonnees] = remplacement;
			else
			{
				_chunksData.Remove(data.Coordonnees);
				_chunksACharger.Remove(data.Coordonnees);
				NettoyerRegistreReconstruction(data.Coordonnees);
			}
		}
		RetirerTravauxEnAttentePourChunk(data);
		data.LibérerRids();
		data.LibererDonneesVoxel();
	}

	private void PurgerChunksAbysseHorsFenetre(Vector3 positionObservation, Vector3 positionJoueur)
	{
		if (_chunksDataAbysse3D.Count == 0)
			return;

		int rayonDetail = RayonChargementChunksActif();
		float seuilEvictionCarree = (rayonDetail + 2) * (rayonDetail + 2);
		int rayonProtection = ObtenirRayonSecuriteSolActif();
		float seuilProtectionCarree = rayonProtection * rayonProtection;
		_coordYCollisionAbysseTravail.Clear();
		RemplirCoordYCollisionAutourPointMonde(positionJoueur.Y, _coordYCollisionAbysseTravail);
		_clesChunksAbysseARetirerTemp.Clear();
		foreach (var kv in _chunksDataAbysse3D)
		{
			Vector2I coordXZ = new Vector2I(kv.Key.X, kv.Key.Z);
			float dist2 = DistanceCarreeAuJoueur(coordXZ, positionJoueur);
			if (dist2 <= seuilProtectionCarree)
				continue;
			bool yDansFenetre = EstCoordYDansFenetrePaliersAbysse(kv.Key.Y, positionObservation)
				|| EstCoordYDansFenetrePaliersAbysse(kv.Key.Y, positionJoueur)
				|| _coordYCollisionAbysseTravail.Contains(kv.Key.Y);
			if (dist2 > seuilEvictionCarree || !yDansFenetre)
			{
				if (ActiverDiagnosticCollisionAbysse && _coordYCollisionAbysseTravail.Contains(kv.Key.Y))
					GD.Print($"ZERO-K ABYSSE DIAG PURGE: suppression potentielle couche collision y={kv.Key.Y} coord=({kv.Key.X},{kv.Key.Z}) dist2={dist2:F1}");
				_clesChunksAbysseARetirerTemp.Add(kv.Key);
			}
		}

		for (int i = 0; i < _clesChunksAbysseARetirerTemp.Count; i++)
		{
			Vector3I cle = _clesChunksAbysseARetirerTemp[i];
			if (_chunksDataAbysse3D.TryGetValue(cle, out var data) && data != null)
				RetirerChunkDataAbysse(cle, data);
		}
	}

	public void ReinitialiserTousLesChunksLocaux()
	{
		var dejaLibere = new HashSet<ChunkData>();
		foreach (var kv in _chunksData)
		{
			if (kv.Value != null && dejaLibere.Add(kv.Value))
				kv.Value.LibérerRids();
		}
		foreach (var kv in _chunksDataAbysse3D)
		{
			if (kv.Value != null && dejaLibere.Add(kv.Value))
				kv.Value.LibérerRids();
		}
		_chunksData.Clear();
		_chunksDataAbysse3D.Clear();
		_chunksACharger.Clear();
		_sectionsAReconstruire.Clear();
		_fileAttenteSolidification.Clear();
		_setSolidificationNormale.Clear();
		_fileAttenteSolidificationUrgente.Clear();
		_setSolidificationUrgente.Clear();
		_fileFloreDifferee.Clear();
		_setFloreDifferee.Clear();
		_frameEnqueueFlore.Clear();
		_fileAttenteMathsData.Clear();
		_curseurSelectionRequetes = 0;
		_curseurSelectionSolidification = 0;
		_indexCullingScan = 0;
		_indexDormanceScan = 0;
		_timerTrimAbysse = 0f;
		_demandesAbysseFrameDerniereEmission.Clear();
	}

	/// <summary>
	/// Émet IMMÉDIATEMENT les requêtes pour les chunks manquants dans un petit rayon autour du joueur.
	/// Appelée même quand le budget frame est dépassé pour éviter les chutes dans le vide.
	/// </summary>
	private void GarantirRequetesChunksProcheJoueur(Vector3 positionObservation, Vector2I chunkObservationActuel)
	{
		bool modeAbysse = _dimensionReseauActive == (int)DimensionJeu.Abysse;
		bool zoneCritiqueAbysse = false;
		float vitesseXZ = 0f;
		if (EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef))
		{
			Vector3 v = joueurRef.Velocity;
			vitesseXZ = Mathf.Sqrt(v.X * v.X + v.Z * v.Z);
			if (modeAbysse)
			{
				bool enVideAttendu = EstVideAbysseAttendu(joueurRef.GlobalPosition);
				bool localeOk = AbysseCollisionLocaleActive(joueurRef.GlobalPosition);
				// Pas d’urgence max dès qu’on chute : évite rafales de requêtes/solidifications (micro-freezes).
				zoneCritiqueAbysse = !localeOk && !enVideAttendu;
			}
		}
		// Rayon minimal : couvre au moins le RayonGrilleMinSpawnPret + anticipation courte dans la direction de déplacement.
		int bonusVitesse = (modeAbysse && vitesseXZ >= 4.0f) ? 1 : 0;
		int rayonMin = Mathf.Clamp(RayonGrilleMinSpawnPret + (modeAbysse ? 2 : 1) + bonusVitesse, 1, Mathf.Max(1, RayonDormancePhysique + 2));
		int budgetRequetesForce = modeAbysse
			? (zoneCritiqueAbysse ? (vitesseXZ >= 4.0f ? 20 : 14) : (vitesseXZ >= 4.0f ? 12 : 8))
			: 6;
		if (modeAbysse && JoueurEnModeVolCreatif())
			budgetRequetesForce = Mathf.Min(budgetRequetesForce, 6);
		int emises = 0;
		for (int dx = -rayonMin; dx <= rayonMin && emises < budgetRequetesForce; dx++)
		{
			for (int dz = -rayonMin; dz <= rayonMin && emises < budgetRequetesForce; dz++)
			{
				Vector2I cible = new Vector2I(chunkObservationActuel.X + dx, chunkObservationActuel.Y + dz);
				if (ChunkDisponiblePourObservation(cible, positionObservation)) continue;
				DemanderChunk(cible);
				emises++;
			}
		}
		// Anticipation chute : si le joueur se déplace vite, pousser aussi le chunk sous sa trajectoire.
		if (EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRefAnticipation) && joueurRefAnticipation.Velocity.LengthSquared() > 1f && emises < budgetRequetesForce)
		{
			Vector3 cibleAnticipee = positionObservation + joueurRefAnticipation.Velocity.Normalized() * TailleChunk;
			Vector2I chunkAnticipe = Gestionnaire_Monde.WorldToChunkCoord(cibleAnticipee, TailleChunk);
			if (!ChunkDisponiblePourObservation(chunkAnticipe, positionObservation))
			{
				DemanderChunk(chunkAnticipe);
			}
		}
	}
}
