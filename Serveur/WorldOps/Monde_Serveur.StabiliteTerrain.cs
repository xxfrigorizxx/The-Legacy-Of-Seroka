using Godot;
using System.Collections.Generic;

/// <summary>
/// Effondrement inter-chunks, refusion des blocs chutants de terrain et scan au chargement.
/// Partie de <see cref="Monde_Serveur"/>.
/// </summary>
public partial class Monde_Serveur : Node
{
	private const int MaxEffondrementsParOperation = 4096;
	private const int MaxRefusionsParSeconde = 6;
	private const int MaxColonnesScanFlottantParFrame = 8;

	private HashSet<Vector3I> _stabiliteVisites;
	private int _stabiliteBudgetRestant;
	private float _tempsFenetreRefusion;
	private int _compteurRefusionsFenetre;

	private struct ScanTerrainFlottant
	{
		public Vector2I Coord;
		public int CoordY;
		public int ColonneIndex;
	}

	private readonly Queue<ScanTerrainFlottant> _fileScanTerrainFlottant = new Queue<ScanTerrainFlottant>();

	/// <summary>Matières de sol refusionnables (herbe, terre, sable, neige, sable quartz…).</summary>
	public static bool EstMateriauTerrainRefusionnable(byte mat) =>
		Atlas_Matiere.EstIdVoxelSurfacePosable(mat);

	internal void ConfigurerCallbacksStabiliteChunk(Chunk_Serveur chunk)
	{
		chunk.ConfigurerStabiliteGlobale(
			DemarrerOperationStabilite,
			VerifierStabiliteGlobale,
			EstVoxelSolideGlobal,
			ConsommerBudgetStabilite);
	}

	private void DemarrerOperationStabilite()
	{
		_stabiliteVisites ??= new HashSet<Vector3I>();
		_stabiliteVisites.Clear();
		_stabiliteBudgetRestant = MaxEffondrementsParOperation;
	}

	private bool ConsommerBudgetStabilite()
	{
		if (_stabiliteBudgetRestant <= 0)
			return false;
		_stabiliteBudgetRestant--;
		return true;
	}

	/// <summary>
	/// Vérifie la stabilité du voxel au-dessus de <paramref name="posGlobalSousBloc"/> (cellule « air / support retiré »).
	/// Ne génère jamais de chunk : lecture des chunks déjà chargés uniquement.
	/// </summary>
	public void VerifierStabiliteGlobale(Vector3I posGlobalSousBloc)
	{
		if (_stabiliteBudgetRestant <= 0)
			return;

		Vector3I posGlobalBloc = posGlobalSousBloc + Vector3I.Up;
		if (_stabiliteVisites != null && !_stabiliteVisites.Add(posGlobalBloc))
			return;

		if (!EssayerResoudreChunkDepuisVoxelGlobal(posGlobalBloc, out Chunk_Serveur chunk, out int lx, out int ly, out int lz))
			return;

		chunk.VerifierStabiliteLocal(new Vector3I(lx, ly - 1, lz));
	}

	public bool EstVoxelSolideGlobal(Vector3I posGlobal)
	{
		if (!EssayerResoudreChunkDepuisVoxelGlobal(posGlobal, out Chunk_Serveur chunk, out int lx, out int ly, out int lz))
			return false;
		return chunk.EstVoxelSolide(lx, ly, lz);
	}

	private bool EssayerResoudreChunkDepuisVoxelGlobal(Vector3I posGlobal, out Chunk_Serveur chunk, out int lx, out int ly, out int lz)
	{
		chunk = null;
		lx = ly = lz = 0;
		Gestionnaire_Monde.WorldToChunkAndLocal(posGlobal.X, posGlobal.Z, TailleChunk, out Vector2I coord, out lx, out lz);
		int coordY = ModeProfondeurActive
			? CoordYDepuisMondeYProfond(posGlobal.Y)
			: CoordYDepuisMondeY(posGlobal.Y, HauteurMax);
		ly = ModeProfondeurActive
			? LocalYDepuisMondeYProfond(posGlobal.Y)
			: LocalYDepuisMondeY(posGlobal.Y, HauteurMax);
		return TryGetChunkRuntime(coord, coordY, out chunk) && chunk != null;
	}

