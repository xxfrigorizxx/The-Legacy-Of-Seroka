using Godot;
using System;

/// <summary>Manipule les sommets (Vertices) d'un maillage 3D en temps réel pour simuler l'usure et la taille de la pierre.</summary>
public static class SculpteurPrimitif
{
    /// <param name="affutagePointeHautBas">True si la souris a surtout bougé en vertical → taille une pointe (sommet +Y).</param>
    public static ArrayMesh TaillerRoche(Mesh meshOriginal, float angleDegres, bool affutagePointeHautBas = false)
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

        if (affutagePointeHautBas)
        {
            float radRot = Mathf.DegToRad(angleDegres);
            Vector3 axeHaut = new Vector3(Mathf.Sin(radRot), Mathf.Cos(radRot), 0f).Normalized();
            for (int i = 0; i < mdt.GetVertexCount(); i++)
            {
                Vector3 v = mdt.GetVertex(i);
                float h = v.Dot(axeHaut);
                if (h > 0.02f)
                {
                    float t = Mathf.Clamp(h * 2.6f, 0f, 1f);
                    float resserre = 1f - 0.2f * t;
                    Vector3 perp = v - axeHaut * h;
                    v = axeHaut * h * (1f - 0.12f * t) + perp * resserre;
                    if (perp.LengthSquared() > 1e-8f)
                        v -= perp.Normalized() * (t * 0.11f);
                }
                mdt.SetVertex(i, v);
            }
        }
        else
        {
            float rad = Mathf.DegToRad(angleDegres);
            Vector3 directionUsure = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad)).Normalized();

            for (int i = 0; i < mdt.GetVertexCount(); i++)
            {
                Vector3 v = mdt.GetVertex(i);
                float exposition = v.Dot(directionUsure);

                if (exposition > 0.01f)
                {
                    float forceEclat = Mathf.Clamp(exposition * 4.0f, 0f, 1f);
                    v.Y = Mathf.Lerp(v.Y, 0f, forceEclat);
                    v -= directionUsure * (exposition * 0.35f);
                }

                mdt.SetVertex(i, v);
            }
        }

        // Recalcul des normales (MeshDataTool n'a pas GenerateNormals en Godot 4)
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
