using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private Vector3 CalculerPointSpawnLancer(Vector3 direction)
    {
        direction = direction.Normalized();
        Vector3 offsetMain = _camera.GlobalTransform.Basis.X * 0.3f + _camera.GlobalTransform.Basis.Y * -0.2f;
        Vector3 orig = _camera.GlobalPosition + direction * 0.4f + offsetMain;

        var query = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition, orig + direction * 0.2f);
        query.CollisionMask = 1;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0 && hit.ContainsKey("position"))
            return (Vector3)hit["position"] - direction * 0.1f;
        return orig;
    }

    /// <summary>Vitesse cible (m/s) pour un lancer : indÃ©pendante de la masse (impulsion = mÃ—v).</summary>
    private static float ObtenirVitesseCibleLancer(float forceCharge)
    {
        float f = Mathf.Clamp(forceCharge, 0.5f, 5.0f);
        return Mathf.Lerp(8f, 24f, Mathf.InverseLerp(0.5f, 5f, f));
    }

    /// <summary>Clic court Â« poser Â» : petit Ã©lan vers la visÃ©e pour ne pas tomber comme un plomb.</summary>
    private void AppliquerImpulsionLacherDoux(Node3D nePose)
    {
        if (nePose is not RigidBody3D rb || _camera == null) return;
        rb.Sleeping = false;
        Vector3 dir = -_camera.GlobalTransform.Basis.Z;
        dir = new Vector3(dir.X, Mathf.Max(0.1f, dir.Y + 0.32f), dir.Z);
        if (dir.LengthSquared() < 1e-6f) return;
        dir = dir.Normalized();
        float m = Mathf.Max(0.012f, rb.Mass);
        float bonusLeger = Mathf.Clamp(1.22f - m * 0.028f, 0.9f, 1.28f);
        float v = 2.85f * bonusLeger;
        rb.ApplyCentralImpulse(dir * (m * v));
    }

    /// <summary>Lance lâ€™objet tenu : impulsion = masse Ã— vitesse cible (mÃªme sensation petit caillou / gros morceau).</summary>
    private void ExecuterLancer(float force)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide) return;
        if (ItemPhysique.EstPinceOsPorteObjet(mainActive))
            return;

        Vector3 direction = -_camera.GlobalTransform.Basis.Z.Normalized();
        Vector3 pointDeSpawn = CalculerPointSpawnLancer(direction);

        // Une seule unité physique (la pile reste en main jusqu'à ConsommerUneUniteMainActive).
        SlotInventaire slotLancer = mainActive;
        slotLancer.Quantite = 1;

        // 2. On invoque le bloc
        Node3D corpsCree = CreerBlocPose(pointDeSpawn, slotLancer);

        // 3. Impulsion massique : vitesse quasi constante quelle que soit la masse (les lourds partent vraiment).
        if (corpsCree is RigidBody3D rb)
        {
            rb.Sleeping = false;
            Vector3 dir = (direction + Vector3.Up * 0.15f).Normalized();
            float v = ObtenirVitesseCibleLancer(force);
            float m = Mathf.Max(0.012f, rb.Mass);
            float bonusLeger = Mathf.Clamp(1.18f - m * 0.022f, 0.9f, 1.22f);
            rb.ApplyCentralImpulse(dir * (m * v * bonusLeger));
            if (corpsCree is ItemPhysique ipLance && ItemPhysique.EstIdRocheMatiere(mainActive.ID))
                ipLance.ActiverGraceImpactAuLancer(24);
        }

        // 4. Retirer une unité de la pile (comme à la pose au sol).
        ConsommerUneUniteMainActive();
        RafraichirHUD();
        ReinitialiserRotationManuelle();

        if (!Engine.IsEditorHint())
            SauvegarderEtatPersistantMonde(GetTree());
    }

    /// <summary>Dague (105) au sol : une enveloppe convexe par mesh du GLB (même principe que l’atelier, mais convexe — trimesh concave interdit sur RigidBody dynamique).</summary>
    private static int AjouterCollisionsConvexesDepuisMeshesSousRacineItem(ItemPhysique corpsRacine, Node racineVisuel)
    {
        var pile = new List<Node> { racineVisuel };
        int nb = 0;
        for (int i = 0; i < pile.Count; i++)
        {
            foreach (Node c in pile[i].GetChildren())
            {
                pile.Add(c);
                if (c is not MeshInstance3D mi || mi.Mesh == null)
                    continue;
                Shape3D shape = mi.Mesh.CreateConvexShape(true, true);
                if (shape == null)
                    continue;
                Transform3D t = mi.Transform;
                for (Node p = mi.GetParent(); p != null && p != corpsRacine; p = p.GetParent())
                {
                    if (p is Node3D n3)
                        t = n3.Transform * t;
                }
                corpsRacine.AddChild(new CollisionShape3D
                {
                    Name = "CollisionConvexGlb" + nb,
                    Shape = shape,
                    Transform = t
                });
                nb++;
            }
        }
        return nb;
    }

    /// <summary>CrÃ©e un bloc physique posÃ© avec IndexCacheMemoire assignÃ© (forme exacte conservÃ©e au rejet). Retourne le nÅ“ud crÃ©Ã© (pour lancer avec impulsion). ItemPhysique est le RigidBody3D racine.</summary>
    private Node3D CreerBlocPose(Vector3 pointDeChute, SlotInventaire mainActive, bool modeGhost = false)
    {
        int id = mainActive.ID;
        Node3D corps;
        if (mainActive.EstUnEclat && mainActive.MeshEclat != null)
        {
            bool boisSculpte = mainActive.ID == 30 || mainActive.ID == 32;
            Vector3 scaleInv = mainActive.ScaleEclat.LengthSquared() > 1e-8f ? mainActive.ScaleEclat : Vector3.One;
            Mesh meshPose = mainActive.MeshEclat;
            Vector3 scaleRb = scaleInv;
            bool meshBoisBake = false;
            // Bois taillÃ© : cuire ScaleEclat dans les sommets (comme les Ã©clats de roche). Ã‰vite scale non uniforme sur le RigidBody3D = visuel/collision faux en jeu.
            if (boisSculpte && (scaleInv - Vector3.One).LengthSquared() > 1e-8f)
            {
                ArrayMesh baked = ItemPhysique.DupliquerMeshBakeEchelle(mainActive.MeshEclat, scaleInv);
                if (baked != null)
                {
                    meshPose = baked;
                    scaleRb = Vector3.One;
                    meshBoisBake = true;
                }
            }
            var item = new ItemPhysique
            {
                ID_Objet = mainActive.ID,
                IndexChimique = mainActive.IndexChimique,
                EstUnEclat = true,
                NiveauFracture = mainActive.NiveauFracture,
                Scale = scaleRb,
                IndexBotanique = boisSculpte ? mainActive.IndexBotanique : (byte)0,
                Name = "ItemPhysique",
                GenomeAssemblage = mainActive.GenomeAssemblage ?? ""
            };
            if (meshBoisBake)
                item.SetMeta(ItemPhysique.MetaScaleEclatInventaire, scaleInv);
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            // FIX CRITIQUE : pas de matÃ©riau gris unique â€” l'ArrayMesh forgÃ© porte ses textures par surface
            Material matVisuel = null;
            if (mainActive.ID != 100)
            {
                int chimPourRoche = ItemPhysique.EstIdRocheMatiere(mainActive.ID)
                    ? ItemPhysique.IndexChimiqueDepuisIdRoche(mainActive.ID)
                    : Mathf.Clamp(mainActive.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
                matVisuel = boisSculpte
                    ? (mainActive.ID == 32 && mainActive.IndexChimique == 1 && mainActive.IndexBotanique == LSystem_Botanique.IndexChene
                        ? ArbreVivant.ObtenirMaterielBoisTriplanarBatonChenEPale()
                        : ArbreVivant.ObtenirMaterielBoisTriplanar(mainActive.IndexBotanique))
                    : ItemPhysique.CreerMaterielProcedural(ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID), chimPourRoche);
            }
            item.AddChild(new MeshInstance3D { Name = "MeshInstance3D", Mesh = meshPose, MaterialOverride = matVisuel });
            item.AddChild(new CollisionShape3D { Name = "CollisionShape3D", Shape = ItemPhysique.CreerShapeCollisionConvexeRobuste(meshPose) });
            corps = item;
        }
        else if (id == 105)
        {
            SlotInventaire slotDague = mainActive;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slotDague);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = mainActive.IndexTaille,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            item.SetMeta(MetaDurabiliteOutilMax, slotDague.DurabiliteOutilMax);
            item.SetMeta(MetaDurabiliteOutilActuelle, slotDague.DurabiliteOutilActuelle);
            item.SetMeta(MetaTailleLameRoche, Mathf.Clamp(slotDague.IndexTailleLameRoche <= 0 ? 2 : slotDague.IndexTailleLameRoche, 0, 4));
            var meshRoot = new MeshInstance3D { Name = "MeshInstance3D" };
            InstancierModeleArme(meshRoot, slotDague, 0.625f, ObtenirFacteurEchelleLameDague(slotDague));
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new CapsuleShape3D { Radius = 0.07f, Height = 0.46f }
                });
            }
            corps = item;
        }
        else if (id == 106 || id == IdObjetHachePierreTier1)
        {
            SlotInventaire slotHachette = mainActive;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slotHachette);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = mainActive.IndexTaille,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            item.SetMeta(MetaDurabiliteOutilMax, slotHachette.DurabiliteOutilMax);
            item.SetMeta(MetaDurabiliteOutilActuelle, slotHachette.DurabiliteOutilActuelle);
            var meshRoot = new MeshInstance3D { Name = "MeshInstance3D" };
            InstancierModeleArme(meshRoot, slotHachette, 0.625f, 1f);
            item.AddChild(meshRoot);
            // Hachette/Hache : collision pilotée par le mesh GLB réel pour coller au visuel au sol.
            // (évite la box fixe qui peut désaligner l'objet et donner un effet de traversée du sol)
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new CapsuleShape3D
                    {
                        Radius = id == IdObjetHachePierreTier1 ? 0.065f : 0.06f,
                        Height = id == IdObjetHachePierreTier1 ? 0.50f : 0.46f
                    }
                });
            }
            corps = item;
        }
        else if (id == IdObjetPellePierreTier0)
        {
            SlotInventaire slotPelle = mainActive;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slotPelle);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = mainActive.IndexTaille,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            item.SetMeta(MetaDurabiliteOutilMax, slotPelle.DurabiliteOutilMax);
            item.SetMeta(MetaDurabiliteOutilActuelle, slotPelle.DurabiliteOutilActuelle);
            var meshRoot = new MeshInstance3D { Name = "MeshInstance3D" };
            InstancierModeleArme(meshRoot, slotPelle, 0.64f, 1f);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.12f, 0.52f, 0.22f) },
                    Position = new Vector3(0, 0.24f, 0)
                });
            }
            corps = item;
        }
        else if (id == IdObjetPiochePierreTier0)
        {
            SlotInventaire slotPioche = mainActive;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slotPioche);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = mainActive.IndexTaille,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            item.SetMeta(MetaDurabiliteOutilMax, slotPioche.DurabiliteOutilMax);
            item.SetMeta(MetaDurabiliteOutilActuelle, slotPioche.DurabiliteOutilActuelle);
            if (!string.IsNullOrEmpty(slotPioche.GenomeAssemblage))
            {
                item.GenomeAssemblage = slotPioche.GenomeAssemblage;
                item.SetMeta(MetaGenomeAssemblage, slotPioche.GenomeAssemblage);
            }
            var meshRoot = new MeshInstance3D { Name = "MeshInstance3D" };
            InstancierModeleArme(meshRoot, slotPioche, 0.65f, 1f);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.13f, 0.54f, 0.22f) },
                    Position = new Vector3(0, 0.24f, 0)
                });
            }
            corps = item;
        }
        else if (id == IdObjetLancePierreTier0)
        {
            SlotInventaire slotLance = mainActive;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slotLance);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = mainActive.IndexTaille,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            item.SetMeta(MetaDurabiliteOutilMax, slotLance.DurabiliteOutilMax);
            item.SetMeta(MetaDurabiliteOutilActuelle, slotLance.DurabiliteOutilActuelle);
            var meshRoot = new MeshInstance3D { Name = "MeshInstance3D" };
            InstancierModeleArme(meshRoot, slotLance, 0.66f, 1f);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new CapsuleShape3D { Radius = 0.055f, Height = 0.92f }
                });
            }
            corps = item;
        }
        else if (id == IdObjetFauxPierreTier0)
        {
            SlotInventaire slotFaux = mainActive;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slotFaux);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = mainActive.IndexTaille,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            item.SetMeta(MetaDurabiliteOutilMax, slotFaux.DurabiliteOutilMax);
            item.SetMeta(MetaDurabiliteOutilActuelle, slotFaux.DurabiliteOutilActuelle);
            item.SetMeta(MetaTailleLameRoche, Mathf.Clamp(slotFaux.IndexTailleLameRoche <= 0 ? 2 : slotFaux.IndexTailleLameRoche, 0, 4));
            var meshRoot = new MeshInstance3D { Name = "MeshInstance3D" };
            InstancierModeleArme(meshRoot, slotFaux, 0.63f, ObtenirFacteurEchelleLameDague(slotFaux));
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new CapsuleShape3D { Radius = 0.07f, Height = 0.48f }
                });
            }
            corps = item;
        }
        else if (id == IdObjetRackBatons || id == IdObjetRackBuches)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(mainActive.GenomeAssemblage))
            {
                item.GenomeAssemblage = mainActive.GenomeAssemblage;
                item.SetMeta(MetaGenomeAssemblage, mainActive.GenomeAssemblage);
            }
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            if (id == IdObjetRackBatons) InstancierModeleRackBatons(meshRoot, mainActive, 1.05f, true);
            else InstancierModeleRackBuches(meshRoot, mainActive, 1.05f, true);
            item.AddChild(meshRoot);
            // MÃªme logique que la table (200) : collisions exactes depuis les meshes pour Ã©viter la lÃ©vitation.
            var pileRack = new List<Node> { meshRoot };
            for (int i = 0; i < pileRack.Count; i++)
            {
                foreach (Node c in pileRack[i].GetChildren())
                {
                    if (c is MeshInstance3D mi && mi.Mesh != null)
                    {
                        Shape3D shape = mi.Mesh.CreateTrimeshShape();
                        if (shape != null)
                        {
                            Transform3D t = mi.Transform;
                            Node parentNode = mi.GetParent();
                            while (parentNode != null && parentNode != item && parentNode is Node3D n3d)
                            {
                                t = n3d.Transform * t;
                                parentNode = parentNode.GetParent();
                            }
                            var colNode = new CollisionShape3D { Shape = shape, Transform = t };
                            item.AddChild(colNode);
                        }
                    }
                    pileRack.Add(c);
                }
            }
            // Fallback sÃ©curitÃ© si jamais le GLB ne retourne aucune surface exploitable.
            if (item.GetChildCount() <= 1)
                item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.9f, 0.68f, 0.52f) }, Position = new Vector3(0f, 0.34f, 0f) });
            string cle = !string.IsNullOrEmpty(mainActive.CleConteneur) ? mainActive.CleConteneur : Guid.NewGuid().ToString("N");
            item.SetMeta("CleConteneur", cle);
            corps = item;
        }
        else if (id == IdObjetCoffreBoisTier0)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleCoffreBoisTier0(meshRoot, mainActive, 0.88f, true);
            item.AddChild(meshRoot);
            var pileCoffre = new List<Node> { meshRoot };
            for (int i = 0; i < pileCoffre.Count; i++)
            {
                foreach (Node c in pileCoffre[i].GetChildren())
                {
                    if (c is MeshInstance3D mi && mi.Mesh != null)
                    {
                        Shape3D shape = mi.Mesh.CreateTrimeshShape();
                        if (shape != null)
                        {
                            Transform3D t = mi.Transform;
                            Node parentNode = mi.GetParent();
                            while (parentNode != null && parentNode != item && parentNode is Node3D n3d)
                            {
                                t = n3d.Transform * t;
                                parentNode = parentNode.GetParent();
                            }
                            var colNode = new CollisionShape3D { Shape = shape, Transform = t };
                            item.AddChild(colNode);
                        }
                    }
                    pileCoffre.Add(c);
                }
            }
            if (item.GetChildCount() <= 1)
                item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.52f, 0.38f, 0.42f) }, Position = new Vector3(0f, 0.19f, 0f) });
            string cleCoffre = !string.IsNullOrEmpty(mainActive.CleConteneur) ? mainActive.CleConteneur : Guid.NewGuid().ToString("N");
            item.SetMeta("CleConteneur", cleCoffre);
            RestaurerContenuCoffreSurItem(item, cleCoffre);
            corps = item;
        }
        else if (id == IdObjetPitFeu || id == IdObjetPitFeuRoche)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            if (id == IdObjetPitFeuRoche)
                InstancierModelePitFeuRoche(meshRoot, mainActive, 0.96f, true);
            else
                InstancierModelePitFeu(meshRoot, mainActive, 0.92f, true);
            item.AddChild(meshRoot);
            var pilePit = new List<Node> { meshRoot };
            for (int i = 0; i < pilePit.Count; i++)
            {
                foreach (Node c in pilePit[i].GetChildren())
                {
                    if (c is MeshInstance3D mi && mi.Mesh != null)
                    {
                        Shape3D shape = mi.Mesh.CreateTrimeshShape();
                        if (shape != null)
                        {
                            Transform3D t = mi.Transform;
                            Node parentNode = mi.GetParent();
                            while (parentNode != null && parentNode != item && parentNode is Node3D n3d)
                            {
                                t = n3d.Transform * t;
                                parentNode = parentNode.GetParent();
                            }
                            var colNode = new CollisionShape3D { Shape = shape, Transform = t };
                            item.AddChild(colNode);
                        }
                    }
                    pilePit.Add(c);
                }
            }
            if (item.GetChildCount() <= 1)
            {
                Vector3 tailleBox = id == IdObjetPitFeuRoche
                    ? new Vector3(0.94f, 0.34f, 0.94f)
                    : new Vector3(0.86f, 0.32f, 0.86f);
                item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = tailleBox }, Position = new Vector3(0f, 0.16f, 0f) });
            }
            corps = item;
        }
        else if (id == IdObjetFourTorchie)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleFourTorchie(meshRoot, mainActive, TailleFourTorchiePoseMetres, true);
            item.AddChild(meshRoot);
            var pileFour = new List<Node> { meshRoot };
            for (int i = 0; i < pileFour.Count; i++)
            {
                foreach (Node c in pileFour[i].GetChildren())
                {
                    if (c is MeshInstance3D mi && mi.Mesh != null)
                    {
                        Shape3D shape = mi.Mesh.CreateTrimeshShape();
                        if (shape != null)
                        {
                            Transform3D t = mi.Transform;
                            Node parentNode = mi.GetParent();
                            while (parentNode != null && parentNode != item && parentNode is Node3D n3d)
                            {
                                t = n3d.Transform * t;
                                parentNode = parentNode.GetParent();
                            }
                            var colNode = new CollisionShape3D { Shape = shape, Transform = t };
                            item.AddChild(colNode);
                        }
                    }
                    pileFour.Add(c);
                }
            }
            if (item.GetChildCount() <= 1)
                item.AddChild(new CollisionShape3D
                {
                    Shape = new BoxShape3D
                    {
                        Size = new Vector3(TailleFourTorchiePoseMetres * 0.92f, TailleFourTorchiePoseMetres * 0.52f, TailleFourTorchiePoseMetres * 0.92f)
                    },
                    Position = new Vector3(0f, TailleFourTorchiePoseMetres * 0.26f, 0f)
                });
            corps = item;
        }
        else if (id == IdObjetMailletBois)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleMailletBois(meshRoot, mainActive, 0.70f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new CapsuleShape3D { Radius = 0.06f, Height = 0.36f }
                });
            }
            corps = item;
        }
        else if (id == IdObjetBolBois)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleBolBois(meshRoot, mainActive, 0.62f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.22f, 0.10f, 0.22f) },
                    Position = new Vector3(0f, 0.05f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetBolEau)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleBolEau(meshRoot, mainActive, _gestionnaireMonde?.MaterielEau, 0.62f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.22f, 0.10f, 0.22f) },
                    Position = new Vector3(0f, 0.05f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetArgileHumidifiee)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleArgileHumidifiee(meshRoot, mainActive, 0.58f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new SphereShape3D { Radius = 0.12f },
                    Position = new Vector3(0f, 0.06f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetChamotte)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleChamotte(meshRoot, mainActive, 0.36f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new SphereShape3D { Radius = 0.11f },
                    Position = new Vector3(0f, 0.05f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetBolArgile)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleBolArgile(meshRoot, mainActive, 0.42f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.18f, 0.08f, 0.18f) },
                    Position = new Vector3(0f, 0.04f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetBolCeramique)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true,
                GenomeAssemblage = mainActive.GenomeAssemblage
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleBolCeramique(meshRoot, mainActive, 0.42f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.18f, 0.08f, 0.18f) },
                    Position = new Vector3(0f, 0.04f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetMouleArgile)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleMouleArgile(meshRoot, mainActive, 0.44f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.22f, 0.10f, 0.32f) },
                    Position = new Vector3(0f, 0.05f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetMouleCeramique)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true,
                GenomeAssemblage = mainActive.GenomeAssemblage
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleMouleCeramique(meshRoot, mainActive, 0.44f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.22f, 0.10f, 0.32f) },
                    Position = new Vector3(0f, 0.05f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetPinceOs)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModelePinceOs(meshRoot, mainActive, 0.64f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.14f, 0.04f, 0.22f) },
                    Position = new Vector3(0f, 0.02f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetTorchie)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleTorchie(meshRoot, mainActive, 0.56f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.20f, 0.08f, 0.12f) },
                    Position = new Vector3(0f, 0.04f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetMortierPilonBois)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleMortierPilonBois(meshRoot, mainActive, 0.72f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.34f, 0.24f, 0.34f) },
                    Position = new Vector3(0f, 0.12f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetAtelleJambe)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleAtelleJambe(meshRoot, mainActive, 0.66f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.32f, 0.12f, 0.16f) },
                    Position = new Vector3(0f, 0.06f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetAtelleBras)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleAtelleBras(meshRoot, mainActive, 0.66f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.32f, 0.12f, 0.16f) },
                    Position = new Vector3(0f, 0.06f, 0f)
                });
            }
            corps = item;
        }
        else if (id == IdObjetBandageTier1)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleBandageTier1(meshRoot, mainActive, 0.28f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.14f, 0.06f, 0.10f) },
                    Position = new Vector3(0f, 0.03f, 0f)
                });
            }
            corps = item;
        }
        else if (EstIdFondation(id))
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleFondation(meshRoot, mainActive, 4.0f, true);
            item.AddChild(meshRoot);
            AjouterCollisionPlateauFondation(item, meshRoot);
            corps = item;
        }
        else if (EstIdSolBois(id))
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleSolBois(meshRoot, mainActive, true);
            item.AddChild(meshRoot);
            AjouterCollisionPlancherSolBois(item, meshRoot);
            corps = item;
        }
        else if (EstIdSolRoche(id))
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleSolRoche(meshRoot, mainActive, true);
            item.AddChild(meshRoot);
            AjouterCollisionPlancherSolBois(item, meshRoot);
            corps = item;
        }
        else if (EstIdMuret(id))
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleMuretBois(meshRoot, mainActive, true);
            item.AddChild(meshRoot);
            AjouterCollisionMuretBois(item, meshRoot);
            corps = item;
        }
        else if (EstIdMurBois(id))
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            if (EstIdMurBoisFenetre(id))
                InstancierModeleMurBoisFenetre(meshRoot, mainActive, true);
            else if (EstIdMurBoisCadrePorte(id))
                InstancierModeleMurBoisCadrePorte(meshRoot, mainActive, true);
            else
                InstancierModeleMurBois(meshRoot, mainActive, true);
            item.AddChild(meshRoot);
            if (EstIdMurBoisCadrePorte(id))
                AjouterCollisionMurBoisCadrePorte(item, meshRoot);
            else
                AjouterCollisionMurBois(item, meshRoot);
            corps = item;
        }
        else if (EstIdPorteBois(id))
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModelePorteBois(meshRoot, mainActive, true);
            item.AddChild(meshRoot);
            AjouterCollisionPorteBois(item, meshRoot);
            corps = item;
        }
        else if (EstIdToitChaume(id))
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleToitChaume(meshRoot, mainActive, ToitChaumeVarianteVisuelle.Solo, true);
            item.AddChild(meshRoot);
            AjouterCollisionToitChaume(item, meshRoot);
            corps = item;
        }
        else if (id == IdObjetFenetreBois)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleFenetreBois(meshRoot, mainActive, 0.92f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.78f, 0.92f, 0.12f) },
                    Position = new Vector3(0f, 0.46f, 0f)
                });
            }
            corps = item;
        }
        else if (EstIdTorche(id))
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleTorche(meshRoot, mainActive, true);
            item.AddChild(meshRoot);
            AjouterCollisionTorche(item, meshRoot);
            corps = item;
        }
        else if (id == 200)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            // FIX CRITIQUE : point zÃ©ro aux pieds du meuble (ancrerBaseAuSol = true), ~1,2 m sur la plus grande dimension.
            InstancierModeleAtelierPrimitif(meshRoot, mainActive, 1.2f, true);
            item.AddChild(meshRoot);

            var pile = new List<Node> { meshRoot };
            for (int i = 0; i < pile.Count; i++)
            {
                foreach (Node c in pile[i].GetChildren())
                {
                    if (c is MeshInstance3D mi && mi.Mesh != null)
                    {
                        Shape3D shape = mi.Mesh.CreateTrimeshShape();
                        if (shape != null)
                        {
                            Transform3D t = mi.Transform;
                            Node parentNode = mi.GetParent();
                            while (parentNode != null && parentNode != item && parentNode is Node3D n3d)
                            {
                                t = n3d.Transform * t;
                                parentNode = parentNode.GetParent();
                            }
                            var colNode = new CollisionShape3D { Shape = shape, Transform = t };
                            item.AddChild(colNode);
                        }
                    }
                    pile.Add(c);
                }
            }
            corps = item;
        }
        else if (id == IdObjetTableBoisDecorative)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleTableBoisDecorative(meshRoot, mainActive, 1.2f, true);
            item.AddChild(meshRoot);

            var pile = new List<Node> { meshRoot };
            for (int i = 0; i < pile.Count; i++)
            {
                foreach (Node c in pile[i].GetChildren())
                {
                    if (c is MeshInstance3D mi && mi.Mesh != null)
                    {
                        Shape3D shape = mi.Mesh.CreateTrimeshShape();
                        if (shape != null)
                        {
                            Transform3D t = mi.Transform;
                            Node parentNode = mi.GetParent();
                            while (parentNode != null && parentNode != item && parentNode is Node3D n3d)
                            {
                                t = n3d.Transform * t;
                                parentNode = parentNode.GetParent();
                            }
                            var colNode = new CollisionShape3D { Shape = shape, Transform = t };
                            item.AddChild(colNode);
                        }
                    }
                    pile.Add(c);
                }
            }
            corps = item;
        }
        else if (id == IdObjetTableArtisanaTier1)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? "",
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleTableArtisanaTier1(meshRoot, mainActive, 1.35f, true);
            item.AddChild(meshRoot);

            var pile = new List<Node> { meshRoot };
            for (int i = 0; i < pile.Count; i++)
            {
                foreach (Node c in pile[i].GetChildren())
                {
                    if (c is MeshInstance3D mi && mi.Mesh != null)
                    {
                        Shape3D shape = mi.Mesh.CreateTrimeshShape();
                        if (shape != null)
                        {
                            Transform3D t = mi.Transform;
                            Node parentNode = mi.GetParent();
                            while (parentNode != null && parentNode != item && parentNode is Node3D n3d)
                            {
                                t = n3d.Transform * t;
                                parentNode = parentNode.GetParent();
                            }
                            var colNode = new CollisionShape3D { Shape = shape, Transform = t };
                            item.AddChild(colNode);
                        }
                    }
                    pile.Add(c);
                }
            }
            corps = item;
        }
        else if (id == IdObjetTableAnalyseTier1)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleTableAnalyseTier1(meshRoot, mainActive, 1.53f, true);
            item.AddChild(meshRoot);

            var pile = new List<Node> { meshRoot };
            for (int i = 0; i < pile.Count; i++)
            {
                foreach (Node c in pile[i].GetChildren())
                {
                    if (c is MeshInstance3D mi && mi.Mesh != null)
                    {
                        Shape3D shape = mi.Mesh.CreateTrimeshShape();
                        if (shape != null)
                        {
                            Transform3D t = mi.Transform;
                            Node parentNode = mi.GetParent();
                            while (parentNode != null && parentNode != item && parentNode is Node3D n3d)
                            {
                                t = n3d.Transform * t;
                                parentNode = parentNode.GetParent();
                            }
                            var colNode = new CollisionShape3D { Shape = shape, Transform = t };
                            item.AddChild(colNode);
                        }
                    }
                    pile.Add(c);
                }
            }
            corps = item;
        }
        else if (ItemPhysique.EstIdRocheMatiere(id))
        {
            float tailleBase = mainActive.IndexTaille switch { 0 => 0.08f, 1 => 0.15f, 2 => 0.25f, 3 => 0.40f, 4 => 0.65f, _ => 0.2f };

            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = ItemPhysique.IndexChimiqueDepuisIdRoche(id),
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = Mathf.Clamp(mainActive.IndexTaille, 0, 4),
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique"
            };

            Vector3 scaleForme = Vector3.One;
            if (mainActive.IndexMorphologique == 1) scaleForme = new Vector3(1f, 0.4f, 1f);
            else if (mainActive.IndexMorphologique == 2) scaleForme = new Vector3(1f, 0.7f, 1.4f);
            else if (mainActive.IndexMorphologique == 3) scaleForme = new Vector3(0.6f, 1.3f, 0.6f);

            var sphereMesh = new SphereMesh { Radius = tailleBase, Height = tailleBase * 2f };
            Mesh finalMesh = sphereMesh;
            Shape3D colShape;

            if (mainActive.IndexMorphologique == 0)
            {
                colShape = new SphereShape3D { Radius = tailleBase };
            }
            else
            {
                Godot.Collections.Array arrays = sphereMesh.GetMeshArrays();
                Vector3[] vertices = ((Variant)arrays[(int)Mesh.ArrayType.Vertex]).AsVector3Array();
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = new Vector3(vertices[i].X * scaleForme.X, vertices[i].Y * scaleForme.Y, vertices[i].Z * scaleForme.Z);
                }
                arrays[(int)Mesh.ArrayType.Vertex] = vertices;
                var bakedMesh = new ArrayMesh();
                bakedMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                finalMesh = bakedMesh;
                colShape = bakedMesh.CreateConvexShape(true, true);
            }

            var meshNode = new MeshInstance3D { Name = "MeshInstance3D", Mesh = finalMesh };
            AppliquerMaterielObjet(meshNode, id, ItemPhysique.IndexChimiqueDepuisIdRoche(id), 0, 0);

            item.AddChild(meshNode);
            item.AddChild(new CollisionShape3D { Name = "CollisionShape3D", Shape = colShape });
            item.SetMeta(ItemPhysique.MetaRocheForgeeParJoueur, true);
            corps = item;
        }
        else if (id == 15 || id == 16 || id == 17) // Fibres flexibles : fagot de brins (teinte selon profil)
        {
            Color teinte = Atlas_Matiere.ObtenirProfilFlexible(id, out var profilF)
                ? profilF.CouleurCorde
                : new Color(0.35f, 0.55f, 0.15f);
            var item = new ItemPhysique { ID_Objet = id, Name = "ItemPhysique" };
            var matFibre = new StandardMaterial3D { AlbedoColor = teinte, Roughness = 0.9f, Metallic = 0f };
            float l = id == 17 ? 0.42f : 0.38f;
            for (int i = 0; i < 6; i++)
            {
                float a = (i / 6f) * Mathf.Pi * 0.6f - 0.15f;
                float x = Mathf.Sin(a) * 0.025f; float z = Mathf.Cos(a) * 0.025f;
                var mi = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.01f, Height = l - 0.02f }, MaterialOverride = matFibre, Position = new Vector3(x, l * 0.5f, z), Rotation = new Vector3(0.08f * (i - 3), 0.1f * (i % 2 - 0.5f), 0.06f * (i - 2)) };
                item.AddChild(mi);
            }
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.12f, l, 0.12f) }, Position = new Vector3(0, l * 0.5f, 0) });
            corps = item;
        }
        else if (id == 20) // Tressage / corde tier 0 : modÃ¨le GLB + mÃªmes matÃ©riaux procÃ©duraux que lâ€™inventaire.
        {
            int idA = mainActive.IndexChimique, idB = mainActive.IndexMorphologique;
            var item = new ItemPhysique { ID_Objet = id, IndexChimique = idA, IndexCacheMemoire = idB, NiveauFracture = mainActive.NiveauFracture, Name = "ItemPhysique" };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleCordeTier0Gazon(meshRoot, mainActive, 0.32f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.045f, Height = 0.28f } });
            corps = item;
        }
        else if (id == 21) // Tissu tier 0 : 4 cordes tissÃ©es â€” GLB + mÃªme matiÃ¨re plate que la corde.
        {
            int idA = mainActive.IndexChimique, idB = mainActive.IndexMorphologique;
            var item = new ItemPhysique { ID_Objet = id, IndexChimique = idA, IndexCacheMemoire = idB, NiveauFracture = mainActive.NiveauFracture, Name = "ItemPhysique" };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleTissuTier0(meshRoot, mainActive, 0.34f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.32f, 0.06f, 0.32f) } });
            corps = item;
        }
        else if (id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches) // 102 = ceinture seule ; 104 = GLB avec poches + stockage persistant.
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique"
            };
            if (!string.IsNullOrEmpty(mainActive.CleConteneur))
                item.SetMeta("CleConteneur", mainActive.CleConteneur);
            if (!string.IsNullOrEmpty(mainActive.GenomeAssemblage))
            {
                item.GenomeAssemblage = mainActive.GenomeAssemblage;
                item.SetMeta(MetaGenomeAssemblage, mainActive.GenomeAssemblage);
            }
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            if (id == IdObjetCeintureSacoches)
                InstancierModeleCeintureSacoches(meshRoot, mainActive, 0.42f);
            else
                InstancierModeleCeinturePoches(meshRoot, mainActive, 0.4f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = id == IdObjetCeintureSacoches ? new Vector3(0.52f, 0.12f, 0.32f) : new Vector3(0.42f, 0.09f, 0.28f) } });
            corps = item;
        }
        else if (id == IdObjetPochetteTier0) // Pochette tier 0 : tissu + corde, mÃªme matiÃ¨re procÃ©durale que ceinture.
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique"
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModelePochetteTier0(meshRoot, mainActive, 0.36f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.24f, 0.08f, 0.2f) } });
            corps = item;
        }
        else if (id == IdObjetSacTier0) // Sac tier 0 : modÃ¨le dÃ©diÃ© + matiÃ¨re corde/tissu.
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique"
            };
            if (!string.IsNullOrEmpty(mainActive.CleConteneur))
                item.SetMeta("CleConteneur", mainActive.CleConteneur);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleSacTier0(meshRoot, mainActive, 0.4f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.36f, 0.14f, 0.28f) } });
            corps = item;
        }
        else if (id == IdObjetCarnetSavoir)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleCarnetSavoir(meshRoot, mainActive, 0.48f, false);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.30f, 0.055f, 0.42f) } });
            corps = item;
        }
        else if (id == IdObjetAllumeFeu)
        {
            SlotInventaire slotAllumeFeu = mainActive;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slotAllumeFeu);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = slotAllumeFeu.IndexChimique,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            item.SetMeta(MetaDurabiliteOutilMax, slotAllumeFeu.DurabiliteOutilMax);
            item.SetMeta(MetaDurabiliteOutilActuelle, slotAllumeFeu.DurabiliteOutilActuelle);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleAllumeFeu(meshRoot, slotAllumeFeu, 0.42f, false);
            item.AddChild(meshRoot);
            if (AjouterCollisionsConvexesDepuisMeshesSousRacineItem(item, meshRoot) == 0)
            {
                item.AddChild(new CollisionShape3D
                {
                    Name = "CollisionShape3D",
                    Shape = new BoxShape3D { Size = new Vector3(0.18f, 0.05f, 0.08f) },
                    Position = new Vector3(0f, 0.025f, 0f)
                });
            }
            corps = item;
        }
        else if (id == 30 || id == 32)
        {
            int f = Mathf.Clamp(mainActive.IndexMorphologique, 0, 3);
            CalculerDimensionsBoisPose(id, mainActive.IndexMorphologique, mainActive.IndexTaille, out float br, out float baseLengthCalc, out float w, out float hh);
            float bl = baseLengthCalc;
            if (mainActive.ScaleEclat.Z > 0.1f)
                bl = baseLengthCalc * mainActive.ScaleEclat.Z;
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = Mathf.Clamp(mainActive.IndexTaille, 0, 4),
                IndexChimique = mainActive.IndexChimique,
                Name = "ItemPhysique",
                ContinuousCd = true,
                NiveauFracture = 0
            };
            item.SetMeta("ScaleLongueurBois", bl / Mathf.Max(0.001f, baseLengthCalc));
            Mesh meshObj = GenererMeshBoisFendu(br, bl, mainActive.IndexMorphologique);
            Shape3D colObj;
            if (f == 0)
            {
                colObj = new CylinderShape3D { Radius = br, Height = bl };
            }
            else
            {
                // FIX CRITIQUE : Une BoÃ®te statique est beaucoup plus stable qu'un ConvexShape pour Jolt.
                // Elle englobe le morceau coupÃ© et empÃªche le passage Ã  travers la terre.
                float wCol = br * 2f; float hCol = br;
                if (f == 2) { wCol = br; hCol = br; }
                else if (f >= 3) { wCol = br; hCol = br * 0.4f; }
                colObj = new BoxShape3D { Size = new Vector3(wCol, hCol, bl) };
            }
            var meshNode = new MeshInstance3D
            {
                Mesh = meshObj,
                MaterialOverride = id == 32 && mainActive.IndexChimique == 1 && mainActive.IndexBotanique == LSystem_Botanique.IndexChene
                    ? ArbreVivant.ObtenirMaterielBoisTriplanarBatonChenEPale()
                    : ArbreVivant.ObtenirMaterielBoisTriplanar(mainActive.IndexBotanique)
            };
            meshNode.RotationDegrees = new Vector3(90f, 0f, 0f);
            var colNode = new CollisionShape3D { Shape = colObj };
            colNode.RotationDegrees = new Vector3(90f, 0f, 0f);
            item.AddChild(meshNode);
            item.AddChild(colNode);
            corps = item;
        }
        else if (id == 34) // Feuilles arrachées
        {
            byte essenceFeuille = mainActive.IndexBotanique;
            if (BlocChutant.EssenceUtiliseFeuilleGlb(essenceFeuille))
            {
                var item = new ItemPhysique
                {
                    ID_Objet = id,
                    IndexBotanique = essenceFeuille,
                    IndexCacheMemoire = 0,
                    Name = "ItemPhysique",
                    ContinuousCd = true
                };
                var meshRoot = new Node3D { Name = "MeshInstance3D" };
                InstancierModeleFeuilleArrachee(meshRoot, mainActive, 0.22f);
                item.AddChild(meshRoot);
                item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.14f, 0.03f, 0.14f) } });
                corps = item;
            }
            else
            {
                var matFeuilles = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.55f, 0.15f), Roughness = 0.95f, Metallic = 0f };
                corps = BlocChutant.CreerFeuillageArrache(pointDeChute, matFeuilles, null, essenceFeuille);
            }
        }
        else if (id == IdObjetAloeVera)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            Mesh meshAloe = ObtenirMeshDepuisCache(id, 0, 0);
            var meshNode = new MeshInstance3D { Name = "MeshInstance3D", Mesh = meshAloe };
            AppliquerMaterielObjet(meshNode, id, mainActive.IndexChimique, 0, 0, mainActive.IndexBotanique);
            item.AddChild(meshNode);
            Shape3D shapeAloe = meshAloe?.CreateConvexShape(true, true);
            if (shapeAloe == null)
                shapeAloe = new BoxShape3D { Size = new Vector3(0.18f, 0.28f, 0.18f) };
            item.AddChild(new CollisionShape3D { Shape = shapeAloe });
            corps = item;
        }
        else if (id == 10 || id == 11 || id == BlocChutant.ID_BRANCHE)
        {
            if (id == BlocChutant.ID_BRANCHE)
            {
                Material matEssence = ArbreVivant.ObtenirMaterielBoisTriplanar(mainActive.IndexBotanique);
                bool tailléeBuisson = mainActive.IndexMorphologique == 1;
                corps = BlocChutant.Creer(pointDeChute, (byte)id, matEssence, tailléeBuisson);
                corps.SetMeta("IndexBotanique", (int)mainActive.IndexBotanique);
            }
            else
            {
                var mat = new StandardMaterial3D { AlbedoColor = new Color(0.38f, 0.46f, 0.2f), Roughness = 0.92f, Metallic = 0f };
                corps = BlocChutant.Creer(pointDeChute, (byte)id, mat);
            }
        }
        else if (id == IdObjetBaie)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleBaie(meshRoot, mainActive, 0.22f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.08f } });
            corps = item;
        }
        else if (id == IdObjetSteakCru)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = 0,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleSteakCru(meshRoot, mainActive, 0.2f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.07f } });
            corps = item;
        }
        else if (EstIdCharbonRecolte(id))
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = 0,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleCharbon(meshRoot, mainActive, 0.22f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.16f, 0.12f, 0.14f) } });
            corps = item;
        }
        else if (EstIdQuartzRecolte(id))
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = 0,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleQuartz(meshRoot, mainActive, 0.22f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.14f, 0.14f, 0.14f) } });
            corps = item;
        }
        else if (EstIdEtainRecolte(id))
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = 0,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleEtain(meshRoot, mainActive, 0.22f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.15f, 0.13f, 0.14f) } });
            corps = item;
        }
        else if (id == IdObjetSteakCuit)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = 0,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleSteakCuit(meshRoot, mainActive, 0.2f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.07f } });
            corps = item;
        }
        else if (id == IdObjetOsBoeuf)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = 0,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleOsBoeuf(meshRoot, mainActive, 0.308f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.056f, Height = 0.308f } });
            corps = item;
        }
        else if (id == IdObjetCuirBoeuf)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = 0,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true,
                GenomeAssemblage = mainActive.GenomeAssemblage ?? ""
            };
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleCuirBoeuf(meshRoot, mainActive, 0.288f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.336f, 0.072f, 0.264f) } });
            corps = item;
        }
        else if (id == IdObjetIntestinBoeuf)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = 0,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleIntestinBoeuf(meshRoot, mainActive, 0.24f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.06f, Height = 0.24f } });
            corps = item;
        }
        else if (id == IdObjetIntestinBoeufNettoye)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = 0,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleIntestinBoeufNettoye(meshRoot, mainActive, 0.24f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.06f, Height = 0.24f } });
            corps = item;
        }
        else // 999 Buisson â€” RigidBody3D pour pouvoir le lancer comme les autres objets posÃ©s.
        {
            float cote = 0.85f;
            var rb = new RigidBody3D { Mass = cote * cote * cote * 190f, ContinuousCd = true };
            rb.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(cote, cote, cote) } });
            rb.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(cote, cote, cote) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.8f, 0.2f) } });
            corps = rb;
        }
        corps.SetMeta("ID_Matiere", id);
        if (corps is ItemPhysique ipQuantite && mainActive.Quantite > 1)
            ipQuantite.SetMeta(MetaQuantiteObjetPose, mainActive.Quantite);
        bool enChargementPersistant = _chargementObjetsPosesMondeEnCours;
        bool estFondationPose = EstIdFondation(id);
        bool estPlancherPose = EstIdPlancher(id);
        if (!modeGhost)
        {
            corps.AddToGroup("BlocsPoses");
            Node parentPose = GetParent();
            Gestionnaire_Monde gmPose = ObtenirGestionnaireMondePersistant();
            if (gmPose != null)
            {
                Node3D racineDim = gmPose.ObtenirRacineDimension(gmPose.ObtenirDimensionLocaleActiveId());
                if (racineDim != null)
                    parentPose = racineDim;
            }
            if (parentPose == null || !GodotObject.IsInstanceValid(parentPose))
            {
                corps.QueueFree();
                return null;
            }
            parentPose.AddChild(corps);
            // Placement pur : pas de translation Y supplÃ©mentaire (Ã©vite double offset / lÃ©vitation atelier).
            corps.GlobalPosition = pointDeChute;
        }
        bool estMuretPose = EstIdMuret(id);
        bool estMurPose = EstIdMurBois(id);
        bool estPortePose = EstIdPorteBois(id);
        bool estToitPose = EstIdToitChaume(id);
        if ((estFondationPose || estPlancherPose || estMuretPose || estMurPose || estPortePose || estToitPose) && !enChargementPersistant && !modeGhost)
            AjouterXpMetier("Batisseur", 1UL);
        bool fondationSurSupportEleve = estFondationPose && !enChargementPersistant && !modeGhost
            && (FondationReposantSurFondationOuStructure(corps.GlobalPosition) || _offsetEtagesFondationManuel != 0);
        if (!modeGhost && !EstIdPorteBois(id) && !EstIdToitChaume(id) && (id == IdObjetTableBoisDecorative || id == IdObjetTableArtisanaTier1 || id == IdObjetTableAnalyseTier1 || id == IdObjetRackBatons || id == IdObjetRackBuches || id == IdObjetCoffreBoisTier0 || EstIdPitFeu(id) || EstIdFourTorchie(id) || EstIdFondation(id) || EstIdMuret(id) || EstIdMurBois(id)))
        {
            // Snap sol robuste pour le rack: corrige les cas oÃ¹ le raycast vise une surface dÃ©calÃ©e.
            var espace = GetWorld3D()?.DirectSpaceState;
            if (espace != null && !enChargementPersistant && !(estFondationPose && fondationSurSupportEleve))
            {
                Vector3 origine = corps.GlobalPosition + Vector3.Up * 4f;
                Vector3 dest = corps.GlobalPosition + Vector3.Down * 8f;
                var q = PhysicsRayQueryParameters3D.Create(origine, dest);
                var excludes = new Godot.Collections.Array<Rid>();
                if (corps is CollisionObject3D coRack)
                    excludes.Add(coRack.GetRid());
                q.Exclude = excludes;
                q.CollideWithAreas = false;

                bool EstImpactToitChaume(Godot.Collections.Dictionary hitRay)
                {
                    if (hitRay == null || !hitRay.ContainsKey("collider"))
                        return false;
                    Node n = NoeudDepuisColliderRaycast(hitRay["collider"].AsGodotObject());
                    for (Node cur = n; cur != null; cur = cur.GetParent())
                    {
                        if (cur is ItemPhysique ip && ip.IsInGroup("BlocsPoses"))
                            return EstIdToitChaume(ip.ID_Objet);
                    }
                    return false;
                }

                Godot.Collections.Dictionary hit = null;
                const int maxEssaisSnap = 6;
                for (int essai = 0; essai < maxEssaisSnap; essai++)
                {
                    hit = espace.IntersectRay(q);
                    if (hit.Count == 0 || !hit.ContainsKey("position"))
                        break;
                    if (!EstImpactToitChaume(hit))
                        break;
                    if (hit.ContainsKey("rid"))
                        excludes.Add((Rid)hit["rid"]);
                    q.Exclude = excludes;
                    hit = null;
                }

                if (hit != null && hit.Count > 0 && hit.ContainsKey("position"))
                {
                    Aabb? box = null;
                    AccumulerAabbMeshes(corps, Transform3D.Identity, ref box);
                    if (box.HasValue)
                    {
                        float minY = box.Value.Position.Y;
                        float hitY = ((Vector3)hit["position"]).Y;
                        corps.GlobalPosition += Vector3.Up * (hitY - minY + 0.005f);
                        if (EstIdFondation(id))
                            corps.GlobalPosition += Vector3.Down * 0.02f;
                    }
                }
            }
        }
        // MÃªme calque que le terrain PhysicsServer3D / StaticBody (bit 1) : collision fiable au sol.
        if (corps is RigidBody3D rbPose)
        {
            rbPose.CollisionLayer = 1;
            rbPose.CollisionMask = 1;
            rbPose.ContinuousCd = true;

            if (ItemPhysique.EstIdRocheMatiere(id))
            {
                int morphR = Mathf.Clamp(mainActive.IndexMorphologique, 0, 3);
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                if (morphR == 0)
                {
                    rbPose.PhysicsMaterialOverride = _physMatRocheRonde;
                    rbPose.LinearDamp = 0.04f;
                    rbPose.AngularDamp = 0.04f;
                }
                else if (morphR == 1)
                {
                    rbPose.PhysicsMaterialOverride = _physMatRochePlate;
                    rbPose.LinearDamp = 0.38f;
                    rbPose.AngularDamp = 1.55f;
                }
                else if (morphR == 2)
                {
                    rbPose.PhysicsMaterialOverride = _physMatRocheOvale;
                    rbPose.LinearDamp = 0.11f;
                    rbPose.AngularDamp = 0.3f;
                }
                else
                {
                    rbPose.PhysicsMaterialOverride = _physMatRochePointe;
                    rbPose.LinearDamp = 0.2f;
                    rbPose.AngularDamp = 0.88f;
                }
            }
            else if (id == 30 || id == 32 || id == 200 || id == IdObjetTableBoisDecorative || id == IdObjetTableArtisanaTier1 || id == IdObjetTableAnalyseTier1 || id == IdObjetRackBatons || id == IdObjetRackBuches || id == IdObjetCoffreBoisTier0 || EstIdPitFeu(id) || EstIdFourTorchie(id) || EstIdFondation(id) || EstIdPlancher(id) || EstIdMuret(id) || EstIdMurBois(id) || EstIdPorteBois(id) || EstIdToitChaume(id) || EstIdTorche(id) || id == IdObjetFenetreBois)
            {
                rbPose.PhysicsMaterialOverride = _physMatBois;
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                rbPose.LinearDamp = 0.06f;
                rbPose.AngularDamp = 0.42f;
                if (id == 200)
                {
                    // TrÃ¨s lourd + pas de gravitÃ© : Ã©vite tout glissement / dÃ©rive si le moteur rÃ©veille le corps un instant.
                    rbPose.Mass = 2800f;
                    rbPose.GravityScale = 0f;
                    rbPose.Sleeping = true;
                }
                else if (id == IdObjetTableBoisDecorative)
                {
                    rbPose.Mass = 1800f;
                    rbPose.GravityScale = 0f;
                    rbPose.Sleeping = true;
                }
                else if (id == IdObjetTableArtisanaTier1)
                {
                    rbPose.Mass = 2200f;
                    rbPose.GravityScale = 0f;
                    rbPose.Sleeping = true;
                }
                else if (id == IdObjetTableAnalyseTier1)
                {
                    rbPose.Mass = 2400f;
                    rbPose.GravityScale = 0f;
                    rbPose.Sleeping = true;
                }
                else if (id == IdObjetRackBatons || id == IdObjetRackBuches)
                {
                    rbPose.Mass = 1200f;
                    rbPose.GravityScale = 0f;
                    rbPose.Sleeping = true;
                }
                else if (id == IdObjetCoffreBoisTier0)
                {
                    rbPose.Mass = 42f;
                    rbPose.GravityScale = 0f;
                    rbPose.Sleeping = true;
                }
                else if (EstIdPitFeu(id))
                {
                    rbPose.Mass = id == IdObjetPitFeuRoche ? 34f : 26f;
                    rbPose.GravityScale = 0f;
                    rbPose.Sleeping = true;
                }
                else if (EstIdFourTorchie(id))
                {
                    rbPose.Mass = 28f;
                    rbPose.GravityScale = 0f;
                    rbPose.Sleeping = true;
                }
                else if (EstIdFondation(id))
                {
                    rbPose.Mass = ObtenirMasseSlotInventaireKg(new SlotInventaire { ID = id });
                    rbPose.GravityScale = 0f;
                    rbPose.LockRotation = true;
                    rbPose.Sleeping = true;
                }
                else if (EstIdPlancher(id))
                {
                    rbPose.Mass = ObtenirMasseSlotInventaireKg(new SlotInventaire { ID = id });
                    rbPose.GravityScale = 0f;
                    rbPose.LockRotation = true;
                    rbPose.Sleeping = true;
                }
                else if (EstIdMuret(id))
                {
                    rbPose.Mass = ObtenirMasseSlotInventaireKg(new SlotInventaire { ID = id });
                    rbPose.GravityScale = 0f;
                    rbPose.LockRotation = true;
                    rbPose.Sleeping = true;
                }
                else if (EstIdMurBois(id))
                {
                    rbPose.Mass = ObtenirMasseSlotInventaireKg(new SlotInventaire { ID = id });
                    rbPose.GravityScale = 0f;
                    rbPose.LockRotation = true;
                    rbPose.Sleeping = true;
                }
                else if (EstIdPorteBois(id))
                {
                    rbPose.Mass = ObtenirMasseSlotInventaireKg(new SlotInventaire { ID = id });
                    rbPose.GravityScale = 0f;
                    rbPose.LockRotation = true;
                    rbPose.Sleeping = true;
                }
                else if (EstIdToitChaume(id))
                {
                    rbPose.Mass = ObtenirMasseSlotInventaireKg(new SlotInventaire { ID = id });
                    rbPose.GravityScale = 0f;
                    rbPose.LockRotation = true;
                    rbPose.Sleeping = true;
                }
                else if (EstIdTorche(id))
                {
                    rbPose.Mass = ObtenirMasseSlotInventaireKg(new SlotInventaire { ID = id });
                    rbPose.GravityScale = 0f;
                    rbPose.LockRotation = true;
                    rbPose.Sleeping = true;
                }
            }
            else if (id == IdObjetAllumeFeu)
            {
                rbPose.PhysicsMaterialOverride = _physMatRochePlate;
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                rbPose.LinearDamp = 0.22f;
                rbPose.AngularDamp = 0.72f;
                rbPose.Mass = 0.26f;
            }
            else if (id is >= 15 and <= 17)
            {
                rbPose.PhysicsMaterialOverride = _physMatFibre;
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                rbPose.LinearDamp = 0.42f;
                rbPose.AngularDamp = 1.0f;
            }
            else if (id == 20 || id == 21 || id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches || id == IdObjetPochetteTier0 || id == IdObjetSacTier0 || id == IdObjetCarnetSavoir
                || id == IdObjetSteakCru || id == IdObjetSteakCuit || id == IdObjetOsBoeuf || id == IdObjetCuirBoeuf || id == IdObjetIntestinBoeuf || id == IdObjetIntestinBoeufNettoye
                || EstIdCharbonRecolte(id) || EstIdQuartzRecolte(id) || EstIdEtainRecolte(id))
            {
                rbPose.PhysicsMaterialOverride = _physMatCorde;
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                rbPose.LinearDamp = 0.32f;
                rbPose.AngularDamp = 0.95f;
                if (EstIdQuartzRecolte(id))
                    rbPose.Mass = id == IdObjetQuartzPur ? 0.13f : 0.11f;
                else if (EstIdEtainRecolte(id))
                    rbPose.Mass = 0.12f;
                else if (EstIdCharbonRecolte(id))
                    rbPose.Mass = id == IdObjetCharbonAntracite ? 0.14f : 0.11f;
                else if (id == IdObjetSteakCru || id == IdObjetSteakCuit || id == IdObjetOsBoeuf || id == IdObjetCuirBoeuf || id == IdObjetIntestinBoeuf || id == IdObjetIntestinBoeufNettoye)
                {
                    rbPose.Mass = id == IdObjetOsBoeuf ? 0.55f : (id == IdObjetCuirBoeuf ? 0.25f : ((id == IdObjetIntestinBoeuf || id == IdObjetIntestinBoeufNettoye) ? 0.20f : 0.18f));
                    rbPose.AngularDamp = 1.35f;
                }
            }
            else if (id == 999)
            {
                rbPose.PhysicsMaterialOverride = _physMatVegetalLache;
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                rbPose.LinearDamp = 0.28f;
                rbPose.AngularDamp = 0.75f;
            }
            else if (mainActive.EstUnEclat)
            {
                // Bois 30/32 et roches 40â€“49 sont dÃ©jÃ  couverts plus haut ; ici : outil forgÃ© (100) et autres Ã©clats.
                if (id == 100)
                {
                    rbPose.PhysicsMaterialOverride = _physMatMetalForge;
                    rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                    rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                    rbPose.LinearDamp = 0.12f;
                    rbPose.AngularDamp = 0.55f;
                }
                else
                {
                    rbPose.PhysicsMaterialOverride = _physMatDefautObjet;
                    rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                    rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                    rbPose.LinearDamp = 0.18f;
                    rbPose.AngularDamp = 0.65f;
                }
            }
            if (id == 105 && rbPose is ItemPhysique ipDague)
                ItemPhysique.AppliquerPhysiqueDague105(ipDague);
            else if ((id == 106 || id == IdObjetHachePierreTier1) && rbPose is ItemPhysique ipHachette)
                ItemPhysique.AppliquerPhysiqueHachette106(ipHachette);
            else if (id == IdObjetPellePierreTier0 && rbPose is ItemPhysique ipPelle)
                ItemPhysique.AppliquerPhysiquePelle107(ipPelle);
            else if (id == IdObjetPiochePierreTier0 && rbPose is ItemPhysique ipPioche)
                ItemPhysique.AppliquerPhysiquePioche108(ipPioche);
            else if (id == IdObjetLancePierreTier0 && rbPose is ItemPhysique ipLance)
                ItemPhysique.AppliquerPhysiqueLance111(ipLance);
            else if (id == IdObjetFauxPierreTier0 && rbPose is ItemPhysique ipFaux)
                ItemPhysique.AppliquerPhysiqueFaux112(ipFaux);
        }
        // Fibres / corde non Ã©lastiques : ne pas appliquer dâ€™Ã©chelle Â« Ã©tirÃ©e Â» (herbe, liane, corde boyau+herbe, etc.)
        bool estFlexOuCorde = id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches || id == IdObjetPochetteTier0 || id == IdObjetSacTier0;
        if (estFlexOuCorde && !ObtenirSlotFlexibleEtirable(mainActive))
            corps.Scale = Vector3.One;
        else if (!ItemPhysique.EstIdRocheMatiere(id) && id != 30 && id != 32 && mainActive.ScaleEclat != Vector3.Zero)
            corps.Scale = mainActive.ScaleEclat;
        return corps;
    }
}
