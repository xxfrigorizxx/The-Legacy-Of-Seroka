using Godot;
using System.Threading.Tasks;

/// <summary>
/// Planification de génération de chunk (worker async). Partie de <see cref="Monde_Serveur"/>.
/// CONTRAT: anti-duplication via <c>_chunksEnCoursGeneration</c> + verrou <c>_verrouGeneration</c> inchangés.
/// </summary>
public partial class Monde_Serveur : Node
{
	internal void DeclencherGenerationChunk(Vector2I coord, int coordY, Vector3I cleDemande)
	{
		// Garde-fou : ne jamais regénérer si une cicatrice disque existe (trous minés).
		if (FichierChunkExiste(coord, coordY))
		{
			lock (_verrouGeneration)
			{
				_chunksEnCoursGeneration.Remove(cleDemande);
				if (_chunksEnGenerationActive > 0)
					_chunksEnGenerationActive--;
			}
			Callable.From(() => EnregistrerDemandeChunk(coord, coordY)).CallDeferred();
			return;
		}
		lock (_verrouGeneration)
		{
			if (!_chunksEnCoursGeneration.Add(cleDemande))
				return;
			_chunksEnGenerationActive++;
		}

		Task.Run(() =>
		{
			var chunk = CreerChunkServeur(coord, coordY);
			chunk.GenererDonneesVoxel();
			var donnees = chunk.ObtenirDonneesPourClient();
			_chunksGeneres.Enqueue((coord, coordY, chunk, donnees));
		});
	}
}
