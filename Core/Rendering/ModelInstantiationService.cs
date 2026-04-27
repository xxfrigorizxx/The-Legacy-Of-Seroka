using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    /// <summary>True si l'objet a un mesh à afficher en main / preview.</summary>
    private static bool EstObjetAvecVisuel(int id)
    {
        if (id >= 1 && id <= 9) return true;
        return ItemPhysique.EstIdRocheMatiere(id) || id == 10 || id == 11 || id == BlocChutant.ID_BRANCHE || id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == Joueur.IdObjetCeinturePoches || id == Joueur.IdObjetCeintureSacoches || id == Joueur.IdObjetPochetteTier0 || id == Joueur.IdObjetSacTier0 || id == Joueur.IdObjetCarnetSavoir || id == Joueur.IdObjetSteakCru || id == Joueur.IdObjetSteakCuit || id == Joueur.IdObjetOsBoeuf || id == Joueur.IdObjetCuirBoeuf || id == Joueur.IdObjetIntestinBoeuf || id == Joueur.IdObjetIntestinBoeufNettoye || id == 30 || id == 32 || id == 34 || id == Joueur.IdObjetBaie || id == 100 || id == 105 || id == 106 || id == Joueur.IdObjetPellePierreTier0 || id == Joueur.IdObjetPiochePierreTier0 || id == Joueur.IdObjetLancePierreTier0 || id == Joueur.IdObjetFauxPierreTier0 || id == 200 || id == Joueur.IdObjetRackBatons || id == Joueur.IdObjetRackBuches || id == Joueur.IdObjetCoffreBoisTier0 || id == Joueur.IdObjetPitFeu || id == Joueur.IdObjetPitFeuRoche || id == Joueur.IdObjetAllumeFeu;
    }

    public static void NettoyerModelesEnfants(Node3D parent)
    {
        if (parent == null) return;
        Godot.Collections.Array<Node> enfants = parent.GetChildren();
        for (int i = enfants.Count - 1; i >= 0; i--)
        {
            Node n = enfants[i];
            if (n.Name.ToString().Contains("ModeleArme"))
                n.Free();
        }
        if (parent.HasMeta(MetaSignatureCorde20))
            parent.RemoveMeta(MetaSignatureCorde20);
        if (parent.HasMeta(MetaSignatureTissu21))
            parent.RemoveMeta(MetaSignatureTissu21);
        if (parent.HasMeta(MetaSignatureCeinture102))
            parent.RemoveMeta(MetaSignatureCeinture102);
        if (parent.HasMeta(MetaSignatureCeinture104))
            parent.RemoveMeta(MetaSignatureCeinture104);
        if (parent.HasMeta(MetaSignaturePochette103))
            parent.RemoveMeta(MetaSignaturePochette103);
        if (parent.HasMeta(MetaSignatureSac101))
            parent.RemoveMeta(MetaSignatureSac101);
        if (parent.HasMeta(MetaSignatureRack109))
            parent.RemoveMeta(MetaSignatureRack109);
        if (parent.HasMeta(MetaSignaturePelle107))
            parent.RemoveMeta(MetaSignaturePelle107);
        if (parent.HasMeta(MetaSignaturePioche108))
            parent.RemoveMeta(MetaSignaturePioche108);
        if (parent.HasMeta(MetaSignatureLance111))
            parent.RemoveMeta(MetaSignatureLance111);
        if (parent.HasMeta(MetaSignatureFaux112))
            parent.RemoveMeta(MetaSignatureFaux112);
        if (parent.HasMeta(MetaSignatureCarnet114))
            parent.RemoveMeta(MetaSignatureCarnet114);
        if (parent.HasMeta(MetaSignatureLootCuir117))
            parent.RemoveMeta(MetaSignatureLootCuir117);
        if (parent.HasMeta(MetaSignatureAllumeFeu121))
            parent.RemoveMeta(MetaSignatureAllumeFeu121);
    }

    private static Aabb TransformerAabb(Transform3D t, Aabb a)
    {
        Vector3 p = a.Position;
        Vector3 s = a.Size;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) != 0 ? p.X + s.X : p.X,
                (i & 2) != 0 ? p.Y + s.Y : p.Y,
                (i & 4) != 0 ? p.Z + s.Z : p.Z);
            Vector3 w = t * corner;
            min.X = Mathf.Min(min.X, w.X); min.Y = Mathf.Min(min.Y, w.Y); min.Z = Mathf.Min(min.Z, w.Z);
            max.X = Mathf.Max(max.X, w.X); max.Y = Mathf.Max(max.Y, w.Y); max.Z = Mathf.Max(max.Z, w.Z);
        }
        return new Aabb(min, max - min);
    }

    private static void AccumulerAabbMeshes(Node3D n, Transform3D parentVersRacine, ref Aabb? combine)
    {
        Transform3D racineVersNoeud = parentVersRacine * n.Transform;
        if (n is MeshInstance3D mi && mi.Mesh != null)
        {
            Aabb b = TransformerAabb(racineVersNoeud, mi.Mesh.GetAabb());
            combine = combine.HasValue ? combine.Value.Merge(b) : b;
        }
        foreach (Node ch in n.GetChildren())
        {
            if (ch is Node3D c3)
                AccumulerAabbMeshes(c3, racineVersNoeud, ref combine);
            else
                AccumulerAabbSousNoeudsSansTransform3D(ch, racineVersNoeud, ref combine);
        }
    }

    /// <summary>Certains GLB insèrent des <see cref="Node"/> sans transform ; les meshes descendants doivent quand même être pris en compte pour l’AABB.</summary>
    private static void AccumulerAabbSousNoeudsSansTransform3D(Node n, Transform3D parentVersRacine, ref Aabb? combine)
    {
        foreach (Node ch in n.GetChildren())
        {
            if (ch is Node3D c3)
                AccumulerAabbMeshes(c3, parentVersRacine, ref combine);
            else
                AccumulerAabbSousNoeudsSansTransform3D(ch, parentVersRacine, ref combine);
        }
    }

    /// <summary>Réduit le GLB (souvent en unités Tripo) pour la caméra / le SubViewport, et centre le pivot sur la géométrie.</summary>
    public static void NormaliserEchelleEtCentrerModeleArme(Node3D modeleRacine, float tailleMaxDimension)
    {
        if (modeleRacine == null) return;
        Aabb? combine = null;
        AccumulerAabbMeshes(modeleRacine, Transform3D.Identity, ref combine);
        if (!combine.HasValue) return;
        Aabb box = combine.Value;
        float m = Mathf.Max(box.Size.X, Mathf.Max(box.Size.Y, box.Size.Z));
        if (m < 1e-8f) return;
        float s = tailleMaxDimension / m;
        Vector3 centre = box.GetCenter();
        modeleRacine.Scale = modeleRacine.Scale * s;
        modeleRacine.Position = -centre * s;
    }

    /// <summary>Comme les armes mais ancre le bas du mesh sur Y=0 (pivot sol) et centre en X/Z — évite la table qui flotte.</summary>
    public static void NormaliserEchelleTableAtelierAuSol(Node3D modeleRacine, float tailleMaxDimension)
    {
        if (modeleRacine == null) return;
        Aabb? combine = null;
        AccumulerAabbMeshes(modeleRacine, Transform3D.Identity, ref combine);
        if (!combine.HasValue) return;
        Aabb box = combine.Value;
        float m = Mathf.Max(box.Size.X, Mathf.Max(box.Size.Y, box.Size.Z));
        if (m < 1e-8f) return;
        float s = tailleMaxDimension / m;
        modeleRacine.Scale = modeleRacine.Scale * s;
        // Conserver la translation d’origine du GLB : sinon le min Y est calculé avec l’ancienne Position
        // mais on l’écrase, ce qui remonte le mesh (table qui flotte).
        Vector3 posAvant = modeleRacine.Position;
        combine = null;
        AccumulerAabbMeshes(modeleRacine, Transform3D.Identity, ref combine);
        if (!combine.HasValue) return;
        Aabb apres = combine.Value;
        Vector3 centre = apres.GetCenter();
        modeleRacine.Position = new Vector3(
            posAvant.X - centre.X,
            posAvant.Y - apres.Position.Y,
            posAvant.Z - centre.Z);
    }

    private static int SignatureSlotDague105(SlotInventaire s)
    {
        if (s.ID != 105) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.IndexTaille, s.IndexTailleLameRoche, s.NiveauFracture);
    }

    private static int SignatureSlotHachette106(SlotInventaire s)
    {
        if (s.ID != 106) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.IndexTaille, s.IndexBotanique, s.NiveauFracture);
    }

    private static int SignatureSlotPelle107(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetPellePierreTier0) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.IndexTaille, s.IndexBotanique, s.NiveauFracture);
    }

    private static int SignatureSlotPioche108(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetPiochePierreTier0) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.IndexTaille, s.IndexBotanique, s.NiveauFracture, s.GenomeAssemblage ?? "");
    }

    private static int SignatureSlotLance111(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetLancePierreTier0) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.IndexTaille, s.IndexBotanique, s.NiveauFracture);
    }

    private static int SignatureSlotFaux112(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetFauxPierreTier0) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.IndexTaille, s.IndexTailleLameRoche, s.IndexBotanique, s.NiveauFracture);
    }

    private static int SignatureSlotAtelier200(SlotInventaire s)
    {
        if (s.ID != 200) return -1;
        return HashCode.Combine(s.IndexBotanique, s.IndexChimique, s.IndexMorphologique);
    }

    private static int SignatureSlotRack109(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetRackBatons && s.ID != Joueur.IdObjetRackBuches) return -1;
        return HashCode.Combine(s.ID, s.IndexBotanique, s.IndexChimique, s.IndexMorphologique, s.CleConteneur ?? "", s.GenomeAssemblage ?? "");
    }

    private static int SignatureSlotCoffre113(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetCoffreBoisTier0) return -1;
        return HashCode.Combine(s.IndexBotanique, s.IndexChimique, s.IndexMorphologique, s.CleConteneur ?? "");
    }

    private static int SignatureSlotCorde20(SlotInventaire s)
    {
        if (s.ID != 20) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture, s.IndexBotanique);
    }

    private static int SignatureSlotTissu21(SlotInventaire s)
    {
        if (s.ID != 21) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture, s.IndexBotanique);
    }

    private static int SignatureSlotCeinture102(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetCeinturePoches) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture, s.IndexBotanique);
    }

    private static int SignatureSlotCeinture104(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetCeintureSacoches) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture, s.CleConteneur ?? "", s.GenomeAssemblage ?? "", s.IndexBotanique);
    }

    private static int SignatureSlotPochette103(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetPochetteTier0) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture, s.IndexBotanique);
    }

    private static int SignatureSlotSac101(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetSacTier0) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture, s.CleConteneur ?? "", s.IndexBotanique);
    }

    private static int SignatureSlotCarnet114(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetCarnetSavoir) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.IndexTaille, s.NiveauFracture, s.IndexBotanique);
    }

    private static Material ObtenirMaterielPochetteCeinture(SlotInventaire ceinture, byte tagPochette)
    {
        if (tagPochette == Joueur.TagVarianteLiane)
            return Atlas_Matiere.ObtenirMaterielCorde(16, 16, 0);
        if (tagPochette == Joueur.TagVarianteHerbeSolide)
            return Atlas_Matiere.ObtenirMaterielCorde(15, 15, 2);
        if (tagPochette == Joueur.TagVarianteIntestin)
            return Atlas_Matiere.ObtenirMaterielCorde(17, 17, 0);
        if (tagPochette == Joueur.TagVarianteIntestinSolide)
            return Atlas_Matiere.ObtenirMaterielCorde(17, 17, 2);
        return Atlas_Matiere.ObtenirMaterielCorde(ceinture.IndexChimique, ceinture.IndexMorphologique, ceinture.NiveauFracture);
    }

    /// <summary>Échelle de la lame dague : référence = roche moyenne (index 2) ; plus grosse roche en pointe → lame un peu plus massive.</summary>
    private static float ObtenirFacteurEchelleLameDague(SlotInventaire slot)
    {
        if (slot.ID != 105 && slot.ID != Joueur.IdObjetFauxPierreTier0) return 1f;
        int t = slot.IndexTailleLameRoche <= 0 ? 2 : Mathf.Clamp(slot.IndexTailleLameRoche, 0, 4);
        return 1f + (t - 2) * 0.065f;
    }

    /// <summary>Cherche un <see cref="MeshInstance3D"/> dont le nom contient <paramref name="sousChaine"/> (suffixes d’import Godot).</summary>
    public static MeshInstance3D TrouverMeshInstanceDontLeNomContient(Node racine, string sousChaine)
    {
        if (racine == null || string.IsNullOrEmpty(sousChaine)) return null;
        var pile = new List<Node> { racine };
        for (int i = 0; i < pile.Count; i++)
        {
            foreach (Node c in pile[i].GetChildren())
            {
                if (c is MeshInstance3D mi && c.Name.ToString().Contains(sousChaine))
                    return mi;
                pile.Add(c);
            }
        }
        return null;
    }

    private static List<MeshInstance3D> ListerMeshes(Node racine)
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

    private static MeshInstance3D TrouverMeshParMots(Node racine, params string[] mots)
    {
        if (racine == null || mots == null || mots.Length == 0) return null;
        var meshes = ListerMeshes(racine);
        foreach (var mi in meshes)
        {
            string n = mi.Name.ToString().ToLowerInvariant();
            foreach (string mot in mots)
            {
                if (!string.IsNullOrEmpty(mot) && n.Contains(mot.ToLowerInvariant()))
                    return mi;
            }
        }
        return null;
    }

    /// <summary>
    /// Recrée le mesh avec une normale par triangle (shading « facette »). Les GLB de corde/tissu arrivent souvent
    /// avec des normales lissées : N·L est quasi constant et le tressage disparaît visuellement malgré la géométrie.
    /// </summary>
    private static Mesh ForcerMeshNormalesParFacette(Mesh source)
    {
        if (source == null || source.GetSurfaceCount() == 0) return null;
        var output = new ArrayMesh();
        for (int surf = 0; surf < source.GetSurfaceCount(); surf++)
        {
            Godot.Collections.Array arrays = source.SurfaceGetArrays(surf);
            Variant vertVar = arrays[(int)Mesh.ArrayType.Vertex];
            if (vertVar.VariantType == Variant.Type.Nil) continue;
            Vector3[] verts = vertVar.AsVector3Array();
            if (verts == null || verts.Length < 3) continue;

            Vector2[] uvs = null;
            Variant uvVar = arrays[(int)Mesh.ArrayType.TexUV];
            if (uvVar.VariantType != Variant.Type.Nil)
                uvs = uvVar.AsVector2Array();

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            bool ajoute = false;

            void PousserTri(Vector3 a, Vector3 b, Vector3 c, Vector2? uva, Vector2? uvb, Vector2? uvc)
            {
                Vector3 n = (b - a).Cross(c - a);
                if (n.LengthSquared() < 1e-16f) return;
                n = n.Normalized();
                if (uva.HasValue && uvb.HasValue && uvc.HasValue)
                {
                    st.SetNormal(n); st.SetUV(uva.Value); st.AddVertex(a);
                    st.SetNormal(n); st.SetUV(uvb.Value); st.AddVertex(b);
                    st.SetNormal(n); st.SetUV(uvc.Value); st.AddVertex(c);
                }
                else
                {
                    st.SetNormal(n); st.AddVertex(a);
                    st.SetNormal(n); st.AddVertex(b);
                    st.SetNormal(n); st.AddVertex(c);
                }
                ajoute = true;
            }

            Variant idxVar = arrays[(int)Mesh.ArrayType.Index];
            if (idxVar.VariantType != Variant.Type.Nil)
            {
                int[] idx = idxVar.AsInt32Array();
                if (idx != null && idx.Length >= 3)
                {
                    for (int i = 0; i + 2 < idx.Length; i += 3)
                    {
                        int ia = idx[i], ib = idx[i + 1], ic = idx[i + 2];
                        if ((uint)ia >= (uint)verts.Length || (uint)ib >= (uint)verts.Length || (uint)ic >= (uint)verts.Length)
                            continue;
                        Vector3 a = verts[ia], b = verts[ib], c = verts[ic];
                        if (uvs != null && ia < uvs.Length && ib < uvs.Length && ic < uvs.Length)
                            PousserTri(a, b, c, uvs[ia], uvs[ib], uvs[ic]);
                        else
                            PousserTri(a, b, c, null, null, null);
                    }
                }
            }
            else
            {
                for (int i = 0; i + 2 < verts.Length; i += 3)
                {
                    Vector3 a = verts[i], b = verts[i + 1], c = verts[i + 2];
                    if (uvs != null && i + 2 < uvs.Length)
                        PousserTri(a, b, c, uvs[i], uvs[i + 1], uvs[i + 2]);
                    else
                        PousserTri(a, b, c, null, null, null);
                }
            }

            if (ajoute)
                st.Commit(output);
        }
        return output.GetSurfaceCount() > 0 ? output : null;
    }

    private static void RemplacerMeshParNormalesFacettes(MeshInstance3D mi)
    {
        if (mi?.Mesh == null) return;
        Mesh plat = ForcerMeshNormalesParFacette(mi.Mesh);
        if (plat != null)
            mi.Mesh = plat;
    }

    /// <param name="tailleMaxMetres">Hors main : ~1,1 m pour une table lisible au sol.</param>
    /// <param name="ancrerBaseAuSol">True une fois posée : base du mesh sur Y=0 sous le RigidBody.</param>
    public static void InstancierModeleAtelierPrimitif(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.88f, bool ancrerBaseAuSol = false)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Ateliers/table_de_Craft_tiere_0.glb");
        if (scene == null) return;

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        var rng = new RandomNumberGenerator();
        rng.Seed = unchecked((ulong)(uint)HashCode.Combine(slot.IndexBotanique, slot.IndexChimique, slot.IndexMorphologique, 200));

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
                    mi.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(slot.IndexBotanique);
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
        if (scene == null)
        {
            var fb = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.52f, 0.36f, 0.4f) },
                MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar((byte)Mathf.Clamp((int)slot.IndexBotanique, 0, 4))
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
                    RemplacerMeshParNormalesFacettes(mi);
                    AppliquerMaterielObjet(mi, 32, 0, 0, 0, essenceBois);
                }
                else
                    mi.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
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
        void ParcourirMeshesPit(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                RemplacerMeshParNormalesFacettes(mi);
                mi.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
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

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                RemplacerMeshParNormalesFacettes(mi);
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
                    mi.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
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

    /// <summary>Allume-feu préhistorique : modèle GLB avec matériau dépendant de la roche sulfureuse (marcassite/pyrite).</summary>
    public static void InstancierModeleAllumeFeu(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.42f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/Equipements/alume_feu_preistorique.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        int idxSulfure = Mathf.Clamp(slot.IndexChimique, ItemPhysique.IndexChimiqueSilex, ItemPhysique.TableGeologique.Length - 1);
        if (idxSulfure != 10 && idxSulfure != 11)
            idxSulfure = 10;
        Material matSilex = ItemPhysique.CreerMaterielProcedural(true, ItemPhysique.IndexChimiqueSilex);
        Material matSulfure = ItemPhysique.CreerMaterielProcedural(false, idxSulfure);
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.05f, 0.08f) },
                MaterialOverride = matSulfure
            };
            parent.AddChild(fallback);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        int meshIndex = 0;
        void ParcourirMeshesAllumeFeu(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nom = mi.Name.ToString().ToLowerInvariant();
                bool estSilex = nom.Contains("silex") || nom.Contains("flint");
                bool estSulfure = nom.Contains("pyrit") || nom.Contains("marcas") || nom.Contains("sulf");
                if (estSilex)
                    mi.MaterialOverride = matSilex;
                else if (estSulfure)
                    mi.MaterialOverride = matSulfure;
                else
                    mi.MaterialOverride = (meshIndex++ % 2 == 0) ? matSulfure : matSilex;
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshesAllumeFeu(c);
        }

        ParcourirMeshesAllumeFeu(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

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
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
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

    public static void InstancierModeleArme(Node3D parent, SlotInventaire slot, float tailleMaxUnites = 0.525f, float facteurEchelleLame = 1f)
    {
        NettoyerModelesEnfants(parent);
        if (slot.ID != 105 && slot.ID != 106 && slot.ID != Joueur.IdObjetPellePierreTier0 && slot.ID != Joueur.IdObjetPiochePierreTier0 && slot.ID != Joueur.IdObjetLancePierreTier0 && slot.ID != Joueur.IdObjetFauxPierreTier0) return;

        if (slot.ID == 106 || slot.ID == Joueur.IdObjetPellePierreTier0 || slot.ID == Joueur.IdObjetPiochePierreTier0 || slot.ID == Joueur.IdObjetLancePierreTier0 || slot.ID == Joueur.IdObjetFauxPierreTier0)
        {
            bool estPelle = slot.ID == Joueur.IdObjetPellePierreTier0;
            bool estPioche = slot.ID == Joueur.IdObjetPiochePierreTier0;
            bool estLance = slot.ID == Joueur.IdObjetLancePierreTier0;
            bool estFaux = slot.ID == Joueur.IdObjetFauxPierreTier0;
            PackedScene sceneHachette = GD.Load<PackedScene>(estPelle
                ? "res://Modeles/Equipements/Pelle_Pierre_tier0.glb"
                : (estPioche ? "res://Modeles/Equipements/Pioche_pierre_tier0.glb" : (estLance ? "res://Modeles/Equipements/Lance_en_pierre_tier0.glb" : (estFaux ? "res://Modeles/Equipements/Epe_pierre_tier0.glb" : "res://Modeles/Equipements/hachette_premitive_tier0.glb"))));
            if (sceneHachette == null) return;

            float tailleNorm = tailleMaxUnites * Mathf.Clamp(facteurEchelleLame, 0.72f, 1.28f);
            Node3D modeleHachette = sceneHachette.Instantiate<Node3D>();
            modeleHachette.Name = "ModeleArme";

            MeshInstance3D miLame106;
            MeshInstance3D miManche106;
            MeshInstance3D miCorde106;
            if (estPelle || estLance || estFaux)
            {
                // Pelle/Faux : part_0 = manche, part_1 = corde, part_2 = roche. Lance : même nœuds, ordre géométrique inversé (corrigé après fallback).
                MeshInstance3D part0 = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_0")
                    ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_0")
                    ?? TrouverMeshParMots(modeleHachette, "manche", "wood", "bois", "baton", "stick", "handle", "shaft");
                MeshInstance3D part1 = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_1")
                    ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_1")
                    ?? TrouverMeshParMots(modeleHachette, "cord", "rope", "ficelle", "lien");
                MeshInstance3D part2 = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_2")
                    ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_2")
                    ?? TrouverMeshParMots(modeleHachette, "pierre", "stone", "rock", "lame", "head", "blade", "spade", "tip", "pointe", "spear", "lance");
                miManche106 = part0;
                miCorde106 = part1;
                miLame106 = part2;
            }
            else
            {
                MeshInstance3D partA = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_1")
                    ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_1")
                    ?? TrouverMeshParMots(modeleHachette, "cord", "rope", "ficelle", "lien");
                MeshInstance3D partB = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_4")
                    ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_4")
                    ?? (estPioche ? TrouverMeshParMots(modeleHachette, "pierre", "stone", "rock", "lame", "head", "blade", "pick", "pioche") : null);
                MeshInstance3D partC = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_5")
                    ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_5")
                    ?? (estPioche ? TrouverMeshParMots(modeleHachette, "manche", "wood", "bois", "baton", "stick", "handle", "shaft") : null);
                miLame106 = partB;
                miManche106 = partC;
                miCorde106 = partA;
            }

            // Fallback robuste: si le GLB pelle a des noms différents, on répartit les meshes restants.
            var tousMeshes = ListerMeshes(modeleHachette);
            if (miLame106 == null || miManche106 == null)
            {
                var restants = new List<MeshInstance3D>();
                foreach (var mi in tousMeshes)
                {
                    if (mi == null || mi == miCorde106) continue;
                    if (mi == miLame106 || mi == miManche106) continue;
                    restants.Add(mi);
                }
                if (miLame106 == null && restants.Count > 0)
                {
                    miLame106 = restants[0];
                    restants.RemoveAt(0);
                }
                if (miManche106 == null && restants.Count > 0)
                    miManche106 = restants[0];
            }

            // Lance : dans le GLB, tripo_part_0 / _2 sont inversés par rapport à la pelle (pointe vs manche) ; la corde reste _1.
            if (estLance && miManche106 != null && miLame106 != null)
                (miManche106, miLame106) = (miLame106, miManche106);

            int idRoche106 = ItemPhysique.IdRocheMatiereMin + Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            int idxRocheSecondaire = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            if (estPioche && !string.IsNullOrEmpty(slot.GenomeAssemblage) && slot.GenomeAssemblage.StartsWith("PICKR:"))
            {
                string raw = slot.GenomeAssemblage.Substring("PICKR:".Length);
                if (int.TryParse(raw, out int parsed))
                    idxRocheSecondaire = Mathf.Clamp(parsed, 0, ItemPhysique.TableGeologique.Length - 1);
            }
            int idRocheSecondaire = ItemPhysique.IdRocheMatiereMin + idxRocheSecondaire;

            if (estPioche)
            {
                var tetesRoche = new List<MeshInstance3D>();
                if (miLame106 != null) tetesRoche.Add(miLame106);
                foreach (var mi in tousMeshes)
                {
                    if (mi == null || mi == miCorde106 || mi == miManche106) continue;
                    if (tetesRoche.Contains(mi)) continue;
                    string n = mi.Name.ToString().ToLowerInvariant();
                    bool sembleRoche = n.Contains("pierre") || n.Contains("stone") || n.Contains("rock")
                        || n.Contains("head") || n.Contains("blade") || n.Contains("pick") || n.Contains("pioche") || n.Contains("lame");
                    if (sembleRoche || tetesRoche.Count == 0)
                        tetesRoche.Add(mi);
                }
                for (int i = 0; i < tetesRoche.Count; i++)
                {
                    MeshInstance3D tete = tetesRoche[i];
                    int idRoche = i == 1 ? idRocheSecondaire : idRoche106;
                    int idxRoche = i == 1 ? idxRocheSecondaire : slot.IndexChimique;
                    RemplacerMeshParNormalesFacettes(tete);
                    AppliquerMaterielObjet(tete, idRoche, idxRoche, 0, 0, slot.IndexBotanique);
                }
            }
            else if (miLame106 != null)
            {
                RemplacerMeshParNormalesFacettes(miLame106);
                AppliquerMaterielObjet(miLame106, idRoche106, slot.IndexChimique, 0, 0, slot.IndexBotanique);
            }
            if (miManche106 != null)
            {
                RemplacerMeshParNormalesFacettes(miManche106);
                AppliquerMaterielObjet(miManche106, 32, 0, 0, 0, slot.IndexBotanique);
            }
            if (miCorde106 != null)
            {
                RemplacerMeshParNormalesFacettes(miCorde106);
                AppliquerMaterielObjet(miCorde106, 20, slot.IndexMorphologique, slot.IndexTaille, slot.NiveauFracture, slot.IndexBotanique);
            }

            NormaliserEchelleEtCentrerModeleArme(modeleHachette, tailleNorm);
            parent.AddChild(modeleHachette);
            return;
        }

        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Equipements/Dague_Pure_Tier0.glb");
        if (scene == null) return;

        float tailleNormDague = tailleMaxUnites * Mathf.Clamp(facteurEchelleLame, 0.72f, 1.28f);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        // tripo_part_4 = lame, tripo_part_3 = manche (ordre mesh du .glb ; les matériaux étaient inversés si on croisait 3/4).
        MeshInstance3D meshLame = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_4")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_4");
        MeshInstance3D meshManche = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_3")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_3");

        int idRocheDague = ItemPhysique.IdRocheMatiereMin + Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        if (meshLame != null)
            AppliquerMaterielObjet(meshLame, idRocheDague, slot.IndexChimique, 0, 0, slot.IndexBotanique);
        if (meshManche != null)
            AppliquerMaterielObjet(meshManche, 20, slot.IndexMorphologique, slot.IndexTaille, slot.NiveauFracture, slot.IndexBotanique);

        NormaliserEchelleEtCentrerModeleArme(modele, tailleNormDague);
        parent.AddChild(modele);
    }

    private static bool EstMatiereFlexible(int id)
    {
        int[] flexibles = { 15, 16, 17, 20, 21, Joueur.IdObjetCeinturePoches, Joueur.IdObjetCeintureSacoches, Joueur.IdObjetPochetteTier0, Joueur.IdObjetSacTier0 };
        return Array.IndexOf(flexibles, id) != -1;
    }

    private static bool EstObjetRigide(int id)
    {
        return ItemPhysique.EstIdRocheMatiere(id);
    }

}
