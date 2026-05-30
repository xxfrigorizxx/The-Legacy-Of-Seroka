using Godot;
using System;

/// <summary>Noyau de génération chunk (transition refacto).</summary>
public sealed class ChunkGenerationKernel
{
    private readonly Monde_Serveur _owner;

    public ChunkGenerationKernel(Monde_Serveur owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void GenererChunk(Vector2I coord, int coordY, Vector3I cleDemande)
    {
        _owner.DeclencherGenerationChunk(coord, coordY, cleDemande);
    }
}
