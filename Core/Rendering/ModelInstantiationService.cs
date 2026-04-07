using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    /// <summary>True si l'objet a un mesh à afficher en main / preview.</summary>
    private static bool EstObjetAvecVisuel(int id)
    {
        if (id >= 1 && id <= 9) return true;
        return ItemPhysique.EstIdRocheMatiere(id) || id == 10 || id == 11 || id == BlocChutant.ID_BRANCHE || id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == Joueur.IdObjetCeinturePoches || id == Joueur.IdObjetCeintureSacoches || id == Joueur.IdObjetPochetteTier0 || id == Joueur.IdObjetSacTier0 || id == 30 || id == 32 || id == 34 || id == Joueur.IdObjetBaie || id == 100 || id == 105 || id == 106 || id == Joueur.IdObjetPellePierreTier0 || id == Joueur.IdObjetPiochePierreTier0 || id == 200;
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
        if (parent.HasMeta(MetaSignaturePelle107))
            parent.RemoveMeta(MetaSignaturePelle107);
        if (parent.HasMeta(MetaSignaturePioche108))
            parent.RemoveMeta(MetaSignaturePioche108);
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
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.IndexTaille, s.IndexBotanique, s.NiveauFracture);
    }

    private static int SignatureSlotAtelier200(SlotInventaire s)
    {
        if (s.ID != 200) return -1;
        return HashCode.Combine(s.IndexBotanique, s.IndexChimique, s.IndexMorphologique);
    }

    private static int SignatureSlotCorde20(SlotInventaire s)
    {
        if (s.ID != 20) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture, s.IndexBotanique);
    }

    private static int SignatureSlotTissu21(SlotInventaire s)
    {
        if (s.ID != 21) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture);
    }

    private static int SignatureSlotCeinture102(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetCeinturePoches) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture);
    }

    private static int SignatureSlotCeinture104(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetCeintureSacoches) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture, s.CleConteneur ?? "");
    }

    private static int SignatureSlotPochette103(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetPochetteTier0) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture);
    }

    private static int SignatureSlotSac101(SlotInventaire s)
    {
        if (s.ID != Joueur.IdObjetSacTier0) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture, s.CleConteneur ?? "");
    }

    /// <summary>Échelle de la lame dague : référence = roche moyenne (index 2) ; plus grosse roche en pointe → lame un peu plus massive.</summary>
    private static float ObtenirFacteurEchelleLameDague(SlotInventaire slot)
    {
        if (slot.ID != 105) return 1f;
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

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                RemplacerMeshParNormalesFacettes(mi);
                AppliquerMaterielObjet(mi, Joueur.IdObjetCeintureSacoches, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, slot.IndexBotanique);
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
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

    public static void InstancierModeleArme(Node3D parent, SlotInventaire slot, float tailleMaxUnites = 0.525f, float facteurEchelleLame = 1f)
    {
        NettoyerModelesEnfants(parent);
        if (slot.ID != 105 && slot.ID != 106 && slot.ID != Joueur.IdObjetPellePierreTier0 && slot.ID != Joueur.IdObjetPiochePierreTier0) return;

        if (slot.ID == 106 || slot.ID == Joueur.IdObjetPellePierreTier0 || slot.ID == Joueur.IdObjetPiochePierreTier0)
        {
            bool estPelle = slot.ID == Joueur.IdObjetPellePierreTier0;
            bool estPioche = slot.ID == Joueur.IdObjetPiochePierreTier0;
            PackedScene sceneHachette = GD.Load<PackedScene>(estPelle
                ? "res://Modeles/Equipements/Pelle_Pierre_tier0.glb"
                : (estPioche ? "res://Modeles/Equipements/Pioche_pierre_tier0.glb" : "res://Modeles/Equipements/hachette_premitive_tier0.glb"));
            if (sceneHachette == null) return;

            float tailleNorm = tailleMaxUnites * Mathf.Clamp(facteurEchelleLame, 0.72f, 1.28f);
            Node3D modeleHachette = sceneHachette.Instantiate<Node3D>();
            modeleHachette.Name = "ModeleArme";

            MeshInstance3D miLame106;
            MeshInstance3D miManche106;
            MeshInstance3D miCorde106;
            if (estPelle)
            {
                // Pelle : mapping validé user -> part_0 = manche, part_1 = corde, part_2 = roche.
                MeshInstance3D part0 = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_0")
                    ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_0")
                    ?? TrouverMeshParMots(modeleHachette, "manche", "wood", "bois", "baton", "stick", "handle", "shaft");
                MeshInstance3D part1 = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_1")
                    ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_1")
                    ?? TrouverMeshParMots(modeleHachette, "cord", "rope", "ficelle", "lien");
                MeshInstance3D part2 = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_2")
                    ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_2")
                    ?? TrouverMeshParMots(modeleHachette, "pierre", "stone", "rock", "lame", "head", "blade", "spade");
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

            int idRoche106 = ItemPhysique.IdRocheMatiereMin + Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            if (miLame106 != null)
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
