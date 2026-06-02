using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Génération/persistance des arbres (essences, spawn, chargement, rattrapage, décharge). Partie de <see cref="Monde_Serveur"/>.
/// CONTRAT: déterminisme des essences et compatibilité de lecture des formats arbres V1..V6.
/// </summary>
public partial class Monde_Serveur : Node
{
	private void AssurerNoiseTemperatureArbres()
	{
		if (_noiseTemperatureArbres != null && _noiseTemperatureArbresSeed == SeedTerrain) return;
		_noiseTemperatureArbres = new FastNoiseLite();
		_noiseTemperatureArbres.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseTemperatureArbres.Seed = SeedTerrain + 2;
		_noiseTemperatureArbres.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseTemperatureArbres.FractalOctaves = 4;
		_noiseTemperatureArbres.Frequency = 0.0005f;
		_noiseTemperatureArbresSeed = SeedTerrain;
	}

	private void AssurerNoiseBiomeForetArbres()
	{
		if (_noiseBiomeForetArbres != null && _noiseBiomeForetArbresSeed == SeedTerrain) return;
		_noiseBiomeForetArbres = new FastNoiseLite();
		_noiseBiomeForetArbres.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseBiomeForetArbres.Seed = SeedTerrain + 77;
		_noiseBiomeForetArbres.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseBiomeForetArbres.FractalOctaves = 3;
		_noiseBiomeForetArbres.Frequency = 0.00028f;
		_noiseBiomeForetArbresSeed = SeedTerrain;
	}

	private void AssurerNoiseHumiditeArbres()
	{
		if (_noiseHumiditeArbres != null && _noiseHumiditeArbresSeed == SeedTerrain) return;
		_noiseHumiditeArbres = new FastNoiseLite();
		_noiseHumiditeArbres.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseHumiditeArbres.Seed = SeedTerrain + 3;
		_noiseHumiditeArbres.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseHumiditeArbres.FractalOctaves = 4;
		_noiseHumiditeArbres.Frequency = 0.0006f;
		_noiseHumiditeArbresSeed = SeedTerrain;
	}

	/// <summary>0=sans arbres, 1=bouleau seul, 2=mixte, 3=chêne seul (tempéré uniquement).</summary>
	private int DeterminerBiomeForetTempere(int gx, int gz)
	{
		float n = _noiseBiomeForetArbres?.GetNoise2D(gx, gz) ?? 0f;
		if (n < -0.44f) return 0;
		if (n < -0.08f) return 1;
		if (n < 0.28f) return 2;
		return 3;
	}

	private byte DeterminerIndexBotaniqueArbre(uint seedArbre, int gx, int gz, byte matSurface)
	{
		// Choix déterministe: un arbre garde la même essence entre chargements.
		uint h = (seedArbre * 1664525u) + 1013904223u;
		float r = (h & 0x00FFFFFFu) / 16777216f;
		// Mode test: injecte beaucoup de jungle partout, sans supprimer totalement les autres essences.
		float ratioJungleTest = Mathf.Clamp(RatioJungleModeTest, 0f, 0.95f);
		if (ModeEssencesPartoutTemporaire && r < ratioJungleTest)
			return LSystem_Botanique.IndexJungle;
		// APISARA : le bruit tempéré (neige/bouleau) ne correspond pas au climat surface ; canopée jungle + chênes.
		if (ActiverGenerationAbysse)
			return (byte)(r < 0.55f ? LSystem_Botanique.IndexJungle : LSystem_Botanique.IndexChene);
		AssurerNoiseTemperatureArbres();
		float temp = _noiseTemperatureArbres?.GetNoise2D(gx, gz) ?? 0f;
		AssurerNoiseHumiditeArbres();
		float humidite = _noiseHumiditeArbres?.GetNoise2D(gx, gz) ?? 0f;
		float humiditeNorm = (humidite + 1f) * 0.5f;
		// Arbres morts uniquement en zones sèches (terre aride + désert sableux), jamais sur herbe.
		if ((matSurface == 6 || matSurface == 3) && temp > 0.08f && humiditeNorm < 0.58f)
			return (byte)(r < 0.50f ? LSystem_Botanique.IndexCheneMort : LSystem_Botanique.IndexBouleauMort);
		// Sols froids explicites: forcer conifères même si la température bruitée locale est moins extrême.
		if (matSurface == 5 || matSurface == 9)
			return (byte)(r < 0.62f ? LSystem_Botanique.IndexSapin : LSystem_Botanique.IndexPin);
		// Zone froide/neige: sapin majoritaire en froid modere, pin plus frequent en grand froid.
		if (temp < -0.32f)
			return (byte)(r < 0.72f ? LSystem_Botanique.IndexPin : LSystem_Botanique.IndexSapin);
		if (temp < -0.15f)
			return (byte)(r < 0.76f ? LSystem_Botanique.IndexSapin : LSystem_Botanique.IndexPin);
		// Jungle: très humide + chaud (on garde les zones neigeuses inchangées).
		if (temp > 0.22f && humidite > 0.62f)
			return (byte)(r < 0.70f ? LSystem_Botanique.IndexJungle : LSystem_Botanique.IndexChene);
		AssurerNoiseBiomeForetArbres();
		int biome = DeterminerBiomeForetTempere(gx, gz);
		if (biome == 1) return LSystem_Botanique.IndexBouleau; // zone bouleaux
		if (biome == 3) return LSystem_Botanique.IndexChene;   // zone chênes
		// Mixte (et vieux saves en zone sans arbres): mélange local d'essences.
		return (byte)(r < 0.50f ? LSystem_Botanique.IndexBouleau : LSystem_Botanique.IndexChene);
	}

