using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public partial class Monde_Client : Node3D
{
	/// <summary>Position d'observation (caméra ou joueur). Utilisée par le radar et par les chunks pour la visibilité du gazon.</summary>
	public Vector3 ObtenirPositionObservation()
	{
		Camera3D cam = ObtenirCameraObservation();
		if (cam != null)
			return cam.GlobalPosition;
		return EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef) ? joueurRef.GlobalPosition : Vector3.Zero;
	}

	/// <summary>Position d'interaction flore : privilégie le corps joueur (contact sol), sinon fallback observation.</summary>
	public Vector3 ObtenirPositionInteractionFlore()
	{
		return EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef) ? joueurRef.GlobalPosition : ObtenirPositionObservation();
	}

	/// <summary>Position utilisée par le radar (chunk le plus proche). Utilise la caméra active si disponible (caméra libre), sinon le corps du joueur.</summary>
	private Vector2I ObtenirCoordonneesChunkJoueur()
	{
		Vector3 pos = ObtenirPositionObservation();
		return Gestionnaire_Monde.WorldToChunkCoord(pos, TailleChunk);
	}

	private void ActualiserVisibiliteEtTriChunks(Vector3 positionObservation)
	{
		if (_radarEnCours) return;

		_radarEnCours = true;
		ulong debutRadarUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		Vector2 posObsV2 = new Vector2(positionObservation.X / (float)TailleChunk, positionObservation.Z / (float)TailleChunk);
		Vector2I chunkCentreRadar = Gestionnaire_Monde.WorldToChunkCoord(positionObservation.X, positionObservation.Z, TailleChunk);
		int cjX = chunkCentreRadar.X;
		int cjZ = chunkCentreRadar.Y;
		int rayonRadar = RayonRadarPreparationActif();
		HashSet<Vector2I> chunksCharges = new HashSet<Vector2I>(_chunksData.Keys);
		List<Vector2I> copieChunksACharger = new List<Vector2I>(_chunksACharger);

		Task.Run(() =>
		{
			HashSet<Vector2I> dejaVu = new HashSet<Vector2I>(copieChunksACharger);
			foreach (var c in chunksCharges) dejaVu.Add(c);
			int rayonInterieur = Mathf.Max(0, rayonRadar - EpaisseurAnneauRadar);
			int ajoutes = 0;
			for (int dx = -rayonRadar; dx <= rayonRadar && ajoutes < _maxAjoutsRadarParPasseDyn; dx++)
				for (int dz = -rayonRadar; dz <= rayonRadar && ajoutes < _maxAjoutsRadarParPasseDyn; dz++)
				{
					int adx = Mathf.Abs(dx);
					int adz = Mathf.Abs(dz);
					Vector2I coord = new Vector2I(cjX + dx, cjZ + dz);
					// Anneau : on ne saute le « cœur » que pour les chunks déjà chargés ou déjà en file (évite de re-trier l’intérieur).
					// Sinon les cases intérieures manquantes n’étaient jamais ajoutées → halo vide / montagnes qui ne chargent pas.
					bool dansCoeur = adx < rayonInterieur && adz < rayonInterieur;
					if (dansCoeur && dejaVu.Contains(coord))
						continue;
					if (dejaVu.Add(coord))
					{
						copieChunksACharger.Add(coord);
						ajoutes++;
					}
				}

			// Tri radial strict : distance au carré (pas de new Vector2 par comparaison — évite des milliers d'allocations).
			float ox = posObsV2.X, oy = posObsV2.Y;
			copieChunksACharger.Sort((a, b) =>
			{
				float da = (a.X - ox) * (a.X - ox) + (a.Y - oy) * (a.Y - oy);
				float db = (b.X - ox) * (b.X - ox) + (b.Y - oy) * (b.Y - oy);
				return da.CompareTo(db);
			});

			Callable.From(() =>
			{
				AppliquerNouveauTriRadar(copieChunksACharger);
				if (ActiverProfilagePerfMondeClient)
					PerfBudgetMonitor.End("MondeClient/RadarBuild", debutRadarUs);
			}).CallDeferred();
		});
	}

	private void AppliquerNouveauTriRadar(List<Vector2I> nouvelleListeTriee)
	{
		ulong debutApplyRadarUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		if (nouvelleListeTriee == null || nouvelleListeTriee.Count == 0)
		{
			_chunksACharger.Clear();
			_radarEnCours = false;
			if (ActiverProfilagePerfMondeClient)
				PerfBudgetMonitor.End("MondeClient/RadarApply", debutApplyRadarUs);
			return;
		}
		int rayonRadar = RayonRadarPreparationActif();
		int cap = Mathf.Clamp((2 * rayonRadar + 1) * (2 * rayonRadar + 1), 256, 65536);
		int n = Mathf.Min(cap, nouvelleListeTriee.Count);
		_chunksACharger.Clear();
		if (_chunksACharger.Capacity < n)
			_chunksACharger.Capacity = n;
		for (int i = 0; i < n; i++)
			_chunksACharger.Add(nouvelleListeTriee[i]);
		_radarEnCours = false;
		if (ActiverProfilagePerfMondeClient)
			PerfBudgetMonitor.End("MondeClient/RadarApply", debutApplyRadarUs);
		// Le dépilage est fait dans _PhysicsProcess (usine en continu, 60 TPS)
	}

	/// <summary>Dormance physique progressive: limite les transitions BodySetSpace par frame pour supprimer les micro-spikes.</summary>
	private void ActualiserDormanceChunks(int obsChunkX, int obsChunkZ, int maxTransitions)
	{
		World3D world = GetWorld3D();
		if (world == null) return;
		Rid space = world.Space;
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			// En Abysse multi-couches, on évite d'endormir agressivement par vue 2D:
			// cela peut désactiver la mauvaise couche Y et provoquer des chutes.
			Vector3 obsMonde = _joueur?.GlobalPosition ?? ObtenirPositionObservation();
			Vector2I cpAbysse = Gestionnaire_Monde.WorldToChunkCoord(obsMonde, TailleChunk);
			_coordYActifsAbysseTravail.Clear();
			RemplirCoordYPrioritairesAbysse(obsMonde, _coordYActifsAbysseTravail);
			int rayon = ObtenirRayonSecuriteSolActif();
			foreach (int y in _coordYActifsAbysseTravail)
			{
				for (int dx = -rayon; dx <= rayon; dx++)
				{
					for (int dz = -rayon; dz <= rayon; dz++)
					{
						Vector3I cle = new Vector3I(cpAbysse.X + dx, y, cpAbysse.Y + dz);
						if (!_chunksDataAbysse3D.TryGetValue(cle, out var d) || d == null) continue;
						if (d.PhysicsBodyRID.IsValid)
						{
							if (d.Dormant)
							{
								d.Dormant = false;
								PhysicsServer3D.Singleton.BodySetSpace(d.PhysicsBodyRID, space);
								if (d.EstEnFileSolidification)
									RetirerDeFileSolidification(d);
								SynchroniserFloreDesQueCollisionChunkActive(d);
							}
						}
						else if (!d.EstEnFileSolidification)
						{
							RetirerDeFileSolidification(d);
							EnfilerSolidificationUrgenteUnique(d);
							d.EstEnFileSolidification = true;
						}
					}
				}
			}
			return;
		}

		int transitions = 0;
		int limite = Mathf.Max(1, maxTransitions);
		int rayonReveil = Mathf.Max(1, RayonDormancePhysique);

		// Réveil portail (0,0) : hors budget transitions (sous FPS bas le passage joueur épuisait la limite avant le chunk portail).
		void ReveillerChunkPourPortailNexusSansBudget(ChunkData data)
		{
			if (data.Dormant && data.PhysicsBodyRID.IsValid)
			{
				data.Dormant = false;
				PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, space);
				if (data.EstEnFileSolidification)
					RetirerDeFileSolidification(data);
				SynchroniserFloreDesQueCollisionChunkActive(data);
			}
			else if (!data.PhysicsBodyRID.IsValid && !data.EstEnFileSolidification)
			{
				RetirerDeFileSolidification(data);
				EnfilerSolidificationUrgenteUnique(data);
				data.EstEnFileSolidification = true;
			}
		}

		// La dormance suit la caméra (obsChunk) : garder le sol actif autour du corps joueur même si on regarde ailleurs.
		if (EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef))
		{
			Vector2I cp = Gestionnaire_Monde.WorldToChunkCoord(joueurRef.GlobalPosition, TailleChunk);
			for (int dx = -rayonReveil; dx <= rayonReveil; dx++)
			{
				for (int dz = -rayonReveil; dz <= rayonReveil; dz++)
				{
					var coord = new Vector2I(cp.X + dx, cp.Y + dz);
					if (!_chunksData.TryGetValue(coord, out ChunkData d)) continue;
					ReveillerChunkPourPortailNexusSansBudget(d);
				}
			}
		}

		bool BasculerDormanceChunk(ChunkData data, bool dormantCible)
		{
			if (transitions >= limite) return false;
			data.Dormant = dormantCible;
			if (data.PhysicsBodyRID.IsValid)
			{
				if (dormantCible)
				{
					PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, default(Rid));
					transitions++;
					if (data.EstEnFileSolidification)
					{
						RetirerDeFileSolidification(data);
					}
				}
				else
				{
					// Réveil dynamique : activer les collisions tout de suite dans le rayon (pas de file).
					PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, space);
					transitions++;
					if (data.EstEnFileSolidification)
					{
						RetirerDeFileSolidification(data);
					}
					SynchroniserFloreDesQueCollisionChunkActive(data);
				}
			}
			else if (!dormantCible)
			{
				// Corps non créé (lazy) : enfile pour création/activation progressive.
				if (!data.EstEnFileSolidification)
				{
					AjouterEnFileSolidification(data);
				}
			}
			return transitions < limite;
		}

		// PASSAGE A-portail (priorité absolue) : XZ monde (0,0). Hors budget transitions — indispensable pour le raycast du portail Nexus.
		if (EstDimensionActiveeAvecPortailNexusAuChunkOrigine())
		{
			for (int dx = -rayonReveil; dx <= rayonReveil; dx++)
			{
				for (int dz = -rayonReveil; dz <= rayonReveil; dz++)
				{
					var coord = new Vector2I(dx, dz);
					if (!_chunksData.TryGetValue(coord, out var dataPortail)) continue;
					ReveillerChunkPourPortailNexusSansBudget(dataPortail);
				}
			}
		}

		// PASSAGE A (priorité sécurité): réveille le rayon proche du joueur (budget transitions).
		for (int dx = -rayonReveil; dx <= rayonReveil; dx++)
		{
			for (int dz = -rayonReveil; dz <= rayonReveil; dz++)
			{
				if (transitions >= limite) return;
				var coord = new Vector2I(obsChunkX + dx, obsChunkZ + dz);
				if (!_chunksData.TryGetValue(coord, out var data)) continue;

				if (data.Dormant)
				{
					if (!BasculerDormanceChunk(data, false)) return;
				}
				else if (!data.PhysicsBodyRID.IsValid && !data.EstEnFileSolidification)
				{
					// Garantit qu'un chunk proche sans body est solidifié rapidement.
					RetirerDeFileSolidification(data);
					EnfilerSolidificationUrgenteUnique(data);
					data.EstEnFileSolidification = true;
				}
			}
		}

		// PASSAGE B (secondaire): endort le lointain avec le budget restant.
		AssurerCacheCoordsChunks();
		int total = _cacheCoordsChunks.Count;
		if (total == 0) return;
		int evaluations = 0;
		int maxEvaluations = Mathf.Max(limite * 4, 96);
		if (_niveauUrgencePerf >= 2)
			maxEvaluations = Mathf.Max(limite * 2, 56);
		else if (_niveauUrgencePerf == 1)
			maxEvaluations = Mathf.Max(limite * 3, 72);
		if (ModeSurvieFpsAgressif && _fpsMoyenneAuto < 55f)
			maxEvaluations = Mathf.Max(48, Mathf.RoundToInt(maxEvaluations * 0.75f));
		while (evaluations < maxEvaluations && transitions < limite)
		{
			total = _cacheCoordsChunks.Count;
			if (total <= 0) break;
			if (_indexDormanceScan >= total) _indexDormanceScan = 0;
			if ((uint)_indexDormanceScan >= (uint)_cacheCoordsChunks.Count) break;
			Vector2I coord = _cacheCoordsChunks[_indexDormanceScan];
			_indexDormanceScan++;
			evaluations++;
			if (!_chunksData.TryGetValue(coord, out var data)) continue;
			int dx = Mathf.Abs(data.Coordonnees.X - obsChunkX);
			int dz = Mathf.Abs(data.Coordonnees.Y - obsChunkZ);
			bool doitDormir = dx > RayonDormancePhysique || dz > RayonDormancePhysique;
			if (doitDormir && EstDimensionActiveeAvecPortailNexusAuChunkOrigine()
				&& ChunkEstDansDisquePhysiquePortailVersApisara(data.Coordonnees))
				continue;
			if (!doitDormir || data.Dormant) continue;
			if (!BasculerDormanceChunk(data, true)) return;
		}
	}
}
