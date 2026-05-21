using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public partial class Joueur
{
    private const int VersionPersistenceJoueur = 7;
    private const int VersionPersistenceObjetsPoses = 4;
    private const int VersionPersistenceProgression = 3;
    private bool _persistantPhaseJoueurChargee;
    private bool _persistantObjetsSolCharges;
    private int _objetsPosesAttendusDernierChargement;
    private int _objetsPosesSpawnesDernierChargement;
    /// <summary>Évite d’écrire <c>placed_objects.dat</c> pendant le remplacement des objets (fenêtre vide = tout effacé au disque).</summary>
    private bool _chargementObjetsPosesMondeEnCours;
    private const string NomFichierObjetsPosesLegacy = "placed_objects.dat";
    private const string NomFichierBlocsChutantsLegacy = "dropped_blocks.dat";

    private static string ObtenirCheminDossierSauvegardeMonde()
    {
        string nomMonde = GameState.Instance?.NomMondeActuel ?? "MonMonde";
        return ProjectSettings.GlobalizePath($"user://saves/{nomMonde}");
    }

    private int ObtenirDimensionLocaleActiveId()
    {
        if (_gestionnaireMonde != null && GodotObject.IsInstanceValid(_gestionnaireMonde))
            return _gestionnaireMonde.ObtenirDimensionLocaleActiveId();
        Node racine = Engine.GetMainLoop() is SceneTree arbre ? arbre.CurrentScene : null;
        Gestionnaire_Monde gestionnaire = racine?.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
        return gestionnaire != null ? gestionnaire.ObtenirDimensionLocaleActiveId() : (int)DimensionJeu.Alpha;
    }

    private string ObtenirSuffixeDimensionCanoniqueActive()
    {
        return ConstantesDimensions.ObtenirNomCanonique(ObtenirDimensionLocaleActiveId());
    }

    private Gestionnaire_Monde ObtenirGestionnaireMondePersistant()
    {
        if (_gestionnaireMonde != null && GodotObject.IsInstanceValid(_gestionnaireMonde))
            return _gestionnaireMonde;
        Node racine = Engine.GetMainLoop() is SceneTree arbre ? arbre.CurrentScene : null;
        return racine?.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
    }

    private static string ObtenirCheminObjetsPosesPourDimension(int dimensionId)
    {
        string dossier = ObtenirCheminDossierSauvegardeMonde();
        return Path.Combine(dossier, ObtenirNomFichierObjetsPosesDimension(ConstantesDimensions.ObtenirNomCanonique(dimensionId)));
    }

    /// <summary>Vrai si le nœud est sous la racine 3D de la dimension donnée (ARAPA, PETA, OMEGA, DERATA).</summary>
    private bool EstNoeudDansDimension(Node n, int dimensionId)
    {
        if (n is not Node3D n3)
            return false;
        Gestionnaire_Monde gm = ObtenirGestionnaireMondePersistant();
        if (gm == null)
            return dimensionId == (int)DimensionJeu.Alpha;
        Node3D racine = gm.ObtenirRacineDimension(dimensionId);
        if (racine == null)
            return dimensionId == gm.ObtenirDimensionLocaleActiveId();
        if (racine == n3 || racine.IsAncestorOf(n3))
            return true;
        foreach (var info in ConstantesDimensions.Toutes())
        {
            if (info.Id == dimensionId)
                continue;
            Node3D autre = gm.ObtenirRacineDimension(info.Id);
            if (autre != null && (autre == n3 || autre.IsAncestorOf(n3)))
                return false;
        }
        return dimensionId == gm.ObtenirDimensionLocaleActiveId();
    }

    private bool EstNoeudDansDimensionActive(Node n)
        => EstNoeudDansDimension(n, ObtenirDimensionLocaleActiveId());

    private bool DoitAnnulerSauvegardeObjetsPosesIncomplete(string chemin, int nombreTrouve, int dimensionId)
    {
        if (!File.Exists(chemin))
            return false;
        int ancienNombre = LireNombreObjetsPosesDepuisFichier(chemin);
        if (ancienNombre <= 0)
            return false;

        int dimensionActive = ObtenirDimensionLocaleActiveId();
        bool estDimensionActive = dimensionId == dimensionActive;

        // Hors dimension active, les constructions ne sont pas respawnées en scène au boot (seul placed_objects.ARAPA.dat est chargé).
        // Toute sauvegarde globale (autosync post-restauration, pose d’objet, menu) ne doit jamais écraser OMEGA/PETA/etc. avec 0 entrée.
        if (!estDimensionActive && nombreTrouve == 0)
        {
            GD.Print($"ZERO-K : Sauvegarde {Path.GetFileName(chemin)} ignorée — 0 en scène, {ancienNombre} sur disque (dimension {ConstantesDimensions.ObtenirNomCanonique(dimensionId)} non active).");
            return true;
        }

        if (!estDimensionActive)
            return false;

        if (nombreTrouve == 0)
        {
            GD.PrintErr($"ZERO-K : Sauvegarde objets posés refusée — 0 en scène alors que {ancienNombre} sur disque (protection perte).");
            return true;
        }
        if (_persistantObjetsSolCharges)
        {
            if (nombreTrouve < ancienNombre
                && _objetsPosesAttendusDernierChargement > 0
                && _objetsPosesSpawnesDernierChargement < _objetsPosesAttendusDernierChargement)
            {
                GD.PrintErr($"ZERO-K : Sauvegarde objets posés refusée — {nombreTrouve} en scène vs {ancienNombre} disque (respawn incomplet).");
                return true;
            }
            return false;
        }
        return nombreTrouve < ancienNombre;
    }

    /// <summary>Ne pas écraser un fichier dimension hors scène active (même en-tête version + count que les objets posés).</summary>
    private bool DoitPreserverFichierPersistantDimensionInactive(string chemin, int nombreTrouve, int dimensionId)
    {
        if (nombreTrouve != 0 || dimensionId == ObtenirDimensionLocaleActiveId())
            return false;
        if (!File.Exists(chemin))
            return false;
        return LireNombreObjetsPosesDepuisFichier(chemin) > 0;
    }

    private static string ObtenirNomFichierObjetsPosesDimension(string suffixeDimensionCanonique)
        => $"placed_objects.{suffixeDimensionCanonique}.dat";

    private static string ObtenirNomFichierBlocsChutantsDimension(string suffixeDimensionCanonique)
        => $"dropped_blocks.{suffixeDimensionCanonique}.dat";

    private string ObtenirCheminObjetsPosesDimensionActive()
    {
        string dossier = ObtenirCheminDossierSauvegardeMonde();
        return Path.Combine(dossier, ObtenirNomFichierObjetsPosesDimension(ObtenirSuffixeDimensionCanoniqueActive()));
    }

    private string ObtenirCheminObjetsPosesLegacy()
    {
        return Path.Combine(ObtenirCheminDossierSauvegardeMonde(), NomFichierObjetsPosesLegacy);
    }

    private string ObtenirCheminBlocsChutantsDimensionActive()
    {
        string dossier = ObtenirCheminDossierSauvegardeMonde();
        return Path.Combine(dossier, ObtenirNomFichierBlocsChutantsDimension(ObtenirSuffixeDimensionCanoniqueActive()));
    }

    private string ObtenirCheminBlocsChutantsLegacy()
    {
        return Path.Combine(ObtenirCheminDossierSauvegardeMonde(), NomFichierBlocsChutantsLegacy);
    }

    private static void EcrireSlot(BinaryWriter w, SlotInventaire s)
    {
        w.Write(!s.EstVide);
        if (s.EstVide) return;
        w.Write(s.ID);
        w.Write(s.IndexMorphologique);
        w.Write(s.IndexChimique);
        w.Write(s.IndexTaille);
        w.Write(s.EstUnEclat);
        w.Write(s.NiveauFracture);
        w.Write(s.ScaleEclat.X);
        w.Write(s.ScaleEclat.Y);
        w.Write(s.ScaleEclat.Z);
        w.Write(s.IndexBotanique);
        w.Write(s.GenomeAssemblage ?? "");
        w.Write(s.DurabiliteOutilMax);
        w.Write(s.DurabiliteOutilActuelle);
        w.Write(s.IndexTailleLameRoche);
        w.Write(s.Quantite > 0 ? s.Quantite : 1);
        w.Write(s.CleConteneur ?? "");
    }

    private static void EcrireUInt128(BinaryWriter w, UInt128 valeur)
    {
        ulong low = (ulong)(valeur & ulong.MaxValue);
        ulong high = (ulong)(valeur >> 64);
        w.Write(low);
        w.Write(high);
    }

    private static UInt128 LireUInt128(BinaryReader r)
    {
        ulong low = r.ReadUInt64();
        ulong high = r.ReadUInt64();
        return ((UInt128)high << 64) | low;
    }

    private static SlotInventaire LireSlot(BinaryReader r, bool lireExtras)
    {
        if (!r.ReadBoolean()) return new SlotInventaire();
        var s = new SlotInventaire
        {
            ID = r.ReadInt32(),
            IndexMorphologique = r.ReadInt32(),
            IndexChimique = r.ReadInt32(),
            IndexTaille = r.ReadInt32(),
            EstUnEclat = r.ReadBoolean(),
            NiveauFracture = r.ReadInt32(),
            ScaleEclat = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
            IndexBotanique = r.ReadByte(),
            GenomeAssemblage = r.ReadString(),
            DurabiliteOutilMax = r.ReadSingle(),
            DurabiliteOutilActuelle = r.ReadSingle(),
            IndexTailleLameRoche = r.ReadInt32(),
            MeshEclat = null
        };
        if (lireExtras)
        {
            s.Quantite = r.ReadInt32();
            s.CleConteneur = r.ReadString();
        }
        else
        {
            s.Quantite = 1;
            s.CleConteneur = "";
        }
        // Les MeshEclat ne sont pas sérialisables facilement : repli propre vers un slot non-éclat.
        if (s.EstUnEclat && s.MeshEclat == null)
            s.EstUnEclat = false;
        return s;
    }

    /// <param name="arbreScene">Si le joueur n’est plus dans l’arbre (<see cref="Node._ExitTree"/>), passer le <see cref="SceneTree"/> du <c>Gestionnaire_Monde</c> parent.</param>
    public void SauvegarderEtatPersistantMonde(SceneTree arbreScene = null)
    {
        SceneTree tree = arbreScene;
        if (tree == null && IsInsideTree())
            tree = GetTree();
        if (tree == null)
            tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            GD.PrintErr("ZERO-K : Sauvegarde état monde annulée (SceneTree indisponible).");
            return;
        }

        SauvegarderProgressionJoueurMonde();
        SauvegarderInventaireMonde();
        SauvegarderCarnetSavoirMonde();
        SauvegarderObjetsPosesMonde(tree);
        SauvegarderBlocsChutantsMonde(tree);
        ObtenirGestionnaireFauneCourant(tree)?.SauvegarderFauneMonde();
    }

    /// <summary>
    /// Inventaire / progression / carnet uniquement — à utiliser quand l’arbre monde peut être en cours de destruction
    /// (ex. <see cref="Node._ExitTree"/> du joueur après ses frères <c>BlocsPoses</c>), pour ne pas écraser <c>placed_objects.dat</c> avec 0 entrée.
    /// </summary>
    private void SauvegarderFichiersJoueurHorsObjetsAuSol()
    {
        // Protection anti-écrasement : si la phase joueur n'a pas encore été restaurée, les slots sont
        // encore à l'état par défaut en mémoire. Les écrire ici viderait inventaire + carnet au disque.
        if (!_persistantPhaseJoueurChargee)
        {
            GD.Print("ZERO-K : Sauvegarde joueur ignorée (restauration joueur non initialisée).");
            return;
        }
        SauvegarderProgressionJoueurMonde();
        SauvegarderInventaireMonde();
        SauvegarderCarnetSavoirMonde();
    }

    public void SauvegarderEtatPersistantJoueurSeulement()
    {
        SauvegarderFichiersJoueurHorsObjetsAuSol();
    }

    /// <summary>Compat : n’exécute que la phase joueur (inventaire, progression, carnet). Les objets au sol sont chargés par <see cref="ChargerEtatPersistantPhaseObjetsAuSolEtFaune"/>.</summary>
    public void ChargerEtatPersistantMonde() => ChargerEtatPersistantPhaseJoueur();

    /// <summary>Phase A — dès le premier <c>CallDeferred</c> au chargement de scène.</summary>
    public void ChargerEtatPersistantPhaseJoueur()
    {
        if (_persistantPhaseJoueurChargee) return;
        _persistantPhaseJoueurChargee = true;
        ChargerProgressionJoueurMonde();
        ChargerInventaireMonde();
        bool carnetCharge = ChargerCarnetSavoirMonde();
        if (!carnetCharge)
            InitialiserCarnetParDefautSiAucuneDonnee();
        RafraichirHUD();
    }

    /// <summary>Phase B — après que le terrain sous le spawn soit prêt ; gèle les corps jusqu’à collision chunk sous chaque objet.</summary>
    public void ChargerEtatPersistantPhaseObjetsAuSolEtFaune()
    {
        if (PersistanceObjetsSolChargeeAvecSucces())
            return;
        try
        {
            ChargerObjetsPosesMonde();
            ChargerBlocsChutantsMonde();
            ObtenirGestionnaireFauneCourant(null)?.ChargerFauneMonde();
        }
        finally
        {
            MarquerPersistanceObjetsSolChargeeSiSucces();
        }
        RafraichirHUD();
    }

    public const string NomMethodeRechargerPersistanceDimensionDifferee = nameof(RechargerEtatPersistantDimensionActiveDiffere);

    /// <summary>Appel différé après changement de dimension (chunks réinitialisés).</summary>
    public void RechargerEtatPersistantDimensionActiveDiffere()
    {
        RechargerEtatPersistantDimensionActive();
    }

    /// <summary>Recharge l'état persistant lié à la dimension active (objets posés, blocs chutants, faune) après un transfert dimensionnel.</summary>
    public void RechargerEtatPersistantDimensionActive()
    {
        string nomDim = ConstantesDimensions.ObtenirNomCanonique(ObtenirDimensionLocaleActiveId());
        GD.Print($"ZERO-K : Rechargement persistance dimension active [{nomDim}]…");
        ChargerObjetsPosesMonde();
        ChargerBlocsChutantsMonde();
        ObtenirGestionnaireFauneCourant(null)?.ChargerFauneMonde();
        MarquerPersistanceObjetsSolChargeeSiSucces();
        RafraichirHUD();
    }

    private bool PersistanceObjetsSolChargeeAvecSucces()
    {
        if (!_persistantObjetsSolCharges)
            return false;
        if (_objetsPosesAttendusDernierChargement <= 0)
            return true;
        return _objetsPosesSpawnesDernierChargement >= _objetsPosesAttendusDernierChargement;
    }

    private void MarquerPersistanceObjetsSolChargeeSiSucces()
    {
        if (_objetsPosesAttendusDernierChargement == 0 || _objetsPosesSpawnesDernierChargement >= _objetsPosesAttendusDernierChargement)
            _persistantObjetsSolCharges = true;
        else
            GD.PrintErr($"ZERO-K : Persistance objets sol incomplète ({_objetsPosesSpawnesDernierChargement}/{_objetsPosesAttendusDernierChargement}) — nouvel essai possible.");
    }

    private void EnregistrerGelRestaurationSolSiBesoin(Node3D noeud)
    {
        if (noeud is RigidBody3D rb)
            _gestionnaireMonde?.EnregistrerRigidBodyRestaurationSolAuChargement(rb);
    }

    private void AjouterBlocChutantAuParent(BlocChutant bloc, Vector3 positionGlobale, Vector3 rotationGlobaleDegres)
    {
        if (bloc == null || !GodotObject.IsInstanceValid(bloc))
            return;

        Node parent = GetParent();
        if (_gestionnaireMonde != null && GodotObject.IsInstanceValid(_gestionnaireMonde))
        {
            Node3D racineDim = _gestionnaireMonde.ObtenirRacineDimension(_gestionnaireMonde.ObtenirDimensionLocaleActiveId());
            if (racineDim != null)
                parent = racineDim;
        }
        if (parent == null || !GodotObject.IsInstanceValid(parent))
        {
            bloc.QueueFree();
            return;
        }

        parent.AddChild(bloc);
        bloc.GlobalPosition = positionGlobale;
        bloc.GlobalRotationDegrees = rotationGlobaleDegres;
        EnregistrerGelRestaurationSolSiBesoin(bloc);
    }

    private GestionnaireFauneBoeufs ObtenirGestionnaireFauneCourant(SceneTree arbreScene)
    {
        if (_gestionnaireMonde != null && GodotObject.IsInstanceValid(_gestionnaireMonde))
        {
            var depuisMonde = _gestionnaireMonde.GetNodeOrNull<GestionnaireFauneBoeufs>("GestionnaireFauneBoeufs");
            if (depuisMonde != null)
                return depuisMonde;
        }
        SceneTree tree = arbreScene;
        if (tree == null && IsInsideTree())
            tree = GetTree();
        if (tree == null)
            tree = Engine.GetMainLoop() as SceneTree;
        Node scene = tree?.CurrentScene;
        if (scene == null) return null;
        return scene.GetNodeOrNull<GestionnaireFauneBoeufs>("GestionnaireFauneBoeufs");
    }

    private void SauvegarderProgressionJoueurMonde()
    {
        try
        {
            string dossier = ObtenirCheminDossierSauvegardeMonde();
            Directory.CreateDirectory(dossier);
            string chemin = Path.Combine(dossier, "player_progression.dat");
            using var w = new BinaryWriter(File.Open(chemin, FileMode.Create));
            w.Write(VersionPersistenceProgression);
            w.Write(_futureStates.Count);
            foreach (var kv in _futureStates)
            {
                w.Write(kv.Key ?? "");
                w.Write(kv.Value);
                EcrireUInt128(w, ObtenirXpFutureState(kv.Key));
            }
            w.Write(_metiers.Count);
            foreach (var kv in _metiers)
            {
                w.Write(kv.Key ?? "");
                w.Write(kv.Value);
                EcrireUInt128(w, ObtenirXpMetier(kv.Key));
            }
            w.Write(_degatsCumulesConstitution);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur sauvegarde progression joueur : {ex.Message}");
        }
    }

    private void ChargerProgressionJoueurMonde()
    {
        try
        {
            string chemin = Path.Combine(ObtenirCheminDossierSauvegardeMonde(), "player_progression.dat");
            if (!File.Exists(chemin))
                return;
            using var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
            int version = r.ReadInt32();
            if (version < 1 || version > VersionPersistenceProgression)
                return;
            _futureStates.Clear();
            _futureStateXp.Clear();
            int nStats = Mathf.Max(0, r.ReadInt32());
            for (int i = 0; i < nStats; i++)
            {
                string nom = r.ReadString();
                ulong niveau = r.ReadUInt64();
                UInt128 xp = version >= 2 ? LireUInt128(r) : r.ReadUInt64();
                if (string.IsNullOrWhiteSpace(nom))
                    continue;
                _futureStates[nom] = Math.Min(niveau, NiveauMaxFutureState);
                _futureStateXp[nom] = xp;
            }
            _metiers.Clear();
            _metierXp.Clear();
            int nMetiers = Mathf.Max(0, r.ReadInt32());
            for (int i = 0; i < nMetiers; i++)
            {
                string nom = r.ReadString();
                ulong niveau = r.ReadUInt64();
                UInt128 xp = version >= 2 ? LireUInt128(r) : r.ReadUInt64();
                if (string.IsNullOrWhiteSpace(nom))
                    continue;
                _metiers[nom] = Math.Min(niveau, NiveauMaxFutureState);
                _metierXp[nom] = xp;
            }
            _degatsCumulesConstitution = version >= 3 ? r.ReadUInt64() : 0UL;
            AjouterFutureStateSiAbsent("Force", 0UL);
            AjouterFutureStateSiAbsent("Constitution", 0UL);
            AjouterFutureStateSiAbsent("Dextiriter", 0UL);
            AjouterFutureStateSiAbsent("Agiliter", 0UL);
            AjouterFutureStateSiAbsent("Metaboliste", 0UL);
            AjouterFutureStateSiAbsent("Intelligence", 0UL);
            AjouterMetierSiAbsent("Bucheron", 0UL);
            AjouterMetierSiAbsent("Traisage", 0UL);
            AjouterMetierSiAbsent("Artisana", 0UL);
            AjouterMetierSiAbsent("Batisseur", 0UL);
            AjouterMetierSiAbsent("Mineur", 0UL);
            AjouterMetierSiAbsent("Forgeron", 0UL);
            AjouterMetierSiAbsent("Terrassier", 0UL);
            AjouterMetierSiAbsent("Cuisinier", 0UL);
            AjouterMetierSiAbsent("Boucher", 0UL);
            AjouterMetierSiAbsent("Chasseur", 0UL);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur chargement progression joueur : {ex.Message}");
        }
    }

    private void AppliquerProgressionEtMetiersParDefaut()
    {
        _futureStates.Clear();
        _futureStateXp.Clear();
        _metiers.Clear();
        _metierXp.Clear();
        _degatsCumulesConstitution = 0;
        AjouterFutureStateSiAbsent("Force", 0UL);
        AjouterFutureStateSiAbsent("Constitution", 0UL);
        AjouterFutureStateSiAbsent("Dextiriter", 0UL);
        AjouterFutureStateSiAbsent("Agiliter", 0UL);
        AjouterFutureStateSiAbsent("Metaboliste", 0UL);
        AjouterFutureStateSiAbsent("Intelligence", 0UL);
        AjouterMetierSiAbsent("Bucheron", 0UL);
        AjouterMetierSiAbsent("Traisage", 0UL);
        AjouterMetierSiAbsent("Artisana", 0UL);
        AjouterMetierSiAbsent("Batisseur", 0UL);
        AjouterMetierSiAbsent("Mineur", 0UL);
        AjouterMetierSiAbsent("Forgeron", 0UL);
        AjouterMetierSiAbsent("Terrassier", 0UL);
        AjouterMetierSiAbsent("Cuisinier", 0UL);
        AjouterMetierSiAbsent("Boucher", 0UL);
        AjouterMetierSiAbsent("Chasseur", 0UL);
    }

    /// <summary>Nouveau personnage en mémoire après mort (monde / chunks / objets posés inchangés).</summary>
    public void ReinitialiserEtatJoueurNouveauPersonnageMemeMonde()
    {
        MainGauche = new SlotInventaire();
        MainDroite = new SlotInventaire();
        EquipementSacDos = new SlotInventaire();
        EquipementCeinture = new SlotInventaire();
        EquipementCarnet = new SlotInventaire();
        MainGaucheEstActive = true;
        for (int i = 0; i < GrilleCraftPoche.Length; i++)
            GrilleCraftPoche[i] = new SlotInventaire();
        GrilleSacStockage = new SlotInventaire[1];
        for (int i = 0; i < GrilleCeintureStockage.Length; i++)
            GrilleCeintureStockage[i] = new SlotInventaire();
        _memoireStockageSacs.Clear();
        SlotResultatCraft = new SlotInventaire();
        AppliquerProgressionEtMetiersParDefaut();
        ImporterCraftsDecouverts(Array.Empty<string>());
        ReinitialiserCarnetSavoirParDefaut();
        InitialiserSanteCorps();
        _faimJoueur = FaimMaxJoueur;
        _enduranceJoueur = EnduranceMaxJoueur;
        _timerAtelleJambeGaucheRestant = 0f;
        _timerAtelleJambeDroiteRestant = 0f;
        _timerAtelleBrasGaucheRestant = 0f;
        _timerAtelleBrasDroitRestant = 0f;
        ReinitialiserEffetBandageTier1();
        RafraichirHUD();
        SauvegarderEtatPersistantJoueurSeulement();
    }

    private void SauvegarderInventaireMonde()
    {
        if (!_persistantPhaseJoueurChargee)
        {
            GD.Print("ZERO-K : Sauvegarde inventaire ignorée (restauration joueur non initialisée).");
            return;
        }
        try
        {
            string dossier = ObtenirCheminDossierSauvegardeMonde();
            Directory.CreateDirectory(dossier);
            string chemin = Path.Combine(dossier, "player_inventory.dat");
            using var w = new BinaryWriter(File.Open(chemin, FileMode.Create));
            w.Write(VersionPersistenceJoueur);
            SauvegarderStockageSacEquipeDansMemoire();
            SauvegarderStockageCeintureSacochesEquipeDansMemoire();
            EcrireSlot(w, MainGauche);
            EcrireSlot(w, MainDroite);
            EcrireSlot(w, EquipementSacDos);
            EcrireSlot(w, EquipementCeinture);
            EcrireSlot(w, EquipementCarnet);
            w.Write(MainGaucheEstActive);
            for (int i = 0; i < 4; i++)
                EcrireSlot(w, i < GrilleCraftPoche.Length ? GrilleCraftPoche[i] : new SlotInventaire());
            w.Write(_memoireStockageSacs.Count);
            foreach (var kv in _memoireStockageSacs)
            {
                w.Write(kv.Key ?? "");
                int n = kv.Value != null ? kv.Value.Length : 0;
                w.Write(n);
                for (int i = 0; i < n; i++)
                    EcrireSlot(w, kv.Value[i]);
            }
            string[] craftsDecouverts = ExporterCraftsDecouverts();
            w.Write(craftsDecouverts.Length);
            for (int i = 0; i < craftsDecouverts.Length; i++)
                w.Write(craftsDecouverts[i] ?? "");

            // Etat survie/corps: persiste explicitement pour éviter toute "régénération" au relog.
            w.Write(_pvTete);
            w.Write(_pvTorse);
            w.Write(_pvBrasGauche);
            w.Write(_pvBrasDroit);
            w.Write(_pvJambeGauche);
            w.Write(_pvJambeDroite);
            w.Write(_integriteOsTete);
            w.Write(_integriteOsTorse);
            w.Write(_integriteOsBrasGauche);
            w.Write(_integriteOsBrasDroit);
            w.Write(_integriteOsJambeGauche);
            w.Write(_integriteOsJambeDroite);
            w.Write(_faimJoueur);
            w.Write(_enduranceJoueur);
            w.Write(_timerAtelleJambeGaucheRestant);
            w.Write(_timerAtelleJambeDroiteRestant);
            w.Write(_timerAtelleBrasGaucheRestant);
            w.Write(_timerAtelleBrasDroitRestant);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur sauvegarde inventaire joueur : {ex.Message}");
        }
    }

    private void ChargerInventaireMonde()
    {
        try
        {
            string chemin = Path.Combine(ObtenirCheminDossierSauvegardeMonde(), "player_inventory.dat");
            if (!File.Exists(chemin)) return;
            using var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
            int version = r.ReadInt32();
            if (version < 1 || version > VersionPersistenceJoueur)
            {
                GD.PrintErr($"ZERO-K : player_inventory.dat version {version} non prise en charge (max {VersionPersistenceJoueur}) — inventaire sur disque conservé en mémoire par défaut.");
                return;
            }
            bool lireExtras = version >= 2;

            MainGauche = LireSlot(r, lireExtras);
            MainDroite = LireSlot(r, lireExtras);
            EquipementSacDos = LireSlot(r, lireExtras);
            EquipementCeinture = LireSlot(r, lireExtras);
            EquipementCarnet = version >= 3 ? LireSlot(r, true) : new SlotInventaire();
            MainGaucheEstActive = r.ReadBoolean();
            for (int i = 0; i < 4 && i < GrilleCraftPoche.Length; i++)
                GrilleCraftPoche[i] = LireSlot(r, lireExtras);
            if (version >= 2)
            {
                int nSacs = r.ReadInt32();
                _memoireStockageSacs.Clear();
                for (int i = 0; i < nSacs; i++)
                {
                    string cle = r.ReadString();
                    int n = r.ReadInt32();
                    var slots = new SlotInventaire[Mathf.Max(1, n)];
                    for (int s = 0; s < slots.Length; s++)
                        slots[s] = LireSlot(r, true);
                    if (!string.IsNullOrEmpty(cle))
                        _memoireStockageSacs[cle] = slots;
                }
            }
            if (version >= 5)
            {
                int nCrafts = Mathf.Max(0, r.ReadInt32());
                var clesCrafts = new string[nCrafts];
                for (int i = 0; i < nCrafts; i++)
                    clesCrafts[i] = r.ReadString();
                ImporterCraftsDecouverts(clesCrafts);
            }
            else if (version >= 4)
            {
                int nCrafts = Mathf.Max(0, r.ReadInt32());
                ImporterCraftsDecouverts(Array.Empty<string>());
                for (int i = 0; i < nCrafts; i++)
                {
                    int idLegacy = r.ReadInt32();
                    DebloquerCraft(idLegacy);
                }
            }
            else
            {
                ImporterCraftsDecouverts(Array.Empty<string>());
            }
            if (version >= 6)
            {
                _pvTete = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirPvMaxSectionCorps(SectionCorpsTete));
                _pvTorse = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirPvMaxSectionCorps(SectionCorpsTorse));
                _pvBrasGauche = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirPvMaxSectionCorps(SectionCorpsBrasGauche));
                _pvBrasDroit = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirPvMaxSectionCorps(SectionCorpsBrasDroit));
                _pvJambeGauche = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirPvMaxSectionCorps(SectionCorpsJambeGauche));
                _pvJambeDroite = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirPvMaxSectionCorps(SectionCorpsJambeDroite));
                _integriteOsTete = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirIntegriteOsBaseSection(SectionCorpsTete));
                _integriteOsTorse = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirIntegriteOsBaseSection(SectionCorpsTorse));
                _integriteOsBrasGauche = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirIntegriteOsBaseSection(SectionCorpsBrasGauche));
                _integriteOsBrasDroit = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirIntegriteOsBaseSection(SectionCorpsBrasDroit));
                _integriteOsJambeGauche = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirIntegriteOsBaseSection(SectionCorpsJambeGauche));
                _integriteOsJambeDroite = Mathf.Clamp(r.ReadSingle(), 0f, ObtenirIntegriteOsBaseSection(SectionCorpsJambeDroite));
                _faimJoueur = Mathf.Clamp(r.ReadSingle(), 0f, FaimMaxJoueur);
                _enduranceJoueur = Mathf.Clamp(r.ReadSingle(), 0f, EnduranceMaxJoueur);
                _timerAtelleJambeGaucheRestant = Mathf.Max(0f, r.ReadSingle());
                _timerAtelleJambeDroiteRestant = Mathf.Max(0f, r.ReadSingle());
                if (version >= 7)
                {
                    _timerAtelleBrasGaucheRestant = Mathf.Max(0f, r.ReadSingle());
                    _timerAtelleBrasDroitRestant = Mathf.Max(0f, r.ReadSingle());
                }
                else
                {
                    _timerAtelleBrasGaucheRestant = 0f;
                    _timerAtelleBrasDroitRestant = 0f;
                }
            }
            ChargerStockageDepuisSacEquipe();
            ChargerStockageDepuisCeintureSacochesEquipe();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur chargement inventaire joueur : {ex.Message}");
        }
    }

    private sealed class CarnetSavoirData
    {
        public string[] pages { get; set; } = Array.Empty<string>();
        public int page_index { get; set; }
    }

    private void SauvegarderCarnetSavoirMonde()
    {
        try
        {
            ObtenirEtatCarnetPourSauvegarde(out string[] pages, out int pageIndex);
            var data = new CarnetSavoirData
            {
                pages = pages ?? Array.Empty<string>(),
                page_index = pageIndex
            };

            string dossier = ObtenirCheminDossierSauvegardeMonde();
            Directory.CreateDirectory(dossier);
            string chemin = Path.Combine(dossier, "player_carnet_savoir.json");
            string json = JsonSerializer.Serialize(data);
            File.WriteAllText(chemin, json);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur sauvegarde carnet du savoir : {ex.Message}");
        }
    }

    private bool ChargerCarnetSavoirMonde()
    {
        try
        {
            string chemin = Path.Combine(ObtenirCheminDossierSauvegardeMonde(), "player_carnet_savoir.json");
            if (!File.Exists(chemin))
                return false;

            string json = File.ReadAllText(chemin);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            CarnetSavoirData data = JsonSerializer.Deserialize<CarnetSavoirData>(json);
            if (data == null)
                return false;

            DefinirEtatCarnetDepuisSauvegarde(data.pages ?? Array.Empty<string>(), data.page_index);
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur chargement carnet du savoir : {ex.Message}");
            return false;
        }
    }

    private bool EssayerConstruireSlotObjetPose(Node n, out SlotInventaire slot, out Vector3 pos, out Vector3 rotDeg)
    {
        slot = new SlotInventaire();
        pos = Vector3.Zero;
        rotDeg = Vector3.Zero;
        if (n is not Node3D n3) return false;
        pos = n3.GlobalPosition;
        rotDeg = n3.GlobalRotationDegrees;

        int id = n.HasMeta("ID_Matiere") ? (int)n.GetMeta("ID_Matiere").AsInt32() : 0;
        var item = n as ItemPhysique ?? n.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (item != null && item.ID_Objet != 0) id = item.ID_Objet;
        if (id == 0) return false;

        Vector3 scaleSlot = item != null ? item.Scale : Vector3.One;
        if (item != null && (id == 30 || id == 32) && item.HasMeta("ScaleLongueurBois"))
            scaleSlot = new Vector3(1f, 1f, (float)item.GetMeta("ScaleLongueurBois").AsDouble());

        slot = new SlotInventaire
        {
            ID = id,
            IndexMorphologique = item?.IndexCacheMemoire ?? 0,
            IndexChimique = item?.IndexChimique ?? 0,
            IndexTaille = item?.IndexTailleRoche ?? 2,
            EstUnEclat = item?.EstUnEclat ?? false,
            MeshEclat = null,
            NiveauFracture = item?.NiveauFracture ?? 0,
            ScaleEclat = scaleSlot,
            IndexBotanique = item != null ? item.IndexBotanique : LSystem_Botanique.IndexChene,
            GenomeAssemblage = item?.GenomeAssemblage ?? "",
            CleConteneur = (item != null && item.HasMeta("CleConteneur")) ? item.GetMeta("CleConteneur").AsString() : "",
            DurabiliteOutilMax = (item != null && item.HasMeta(MetaDurabiliteOutilMax)) ? (float)item.GetMeta(MetaDurabiliteOutilMax).AsDouble() : 0f,
            DurabiliteOutilActuelle = (item != null && item.HasMeta(MetaDurabiliteOutilActuelle)) ? (float)item.GetMeta(MetaDurabiliteOutilActuelle).AsDouble() : 0f,
            IndexTailleLameRoche = (item != null && item.HasMeta(MetaTailleLameRoche)) ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32() : 2,
            Quantite = (item != null && item.HasMeta(MetaQuantiteObjetPose)) ? (int)item.GetMeta(MetaQuantiteObjetPose).AsInt32() : 1
        };
        if (slot.EstUnEclat)
            slot.EstUnEclat = false;
        return true;
    }

    private static ItemPhysique TrouverItemPhysiqueDansNoeud(Node3D n)
    {
        if (n is ItemPhysique i) return i;
        return n.GetNodeOrNull<ItemPhysique>("ItemPhysique");
    }

    private static bool EstBlocChutantPersistable(int id)
    {
        return id == 10 || id == 11 || id == 15 || id == Joueur.IdObjetBaie
            || id == BlocChutant.ID_BRANCHE
            || id == BlocChutant.ID_BOIS
            || id == BlocChutant.ID_FEUILLE_ARRACHEE
            || id == 32;
    }

    private static SlotInventaire[] CopierGrilleAtelierOuVide(ItemPhysique item)
    {
        var copie = new SlotInventaire[9];
        for (int i = 0; i < copie.Length; i++)
            copie[i] = new SlotInventaire();
        if (item?.GrillePlanTravailAtelier == null) return copie;
        int len = Mathf.Min(9, item.GrillePlanTravailAtelier.Length);
        for (int i = 0; i < len; i++)
            copie[i] = item.GrillePlanTravailAtelier[i];
        return copie;
    }

    private static SlotInventaire[] CopierGrilleCoffreOuVide(ItemPhysique item)
    {
        var copie = new SlotInventaire[10];
        for (int i = 0; i < copie.Length; i++)
            copie[i] = new SlotInventaire();
        if (item?.GrilleStockageCoffre == null) return copie;
        int len = Mathf.Min(10, item.GrilleStockageCoffre.Length);
        for (int i = 0; i < len; i++)
            copie[i] = item.GrilleStockageCoffre[i];
        return copie;
    }

    private static int LireNombreObjetsPosesDepuisFichier(string chemin)
    {
        try
        {
            using var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
            int version = r.ReadInt32();
            if (version < 1 || version > VersionPersistenceObjetsPoses)
                return -1;
            return Mathf.Max(0, r.ReadInt32());
        }
        catch
        {
            return -1;
        }
    }

    private List<(SlotInventaire slot, Vector3 pos, Vector3 rot, SlotInventaire[] atelier, SlotInventaire[] coffre)> CollecterObjetsPosesDimension(SceneTree tree, int dimensionId)
    {
        var aSauver = new List<(SlotInventaire slot, Vector3 pos, Vector3 rot, SlotInventaire[] atelier, SlotInventaire[] coffre)>();
        if (tree == null)
            return aSauver;
        foreach (Node n in tree.GetNodesInGroup("BlocsPoses"))
        {
            if (!EstNoeudDansDimension(n, dimensionId))
                continue;
            if (!EssayerConstruireSlotObjetPose(n, out var s, out var p, out var r))
                continue;
            SlotInventaire[] atelier = null;
            SlotInventaire[] coffre = null;
            if (n is Node3D n3)
            {
                var item = TrouverItemPhysiqueDansNoeud(n3);
                if (s.ID == 200 || s.ID == IdObjetRackBatons || s.ID == IdObjetRackBuches)
                    atelier = CopierGrilleAtelierOuVide(item);
                if (s.ID == IdObjetCoffreBoisTier0)
                    coffre = CopierGrilleCoffreOuVide(item);
            }
            aSauver.Add((s, p, r, atelier, coffre));
        }
        return aSauver;
    }

    private void EcrireFichierObjetsPoses(string chemin, List<(SlotInventaire slot, Vector3 pos, Vector3 rot, SlotInventaire[] atelier, SlotInventaire[] coffre)> aSauver)
    {
        using var w = new BinaryWriter(File.Open(chemin, FileMode.Create));
        w.Write(VersionPersistenceObjetsPoses);
        w.Write(aSauver.Count);
        foreach (var e in aSauver)
        {
            EcrireSlot(w, e.slot);
            w.Write(e.pos.X); w.Write(e.pos.Y); w.Write(e.pos.Z);
            w.Write(e.rot.X); w.Write(e.rot.Y); w.Write(e.rot.Z);
            bool aAtelier = (e.slot.ID == 200 || e.slot.ID == IdObjetRackBatons || e.slot.ID == IdObjetRackBuches) && e.atelier != null;
            w.Write(aAtelier);
            if (aAtelier)
            {
                for (int i = 0; i < 9; i++)
                    EcrireSlot(w, i < e.atelier.Length ? e.atelier[i] : new SlotInventaire());
            }
            bool aCoffre = e.slot.ID == IdObjetCoffreBoisTier0 && e.coffre != null;
            w.Write(aCoffre);
            if (aCoffre)
            {
                for (int i = 0; i < 10; i++)
                    EcrireSlot(w, i < e.coffre.Length ? e.coffre[i] : new SlotInventaire());
            }
        }
    }

    private bool SauvegarderObjetsPosesPourDimension(SceneTree tree, int dimensionId)
    {
        string chemin = ObtenirCheminObjetsPosesPourDimension(dimensionId);
        string nomFichier = Path.GetFileName(chemin);
        var aSauver = CollecterObjetsPosesDimension(tree, dimensionId);

        int nbAtelier200 = 0, nbRack = 0, nbCoffre = 0, nbFondations = 0, nbAutres = 0;
        foreach (var e in aSauver)
        {
            if (e.slot.EstVide) continue;
            int id = e.slot.ID;
            if (id == 200) nbAtelier200++;
            else if (id == IdObjetRackBatons || id == IdObjetRackBuches) nbRack++;
            else if (id == IdObjetCoffreBoisTier0) nbCoffre++;
            else if (EstIdFondation(id)) nbFondations++;
            else nbAutres++;
        }
        string nomDim = ConstantesDimensions.ObtenirNomCanonique(dimensionId);
        GD.Print($"ZERO-K : Sauvegarde objets posés → {nomFichier} [{nomDim}] : {aSauver.Count} objet(s) " +
            $"(fondations={nbFondations}, atelier 200={nbAtelier200}, racks={nbRack}, coffres={nbCoffre}, autres={nbAutres}) — monde {GameState.Instance?.NomMondeActuel ?? "?"}");

        if (DoitAnnulerSauvegardeObjetsPosesIncomplete(chemin, aSauver.Count, dimensionId))
        {
            int ancienNombre = LireNombreObjetsPosesDepuisFichier(chemin);
            GD.Print($"ZERO-K : Sauvegarde {nomFichier} annulée (protection : {aSauver.Count} en scène vs {ancienNombre} sur disque).");
            return false;
        }

        EcrireFichierObjetsPoses(chemin, aSauver);
        return true;
    }

    /// <summary>Écrit un fichier par dimension (ARAPA, APISARA, PETA, OMEGA, DERATA) depuis les racines en mémoire.</summary>
    private void SauvegarderObjetsPosesMonde(SceneTree tree)
    {
        try
        {
            if (tree == null) return;
            if (_chargementObjetsPosesMondeEnCours)
            {
                GD.Print("ZERO-K : Sauvegarde objets posés ignorée (chargement des objets posés en cours).");
                return;
            }
            Directory.CreateDirectory(ObtenirCheminDossierSauvegardeMonde());
            foreach (var info in ConstantesDimensions.ToutesAvecPersistanceModifications())
                SauvegarderObjetsPosesPourDimension(tree, info.Id);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur sauvegarde objets posés : {ex.Message}");
        }
    }

    private void ChargerObjetsPosesMonde()
    {
        try
        {
            string cheminDimension = ObtenirCheminObjetsPosesDimensionActive();
            string cheminLegacy = ObtenirCheminObjetsPosesLegacy();
            string chemin = cheminDimension;
            bool lectureLegacy = false;
            if (!File.Exists(chemin))
            {
                // placed_objects.dat = ancienne sauvegarde Alpha uniquement — pas pour PETA / OMEGA / DERATA.
                if (ObtenirDimensionLocaleActiveId() != (int)DimensionJeu.Alpha)
                    return;
                if (!File.Exists(cheminLegacy)) return;
                chemin = cheminLegacy;
                lectureLegacy = true;
                GD.Print($"ZERO-K : Migration legacy objets posés détectée ({Path.GetFileName(cheminLegacy)} -> {Path.GetFileName(cheminDimension)}).");
            }

            // Lecture intégrale AVANT toute destruction : sinon fichier vide / version inconnue / erreur
            // supprime tout l’existant sans rien respawner (ateliers, racks, roches au sol « posées », etc.).
            var entrees = new List<(SlotInventaire slot, Vector3 pos, Vector3 rot, SlotInventaire[] atelier, SlotInventaire[] coffre)>();
            using (var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)))
            {
                int version = r.ReadInt32();
                if (version < 1 || version > VersionPersistenceObjetsPoses)
                {
                    GD.PrintErr($"ZERO-K : {Path.GetFileName(chemin)} version {version} non prise en charge — les objets déjà en scène sont conservés.");
                    return;
                }
                bool lireExtras = version >= 3;
                int nObj = r.ReadInt32();
                for (int i = 0; i < nObj; i++)
                {
                    SlotInventaire s = LireSlot(r, lireExtras);
                    Vector3 p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    Vector3 rot = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    SlotInventaire[] grilleAtelier = null;
                    SlotInventaire[] grilleCoffre = null;
                    if (version >= 2)
                    {
                        bool aAtelier = r.ReadBoolean();
                        if (aAtelier)
                        {
                            grilleAtelier = new SlotInventaire[9];
                            for (int g = 0; g < 9; g++)
                                grilleAtelier[g] = LireSlot(r, lireExtras);
                        }
                    }
                    if (version >= 4)
                    {
                        bool aCoffre = r.ReadBoolean();
                        if (aCoffre)
                        {
                            grilleCoffre = new SlotInventaire[10];
                            for (int g = 0; g < 10; g++)
                                grilleCoffre[g] = LireSlot(r, lireExtras);
                        }
                    }
                    entrees.Add((s, p, rot, grilleAtelier, grilleCoffre));
                }
            }

            int nbAtelier200L = 0, nbRackL = 0, nbCoffreL = 0, nbFondationsL = 0, nbAutresL = 0;
            foreach (var e in entrees)
            {
                if (e.slot.EstVide) continue;
                int id = e.slot.ID;
                if (id == 200) nbAtelier200L++;
                else if (id == IdObjetRackBatons || id == IdObjetRackBuches) nbRackL++;
                else if (id == IdObjetCoffreBoisTier0) nbCoffreL++;
                else if (EstIdFondation(id)) nbFondationsL++;
                else nbAutresL++;
            }
            string dimActive = ConstantesDimensions.ObtenirNomCanonique(ObtenirDimensionLocaleActiveId());
            GD.Print($"ZERO-K : Chargement objets posés [{dimActive}] depuis {Path.GetFileName(chemin)} : {entrees.Count} entrée(s) " +
                $"(fondations={nbFondationsL}, atelier 200={nbAtelier200L}, racks={nbRackL}, coffres={nbCoffreL}, autres={nbAutresL}) — monde {GameState.Instance?.NomMondeActuel ?? "?"}");

            // Référencer les anciens avant de respawner : évite une fenêtre où le groupe BlocsPoses est vide
            // si une sauvegarde (autosave, pose, fermeture) s’exécute entre QueueFree et CreerBlocPose.
            var anciensBlocsPoses = new List<Node>();
            if (IsInsideTree())
            {
                foreach (Node n in GetTree().GetNodesInGroup("BlocsPoses"))
                {
                    if (EstNoeudDansDimensionActive(n))
                        anciensBlocsPoses.Add(n);
                }
            }

            _objetsPosesAttendusDernierChargement = entrees.Count;
            _objetsPosesSpawnesDernierChargement = 0;
            _chargementObjetsPosesMondeEnCours = true;
            try
            {
                var nouveauxBlocsPoses = new List<Node>();
                foreach (var e in entrees)
                {
                    if (e.slot.EstVide) continue;
                    SlotInventaire s = e.slot;
                    Vector3 p = e.pos;
                    Vector3 rot = e.rot;
                    SlotInventaire[] grilleAtelier = e.atelier;
                    SlotInventaire[] grilleCoffre = e.coffre;
                    Node3D n = CreerBlocPose(p, s);
                    if (n != null)
                    {
                        nouveauxBlocsPoses.Add(n);
                        _objetsPosesSpawnesDernierChargement++;
                        n.GlobalRotationDegrees = rot;
                        if ((s.ID == 200 || s.ID == IdObjetRackBatons || s.ID == IdObjetRackBuches) && grilleAtelier != null)
                        {
                            var item = TrouverItemPhysiqueDansNoeud(n);
                            if (item != null && item.GrillePlanTravailAtelier != null)
                            {
                                int len = Mathf.Min(9, item.GrillePlanTravailAtelier.Length);
                                for (int g = 0; g < len; g++)
                                    item.GrillePlanTravailAtelier[g] = grilleAtelier[g];
                                if (item.ID_Objet == IdObjetRackBatons)
                                    SynchroniserVisuelRackBatons(item);
                                else if (item.ID_Objet == IdObjetRackBuches)
                                    SynchroniserVisuelRackBuches(item);
                            }
                        }
                        if (s.ID == IdObjetCoffreBoisTier0 && grilleCoffre != null)
                        {
                            var itemC = TrouverItemPhysiqueDansNoeud(n);
                            if (itemC != null && itemC.GrilleStockageCoffre != null)
                            {
                                int lenC = Mathf.Min(10, itemC.GrilleStockageCoffre.Length);
                                for (int g = 0; g < lenC; g++)
                                    itemC.GrilleStockageCoffre[g] = grilleCoffre[g];
                            }
                        }
                        EnregistrerGelRestaurationSolSiBesoin(n);
                    }
                    else if (EstIdFondation(s.ID) || s.ID == 200 || s.ID == IdObjetTableAnalyseTier1)
                        GD.PrintErr($"ZERO-K : Échec respawn objet posé id={s.ID} pos={p} (fichier conservé).");
                }

                bool remplacerAnciens = entrees.Count == 0
                    || _objetsPosesSpawnesDernierChargement > 0
                    || anciensBlocsPoses.Count == 0;
                if (remplacerAnciens)
                {
                    foreach (Node n in anciensBlocsPoses)
                    {
                        if (GodotObject.IsInstanceValid(n) && n.IsInsideTree())
                            n.QueueFree();
                    }
                }
                else
                {
                    foreach (Node n in nouveauxBlocsPoses)
                    {
                        if (GodotObject.IsInstanceValid(n) && n.IsInsideTree())
                            n.QueueFree();
                    }
                    _objetsPosesSpawnesDernierChargement = 0;
                    GD.PrintErr($"ZERO-K : Respawn objets posés annulé — anciennes constructions conservées en scène.");
                }
            }
            finally
            {
                _chargementObjetsPosesMondeEnCours = false;
            }
            if (_objetsPosesAttendusDernierChargement > 0 && _objetsPosesSpawnesDernierChargement == 0)
                GD.PrintErr($"ZERO-K : Aucun objet posé respawné ({_objetsPosesAttendusDernierChargement} en fichier) — sauvegarde disque protégée tant que la scène reste vide.");
            if (lectureLegacy)
                GD.Print($"ZERO-K : Migration objets posés effectuée vers {Path.GetFileName(cheminDimension)} à la prochaine sauvegarde.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur chargement objets posés : {ex.Message}");
        }
    }

    private static string ObtenirCheminBlocsChutantsPourDimension(int dimensionId)
    {
        string dossier = ObtenirCheminDossierSauvegardeMonde();
        return Path.Combine(dossier, ObtenirNomFichierBlocsChutantsDimension(ConstantesDimensions.ObtenirNomCanonique(dimensionId)));
    }

    private List<(byte id, Vector3 pos, Vector3 rot)> CollecterBlocsChutantsDimension(SceneTree tree, int dimensionId)
    {
        var aSauver = new List<(byte id, Vector3 pos, Vector3 rot)>();
        if (tree == null)
            return aSauver;
        foreach (Node n in tree.GetNodesInGroup("PersistantsBlocChutant"))
        {
            if (!EstNoeudDansDimension(n, dimensionId))
                continue;
            if (n is not BlocChutant bc || !GodotObject.IsInstanceValid(bc) || !bc.IsInsideTree()) continue;
            int id = bc.HasMeta("ID_Matiere") ? bc.GetMeta("ID_Matiere").AsInt32() : 0;
            if (id <= 0 || id > 255 || !EstBlocChutantPersistable(id)) continue;
            aSauver.Add(((byte)id, bc.GlobalPosition, bc.GlobalRotationDegrees));
        }
        return aSauver;
    }

    private void EcrireFichierBlocsChutants(string chemin, List<(byte id, Vector3 pos, Vector3 rot)> aSauver)
    {
        using var w = new BinaryWriter(File.Open(chemin, FileMode.Create));
        w.Write(VersionPersistenceObjetsPoses);
        w.Write(aSauver.Count);
        foreach (var e in aSauver)
        {
            w.Write(e.id);
            w.Write(e.pos.X); w.Write(e.pos.Y); w.Write(e.pos.Z);
            w.Write(e.rot.X); w.Write(e.rot.Y); w.Write(e.rot.Z);
        }
    }

    private void SauvegarderBlocsChutantsPourDimension(SceneTree tree, int dimensionId)
    {
        string chemin = ObtenirCheminBlocsChutantsPourDimension(dimensionId);
        var aSauver = CollecterBlocsChutantsDimension(tree, dimensionId);
        string nomDim = ConstantesDimensions.ObtenirNomCanonique(dimensionId);
        if (DoitPreserverFichierPersistantDimensionInactive(chemin, aSauver.Count, dimensionId))
        {
            GD.Print($"ZERO-K : Sauvegarde {Path.GetFileName(chemin)} ignorée — dimension {nomDim} non active, données conservées.");
            return;
        }
        GD.Print($"ZERO-K : Sauvegarde blocs chutants → {Path.GetFileName(chemin)} [{nomDim}] : {aSauver.Count} entrée(s).");
        EcrireFichierBlocsChutants(chemin, aSauver);
    }

    private void SauvegarderBlocsChutantsMonde(SceneTree tree)
    {
        try
        {
            if (tree == null) return;
            Directory.CreateDirectory(ObtenirCheminDossierSauvegardeMonde());
            foreach (var info in ConstantesDimensions.ToutesAvecPersistanceModifications())
                SauvegarderBlocsChutantsPourDimension(tree, info.Id);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur sauvegarde blocschutants : {ex.Message}");
        }
    }

    private void ChargerBlocsChutantsMonde()
    {
        try
        {
            string cheminDimension = ObtenirCheminBlocsChutantsDimensionActive();
            string cheminLegacy = ObtenirCheminBlocsChutantsLegacy();
            string chemin = cheminDimension;
            bool lectureLegacy = false;
            if (!File.Exists(chemin))
            {
                if (ObtenirDimensionLocaleActiveId() != (int)DimensionJeu.Alpha)
                    return;
                if (!File.Exists(cheminLegacy)) return;
                chemin = cheminLegacy;
                lectureLegacy = true;
                GD.Print($"ZERO-K : Migration legacy blocs chutants détectée ({Path.GetFileName(cheminLegacy)} -> {Path.GetFileName(cheminDimension)}).");
            }

            var entrees = new List<(byte id, Vector3 pos, Vector3 rot)>();
            using (var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)))
            {
                int version = r.ReadInt32();
                if (version < 1 || version > VersionPersistenceObjetsPoses)
                {
                    GD.PrintErr($"ZERO-K : {Path.GetFileName(chemin)} version {version} non prise en charge — blocs persistants conservés.");
                    return;
                }
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    byte id = r.ReadByte();
                    Vector3 pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    Vector3 rot = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    entrees.Add((id, pos, rot));
                }
            }

            if (IsInsideTree())
            {
                foreach (Node n in GetTree().GetNodesInGroup("PersistantsBlocChutant"))
                {
                    if (EstNoeudDansDimension(n, ObtenirDimensionLocaleActiveId()))
                        n.QueueFree();
                }
            }

            Material matTerrain = _gestionnaireMonde?.MaterielTerrain ?? GD.Load<Material>("res://Manteau_Planetaire.tres");
            foreach (var e in entrees)
            {
                if (!EstBlocChutantPersistable(e.id)) continue;
                BlocChutant bloc = e.id == BlocChutant.ID_FEUILLE_ARRACHEE
                    ? BlocChutant.CreerFeuillageArrache(e.pos, null)
                    : BlocChutant.Creer(e.pos, e.id, matTerrain);
                if (bloc == null) continue;
                if (GetParent() != null)
                {
                    if (IsInsideTree())
                        CallDeferred(nameof(AjouterBlocChutantAuParent), bloc, e.pos, e.rot);
                    else
                        AjouterBlocChutantAuParent(bloc, e.pos, e.rot);
                }
            }
            if (lectureLegacy)
                GD.Print($"ZERO-K : Migration blocs chutants effectuée vers {Path.GetFileName(cheminDimension)} à la prochaine sauvegarde.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur chargement blocschutants : {ex.Message}");
        }
    }

    /// <summary>
    /// Sous <c>Monde_Zero</c>, l’atelier et les <c>BlocsPoses</c> sont souvent des <b>frères</b> du joueur.
    /// À la destruction de la scène, Godot peut libérer ces nœuds <b>avant</b> le <c>_ExitTree</c> du joueur :
    /// un <see cref="SauvegarderEtatPersistantMonde"/> ici voyait 0 objet et <b>effaçait</b> <c>placed_objects.dat</c>.
    /// On ne persiste donc que position + fichiers joueur ; tables / faune / chunks passent par le menu pause, Quitter, ou <see cref="Gestionnaire_Monde.SauvegarderManuelDepuisMenu"/>.
    /// </summary>
    public override void _ExitTree()
    {
        if (!Engine.IsEditorHint())
        {
            GameState.Instance?.SauvegarderPositionJoueur(GlobalPosition);
            // Ne jamais réécrire objets posés / blocs chutants ici : les BlocsPoses peuvent déjà être libérés.
            SauvegarderFichiersJoueurHorsObjetsAuSol();
        }
        base._ExitTree();
    }
}
