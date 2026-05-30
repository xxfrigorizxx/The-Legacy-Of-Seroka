using Godot;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

public partial class Chunk_Serveur : RefCounted
{
	/// <summary>Distance radiale warping APISARA (plaines / muraille) — partagée hauteur + masques biome.</summary>
	private float CalculerDistanceProfilAbysse(float xGlobal, float zGlobal)
	{
		float distance = Mathf.Sqrt(xGlobal * xGlobal + zGlobal * zGlobal);
		float angle = Mathf.Atan2(zGlobal, xGlobal);

		// Décale radialement les frontières de zones pour casser l'anneau parfait
		// et obtenir une muraille plus organique (bosses, creux, pointes irrégulières).
		float modulationAngulaire =
			Mathf.Sin(angle * 3.1f + 0.8f) * 38f +
			Mathf.Sin(angle * 6.7f - 1.4f) * 22f;
		float bruitMacroAnneau = _noiseSurface.GetNoise2D(xGlobal * 0.0018f + 2600f, zGlobal * 0.0018f + 2600f) * 54f;
		float bruitWarpAnneau = _noiseErosion.GetNoise2D(xGlobal * 0.0034f - 3700f, zGlobal * 0.0034f - 3700f) * 28f;
		return distance + modulationAngulaire + bruitMacroAnneau + bruitWarpAnneau;
	}

	/// <summary>Les deux plaines herbe (sanctuaire intérieur + plaine extérieure), hors trou et hors muraille.</summary>
	private bool EstPlaineJungleAbysse(float xGlobal, float zGlobal)
	{
		if (!_generationAbysseActive) return false;
		float dp = CalculerDistanceProfilAbysse(xGlobal, zGlobal);
		if (dp <= AbyssRayonTrouNoir) return false;
		if (dp > AbyssRayonTrouNoir && dp <= AbyssRayonX) return true;
		if (dp >= AbyssRayonY && dp <= AbyssRayonZ) return true;
		return false;
	}

	private int CalculerHauteurTerrainAbysse(int xGlobal, int zGlobal) =>
		ApisaraHauteurTerrain.CalculerSurfaceMonde(
			xGlobal, zGlobal,
			_noiseSurface, _noiseErosion, _noiseCavernes, _noiseRivieres,
			_noiseAbysseChaos3D, _noiseAbysseChaosDetail3D);

