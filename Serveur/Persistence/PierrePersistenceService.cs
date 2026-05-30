using Godot;
using System;

/// <summary>Façade persistance pierres (transition refacto).</summary>
public sealed class PierrePersistenceService
{
    private readonly Monde_Serveur _owner;

    public PierrePersistenceService(Monde_Serveur owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void SauvegarderPierresChunk(Vector2I coord, int coordY)
        => _owner.SauvegarderPierresChunk(coord, coordY);

    public bool ChargerEtSpawnerPierresChunk(Vector2I coord, int coordY)
        => _owner.ChargerEtSpawnerPierresChunk(coord, coordY);
}
