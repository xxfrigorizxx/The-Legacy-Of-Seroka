using Godot;
using System;

public partial class Joueur
{
    private static string ObtenirCheminModeleCharbon(int idObjet) => idObjet switch
    {
        IdObjetCharbonBasseQualite => "res://Modeles/materials/Minerais/Charbon_basse_qualiter.glb",
        IdObjetCharbonMoyenneQualite => "res://Modeles/materials/Minerais/charbon_moyen_qualiter.glb",
        IdObjetCharbonBonneQualite => "res://Modeles/materials/Minerais/Charbon_bonne_qualiter.glb",
        IdObjetCharbonAntracite => "res://Modeles/materials/Minerais/Charbon_antracite_qualiter.glb",
        _ => ""
    };

    private static readonly StandardMaterial3D[] MaterielsCharbonCache = new StandardMaterial3D[4];

    /// <summary>Matériau noir mat ; anthracite = noir profond avec léger reflet.</summary>
    public static StandardMaterial3D ObtenirMaterielCharbon(int idObjet)
    {
        int idx = idObjet switch
        {
            IdObjetCharbonBasseQualite => 0,
            IdObjetCharbonMoyenneQualite => 1,
            IdObjetCharbonBonneQualite => 2,
            IdObjetCharbonAntracite => 3,
            _ => -1
        };
        if (idx < 0)
            return new StandardMaterial3D { AlbedoColor = Colors.Black, Roughness = 1f, Metallic = 0f };

        if (MaterielsCharbonCache[idx] != null)
            return MaterielsCharbonCache[idx];

        StandardMaterial3D mat = idObjet == IdObjetCharbonAntracite
            ? new StandardMaterial3D
            {
                AlbedoColor = new Color(0.006f, 0.006f, 0.008f),
                Roughness = 0.38f,
                Metallic = 0.22f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.SchlickGgx,
                NormalEnabled = false,
                RimEnabled = false
            }
            : new StandardMaterial3D
            {
                AlbedoColor = idObjet switch
                {
                    IdObjetCharbonBasseQualite => new Color(0.16f, 0.16f, 0.16f),
                    IdObjetCharbonMoyenneQualite => new Color(0.08f, 0.08f, 0.08f),
                    IdObjetCharbonBonneQualite => new Color(0.018f, 0.018f, 0.018f),
                    _ => new Color(0.12f, 0.12f, 0.12f)
                },
                Roughness = 1f,
                Metallic = 0f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
                NormalEnabled = false,
                RimEnabled = false
            };

        MaterielsCharbonCache[idx] = mat;
        return mat;
    }

    private static void AppliquerMateriauCharbonSurMeshes(Node racine, int idObjet)
    {
        Material materiau = ObtenirMaterielCharbon(idObjet);
        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
                mi.MaterialOverride = materiau;
            foreach (Node enfant in n.GetChildren())
                Parcourir(enfant);
        }
        Parcourir(racine);
    }

    /// <summary>Morceau de charbon miné (GLB) — qualité selon l'ID objet.</summary>
    public static void InstancierModeleCharbon(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.22f)
    {
        if (!EstIdCharbonRecolte(slot.ID))
            return;
        string chemin = ObtenirCheminModeleCharbon(slot.ID);
        if (string.IsNullOrEmpty(chemin) || !ResourceLoader.Exists(chemin))
            return;

        PackedScene scene = GD.Load<PackedScene>(chemin);
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauCharbonSurMeshes(modele, slot.ID);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }
}
