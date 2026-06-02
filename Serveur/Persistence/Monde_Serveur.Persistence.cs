using Godot;
using System;
using System.IO;

/// <summary>
/// Persistance disque chunk + flore (chemins, lecture/écriture binaire). Partie de <see cref="Monde_Serveur"/>.
/// CONTRAT: chemins <c>user://saves/{NomMondeActuel}/chunks_{dimension}</c> et formats binaires inchangés.
/// </summary>
public partial class Monde_Serveur : Node
{
	private string ObtenirNomDimensionNormalise()
	{
		string brut = string.IsNullOrWhiteSpace(NomDimension) ? "ARAPA" : NomDimension.Trim();
		return brut.Replace("/", "_").Replace("\\", "_").Replace(" ", "_");
	}

	private string ObtenirDossierChunksRelatif()
	{
		string nom = GameState.Instance?.NomMondeActuel ?? "MonMonde";
		string suffixeDimension = ObtenirNomDimensionNormalise();
		return $"user://saves/{nom}/chunks_{suffixeDimension}";
	}

	private string ObtenirCheminChunkRelatif(Vector2I coord, int coordY)
		=> $"{ObtenirDossierChunksRelatif()}/chunk_{coord.X}_{coordY}_{coord.Y}.bin";

	private bool FichierChunkExiste(Vector2I coord, int coordY)
	{
		return File.Exists(ProjectSettings.GlobalizePath(ObtenirCheminChunkRelatif(coord, coordY)));
	}

	private string ObtenirCheminSauvegarde(Vector2I coord, int coordY) => ObtenirCheminChunkRelatif(coord, coordY);

	/// <summary>Délègue au chunk la sauvegarde binaire. NE sauvegarde QUE si EstModifie.</summary>
	internal void SauvegarderChunkSurDisque(Vector2I coord, Chunk_Serveur chunk)
	{
		chunk.SauvegarderChunkSurDisque();
	}

	/// <summary>Résurrection : chargement binaire via BinaryReader. Si fichier absent ou corrompu → régénération procédurale.</summary>
	internal Chunk_Serveur ChargerChunkDepuisDisque(Vector2I coord, int coordY)
	{
		GD.Print($"ZERO-K DIAG : Tentative chargement Chunk {coord}...");
		string cheminGodot = ObtenirCheminSauvegarde(coord, coordY);
		string cheminAbsolu = ProjectSettings.GlobalizePath(cheminGodot);
		if (!File.Exists(cheminAbsolu))
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} — fichier inexistant ({cheminGodot}).");
			return null;
		}
		int hauteurTrancheAttendue = ModeProfondeurActive
			? ConstantesProfondeurVerticale.HauteurTrancheMetres
			: HauteurMax;
		int voxelCount = (TailleChunk + 1) * (hauteurTrancheAttendue + 1) * (TailleChunk + 1);
		int tailleAttendue = voxelCount * 9;
		byte[] donneesVoxels;
		try
		{
			using (var reader = new BinaryReader(File.Open(cheminAbsolu, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)))
			{
				byte version = reader.ReadByte();
				if (ModeProfondeurActive)
				{
					if (version != ConstantesProfondeurVerticale.VersionChunkProfondeur)
					{
						GD.PrintErr($"ZERO-K REJET : Chunk {coord} couche {coordY} — sauvegarde tranche 720 m (v{version}) incompatible, régénération ({cheminGodot}).");
						return null;
					}
					ushort hauteurFichier = reader.ReadUInt16();
					if (hauteurFichier != ConstantesProfondeurVerticale.HauteurTrancheMetres)
					{
						GD.PrintErr($"ZERO-K REJET : Chunk {coord} — hauteur tranche {hauteurFichier} ≠ {ConstantesProfondeurVerticale.HauteurTrancheMetres}.");
						return null;
					}
				}
				else if (version != 1)
				{
					GD.PrintErr($"ZERO-K REJET : Chunk {coord} — version {version} non supportée ({cheminGodot}).");
					return null;
				}
				int tailleLu = reader.ReadInt32();
				if (tailleLu != tailleAttendue)
				{
					GD.PrintErr($"ZERO-K REJET : Chunk {coord} corrompu (taille {tailleLu} ≠ {tailleAttendue}) ({cheminGodot}). Régénération forcée.");
					return null;
				}
				donneesVoxels = reader.ReadBytes(tailleLu);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} — erreur lecture ({cheminGodot}) : {ex.Message}");
			return null;
		}
		if (donneesVoxels == null || donneesVoxels.Length != tailleAttendue)
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} refusé ! Taille lue : {donneesVoxels?.Length ?? 0} | Attendue : {tailleAttendue} ({cheminGodot}).");
			return null;
		}
		GD.Print($"ZERO-K SUCCÈS : Chunk {coord} chargé depuis le disque ({donneesVoxels.Length} bytes).");
		var chunk = CreerChunkServeur(coord, coordY);
		if (!chunk.AppliquerTableauBytes(donneesVoxels))
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} — AppliquerTableauBytes a échoué ({cheminGodot}). Régénération forcée.");
			return null;
		}
		if (ActiverGenerationAbysse)
			chunk.ReparerGeometrieExtrusionAbysseSiChargee();
		ChargerFloreChunk(coord, chunk);
		return chunk;
	}

	/// <summary>Sauvegarde l’inventaire flore du chunk (herbe/buissons retirés ou repoussés).</summary>
	internal void SauvegarderFloreChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (chunk == null) return;
		string dossier = ProjectSettings.GlobalizePath(ObtenirDossierChunksRelatif() + "/");
		Directory.CreateDirectory(dossier);
		int coordY = chunk?.ChunkOffsetY ?? 0;
		string chemin = Path.Combine(dossier, $"chunk_{coord.X}_{coordY}_{coord.Y}_flore.bin");
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
	internal void ChargerFloreChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (chunk == null) return;
		string chemin = Path.Combine(ProjectSettings.GlobalizePath(ObtenirDossierChunksRelatif()), $"chunk_{coord.X}_{chunk.ChunkOffsetY}_{coord.Y}_flore.bin");
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
			chunk.EnrichirBuissonsDepuisInventaireSiAbsents();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Erreur chargement flore chunk {coord} : {ex.Message}");
			chunk.RegenererInventaireFloreDepuisSurface();
		}
		if (ActiverGenerationAbysse)
			chunk.AppliquerEnsemencementFloreTrouAbysse(notifierClient: false);
	}
}
