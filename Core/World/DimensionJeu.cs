using Godot;
using System.Collections.Generic;

public enum DimensionJeu
{
	Alpha = 0,
	/// <summary>Identifiant technique réseau / code. Le nom canonique de cette dimension est <see cref="ConstantesDimensionAbysse.Apisara"/>.</summary>
	Abysse = 1,
	/// <summary>Clone Alpha (même seed, persistance indépendante). Code/dossier : <see cref="ConstantesDimensions.NomBeta"/>.</summary>
	Beta = 2,
	/// <summary>Clone Alpha (même seed, persistance indépendante). Code/dossier : <see cref="ConstantesDimensions.NomOmega"/>.</summary>
	Omega = 3,
	/// <summary>Clone Alpha (même seed, persistance indépendante). Code/dossier : <see cref="ConstantesDimensions.NomDelta"/>.</summary>
	Delta = 4
}

/// <summary>Table centralisée des dimensions : nom de dossier de sauvegarde (suffixe <c>chunks_*</c>),
/// décalage de fuseau horaire en heures par rapport à Alpha, point de téléportation par défaut, et drapeau « heure figée ».</summary>
public static class ConstantesDimensions
{
	public const string NomAlpha = "Dimension_Alpha";
	public const string NomBeta = "PETA";
	public const string NomOmega = "OMEGA";
	public const string NomDelta = "DERATA";

	public readonly struct InfoDimension
	{
		public readonly int Id;
		public readonly string NomCanonique;
		public readonly double FuseauOffsetHeures;
		public readonly Vector3 PointTeleportDefaut;
		public readonly bool HeureFiguree;
		public readonly bool EstAlphaLike;

		public InfoDimension(int id, string nom, double offset, Vector3 pointTeleport, bool heureFiguree, bool alphaLike)
		{
			Id = id;
			NomCanonique = nom;
			FuseauOffsetHeures = offset;
			PointTeleportDefaut = pointTeleport;
			HeureFiguree = heureFiguree;
			EstAlphaLike = alphaLike;
		}
	}

	private static readonly Dictionary<int, InfoDimension> _table = new Dictionary<int, InfoDimension>
	{
		[(int)DimensionJeu.Alpha] = new InfoDimension((int)DimensionJeu.Alpha, NomAlpha,    0.0,  new Vector3(0f, 170f, 0f),     heureFiguree: false, alphaLike: true),
		[(int)DimensionJeu.Abysse] = new InfoDimension((int)DimensionJeu.Abysse, ConstantesDimensionAbysse.Apisara, 6.0, new Vector3(1520f, 190f, 0f), heureFiguree: true,  alphaLike: false),
		[(int)DimensionJeu.Beta]  = new InfoDimension((int)DimensionJeu.Beta,  NomBeta,    6.0,  new Vector3(0f, 170f, 0f),     heureFiguree: false, alphaLike: true),
		[(int)DimensionJeu.Omega] = new InfoDimension((int)DimensionJeu.Omega, NomOmega,  12.0,  new Vector3(0f, 170f, 0f),     heureFiguree: false, alphaLike: true),
		[(int)DimensionJeu.Delta] = new InfoDimension((int)DimensionJeu.Delta, NomDelta,  18.0,  new Vector3(0f, 170f, 0f),     heureFiguree: false, alphaLike: true),
	};

	public static bool EssayerObtenirInfo(int dimensionId, out InfoDimension info)
	{
		return _table.TryGetValue(dimensionId, out info);
	}

	public static InfoDimension ObtenirInfoOuAlpha(int dimensionId)
	{
		return _table.TryGetValue(dimensionId, out var info) ? info : _table[(int)DimensionJeu.Alpha];
	}

	public static IEnumerable<InfoDimension> Toutes() => _table.Values;

	/// <summary>Itère sur les dimensions « Alpha-like » (Alpha + clones Beta/Omega/Delta), exclut Abysse.</summary>
	public static IEnumerable<InfoDimension> ToutesAlphaLike()
	{
		foreach (var info in _table.Values)
			if (info.EstAlphaLike)
				yield return info;
	}

	public static string ObtenirNomCanonique(int dimensionId)
	{
		return _table.TryGetValue(dimensionId, out var info) ? info.NomCanonique : NomAlpha;
	}
}

public static class ConstantesDimensionAbysse
{
	/// <summary>Nom canonique de la dimension (lore, UI, dossiers chunks : <c>chunks_APISARA</c>). Ce n'est pas « la dimension Abysse » : c'est APISARA.</summary>
	public const string Apisara = "APISARA";

	public const float FondAbsolu = -15000f;
	public const float RayonTrouNoir = 500f;
	public const int TaillePalierMetres = 500;
	public const int DemiFenetrePaliersActifs = 1;
	/// <summary>Dans la colonne du trou APISARA : herbe possible sur replats jusqu’à cette altitude monde (inclusive basse).</summary>
	public const float LimiteInferieureHerbeTrouMonde = -500f;

	public static int ObtenirIndexStageDepuisYMonde(float yMonde)
	{
		return Godot.Mathf.FloorToInt(yMonde / Godot.Mathf.Max(1f, TaillePalierMetres));
	}

	public static int ObtenirIndexStageDepuisCoordYChunk(int coordYChunk, int hauteurChunk)
	{
		float h = Godot.Mathf.Max(1f, hauteurChunk);
		float centreChunkY = coordYChunk * h + h * 0.5f;
		return ObtenirIndexStageDepuisYMonde(centreChunkY);
	}

	public static void ObtenirPlageCoordYChunkDuStage(int indexStage, int hauteurChunk, out int coordYMin, out int coordYMax)
	{
		float h = Godot.Mathf.Max(1f, hauteurChunk);
		float stageMinY = indexStage * (float)TaillePalierMetres;
		float stageMaxYInclus = ((indexStage + 1) * (float)TaillePalierMetres) - 0.001f;
		coordYMin = Godot.Mathf.FloorToInt(stageMinY / h);
		coordYMax = Godot.Mathf.FloorToInt(stageMaxYInclus / h);
		if (coordYMax < coordYMin)
			coordYMax = coordYMin;
	}

	public static int ObtenirCoordYChunkRepresentatifDuStage(int indexStage, int hauteurChunk)
	{
		float h = Godot.Mathf.Max(1f, hauteurChunk);
		// Le modèle Abysse "2D par stage" ne conserve qu'une couche Y représentative par palier.
		// En négatif, un Floor() sur le centre du palier saute des couches (ex: -2 devient -3 avec h=720, palier=1000),
		// ce qui crée des trous visibles de parois/collisions entre certains paliers.
		// On choisit donc une ancre différente selon le signe:
		// - paliers >= 0 : borne basse du palier
		// - paliers < 0 : borne haute inclusive du palier
		// Cette règle donne une progression continue des coordY représentatifs au lieu de sauts.
		float stageMinY = indexStage * (float)TaillePalierMetres;
		float stageMaxYInclus = ((indexStage + 1) * (float)TaillePalierMetres) - 0.001f;
		float yAncre = indexStage >= 0 ? stageMinY : stageMaxYInclus;
		return Godot.Mathf.FloorToInt(yAncre / h);
	}

	public static bool EstDansTrouNoirXZ(float xMonde, float zMonde)
	{
		float distance = Godot.Mathf.Sqrt((xMonde * xMonde) + (zMonde * zMonde));
		return distance <= RayonTrouNoir;
	}
}

