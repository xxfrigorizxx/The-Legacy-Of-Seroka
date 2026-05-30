using System;

/// <summary>Façade simulation eau runtime (transition refacto).</summary>
public sealed class WaterSimulationService
{
    private readonly Monde_Serveur _owner;

    public WaterSimulationService(Monde_Serveur owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public int TickRuntime()
    {
        return _owner.TickEauRuntimeInterne();
    }
}