	/// <summary>Spawn les ArbreVivant 3D pour ce chunk (procédural ou chargé).</summary>
	private void SpawnerArbresChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (_parentPourArbres == null || chunk.InventaireArbres.Count == 0) return;
		AssurerPoolSeedsArbresPregen();
		foreach (var kv in chunk.InventaireArbres)
		{
			// Base collée au sol (Y - 0.5 pour éviter troncs flottants)
			Vector3 pos = new Vector3(kv.Key.X + 0.5f, kv.Key.Y - 0.5f, kv.Key.Z + 0.5f);
			int age = Mathf.Max(1, kv.Value.Stage + 1);
			int lx = kv.Key.X - coord.X * chunk.TailleChunk;
			int lz = kv.Key.Z - coord.Y * chunk.TailleChunk;
			var (_, matSurface) = chunk.ObtenirSurfaceEtMateriau(lx, lz);
			byte indexBotanique = DeterminerIndexBotaniqueArbre(kv.Value.Seed, kv.Key.X, kv.Key.Z, matSurface);
			uint seedPregen = SelectionnerSeedArbreDepuisPool(indexBotanique, age, kv.Key.X, kv.Key.Z, kv.Value.Seed);
			_fileSpawnArbres.Enqueue((coord, pos, age, seedPregen, indexBotanique, 0));
		}
	}

	/// <summary>Priorité au disque: si un save arbres existe, on le charge; sinon fallback procédural du chunk.</summary>
	internal void SpawnerArbresChunkAvecPrioriteSauvegarde(Vector2I coord, Chunk_Serveur chunk)
	{
		if (ChargerArbresChunk(coord, chunk))
			return;
		// Migration : chunk disque sans fichier arbres → rejouer la passe procédurale (APISARA ou profondeur 3D).
		if ((ActiverGenerationAbysse || ModeProfondeurActive) && chunk != null && chunk.EstChargeDepuisDisque && chunk.InventaireArbres.Count == 0)
		{
			chunk.RegenererInventaireArbresProcedural();
			if (chunk.InventaireArbres.Count > 0)
				chunk.MarquerModifie();
		}
		SpawnerArbresChunk(coord, chunk);
	}

	private void InstancierArbreVivant(Vector3 pos, int age, uint seed, byte indexBotanique, int joursRattrapage)
	{
		if (_parentPourArbres == null) return;
		var arbre = new ArbreVivant
		{
			AgeEnJours = Mathf.Max(1, age),
			ResistanceActuelle = ArbreVivant.ResistanceMaxPourAge(Mathf.Max(1, age)),
			Seed = seed,
			IndexBotanique = indexBotanique
		};
		_parentPourArbres.AddChild(arbre);
		arbre.GlobalPosition = pos;
		if (joursRattrapage > 0)
			arbre.RattraperCroissance(joursRattrapage, pos);
	}

	/// <summary>Charge et spawn les arbres depuis disque. Rattrape la croissance du temps passé hors-ligne.</summary>
	private bool ChargerArbresChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (_parentPourArbres == null) return false;
		int coordY = chunk?.ChunkOffsetY ?? 0;
		string chemin = Path.Combine(ProjectSettings.GlobalizePath(ObtenirDossierChunksRelatif()), $"chunk_{coord.X}_{coordY}_{coord.Y}_arbres.bin");
		if (!File.Exists(chemin)) return false;
		try
		{
			using (var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)))
			{
				int magic = r.ReadInt32();
				int jourDeSauvegarde = 0;
				long unixSauvegarde = 0L;
				bool formatV3 = magic == MagicArbresV3;
				bool formatV4 = magic == MagicArbresV4;
				bool formatV5 = magic == MagicArbresV5;
				bool formatV6 = magic == MagicArbresV6;
				if (magic == MagicArbresV2 || formatV3 || formatV4 || formatV5 || formatV6) // V2+ avec jour de sauvegarde
					jourDeSauvegarde = r.ReadInt32();
				if (formatV4 || formatV5 || formatV6)
					unixSauvegarde = r.ReadInt64();
				else if (magic != 0x5A4B3250 && !formatV3 && magic != MagicArbresV2)
					return false; // Format inconnu

				int joursEcoules = CalculerJoursRattrapageArbres(jourDeSauvegarde, unixSauvegarde);
				int count = r.ReadInt32();

				for (int i = 0; i < count; i++)
				{
					int gx = r.ReadInt32(), gy = r.ReadInt32(), gz = r.ReadInt32();
					int ageSauvegarde;
					byte indexBotaniqueSauvegarde;
					uint seedSauvegarde = 0u;
					if (magic == MagicArbresV2 || formatV3 || formatV4 || formatV5 || formatV6)
					{
						ageSauvegarde = r.ReadInt32();
						indexBotaniqueSauvegarde = (formatV3 || formatV4 || formatV5 || formatV6) ? r.ReadByte() : LSystem_Botanique.IndexChene;
						if (formatV5 || formatV6)
							seedSauvegarde = r.ReadUInt32();
					}
					else
					{
						byte stage = r.ReadByte();
						seedSauvegarde = r.ReadUInt32(); // seed legacy (v1)
						ageSauvegarde = stage + 1; // Ancien format Stage 0-4 → age 1-5
						indexBotaniqueSauvegarde = LSystem_Botanique.IndexChene;
					}

					// Migration rétrocompatible:
					// formats <= V5 sauvegardaient Y avec un cast int sur (racineY - 0.5),
					// ce qui perdait 1 bloc. On corrige ici pour remonter les arbres.
					if (!formatV6)
						gy += 1;
					Vector3 pos = new Vector3(gx + 0.5f, gy - 0.5f, gz + 0.5f);
					uint seedHashPos = (uint)((gx * 73856093) ^ (gz * 19349663));
					uint seedArbre = seedSauvegarde != 0u ? seedSauvegarde : seedHashPos;
					int ageCharge = Mathf.Max(1, ageSauvegarde);
					int lx = gx - coord.X * chunk.TailleChunk;
					int lz = gz - coord.Y * chunk.TailleChunk;
					var (_, matSurface) = chunk.ObtenirSurfaceEtMateriau(lx, lz);
					byte indexBotanique = (formatV3 || formatV4 || formatV5 || formatV6) ? indexBotaniqueSauvegarde : DeterminerIndexBotaniqueArbre(seedArbre, gx, gz, matSurface);
					_fileSpawnArbres.Enqueue((coord, pos, ageCharge, seedArbre, indexBotanique, joursEcoules));
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Erreur chargement arbres chunk {coord} : {ex.Message}");
			return false;
		}
	}

	private int CalculerJoursRattrapageArbres(int jourDeSauvegarde, long unixSauvegarde)
	{
		int jourActuel = GameState.Instance != null ? GameState.Instance.JourAbsolu : 0;
		int joursJeu = Mathf.Max(0, jourActuel - jourDeSauvegarde);
		if (unixSauvegarde <= 0L) return joursJeu;
		long unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		long deltaSec = Math.Max(0L, unixNow - unixSauvegarde);
		int joursReels = (int)(deltaSec / 86400L);
		return Mathf.Max(joursJeu, joursReels);
	}

	/// <summary>Vide la file de spawn arbres avant sauvegarde pour éviter les fichiers vides lors d'un reload rapide.</summary>
	private void ForcerInstanciationArbresEnAttente(Vector2I? filtreCoord = null)
	{
		if (_fileSpawnArbres.Count == 0) return;
		var restant = new Queue<(Vector2I coord, Vector3 pos, int age, uint seed, byte indexBotanique, int joursRattrapage)>();
		while (_fileSpawnArbres.Count > 0)
		{
			var a = _fileSpawnArbres.Dequeue();
			bool coordOk = !filtreCoord.HasValue || a.coord == filtreCoord.Value;
			if (!coordOk)
			{
				restant.Enqueue(a);
				continue;
			}
			if (!ColonneChunkRuntimeChargee(a.coord)) continue;
			InstancierArbreVivant(a.pos, a.age, a.seed, a.indexBotanique, a.joursRattrapage);
		}
		while (restant.Count > 0)
			_fileSpawnArbres.Enqueue(restant.Dequeue());
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
}
