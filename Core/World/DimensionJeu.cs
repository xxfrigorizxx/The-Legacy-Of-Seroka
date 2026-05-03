public enum DimensionJeu
{
	Alpha = 0,
	Abysse = 1
}

public static class ConstantesDimensionAbysse
{
	public const float FondAbsolu = -2000000000f;
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
		float yCentreStage = indexStage * (float)TaillePalierMetres + (TaillePalierMetres * 0.5f);
		return Godot.Mathf.FloorToInt(yCentreStage / h);
	}
}
