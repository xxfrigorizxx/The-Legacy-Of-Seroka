using Godot;
using System;

/// <summary>Affûtage pierre (fort) vs bois (léger : copeaux, sans écraser le cylindre ni les UV).</summary>
public static class SculpteurPrimitif
{
    /// <param name="idMatiere">10/12 roche, 11 silex, 30/32 bois.</param>
    /// <param name="pointeHautBas">True si friction surtout verticale.</param>
    public static ArrayMesh TaillerRoche(Mesh meshOriginal, float angleDegres, int idMatiere, bool pointeHautBas)
    {
        ArrayMesh arrayMesh = new ArrayMesh();

        if (meshOriginal is ArrayMesh am)
        {
            arrayMesh = (ArrayMesh)am.Duplicate();
        }
        else if (meshOriginal is PrimitiveMesh prim)
        {
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, prim.GetMeshArrays());
        }
        else
        {
            return null;
        }

        var mdt = new MeshDataTool();
        if (mdt.CreateFromSurface(arrayMesh, 0) != Error.Ok)
            return arrayMesh;

        bool estBois = idMatiere == 30 || idMatiere == 32;

        int vc = mdt.GetVertexCount();
        float yMin = float.MaxValue, yMax = float.MinValue;
        for (int i = 0; i < vc; i++)
        {
            float y = mdt.GetVertex(i).Y;
            yMin = Mathf.Min(yMin, y);
            yMax = Mathf.Max(yMax, y);
        }
        float ySpan = Mathf.Max(1e-5f, yMax - yMin);
        float ySeuilPartieHaute = yMin + ySpan * (estBois ? 0.82f : 0.38f);

        float limiteEpaisseur = (idMatiere == 11) ? 0.005f
            : estBois ? 0.055f
            : 0.025f;

        if (pointeHautBas)
        {
            float radRot = Mathf.DegToRad(angleDegres);
            Vector3 axeHaut = new Vector3(Mathf.Sin(radRot), Mathf.Cos(radRot), 0f).Normalized();
            for (int i = 0; i < vc; i++)
            {
                Vector3 v = mdt.GetVertex(i);
                if (v.Y < ySeuilPartieHaute)
                {
                    mdt.SetVertex(i, v);
                    continue;
                }

                float h = v.Dot(axeHaut);
                float hMinEffet = estBois ? 0.06f : 0.02f;
                if (h > hMinEffet)
                {
                    float t = Mathf.Clamp(h * (estBois ? 1.8f : 2.6f), 0f, 1f);
                    float factRess = estBois ? 0.07f : 0.2f;
                    float resserre = 1f - factRess * t;
                    Vector3 perp = v - axeHaut * h;
                    float factLong = estBois ? 0.05f : 0.12f;
                    float hReduit = h * (1f - factLong * t);
                    float hCible = Mathf.Max(hReduit, limiteEpaisseur);
                    v = axeHaut * hCible + perp * resserre;
                    float coupPerp = estBois ? 0.035f : 0.11f;
                    if (perp.LengthSquared() > 1e-8f)
                        v -= perp.Normalized() * (t * coupPerp);
                }
                mdt.SetVertex(i, v);
            }
        }
        else
        {
            float rad = Mathf.DegToRad(angleDegres);
            Vector3 directionUsure = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad)).Normalized();

            for (int i = 0; i < vc; i++)
            {
                Vector3 v = mdt.GetVertex(i);
                float exposition = v.Dot(directionUsure);
                float seuilExpo = estBois ? 0.035f : 0.01f;
                if (exposition > seuilExpo)
                {
                    float forceEclat = Mathf.Clamp(exposition * (estBois ? 1.6f : 4.0f), 0f, 1f);
                    float tHaut = Mathf.Clamp((v.Y - yMin) / ySpan, 0f, 1f);
                    float poidsHaut = Mathf.SmoothStep(0.15f, 1f, tHaut);
                    if (!estBois)
                        forceEclat *= Mathf.Lerp(0.55f, 1f, poidsHaut);
                    else
                        forceEclat *= Mathf.Lerp(0.45f, 0.85f, poidsHaut);

                    Vector3 perp = v - directionUsure * exposition;
                    float pince = estBois ? 0.085f : 0.22f;
                    perp *= 1f - pince * forceEclat;
                    v = directionUsure * exposition + perp;

                    if (!estBois)
                    {
                        float cibleY = Mathf.Sign(v.Y) * limiteEpaisseur;
                        if (Mathf.Abs(v.Y) < 1e-6f) cibleY = limiteEpaisseur;
                        v.Y = Mathf.Lerp(v.Y, cibleY, forceEclat);
                        v -= directionUsure * (exposition * 0.05f);
                    }
                    else
                    {
                        // Bois : pas d’écrasement vertical type « galette » — léger rabotage tangentiel (copeaux).
                        v -= directionUsure * (exposition * 0.016f);
                    }
                }

                mdt.SetVertex(i, v);
            }
        }

        for (int i = 0; i < mdt.GetVertexCount(); i++)
        {
            int[] faces = mdt.GetVertexFaces(i);
            Vector3 sum = Vector3.Zero;
            foreach (int faceIdx in faces)
                sum += mdt.GetFaceNormal(faceIdx);
            if (sum.LengthSquared() > 0.0001f)
                mdt.SetVertexNormal(i, sum.Normalized());
        }

        ArrayMesh finalMesh = new ArrayMesh();
        mdt.CommitToSurface(finalMesh);
        return finalMesh;
    }
}
