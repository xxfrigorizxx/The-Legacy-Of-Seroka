using Godot;
using System;

/// <summary>
/// Façade de coordination dimensionnelle.
/// Encapsule les appels de transfert/changement de dimension sans modifier la logique métier existante.
/// </summary>
public sealed class DimensionCoordinator
{
    private readonly Action<int, Vector3, string, bool> _appliquerChangementDimensionLocale;
    private readonly Action<long, int, Vector3, string> _transfererPeerVersDimension;
    private readonly Action<int> _mettreAJourSuspensionServeursDimensions;

    public DimensionCoordinator(
        Action<int, Vector3, string, bool> appliquerChangementDimensionLocale,
        Action<long, int, Vector3, string> transfererPeerVersDimension,
        Action<int> mettreAJourSuspensionServeursDimensions)
    {
        _appliquerChangementDimensionLocale = appliquerChangementDimensionLocale;
        _transfererPeerVersDimension = transfererPeerVersDimension;
        _mettreAJourSuspensionServeursDimensions = mettreAJourSuspensionServeursDimensions;
    }

    public void AppliquerChangementDimensionLocale(int dimensionId, Vector3 positionCible, string messageServeur, bool rechargerPersistanceDimension = true)
    {
        _appliquerChangementDimensionLocale?.Invoke(dimensionId, positionCible, messageServeur, rechargerPersistanceDimension);
    }

    public void TransfererPeerVersDimension(long peerId, int dimensionCible, Vector3 positionCible, string messageServeur)
    {
        _transfererPeerVersDimension?.Invoke(peerId, dimensionCible, positionCible, messageServeur);
    }

    public void MettreAJourSuspensionServeursDimensions(int dimensionActiveId)
    {
        _mettreAJourSuspensionServeursDimensions?.Invoke(dimensionActiveId);
    }
}
