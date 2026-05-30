using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private static int CalculerSignatureVisuelleRack(ItemPhysique rack)
    {
        if (rack?.GrillePlanTravailAtelier == null) return 0;
        int h = 17;
        int n = Mathf.Min(9, rack.GrillePlanTravailAtelier.Length);
        for (int i = 0; i < n; i++)
        {
            var s = rack.GrillePlanTravailAtelier[i];
            h = HashCode.Combine(h, s.ID, Joueur.ObtenirQuantiteSlot(s), s.IndexBotanique, s.IndexChimique, s.IndexMorphologique, s.NiveauFracture);
        }
        return h;
    }

    private static Node3D ObtenirOuCreerConteneurVisuelRack(Node3D meshRoot)
    {
        if (meshRoot == null) return null;
        Node3D n = meshRoot.GetNodeOrNull<Node3D>("RackContenuVisuel");
        if (n != null && GodotObject.IsInstanceValid(n)) return n;
        n = new Node3D { Name = "RackContenuVisuel" };
        meshRoot.AddChild(n);
        return n;
    }

    public void SynchroniserVisuelRackBatons(ItemPhysique rack)
    {
        if (rack == null || !GodotObject.IsInstanceValid(rack) || rack.ID_Objet != Joueur.IdObjetRackBatons)
            return;
        Node3D meshRoot = rack.GetNodeOrNull<Node3D>("MeshInstance3D");
        if (meshRoot == null || !GodotObject.IsInstanceValid(meshRoot))
            return;

        int sig = CalculerSignatureVisuelleRack(rack);
        int sigPrec = rack.HasMeta("RackVisSig") ? rack.GetMeta("RackVisSig").AsInt32() : int.MinValue;
        if (sig == sigPrec)
            return;
        rack.SetMeta("RackVisSig", sig);

        Node3D conteneur = ObtenirOuCreerConteneurVisuelRack(meshRoot);
        if (conteneur == null) return;
        foreach (Node c in conteneur.GetChildren())
            c.QueueFree();

        // Génère jusqu'à 30 tiges visuelles, positionnées dans le rack.
        var unites = new List<SlotInventaire>(30);
        int n = Mathf.Min(9, rack.GrillePlanTravailAtelier.Length);
        for (int i = 0; i < n && unites.Count < 30; i++)
        {
            var s = rack.GrillePlanTravailAtelier[i];
            if (s.EstVide || (s.ID != 30 && s.ID != 32 && s.ID != BlocChutant.ID_BRANCHE)) continue;
            int q = Mathf.Clamp(ObtenirQuantiteSlot(s), 1, 30);
            for (int k = 0; k < q && unites.Count < 30; k++)
                unites.Add(s);
        }

        for (int i = 0; i < unites.Count; i++)
        {
            var s = unites[i];
            int col = i % 5;
            int row = i / 5;

            float x = -0.18f + col * 0.09f;
            float z = -0.24f + row * 0.08f;
            float yBase = 0.01f;

            var rng = new RandomNumberGenerator();
            rng.Seed = unchecked((ulong)(uint)HashCode.Combine(sig, i, s.ID, s.IndexBotanique));
            float tiltX = rng.RandfRange(-9f, 9f);
            float tiltZ = rng.RandfRange(-6f, 6f);
            float yaw = rng.RandfRange(0f, 360f);

            Mesh batonMesh = s.EstUnEclat ? s.MeshEclat : ObtenirMeshDepuisCache(s.ID, s.IndexMorphologique, s.IndexTaille);
            if (batonMesh == null) continue;
            float scale = s.ID == 30 ? 0.72f : 0.72f;
            float demiH = batonMesh.GetAabb().Size.Y * scale * 0.5f;
            var mi = new MeshInstance3D
            {
                Name = $"Stick_{i:D2}",
                Mesh = batonMesh,
                Position = new Vector3(x, yBase + demiH, z),
                RotationDegrees = new Vector3(tiltX, yaw, tiltZ)
            };
            mi.Scale = Vector3.One * scale;
            AppliquerMaterielObjet(mi, s.ID, s.IndexChimique, s.IndexMorphologique, s.NiveauFracture, s.IndexBotanique);
            conteneur.AddChild(mi);
        }
    }

    public void SynchroniserVisuelRackBuches(ItemPhysique rack)
    {
        if (rack == null || !GodotObject.IsInstanceValid(rack) || rack.ID_Objet != Joueur.IdObjetRackBuches)
            return;
        Node3D meshRoot = rack.GetNodeOrNull<Node3D>("MeshInstance3D");
        if (meshRoot == null || !GodotObject.IsInstanceValid(meshRoot))
            return;

        int sig = CalculerSignatureVisuelleRack(rack);
        int sigPrec = rack.HasMeta("RackVisSig") ? rack.GetMeta("RackVisSig").AsInt32() : int.MinValue;
        if (sig == sigPrec)
            return;
        rack.SetMeta("RackVisSig", sig);

        Node3D conteneur = ObtenirOuCreerConteneurVisuelRack(meshRoot);
        if (conteneur == null) return;
        foreach (Node c in conteneur.GetChildren())
            c.QueueFree();

        var unites = new List<SlotInventaire>(10);
        int n = Mathf.Min(9, rack.GrillePlanTravailAtelier.Length);
        for (int i = 0; i < n && unites.Count < 10; i++)
        {
            var s = rack.GrillePlanTravailAtelier[i];
            if (s.EstVide || s.ID != 30) continue;
            int q = Mathf.Clamp(ObtenirQuantiteSlot(s), 1, 10);
            for (int k = 0; k < q && unites.Count < 10; k++)
                unites.Add(s);
        }

        for (int i = 0; i < unites.Count; i++)
        {
            SlotInventaire s = unites[i];
            int col = i % 5;
            int row = i / 5;
            float x = -0.22f + col * 0.11f;
            float z = row == 0 ? -0.07f : 0.08f;
            float y = 0.18f + row * 0.09f;

            var rng = new RandomNumberGenerator();
            rng.Seed = unchecked((ulong)(uint)HashCode.Combine(sig, i, s.IndexBotanique, s.IndexMorphologique));

            Mesh meshBuche = s.EstUnEclat ? s.MeshEclat : ObtenirMeshDepuisCache(30, s.IndexMorphologique, s.IndexTaille);
            if (meshBuche == null) continue;
            var mi = new MeshInstance3D
            {
                Name = $"Log_{i:D2}",
                Mesh = meshBuche,
                Position = new Vector3(x, y, z),
                RotationDegrees = new Vector3(90f + rng.RandfRange(-4f, 4f), rng.RandfRange(-12f, 12f), rng.RandfRange(-6f, 6f))
            };
            mi.Scale = Vector3.One * 0.58f;
            AppliquerMaterielObjet(mi, 30, s.IndexChimique, s.IndexMorphologique, s.NiveauFracture, s.IndexBotanique);
            conteneur.AddChild(mi);
        }
    }

    /// <summary>Corde tressée tier 0 (gazon) : GLB <c>traisagre_corde_tier0.glb</c> + matériaux <see cref="Atlas_Matiere.ObtenirMaterielCorde"/> (même logique cord/roche que l’atelier).</summary>
    public static void InstancierModeleCordeTier0Gazon(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.34f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/materials/traisagre_corde_tier0.glb");
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        var rng = new RandomNumberGenerator();
        rng.Seed = unchecked((ulong)(uint)HashCode.Combine(slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, slot.IndexBotanique, 20));

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nomLower = mi.Name.ToString().ToLowerInvariant();
                if (nomLower.Contains("cord"))
                {
                    RemplacerMeshParNormalesFacettes(mi);
                    AppliquerMaterielObjet(mi, 20, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, slot.IndexBotanique);
                }
                else if (nomLower.Contains("roche"))
                {
                    int randChimique = rng.RandiRange(0, ItemPhysique.TableGeologique.Length - 1);
                    int idRoche = ItemPhysique.IdRocheMatiereMin + randChimique;
                    AppliquerMaterielObjet(mi, idRoche, randChimique, 0, 0, slot.IndexBotanique);
                }
                else
                {
                    RemplacerMeshParNormalesFacettes(mi);
                    AppliquerMaterielObjet(mi, 20, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, slot.IndexBotanique);
                }
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Tissu tier 0 : GLB <c>tissu_tier0.glb</c> ; matériau identique à la corde (<see cref="Atlas_Matiere.ObtenirMaterielCorde"/>), sans triplanar bruit sur le relief.</summary>
    public static void InstancierModeleTissuTier0(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.36f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/materials/tissu_tier0.glb");
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                RemplacerMeshParNormalesFacettes(mi);
                AppliquerMaterielObjet(mi, 21, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, slot.IndexBotanique);
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Ceinture à poches : GLB <c>centure_tresser.glb</c> ; même matériau procédural que corde/tissu (<see cref="Atlas_Matiere.ObtenirMaterielCorde"/>).</summary>
    public static void InstancierModeleCeinturePoches(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.4f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Equipable/centure_tresser.glb");
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                RemplacerMeshParNormalesFacettes(mi);
                AppliquerMaterielObjet(mi, Joueur.IdObjetCeinturePoches, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, slot.IndexBotanique);
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Ceinture à sacoches (104) : GLB avec poches visibles ; même matière corde/tissu procédurale que ceinture / pochette.</summary>
    public static void InstancierModeleCeintureSacoches(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.42f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Equipable/centure_tier0_Avec_pochette.glb");
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        var tagsPochettes = ObtenirTagsPochettesCeinture(slot);
        byte tagCeinture = EstVarianteHerbeSolide(slot) ? Joueur.TagVarianteHerbeSolide
            : (EstVarianteLiane(slot) ? Joueur.TagVarianteLiane : (byte)0);
        var matCeinture = ObtenirMaterielPochetteCeinture(slot, tagCeinture);
        var matsPochettes = new Material[]
        {
            ObtenirMaterielPochetteCeinture(slot, tagsPochettes[0]),
            ObtenirMaterielPochetteCeinture(slot, tagsPochettes[1]),
            ObtenirMaterielPochetteCeinture(slot, tagsPochettes[2]),
            ObtenirMaterielPochetteCeinture(slot, tagsPochettes[3])
        };

        var meshesPochettes = new List<MeshInstance3D>();
        var meshesCeinture = new List<MeshInstance3D>();
        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                RemplacerMeshParNormalesFacettes(mi);
                string nom = mi.Name.ToString().ToLowerInvariant();
                bool estPochette = nom.Contains("pochette") || nom.Contains("pouch") || nom.Contains("sacoche");
                if (estPochette) meshesPochettes.Add(mi);
                else meshesCeinture.Add(mi);
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        foreach (var m in meshesCeinture)
            m.MaterialOverride = matCeinture;

        if (meshesPochettes.Count > 0)
        {
            // Mapping stable: ligne haute (Z+) puis basse (Z-), de gauche (X-) vers droite (X+).
            meshesPochettes.Sort((a, b) =>
            {
                Vector3 pa = a.GlobalTransform.Origin;
                Vector3 pb = b.GlobalTransform.Origin;
                int zCmp = pb.Z.CompareTo(pa.Z);
                return zCmp != 0 ? zCmp : pa.X.CompareTo(pb.X);
            });
            for (int i = 0; i < meshesPochettes.Count; i++)
                meshesPochettes[i].MaterialOverride = matsPochettes[Mathf.Clamp(i, 0, matsPochettes.Length - 1)];
        }
        else
        {
            // Fallback import: pas de mesh explicitement nommé "pochette".
            foreach (var m in ListerMeshes(modele))
                m.MaterialOverride = matCeinture;
        }
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Pochette tier 0 : GLB <c>Pochette_Tiere0.glb</c> ; même matériau procédural que corde/tissu/ceinture.</summary>
    public static void InstancierModelePochetteTier0(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.36f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/materials/Pochette_Tiere0.glb");
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                RemplacerMeshParNormalesFacettes(mi);
                AppliquerMaterielObjet(mi, Joueur.IdObjetPochetteTier0, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, slot.IndexBotanique);
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    public static void InstancierModeleSacTier0(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.4f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Equipable/Sac_Tiere0.glb");
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                RemplacerMeshParNormalesFacettes(mi);
                AppliquerMaterielObjet(mi, Joueur.IdObjetSacTier0, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, slot.IndexBotanique);
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Petite baie récoltée sur buisson : modèle GLB dédié + teinte pilotée par IndexChimique.</summary>
    public static void InstancierModeleBaie(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.18f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Nouriture/Petite_Bais.glb");
        NettoyerModelesEnfants(parent);
        Node3D modele;
        if (scene != null)
        {
            modele = scene.Instantiate<Node3D>();
        }
        else
        {
            // Fallback export/runtime: garder un visuel même si le GLB n'est pas dispo.
            var racineFallback = new Node3D { Name = "ModeleArme" };
            var meshFallback = new MeshInstance3D
            {
                Name = "ModeleArme_Fallback",
                Mesh = new SphereMesh
                {
                    Radius = 0.06f,
                    Height = 0.12f,
                    RadialSegments = 12,
                    Rings = 8
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = Joueur.ObtenirCouleurAlbedoBaie(slot.IndexChimique),
                    Roughness = 0.78f,
                    Metallic = 0f
                }
            };
            racineFallback.AddChild(meshFallback);
            modele = racineFallback;
            GD.PrintErr("ZERO-K : Modele baie GLB introuvable, fallback sphere utilise.");
        }
        modele.Name = "ModeleArme";

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
                AppliquerMaterielObjet(mi, Joueur.IdObjetBaie, slot.IndexChimique, 0, 0, slot.IndexBotanique);
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Feuille arrachée (bouleau, chêne, sapin, …) : GLB dédié (sol, inventaire, main, lancer).</summary>
    public static void InstancierModeleFeuilleArrachee(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.2f)
    {
        if (!BlocChutant.EssenceUtiliseFeuilleGlb(slot.IndexBotanique))
            return;
        NettoyerModelesEnfants(parent);
        Node3D? visuel = BlocChutant.InstancierRacineVisuelFeuilleGlb(slot.IndexBotanique, parent, tailleMaxMetres, variationAleatoire: false, out _);
        if (visuel == null)
            GD.PrintErr($"ZERO-K : Feuille GLB essence {slot.IndexBotanique} — repli sans modèle.");
        else
            visuel.Name = "ModeleArme";
    }

    /// <inheritdoc cref="InstancierModeleFeuilleArrachee"/>
    public static void InstancierModeleFeuilleBouleau(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.2f)
        => InstancierModeleFeuilleArrachee(parent, slot, tailleMaxMetres);

    /// <summary>Steak cru (GLB) — loot bovin.</summary>
    public static void InstancierModeleSteakCru(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.2f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Nouriture/steak_cru.glb");
        if (scene == null) return;
        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Steak cuit (GLB) — résultat cuisson pit roche.</summary>
    public static void InstancierModeleSteakCuit(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.2f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Nouriture/steak+cuit.glb");
        if (scene == null) return;
        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Os (GLB) — loot bovin. Échelle visuelle +40 % par rapport à la base d’origine (0,22 m).</summary>
    public static void InstancierModeleOsBoeuf(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.308f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/materials/bone.glb");
        if (scene == null) return;
        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Cuir (GLB) — albedo depuis <see cref="SlotInventaire.GenomeAssemblage"/> (<c>PEAU:</c> + chemin res:// ou repli teinte). Échelle visuelle +20 % par rapport à la base d’origine (0,24 m).</summary>
    public static void InstancierModeleCuirBoeuf(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.288f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/materials/Cuire.glb");
        if (scene == null) return;
        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        string g = slot.GenomeAssemblage ?? "";
        Texture2D albedo = null;
        if (g.StartsWith("PEAU:", StringComparison.Ordinal))
        {
            string reste = g.Length > 5 ? g.Substring(5) : "";
            if (reste.Length > 0 && reste != "TAUREAU" && reste != "VACHE" && ResourceLoader.Exists(reste))
                albedo = GD.Load<Texture2D>(reste);
        }
        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                if (albedo != null)
                    mi.MaterialOverride = new StandardMaterial3D { AlbedoTexture = albedo, Roughness = 0.88f, Metallic = 0f };
                else
                {
                    bool taureau = g.IndexOf("TAUREAU", StringComparison.Ordinal) >= 0;
                    mi.MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = taureau ? new Color(0.34f, 0.21f, 0.13f) : new Color(0.44f, 0.36f, 0.28f),
                        Roughness = 0.9f,
                        Metallic = 0f
                    };
                }
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }
        ParcourirMeshes(modele);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    private static void AppliquerMateriauIntestin(Node3D modele, Material materiau)
    {
        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
                mi.MaterialOverride = materiau;
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
    }

    private static Texture2D CreerTextureProceduraleIntestinNettoye()
    {
        const int largeur = 128;
        const int hauteur = 128;
        var img = Image.CreateEmpty(largeur, hauteur, false, Image.Format.Rgba8);
        Color baseC = new Color(0.82f, 0.70f, 0.64f);
        Color veineC = new Color(0.92f, 0.78f, 0.72f);
        for (int y = 0; y < hauteur; y++)
        {
            float vy = y / (float)(hauteur - 1);
            for (int x = 0; x < largeur; x++)
            {
                float vx = x / (float)(largeur - 1);
                float bandes = Mathf.Sin((vx * 10.8f + vy * 2.4f) * Mathf.Pi);
                float nervure = Mathf.Sin((vx * 27.0f - vy * 6.0f) * Mathf.Pi) * 0.5f + 0.5f;
                float grain = Mathf.Sin((vx * 96.0f + 0.37f) * 12.0f) * Mathf.Sin((vy * 96.0f + 0.91f) * 11.0f);
                float lissage = Mathf.Clamp(0.52f + bandes * 0.20f + nervure * 0.18f + grain * 0.10f, 0.2f, 1f);
                Color c = baseC.Lerp(veineC, Mathf.Clamp(nervure * 0.65f + 0.12f, 0f, 1f));
                c = c.Lightened((lissage - 0.5f) * 0.34f);
                img.SetPixel(x, y, new Color(c.R, c.G, c.B, 1f));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Intestin (GLB) — loot bovin. Matériau organique rose appliqué en code.</summary>
    public static void InstancierModeleIntestinBoeuf(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.26f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/materials/intestin+de+bovin.glb");
        if (scene == null) return;
        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        var matIntestinSale = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.82f, 0.45f, 0.52f),
            Roughness = 0.93f,
            Metallic = 0f
        };
        AppliquerMateriauIntestin(modele, matIntestinSale);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Intestin propre (GLB) — texture procédurale réaliste générée en code.</summary>
    public static void InstancierModeleIntestinBoeufNettoye(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.26f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/materials/intestin+netoyer.glb");
        if (scene == null) return;
        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        Texture2D texNettoyee = CreerTextureProceduraleIntestinNettoye();
        var matIntestinNettoye = new StandardMaterial3D
        {
            AlbedoTexture = texNettoyee,
            AlbedoColor = new Color(1f, 1f, 1f),
            Roughness = 0.8f,
            Metallic = 0f
        };
        AppliquerMateriauIntestin(modele, matIntestinNettoye);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }
}
