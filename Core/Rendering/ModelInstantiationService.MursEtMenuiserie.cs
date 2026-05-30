using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    /// <summary>Muret (bois/pierre) 4m x 1m : même géométrie/pose, matériau selon l'ID.</summary>
    public static void InstancierModeleMuretBois(Node3D parent, SlotInventaire slot, bool ancrerBaseAuSol = true)
    {
        bool estMuretPierre = slot.ID == IdObjetMuretPierre;
        string cheminGlb = estMuretPierre
            ? "res://Modeles/structure/mur/muret_pierre.glb"
            : "res://Modeles/structure/mur/muret.glb";
        const float longueur = 4f;
        const float hauteur = 1f;
        const float epaisseur = 0.22f;
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        Material matMuret;
        if (estMuretPierre)
        {
            int idxRoche = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            matMuret = ObtenirMaterielSolRoche(idxRoche);
        }
        else
        {
            byte essenceBois = slot.IndexBotanique;
            if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide
                || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
                essenceBois = LSystem_Botanique.IndexChene;
            matMuret = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
        }

        if (scene == null)
        {
            var fb = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(longueur, hauteur, epaisseur) },
                MaterialOverride = matMuret
            };
            parent.AddChild(fb);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMaterielPlancherSurMeshesGlb(modele, matMuret);
        if (ancrerBaseAuSol)
        {
            // Contraintes gameplay:
            // - longueur quasi fondation (~4 m)
            // - hauteur exactement 1 m
            // On ajuste X/Z pour la longueur puis Y pour verrouiller la hauteur.
            Aabb? bounds = null;
            AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
            if (bounds.HasValue)
            {
                Aabb b0 = bounds.Value;
                float longueurModele = Mathf.Max(b0.Size.X, b0.Size.Z);
                // Léger débord pour recouvrir proprement les coins de fondation.
                const float longueurCibleMonde = 4.06f;
                if (longueurModele > 1e-4f)
                {
                    float sXZ = longueurCibleMonde / longueurModele;
                    modele.Scale = new Vector3(modele.Scale.X * sXZ, modele.Scale.Y, modele.Scale.Z * sXZ);
                }

                bounds = null;
                AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
                if (bounds.HasValue)
                {
                    Aabb b1 = bounds.Value;
                    const float hauteurCibleMonde = 1.0f;
                    if (b1.Size.Y > 1e-4f)
                    {
                        float sY = hauteurCibleMonde / b1.Size.Y;
                        modele.Scale = new Vector3(modele.Scale.X, modele.Scale.Y * sY, modele.Scale.Z);
                    }

                    bounds = null;
                    AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
                    if (bounds.HasValue)
                    {
                        Aabb b = bounds.Value;
                        Vector3 centre = b.GetCenter();
                        Vector3 posAvant = modele.Position;
                        modele.Position = new Vector3(
                            posAvant.X - centre.X,
                            posAvant.Y - b.Position.Y,
                            posAvant.Z - centre.Z);
                    }
                }
            }
        }
        else
            NormaliserEchelleEtCentrerModeleArme(modele, 0.92f);
        parent.AddChild(modele);
    }

    /// <summary>Collision simple muret : boîte 4m x 1m x ~0.22m ancrée au sol.</summary>
    public static void AjouterCollisionMuretBois(RigidBody3D corps, Node3D meshRoot)
    {
        if (corps == null || meshRoot == null) return;
        // Collision "mesh exacte" pour alignement parfait avec le visuel du GLB.
        // (Même stratégie robuste que certains meubles complexes.)
        bool auMoinsUneShape = false;
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
                        while (parentNode != null && parentNode != corps && parentNode is Node3D n3d)
                        {
                            t = n3d.Transform * t;
                            parentNode = parentNode.GetParent();
                        }
                        corps.AddChild(new CollisionShape3D
                        {
                            Name = "CollisionMuret",
                            Shape = shape,
                            Transform = t
                        });
                        auMoinsUneShape = true;
                    }
                }
                pile.Add(c);
            }
        }

        if (auMoinsUneShape)
            return;

        // Fallback sécurité si le GLB est indisponible.
        corps.AddChild(new CollisionShape3D
        {
            Name = "CollisionMuret",
            Shape = new BoxShape3D { Size = new Vector3(4.06f, 1f, 0.22f) },
            Position = new Vector3(0f, 0.5f, 0f)
        });
    }

    /// <summary>Mur bois (4m x 3m) : GLB structure/mur, matériau bois selon essence.</summary>
    public static void InstancierModeleMurBois(Node3D parent, SlotInventaire slot, bool ancrerBaseAuSol = true)
    {
        const string cheminGlb = "res://Modeles/structure/mur/Mur_bois.glb";
        const float largeur = 4f;
        const float hauteur = 3f;
        const float epaisseur = 0.22f;
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide
            || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        Material matBois = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);

        if (scene == null)
        {
            parent.AddChild(new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(largeur, hauteur, epaisseur) },
                MaterialOverride = matBois
            });
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMaterielPlancherSurMeshesGlb(modele, matBois);

        if (ancrerBaseAuSol)
        {
            Aabb? bounds = null;
            AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
            if (bounds.HasValue)
            {
                Aabb b0 = bounds.Value;
                float largeurModele = Mathf.Max(b0.Size.X, b0.Size.Z);
                if (largeurModele > 1e-4f)
                {
                    float sXZ = largeur / largeurModele;
                    modele.Scale = new Vector3(modele.Scale.X * sXZ, modele.Scale.Y, modele.Scale.Z * sXZ);
                }

                bounds = null;
                AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
                if (bounds.HasValue)
                {
                    Aabb b1 = bounds.Value;
                    if (b1.Size.Y > 1e-4f)
                    {
                        float sY = hauteur / b1.Size.Y;
                        modele.Scale = new Vector3(modele.Scale.X, modele.Scale.Y * sY, modele.Scale.Z);
                    }
                    bounds = null;
                    AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
                    if (bounds.HasValue)
                    {
                        Aabb b = bounds.Value;
                        Vector3 centre = b.GetCenter();
                        Vector3 posAvant = modele.Position;
                        modele.Position = new Vector3(
                            posAvant.X - centre.X,
                            posAvant.Y - b.Position.Y,
                            posAvant.Z - centre.Z);
                    }
                }
            }
        }
        else
            NormaliserEchelleEtCentrerModeleArme(modele, 0.92f);

        parent.AddChild(modele);
    }

    /// <summary>Mur bois fenêtré (4m x 3m) : double essence (mur + fenêtre) via IndexBotanique/IndexChimique.</summary>
    public static void InstancierModeleMurBoisFenetre(Node3D parent, SlotInventaire slot, bool ancrerBaseAuSol = true)
    {
        const string cheminGlb = "res://Modeles/structure/mur/Mur_bois_fenetre.glb";
        const float largeur = 4f;
        const float hauteur = 3f;
        const float epaisseur = 0.22f;
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceMur = slot.IndexBotanique;
        if (essenceMur == Joueur.TagVarianteLiane || essenceMur == Joueur.TagVarianteHerbeSolide
            || essenceMur == Joueur.TagVarianteIntestin || essenceMur == Joueur.TagVarianteIntestinSolide)
            essenceMur = LSystem_Botanique.IndexChene;
        byte essenceFenetre = (byte)Mathf.Clamp(slot.IndexChimique, 0, 4);
        Material matMur = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceMur);
        Material matFenetre = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceFenetre);

        if (scene == null)
        {
            parent.AddChild(new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(largeur, hauteur, epaisseur) },
                MaterialOverride = matMur
            });
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nom = mi.Name.ToString().ToLowerInvariant();
                bool estFenetre = nom.Contains("fenetre") || nom.Contains("window") || nom.Contains("vitre") || nom.Contains("cadre") || nom.Contains("frame");
                mi.MaterialOverride = estFenetre ? matFenetre : matMur;
            }
            foreach (Node c in n.GetChildren())
                Parcourir(c);
        }
        Parcourir(modele);

        if (ancrerBaseAuSol)
        {
            Aabb? bounds = null;
            AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
            if (bounds.HasValue)
            {
                Aabb b0 = bounds.Value;
                float largeurModele = Mathf.Max(b0.Size.X, b0.Size.Z);
                if (largeurModele > 1e-4f)
                {
                    float sXZ = largeur / largeurModele;
                    modele.Scale = new Vector3(modele.Scale.X * sXZ, modele.Scale.Y, modele.Scale.Z * sXZ);
                }

                bounds = null;
                AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
                if (bounds.HasValue)
                {
                    Aabb b1 = bounds.Value;
                    if (b1.Size.Y > 1e-4f)
                    {
                        float sY = hauteur / b1.Size.Y;
                        modele.Scale = new Vector3(modele.Scale.X, modele.Scale.Y * sY, modele.Scale.Z);
                    }
                    bounds = null;
                    AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
                    if (bounds.HasValue)
                    {
                        Aabb b = bounds.Value;
                        Vector3 centre = b.GetCenter();
                        Vector3 posAvant = modele.Position;
                        modele.Position = new Vector3(
                            posAvant.X - centre.X,
                            posAvant.Y - b.Position.Y,
                            posAvant.Z - centre.Z);
                    }
                }
            }
        }
        else
            NormaliserEchelleEtCentrerModeleArme(modele, 0.92f);

        parent.AddChild(modele);
    }

    /// <summary>Fenêtre bois craftable: texture bois selon l'essence utilisée au craft.</summary>
    public static void InstancierModeleFenetreBois(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.92f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/materials/travailler/fenetre.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide
            || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        Material matBois = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
        if (scene == null)
        {
            parent.AddChild(new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.74f, 0.90f, 0.08f) },
                MaterialOverride = matBois
            });
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        foreach (MeshInstance3D mi in ListerMeshes(modele))
            mi.MaterialOverride = matBois;
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Mur bois cadre de porte (4m x 3m) : essence unique.</summary>
    public static void InstancierModeleMurBoisCadrePorte(Node3D parent, SlotInventaire slot, bool ancrerBaseAuSol = true)
    {
        const string cheminGlb = "res://Modeles/structure/mur/Mur_bois_carde_porte.glb";
        const float largeur = 4f;
        const float hauteur = 3f;
        const float epaisseur = 0.22f;
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide
            || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        Material matBois = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);

        if (scene == null)
        {
            parent.AddChild(new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(largeur, hauteur, epaisseur) },
                MaterialOverride = matBois
            });
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMaterielPlancherSurMeshesGlb(modele, matBois);

        if (ancrerBaseAuSol)
        {
            Aabb? bounds = null;
            AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
            if (bounds.HasValue)
            {
                Aabb b0 = bounds.Value;
                float largeurModele = Mathf.Max(b0.Size.X, b0.Size.Z);
                if (largeurModele > 1e-4f)
                {
                    float sXZ = largeur / largeurModele;
                    modele.Scale = new Vector3(modele.Scale.X * sXZ, modele.Scale.Y, modele.Scale.Z * sXZ);
                }

                bounds = null;
                AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
                if (bounds.HasValue)
                {
                    Aabb b1 = bounds.Value;
                    if (b1.Size.Y > 1e-4f)
                    {
                        float sY = hauteur / b1.Size.Y;
                        modele.Scale = new Vector3(modele.Scale.X, modele.Scale.Y * sY, modele.Scale.Z);
                    }
                    bounds = null;
                    AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
                    if (bounds.HasValue)
                    {
                        Aabb b = bounds.Value;
                        Vector3 centre = b.GetCenter();
                        Vector3 posAvant = modele.Position;
                        modele.Position = new Vector3(
                            posAvant.X - centre.X,
                            posAvant.Y - b.Position.Y,
                            posAvant.Z - centre.Z);
                    }
                }
            }
        }
        else
            NormaliserEchelleEtCentrerModeleArme(modele, 0.92f);

        parent.AddChild(modele);
    }

    /// <summary>Porte bois : GLB porte, centrée et ancrée au sol pour s'insérer dans un mur cadre de porte.</summary>
    public static void InstancierModelePorteBois(Node3D parent, SlotInventaire slot, bool ancrerBaseAuSol = true)
    {
        const string cheminGlb = "res://Modeles/materials/travailler/porte.glb";
        const float largeur = 1.35f;
        const float hauteur = 2.4f;
        const float epaisseur = 0.12f;
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide
            || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        Material matBois = ObtenirMaterielBoisPorteCoffre(essenceBois);

        if (scene == null)
        {
            parent.AddChild(new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(largeur, hauteur, epaisseur) },
                MaterialOverride = matBois
            });
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMaterielPlancherSurMeshesGlb(modele, matBois);
        if (ancrerBaseAuSol)
        {
            Aabb? bounds = null;
            AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
            if (bounds.HasValue)
            {
                Aabb b0 = bounds.Value;
                float largeurModele = Mathf.Max(b0.Size.X, b0.Size.Z);
                if (largeurModele > 1e-4f)
                {
                    float sXZ = largeur / largeurModele;
                    modele.Scale = new Vector3(modele.Scale.X * sXZ, modele.Scale.Y, modele.Scale.Z * sXZ);
                }

                bounds = null;
                AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
                if (bounds.HasValue)
                {
                    Aabb b1 = bounds.Value;
                    if (b1.Size.Y > 1e-4f)
                    {
                        float sY = hauteur / b1.Size.Y;
                        modele.Scale = new Vector3(modele.Scale.X, modele.Scale.Y * sY, modele.Scale.Z);
                    }
                    bounds = null;
                    AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
                    if (bounds.HasValue)
                    {
                        Aabb b = bounds.Value;
                        Vector3 centre = b.GetCenter();
                        Vector3 posAvant = modele.Position;
                        modele.Position = new Vector3(
                            posAvant.X - centre.X,
                            posAvant.Y - b.Position.Y,
                            posAvant.Z - centre.Z);
                    }
                }
            }
        }
        else
            NormaliserEchelleEtCentrerModeleArme(modele, 0.92f);

        parent.AddChild(modele);
    }

    /// <summary>Torche: bâton bois + tissu, avec option visuelle allumée via <c>GenomeAssemblage=TORCHE:1</c>.</summary>
    public static void InstancierModeleTorche(Node3D parent, SlotInventaire slot, bool ancrerBaseAuSol = true)
    {
        const string cheminGlb = "res://Modeles/Equipements/torch.glb";
        const float largeur = 0.16f;
        const float hauteur = 1.12f;
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        Material matBois = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
        bool allumee = string.Equals(slot.GenomeAssemblage ?? "", "TORCHE:1", StringComparison.Ordinal);

        if (scene == null)
        {
            var fb = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new CapsuleMesh { Radius = 0.05f, Height = 0.8f },
                MaterialOverride = matBois
            };
            parent.AddChild(fb);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        foreach (MeshInstance3D mi in ListerMeshes(modele))
        {
            string nom = mi.Name.ToString().ToLowerInvariant();
            bool estTissu = nom.Contains("cloth") || nom.Contains("tissu") || nom.Contains("fabric") || nom.Contains("rag") || nom.Contains("chiff");
            if (estTissu)
                AppliquerMaterielObjet(mi, 21, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, slot.IndexBotanique);
            else
                mi.MaterialOverride = matBois;
        }
        if (ancrerBaseAuSol)
            NormaliserDimensionsAncrerAuSol(modele, largeur, hauteur, largeur);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, 0.58f);
        parent.AddChild(modele);

        if (allumee)
            ItemPhysique.AttacherVisuelFlammeTorche(parent);
    }

    private static Material ObtenirMaterielBoisPorteCoffre(byte essenceBois)
    {
        var src = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois) as StandardMaterial3D;
        if (src == null)
            return ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);

        var mat = (StandardMaterial3D)src.Duplicate(true);
        mat.NormalEnabled = false;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        mat.Roughness = 0.88f;
        mat.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
        Color baseC = mat.AlbedoColor;
        const float boostLuminosite = 1.20f;
        mat.AlbedoColor = new Color(
            Mathf.Min(baseC.R * boostLuminosite, 1.25f),
            Mathf.Min(baseC.G * boostLuminosite, 1.25f),
            Mathf.Min(baseC.B * boostLuminosite, 1.25f),
            baseC.A);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(0.18f, 0.14f, 0.09f);
        mat.EmissionEnergyMultiplier = 0.12f;
        return mat;
    }

    public enum ToitChaumeVarianteVisuelle
    {
        Solo,
        Long,
        Angle
    }

    private static Material ObtenirMaterielToitChaumeDepuisSlot(SlotInventaire slot)
    {
        byte tag = slot.IndexBotanique;
        if (tag == Joueur.TagVarianteLiane)
            return Atlas_Matiere.ObtenirMaterielCorde(16, 16, 0);
        if (tag == Joueur.TagVarianteHerbeSolide)
            return Atlas_Matiere.ObtenirMaterielCorde(15, 15, 2);
        if (tag == Joueur.TagVarianteIntestin)
            return Atlas_Matiere.ObtenirMaterielCorde(17, 17, 0);
        if (tag == Joueur.TagVarianteIntestinSolide)
            return Atlas_Matiere.ObtenirMaterielCorde(17, 17, 2);
        return Atlas_Matiere.ObtenirMaterielCorde(16, 16, 0);
    }

    private static string ObtenirCheminModeleToitChaume(ToitChaumeVarianteVisuelle variante) => variante switch
    {
        ToitChaumeVarianteVisuelle.Long => "res://Modeles/structure/toie/Toie_chaume_long.glb",
        ToitChaumeVarianteVisuelle.Angle => "res://Modeles/structure/toie/Toie_chaum_L.glb",
        _ => "res://Modeles/structure/toie/toie_chaume.glb"
    };

    /// <summary>Toit chaume modulaire : variante visuelle (solo/long/L), texture via liage (IndexBotanique).</summary>
    public static void InstancierModeleToitChaume(
        Node3D parent,
        SlotInventaire slot,
        ToitChaumeVarianteVisuelle variante = ToitChaumeVarianteVisuelle.Solo,
        bool ancrerBaseAuSol = true,
        float facteurEchelleXZ = 1f,
        Vector3? decalageLocal = null,
        float rotationLocaleYDeg = 0f)
    {
        string cheminGlb = ObtenirCheminModeleToitChaume(variante);
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        Material matChaume = ObtenirMaterielToitChaumeDepuisSlot(slot);
        const float tailleSolo = 4.30f;
        const float tailleLongue = 8.30f;
        float cibleX = variante == ToitChaumeVarianteVisuelle.Solo ? tailleSolo : tailleLongue;
        float cibleZ = variante == ToitChaumeVarianteVisuelle.Long ? tailleSolo : (variante == ToitChaumeVarianteVisuelle.Solo ? tailleSolo : tailleLongue);
        cibleX *= facteurEchelleXZ;
        cibleZ *= facteurEchelleXZ;
        const float hauteurFallback = 0.42f;
        if (scene == null)
        {
            parent.AddChild(new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(cibleX, hauteurFallback, cibleZ) },
                MaterialOverride = matChaume,
                Position = decalageLocal ?? Vector3.Zero,
                RotationDegrees = new Vector3(0f, rotationLocaleYDeg, 0f)
            });
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMaterielPlancherSurMeshesGlb(modele, matChaume);
        if (ancrerBaseAuSol)
        {
            // Important: on préserve la silhouette (pas d'écrasement Y forcé).
            // On ajuste l'emprise X/Z puis on garde la proportion verticale du GLB.
            Aabb? bounds = null;
            AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
            if (bounds.HasValue)
            {
                Aabb b0 = bounds.Value;
                float sx = b0.Size.X > 1e-4f ? (cibleX / b0.Size.X) : 1f;
                float sz = b0.Size.Z > 1e-4f ? (cibleZ / b0.Size.Z) : 1f;
                // Garder l'emprise X/Z demandée (4.2 / 8.2) mais conserver le volume:
                // on scale Y avec la moyenne XZ pour éviter l'effet "crêpe".
                float sy = (sx + sz) * 0.5f;
                modele.Scale *= new Vector3(sx, sy, sz);

                bounds = null;
                AccumulerAabbMeshes(modele, Transform3D.Identity, ref bounds);
                if (bounds.HasValue)
                {
                    Aabb b = bounds.Value;
                    Vector3 centre = b.GetCenter();
                    Vector3 posAvant = modele.Position;
                    modele.Position = new Vector3(
                        posAvant.X - centre.X,
                        posAvant.Y - b.Position.Y,
                        posAvant.Z - centre.Z);
                }
            }
        }
        else
            NormaliserEchelleEtCentrerModeleArme(modele, 0.9f);
        modele.Position += decalageLocal ?? Vector3.Zero;
        modele.RotationDegrees = new Vector3(0f, rotationLocaleYDeg, 0f);
        parent.AddChild(modele);
    }

    /// <summary>Collision toit chaume : trimesh alignée au visuel, fallback boîte.</summary>
    public static void AjouterCollisionToitChaume(RigidBody3D corps, Node3D meshRoot)
    {
        if (corps == null || meshRoot == null) return;
        bool auMoinsUneShape = false;
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
                        while (parentNode != null && parentNode != corps && parentNode is Node3D n3d)
                        {
                            t = n3d.Transform * t;
                            parentNode = parentNode.GetParent();
                        }
                        corps.AddChild(new CollisionShape3D
                        {
                            Name = "CollisionToitChaume",
                            Shape = shape,
                            Transform = t
                        });
                        auMoinsUneShape = true;
                    }
                }
                pile.Add(c);
            }
        }
        if (auMoinsUneShape)
            return;
        corps.AddChild(new CollisionShape3D
        {
            Name = "CollisionToitChaume",
            Shape = new BoxShape3D { Size = new Vector3(4.30f, 0.34f, 4.30f) },
            Position = new Vector3(0f, 0.17f, 0f)
        });
    }

    /// <summary>Collision torche : capsule compacte pour pose sol/mur.</summary>
    public static void AjouterCollisionTorche(RigidBody3D corps, Node3D meshRoot)
    {
        if (corps == null) return;
        corps.AddChild(new CollisionShape3D
        {
            Name = "CollisionTorche",
            Shape = new CapsuleShape3D { Radius = 0.08f, Height = 0.92f },
            Position = new Vector3(0f, 0.56f, 0f)
        });
    }

    /// <summary>Collision porte bois : trimesh fidèle au modèle, repli boîte 1.35x2.4x0.12.</summary>
    public static void AjouterCollisionPorteBois(RigidBody3D corps, Node3D meshRoot)
    {
        if (corps == null || meshRoot == null) return;
        bool auMoinsUneShape = false;
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
                        while (parentNode != null && parentNode != corps && parentNode is Node3D n3d)
                        {
                            t = n3d.Transform * t;
                            parentNode = parentNode.GetParent();
                        }
                        corps.AddChild(new CollisionShape3D
                        {
                            Name = "CollisionPorteBois",
                            Shape = shape,
                            Transform = t
                        });
                        auMoinsUneShape = true;
                    }
                }
                pile.Add(c);
            }
        }

        if (auMoinsUneShape)
            return;

        corps.AddChild(new CollisionShape3D
        {
            Name = "CollisionPorteBois",
            Shape = new BoxShape3D { Size = new Vector3(1.35f, 2.4f, 0.12f) },
            Position = new Vector3(0f, 1.2f, 0f)
        });
    }

    /// <summary>Collision mur bois : trimesh alignée au visuel, repli boîte 4x3x0,22.</summary>
    public static void AjouterCollisionMurBois(RigidBody3D corps, Node3D meshRoot)
    {
        if (corps == null || meshRoot == null) return;
        bool auMoinsUneShape = false;
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
                        while (parentNode != null && parentNode != corps && parentNode is Node3D n3d)
                        {
                            t = n3d.Transform * t;
                            parentNode = parentNode.GetParent();
                        }
                        corps.AddChild(new CollisionShape3D
                        {
                            Name = "CollisionMurBois",
                            Shape = shape,
                            Transform = t
                        });
                        auMoinsUneShape = true;
                    }
                }
                pile.Add(c);
            }
        }
        if (auMoinsUneShape)
            return;
        corps.AddChild(new CollisionShape3D
        {
            Name = "CollisionMurBois",
            Shape = new BoxShape3D { Size = new Vector3(4f, 3f, 0.22f) },
            Position = new Vector3(0f, 1.5f, 0f)
        });
    }

    /// <summary>
    /// Collision dédiée pour le mur cadre de porte :
    /// 3 boîtes (2 montants + 1 linteau) recalées sur l'AABB du mesh pour éviter tout décalage.
    /// </summary>
    public static void AjouterCollisionMurBoisCadrePorte(RigidBody3D corps, Node3D meshRoot)
    {
        if (corps == null || meshRoot == null) return;

        float largeurTotale = 4f;
        float hauteurTotale = 3f;
        float epaisseur = 0.22f;
        bool largeurSurAxeX = true;
        Vector3 offsetPivot = Vector3.Zero;

        Aabb? bounds = null;
        AccumulerAabbMeshes(meshRoot, Transform3D.Identity, ref bounds);
        if (bounds.HasValue)
        {
            Aabb b = bounds.Value;
            float sizeX = Mathf.Max(0.01f, b.Size.X);
            float sizeY = Mathf.Max(0.01f, b.Size.Y);
            float sizeZ = Mathf.Max(0.01f, b.Size.Z);
            largeurTotale = Mathf.Max(sizeX, sizeZ);
            epaisseur = Mathf.Min(sizeX, sizeZ);
            hauteurTotale = sizeY;
            largeurSurAxeX = sizeX >= sizeZ;
            Vector3 centre = b.GetCenter();
            offsetPivot = new Vector3(centre.X, b.Position.Y, centre.Z);
        }

        // Ratios gameplay validés sur 4x3 (ouverture ~1.7 x 2.3).
        float largeurOuverture = Mathf.Clamp(largeurTotale * 0.425f, 1.35f, largeurTotale - 0.25f);
        float hauteurOuverture = Mathf.Clamp(hauteurTotale * 0.767f, 1.9f, hauteurTotale - 0.2f);

        float largeurMontant = (largeurTotale - largeurOuverture) * 0.5f;
        float demiOuverture = largeurOuverture * 0.5f;
        float hauteurLinteau = Mathf.Max(0.15f, hauteurTotale - hauteurOuverture);

        Basis baseCollision = largeurSurAxeX
            ? Basis.Identity
            : new Basis(Vector3.Up, Mathf.Pi * 0.5f);
        Vector3 PositionnerLocal(Vector3 local) => offsetPivot + (baseCollision * local);

        // Montant gauche
        corps.AddChild(new CollisionShape3D
        {
            Name = "CollisionPorte_MontantG",
            Shape = new BoxShape3D { Size = new Vector3(largeurMontant, hauteurOuverture, epaisseur) },
            Transform = new Transform3D(
                baseCollision,
                PositionnerLocal(new Vector3(-(demiOuverture + largeurMontant * 0.5f), hauteurOuverture * 0.5f, 0f)))
        });

        // Montant droit
        corps.AddChild(new CollisionShape3D
        {
            Name = "CollisionPorte_MontantD",
            Shape = new BoxShape3D { Size = new Vector3(largeurMontant, hauteurOuverture, epaisseur) },
            Transform = new Transform3D(
                baseCollision,
                PositionnerLocal(new Vector3(+(demiOuverture + largeurMontant * 0.5f), hauteurOuverture * 0.5f, 0f)))
        });

        // Linteau supérieur
        corps.AddChild(new CollisionShape3D
        {
            Name = "CollisionPorte_Linteau",
            Shape = new BoxShape3D { Size = new Vector3(largeurTotale, hauteurLinteau, epaisseur) },
            Transform = new Transform3D(
                baseCollision,
                PositionnerLocal(new Vector3(0f, hauteurOuverture + hauteurLinteau * 0.5f, 0f)))
        });
    }

    /// <summary>Collision plane pour marcher sur un plancher (bois ou roche).</summary>
    public static void AjouterCollisionPlancherSolBois(RigidBody3D corps, Node3D meshRoot)
    {
        if (corps == null || meshRoot == null) return;
        Aabb? bounds = null;
        AccumulerAabbMeshes(meshRoot, Transform3D.Identity, ref bounds);
        const float epaisseurPlateau = 0.06f;
        if (bounds.HasValue)
        {
            Aabb b = bounds.Value;
            float topY = b.End.Y;
            Vector3 centre = b.GetCenter();
            float largeur = Mathf.Clamp(b.Size.X, 0.8f, Joueur.PlancherEmpriseMetres);
            float profondeur = Mathf.Clamp(b.Size.Z, 0.8f, Joueur.PlancherEmpriseMetres);
            corps.AddChild(new CollisionShape3D
            {
                Name = "CollisionPlateauMarche",
                Shape = new BoxShape3D { Size = new Vector3(largeur, epaisseurPlateau, profondeur) },
                Position = new Vector3(centre.X, topY - epaisseurPlateau * 0.5f, centre.Z)
            });
            return;
        }
        corps.AddChild(new CollisionShape3D
        {
            Name = "CollisionPlateauMarche",
            Shape = new BoxShape3D { Size = new Vector3(Joueur.PlancherEmpriseMetres, epaisseurPlateau, Joueur.PlancherEmpriseMetres) },
            Position = new Vector3(0f, Joueur.PlancherEpaisseurMetres - epaisseurPlateau * 0.5f, 0f)
        });
    }
}
