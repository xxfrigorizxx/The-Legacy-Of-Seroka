using System;

/// <summary>
/// Orchestrateur de tick serveur.
/// Première étape: point d'entrée unique, sans changement de comportement.
/// </summary>
public sealed class ServerTickOrchestrator
{
    private readonly Monde_Serveur _owner;

    public ServerTickOrchestrator(Monde_Serveur owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void Execute(double delta)
    {
        // Etape transitoire: la logique reste centralisee dans le tick historique.
        _owner.ExecuterTickMonolithique(delta);
    }
}
