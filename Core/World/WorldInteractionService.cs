using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    /// <summary>E : si la main active tient un objet → accrocher (corde) ou poser (flexible / autres) ; sinon ramasser.</summary>
    private void ExecuterToucheInteragir()
    {
        _rayon.ForceRaycastUpdate();
        if (_rayon.IsColliding())
        {
            Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
            var itemTouche = objetTouche as ItemPhysique
                ?? (objetTouche as Node)?.GetParent() as ItemPhysique
                ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");

                if (itemTouche != null && itemTouche.ID_Objet == 200)
                {
                    if (Input.IsKeyPressed(Key.Shift))
                    {
                        ExecuterRamassageObjet();
                        return;
                    }

                // E (seul) = Ouvrir le plan de travail (Grille 3x3)
                else
                {
                    if (_menuAnatomie != null)
                    {
                        AtelierPlanTravailOuvert = itemTouche;
                        CraftGrille3x3AuTable = true;
                        if (!_menuAnatomie.EstOuvert)
                            _menuAnatomie.BasculerVisibilite();
                        else
                            _menuAnatomie.RafraichirMenu();

                        GetViewport().SetInputAsHandled();
                        GD.Print("ZERO-K : Plan de travail 3x3 de l'Atelier ouvert.");
                    }
                }
                return;
            }
        }

        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (!mainActive.EstVide)
        {
            if (mainActive.ID == 20)
            {
                if (ExecuterAttacheCordeSiPossible(mainActive))
                    return;
                ExecuterPlacementDepuisInteragir(mainActive);
                return;
            }
            if (mainActive.ID == 21)
            {
                ExecuterPlacementDepuisInteragir(mainActive);
                return;
            }
            if (EstMatiereFlexible(mainActive.ID))
            {
                ExecuterPlacementDepuisInteragir(mainActive);
                return;
            }
            if (EstObjetPosableAuSol(mainActive))
            {
                ExecuterPlacementDepuisInteragir(mainActive);
                return;
            }
            GD.Print("ZERO-K : Cet objet ne se pose pas avec E (utilisez le clic droit pour le terrain / certains cas).");
            return;
        }
        ExecuterRamassageObjet();
    }

    /// <summary>True si la corde ou la fibre peut « s'étirer » visuellement (ScaleEclat) : les deux brins de la corde doivent être étirables.</summary>
    public static bool ObtenirSlotFlexibleEtirable(SlotInventaire s)
    {
        if (s.ID == 20 || s.ID == 21)
        {
            bool a = Atlas_Matiere.ObtenirProfilFlexible(s.IndexChimique, out var pa) && pa.Etirable;
            bool b = Atlas_Matiere.ObtenirProfilFlexible(s.IndexMorphologique, out var pb) && pb.Etirable;
            return a && b;
        }
        if (EstMatiereFlexible(s.ID))
            return Atlas_Matiere.ObtenirProfilFlexible(s.ID, out var p) && p.Etirable;
        return false;
    }

    /// <summary>Échelle pour l’établi CAO (hors 30/32, gérés à part) : fibres/corde non élastiques = taille naturelle, sans ScaleEclat « étiré ».</summary>
    public static Vector3 ObtenirEchellePieceFlexibleCAO(SlotInventaire slot)
    {
        bool estFlexOuCorde = slot.ID == 15 || slot.ID == 16 || slot.ID == 17 || slot.ID == 20 || slot.ID == 21;
        if (estFlexOuCorde && !ObtenirSlotFlexibleEtirable(slot))
            return Vector3.One;
        if (slot.ScaleEclat != Vector3.Zero)
            return slot.ScaleEclat;
        return Vector3.One;
    }

    /// <summary>Fibres + corde : manipulation fine sur le plan de l’établi (rayon réduit).</summary>
    public static bool EstFlexibleOuCordePourPlanCAO(int idObjet) => idObjet is 15 or 16 or 17 or 20 or 21;

    private static bool EstObjetPosableAuSol(SlotInventaire s)
    {
        if (s.EstVide || s.ID == 0) return false;
        if (s.ID >= 1 && s.ID <= 9 && s.ID != 4) return true;
        return s.ID == 999 || ItemPhysique.EstIdRocheMatiere(s.ID) || s.ID == 30 || s.ID == 32 || s.ID == 34 || s.ID == 21 || s.ID == 200;
    }

    /// <summary>Corde (20) : accrocher au point de visée si surface valide (sol, roche, arbre, bloc posé).</summary>
    private bool ExecuterAttacheCordeSiPossible(SlotInventaire mainCorde)
    {
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return false;
        Node col = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (col == null) return false;
        if (col == this || col.IsAncestorOf(this) || IsAncestorOf(col)) return false;

        bool ancre = col is StaticBody3D || col is RigidBody3D || ResoudreRigidBodyDepuisCollider(col) != null || col.IsInGroup("BlocsPoses") || ObtenirArbreDepuisCollider(col) != null;
        if (!ancre) return false;

        Vector3 pt = _rayon.GetCollisionPoint();
        Vector3 n = _rayon.GetCollisionNormal().Normalized();
        Vector3 tangent = Vector3.Up.Cross(n);
        if (tangent.LengthSquared() < 1e-4f) tangent = Vector3.Right.Cross(n);
        tangent = tangent.Normalized();

        Node3D corps = CreerBlocPose(pt + n * 0.07f, mainCorde);
        if (corps == null) return false;
        corps.SetMeta("Corde_Accrochee", true);
        corps.SetMeta("Corde_Normal", n);
        var b = Basis.LookingAt(tangent, n).Orthonormalized();
        corps.GlobalTransform = new Transform3D(b, corps.GlobalPosition);

        if (MainGaucheEstActive) MainGauche = default;
        else MainDroite = default;
        RafraichirHUD();
        GD.Print("ZERO-K : Corde accrochée à la surface (E).");
        return true;
    }

    /// <summary>Pose via E : portée courte pour fibres/corde, normale pour le reste. Respecte l’élasticité (pas d’étirement si non élastique).</summary>
    private void ExecuterPlacementDepuisInteragir(SlotInventaire mainActive)
    {
        ExecuterPlacementAvecOptions(mainActive, depuisInteragir: true);
    }

    private static string LireGenomeSurItemPhysique(ItemPhysique item)
    {
        if (item == null) return "";
        if (!string.IsNullOrEmpty(item.GenomeAssemblage)) return item.GenomeAssemblage;
        return item.HasMeta(MetaGenomeAssemblage) ? item.GetMeta(MetaGenomeAssemblage).AsString() : "";
    }

    /// <summary>Phase 2 pure : ramassage des objets physiques (Caillou, Silex, BlocsPoses). Touche E (interagir).
    /// Copie IndexCacheMemoire dans le SlotInventaire pour conserver la forme exacte.</summary>
    private void ExecuterRamassageObjet()
    {
        if (!MainGauche.EstVide && !MainDroite.EstVide) return;
        if (!_rayon.IsColliding()) return;

        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (objetTouche == null) return;

        SlotInventaire nouveauSlot = default;

        if (objetTouche.IsInGroup("BlocsPoses"))
        {
            int id = objetTouche.HasMeta("ID_Matiere") ? (int)objetTouche.GetMeta("ID_Matiere").AsInt32() : 1;
            var item = objetTouche as ItemPhysique ?? (objetTouche as Node)?.GetParent() as ItemPhysique ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            nouveauSlot = new SlotInventaire
            {
                ID = id,
                IndexMorphologique = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32)
                    ? MorphologieBoisDepuisItem(item)
                    : (item?.IndexCacheMemoire ?? 0),
                IndexChimique = item?.IndexChimique ?? 0,
                IndexTaille = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32)
                    ? Mathf.Clamp(item.IndexTailleRoche, 0, 4)
                    : (item != null && (item.ID_Objet == 105 || item.ID_Objet == 106 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet)) ? item.IndexTailleRoche : 2),
                IndexTailleLameRoche = item != null && item.ID_Objet == 105 && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : (item != null && item.ID_Objet == 105 ? 2 : 0),
                EstUnEclat = item?.EstUnEclat ?? false,
                MeshEclat = (item != null && item.EstUnEclat) ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item?.NiveauFracture ?? 0,
                // FIX CRITIQUE : bois 30/32 → meta ScaleLongueurBois ou repli sur la longueur mesh
                ScaleEclat = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32)
                    ? ScaleEclatBoisAuRamassage(item)
                    : (item != null ? item.Scale : Vector3.One),
                IndexBotanique = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == 106) ? item.IndexBotanique : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item)
            };
            if ((nouveauSlot.ID == 105 || nouveauSlot.ID == 106) && item != null)
                RemplirDurabiliteOutilDepuisItemPhysique(ref nouveauSlot, item);
        }
        else if (objetTouche is RigidBody3D rb)
        {
            // BlocChutant (fibre, buisson tombé) : pas d'ItemPhysique, on lit le meta.
            if (objetTouche is BlocChutant)
            {
                int id = objetTouche.HasMeta("ID_Matiere") ? (int)objetTouche.GetMeta("ID_Matiere").AsInt32() : 1;
                nouveauSlot = new SlotInventaire { ID = id, IndexMorphologique = 0, IndexChimique = 0 };
            }
            else
            {
            var item = rb as ItemPhysique ?? (rb as Node)?.GetParent() as ItemPhysique ?? rb.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            if (item == null) return;
            if (ItemPhysique.EstIdRocheMatiere(item.ID_Objet) && item.IndexTailleRoche >= 3)
            {
                GD.Print("ZERO-K : Masse excessive. La colonne vertébrale céderait. Action bloquée.");
                return;
            }
            nouveauSlot = new SlotInventaire
            {
                ID = item.ID_Objet,
                IndexMorphologique = item.ID_Objet == 30 || item.ID_Objet == 32 ? MorphologieBoisDepuisItem(item) : item.IndexCacheMemoire,
                IndexChimique = item.IndexChimique,
                IndexTaille = item.ID_Objet == 30 || item.ID_Objet == 32
                    ? Mathf.Clamp(item.IndexTailleRoche, 0, 4)
                    : (item.ID_Objet == 105 || item.ID_Objet == 106 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 2),
                IndexTailleLameRoche = item.ID_Objet == 105 && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : (item.ID_Objet == 105 ? 2 : 0),
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32) ? ScaleEclatBoisAuRamassage(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == 106 || item.ID_Objet == 200) ? item.IndexBotanique : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item)
            };
            if (nouveauSlot.ID == 105 || nouveauSlot.ID == 106)
                RemplirDurabiliteOutilDepuisItemPhysique(ref nouveauSlot, item);
            }
        }
        else if (objetTouche is StaticBody3D sb)
        {
            var item = sb.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            if (item == null) return;
            if (ItemPhysique.EstIdRocheMatiere(item.ID_Objet) && item.IndexTailleRoche >= 3)
            {
                GD.Print("ZERO-K : Masse excessive. La colonne vertébrale céderait. Action bloquée.");
                return;
            }
            nouveauSlot = new SlotInventaire
            {
                ID = item.ID_Objet,
                IndexMorphologique = item.ID_Objet == 30 || item.ID_Objet == 32 ? MorphologieBoisDepuisItem(item) : item.IndexCacheMemoire,
                IndexChimique = item.IndexChimique,
                IndexTaille = item.ID_Objet == 30 || item.ID_Objet == 32
                    ? Mathf.Clamp(item.IndexTailleRoche, 0, 4)
                    : (item.ID_Objet == 105 || item.ID_Objet == 106 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 2),
                IndexTailleLameRoche = item.ID_Objet == 105 && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : (item.ID_Objet == 105 ? 2 : 0),
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32) ? ScaleEclatBoisAuRamassage(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == 106) ? item.IndexBotanique : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item)
            };
            if (nouveauSlot.ID == 105 || nouveauSlot.ID == 106)
                RemplirDurabiliteOutilDepuisItemPhysique(ref nouveauSlot, item);
        }
        else
            return;

        if (MainGaucheEstActive)
        {
            if (MainGauche.EstVide) MainGauche = nouveauSlot;
            else if (MainDroite.EstVide) MainDroite = nouveauSlot;
            else return;
        }
        else
        {
            if (MainDroite.EstVide) MainDroite = nouveauSlot;
            else if (MainGauche.EstVide) MainGauche = nouveauSlot;
            else return;
        }
        objetTouche.QueueFree();
        ReinitialiserRotationManuelle();
        RafraichirHUD();
    }

    /// <summary>Placement (construction ou rejet d'objet). Clic droit.</summary>
    private void ExecuterPlacement()
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide)
        {
            GD.Print("ZERO-K : La main sélectionnée est vide. Impossible de poser.");
            return;
        }
        ExecuterPlacementAvecOptions(mainActive, depuisInteragir: false);
    }

    private void ExecuterPlacementAvecOptions(SlotInventaire mainActive, bool depuisInteragir)
    {
        if (mainActive.EstVide) return;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return;

        Vector3 pointImpact = _rayon.GetCollisionPoint();
        Vector3 normaleImpact = _rayon.GetCollisionNormal();
        Vector3 pointDeChute;

        if (mainActive.ID == 200)
        {
            Node noeudCol = NoeudDepuisColliderRaycast(_rayon.GetCollider());
            if (!EstSolViseParRayon(_rayon, noeudCol))
            {
                GD.Print("ZERO-K : Posez l’atelier sur le sol (terrain / herbe), pas sur un objet vertical.");
                return;
            }

            if (_gestionnaireMonde != null && _gestionnaireMonde.UseArchitectureReseau)
                _gestionnaireMonde.AppliquerFauchageGlobal(pointImpact, RayonFauchagePoseAtelier200);

            // FIX CRITIQUE : On supprime la lecture du voxel hSurf + 1f.
            // L'objet se pose EXACTEMENT sur le point du raycast, ancré par son pivot.
            pointDeChute = pointImpact;
        }
        else
        {
            float decalNormale = 0.1f;
            pointDeChute = pointImpact + (normaleImpact * decalNormale);
        }
        float distance = GlobalPosition.DistanceTo(pointDeChute);
        // Flexible / corde avec E : on peut poser près du corps (manipulation fine) ; clic droit garde la marge anti-auto-collision
        bool flexOuCordeE = depuisInteragir && (EstMatiereFlexible(mainActive.ID) || mainActive.ID == 20 || mainActive.ID == 21);
        // Atelier : marge courte pour poser sous la visée (évite un rejet silencieux puis une pose « ailleurs »).
        float distMin = flexOuCordeE ? 0.35f : (mainActive.ID == 200 ? 0.55f : 1.4f);
        if (distance < distMin) return;

        int id = mainActive.ID;
        if (id == 0) return;
        if (id >= 1 && id <= 9 && id != 4)
        {
            _gestionnaireMonde?.AppliquerCreationGlobale(pointImpact, normaleImpact, RAYON_SCULPTURE, id);
        }
        else if (id == 999 || ItemPhysique.EstIdRocheMatiere(id) || id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == 30 || id == 32 || id == 34 || id == 105 || id == 106 || id == 200)
        {
            Node3D nePose = CreerBlocPose(pointDeChute, mainActive);
            if (id != 200)
                AppliquerImpulsionLacherDoux(nePose);
        }
        else
        {
            GD.Print($"ZERO-K : Matière {id} non géologique. Pose ignorée.");
            return;
        }

        if (MainGaucheEstActive) MainGauche = default;
        else MainDroite = default;

        ReinitialiserRotationManuelle();
        RafraichirHUD();
    }

    /// <summary>Clic droit court : si la visée est le sol et l’outil peut faucher, exécute le même fauchage que le clic gauche (gazon 3D → fibres).</summary>
    /// <returns>True si le fauchage a été traité (ne pas enchaîner sur la pose au sol).</returns>
    private bool ExecuterFauchageSolPrioritaireClicDroit()
    {
        AssurerDurabiliteOutilsSurLesMains();
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide || !PeutUtiliserFrappe(mainActive))
            return false;

        // Roche plate (1), pointe (3) ou dague — pas la hachette pour le gazon.
        bool estOutilFaucheur = mainActive.ID == 105
            || (ItemPhysique.EstIdRocheMatiere(mainActive.ID) && (mainActive.IndexMorphologique == 1 || mainActive.IndexMorphologique == 3));
        if (!estOutilFaucheur)
            return false;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (!EstSolViseParRayon(_rayon, objetTouche))
            return false;

        Vector3 directionMouvement = -_camera.GlobalTransform.Basis.Z.Normalized();
        var (effHache, effPelle, masseOutil) = AnalyserOutilCAO(directionMouvement);
        if (effPelle >= 0.6f)
            return false;

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

    /// <summary>Collider Jolt = souvent <see cref="CollisionShape3D"/> ; on remonte au corps pour groupes / noms.</summary>
    private static Node NoeudDepuisColliderRaycast(GodotObject collider)
    {
        if (collider == null) return null;
        if (collider is CollisionShape3D sh)
            return sh.GetParent() as Node ?? sh;
        return collider as Node;
    }

    /// <summary>Hache = tranchant perpendiculaire à la frappe (<c>alignement</c> → 0). Pelle = plat aligné (<c>alignement</c> → 1).</summary>
}
