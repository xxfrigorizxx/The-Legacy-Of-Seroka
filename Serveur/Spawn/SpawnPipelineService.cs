using Godot;
using System;

/// <summary>Façade pipeline spawn (arbres, ensemencement, stase pierres).</summary>
public sealed class SpawnPipelineService
{
    private readonly Monde_Serveur _owner;

    public SpawnPipelineService(Monde_Serveur owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void SpawnerArbresChunkAvecPrioriteSauvegarde(Vector2I coord, Chunk_Serveur chunk)
        => _owner.SpawnerArbresChunkAvecPrioriteSauvegarde(coord, chunk);

    public void DeclencherEnsemencement(Vector2I chunkCoord, Chunk_Serveur chunk, float tailleChunk, Action<Vector2I, Chunk_Serveur> onStasePrete = null)
        => _owner.DeclencherEnsemencement(chunkCoord, chunk, tailleChunk, onStasePrete);

    public void LibererRochesChunk(Vector2I coordChunk)
        => _owner.LibererRochesChunk(coordChunk);
}
