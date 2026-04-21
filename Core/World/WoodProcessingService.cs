using Godot;

public partial class Joueur
{
    /// <summary>ID morpho auto : dimensions réelles (m) → entier stable (comme index cache roche, pour crafts / réseau futur).</summary>
    /// <summary>Récupère ScaleEclat inventaire depuis le cylindre réel (bûche/bâton sans scale sur le RB).</summary>
    private static float ObtenirLongueurBoisWorld(int indexTaille) =>
        indexTaille switch { 0 => 1.2f, 1 => 1.0f, 2 => 0.5f, 3 => 0.25f, _ => 1.0f };

    /// <summary>Fente longitudinale (0–3) sur l’objet posé ; les anciennes valeurs hors plage sont ramenées à 0.</summary>
    private static int MorphologieBoisDepuisItem(ItemPhysique item)
    {
        if (item == null || (item.ID_Objet != 30 && item.ID_Objet != 32 && item.ID_Objet != BlocChutant.ID_BRANCHE)) return 0;
        int m = item.IndexCacheMemoire;
        return m >= 0 && m <= 3 ? m : 0;
    }

    /// <summary>Longueur inventaire (ScaleEclat.Z) : meta prioritaire, sinon déduit du mesh local (évite tronc → standard si meta perdu).</summary>
    private static Vector3 ScaleEclatBoisAuRamassage(ItemPhysique item)
    {
        if (item == null || (item.ID_Objet != 30 && item.ID_Objet != 32 && item.ID_Objet != BlocChutant.ID_BRANCHE))
            return Vector3.One;
        if (item.HasMeta("ScaleLongueurBois"))
            return new Vector3(1, 1, (float)item.GetMeta("ScaleLongueurBois").AsSingle());
        int t = Mathf.Clamp(item.IndexTailleRoche, 0, 4);
        float baseLen = ObtenirLongueurBoisWorld(t);
        Mesh m = item.ObtenirMeshVisuel();
        if (m != null)
        {
            float meshLen = m.GetAabb().Size.Y;
            if (meshLen > 0.02f)
                return new Vector3(1, 1, meshLen / Mathf.Max(0.001f, baseLen));
        }
        return Vector3.One;
    }

    public static void CalculerDimensionsBoisPose(int idObjet, int indexMorphologique, int indexTaille, out float baseRadius, out float baseLength, out float w, out float h)
    {
        int f = Mathf.Clamp(indexMorphologique, 0, 3);
        int t = Mathf.Clamp(indexTaille, 0, 3);
        baseRadius = idObjet == 30 ? 0.12f : 0.02f;
        baseLength = ObtenirLongueurBoisWorld(t);
        w = baseRadius * 2f;
        h = baseRadius * 2f;
        if (f == 1) h = baseRadius;
        else if (f == 2) { w = baseRadius; h = baseRadius; }
        else if (f >= 3) { w = baseRadius; h = baseRadius * 0.3f; }
    }

    public static Mesh GenererMeshBoisFendu(float rayon, float hauteur, int morpho)
    {
        if (morpho <= 0) return new CylinderMesh { TopRadius = rayon, BottomRadius = rayon, Height = hauteur };
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        float angleMax = Mathf.Pi / Mathf.Pow(2, morpho - 1);
        int segments = Mathf.Max(4, 16 / morpho);
        float demiH = hauteur * 0.5f;

        Vector3[] arcTop = new Vector3[segments + 1];
        Vector3[] arcBot = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float a = (i / (float)segments) * angleMax;
            float x = Mathf.Sin(a) * rayon;
            float z = Mathf.Cos(a) * rayon;
            arcTop[i] = new Vector3(x, demiH, z);
            arcBot[i] = new Vector3(x, -demiH, z);
        }
        Vector3 centerTop = new Vector3(0, demiH, 0);
        Vector3 centerBot = new Vector3(0, -demiH, 0);

        int idx = 0;
        void AddTri(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            st.SetNormal(n); st.SetUV(uv1); st.AddVertex(v1);
            st.SetNormal(n); st.SetUV(uv2); st.AddVertex(v2);
            st.SetNormal(n); st.SetUV(uv3); st.AddVertex(v3);
            st.AddIndex(idx); st.AddIndex(idx + 1); st.AddIndex(idx + 2);
            idx += 3;
        }

        // 1. Ecorce (Courbe Extérieure)
        for (int i = 0; i < segments; i++)
        {
            Vector3 t1 = arcTop[i], t2 = arcTop[i + 1], b1 = arcBot[i], b2 = arcBot[i + 1];
            Vector3 nMid = new Vector3((t1.X + t2.X) * 0.5f, 0, (t1.Z + t2.Z) * 0.5f).Normalized();
            float u1 = (float)i / segments, u2 = (float)(i + 1) / segments;
            AddTri(t1, t2, b1, nMid, new Vector2(u1, 0), new Vector2(u2, 0), new Vector2(u1, 1));
            AddTri(t2, b2, b1, nMid, new Vector2(u2, 0), new Vector2(u2, 1), new Vector2(u1, 1));
        }
        // 2. Capuchon Haut
        for (int i = 0; i < segments; i++)
        {
            AddTri(centerTop, arcTop[i + 1], arcTop[i], Vector3.Up, new Vector2(0.5f, 0.5f), new Vector2(arcTop[i + 1].X / rayon, arcTop[i + 1].Z / rayon), new Vector2(arcTop[i].X / rayon, arcTop[i].Z / rayon));
        }
        // 3. Capuchon Bas
        for (int i = 0; i < segments; i++)
        {
            AddTri(centerBot, arcBot[i], arcBot[i + 1], Vector3.Down, new Vector2(0.5f, 0.5f), new Vector2(arcBot[i].X / rayon, arcBot[i].Z / rayon), new Vector2(arcBot[i + 1].X / rayon, arcBot[i + 1].Z / rayon));
        }
        // 4. Aubier - Face A
        Vector3 nA = new Vector3(-1, 0, 0);
        AddTri(centerTop, centerBot, arcTop[0], nA, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0));
        AddTri(arcTop[0], centerBot, arcBot[0], nA, new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1));
        // 5. Aubier - Face B
        Vector3 dirB = new Vector3(arcTop[segments].X, 0, arcTop[segments].Z).Normalized();
        Vector3 nB = new Vector3(dirB.Z, 0, -dirB.X);
        AddTri(centerTop, arcTop[segments], centerBot, nB, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1));
        AddTri(arcTop[segments], arcBot[segments], centerBot, nB, new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        return st.Commit();
    }

}
