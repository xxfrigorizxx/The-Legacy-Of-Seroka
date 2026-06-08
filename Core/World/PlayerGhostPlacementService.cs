using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private void ReinitialiserRotationManuelle()
    {
        _rotationManuelleX = 0f;
        _rotationManuelleY = 0f;
        _rotationManuelleZ = 0f;
        _offsetEtagesFondationManuel = 0;
        _modeSnapMuretManuel = 0;
    }

    private static bool EstStructureSupporteeModePlacement(int id)
    {
        return id == 200
            || EstIdTableBoisDecorative(id)
            || EstIdTableArtisanaTier1(id)
            || id == IdObjetTableAnalyseTier1
            || id == IdObjetRackBatons
            || id == IdObjetRackBuches
            || id == IdObjetCoffreBoisTier0
            || id == IdObjetPitFeu
            || id == IdObjetPitFeuRoche
            || id == IdObjetFourTorchie
            || EstIdFondation(id)
            || EstIdPlancher(id)
            || EstIdMuret(id)
            || EstIdMurBois(id)
            || EstIdPorteBois(id)
            || EstIdToitChaume(id)
            || EstIdTorche(id);
    }

    private static bool EstObjetSoinPosableShift(int id)
    {
        return id == IdObjetAtelleJambe
            || id == IdObjetAtelleBras
            || id == IdObjetBandageTier1
            || id == IdObjetAloeVera;
    }

    private bool EstModePlacementStructurePourSlot(SlotInventaire mainActive)
    {
        return _modePlacementStructureActif
            && !mainActive.EstVide
            && EstStructureSupporteeModePlacement(mainActive.ID);
    }

    private bool EstModePlacementLancerShiftPourSlot(SlotInventaire mainActive)
    {
        return _modePlacementLancerShiftActif
            && !mainActive.EstVide
            && EstObjetLancableAuMaintien(mainActive);
    }

    private bool EstModePlacementGhostActifPourSlot(SlotInventaire mainActive)
    {
        return EstModePlacementStructurePourSlot(mainActive) || EstModePlacementLancerShiftPourSlot(mainActive);
    }

    private bool EstModePlacementGhostActif()
    {
        return _modePlacementStructureActif || _modePlacementLancerShiftActif;
    }

    private void DemarrerModePlacementStructure(SlotInventaire mainActive)
    {
        if (mainActive.EstVide || !EstStructureSupporteeModePlacement(mainActive.ID))
            return;

        _modePlacementLancerShiftActif = false;
        _modePlacementStructureActif = true;
        _ghostPlacementValide = false;
        if (_ghostPlacementStructure == null || !GodotObject.IsInstanceValid(_ghostPlacementStructure) || _ghostPlacementId != mainActive.ID)
            RecreerGhostPlacementStructure(mainActive);
        MettreAJourGhostPlacementStructure(mainActive);
    }

    private void DemarrerModePlacementLancerShift(SlotInventaire mainActive)
    {
        if (mainActive.EstVide || !EstObjetLancableAuMaintien(mainActive))
            return;

        _modePlacementStructureActif = false;
        _modePlacementLancerShiftActif = true;
        _ghostPlacementValide = false;
        if (_ghostPlacementStructure == null || !GodotObject.IsInstanceValid(_ghostPlacementStructure) || _ghostPlacementId != mainActive.ID)
            RecreerGhostPlacementStructure(mainActive);
        MettreAJourGhostPlacementStructure(mainActive);
    }

    private void AnnulerModePlacementStructure(bool reinitialiserRotation)
    {
        _modePlacementStructureActif = false;
        _modePlacementLancerShiftActif = false;
        _ghostPlacementValide = false;
        _ghostPlacementId = -1;
        _ghostPlacementCouleur = Colors.Transparent;
        if (_ghostPlacementStructure != null && GodotObject.IsInstanceValid(_ghostPlacementStructure))
            _ghostPlacementStructure.QueueFree();
        _ghostPlacementStructure = null;
        if (reinitialiserRotation)
            ReinitialiserRotationManuelle();
        else
            _offsetEtagesFondationManuel = 0;
    }

    private void RecreerGhostPlacementStructure(SlotInventaire mainActive)
    {
        if (_ghostPlacementStructure != null && GodotObject.IsInstanceValid(_ghostPlacementStructure))
            _ghostPlacementStructure.QueueFree();

        if (EstModePlacementLancerShiftPourSlot(mainActive))
        {
            _ghostPlacementStructure = CreerBlocPose(Vector3.Zero, mainActive, modeGhost: true);
            if (_ghostPlacementStructure == null)
                _ghostPlacementStructure = new Node3D { Name = "GhostPlacementStructure" };
            ConfigurerNoeudGhostPlacement(_ghostPlacementStructure);
        }
        else
        {
            _ghostPlacementStructure = new Node3D { Name = "GhostPlacementStructure" };
            var meshRoot = new Node3D { Name = "GhostMeshRoot" };
            _ghostPlacementStructure.AddChild(meshRoot);

            if (mainActive.ID == 200)
                InstancierModeleAtelierPrimitif(meshRoot, mainActive, 1.2f, true);
            else if (mainActive.ID == IdObjetTableBoisDecorative)
                InstancierModeleTableBoisDecorative(meshRoot, mainActive, 1.2f, true);
            else if (mainActive.ID == IdObjetTableArtisanaTier1)
                InstancierModeleTableArtisanaTier1(meshRoot, mainActive, 1.35f, true);
            else if (mainActive.ID == IdObjetTableAnalyseTier1)
                InstancierModeleTableAnalyseTier1(meshRoot, mainActive, 1.53f, true);
            else if (mainActive.ID == IdObjetRackBatons)
                InstancierModeleRackBatons(meshRoot, mainActive, 1.05f, true);
            else if (mainActive.ID == IdObjetRackBuches)
                InstancierModeleRackBuches(meshRoot, mainActive, 1.05f, true);
            else if (mainActive.ID == IdObjetCoffreBoisTier0)
                InstancierModeleCoffreBoisTier0(meshRoot, mainActive, 0.88f, true);
            else if (mainActive.ID == IdObjetPitFeuRoche)
                InstancierModelePitFeuRoche(meshRoot, mainActive, 0.96f, true);
            else if (mainActive.ID == IdObjetPitFeu)
                InstancierModelePitFeu(meshRoot, mainActive, 0.92f, true);
            else if (mainActive.ID == IdObjetFourTorchie)
                InstancierModeleFourTorchie(meshRoot, mainActive, TailleFourTorchiePoseMetres, true);
            else if (EstIdFondation(mainActive.ID))
                InstancierModeleFondation(meshRoot, mainActive, 4.0f, true);
            else if (EstIdSolBois(mainActive.ID))
                InstancierModeleSolBois(meshRoot, mainActive, true);
            else if (EstIdSolRoche(mainActive.ID))
                InstancierModeleSolRoche(meshRoot, mainActive, true);
            else if (EstIdMuret(mainActive.ID))
                InstancierModeleMuretBois(meshRoot, mainActive, true);
            else if (EstIdMurBois(mainActive.ID))
            {
                if (EstIdMurBoisFenetre(mainActive.ID))
                    InstancierModeleMurBoisFenetre(meshRoot, mainActive, true);
                else if (EstIdMurBoisCadrePorte(mainActive.ID))
                    InstancierModeleMurBoisCadrePorte(meshRoot, mainActive, true);
                else
                    InstancierModeleMurBois(meshRoot, mainActive, true);
            }
            else if (EstIdPorteBois(mainActive.ID))
                InstancierModelePorteBois(meshRoot, mainActive, true);
            else if (EstIdToitChaume(mainActive.ID))
                InstancierModeleToitChaume(meshRoot, mainActive, ToitChaumeVarianteVisuelle.Solo, true);
            else if (EstIdTorche(mainActive.ID))
                InstancierModeleTorche(meshRoot, mainActive, true);
        }

        _ghostPlacementStructure.SetMeta("ID_Matiere", mainActive.ID);
        _ghostPlacementStructure.TopLevel = true;
        GetParent()?.AddChild(_ghostPlacementStructure);
        _ghostPlacementId = mainActive.ID;

        AppliquerCouleurGhostPlacementStructure(estValide: false);
    }

    private static void ConfigurerNoeudGhostPlacement(Node racine)
    {
        if (racine == null)
            return;
        var pile = new List<Node> { racine };
        for (int i = 0; i < pile.Count; i++)
        {
            Node noeud = pile[i];
            foreach (Node enfant in noeud.GetChildren())
                pile.Add(enfant);

            if (noeud is CollisionShape3D collisionShape)
                collisionShape.Disabled = true;
            if (noeud is CollisionObject3D collisionObject)
            {
                collisionObject.CollisionLayer = 0;
                collisionObject.CollisionMask = 0;
            }
            if (noeud is RigidBody3D rb)
            {
                rb.Freeze = true;
                rb.GravityScale = 0f;
                rb.LinearVelocity = Vector3.Zero;
                rb.AngularVelocity = Vector3.Zero;
                rb.Sleeping = true;
            }
        }
    }

    private static List<MeshInstance3D> ListerMeshesGhost(Node racine)
    {
        var resultat = new List<MeshInstance3D>();
        if (racine == null) return resultat;
        var pile = new List<Node> { racine };
        for (int i = 0; i < pile.Count; i++)
        {
            foreach (Node c in pile[i].GetChildren())
            {
                if (c is MeshInstance3D mi)
                    resultat.Add(mi);
                pile.Add(c);
            }
        }
        return resultat;
    }

    private static Material CreerMateriauGhost(Material baseMat, Color couleur)
    {
        if (baseMat is StandardMaterial3D std)
        {
            var copie = (StandardMaterial3D)std.Duplicate();
            copie.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            copie.AlbedoColor = new Color(couleur.R, couleur.G, couleur.B, 0.42f);
            return copie;
        }

        return new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(couleur.R, couleur.G, couleur.B, 0.42f),
            Roughness = 0.95f,
            Metallic = 0f
        };
    }

    private void AppliquerCouleurGhostPlacementStructure(bool estValide)
    {
        if (_ghostPlacementStructure == null || !GodotObject.IsInstanceValid(_ghostPlacementStructure))
            return;

        Color cible = estValide
            ? new Color(0.24f, 0.92f, 0.35f, 0.42f)
            : new Color(0.98f, 0.28f, 0.28f, 0.42f);
        if (_ghostPlacementCouleur == cible)
            return;

        _ghostPlacementCouleur = cible;
        foreach (MeshInstance3D mi in ListerMeshesGhost(_ghostPlacementStructure))
        {
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            int surfaceCount = mi.Mesh?.GetSurfaceCount() ?? 0;
            if (surfaceCount <= 0)
            {
                mi.MaterialOverride = CreerMateriauGhost(mi.MaterialOverride, cible);
                continue;
            }

            for (int i = 0; i < surfaceCount; i++)
            {
                Material source = mi.GetSurfaceOverrideMaterial(i)
                    ?? mi.MaterialOverride
                    ?? mi.Mesh.SurfaceGetMaterial(i);
                mi.SetSurfaceOverrideMaterial(i, CreerMateriauGhost(source, cible));
            }
        }
    }

    private void MettreAJourGhostPlacementStructure(SlotInventaire mainActive)
    {
        if (!EstModePlacementGhostActifPourSlot(mainActive))
            return;

        if (_ghostPlacementStructure == null || !GodotObject.IsInstanceValid(_ghostPlacementStructure) || _ghostPlacementId != mainActive.ID)
            RecreerGhostPlacementStructure(mainActive);
        if (_ghostPlacementStructure == null || !GodotObject.IsInstanceValid(_ghostPlacementStructure))
            return;

        Vector3 pointDeChute;
        Vector3 pointAligne;
        Vector3 rotationDeg;
        bool poseValide;
        bool affiche;
        if (EstModePlacementStructurePourSlot(mainActive))
        {
            affiche = EssayerCalculerApercuPlacementStructure(
                mainActive,
                depuisInteragir: false,
                out pointDeChute,
                out pointAligne,
                out rotationDeg,
                out poseValide);
        }
        else
        {
            affiche = EssayerCalculerApercuPlacementObjetLancable(
                mainActive,
                out pointDeChute,
                out pointAligne,
                out rotationDeg,
                out poseValide);
        }
        if (!affiche)
        {
            _ghostPlacementStructure.Visible = false;
            _ghostPlacementValide = false;
            return;
        }

        _ghostPlacementStructure.Visible = true;
        _ghostPlacementStructure.GlobalPosition = pointDeChute;
        _ghostPlacementStructure.GlobalRotationDegrees = rotationDeg;
        _ghostPlacementStructure.GlobalPosition = new Vector3(pointAligne.X, _ghostPlacementStructure.GlobalPosition.Y, pointAligne.Z);
        _ghostPlacementValide = poseValide;
        AppliquerCouleurGhostPlacementStructure(_ghostPlacementValide);
    }
}