	/// <summary>Même masque que la rampe spirale ciselée dans le trou ; 0 = hors bande. Sert aussi à ne pas poser de gazon sur la descente.</summary>
	private float CalculerIntensiteSpiraleDescenteAbysse(float xGlobal, float yGlobal, float zGlobal)
	{
		if (!_generationAbysseActive) return 0f;
		if (yGlobal > AbyssYSpiraleTop || yGlobal < AbyssYSpiraleBottom) return 0f;
		float distance = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal));
		if (distance < AbyssRayonSpiraleMin || distance > AbyssRayonSpiraleMax) return 0f;

		float angle = Mathf.Atan2(zGlobal, xGlobal);
		float warpHelicoidal = _noiseErosion.GetNoise3D(xGlobal * 0.0062f + 4100f, yGlobal * 0.0093f - 1700f, zGlobal * 0.0062f - 2600f) * 0.85f;
		float phase = angle * 6.2f + yGlobal * 0.018f + warpHelicoidal;
		float signalSpirale = 0.5f + (0.5f * Mathf.Cos(phase));
		float masqueSpirale = Mathf.SmoothStep(0.58f, 0.92f, signalSpirale);

		float n3dBrut = _noiseAbysseSpirale3D.GetNoise3D(xGlobal, yGlobal, zGlobal);
		float n3d = Mathf.Pow(Mathf.Clamp((n3dBrut + 1f) * 0.5f, 0f, 1f), 1.25f);

		float attenuationRayon = Mathf.Clamp((distance - AbyssRayonSpiraleMin) / Mathf.Max(1f, AbyssRayonTrouNoir - AbyssRayonSpiraleMin), 0f, 1f);
		attenuationRayon *= attenuationRayon;

		float tDescente = Mathf.Clamp((AbyssYSpiraleTop - yGlobal) / Mathf.Max(1f, AbyssYSpiraleTop - AbyssYSpiraleBottom), 0f, 1f);
		float gainDescente = Mathf.Lerp(0.85f, 1.22f, tDescente);

		float varianteOrg = Mathf.Lerp(0.72f, 1.28f, Mathf.Clamp((_noiseAbysseChaosDetail3D.GetNoise3D(xGlobal * 0.31f, yGlobal * 0.41f, zGlobal * 0.31f) + 1f) * 0.5f, 0f, 1f));
		return masqueSpirale * n3d * attenuationRayon * gainDescente * varianteOrg;
	}

	private bool EvaluerExtrusionParoiAbysse(float xGlobal, float yGlobal, float zGlobal, out float profondeurInward)
	{
		profondeurInward = 0f;
		float intensite = CalculerIntensiteSpiraleDescenteAbysse(xGlobal, yGlobal, zGlobal);
		if (intensite <= 0.02f) return false;

		float distance = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal));
		profondeurInward = Mathf.Lerp(AbyssExtrusionMin, AbyssExtrusionMax, Mathf.Clamp(intensite, 0f, 1f));
		float seuilInterieur = AbyssRayonTrouNoir - profondeurInward;
		return distance >= seuilInterieur && distance <= AbyssRayonTrouNoir;
	}

	/// <summary>
	/// Muraille porteuse continue du gouffre APISARA.
	/// Sert de "coeur plein" pour éviter les fentes entre extrusions décoratives.
	/// </summary>
	private bool EvaluerNoyauMurailleContinueAbysse(float xGlobal, float yGlobal, float zGlobal, out float profondeurInward)
	{
		profondeurInward = 0f;
		if (!_generationAbysseActive) return false;
		if (yGlobal < AbyssYAnneauTransitionBottom || yGlobal > AbyssYSpiraleTop) return false;

		float distance = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal));
		if (distance > AbyssRayonTrouNoir) return false;

		float tVertical = Mathf.Clamp((yGlobal - AbyssYAnneauTransitionBottom) / Mathf.Max(1e-3f, AbyssYSpiraleTop - AbyssYAnneauTransitionBottom), 0f, 1f);
		tVertical = tVertical * tVertical * (3f - 2f * tVertical);
		float bruitContinuite = Mathf.Clamp((_noiseAbysseChaosDetail3D.GetNoise3D(
			xGlobal * 0.004f + 1300f,
			yGlobal * 0.006f - 900f,
			zGlobal * 0.004f - 1300f) + 1f) * 0.5f, 0f, 1f);
		float profondeurBase = Mathf.Lerp(48f, 30f, tVertical) * Mathf.Lerp(0.88f, 1.12f, bruitContinuite);
		profondeurInward = Mathf.Clamp(profondeurBase, 24f, 58f);

		float seuilInterieur = AbyssRayonTrouNoir - profondeurInward;
		return distance >= seuilInterieur && distance <= AbyssRayonTrouNoir;
	}

	/// <summary>
	/// Anneau [-510,-450[ : pics pierre triangulaires (vue de côté) ancrés au mur, herbe en passe séparée.
	/// Profondeur maximale vers Y=-450 (base du pic côté mur), pointe vers Y=-510.
	/// </summary>
	private bool EvaluerExtrusionAnneauAbysse(float xGlobal, float yGlobal, float zGlobal, out float profondeurInward)
	{
		profondeurInward = 0f;
		if (!_generationAbysseActive) return false;
		if (yGlobal >= AbyssYSpiraleBottom || yGlobal < AbyssYAnneauTransitionBottom) return false;

		float distance = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal));
		if (distance > AbyssRayonTrouNoir) return false;

		float angle = Mathf.Atan2(zGlobal, xGlobal);
		float angleNorm = Mathf.PosMod(angle + Mathf.Pi, Mathf.Tau) / Mathf.Tau;
		float xMur = Mathf.Cos(angle) * AbyssRayonTrouNoir;
		float zMur = Mathf.Sin(angle) * AbyssRayonTrouNoir;
		float yAniso = yGlobal * AbyssChaosYScale;

		float warpAng = _noiseAbysseChaosDetail3D.GetNoise3D(xMur * 0.019f + 120f, yAniso, zMur * 0.019f - 120f) * 0.075f;
		float densitePic = AbyssPicAnneauNombre * Mathf.Lerp(0.78f, 1.22f, Mathf.Clamp((_noiseAbysseChaos3D.GetNoise2D(xMur * 0.0022f + 900f, zMur * 0.0022f - 900f) + 1f) * 0.5f, 0f, 1f));
		float scaled = (angleNorm + warpAng) * densitePic;
		float frac = scaled - Mathf.Floor(scaled);
		float distCreneau = Mathf.Min(frac, 1f - frac);
		float porteAngulaire = 1f - Mathf.SmoothStep(0.035f, 0.13f, distCreneau);

		float tVert = (yGlobal - AbyssYAnneauTransitionBottom) / Mathf.Max(1e-3f, AbyssYSpiraleBottom - AbyssYAnneauTransitionBottom);
		tVert = Mathf.Clamp(tVert, 0f, 1f);
		tVert = tVert * tVert * (3f - 2f * tVert);

		float bruitMacro = Mathf.Clamp((_noiseAbysseChaos3D.GetNoise3D(xMur, yAniso, zMur) + 1f) * 0.5f, 0f, 1f);
		float bruitDetail = Mathf.Clamp((_noiseAbysseChaosDetail3D.GetNoise3D(xMur * 1.75f + 700f, yAniso * 0.8f - 220f, zMur * 1.75f - 700f) + 1f) * 0.5f, 0f, 1f);
		float amplitude = Mathf.Lerp(0.42f, 1f, bruitMacro) * (0.72f + 0.28f * bruitDetail);

		float profondeurBase = Mathf.Lerp(AbyssChaosExtrusionMin + 24f, AbyssChaosExtrusionMax, amplitude);
		profondeurBase *= tVert * porteAngulaire;

		float angleSortieSpirale = ObtenirAngleSortieSpirale();
		float ecartAngle = ObtenirEcartAngulaireAbsolu(angle, angleSortieSpirale);
		float lipAngle = 1f - Mathf.SmoothStep(0.07f, 0.24f, ecartAngle);
		float lipAlt = 1f - Mathf.SmoothStep(10f, 26f, Mathf.Abs(yGlobal - AbyssYSpiraleBottom));
		float lip = lipAngle * lipAngle * (3f - 2f * lipAngle) * lipAlt * lipAlt * (3f - 2f * lipAlt);
		profondeurBase = Mathf.Max(profondeurBase, lip * AbyssPicLipSortieProfondeurMax);

		profondeurInward = Mathf.Clamp(profondeurBase, 0f, AbyssChaosExtrusionMax);
		if (profondeurInward < 1.5f) return false;

		float seuilInterieur = AbyssRayonTrouNoir - profondeurInward;
		return distance >= seuilInterieur && distance <= AbyssRayonTrouNoir;
	}

	/// <summary>
	/// Hors rampe spirale : pics supplémentaires plus rares entre Y=-450 et le haut de la spirale (Y=20).
	/// </summary>
	private bool EvaluerPicSupplementSpiraleAbysse(float xGlobal, float yGlobal, float zGlobal, out float profondeurInward)
	{
		profondeurInward = 0f;
		if (!_generationAbysseActive) return false;
		if (yGlobal <= AbyssYSpiraleBottom || yGlobal > AbyssYSpiraleTop) return false;

		float distance = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal));
		if (distance > AbyssRayonTrouNoir) return false;

		float angle = Mathf.Atan2(zGlobal, xGlobal);
		float angleNorm = Mathf.PosMod(angle + Mathf.Pi, Mathf.Tau) / Mathf.Tau;
		float xMur = Mathf.Cos(angle) * AbyssRayonTrouNoir;
		float zMur = Mathf.Sin(angle) * AbyssRayonTrouNoir;
		float yAniso = yGlobal * AbyssChaosYScale;

		float tProximiteFinSpirale = 1f - (yGlobal - AbyssYSpiraleBottom) / Mathf.Max(1e-3f, AbyssYSpiraleTop - AbyssYSpiraleBottom);
		tProximiteFinSpirale = Mathf.Clamp(tProximiteFinSpirale, 0f, 1f);
		tProximiteFinSpirale = tProximiteFinSpirale * tProximiteFinSpirale * (3f - 2f * tProximiteFinSpirale);
		if (tProximiteFinSpirale < 0.06f) return false;

		float warpAng = _noiseAbysseChaosDetail3D.GetNoise3D(xMur * 0.017f - 900f, yAniso * 1.1f, zMur * 0.017f + 900f) * 0.09f;
		float scaled = (angleNorm + warpAng) * AbyssPicSpiraleNombre;
		float frac = scaled - Mathf.Floor(scaled);
		float distCreneau = Mathf.Min(frac, 1f - frac);
		float porteAngulaire = 1f - Mathf.SmoothStep(0.05f, 0.19f, distCreneau);

		float bruit = Mathf.Clamp((_noiseAbysseChaos3D.GetNoise3D(xMur * 0.35f, yAniso + 80f, zMur * 0.35f) + 1f) * 0.5f, 0f, 1f);
		float profondeurMax = Mathf.Lerp(32f, 138f, bruit) * tProximiteFinSpirale * porteAngulaire;

		profondeurInward = Mathf.Clamp(profondeurMax, 0f, 165f);
		if (profondeurInward < 2f) return false;

		float seuilInterieur = AbyssRayonTrouNoir - profondeurInward;
		return distance >= seuilInterieur && distance <= AbyssRayonTrouNoir;
	}

	private static float ObtenirAngleSortieSpirale()
	{
		float angle = (-AbyssYSpiraleBottom * 0.018f) / 6.2f;
		return Mathf.PosMod(angle, Mathf.Tau);
	}

	private static float ObtenirEcartAngulaireAbsolu(float a, float b)
	{
		float d = Mathf.PosMod(a - b + Mathf.Pi, Mathf.Tau) - Mathf.Pi;
		return Mathf.Abs(d);
	}

	private void AppliquerBiomeParasiteCornichesAbysse()
	{
		if (!_generationAbysseActive || _densities == null || _materials == null) return;
		if (!EstChunkAvecGeometrieExtrusionTrou()) return;
		ObtenirPlageIndiceYMonde(AbyssYAnneauTransitionBottom - 8f, AbyssYSpiraleTop + 32f, out int yDebut, out int yFin);
		if (yDebut > yFin) return;
		for (int x = 0; x <= TailleChunk; x++)
		{
			for (int y = yDebut; y <= yFin; y++)
			{
				float globalY = ChunkOffsetY * HauteurMax + y;

				for (int z = 0; z <= TailleChunk; z++)
				{
					if (_densities[x, y, z] <= Isolevel) continue;

					float xGlobal = ChunkOffsetX * TailleChunk + x;
					float zGlobal = ChunkOffsetZ * TailleChunk + z;
					if (!ColonneDansBandeExtrusionTrou(xGlobal, zGlobal)) continue;
					bool appartientSpirale = EvaluerExtrusionParoiAbysse(xGlobal, globalY, zGlobal, out _);
					bool appartientAnneau = !appartientSpirale && EvaluerExtrusionAnneauAbysse(xGlobal, globalY, zGlobal, out _);
					bool appartientPicSupplement = !appartientSpirale && !appartientAnneau && EvaluerPicSupplementSpiraleAbysse(xGlobal, globalY, zGlobal, out _);
					bool appartientNoyau = EvaluerNoyauMurailleContinueAbysse(xGlobal, globalY, zGlobal, out _);
					if (!appartientSpirale && !appartientAnneau && !appartientPicSupplement && !appartientNoyau) continue;

					bool airAuDessus = y >= HauteurMax
						|| (_densities[x, y + 1, z] <= Isolevel && (_densitiesEau == null || _densitiesEau[x, y + 1, z] <= Isolevel));
					if (!airAuDessus)
					{
						_materials[x, y, z] = 2;
						continue;
					}

					// Le noyau structurel reste roche brute; seuls les reliefs extrudés peuvent devenir praticables/herbe.
					if (appartientNoyau && !appartientSpirale && !appartientAnneau && !appartientPicSupplement)
					{
						_materials[x, y, z] = 2;
						continue;
					}

					bool zonePic = appartientAnneau || appartientPicSupplement;
					bool praticable = EstSurfacePraticableBiomeParasite(x, y, z)
						|| (zonePic && EstSurfacePraticablePicAbysseRelaxee(x, y, z));
					_materials[x, y, z] = praticable ? (byte)1 : (byte)2;
				}
			}
		}
	}

	/// <summary>APISARA : buissons à baies cyan fluorescentes (variante 8) uniquement dans le trou, Y monde [-500, 0], sur gazon existant avec espacement.</summary>
	private void EnsemencerBuissonsFluoTrouAbysse()
	{
		if (!_generationAbysseActive) return;
		float yMin = ConstantesDimensionAbysse.LimiteInferieureHerbeTrouMonde;
		var gazonATraiter = new List<Vector3I>(64);
		foreach (var kv in InventaireFlore)
		{
			if (kv.Value != FloreTypeGazon) continue;
			float yM = kv.Key.Y;
			if (yM < yMin || yM > 0f) continue;
			if (!EstDansTrouNoirAbysseMonde(kv.Key.X, kv.Key.Z)) continue;
			if (!PositionFloreDansChunk(kv.Key)) continue;
			gazonATraiter.Add(kv.Key);
		}
		for (int i = 0; i < gazonATraiter.Count; i++)
		{
			Vector3I pos = gazonATraiter[i];
			float xG = pos.X;
			float zG = pos.Z;
			float distCentre = Mathf.Sqrt((xG * xG) + (zG * zG));
			float chanceBuissonFluo = distCentre <= AbyssRayonPlateauCentralFlore
				? AbyssChanceBuissonFluoCentre
				: AbyssChanceBuissonFluo;
			if (DeterministicRand(xG * 1.03f + pos.Y * 0.019f, zG * 0.97f) > chanceBuissonFluo) continue;
			if (!PeutPlacerBuissonAvecEspacement(pos, 3)) continue;
			InventaireFlore[pos] = ConstruireTypeBuisson(8, true);
		}
	}

	private bool PositionFloreDansChunk(Vector3I posGlobale)
	{
		int lx = posGlobale.X - ChunkOffsetX * TailleChunk;
		int lz = posGlobale.Z - ChunkOffsetZ * TailleChunk;
		return lx >= 0 && lx <= TailleChunk && lz >= 0 && lz <= TailleChunk;
	}

	/// <summary>APISARA : gazon seul sur replats du trou (spirale/pics), sans buissons ni arbres.</summary>
	private void EnsemencerFloreTrouAbysseReplats()
	{
		if (!_generationAbysseActive || _densities == null || _materials == null) return;
		float yMin = ConstantesDimensionAbysse.LimiteInferieureHerbeTrouMonde;
		float yMax = AbyssYSpiraleTop + 28f;
		ObtenirPlageIndiceYMonde(yMin, yMax, out int yIndiceMin, out int yIndiceMax);
		if (yIndiceMin > yIndiceMax) return;
		for (int x = 0; x <= TailleChunk; x++)
		for (int z = 0; z <= TailleChunk; z++)
		{
			float xG = ChunkOffsetX * TailleChunk + x;
			float zG = ChunkOffsetZ * TailleChunk + z;
			if (!EstDansTrouNoirAbysseMonde(xG, zG)) continue;
			float distCentre = Mathf.Sqrt((xG * xG) + (zG * zG));
			bool plateauCentral = distCentre <= AbyssRayonPlateauCentralFlore;
			int ySurface = -1;
			for (int y = yIndiceMax; y >= yIndiceMin; y--)
			{
				if (_densities[x, y, z] <= Isolevel) continue;
				bool airDessus = y >= HauteurMax
					|| (_densities[x, y + 1, z] <= Isolevel && (_densitiesEau == null || _densitiesEau[x, y + 1, z] <= Isolevel));
				if (!airDessus) continue;
				ySurface = y;
				break;
			}
			if (ySurface < 0) continue;
			float globalY = ChunkOffsetY * HauteurMax + ySurface;
			bool surfaceHerbe = _materials[x, ySurface, z] == 1;
			bool surfacePlateauCentral = plateauCentral
				&& EstSurfacePraticablePicAbysseRelaxee(x, ySurface, z);
			if (!surfaceHerbe && !surfacePlateauCentral) continue;
			if (!AbysseReplatreHerbeTrouPlat(x, ySurface, z, xG, zG)) continue;
			if (CalculerIntensiteSpiraleDescenteAbysse(xG, globalY, zG) > 0.42f && !surfacePlateauCentral) continue;
			var pos = new Vector3I((int)xG, (int)globalY, (int)zG);
			InventaireFlore[pos] = FloreTypeGazon;
		}
	}

	/// <summary>Replats horizontaux dans le puits : voisins en 8 dans le chunk ; hors grille ignorés. Surface strictement plate (même Y que le centre) pour éviter l’herbe sur les marches.</summary>
	private bool AbysseReplatreHerbeTrouPlat(int lx, int ly, int lz, float xGlobal, float zGlobal)
	{
		if (_densities == null) return false;
		int h0 = ly;
		bool plateauCentral = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal)) <= AbyssRayonPlateauCentralFlore;
		int voisinsExaminés = 0;
		int ecartMax = 0;
		for (int dz = -1; dz <= 1; dz++)
		for (int dx = -1; dx <= 1; dx++)
		{
			if (dx == 0 && dz == 0) continue;
			int nx = lx + dx;
			int nz = lz + dz;
			if (nx < 0 || nx > TailleChunk || nz < 0 || nz > TailleChunk) continue;
			voisinsExaminés++;
			int hy = ObtenirHauteurSurfaceLocale(nx, nz);
			if (hy < 0) return false;
			ecartMax = Mathf.Max(ecartMax, Mathf.Abs(hy - h0));
			if (!plateauCentral && hy != h0) return false;
		}
		if (plateauCentral)
			return ecartMax <= 1;
		return voisinsExaminés > 0;
	}

	private bool EstSurfacePraticableBiomeParasite(int x, int y, int z)
	{
		if (_densities == null) return false;
		if (y < 0 || y > HauteurMax) return false;

		int voisinsTestes = 0;
		int voisinsStables = 0;
		for (int i = 0; i < DirCardinales2D.Length; i++)
		{
			int nx = x + DirCardinales2D[i].X;
			int nz = z + DirCardinales2D[i].Y;
			if (nx < 0 || nx > TailleChunk || nz < 0 || nz > TailleChunk)
				continue;

			voisinsTestes++;
			bool voisinSolide = _densities[nx, y, nz] > Isolevel;
			bool voisinAirAuDessus = y >= HauteurMax
				|| (_densities[nx, y + 1, nz] <= Isolevel && (_densitiesEau == null || _densitiesEau[nx, y + 1, nz] <= Isolevel));
			if (voisinSolide && voisinAirAuDessus)
				voisinsStables++;
		}

		return voisinsTestes >= 3 && voisinsStables >= 2;
	}

	/// <summary>Sommets de pics APISARA souvent trop étroits pour 2/3 voisins stables — critère assoupli sans toucher au reste.</summary>
	private bool EstSurfacePraticablePicAbysseRelaxee(int x, int y, int z)
	{
		if (_densities == null) return false;
		if (y < 0 || y > HauteurMax) return false;

		int voisinsTestes = 0;
		int voisinsStables = 0;
		for (int i = 0; i < DirCardinales2D.Length; i++)
		{
			int nx = x + DirCardinales2D[i].X;
			int nz = z + DirCardinales2D[i].Y;
			if (nx < 0 || nx > TailleChunk || nz < 0 || nz > TailleChunk)
				continue;

			voisinsTestes++;
			bool voisinSolide = _densities[nx, y, nz] > Isolevel;
			bool voisinAirAuDessus = y >= HauteurMax
				|| (_densities[nx, y + 1, nz] <= Isolevel && (_densitiesEau == null || _densitiesEau[nx, y + 1, nz] <= Isolevel));
			if (voisinSolide && voisinAirAuDessus)
				voisinsStables++;
		}

		return voisinsTestes >= 2 && voisinsStables >= 1;
	}

	private bool EstDansTrouNoirAbysseMonde(float xGlobal, float zGlobal)
	{
		if (!_generationAbysseActive) return false;
		float distance = Mathf.Sqrt(xGlobal * xGlobal + zGlobal * zGlobal);
		return distance <= AbyssRayonTrouNoir;
	}
}
