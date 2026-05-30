using Godot;
using System;

/// <summary>Façade persistance chunk (transition refacto).</summary>
public sealed class ChunkPersistenceService
{
    private readonly Monde_Serveur _owner;

    public ChunkPersistenceService(Monde_Serveur owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void SauvegarderChunkSurDisque(Vector2I coord, Chunk_Serveur chunk)
        => _owner.SauvegarderChunkSurDisque(coord, chunk);

    public Chunk_Serveur ChargerChunkDepuisDisque(Vector2I coord, int coordY)
        => _owner.ChargerChunkDepuisDisque(coord, coordY);
}
