using Godot;
using System;

/// <summary>Ordonnanceur génération async (transition refacto).</summary>
public sealed class ChunkGenerationScheduler
{
    private readonly Monde_Serveur _owner;
    private readonly ChunkGenerationKernel _kernel;

    public ChunkGenerationScheduler(Monde_Serveur owner, ChunkGenerationKernel kernel)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public void PlanifierGenerationChunk(Vector2I coord, int coordY, Vector3I cleDemande)
    {
        _kernel.GenererChunk(coord, coordY, cleDemande);
    }
}
