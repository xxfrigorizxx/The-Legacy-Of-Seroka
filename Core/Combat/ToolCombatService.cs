using Godot;
using System;

public partial class Joueur
{
    private const float DureeMinageMainNueSecondes = 3.0f;
    private const float DureeMinagePiochePierreSecondes = 4.0f;
    private const float IntervalleParticulesMinageMainNue = 0.12f;
    private const float DureeGracePerteCibleMinageSecondes = 0.24f;
    private const float DureeRecuperationAtelierMainNue = 5.0f;
    private const float DureeRecuperationAtelierHachette = 2.85f;
    private const float DureeRecuperationRackMainNue = 2.8f;
    private const float DureeRecuperationRackHachette = 1.25f;
    private const float DureeRecuperationFondationBoisHachette = 15.0f;
    private const float DureeRecuperationFondationRochePioche = 15.0f;
    private const float DureeRecuperationFondationMixteOutil = 15.0f;
    private const float IntervalleParticulesRecuperationAtelier = 0.14f;
    private float _progressionMinageMainNue;
    private float _cooldownParticulesMinageMainNue;
    private float _tempsPerteCibleMinage;
    private bool _aCibleMinageActive;
    private Vector3 _pointCibleMinage;
    private Vector3 _normaleCibleMinage = Vector3.Up;
    private int _idCibleMinage = -1;
    private float _progressionRecuperationAtelier;
    private float _cooldownParticulesRecuperationAtelier;
    private float _cooldownMessageRecuperationFondation;
    private float _cooldownMessageEtatBrasAction;
    private float _cooldownMessageInventairePleinMinage;
    private ItemPhysique _atelierCibleRecuperation;
    private const float DureeRecolteBuissonOutilSecondes = 3.0f;
    private const float DureeRecolteAloeDagueSecondes = 1.0f;
    private const float DureeRecolteLianeDagueSecondes = 2.0f;
    private const float RayonDetectionBuisson = 1.25f;
    private const float DistanceMaxViseeDirecteBuisson = 0.55f;
    private const float IntervalleParticulesMinageBuisson = 0.11f;
    private const float IntervalleParticulesMinageLiane = 0.10f;
    private const float DureeDepecageDagueCadavreSecondes = 3.0f;
    private const float IntervalleParticulesDepecageCadavre = 0.10f;
    private float _progressionRecolteBuisson;
    private float _cooldownParticulesMinageBuisson;
    private float _progressionRecolteLianeDague;
    private float _cooldownParticulesMinageLiane;
    private float _progressionDepecageCadavreDague;
    private float _cooldownParticulesDepecageCadavre;
    private float _tempsPerteCibleDepecageCadavre;
    private float _tempsPerteCibleLiane;
    private Vector3 _pointRecolteBuisson;
    private Vector3 _pointRecolteLiane;
    private Vector3 _pointDepecageCadavre;

    private Vector3I _posBuissonRecolte;
    private bool _aCibleBuissonRecolte;
    private ArbreVivant _arbreCibleLiane;
    private BoeufSauvage _boeufCadavreCibleDepecage;
    private bool _bloquerActionClicGaucheApresMinageBuisson;
    private bool _bloquerActionClicGaucheApresDepecage;

    private static bool EstMatiereMinableMainNue(int idMatiere)
    {
        // Main nue : sable + terres + neige (ID 5).
        return idMatiere == 1 || idMatiere == 3 || idMatiere == Atlas_Matiere.IdVoxelSableQuartz
            || idMatiere == 5 || idMatiere == 6 || idMatiere == 7 || idMatiere == 8 || idMatiere == 9;
    }

    private static bool EstMatiereMinablePioche(int idMatiere)
    {
        // Pioche : roche voxel + minerais de terrain (IDs 10–29, 32–48).
        return idMatiere == 2 || Atlas_Matiere.EstIdVoxelTerrainMinerai(idMatiere);
    }

    private void AttribuerXpMetierExtractionTerrain(int idMatiereExtraite)
    {
        // Distribution métier basée sur la matière réellement modifiée.
        if (idMatiereExtraite == 2 || Atlas_Matiere.EstIdVoxelTerrainMinerai(idMatiereExtraite))
            AjouterXpMetier("Mineur", 1UL);
        else if (idMatiereExtraite == 1 || idMatiereExtraite == 3 || idMatiereExtraite == Atlas_Matiere.IdVoxelSableQuartz
            || idMatiereExtraite == 5 || idMatiereExtraite == 6 || idMatiereExtraite == 7 || idMatiereExtraite == 8 || idMatiereExtraite == 9)
            AjouterXpMetier("Terrassier", 1UL);
        else
            return;

        AjouterXpFutureState("Force", 1UL);
    }

    /// <summary>Roche matière plate, ovale ou en pointe : même convention que l’entaille d’<see cref="ArbreVivant"/> vivant.</summary>
    private static bool EstRocheTranchantePourBois(SlotInventaire slot)
    {
        return !slot.EstVide && ItemPhysique.EstIdRocheMatiere(slot.ID)
            && (slot.IndexMorphologique == 1 || slot.IndexMorphologique == 2 || slot.IndexMorphologique == 3);
    }

