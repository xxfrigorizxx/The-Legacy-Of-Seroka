using Godot;
using System;

/// <summary>Façade persistance arbres (transition refacto).</summary>
public sealed class ArbrePersistenceService
{
    private readonly Monde_Serveur _owner;

    public ArbrePersistenceService(Monde_Serveur owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void SauvegarderArbresChunk(Vector2I coord, Chunk_Serveur chunk)
        => _owner.SauvegarderArbresChunk(coord, chunk);
}
