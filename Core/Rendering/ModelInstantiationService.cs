using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    /// <summary>True si l'objet a un mesh à afficher en main / preview.</summary>
    private static bool EstObjetAvecVisuel(int id)
    {
        if (Atlas_Matiere.EstIdVoxelSurfaceTerrain(id)) return true;
        return ItemPhysique.EstIdRocheMatiere(id) || id == 10 || id == 11 || id == Joueur.IdObjetAloeVera || EstIdCharbonRecolte(id) || EstIdQuartzRecolte(id) || EstIdEtainRecolte(id) || id == BlocChutant.ID_BRANCHE || id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == Joueur.IdObjetCeinturePoches || id == Joueur.IdObjetCeintureSacoches || id == Joueur.IdObjetPochetteTier0 || id == Joueur.IdObjetSacTier0 || id == Joueur.IdObjetCarnetSavoir || id == Joueur.IdObjetSteakCru || id == Joueur.IdObjetSteakCuit || id == Joueur.IdObjetOsBoeuf || id == Joueur.IdObjetCuirBoeuf || id == Joueur.IdObjetIntestinBoeuf || id == Joueur.IdObjetIntestinBoeufNettoye || id == 30 || id == 32 || id == 34 || id == Joueur.IdObjetBaie || id == 100 || id == 105 || id == 106 || id == Joueur.IdObjetHachePierreTier1 || id == Joueur.IdObjetPellePierreTier0 || id == Joueur.IdObjetPiochePierreTier0 || id == Joueur.IdObjetLancePierreTier0 || id == Joueur.IdObjetFauxPierreTier0 || id == 200 || id == Joueur.IdObjetTableBoisDecorative || id == Joueur.IdObjetTableArtisanaTier1 || id == Joueur.IdObjetTableAnalyseTier1 || id == Joueur.IdObjetRackBatons || id == Joueur.IdObjetRackBuches || id == Joueur.IdObjetCoffreBoisTier0 || id == Joueur.IdObjetPitFeu || id == Joueur.IdObjetPitFeuRoche || id == Joueur.IdObjetFourTorchie || id == Joueur.IdObjetAllumeFeu || id == Joueur.IdObjetMailletBois || id == Joueur.IdObjetBolBois || id == Joueur.IdObjetBolEau || id == Joueur.IdObjetArgileHumidifiee || id == Joueur.IdObjetBolArgile || id == Joueur.IdObjetBolCeramique || id == Joueur.IdObjetMouleArgile || id == Joueur.IdObjetMouleCeramique || id == Joueur.IdObjetChamotte || id == Joueur.IdObjetPinceOs || id == Joueur.IdObjetTorchie || id == Joueur.IdObjetMortierPilonBois || id == Joueur.IdObjetAtelleJambe || id == Joueur.IdObjetAtelleBras || id == Joueur.IdObjetBandageTier1 || id == Joueur.IdObjetFenetreBois || EstIdFondation(id) || EstIdPlancher(id) || EstIdMuret(id) || EstIdMurBois(id) || EstIdPorteBois(id) || EstIdToitChaume(id) || EstIdTorche(id);
    }

    public static void NettoyerModelesEnfants(Node3D parent)
    {
        if (parent == null) return;
        Godot.Collections.Array<Node> enfants = parent.GetChildren();
        for (int i = enfants.Count - 1; i >= 0; i--)
        {
            Node n = enfants[i];
            string nom = n.Name.ToString();
            if (nom.Contains("ModeleArme")
                || nom.Contains("TorcheFlamme")
                || nom.Contains("TorcheLumiere"))
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
        if (parent.HasMeta(MetaSignatureMailletBois128))
            parent.RemoveMeta(MetaSignatureMailletBois128);
        if (parent.HasMeta(MetaSignatureBolBois129))
            parent.RemoveMeta(MetaSignatureBolBois129);
        if (parent.HasMeta(MetaSignatureMortierPilon130))
            parent.RemoveMeta(MetaSignatureMortierPilon130);
        if (parent.HasMeta(MetaSignatureFenetreBois146))
            parent.RemoveMeta(MetaSignatureFenetreBois146);
        if (parent.HasMeta(MetaSignatureAtelleJambe133))
            parent.RemoveMeta(MetaSignatureAtelleJambe133);
        if (parent.HasMeta(MetaSignatureAtelleBras134))
            parent.RemoveMeta(MetaSignatureAtelleBras134);
        if (parent.HasMeta(MetaSignatureBandageTier1135))
            parent.RemoveMeta(MetaSignatureBandageTier1135);
        if (parent.HasMeta(MetaSignatureTableAnalyse131))
            parent.RemoveMeta(MetaSignatureTableAnalyse131);
        if (parent.HasMeta(MetaSignatureFondation))
            parent.RemoveMeta(MetaSignatureFondation);
        if (parent.HasMeta(MetaSignatureSolBois136))
            parent.RemoveMeta(MetaSignatureSolBois136);
        if (parent.HasMeta(MetaSignatureSolRoche137))
            parent.RemoveMeta(MetaSignatureSolRoche137);
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

    /// <summary>Applique une mise à l'échelle non uniforme pour atteindre des dimensions monde cibles, base ancrée sur Y=0.</summary>
    private static void NormaliserDimensionsAncrerAuSol(Node3D modeleRacine, float cibleX, float cibleY, float cibleZ)
    {
        if (modeleRacine == null) return;
        Aabb? combine = null;
        AccumulerAabbMeshes(modeleRacine, Transform3D.Identity, ref combine);
        if (!combine.HasValue) return;
        Aabb box = combine.Value;
        float sx = box.Size.X > 1e-6f ? cibleX / box.Size.X : 1f;
        float sy = box.Size.Y > 1e-6f ? cibleY / box.Size.Y : 1f;
        float sz = box.Size.Z > 1e-6f ? cibleZ / box.Size.Z : 1f;
        modeleRacine.Scale = new Vector3(modeleRacine.Scale.X * sx, modeleRacine.Scale.Y * sy, modeleRacine.Scale.Z * sz);
        combine = null;
        AccumulerAabbMeshes(modeleRacine, Transform3D.Identity, ref combine);
        if (!combine.HasValue) return;
        Aabb apres = combine.Value;
        Vector3 centre = apres.GetCenter();
        Vector3 posAvant = modeleRacine.Position;
        modeleRacine.Position = new Vector3(
            posAvant.X - centre.X,
            posAvant.Y - apres.Position.Y,
            posAvant.Z - centre.Z);
    }

    /// <summary>Plancher (bois/roche) : carré exact emprise×emprise en X/Z (étire si AABB non carrée) + pivot centré X/Z.</summary>
    private static void NormaliserDimensionsPlancherAncrerAuSol(Node3D modeleRacine, float empriseHorizontale, float epaisseur)
    {
        if (modeleRacine == null) return;
        modeleRacine.Rotation = Vector3.Zero;
        modeleRacine.Scale = Vector3.One;
        modeleRacine.Position = Vector3.Zero;
        Aabb? combine = null;
        AccumulerAabbMeshes(modeleRacine, Transform3D.Identity, ref combine);
        if (!combine.HasValue) return;
        Aabb box = combine.Value;
        float sx = box.Size.X > 1e-6f ? empriseHorizontale / box.Size.X : 1f;
        float sz = box.Size.Z > 1e-6f ? empriseHorizontale / box.Size.Z : 1f;
        float sy = box.Size.Y > 1e-6f ? epaisseur / box.Size.Y : 1f;
        Vector3 scale = modeleRacine.Scale;
        modeleRacine.Scale = new Vector3(scale.X * sx, scale.Y * sy, scale.Z * sz);
        combine = null;
        AccumulerAabbMeshes(modeleRacine, Transform3D.Identity, ref combine);
        if (!combine.HasValue) return;
        Aabb apres = combine.Value;
        Vector3 centre = apres.GetCenter();
        Vector3 posAvant = modeleRacine.Position;
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
            Node noeud = pile[i];
            if (noeud is MeshInstance3D mi)
                resultat.Add(mi);
            foreach (Node c in noeud.GetChildren())
                pile.Add(c);
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
            bool uvValidesPourTangentes = false;

            void PousserTri(Vector3 a, Vector3 b, Vector3 c, Vector2? uva, Vector2? uvb, Vector2? uvc)
            {
                Vector3 n = (b - a).Cross(c - a);
                if (n.LengthSquared() < 1e-16f) return;
                n = n.Normalized();
                if (uva.HasValue && uvb.HasValue && uvc.HasValue)
                {
                    uvValidesPourTangentes = true;
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
            {
                if (uvValidesPourTangentes)
                {
                    try { st.GenerateTangents(); } catch { }
                }
                st.Commit(output);
            }
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







}
