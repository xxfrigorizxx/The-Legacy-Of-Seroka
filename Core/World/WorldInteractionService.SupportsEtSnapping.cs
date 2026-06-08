using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private ItemPhysique ResoudreCadrePorteDepuisNoeud(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur is ItemPhysique item && item.IsInGroup("BlocsPoses") && EstIdMurBoisCadrePorte(item.ID_Objet))
                return item;
        }
        return null;
    }

    private ItemPhysique TrouverCadrePorteProche(Vector3 worldPoint)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return null;
        ItemPhysique meilleur = null;
        float meilleurScore = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdMurBoisCadrePorte(ip.ID_Objet))
                continue;
            float dx = Mathf.Abs(ip.GlobalPosition.X - worldPoint.X);
            float dz = Mathf.Abs(ip.GlobalPosition.Z - worldPoint.Z);
            if (dx > MurLargeurMetres * 0.65f || dz > MurLargeurMetres * 0.65f)
                continue;
            float dy = Mathf.Abs((ip.GlobalPosition.Y + MurHauteurMetres * 0.5f) - worldPoint.Y);
            if (dy > 2.4f)
                continue;
            float score = dx * dx + dz * dz + dy * dy * 0.35f;
            if (score < meilleurScore)
            {
                meilleurScore = score;
                meilleur = ip;
            }
        }
        return meilleur;
    }

    private bool CadrePossedeDejaPorte(ItemPhysique cadre)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return false;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdPorteBois(ip.ID_Objet))
                continue;
            float yBaseCadre = cadre.GlobalPosition.Y - (MurHauteurMetres * 0.5f);
            float yCentrePorteAttendu = yBaseCadre + PorteHauteurMetres * 0.5f;
            if (Mathf.Abs(ip.GlobalPosition.X - cadre.GlobalPosition.X) <= 0.18f
                && Mathf.Abs(ip.GlobalPosition.Z - cadre.GlobalPosition.Z) <= 0.18f
                && Mathf.Abs(ip.GlobalPosition.Y - yCentrePorteAttendu) <= 0.3f)
                return true;
        }
        return false;
    }

    private ItemPhysique TrouverSupportMurProche(Vector3 worldPoint)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return null;
        ItemPhysique meilleur = null;
        float meilleurScore = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !(EstIdMuret(ip.ID_Objet) || EstIdMurBois(ip.ID_Objet)))
                continue;
            Vector3 dims = ObtenirDimensionsApproxStructurePose(ip.ID_Objet);
            float dx = Mathf.Abs(ip.GlobalPosition.X - worldPoint.X);
            float dz = Mathf.Abs(ip.GlobalPosition.Z - worldPoint.Z);
            if (dx > dims.X * 0.65f || dz > dims.X * 0.65f)
                continue;
            float dy = Mathf.Abs((ip.GlobalPosition.Y + dims.Y * 0.5f) - worldPoint.Y);
            if (dy > 2.2f)
                continue;
            float score = dx * dx + dz * dz + dy * dy * 0.3f;
            if (score < meilleurScore)
            {
                meilleurScore = score;
                meilleur = ip;
            }
        }
        return meilleur;
    }

    private ItemPhysique ResoudreMuretDepuisNoeud(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur is ItemPhysique item && item.IsInGroup("BlocsPoses") && (EstIdMuret(item.ID_Objet) || EstIdMurBois(item.ID_Objet)))
                return item;
        }
        return null;
    }

    private ItemPhysique TrouverMuretProchePourSnap(Vector3 worldPoint)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return null;
        ItemPhysique meilleur = null;
        float meilleurScore = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !(EstIdMuret(ip.ID_Objet) || EstIdMurBois(ip.ID_Objet)))
                continue;
            Vector3 dims = ObtenirDimensionsApproxStructurePose(ip.ID_Objet);
            float emprise = Mathf.Max(dims.X, dims.Z);
            float dx = Mathf.Abs(ip.GlobalPosition.X - worldPoint.X);
            float dz = Mathf.Abs(ip.GlobalPosition.Z - worldPoint.Z);
            if (dx > emprise * 0.65f || dz > emprise * 0.65f)
                continue;
            float dy = Mathf.Abs(ip.GlobalPosition.Y - worldPoint.Y);
            if (dy > (dims.Y + MuretHauteurMetres) * 1.2f)
                continue;
            float score = dx * dx + dz * dz + dy * dy * 0.5f;
            if (score < meilleurScore)
            {
                meilleurScore = score;
                meilleur = ip;
            }
        }
        return meilleur;
    }

    private bool MuretExisteDejaSurPosition(Vector3 pointAligne)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return false;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdMuret(ip.ID_Objet))
                continue;
            if (Mathf.Abs(ip.GlobalPosition.Y - pointAligne.Y) > 0.15f)
                continue;
            if (Mathf.Abs(ip.GlobalPosition.X - pointAligne.X) <= MuretTolerancePresenceMetres
                && Mathf.Abs(ip.GlobalPosition.Z - pointAligne.Z) <= MuretTolerancePresenceMetres)
                return true;
        }
        return false;
    }

    private ItemPhysique ResoudreFondationHoteDepuisNoeud(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur is ItemPhysique item && item.IsInGroup("BlocsPoses") && EstIdFondation(item.ID_Objet))
                return item;
        }
        return null;
    }

    /// <summary>Choisit la fondation dont le plateau est sous la visée (évite la fondation voisine plus haute).</summary>
    private ItemPhysique TrouverFondationPourPlancher(Vector3 worldPoint, ItemPhysique? candidatRaycast)
    {
        if (candidatRaycast != null && EstPlateauFondationProcheDuPoint(candidatRaycast, worldPoint))
            return candidatRaycast;

        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return null;

        ItemPhysique meilleure = null;
        float meilleurScore = float.MaxValue;
        float demiEmprise = FondationPasSnapMetres * 0.5f;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdFondation(ip.ID_Objet))
                continue;
            float dx = Mathf.Abs(ip.GlobalPosition.X - worldPoint.X);
            float dz = Mathf.Abs(ip.GlobalPosition.Z - worldPoint.Z);
            if (dx > demiEmprise || dz > demiEmprise)
                continue;
            Vector3 c = ip.GlobalPosition;
            float yTop = c.Y + HauteurApproxFondationMetres;
            if (worldPoint.Y < c.Y - 0.35f)
                continue;
            if (yTop > worldPoint.Y + 0.28f)
                continue;
            float score = (worldPoint.Y - yTop) * (worldPoint.Y - yTop) * 6f + dx * dx + dz * dz;
            if (score < meilleurScore)
            {
                meilleurScore = score;
                meilleure = ip;
            }
        }
        return meilleure;
    }

    private static bool EstPlateauFondationProcheDuPoint(ItemPhysique fondation, Vector3 worldPoint)
    {
        float yTop = fondation.GlobalPosition.Y + HauteurApproxFondationMetres;
        return worldPoint.Y >= fondation.GlobalPosition.Y + HauteurApproxFondationMetres * 0.55f
            && worldPoint.Y <= yTop + 0.35f;
    }

    private ItemPhysique TrouverFondationSousPoint(Vector3 worldPoint)
        => TrouverFondationPourPlancher(worldPoint, null);

    private bool FondationPossedeDejaPlancher(Vector3 centreFondation)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return false;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdPlancher(ip.ID_Objet))
                continue;
            float dx = Mathf.Abs(ip.GlobalPosition.X - centreFondation.X);
            float dz = Mathf.Abs(ip.GlobalPosition.Z - centreFondation.Z);
            if (dx <= ToleranceSolSurFondationMetres && dz <= ToleranceSolSurFondationMetres)
                return true;
        }
        return false;
    }

    private bool EssayerCalculerApercuPlacementStructure(
        SlotInventaire mainActive,
        bool depuisInteragir,
        out Vector3 pointDeChute,
        out Vector3 pointAligne,
        out Vector3 rotationDeg,
        out bool poseValide)
    {
        pointDeChute = Vector3.Zero;
        pointAligne = Vector3.Zero;
        rotationDeg = Vector3.Zero;
        poseValide = false;
        if (mainActive.EstVide || !EstStructureFixePose(mainActive.ID))
            return false;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;
        return EssayerCalculerPoseStructureFixe(mainActive, depuisInteragir, out pointDeChute, out pointAligne, out rotationDeg, out poseValide);
    }

    private bool EssayerCalculerApercuPlacementObjetLancable(
        SlotInventaire mainActive,
        out Vector3 pointDeChute,
        out Vector3 pointAligne,
        out Vector3 rotationDeg,
        out bool poseValide)
    {
        pointDeChute = Vector3.Zero;
        pointAligne = Vector3.Zero;
        rotationDeg = Vector3.Zero;
        poseValide = false;
        if (mainActive.EstVide || !EstObjetLancableAuMaintien(mainActive))
            return false;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;

        Vector3 pointImpact = _rayon.GetCollisionPoint();
        Vector3 normaleImpact = _rayon.GetCollisionNormal();
        pointDeChute = pointImpact + (normaleImpact * 0.1f);
        pointAligne = pointDeChute;
        rotationDeg = new Vector3(_rotationManuelleX, _rotationManuelleY, _rotationManuelleZ);

        float distance = GlobalPosition.DistanceTo(pointDeChute);
        poseValide = distance >= 0.55f;
        return true;
    }

    /// <summary>Structures fixes: conserve la rotation manuelle et fige X/Z sur la visée, seul Y reste recalé par la physique.</summary>
    private void AppliquerTransformPoseStructure(Node3D structure, Vector3 pointDeChute)
    {
        int idObjet = 0;
        if (structure is ItemPhysique item)
            idObjet = item.ID_Objet;
        else if (structure.HasMeta("ID_Matiere"))
            idObjet = structure.GetMeta("ID_Matiere").AsInt32();

        Vector3 pointAligne = pointDeChute;
        bool estFondation = EstIdFondation(idObjet);
        if (estFondation)
            pointAligne = CalculerPositionPoseFondation(pointDeChute, FondationReposantSurFondationOuStructure(pointDeChute));
        Vector3 rotation = EstStructureFixePose(idObjet)
            ? CalculerRotationStructureFixe(idObjet)
            : new Vector3(_rotationManuelleX, _rotationManuelleY, _rotationManuelleZ);
        AppliquerTransformPoseStructure(structure, pointAligne, rotation);
    }

    private Vector3 CalculerRotationStructureFixe(int idObjet = 0)
    {
        float pas = (EstIdPlancher(idObjet) || EstIdToitChaume(idObjet)) ? PasRotationSolBoisDegres : PasRotationStructuresFixesDegres;
        float rotationY = Mathf.Round(_rotationManuelleY / pas) * pas;
        rotationY = Mathf.PosMod(rotationY, 360f);
        return new Vector3(0f, rotationY, 0f);
    }

    private void AppliquerTransformPoseStructure(Node3D structure, Vector3 pointAligne, Vector3 rotationDeg)
    {
        Vector3 pos = structure.GlobalPosition;
        structure.GlobalPosition = new Vector3(pointAligne.X, pos.Y, pointAligne.Z);
        structure.GlobalRotationDegrees = rotationDeg;
        if (structure is RigidBody3D rb)
        {
            rb.LinearVelocity = Vector3.Zero;
            rb.AngularVelocity = Vector3.Zero;
            rb.Sleeping = true;
        }
    }

    /// <summary>
    /// Recale un objet lançable posé pour que le bas visuel de son mesh touche le sol.
    /// Ne s'applique qu'au placement (pas au lancer), afin d'éviter les modèles semi-enterrés.
    /// </summary>
    private void AjusterPoseObjetLancableAuSol(Node3D objetPose)
    {
        if (objetPose == null || !GodotObject.IsInstanceValid(objetPose))
            return;
        if (!EssayerCalculerMinYMondeMeshes(objetPose, out float minYMonde))
            return;

        var espace = GetWorld3D()?.DirectSpaceState;
        if (espace == null)
            return;

        Vector3 origine = objetPose.GlobalPosition + Vector3.Up * 4f;
        Vector3 dest = objetPose.GlobalPosition + Vector3.Down * 8f;
        var q = PhysicsRayQueryParameters3D.Create(origine, dest);
        q.CollideWithAreas = false;
        if (objetPose is CollisionObject3D co)
            q.Exclude = new Godot.Collections.Array<Rid> { co.GetRid() };

        Godot.Collections.Dictionary hit = espace.IntersectRay(q);
        if (hit == null || hit.Count == 0 || !hit.ContainsKey("position"))
            return;

        float hitY = ((Vector3)hit["position"]).Y;
        objetPose.GlobalPosition += Vector3.Up * (hitY - minYMonde + 0.004f);
    }

    private static bool EssayerCalculerMinYMondeMeshes(Node3D racine, out float minYMonde)
    {
        minYMonde = float.MaxValue;
        if (racine == null || !GodotObject.IsInstanceValid(racine))
            return false;

        var pile = new List<(Node noeud, Transform3D monde)>();
        pile.Add((racine, racine.GlobalTransform));
        bool trouve = false;

        for (int i = 0; i < pile.Count; i++)
        {
            (Node noeud, Transform3D monde) courant = pile[i];
            foreach (Node enfant in courant.noeud.GetChildren())
            {
                Transform3D mondeEnfant = courant.monde;
                if (enfant is Node3D n3)
                    mondeEnfant = courant.monde * n3.Transform;

                pile.Add((enfant, mondeEnfant));

                if (enfant is not MeshInstance3D mi || mi.Mesh == null)
                    continue;

                Aabb box = mi.Mesh.GetAabb();
                for (int cx = 0; cx <= 1; cx++)
                {
                    for (int cy = 0; cy <= 1; cy++)
                    {
                        for (int cz = 0; cz <= 1; cz++)
                        {
                            Vector3 coinLocal = box.Position + new Vector3(
                                cx == 0 ? 0f : box.Size.X,
                                cy == 0 ? 0f : box.Size.Y,
                                cz == 0 ? 0f : box.Size.Z);
                            Vector3 coinMonde = mondeEnfant * coinLocal;
                            if (coinMonde.Y < minYMonde)
                                minYMonde = coinMonde.Y;
                            trouve = true;
                        }
                    }
                }
            }
        }

        return trouve;
    }

    private bool SontFondationsAdjacentes(float dx, float dz)
    {
        bool adjacentX = dz <= FondationToleranceAxeSecondaireMetres
            && Mathf.Abs(dx - FondationDistanceCentreAdjacente) <= FondationToleranceAxePrincipalMetres;
        bool adjacentZ = dx <= FondationToleranceAxeSecondaireMetres
            && Mathf.Abs(dz - FondationDistanceCentreAdjacente) <= FondationToleranceAxePrincipalMetres;
        return adjacentX || adjacentZ;
    }

    /// <summary>Y du pivot fondation (base du bloc ~1 m). Sur fondation : 1 étage au-dessus + offset molette ; au sol : contact + offset.</summary>
    private float CalculerYPoseFondation(float yContactOuSol, ItemPhysique? fondationReference)
    {
        if (fondationReference != null)
        {
            int etagesAuDessus = 1 + _offsetEtagesFondationManuel;
            return fondationReference.GlobalPosition.Y + etagesAuDessus * HauteurApproxFondationMetres + MargeEmpilementStructureMetres;
        }
        return yContactOuSol + _offsetEtagesFondationManuel * HauteurApproxFondationMetres;
    }

    private void AjusterOffsetEtagesFondation(int delta)
    {
        int avant = _offsetEtagesFondationManuel;
        _offsetEtagesFondationManuel = Mathf.Clamp(_offsetEtagesFondationManuel + delta, -OffsetEtagesFondationMax, OffsetEtagesFondationMax);
        if (_offsetEtagesFondationManuel == avant)
            return;
        GD.Print(_offsetEtagesFondationManuel == 0
            ? "ZERO-K : Hauteur fondation — 1 étage au-dessus de la cible (molette / Page Haut-Bas)."
            : $"ZERO-K : Hauteur fondation — +{_offsetEtagesFondationManuel} étage(s) supplémentaire(s) (~{_offsetEtagesFondationManuel * HauteurApproxFondationMetres:0.##} m).");
    }

    private void AjusterModeSnapMuret(int delta)
    {
        const int nbModes = 4;
        int mode = (_modeSnapMuretManuel + delta) % nbModes;
        if (mode < 0)
            mode += nbModes;
        _modeSnapMuretManuel = mode;
        string nomMode = _modeSnapMuretManuel switch
        {
            ModeSnapMuretFondation => "Fondation",
            ModeSnapMuretMuret => "Muret",
            ModeSnapMuretTerrain => "Terrain",
            _ => "Auto"
        };
        GD.Print($"ZERO-K : Snap muret = {nomMode} (molette).");
    }

    /// <summary>True si une fondation ou structure fixe est juste sous la position (empilement, pas sol libre).</summary>
    private bool FondationReposantSurFondationOuStructure(Vector3 pos)
    {
        var espace = GetWorld3D()?.DirectSpaceState;
        if (espace == null)
            return false;
        float profondeur = HauteurApproxFondationMetres + 0.35f;
        var q = PhysicsRayQueryParameters3D.Create(pos + Vector3.Up * 0.08f, pos + Vector3.Down * profondeur);
        q.CollideWithAreas = false;
        var hit = espace.IntersectRay(q);
        if (hit.Count == 0 || !hit.ContainsKey("collider"))
            return false;
        Node noeud = NoeudDepuisColliderRaycast(hit["collider"].AsGodotObject());
        ItemPhysique support = ResoudreStructureSupportDepuisNoeud(noeud);
        if (support == null || !EstStructureFixePose(support.ID_Objet))
            return false;
        float ySupport = support.GlobalPosition.Y + ObtenirDimensionsApproxStructurePose(support.ID_Objet).Y;
        return pos.Y >= ySupport + MargeEmpilementStructureMetres - 0.08f;
    }

    /// <summary>Fondation sous le curseur (visée dessus) : centre X/Z pour empilement.</summary>
    private bool EssayerResoudreFondationHoteEmpilement(Vector3 pointDeChute, out ItemPhysique hote, out Vector3 xzCentre)
    {
        hote = null;
        xzCentre = pointDeChute;
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return false;
        float meilleurScore = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdFondation(ip.ID_Objet))
                continue;
            Vector3 c = ip.GlobalPosition;
            float yTop = c.Y + HauteurApproxFondationMetres;
            if (pointDeChute.Y + 0.15f < yTop - FondationToleranceDessusMetres)
                continue;
            float dx = Mathf.Abs(pointDeChute.X - c.X);
            float dz = Mathf.Abs(pointDeChute.Z - c.Z);
            if (dx > FondationToleranceEmpilementXZMetres || dz > FondationToleranceEmpilementXZMetres)
                continue;
            float score = dx + dz;
            if (score >= meilleurScore)
                continue;
            meilleurScore = score;
            hote = ip;
            xzCentre = new Vector3(c.X, pointDeChute.Y, c.Z);
        }
        return hote != null;
    }

    /// <summary>Fondation : première pose libre, puis snap doux uniquement près d'une fondation existante (même étage).</summary>
    private Vector3 CalculerPositionPoseFondation(Vector3 pointDeChute, bool empilementPrioritaire = false)
    {
        if (empilementPrioritaire)
            return pointDeChute;

        Vector3 meilleur = pointDeChute;
        float meilleurDistSq = float.MaxValue;
        bool meilleurAxePrefere = false;
        bool meilleurSignePrefere = false;
        int meilleurOrdre = int.MaxValue;
        bool candidatTrouve = false;
        bool fondationExistante = false;
        float rayonSq = FondationRayonSnapDouxMetres * FondationRayonSnapDouxMetres;
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return pointDeChute;

        void EvaluerCandidat(Vector3 p, bool candidatSurAxeX, bool candidatPositif, bool axePrefereX, bool signePreferePositif, int ordreCandidat)
        {
            float dx = p.X - pointDeChute.X;
            float dz = p.Z - pointDeChute.Z;
            float distSq = dx * dx + dz * dz;
            if (distSq > rayonSq)
                return;
            bool axePrefere = candidatSurAxeX == axePrefereX;
            bool signePrefere = candidatPositif == signePreferePositif;
            const float epsilonDist = 0.0001f;
            bool meilleurParDistance = distSq < (meilleurDistSq - epsilonDist);
            bool distanceQuasiEgale = Mathf.Abs(distSq - meilleurDistSq) <= epsilonDist;
            bool meilleurParPreference = distanceQuasiEgale
                && ((axePrefere && !meilleurAxePrefere)
                    || (axePrefere == meilleurAxePrefere && signePrefere && !meilleurSignePrefere)
                    || (axePrefere == meilleurAxePrefere && signePrefere == meilleurSignePrefere && ordreCandidat < meilleurOrdre));
            if (!candidatTrouve || meilleurParDistance || meilleurParPreference)
            {
                candidatTrouve = true;
                meilleurDistSq = distSq;
                meilleurAxePrefere = axePrefere;
                meilleurSignePrefere = signePrefere;
                meilleurOrdre = ordreCandidat;
                meilleur = new Vector3(p.X, pointDeChute.Y, p.Z);
            }
        }

        foreach (Node n in nodes)
        {
            if (n is not ItemPhysique ip || !EstIdFondation(ip.ID_Objet))
                continue;
            fondationExistante = true;
            Vector3 c = ip.GlobalPosition;
            if (Mathf.Abs(pointDeChute.Y - c.Y) > FondationToleranceDessusMetres)
                continue;
            Vector3 delta = pointDeChute - c;
            bool axePrefereX = Mathf.Abs(delta.X) >= Mathf.Abs(delta.Z);
            bool signePreferePositif = axePrefereX ? delta.X >= 0f : delta.Z >= 0f;
            EvaluerCandidat(new Vector3(c.X + FondationDistanceCentreAdjacente, c.Y, c.Z), candidatSurAxeX: true, candidatPositif: true, axePrefereX, signePreferePositif, ordreCandidat: 0);
            EvaluerCandidat(new Vector3(c.X - FondationDistanceCentreAdjacente, c.Y, c.Z), candidatSurAxeX: true, candidatPositif: false, axePrefereX, signePreferePositif, ordreCandidat: 1);
            EvaluerCandidat(new Vector3(c.X, c.Y, c.Z + FondationDistanceCentreAdjacente), candidatSurAxeX: false, candidatPositif: true, axePrefereX, signePreferePositif, ordreCandidat: 2);
            EvaluerCandidat(new Vector3(c.X, c.Y, c.Z - FondationDistanceCentreAdjacente), candidatSurAxeX: false, candidatPositif: false, axePrefereX, signePreferePositif, ordreCandidat: 3);
        }

        if (!fondationExistante || !candidatTrouve)
            return pointDeChute;
        return meilleur;
    }

    private Vector3 ObtenirDimensionsApproxStructurePose(int idObjet)
    {
        if (EstIdFondation(idObjet))
            return new Vector3(FondationDistanceCentreAdjacente, HauteurApproxFondationMetres, FondationDistanceCentreAdjacente);
        if (EstIdPlancher(idObjet))
            return new Vector3(PlancherEmpriseMetres, PlancherEpaisseurMetres, PlancherEmpriseMetres);
        if (EstIdMuret(idObjet))
            return new Vector3(MuretLongueurMetres, MuretHauteurMetres, MuretEpaisseurMetres);
        if (EstIdMurBois(idObjet))
            return new Vector3(MurLargeurMetres, MurHauteurMetres, MurEpaisseurMetres);
        if (EstIdPorteBois(idObjet))
            return new Vector3(PorteLargeurMetres, PorteHauteurMetres, PorteEpaisseurMetres);
        if (EstIdToitChaume(idObjet))
            return new Vector3(ToitChaumePasGrilleMetres, ToitChaumeHauteurMetres, ToitChaumePasGrilleMetres);
        if (EstIdTorche(idObjet))
            return new Vector3(TorcheRayonMetres * 2f, TorcheHauteurMetres, TorcheRayonMetres * 2f);
        if (idObjet == 200)
            return new Vector3(1.2f, 1.0f, 0.9f);
        if (idObjet == IdObjetTableBoisDecorative)
            return new Vector3(1.2f, 1.0f, 0.9f);
        if (idObjet == IdObjetTableArtisanaTier1)
            return new Vector3(1.35f, 1.05f, 1.0f);
        if (idObjet == IdObjetTableAnalyseTier1)
            return new Vector3(1.53f, 1.20f, 1.20f);
        if (idObjet == IdObjetRackBatons || idObjet == IdObjetRackBuches)
            return new Vector3(0.95f, 0.72f, 0.62f);
        if (idObjet == IdObjetCoffreBoisTier0)
            return new Vector3(0.58f, 0.42f, 0.48f);
        if (EstIdFourTorchie(idObjet))
            return new Vector3(TailleFourTorchiePoseMetres, TailleFourTorchiePoseMetres * 0.55f, TailleFourTorchiePoseMetres);
        if (EstIdPitFeu(idObjet))
            return new Vector3(0.98f, 0.45f, 0.98f);
        return new Vector3(0.8f, 0.8f, 0.8f);
    }

    private bool EstPositionStructureLibre(int idObjet, Vector3 pointDeChute, Vector3 pointAligne, float? yBaseFondationPlancher = null)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return true;
        Vector3 dimsPose = ObtenirDimensionsApproxStructurePose(idObjet);
        Vector3 posPose = new Vector3(pointAligne.X, pointDeChute.Y, pointAligne.Z);
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstStructureFixePose(ip.ID_Objet))
                continue;
            Vector3 posRef = ip.GlobalPosition;
            float dx = Mathf.Abs(posPose.X - posRef.X);
            float dz = Mathf.Abs(posPose.Z - posRef.Z);
            // Planchers voisins sur fondations adjacentes (~3,985 m) : autorisés (un plancher par fondation).
            if (EstIdPlancher(idObjet) && EstIdPlancher(ip.ID_Objet))
            {
                if (dx <= ToleranceSolSurFondationMetres && dz <= ToleranceSolSurFondationMetres)
                    return false;
                continue;
            }
            // Règle gameplay demandée : pour poser un plancher sur une fondation valide,
            // rien d'autre qu'un autre plancher ne doit bloquer la pose.
            if (EstIdPlancher(idObjet))
                continue;
            // Autres fondations voisines (souvent plus hautes) : n'empêchent pas le plancher sur la fondation visée.
            if (EstIdPlancher(idObjet) && EstIdFondation(ip.ID_Objet))
                continue;
            // Les murets en bordure ne doivent pas bloquer la pose d'un plancher.
            if (EstIdPlancher(idObjet) && EstIdMuret(ip.ID_Objet))
                continue;
            // Les murs et portes au-dessus d'une fondation ne doivent pas bloquer la pose d'un plancher.
            if (EstIdPlancher(idObjet) && (EstIdMurBois(ip.ID_Objet) || EstIdPorteBois(ip.ID_Objet)))
                continue;
            // Le muret est volontairement "accolé" à une fondation : ce contact ne bloque pas la pose.
            if (EstIdMuret(idObjet) && EstIdFondation(ip.ID_Objet))
                continue;
            // La porte est volontairement imbriquée dans le mur cadre de porte.
            if (EstIdPorteBois(idObjet) && EstIdMurBoisCadrePorte(ip.ID_Objet))
                continue;
            // Le battant de porte est contenu dans la travée du mur: autoriser le recouvrement avec les supports.
            if (EstIdPorteBois(idObjet) && (EstIdMuret(ip.ID_Objet) || EstIdMurBois(ip.ID_Objet)))
                continue;
            // Un seul battant par cadre.
            if (EstIdPorteBois(idObjet) && EstIdPorteBois(ip.ID_Objet))
            {
                if (dx <= 0.2f && dz <= 0.2f && Mathf.Abs(posPose.Y - posRef.Y) <= 0.35f)
                    return false;
                continue;
            }
            Vector3 dimsRef = ObtenirDimensionsApproxStructurePose(ip.ID_Objet);
            float yTolerance = Mathf.Min(dimsPose.Y, dimsRef.Y) - MargeChevauchementMetres;
            if (yBaseFondationPlancher.HasValue && EstIdFondation(ip.ID_Objet)
                && Mathf.Abs(posRef.Y - yBaseFondationPlancher.Value) > 0.35f)
                continue;
            if (Mathf.Abs(posPose.Y - posRef.Y) > yTolerance)
                continue;
            float xTolerance = ((dimsPose.X + dimsRef.X) * 0.5f) - MargeChevauchementMetres;
            float zTolerance = ((dimsPose.Z + dimsRef.Z) * 0.5f) - MargeChevauchementMetres;
            if (dx < xTolerance && dz < zTolerance)
                return false;
        }
        return true;
    }

    private ItemPhysique ResoudreStructureSupportDepuisNoeud(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur is ItemPhysique item && item.IsInGroup("BlocsPoses") && EstStructureFixePose(item.ID_Objet))
                return item;
        }
        return null;
    }

    private bool EssayerAjusterStructureSansChevauchement(int idObjet, ref Vector3 pointDeChute, ref Vector3 pointAligne)
    {
        if (!EstStructureFixePose(idObjet))
            return true;
        if (EstIdPlancher(idObjet))
            return EstPositionStructureLibre(idObjet, pointDeChute, pointAligne);
        if (EstPositionStructureLibre(idObjet, pointDeChute, pointAligne))
            return true;

        float baseStep = Mathf.Max(0.32f, Mathf.Max(ObtenirDimensionsApproxStructurePose(idObjet).X, ObtenirDimensionsApproxStructurePose(idObjet).Z) * 0.55f);
        Vector2[] directions = new Vector2[]
        {
            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, -1f),
            new Vector2(0.70710677f, 0.70710677f),
            new Vector2(-0.70710677f, 0.70710677f),
            new Vector2(0.70710677f, -0.70710677f),
            new Vector2(-0.70710677f, -0.70710677f),
        };
        float[] yOffsets = EstIdFondation(idObjet)
            ? new float[] { 0f, HauteurApproxFondationMetres, HauteurApproxFondationMetres * 2f, HauteurApproxFondationMetres * 3f }
            : (EstIdMuret(idObjet)
                ? new float[] { 0f, MuretHauteurMetres, MuretHauteurMetres * 2f, 0.2f, -0.2f }
                : new float[] { 0f, 0.2f, -0.2f, 0.4f });

        for (int ring = 1; ring <= 4; ring++)
        {
            float step = baseStep * ring;
            for (int d = 0; d < directions.Length; d++)
            {
                for (int y = 0; y < yOffsets.Length; y++)
                {
                    Vector3 candidatChute = pointDeChute + new Vector3(directions[d].X * step, yOffsets[y], directions[d].Y * step);
                    ItemPhysique? fondationRef = null;
                    bool empileCandidat = false;
                    if (EstIdFondation(idObjet))
                    {
                        if (EssayerResoudreFondationHoteEmpilement(candidatChute, out ItemPhysique hote, out Vector3 xzCentre))
                        {
                            fondationRef = hote;
                            empileCandidat = true;
                            candidatChute.X = xzCentre.X;
                            candidatChute.Z = xzCentre.Z;
                        }
                        candidatChute.Y = CalculerYPoseFondation(candidatChute.Y, fondationRef);
                    }
                    Vector3 candidatAligne = EstIdFondation(idObjet)
                        ? CalculerPositionPoseFondation(candidatChute, empileCandidat)
                        : candidatChute;
                    if (!EstPositionStructureLibre(idObjet, candidatChute, candidatAligne))
                        continue;
                    pointDeChute = candidatChute;
                    pointAligne = candidatAligne;
                    return true;
                }
            }
        }
        return false;
    }

    private bool EstObjetLancableAuMaintien(SlotInventaire slot)
    {
        if (slot.EstVide) return false;
        bool estTerrainVoxel = EstSlotTerrainVoxelPosable(slot);
        bool estAtelier = slot.ID == 200;
        bool estTableAnalyse = slot.ID == IdObjetTableAnalyseTier1;
        bool estRackBatons = slot.ID == IdObjetRackBatons || slot.ID == IdObjetRackBuches;
        bool estBuisson = slot.ID == 10 || slot.ID == 11;
        bool estCoffre = slot.ID == IdObjetCoffreBoisTier0;
        bool estPitFeu = EstIdPitFeu(slot.ID) || EstIdFourTorchie(slot.ID) || EstIdFondation(slot.ID) || EstIdPlancher(slot.ID) || EstIdMuret(slot.ID) || EstIdMurBois(slot.ID) || EstIdPorteBois(slot.ID) || EstIdToitChaume(slot.ID);
        if (ItemPhysique.EstPinceOsPorteObjet(slot))
            return false;
        return !estTerrainVoxel && !estAtelier && !estTableAnalyse && !estRackBatons && !estBuisson && !estCoffre && !estPitFeu;
    }

    /// <summary>Clic droit court : si la visée est le sol et l’outil peut faucher, exécute le même fauchage que le clic gauche (gazon 3D → fibres).</summary>
    /// <returns>True si le fauchage a été traité (ne pas enchaîner sur la pose au sol).</returns>
    private bool ExecuterFauchageSolPrioritaireClicDroit()
    {
        AssurerDurabiliteOutilsSurLesMains();
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide || !PeutUtiliserFrappe(mainActive))
            return false;

        if (!EstOutilFaucheurEnMain(mainActive))
            return false;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (!EstSolViseParRayon(_rayon, objetTouche) && !EstSurfaceHorizontaleFauchable())
            return false;

        Vector3 directionMouvement = -_camera.GlobalTransform.Basis.Z.Normalized();
        var (effHache, effPelle, masseOutil) = AnalyserOutilCAO(directionMouvement);
        ExecuterCreusage(1f, effPelle, masseOutil, _rayon.GetCollisionPoint());
        JouerAnimationFrappe(TypeMouvementFrappe.Estoc);
        return true;
    }

    /// <summary>Terrain voxel / sections de sol : creusage (pelle) ou fauchage (lame) selon l’outil émergent.</summary>
    /// <remarks>Le raycast touche souvent le <see cref="CollisionShape3D"/> enfant ; le <see cref="StaticBody3D"/> s’appelle <c>CollisionSection_*</c>.</remarks>
    private static bool EstSurfaceTerrainVisee(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur.IsInGroup("Terrain")) return true;
            string nm = cur.Name.ToString();
            if (nm.Contains("Terrain") || nm.Contains("CollisionSection")) return true;
        }
        return false;
    }

    private bool EstNoeudSupportStructure(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (!cur.IsInGroup("BlocsPoses"))
                continue;
            if (cur is ItemPhysique item)
                return EstStructureFixePose(item.ID_Objet);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Sol du monde procédural (Monde_Client) : corps créés uniquement via <see cref="PhysicsServer3D"/> sans nœud <see cref="CollisionObject3D"/>.
    /// Dans ce cas <see cref="RayCast3D.GetCollider"/> est souvent <c>null</c> alors que <see cref="RayCast3D.IsColliding"/> est vrai.
    /// </summary>
    private static bool EstSolMondeSansColliderNode(RayCast3D rayon)
    {
        if (!rayon.IsColliding()) return false;
        if (rayon.GetCollider() != null) return false;
        return rayon.GetCollisionNormal().Y >= 0.18f;
    }

    /// <summary>True si la visée est le sol (nœuds terrain legacy OU mesh monde AAA sans objet associé au raycast).</summary>
    private static bool EstSolViseParRayon(RayCast3D rayon, Node noeudDepuisCollider)
    {
        return EstSurfaceTerrainVisee(noeudDepuisCollider) || EstSolMondeSansColliderNode(rayon);
    }

    private bool EstSurfaceSupportStructureVisee(RayCast3D rayon, Node noeudDepuisCollider)
    {
        if (EstSolViseParRayon(rayon, noeudDepuisCollider))
            return true;
        if (!rayon.IsColliding())
            return false;
        if (rayon.GetCollisionNormal().Y < NormaleSupportStructureMinY)
            return false;
        return EstNoeudSupportStructure(noeudDepuisCollider);
    }

    /// <summary>Collider Jolt = souvent <see cref="CollisionShape3D"/> ; on remonte au corps pour groupes / noms.</summary>
    private static Node NoeudDepuisColliderRaycast(GodotObject collider)
    {
        if (collider == null) return null;
        if (collider is CollisionShape3D sh)
            return sh.GetParent() as Node ?? sh;
        return collider as Node;
    }

    /// <summary>Remonte l'arbre depuis un collider ou mesh jusqu'à l'<see cref="ItemPhysique"/> posé.</summary>
    private static ItemPhysique ObtenirItemPhysiqueDepuisNoeud(Node noeud)
    {
        Node courant = noeud;
        while (courant != null)
        {
            if (courant is ItemPhysique item)
                return item;
            courant = courant.GetParent();
        }
        return null;
    }
}
