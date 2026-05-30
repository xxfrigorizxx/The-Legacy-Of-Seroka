using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private const float RayonInteractionBaiesBuisson = 1.2f;
    private const float FondationPasSnapMetres = 4f;
    private const float FondationPenetrationMetres = 0.015f;
    private const float FondationDistanceCentreAdjacente = FondationPasSnapMetres - FondationPenetrationMetres;
    private const float FondationRayonSnapDouxMetres = 4.8f;
    private const float FondationToleranceAxePrincipalMetres = 0.12f;
    private const float FondationToleranceAxeSecondaireMetres = 0.12f;
    private const float FondationToleranceEmpilementXZMetres = 2.05f;
    private const float FondationToleranceDessusMetres = 0.55f;
    private const int OffsetEtagesFondationMax = 12;
    private const float NormaleSupportStructureMinY = 0.6f;
    private const float MargeChevauchementMetres = 0.02f;
    private const float MargeEmpilementStructureMetres = 0.01f;
    private const float PasRotationStructuresFixesDegres = 15f;
    private const float HauteurApproxFondationMetres = 1f;
    private const float HauteurSolBoisMetres = PlancherEpaisseurMetres;
    private const float MuretLongueurMetres = 4f;
    private const float MuretHauteurMetres = 1f;
    private const float MuretEpaisseurMetres = 0.22f;
    private const float MuretOffsetCentreDepuisFondationMetres = FondationPasSnapMetres * 0.5f + MuretEpaisseurMetres * 0.5f - FondationPenetrationMetres;
    private const float MuretToleranceSnapFondationMetres = 2.4f;
    private const float MuretTolerancePresenceMetres = 0.18f;
    private const float PasRotationMuretDegres = 10f;
    private const float MurLargeurMetres = 4f;
    private const float MurHauteurMetres = 3f;
    private const float MurEpaisseurMetres = 0.22f;
    private const float PorteLargeurMetres = 1.35f;
    private const float PorteHauteurMetres = 2.4f;
    private const float PorteEpaisseurMetres = 0.12f;
    private const float ToitChaumeHauteurMetres = 0.42f;
    private const float ToitChaumePasGrilleMetres = 4f;
    private const float ToitChaumeDecalageHauteurMetres = 0.10f;
    private const float TorcheHauteurMetres = 1.12f;
    private const float TorcheRayonMetres = 0.10f;
    private const float TorcheOffsetMurMetres = -0.015f;
    private const float TorcheAngleMurDegres = 45f;
    private const int ModeSnapMuretAuto = 0;
    private const int ModeSnapMuretFondation = 1;
    private const int ModeSnapMuretMuret = 2;
    private const int ModeSnapMuretTerrain = 3;
    private const float PasRotationSolBoisDegres = 90f;
    private const float ToleranceSolSurFondationMetres = 0.35f;

}
