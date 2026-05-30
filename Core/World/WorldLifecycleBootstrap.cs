using System;

/// <summary>
/// Façade de bootstrap monde/UI.
/// Permet de déléguer le câblage de démarrage en gardant les implémentations existantes.
/// </summary>
public sealed class WorldLifecycleBootstrap
{
    private readonly Action _assurerCalquesHudInventaireEtCarnet;
    private readonly Action _creerRepereCentreEcran;
    private readonly Action _creerOverlayParotaroma;
    private readonly Action _assurerOverlayPortailTransition;
    private readonly Action _initialiserWarmupShadersProgressif;

    public WorldLifecycleBootstrap(
        Action assurerCalquesHudInventaireEtCarnet,
        Action creerRepereCentreEcran,
        Action creerOverlayParotaroma,
        Action assurerOverlayPortailTransition,
        Action initialiserWarmupShadersProgressif)
    {
        _assurerCalquesHudInventaireEtCarnet = assurerCalquesHudInventaireEtCarnet;
        _creerRepereCentreEcran = creerRepereCentreEcran;
        _creerOverlayParotaroma = creerOverlayParotaroma;
        _assurerOverlayPortailTransition = assurerOverlayPortailTransition;
        _initialiserWarmupShadersProgressif = initialiserWarmupShadersProgressif;
    }

    public void AssurerCalquesHudInventaireEtCarnet()
        => _assurerCalquesHudInventaireEtCarnet?.Invoke();

    public void InitialiserOverlaysEtReperes()
    {
        _creerRepereCentreEcran?.Invoke();
        _creerOverlayParotaroma?.Invoke();
        _assurerOverlayPortailTransition?.Invoke();
    }

    public void InitialiserWarmupShadersProgressif()
        => _initialiserWarmupShadersProgressif?.Invoke();
}
