using Godot;
using System;

/// <summary>
/// Simulation d'eau runtime (propagation événementielle). Partie de <see cref="Monde_Serveur"/>.
/// CONTRAT: budget <c>MaxEauParTick</c> et anti-retour identiques au comportement historique.
/// </summary>
public partial class Monde_Serveur : Node
{
	internal int TickEauRuntimeInterne()
	{
		if (_fileEau.Count == 0)
			return 0;
		_tickEauCourant++;
		int n = Math.Min(_fileEau.Count, MaxEauParTick);
		for (int i = 0; i < n; i++)
		{
			Vector3I pos = _fileEau.Dequeue();
			_eauActive.Remove(pos);
			if (!EstVoxelEau(pos))
				continue;

			Vector3I posBas = pos + new Vector3I(0, -1, 0);
			if (posBas.Y < 0)
			{
				DefinirVoxel(pos, 0);
				continue;
			}

			if (EstVoxelAir(posBas))
			{
				DefinirVoxel(posBas, 4);
				DefinirVoxel(pos, 0);
				MemoriserFluxEau(pos, posBas);
				ActiverEau(posBas);
				ReveillerVoisins(pos);
				continue;
			}

			bool aPression = EstVoxelEau(pos + new Vector3I(0, 1, 0));
			foreach (var d in DirEauHoriz)
			{
				Vector3I pc = pos + d;
				Vector3I pcb = pc + new Vector3I(0, -1, 0);
				if (!EstVoxelAir(pc))
					continue;
				if (!PeutCoulerVers(pos, pc))
					continue;
				bool auBord = EstVoxelAir(pcb);
				if (aPression || auBord)
				{
					DefinirVoxel(pc, 4);
					DefinirVoxel(pos, 0);
					MemoriserFluxEau(pos, pc);
					ActiverEau(pc);
					ReveillerVoisins(pos);
					break;
				}
			}
		}
		return n;
	}

	private void ActiverEau(Vector3I pos)
	{
		if (_eauActive.Add(pos)) _fileEau.Enqueue(pos);
	}

	private bool PeutCoulerVers(Vector3I source, Vector3I destination)
	{
		if (!_antiRetourEau.TryGetValue(source, out var blocage)) return true;
		if (blocage.tickExpiration <= _tickEauCourant)
		{
			_antiRetourEau.Remove(source);
			return true;
		}
		return blocage.retourInterdit != destination;
	}

	private void MemoriserFluxEau(Vector3I source, Vector3I destination)
	{
		// Évite l'oscillation immédiate destination -> source.
		_antiRetourEau[destination] = (source, _tickEauCourant + DureeBlocageRetourEauTicks);
		if (_antiRetourEau.Count > 20000)
			_antiRetourEau.Clear();
	}

	private void ReveillerVoisins(Vector3I pos)
	{
		foreach (var d in DirVoisins)
			if (EstVoxelEau(pos + d)) ActiverEau(pos + d);
	}

	public void ReveillerEauAdjacente(Vector3 pointGlobal)
	{
		int gx = Mathf.FloorToInt(pointGlobal.X), gy = Mathf.FloorToInt(pointGlobal.Y), gz = Mathf.FloorToInt(pointGlobal.Z);
		var basePos = new Vector3I(gx, gy, gz);
		foreach (var d in DirReveil)
			if (EstVoxelEau(basePos + d)) ActiverEau(basePos + d);
	}
}
