using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private static Mesh _cacheMeshCorde;
    /// <summary>Invalider le cache si la topologie du mesh corde change (Ã©vite un mesh cassÃ© gardÃ© en statique).</summary>
    private const int RevisionCacheMeshCorde = 1;
    private static int _revisionMeshCordeEnCache = -1;

    private static Mesh CreerMeshCordeTressee()
    {
        if (_cacheMeshCorde != null && _revisionMeshCordeEnCache == RevisionCacheMeshCorde)
            return _cacheMeshCorde;
        _cacheMeshCorde = null;
        const float rayonHelice = 0.026f;
        const float rayonTube = 0.012f;
        const float hauteur = 0.28f;
        const int nbTours = 3;
        const int ringsParStrand = 24;
        const int segsParRing = 6;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int strand = 0; strand < 3; strand++)
        {
            float phase = strand * Mathf.Tau / 3f;
            for (int r = 0; r < ringsParStrand; r++)
            {
                float t = r / (float)(ringsParStrand - 1);
                float angle = phase + t * nbTours * Mathf.Tau;
                Vector3 centre = new Vector3(rayonHelice * Mathf.Cos(angle), t * hauteur - hauteur * 0.5f, rayonHelice * Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), hauteur / (rayonHelice * nbTours * Mathf.Tau), Mathf.Cos(angle)).Normalized();
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 binormal = tangent.Cross(radial).Normalized();
                for (int s = 0; s < segsParRing; s++)
                {
                    float a = s * Mathf.Tau / segsParRing;
                    Vector3 offset = (radial * Mathf.Cos(a) + binormal * Mathf.Sin(a)) * rayonTube;
                    st.AddVertex(centre + offset);
                }
            }
            // Quads entre deux anneaux : b = voisin latÃ©ral sur lâ€™anneau (wrap), pas v+s1 (cassait s=dernier â†’ index hors plage).
            for (int r = 0; r < ringsParStrand - 1; r++)
            {
                int v0 = strand * ringsParStrand * segsParRing + r * segsParRing;
                for (int s = 0; s < segsParRing; s++)
                {
                    int s1 = (s + 1) % segsParRing;
                    int a = v0 + s;
                    int b = v0 + s1;
                    int c = v0 + segsParRing + s;
                    int d = v0 + segsParRing + s1;
                    st.AddIndex(a); st.AddIndex(b); st.AddIndex(c);
                    st.AddIndex(b); st.AddIndex(d); st.AddIndex(c);
                }
            }
        }
        st.GenerateNormals();
        _cacheMeshCorde = st.Commit();
        _revisionMeshCordeEnCache = RevisionCacheMeshCorde;
        return _cacheMeshCorde;
    }

    public static Mesh ObtenirMeshDepuisCache(int id, int indexMorpho, int indexTaille = 2)
    {
        if (ItemPhysique.EstIdRocheMatiere(id))
        {
            float r = ItemPhysique.RayonBaseRochesJoueur(indexTaille);
            return new SphereMesh { Radius = r, Height = r * 2f };
        }
        else if (id == 10) return Chunk_Client.ObtenirMeshBuissonProcedural(true);
        else if (id == 11) return Chunk_Client.ObtenirMeshBuissonProcedural(false);
        else if (id == IdObjetAloeVera) return Chunk_Client.ObtenirMeshLamelleAloeObjetProcedural();
        else if (id == BlocChutant.ID_BRANCHE)
        {
            if (indexMorpho == 1)
            {
                const float rr = 0.0267f;
                const float hh = 0.2f;
                return new CylinderMesh { TopRadius = rr, BottomRadius = rr, Height = hh, RadialSegments = 10, Rings = 1 };
            }
            CalculerDimensionsBoisPose(32, indexMorpho, indexTaille, out float br, out float bl, out _, out _);
            return GenererMeshBoisFendu(br, bl, indexMorpho);
        }
        else if (id == 15 || id == 16) return new CapsuleMesh { Radius = 0.009f, Height = 0.34f };
        else if (id == 17) return new CapsuleMesh { Radius = 0.009f, Height = 0.38f };
        else if (id == 20) return null; // GLB res://Modeles/materials/traisagre_corde_tier0.glb via InstancierModeleCordeTier0Gazon
        else if (id == 21) return null; // GLB res://Modeles/materials/tissu_tier0.glb via InstancierModeleTissuTier0
        else if (id == IdObjetSacTier0) return null; // GLB res://Modeles/Equipable/Sac_Tiere0.glb via InstancierModeleSacTier0
        else if (id == IdObjetCarnetSavoir) return null; // modèle procédural via InstancierModeleCarnetSavoir
        else if (id == IdObjetSteakCru || id == IdObjetSteakCuit || id == IdObjetOsBoeuf || id == IdObjetCuirBoeuf || id == IdObjetIntestinBoeuf || id == IdObjetIntestinBoeufNettoye || EstIdCharbonRecolte(id) || EstIdQuartzRecolte(id) || EstIdEtainRecolte(id)) return null; // GLB via InstancierModele* dans ModelInstantiationService
        else if (id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches) return null; // GLB ceinture / ceinture+pochettes via instanciation dÃ©diÃ©e
        else if (id == IdObjetPochetteTier0) return null; // GLB res://Modeles/materials/Pochette_Tiere0.glb via InstancierModelePochetteTier0
        else if (id == IdObjetPellePierreTier0) return null; // GLB res://Modeles/Equipements/Pelle_Pierre_tier0.glb via InstancierModeleArme
        else if (id == IdObjetPiochePierreTier0) return null; // GLB res://Modeles/Equipements/Pioche_pierre_tier0.glb via InstancierModeleArme
        else if (id == IdObjetLancePierreTier0) return null; // GLB res://Modeles/Equipements/Lance_en_pierre_tier0.glb via InstancierModeleArme
        else if (id == IdObjetFauxPierreTier0) return null; // GLB res://Modeles/Equipements/Epe_pierre_tier0.glb via InstancierModeleArme
        else if (id == IdObjetHachePierreTier1) return null; // GLB res://Modeles/Equipable/Hache_pierre.glb via InstancierModeleArme
        else if (id == IdObjetRackBatons || id == IdObjetRackBuches) return null; // GLB rack (bÃ¢tons / bÃ»ches) via instanciation dÃ©diÃ©e
        else if (id == IdObjetCoffreBoisTier0) return null; // GLB coffre via InstancierModeleCoffreBoisTier0
        else if (id == IdObjetPitFeu) return null; // GLB pit à feu via InstancierModelePitFeu
        else if (id == IdObjetPitFeuRoche) return null; // GLB pit à feu roche via InstancierModelePitFeuRoche
        else if (id == IdObjetAllumeFeu) return null; // GLB allume-feu via InstancierModeleAllumeFeu
        else if (id == IdObjetMailletBois) return null; // GLB maillet via InstancierModeleMailletBois
        else if (id == IdObjetBolBois) return null; // GLB bol via InstancierModeleBolBois
        else if (id == IdObjetBolEau) return null; // GLB bol plein via InstancierModeleBolEau
        else if (id == IdObjetArgileHumidifiee) return null; // GLB argile humidifiée via InstancierModeleArgileHumidifiee
        else if (id == IdObjetBolArgile) return null; // GLB bol en argile via InstancierModeleBolArgile
        else if (id == IdObjetBolCeramique) return null; // GLB bol céramique via InstancierModeleBolCeramique
        else if (id == IdObjetMouleArgile) return null; // GLB moule argile via InstancierModeleMouleArgile
        else if (id == IdObjetMouleCeramique) return null; // GLB moule céramique via InstancierModeleMouleCeramique
        else if (id == IdObjetChamotte) return null; // GLB chamotte via InstancierModeleChamotte
        else if (id == IdObjetPinceOs) return null; // GLB pince en os via InstancierModelePinceOs
        else if (id == IdObjetTorchie) return null; // GLB torchie via InstancierModeleTorchie
        else if (id == IdObjetFourTorchie) return null; // GLB four via InstancierModeleFourTorchie
        else if (id == IdObjetMortierPilonBois) return null; // GLB mortier+pilon via InstancierModeleMortierPilonBois
        else if (id == IdObjetAtelleJambe) return null; // GLB res://Modeles/soin/Atelle_jambe.glb via InstancierModeleAtelleJambe
        else if (id == IdObjetAtelleBras) return null; // GLB res://Modeles/soin/Atelle_Bras.glb via InstancierModeleAtelleBras
        else if (id == IdObjetBandageTier1) return null; // GLB res://Modeles/soin/Bandage_tier1.glb via InstancierModeleBandageTier1
        else if (EstIdFondation(id) || EstIdPlancher(id) || EstIdMuret(id) || EstIdMurBois(id) || EstIdToitChaume(id)) return null; // GLB via InstancierModeleFondation / InstancierModeleSol* / InstancierModeleMuretBois / InstancierModeleMurBois / InstancierModeleToitChaume
        else if (id == IdObjetTorche) return null; // GLB res://Modeles/Equipements/torch.glb via InstancierModeleTorche
        else if (id == IdObjetFenetreBois) return null; // GLB res://Modeles/materials/travailler/fenetre.glb via InstancierModeleFenetreBois
        else if (id == IdObjetTableBoisDecorative) return null; // GLB res://Modeles/materials/moblier/table.glb via InstancierModeleTableBoisDecorative
        else if (id == IdObjetTableArtisanaTier1) return null; // GLB res://Modeles/Ateliers/table_artisana_tiere1.glb via InstancierModeleTableArtisanaTier1
        else if (id == 30 || id == 32)
        {
            CalculerDimensionsBoisPose(id, indexMorpho, indexTaille, out float br, out float bl, out _, out _);
            return GenererMeshBoisFendu(br, bl, indexMorpho);
        }
        else if (id == 34) return new QuadMesh { Size = new Vector2(0.12f, 0.18f) }; // Feuilles (GLB bouleau/chêne via InstancierModeleFeuilleArrachee)
        else if (id == IdObjetBaie) return new SphereMesh { Radius = 0.05f, Height = 0.10f, RadialSegments = 10, Rings = 6 };
        if (Atlas_Matiere.EstIdVoxelSurfaceTerrain(id))
            return new BoxMesh { Size = new Vector3(0.2f, 0.2f, 0.2f) };
        return null;
    }

    public static void AppliquerMaterielObjet(MeshInstance3D visuel, int idObjet, int indexChimique, int indexMorphologique = 0, int niveauTressage = 0, byte indexBotanique = LSystem_Botanique.IndexChene)
    {
        // FIX CRITIQUE : Ne JAMAIS Ã©craser le matÃ©riau d'un outil forgÃ© (il possÃ¨de ses propres surfaces cuites)
        if (idObjet == 100)
        {
            visuel.MaterialOverride = null;
            return;
        }
        if (idObjet == 15 || idObjet == 16 || idObjet == 17)
        {
            visuel.MaterialOverride = Atlas_Matiere.ObtenirProfilFlexible(idObjet, out var pf)
                ? new StandardMaterial3D { AlbedoColor = pf.CouleurCorde, Roughness = 0.9f, Metallic = 0f }
                : new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.55f, 0.15f), Roughness = 0.9f };
            return;
        }
        if (idObjet == 20 || idObjet == 21 || idObjet == IdObjetCeinturePoches || idObjet == IdObjetCeintureSacoches || idObjet == IdObjetPochetteTier0 || idObjet == IdObjetSacTier0)
        {
            bool varianteHerbeSolide = indexBotanique == TagVarianteHerbeSolide
                || (indexChimique == 15 && indexMorphologique == 15 && indexBotanique >= 2);
            bool varianteLiane = indexBotanique == TagVarianteLiane
                || (indexChimique == 16 && indexMorphologique == 16 && indexBotanique < 2);
            bool varianteIntestin = indexBotanique == TagVarianteIntestin;
            bool varianteIntestinSolide = indexBotanique == TagVarianteIntestinSolide;

            int matA = indexChimique;
            int matB = indexMorphologique;
            int niveauAspect = niveauTressage;

            if (varianteHerbeSolide)
            {
                // ForÃ§age visuel cohÃ©rent: toute variante herbe solide rend comme une ligature d'herbe solide tier 2.
                matA = 15;
                matB = 15;
                niveauAspect = Mathf.Max(niveauAspect, 2);
            }
            else if (varianteLiane)
            {
                matA = 16;
                matB = 16;
            }
            else if (varianteIntestin || varianteIntestinSolide)
            {
                matA = 17;
                matB = 17;
                if (varianteIntestinSolide)
                    niveauAspect = Mathf.Max(niveauAspect, 2);
            }

            visuel.MaterialOverride = Atlas_Matiere.ObtenirMaterielCorde(matA, matB, niveauAspect);
            return;
        }
        if (idObjet == 30 || idObjet == 32 || (idObjet == BlocChutant.ID_BRANCHE && indexMorphologique != 1))
        {
            visuel.MaterialOverride = idObjet == 32 && indexChimique == 1 && indexBotanique == LSystem_Botanique.IndexChene
                ? ArbreVivant.ObtenirMaterielBoisTriplanarBatonChenEPale()
                : ArbreVivant.ObtenirMaterielBoisTriplanar(indexBotanique);
            return;
        }
        if (idObjet == IdObjetPitFeu || idObjet == IdObjetPitFeuRoche)
        {
            visuel.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(indexBotanique);
            return;
        }
        if (idObjet == 10 || idObjet == 11)
        {
            visuel.MaterialOverride = null; // Le mesh buisson porte dÃ©jÃ  son matÃ©riau procÃ©dural.
            return;
        }
        if (idObjet == IdObjetAloeVera)
        {
            visuel.MaterialOverride = null; // Le mesh aloe porte dÃ©jÃ  son matÃ©riau procÃ©dural.
            return;
        }
        if (idObjet == BlocChutant.ID_BRANCHE)
        {
            visuel.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(indexBotanique);
            return;
        }
        if (idObjet == 34)
        {
            if (BlocChutant.EssenceUtiliseFeuilleGlb(indexBotanique))
                visuel.MaterialOverride = null;
            else
                visuel.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.55f, 0.15f), Roughness = 0.95f, Metallic = 0f };
            return;
        }
        if (idObjet == IdObjetBaie)
        {
            Color c = ObtenirCouleurAlbedoBaie(indexChimique);
            visuel.MaterialOverride = new StandardMaterial3D { AlbedoColor = c, Roughness = 0.34f, Metallic = 0f, EmissionEnabled = true, Emission = c * 0.06f };
            return;
        }
        if (Atlas_Matiere.EstIdVoxelSurfaceTerrain(idObjet))
        {
            Color albedo = idObjet == Atlas_Matiere.IdVoxelSableQuartz
                ? new Color(0.92f, 0.90f, 0.87f)
                : new Color(0.42f, 0.3f, 0.2f);
            visuel.MaterialOverride = new StandardMaterial3D { AlbedoColor = albedo, Roughness = 1f, Metallic = 0f };
            return;
        }
        int chimique = Mathf.Clamp(indexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        if (ItemPhysique.EstIdRocheMatiere(idObjet))
            chimique = ItemPhysique.IndexChimiqueDepuisIdRoche(idObjet);
        visuel.MaterialOverride = ItemPhysique.CreerMaterielProcedural(ItemPhysique.EstMatiereSilexParIdObjet(idObjet), chimique);
    }
}
