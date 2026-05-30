using Godot;
using System;

/// <summary>
/// Façade UI monde (coords + horloge dimension).
/// </summary>
public sealed class WorldUiFacade
{
    public void MettreAJourEntetesMonde(
        CharacterBody3D joueur,
        Label labelCoords,
        Label labelHeureDimension,
        int dimensionLocaleActive,
        Func<int, Monde_Serveur> obtenirServeurDimension,
        double fuseauHoraireHeures,
        ref Vector3 dernieresCoordsAffichees,
        ref string dernierTexteHeureDimension)
    {
        if (labelCoords != null && joueur != null && joueur.IsInsideTree())
        {
            Vector3 p = joueur.GlobalPosition;
            Vector3 pArrondi = new Vector3(
                Mathf.Round(p.X * 10f) * 0.1f,
                Mathf.Round(p.Y * 10f) * 0.1f,
                Mathf.Round(p.Z * 10f) * 0.1f);
            if (pArrondi != dernieresCoordsAffichees)
            {
                dernieresCoordsAffichees = pArrondi;
                labelCoords.Text = $"X: {pArrondi.X:F1}  Y: {pArrondi.Y:F1}  Z: {pArrondi.Z:F1}";
            }
        }

        if (labelHeureDimension == null)
            return;

        var infoDimension = ConstantesDimensions.ObtenirInfoOuAlpha(dimensionLocaleActive);
        string heureAffichee;
        if (infoDimension.HeureFiguree)
        {
            heureAffichee = "13:30:00";
        }
        else
        {
            double offset = obtenirServeurDimension?.Invoke(dimensionLocaleActive)?.FuseauHoraireHeures
                ?? (fuseauHoraireHeures + infoDimension.FuseauOffsetHeures);
            heureAffichee = DateTime.UtcNow.AddHours(offset).ToString("HH:mm:ss");
        }

        string texteHeure = $"{infoDimension.NomCanonique}  {heureAffichee}";
        if (!string.Equals(texteHeure, dernierTexteHeureDimension, StringComparison.Ordinal))
        {
            dernierTexteHeureDimension = texteHeure;
            labelHeureDimension.Text = texteHeure;
        }
    }
}
