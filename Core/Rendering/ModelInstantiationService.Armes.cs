using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    public static void InstancierModeleArme(Node3D parent, SlotInventaire slot, float tailleMaxUnites = 0.525f, float facteurEchelleLame = 1f)
    {
        NettoyerModelesEnfants(parent);
        if (slot.ID != 105 && slot.ID != 106 && slot.ID != Joueur.IdObjetHachePierreTier1 && slot.ID != Joueur.IdObjetPellePierreTier0 && slot.ID != Joueur.IdObjetPiochePierreTier0 && slot.ID != Joueur.IdObjetLancePierreTier0 && slot.ID != Joueur.IdObjetFauxPierreTier0) return;

        if (slot.ID == 106 || slot.ID == Joueur.IdObjetHachePierreTier1 || slot.ID == Joueur.IdObjetPellePierreTier0 || slot.ID == Joueur.IdObjetPiochePierreTier0 || slot.ID == Joueur.IdObjetLancePierreTier0 || slot.ID == Joueur.IdObjetFauxPierreTier0)
        {
            bool estHachePierre = slot.ID == Joueur.IdObjetHachePierreTier1;
            bool estPelle = slot.ID == Joueur.IdObjetPellePierreTier0;
            bool estPioche = slot.ID == Joueur.IdObjetPiochePierreTier0;
            bool estLance = slot.ID == Joueur.IdObjetLancePierreTier0;
            bool estFaux = slot.ID == Joueur.IdObjetFauxPierreTier0;
            PackedScene sceneHachette = GD.Load<PackedScene>(estHachePierre
                ? "res://Modeles/Equipable/Hache_pierre.glb"
                : (estPelle
                ? "res://Modeles/Equipements/Pelle_Pierre_tier0.glb"
                : (estPioche ? "res://Modeles/Equipements/Pioche_pierre_tier0.glb" : (estLance ? "res://Modeles/Equipements/Lance_en_pierre_tier0.glb" : (estFaux ? "res://Modeles/Equipements/Epe_pierre_tier0.glb" : "res://Modeles/Equipements/hachette_premitive_tier0.glb")))));
            if (sceneHachette == null) return;

            float tailleNorm = tailleMaxUnites * Mathf.Clamp(facteurEchelleLame, 0.72f, 1.28f);
            Node3D modeleHachette = sceneHachette.Instantiate<Node3D>();
            modeleHachette.Name = "ModeleArme";

            if (estHachePierre)
            {
                var tousMeshesHachePierre = ListerMeshes(modeleHachette);
                MeshInstance3D meshRoche = TrouverMeshParMots(modeleHachette, "roche", "rock", "stone", "pierre", "head", "blade", "lame");
                MeshInstance3D meshBaton = TrouverMeshParMots(modeleHachette, "baton", "bois", "wood", "stick", "manche", "handle", "shaft");
                if (meshRoche == null && tousMeshesHachePierre.Count > 0) meshRoche = tousMeshesHachePierre[0];
                if (meshBaton == null && tousMeshesHachePierre.Count > 1) meshBaton = tousMeshesHachePierre[1];
                int idRoche132 = ItemPhysique.IdRocheMatiereMin + Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
                if (meshRoche != null)
                {
                    RemplacerMeshParNormalesFacettes(meshRoche);
                    AppliquerMaterielObjet(meshRoche, idRoche132, slot.IndexChimique, 0, 0, slot.IndexBotanique);
                }
                if (meshBaton != null)
                {
                    RemplacerMeshParNormalesFacettes(meshBaton);
                    AppliquerMaterielObjet(meshBaton, 32, 0, 0, 0, slot.IndexBotanique);
                }
                NormaliserEchelleEtCentrerModeleArme(modeleHachette, tailleNorm);
                parent.AddChild(modeleHachette);
                return;
            }

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
