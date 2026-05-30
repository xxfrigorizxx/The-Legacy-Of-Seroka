using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    /// <param name="tailleMaxMetres">Hors main : ~1,1 m pour une table lisible au sol.</param>
    /// <param name="ancrerBaseAuSol">True une fois posée : base du mesh sur Y=0 sous le RigidBody.</param>
    public static void InstancierModeleAtelierPrimitif(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.88f, bool ancrerBaseAuSol = false)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Ateliers/table_de_Craft_tiere_0.glb");
        if (scene == null)
            scene = GD.Load<PackedScene>("res://Modeles/materials/moblier/table.glb");
        if (scene == null) return;

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        Dictionary<string, string> cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string genome = slot.GenomeAssemblage ?? "";
        if (genome.StartsWith("ATELIER200", StringComparison.Ordinal))
        {
            string[] parts = genome.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                int idxEq = p.IndexOf('=');
                if (idxEq <= 0 || idxEq >= p.Length - 1)
                    continue;
                cfg[p.Substring(0, idxEq).Trim()] = p.Substring(idxEq + 1).Trim();
            }
        }

        int idxRoche = cfg.TryGetValue("R", out string rawR) && int.TryParse(rawR, out int rVal)
            ? Mathf.Clamp(rVal, 0, ItemPhysique.TableGeologique.Length - 1)
            : 0;
        int ligC = cfg.TryGetValue("LIGC", out string rawC) && int.TryParse(rawC, out int cVal) ? cVal : slot.IndexChimique;
        int ligM = cfg.TryGetValue("LIGM", out string rawM) && int.TryParse(rawM, out int mVal) ? mVal : slot.IndexMorphologique;
        byte essenceBois = slot.IndexBotanique;

        Material matBois = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);

        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nom = mi.Name.ToString().ToLowerInvariant();
                bool estLigature = nom.Contains("liage") || nom.Contains("ligature") || nom.Contains("corde") || nom.Contains("cordr") || nom.Contains("rope") || nom.Contains("cord") || nom.Contains("liane") || nom.Contains("ficelle");
                bool estRoche = nom.Contains("roche") || nom.Contains("stone") || nom.Contains("rock") || nom.Contains("pierre") || nom.Contains("caill");
                bool estBois = nom.Contains("bois") || nom.Contains("wood") || nom.Contains("planche") || nom.Contains("baton") || nom.Contains("stick") || nom.Contains("table") || nom.Contains("log") || nom.Contains("buche") || nom.StartsWith("t.");

                if (estLigature)
                {
                    AppliquerMaterielObjet(mi, 20, ligC, ligM, slot.NiveauFracture, LSystem_Botanique.IndexChene);
                }
                else if (estRoche)
                {
                    int idRoche = ItemPhysique.IdRocheMatiereMin + idxRoche;
                    AppliquerMaterielObjet(mi, idRoche, idxRoche, 0, 0, essenceBois);
                }
                else if (estBois)
                {
                    mi.MaterialOverride = matBois;
                }
                else
                {
                    // Filet de sécurité atelier 200: toute mesh inconnue hérite du bois crafté.
                    mi.MaterialOverride = matBois;
                }
            }
            foreach (Node c in n.GetChildren())
                Parcourir(c);
        }

        Parcourir(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Table décorative bois : GLB dédié, matériaux distincts demi-bûche / bâtons / ligatures.</summary>
    public static void InstancierModeleTableBoisDecorative(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 1.2f, bool ancrerBaseAuSol = false)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/materials/moblier/table.glb");
        if (scene == null) return;

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        Dictionary<string, string> cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string genome = slot.GenomeAssemblage ?? "";
        if (genome.StartsWith("TABLEDECO147", StringComparison.Ordinal))
        {
            string[] parts = genome.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                int idxEq = p.IndexOf('=');
                if (idxEq <= 0 || idxEq >= p.Length - 1)
                    continue;
                cfg[p.Substring(0, idxEq).Trim()] = p.Substring(idxEq + 1).Trim();
            }
        }

        byte LireByteOu(string key, byte fallback)
        {
            if (cfg.TryGetValue(key, out string raw) && byte.TryParse(raw, out byte v))
                return v;
            return fallback;
        }

        byte essenceDemiBuche = LireByteOu("BF", slot.IndexBotanique);
        byte essenceBaton = LireByteOu("BAT", essenceDemiBuche);
        byte varianteLiage = LireByteOu("LIGV", LSystem_Botanique.IndexChene);
        int ligC = cfg.TryGetValue("LIGC", out string rawC) && int.TryParse(rawC, out int parsedC) ? parsedC : slot.IndexChimique;
        int ligM = cfg.TryGetValue("LIGM", out string rawM) && int.TryParse(rawM, out int parsedM) ? parsedM : slot.IndexMorphologique;

        Material matDemiBuche = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceDemiBuche);
        Material matBaton = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBaton);
        int idLiage = varianteLiage == Joueur.TagVarianteLiane ? 16 : 20;

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nomLower = mi.Name.ToString().ToLowerInvariant();
                bool estDemiBuche = nomLower.Contains("demi") || nomLower.Contains("half") || nomLower.Contains("buche") || nomLower.Contains("log");
                bool estLiage = nomLower.Contains("liage") || nomLower.Contains("ligature") || nomLower.Contains("corde") || nomLower.Contains("rope") || nomLower.Contains("cord") || nomLower.Contains("liane") || nomLower.Contains("ficelle");
                bool estBaton = nomLower.Contains("baton") || nomLower.Contains("stick") || nomLower.Contains("branche") || nomLower.Contains("branch");

                if (estLiage)
                {
                    RemplacerMeshParNormalesFacettes(mi);
                    AppliquerMaterielObjet(mi, idLiage, ligC, ligM, slot.NiveauFracture, varianteLiage);
                }
                else if (estBaton)
                    mi.MaterialOverride = matBaton;
                else if (estDemiBuche)
                    mi.MaterialOverride = matDemiBuche;
                else
                    mi.MaterialOverride = matDemiBuche;
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Table artisanat structures T1 : applique les matériaux des composants H/P/R/T/DB selon le craft.</summary>
    public static void InstancierModeleTableArtisanaTier1(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 1.35f, bool ancrerBaseAuSol = false)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Ateliers/table_artisana_tiere1.glb");
        if (scene == null)
            scene = GD.Load<PackedScene>("res://Modeles/Ateliers/table_analise_tire1.glb");
        if (scene == null) return;

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        Dictionary<string, string> cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string genome = slot.GenomeAssemblage ?? "";
        if (genome.StartsWith("TABLEARTISANA148", StringComparison.Ordinal))
        {
            string[] parts = genome.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                int idxEq = p.IndexOf('=');
                if (idxEq <= 0 || idxEq >= p.Length - 1)
                    continue;
                cfg[p.Substring(0, idxEq).Trim()] = p.Substring(idxEq + 1).Trim();
            }
        }

        byte LireByteOu(string key, byte fallback)
        {
            if (cfg.TryGetValue(key, out string raw) && byte.TryParse(raw, out byte v))
                return v;
            return fallback;
        }
        int LireIntOu(string key, int fallback)
        {
            if (cfg.TryGetValue(key, out string raw) && int.TryParse(raw, out int v))
                return v;
            return fallback;
        }

        byte dbBois = LireByteOu("DB_B", slot.IndexBotanique);
        byte hBois = LireByteOu("H_B", dbBois);
        int hRoche = Mathf.Clamp(LireIntOu("H_R", 0), 0, ItemPhysique.TableGeologique.Length - 1);
        int hLigC = Mathf.Clamp(LireIntOu("H_C", 0), 0, 255);
        int hLigM = Mathf.Clamp(LireIntOu("H_M", hLigC), 0, 255);
        byte pBois = LireByteOu("P_B", dbBois);
        int pRoche = Mathf.Clamp(LireIntOu("P_R", 0), 0, ItemPhysique.TableGeologique.Length - 1);
        int pLigC = Mathf.Clamp(LireIntOu("P_C", 0), 0, 255);
        int pLigM = Mathf.Clamp(LireIntOu("P_M", pLigC), 0, 255);
        int rType = Mathf.Clamp(LireIntOu("R_T", 0), 0, ItemPhysique.TableGeologique.Length - 1);
        byte tBois = LireByteOu("T_B", dbBois);
        int tLigC = Mathf.Clamp(LireIntOu("T_C", slot.IndexChimique), 0, 255);
        int tLigM = Mathf.Clamp(LireIntOu("T_M", slot.IndexMorphologique), 0, 255);

        Material matDb = ArbreVivant.ObtenirMaterielBoisTriplanar(dbBois);
        Material matHBois = ArbreVivant.ObtenirMaterielBoisTriplanar(hBois);
        Material matPBois = ArbreVivant.ObtenirMaterielBoisTriplanar(pBois);
        Material matTBois = ArbreVivant.ObtenirMaterielBoisTriplanar(tBois);

        string ContexteNom(Node n)
        {
            var noms = new List<string>();
            for (Node cur = n; cur != null && cur != modele; cur = cur.GetParent())
                noms.Add(cur.Name.ToString().ToLowerInvariant());
            return string.Join("/", noms);
        }

        void AppliquerMateriauRoche(MeshInstance3D mi, int idxRoche, byte essenceFallback)
        {
            int idRoche = ItemPhysique.IdRocheMatiereMin + Mathf.Clamp(idxRoche, 0, ItemPhysique.TableGeologique.Length - 1);
            AppliquerMaterielObjet(mi, idRoche, Mathf.Clamp(idxRoche, 0, ItemPhysique.TableGeologique.Length - 1), 0, 0, essenceFallback);
        }

        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string contexte = ContexteNom(mi);
                string nomNoeud = mi.Name.ToString().ToLowerInvariant();
                bool estHache = contexte.Contains("hache") || contexte.Contains("hatchette") || contexte.Contains("hatchet");
                bool estPioche = contexte.Contains("pioche") || contexte.Contains("pickaxe") || contexte.Contains("pick");
                bool estTable = contexte.Contains("table");
                bool estDemiBuche = contexte.Contains("demi") || contexte.Contains("buche") || contexte.Contains("half") || contexte.Contains("log");
                bool estRoche = contexte.Contains("roche") || contexte.Contains("rock") || contexte.Contains("stone") || contexte.Contains("pierre");
                bool estLigature = contexte.Contains("liage") || contexte.Contains("ligature") || contexte.Contains("corde") || contexte.Contains("rope") || contexte.Contains("liane") || contexte.Contains("ficelle");
                bool estBaton = contexte.Contains("baton") || contexte.Contains("stick") || contexte.Contains("branche") || contexte.Contains("branch") || contexte.Contains("manche");

                if (estHache)
                {
                    // Le GLB table artisana réutilise les mêmes noms de mesh que la hachette de base.
                    if (nomNoeud.Contains("tripo_part_1"))
                        AppliquerMaterielObjet(mi, 20, hLigC, hLigM, 0, LSystem_Botanique.IndexChene); // corde
                    else if (nomNoeud.Contains("tripo_part_4"))
                        AppliquerMateriauRoche(mi, hRoche, hBois); // tête pierre
                    else if (nomNoeud.Contains("tripo_part_5"))
                        mi.MaterialOverride = matHBois; // manche
                    else if (estRoche)
                        AppliquerMateriauRoche(mi, hRoche, hBois);
                    else if (estLigature)
                        AppliquerMaterielObjet(mi, 20, hLigC, hLigM, 0, LSystem_Botanique.IndexChene);
                    else
                        mi.MaterialOverride = matHBois;
                }
                else if (estPioche)
                {
                    if (nomNoeud.Contains("pierre 1") || nomNoeud.Contains("pierre_1"))
                        AppliquerMateriauRoche(mi, pRoche, pBois);
                    else if (nomNoeud.Contains("pierre 2") || nomNoeud.Contains("pierre_2"))
                        AppliquerMateriauRoche(mi, pRoche, pBois);
                    else if (nomNoeud.Contains("corde"))
                        AppliquerMaterielObjet(mi, 20, pLigC, pLigM, 0, LSystem_Botanique.IndexChene);
                    else if (nomNoeud.Contains("baton"))
                        mi.MaterialOverride = matPBois;
                    else if (estRoche)
                        AppliquerMateriauRoche(mi, pRoche, pBois);
                    else if (estLigature)
                        AppliquerMaterielObjet(mi, 20, pLigC, pLigM, 0, LSystem_Botanique.IndexChene);
                    else
                        mi.MaterialOverride = matPBois;
                }
                else if (estTable)
                {
                    if (estLigature) AppliquerMaterielObjet(mi, 20, tLigC, tLigM, 0, LSystem_Botanique.IndexChene);
                    else mi.MaterialOverride = matTBois;
                }
                else if (estDemiBuche)
                    mi.MaterialOverride = matDb;
                else if (estRoche)
                    AppliquerMateriauRoche(mi, rType, dbBois);
                else if (estBaton)
                    mi.MaterialOverride = matDb;
                else
                    mi.MaterialOverride = matDb;
            }
            foreach (Node c in n.GetChildren())
                Parcourir(c);
        }

        Parcourir(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Table d'analyse tier 1 : GLB dédié + matériaux bois/corde/roche harmonisés selon les tags du slot.</summary>
    public static void InstancierModeleTableAnalyseTier1(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.92f, bool ancrerBaseAuSol = false)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Ateliers/table_analise_tire1.glb");
        if (scene == null)
        {
            // Fallback robuste: si le GLB manque, garder un visuel jouable.
            InstancierModeleAtelierPrimitif(parent, slot, tailleMaxMetres, ancrerBaseAuSol);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        Dictionary<string, string> cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string genome = slot.GenomeAssemblage ?? "";
        if (genome.StartsWith("TABLEANALYSE131", StringComparison.Ordinal))
        {
            string[] parts = genome.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                int idxEq = p.IndexOf('=');
                if (idxEq <= 0 || idxEq >= p.Length - 1)
                    continue;
                cfg[p.Substring(0, idxEq).Trim()] = p.Substring(idxEq + 1).Trim();
            }
        }

        byte LireByteOu(string key, byte fallback)
        {
            if (cfg.TryGetValue(key, out string raw) && byte.TryParse(raw, out byte v))
                return v;
            return fallback;
        }

        int LireIntOu(string key, int fallback)
        {
            if (cfg.TryGetValue(key, out string raw) && int.TryParse(raw, out int v))
                return v;
            return fallback;
        }

        byte essencePlanche = LireByteOu("PLAN", slot.IndexBotanique);
        byte essenceBois1 = LireByteOu("BOIS1", (byte)((essencePlanche + 1) % 5));
        byte essenceBois2 = LireByteOu("BOIS2", (byte)((essencePlanche + 2) % 5));
        byte varianteLiage = LireByteOu("LIGV", LSystem_Botanique.IndexChene);
        int ligC = LireIntOu("LIGC", slot.IndexChimique);
        int ligM = LireIntOu("LIGM", slot.IndexMorphologique);
        byte essenceMortier = LireByteOu("MPM", essencePlanche);
        byte essencePilon = LireByteOu("MPP", essenceBois1);
        int idxRoche1 = Mathf.Clamp(LireIntOu("R1", 0), 0, ItemPhysique.TableGeologique.Length - 1);
        int idxRoche2 = Mathf.Clamp(LireIntOu("R2", 1), 0, ItemPhysique.TableGeologique.Length - 1);
        int idxRoche3 = Mathf.Clamp(LireIntOu("R3", 2), 0, ItemPhysique.TableGeologique.Length - 1);
        string cuirGenome = cfg.TryGetValue("CUIR", out string cuirRaw) ? cuirRaw : "";

        Material matBoisPlanche = ArbreVivant.ObtenirMaterielBoisTriplanar(essencePlanche);
        Material matBois1 = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois1);
        Material matBois2 = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois2);
        Material matMortier = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceMortier);
        Material matPilon = ArbreVivant.ObtenirMaterielBoisTriplanar(essencePilon);
        var matOs = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.88f, 0.78f),
            Roughness = 0.86f,
            Metallic = 0f
        };

        Material matCuir = null;
        if (!string.IsNullOrEmpty(cuirGenome) && cuirGenome.StartsWith("PEAU:", StringComparison.Ordinal))
        {
            string reste = cuirGenome.Length > 5 ? cuirGenome.Substring(5) : "";
            if (reste.Length > 0 && reste != "TAUREAU" && reste != "VACHE" && ResourceLoader.Exists(reste))
            {
                Texture2D texCuir = GD.Load<Texture2D>(reste);
                if (texCuir != null)
                    matCuir = new StandardMaterial3D { AlbedoTexture = texCuir, Roughness = 0.88f, Metallic = 0f };
            }
            if (matCuir == null)
            {
                bool taureau = reste.IndexOf("TAUREAU", StringComparison.OrdinalIgnoreCase) >= 0;
                matCuir = new StandardMaterial3D
                {
                    AlbedoColor = taureau ? new Color(0.34f, 0.21f, 0.13f) : new Color(0.44f, 0.36f, 0.28f),
                    Roughness = 0.9f,
                    Metallic = 0f
                };
            }
        }
        if (matCuir == null)
            matCuir = new StandardMaterial3D { AlbedoColor = new Color(0.44f, 0.36f, 0.28f), Roughness = 0.9f, Metallic = 0f };

        int compteurRocheFallback = 0;
        void AppliquerRocheAleatoire(MeshInstance3D mi, int numero)
        {
            RemplacerMeshParNormalesFacettes(mi);
            int idx = numero switch
            {
                1 => idxRoche1,
                2 => idxRoche2,
                3 => idxRoche3,
                _ => (compteurRocheFallback++ % 3) switch { 0 => idxRoche1, 1 => idxRoche2, _ => idxRoche3 }
            };
            int idRoche = ItemPhysique.IdRocheMatiereMin + idx;
            AppliquerMaterielObjet(mi, idRoche, idx, 0, 0, essencePlanche);
        }

        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nom = mi.Name.ToString().ToLowerInvariant();
                bool estBois1 = nom.Contains("bois1") || nom.Contains("bois_1") || nom.Contains("wood1") || nom.Contains("wood_1");
                bool estBois2 = nom.Contains("bois2") || nom.Contains("bois_2") || nom.Contains("wood2") || nom.Contains("wood_2");
                bool estPlanchePrincipale = nom.Contains("planch") || nom.Contains("plank") || nom.Contains("principal");
                bool estLiage = nom.Contains("liage") || nom.Contains("ligature") || nom.Contains("corde") || nom.Contains("rope") || nom.Contains("cord") || nom.Contains("lian") || nom.Contains("ficelle");
                bool estMortier = nom.Contains("mortier") || nom.Contains("mortar");
                bool estPilon = nom.Contains("pilon") || nom.Contains("pestle") || nom.Contains("club");
                bool estCuir = nom.Contains("cuir") || nom.Contains("leather");
                bool estRoche1 = nom.Contains("roche1") || nom.Contains("roche_1") || nom.Contains("rock1") || nom.Contains("rock_1");
                bool estRoche2 = nom.Contains("roche2") || nom.Contains("roche_2") || nom.Contains("rock2") || nom.Contains("rock_2");
                bool estRoche3 = nom.Contains("roche3") || nom.Contains("roche_3") || nom.Contains("rock3") || nom.Contains("rock_3");
                bool estOs = nom.Contains("bone") || nom.StartsWith("os") || nom.Contains("_os") || nom.Contains("os_");
                bool estRocheGenerique = nom.Contains("roche") || nom.Contains("stone") || nom.Contains("rock");

                if (estCuir)
                    mi.MaterialOverride = matCuir;
                else if (estMortier)
                    mi.MaterialOverride = matMortier;
                else if (estPilon)
                    mi.MaterialOverride = matPilon;
                else if (estLiage)
                    AppliquerMaterielObjet(mi, 20, ligC, ligM, 0, varianteLiage);
                else if (estBois1)
                    mi.MaterialOverride = matBois1;
                else if (estBois2)
                    mi.MaterialOverride = matBois2;
                else if (estPlanchePrincipale)
                    mi.MaterialOverride = matBoisPlanche;
                else if (estRoche1)
                    AppliquerRocheAleatoire(mi, 1);
                else if (estRoche2)
                    AppliquerRocheAleatoire(mi, 2);
                else if (estRoche3)
                    AppliquerRocheAleatoire(mi, 3);
                else if (estOs)
                    mi.MaterialOverride = matOs;
                else if (estRocheGenerique)
                    AppliquerRocheAleatoire(mi, 0);
                else
                    mi.MaterialOverride = matBoisPlanche;
            }
            foreach (Node c in n.GetChildren())
                Parcourir(c);
        }

        Parcourir(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Rack à bâtons : GLB dédié (textures de modèle conservées), fallback atelier si le fichier est absent.</summary>
    public static void InstancierModeleRackBatons(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.9f, bool ancrerBaseAuSol = false)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Storage/Rack_Batons_Tier0.glb");
        if (scene == null)
        {
            InstancierModeleAtelierPrimitif(parent, slot, tailleMaxMetres, ancrerBaseAuSol);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        byte essenceBois = slot.IndexBotanique;
        byte varianteLigature = LSystem_Botanique.IndexChene;
        if (!string.IsNullOrEmpty(slot.GenomeAssemblage) && slot.GenomeAssemblage.StartsWith("RACKL:"))
        {
            string raw = slot.GenomeAssemblage.Substring("RACKL:".Length);
            if (byte.TryParse(raw, out byte tag))
                varianteLigature = tag;
        }
        else
        {
            // Compatibilité anciens racks: le tag ligature était stocké dans IndexBotanique.
            if (slot.IndexBotanique == Joueur.TagVarianteLiane || slot.IndexBotanique == Joueur.TagVarianteHerbeSolide || slot.IndexBotanique == Joueur.TagVarianteIntestin || slot.IndexBotanique == Joueur.TagVarianteIntestinSolide)
                varianteLigature = slot.IndexBotanique;
        }
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;

        // Bois : triplanar selon l’essence du craft ; ligatures : corde/liane du craft.
        int nbMeshesRack = 0;
        void ParcourirMeshesRack(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                nbMeshesRack++;
                string nom = mi.Name.ToString().ToLowerInvariant();
                bool estLigature = nom.Contains("cord")
                    || nom.Contains("rope")
                    || nom.Contains("ligature")
                    || nom.Contains("corde")
                    || nom.Contains("liane")
                    || nom.Contains("ficelle");
                if (estLigature)
                {
                    RemplacerMeshParNormalesFacettes(mi);
                    int idLigature = varianteLigature == Joueur.TagVarianteLiane ? 16 : 20;
                    AppliquerMaterielObjet(mi, idLigature, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, varianteLigature);
                }
                else
                {
                    // Toujours appliquer l’essence du craft : le GLB peut avoir un StandardMaterial blanc par défaut.
                    mi.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
                }
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshesRack(c);
        }

        ParcourirMeshesRack(modele);
        if (nbMeshesRack == 0)
        {
            // Fallback dur: un rack primitif visible, pour éviter tout cas "invisible".
            var bois = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
            var lig = Atlas_Matiere.ObtenirMaterielCorde(slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture);

            MeshInstance3D Montant(Vector3 p, float h)
                => new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.042f, Height = h }, Position = p + new Vector3(0, h * 0.5f, 0), MaterialOverride = bois };
            MeshInstance3D Barre(Vector3 p, float l)
                => new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.028f, BottomRadius = 0.03f, Height = l }, Position = p, RotationDegrees = new Vector3(0, 0, 90), MaterialOverride = bois };
            MeshInstance3D Ligature(Vector3 p)
                => new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.042f, OuterRadius = 0.067f }, Position = p, RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = lig };

            float h = 0.74f;
            float z1 = -0.22f, z2 = 0.22f, x = 0.21f;
            modele.AddChild(Montant(new Vector3(-x, 0, z1), h));
            modele.AddChild(Montant(new Vector3(-x, 0, z2), h));
            modele.AddChild(Montant(new Vector3(x, 0, z1), h));
            modele.AddChild(Montant(new Vector3(x, 0, z2), h));
            modele.AddChild(Barre(new Vector3(0, h * 0.95f, z1), 0.46f));
            modele.AddChild(Barre(new Vector3(0, h * 0.95f, z2), 0.46f));
            modele.AddChild(Ligature(new Vector3(-x, h * 0.95f, z1)));
            modele.AddChild(Ligature(new Vector3(-x, h * 0.95f, z2)));
            modele.AddChild(Ligature(new Vector3(x, h * 0.95f, z1)));
            modele.AddChild(Ligature(new Vector3(x, h * 0.95f, z2)));
        }
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Rack à bûches : GLB dédié, même logique ligatures que rack à bâtons.</summary>
    public static void InstancierModeleRackBuches(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.95f, bool ancrerBaseAuSol = false)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Storage/Rack_Buche_Tiere0.glb");
        if (scene == null)
        {
            InstancierModeleRackBatons(parent, slot, tailleMaxMetres, ancrerBaseAuSol);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        byte essenceBois = slot.IndexBotanique;
        byte varianteLigature = LSystem_Botanique.IndexChene;
        string genome = slot.GenomeAssemblage ?? "";
        if (genome.StartsWith("RACKBL:"))
        {
            string raw = genome.Substring("RACKBL:".Length);
            if (byte.TryParse(raw, out byte tag))
                varianteLigature = tag;
        }
        else if (genome.StartsWith("RACKL:"))
        {
            string raw = genome.Substring("RACKL:".Length);
            if (byte.TryParse(raw, out byte tag))
                varianteLigature = tag;
        }
        else if (slot.IndexBotanique == Joueur.TagVarianteLiane || slot.IndexBotanique == Joueur.TagVarianteHerbeSolide || slot.IndexBotanique == Joueur.TagVarianteIntestin || slot.IndexBotanique == Joueur.TagVarianteIntestinSolide)
        {
            varianteLigature = slot.IndexBotanique;
            essenceBois = LSystem_Botanique.IndexChene;
        }

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nom = mi.Name.ToString().ToLowerInvariant();
                bool estLigature = nom.Contains("cord")
                    || nom.Contains("rope")
                    || nom.Contains("ligature")
                    || nom.Contains("corde")
                    || nom.Contains("liane")
                    || nom.Contains("ficelle");
                if (estLigature)
                {
                    RemplacerMeshParNormalesFacettes(mi);
                    int idLigature = varianteLigature == Joueur.TagVarianteLiane ? 16 : 20;
                    AppliquerMaterielObjet(mi, idLigature, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, varianteLigature);
                }
                else
                    mi.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Carnet du savoir : GLB <c>Modeles/Equipable/Carnet_Du_Savoir.glb</c> ; repli procédural si absent.</summary>
    public static void InstancierModeleCarnetSavoir(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.42f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/Equipable/Carnet_Du_Savoir.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        Node3D modele;

        if (scene != null)
        {
            Node racine = scene.Instantiate();
            if (racine is Node3D nd)
                modele = nd;
            else
            {
                modele = new Node3D();
                modele.AddChild(racine);
            }
            modele.Name = "ModeleArme";

            // GLB Tripo : nœuds « papier » / « cuir » (suffixes éditeur possibles).
            var matPapier = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.97f, 0.97f, 0.98f),
                Roughness = 0.95f,
                Metallic = 0f
            };
            var matCuir = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.36f, 0.22f, 0.13f),
                Roughness = 0.78f,
                Metallic = 0.04f
            };

            void AppliquerMateriauxCarnetGlb(Node n)
            {
                if (n is MeshInstance3D mi)
                {
                    string nom = mi.Name.ToString().ToLowerInvariant();
                    if (nom.Contains("papier"))
                        mi.MaterialOverride = matPapier;
                    else if (nom.Contains("cuir"))
                        mi.MaterialOverride = matCuir;
                }
                foreach (Node c in n.GetChildren())
                    AppliquerMateriauxCarnetGlb(c);
            }

            AppliquerMateriauxCarnetGlb(modele);
        }
        else
        {
            modele = new Node3D { Name = "ModeleArme" };

            var couverture = new MeshInstance3D
            {
                Name = "Couverture",
                Mesh = new BoxMesh { Size = new Vector3(0.34f, 0.045f, 0.46f) },
                Position = new Vector3(0f, 0.028f, 0f)
            };
            couverture.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.34f, 0.21f, 0.12f),
                Roughness = 0.82f,
                Metallic = 0.02f
            };
            modele.AddChild(couverture);

            var pages = new MeshInstance3D
            {
                Name = "Pages",
                Mesh = new BoxMesh { Size = new Vector3(0.30f, 0.032f, 0.42f) },
                Position = new Vector3(0f, 0.03f, 0f)
            };
            pages.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.98f, 0.98f, 0.99f),
                Roughness = 0.96f,
                Metallic = 0f
            };
            modele.AddChild(pages);

            var tranche = new MeshInstance3D
            {
                Name = "Tranche",
                Mesh = new BoxMesh { Size = new Vector3(0.02f, 0.046f, 0.46f) },
                Position = new Vector3(-0.16f, 0.028f, 0f)
            };
            tranche.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.24f, 0.16f, 0.11f),
                Roughness = 0.78f
            };
            modele.AddChild(tranche);
        }

        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Coffre en bois tier0 : GLB + matériau bois selon l’essence du craft.</summary>
    public static void InstancierModeleCoffreBoisTier0(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.82f, bool ancrerBaseAuSol = true)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Storage/Coffre_boie_tier0.glb");
        byte essenceFallback = (byte)Mathf.Clamp((int)slot.IndexBotanique, 0, 4);
        Material matBoisCoffre = ObtenirMaterielBoisPorteCoffre(essenceFallback);
        if (scene == null)
        {
            var fb = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.52f, 0.36f, 0.4f) },
                MaterialOverride = matBoisCoffre
            };
            parent.AddChild(fb);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        byte botaniqueCraft = slot.IndexBotanique;
        byte essenceBois = botaniqueCraft;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        matBoisCoffre = ObtenirMaterielBoisPorteCoffre(essenceBois);

        byte varianteLigature = (botaniqueCraft == Joueur.TagVarianteLiane || botaniqueCraft == Joueur.TagVarianteHerbeSolide || botaniqueCraft == Joueur.TagVarianteIntestin || botaniqueCraft == Joueur.TagVarianteIntestinSolide)
            ? botaniqueCraft
            : LSystem_Botanique.IndexChene;

        void ParcourirMeshesCoffre(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nom = mi.Name.ToString().ToLowerInvariant();
                bool estLigature = nom.Contains("corde")
                    || nom.Contains("cord")
                    || nom.Contains("rope")
                    || nom.Contains("ligature")
                    || nom.Contains("liane")
                    || nom.Contains("ficelle");
                bool estBranche = nom.Contains("branche")
                    || nom.Contains("baton")
                    || nom.Contains("stick")
                    || nom.Contains("shaft");
                if (estLigature)
                {
                    RemplacerMeshParNormalesFacettes(mi);
                    int idLigature = varianteLigature == Joueur.TagVarianteLiane ? 16 : 20;
                    AppliquerMaterielObjet(mi, idLigature, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, varianteLigature);
                }
                else if (estBranche)
                {
                    mi.MaterialOverride = matBoisCoffre;
                }
                else
                    mi.MaterialOverride = matBoisCoffre;
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshesCoffre(c);
        }

        ParcourirMeshesCoffre(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Pit à feu : GLB de survie, recoloré selon l'essence de bois du craft.</summary>
    public static void InstancierModelePitFeu(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.9f, bool ancrerBaseAuSol = true)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/survie/Pit_a_feu.glb");
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        if (scene == null)
        {
            var fb = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.86f, 0.24f, 0.86f) },
                MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar((byte)Mathf.Clamp((int)essenceBois, 0, 4))
            };
            parent.AddChild(fb);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        Material matBoisPit = ObtenirMaterielBoisPitFeu(essenceBois);
        void ParcourirMeshesPit(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                mi.MaterialOverride = matBoisPit;
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshesPit(c);
        }

        ParcourirMeshesPit(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Pit à feu roche : pit bois central + roches teintes aléatoires stables.</summary>
    public static void InstancierModelePitFeuRoche(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.95f, bool ancrerBaseAuSol = true)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/survie/Pit_feu_roche.glb");
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        if (scene == null)
        {
            InstancierModelePitFeu(parent, slot, tailleMaxMetres, ancrerBaseAuSol);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        var rng = new RandomNumberGenerator();
        rng.Seed = unchecked((ulong)(uint)HashCode.Combine(slot.ID, slot.IndexBotanique, slot.IndexChimique, slot.IndexMorphologique));
        int idxRocheCourant = rng.RandiRange(0, ItemPhysique.TableGeologique.Length - 1);
        Material matBoisPit = ObtenirMaterielBoisPitFeu(essenceBois);

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nom = mi.Name.ToString().ToLowerInvariant();
                bool estRoche = nom.Contains("rock")
                    || nom.Contains("roche")
                    || nom.Contains("stone")
                    || nom.Contains("caill");
                if (estRoche)
                {
                    idxRocheCourant = (idxRocheCourant + 3) % ItemPhysique.TableGeologique.Length;
                    int idRoche = ItemPhysique.IdRocheMatiereMin + idxRocheCourant;
                    AppliquerMaterielObjet(mi, idRoche, idxRocheCourant, 0, 0, slot.IndexBotanique);
                }
                else
                {
                    mi.MaterialOverride = matBoisPit;
                }
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    private static Material ObtenirMaterielBoisPitFeu(byte essenceBois)
    {
        var src = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois) as StandardMaterial3D;
        if (src == null)
            return ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);

        var mat = (StandardMaterial3D)src.Duplicate(true);
        mat.NormalEnabled = false;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        mat.Roughness = 0.92f;
        mat.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
        Color baseC = mat.AlbedoColor;
        const float boostLuminosite = 1.08f;
        mat.AlbedoColor = new Color(
            Mathf.Min(baseC.R * boostLuminosite, 1.2f),
            Mathf.Min(baseC.G * boostLuminosite, 1.2f),
            Mathf.Min(baseC.B * boostLuminosite, 1.2f),
            baseC.A);
        return mat;
    }
}