	public bool PeutRefusionnerMaintenant()
	{
		float t = Time.GetTicksMsec() / 1000f;
		if (t - _tempsFenetreRefusion > 1f)
		{
			_tempsFenetreRefusion = t;
			_compteurRefusionsFenetre = 0;
		}
		return _compteurRefusionsFenetre < MaxRefusionsParSeconde;
	}

	public int ObtenirDimensionServeurId() => _dimensionServeurId;

	/// <summary>Re-fusionne un bloc chutant de terrain au repos dans le voxel le plus proche (cellule air).</summary>
	public bool RefusionnerVoxelTerrain(Vector3 posMonde, byte mat)
	{
		if (!EstMateriauTerrainRefusionnable(mat) || !PeutRefusionnerMaintenant())
			return false;

		int gx = Mathf.FloorToInt(posMonde.X);
		int gz = Mathf.FloorToInt(posMonde.Z);
		int gy = Mathf.FloorToInt(posMonde.Y);

		// Balayage du HAUT vers le bas, en commençant 1 cellule au-dessus de l'origine du bloc : un RigidBody au repos
		// pénètre souvent un peu le sol, donc floor(Y) peut tomber DANS le voxel solide de support — la cellule d'air où
		// le cube s'est immobilisé est alors à gy+1. On retient la 1re cellule d'air POSÉE sur un voxel solide rencontrée
		// en descendant : c'est l'emplacement réel de repos. (Avant : recherche seulement vers le bas → refusion ratée
		// dès qu'il y avait pénétration, d'où des cubes qui restaient au sol sans jamais se refusionner.)
		for (int gyEssai = gy + 1; gyEssai >= gy - 3; gyEssai--)
		{
			if (gyEssai < 3)
				break;
			if (!EssayerResoudreChunkDepuisVoxelGlobal(new Vector3I(gx, gyEssai, gz), out Chunk_Serveur chunk, out int lx, out int ly, out int lz))
				continue;
			if (!chunk.EstVoxelAir(lx, ly, lz))
				continue;
			bool support = EstVoxelSolideGlobal(new Vector3I(gx, gyEssai - 1, gz));
			if (!support)
				continue;

			chunk.ModifierVoxelEtNotifier(lx, ly, lz, mat);
			_compteurRefusionsFenetre++;
			return true;
		}
		return false;
	}

	internal void EnfilerScanTerrainFlottant(Vector2I coord, int coordY)
	{
		_fileScanTerrainFlottant.Enqueue(new ScanTerrainFlottant { Coord = coord, CoordY = coordY, ColonneIndex = 0 });
	}

	private void TickScanTerrainFlottant()
	{
		if (_fileScanTerrainFlottant.Count == 0)
			return;

		float fps = (float)Engine.GetFramesPerSecond();
		int budgetColonnes = MaxColonnesScanFlottantParFrame;
		if (fps < 18f)
			budgetColonnes = Mathf.Max(1, budgetColonnes / 4);
		else if (fps < 30f)
			budgetColonnes = Mathf.Max(2, budgetColonnes / 2);

		int colonnesTraitees = 0;
		while (_fileScanTerrainFlottant.Count > 0 && colonnesTraitees < budgetColonnes)
		{
			ScanTerrainFlottant scan = _fileScanTerrainFlottant.Dequeue();
			if (!TryGetChunkRuntime(scan.Coord, scan.CoordY, out Chunk_Serveur chunk) || chunk == null)
				continue;
			if (!chunk.EstChargeDepuisDisque || !chunk.EstModifie)
				continue;

			int colonnesTotal = TailleChunk * TailleChunk;
			DemarrerOperationStabilite();
			while (scan.ColonneIndex < colonnesTotal && colonnesTraitees < budgetColonnes)
			{
				int lx = scan.ColonneIndex % TailleChunk;
				int lz = scan.ColonneIndex / TailleChunk;
				scan.ColonneIndex++;
				colonnesTraitees++;

				for (int ly = chunk.HauteurMax - 1; ly >= 1; ly--)
				{
					if (!chunk.EstVoxelSolide(lx, ly, lz) || !chunk.EstVoxelAir(lx, ly - 1, lz))
						continue;
					Vector3I posGlobalSous = chunk.LocalVersGlobalVoxel(lx, ly - 1, lz);
					if (posGlobalSous.Y <= 2)
						continue;
					VerifierStabiliteGlobale(posGlobalSous);
				}
			}

			if (scan.ColonneIndex < colonnesTotal)
				_fileScanTerrainFlottant.Enqueue(scan);
		}
	}
}
