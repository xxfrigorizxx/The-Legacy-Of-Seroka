using Godot;
using System;

/// <summary>Façade persistance flore (transition refacto).</summary>
public sealed class FloraPersistenceService
{
    private readonly Monde_Serveur _owner;

    public FloraPersistenceService(Monde_Serveur owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void SauvegarderFloreChunk(Vector2I coord, Chunk_Serveur chunk)
        => _owner.SauvegarderFloreChunk(coord, chunk);

    public void ChargerFloreChunk(Vector2I coord, Chunk_Serveur chunk)
        => _owner.ChargerFloreChunk(coord, chunk);
}
