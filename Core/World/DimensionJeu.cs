public enum DimensionJeu
{
	Alpha = 0,
	Abysse = 1
}

public static class ConstantesDimensionAbysse
{
	public const float FondAbsolu = -2000000000f;
	public const float RayonTrouNoir = 500f;
	public const int TaillePalierMetres = 1000;
	public const int DemiFenetrePaliersActifs = 1;

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