    /// <summary>Roche plate (1) ou en pointe (3) : fauchage du gazon au clic sur le sol.</summary>
    private static bool EstRocheFaucheuseEnMain(SlotInventaire slot)
    {
        if (slot.EstVide || !ItemPhysique.EstIdRocheMatiere(slot.ID))
            return false;
        int forme = Mathf.Clamp(slot.IndexMorphologique, 0, 3);
        return forme == 1 || forme == 3;
    }

    /// <summary>Dague, faux, roche plate/pointe ou éclat — outils de fauchage (pas la hachette 106).</summary>
    private static bool EstOutilFaucheurEnMain(SlotInventaire slot)
    {
        if (slot.EstVide)
            return false;
        return slot.ID == 105
            || slot.ID == IdObjetFauxPierreTier0
            || EstRocheFaucheuseEnMain(slot)
            || (slot.EstUnEclat && slot.MeshEclat != null);
    }

    /// <summary>Surface assez horizontale pour faucher même si le raycast touche une fibre posée au sol.</summary>
    private bool EstSurfaceHorizontaleFauchable(float normaleMinY = 0.32f)
    {
        return _rayon != null && _rayon.IsColliding() && _rayon.GetCollisionNormal().Y >= normaleMinY;
    }

    /// <summary>
    /// Cadavre d'arbre : l'essence est normalement sur le <see cref="RigidBody3D"/> ; si la méta a été perdue,
    /// on la relit sur l'enfant « Bois » (copié à la chute depuis <see cref="ArbreVivant"/>).
    /// </summary>
    private static byte LireIndexBotaniqueBoisSurRigid(RigidBody3D rb)
    {
        if (rb == null) return LSystem_Botanique.IndexChene;
        if (rb.HasMeta("IndexBotanique"))
            return (byte)Mathf.Clamp(rb.GetMeta("IndexBotanique").AsInt32(), 0, 255);
        var bois = rb.GetNodeOrNull<MeshInstance3D>("Bois");
        if (bois != null && bois.HasMeta("IndexBotanique"))
            return (byte)Mathf.Clamp(bois.GetMeta("IndexBotanique").AsInt32(), 0, 255);
        return LSystem_Botanique.IndexChene;
    }

    /// <summary>Répare la méta sur le corps si elle manque encore (anciens cadavres) mais que « Bois » la porte.</summary>
    private static void ReparerMetaIndexBotaniqueSurCadavreSiPossible(RigidBody3D cadavre)
    {
        if (cadavre == null || cadavre.HasMeta("IndexBotanique")) return;
        var bois = cadavre.GetNodeOrNull<MeshInstance3D>("Bois");
        if (bois != null && bois.HasMeta("IndexBotanique"))
            cadavre.SetMeta("IndexBotanique", bois.GetMeta("IndexBotanique"));
    }

    private void ReinitialiserMinageMainNueProgression()
    {
        _progressionMinageMainNue = 0f;
        _cooldownParticulesMinageMainNue = 0f;
        _tempsPerteCibleMinage = 0f;
        _aCibleMinageActive = false;
        _pointCibleMinage = Vector3.Zero;
        _normaleCibleMinage = Vector3.Up;
        _idCibleMinage = -1;
        _progressionRecuperationAtelier = 0f;
        _cooldownParticulesRecuperationAtelier = 0f;
        _cooldownMessageRecuperationFondation = 0f;
        _cooldownMessageEtatBrasAction = 0f;
        _atelierCibleRecuperation = null;
        _progressionRecolteBuisson = 0f;
        _cooldownParticulesMinageBuisson = 0f;
        _aCibleBuissonRecolte = false;
        _pointRecolteBuisson = Vector3.Zero;
        _posBuissonRecolte = default;
        _bloquerActionClicGaucheApresMinageBuisson = false;
        ReinitialiserDepecageCadavreDagueProgression();
        ReinitialiserMinageLianeDagueProgression();
    }

    private EtatOsSimple ObtenirEtatOsBrasMainActive()
    {
        string sectionBras = MainGaucheEstActive ? SectionCorpsBrasGauche : SectionCorpsBrasDroit;
        return EvaluerEtatEffectifMembre(sectionBras);
    }

    private void AfficherMessageEtatBrasAction(string message)
    {
        if (_cooldownMessageEtatBrasAction > 0f)
            return;
        _cooldownMessageEtatBrasAction = 0.8f;
        GD.Print(message);
    }

    private float ObtenirMultiplicateurDegatsFrappeSelonEtatOsBras()
    {
        EtatOsSimple etatBras = ObtenirEtatOsBrasMainActive();
        if (etatBras == EtatOsSimple.Casse)
            return 0f;
        return etatBras == EtatOsSimple.Felure ? 0.5f : 1f;
    }





}
