using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class Joueur
{
    private const int VersionPersistenceJoueur = 2;
    private const int VersionPersistenceObjetsPoses = 4;
    private const int VersionPersistenceProgression = 2;
    private bool _etatPersistantCharge;

    private static string ObtenirCheminDossierSauvegardeMonde()
    {
        string nomMonde = GameState.Instance?.NomMondeActuel ?? "MonMonde";
        return ProjectSettings.GlobalizePath($"user://saves/{nomMonde}");
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
        SauvegarderObjetsPosesMonde(tree);
        SauvegarderBlocsChutantsMonde(tree);
        ObtenirGestionnaireFauneCourant(tree)?.SauvegarderFauneMonde();
    }

    public void ChargerEtatPersistantMonde()
    {
        if (_etatPersistantCharge) return;
        _etatPersistantCharge = true;
        ChargerProgressionJoueurMonde();
        ChargerInventaireMonde();
        ChargerObjetsPosesMonde();
        ChargerBlocsChutantsMonde();
        ObtenirGestionnaireFauneCourant(null)?.ChargerFauneMonde();
        RafraichirHUD();
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
            AjouterFutureStateSiAbsent("Force", 0UL);
            AjouterFutureStateSiAbsent("Dextiriter", 0UL);
            AjouterFutureStateSiAbsent("Metaboliste", 0UL);
            AjouterMetierSiAbsent("Bucheron", 0UL);
            AjouterMetierSiAbsent("Traisage", 0UL);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur chargement progression joueur : {ex.Message}");
        }
    }

    private void SauvegarderInventaireMonde()
    {
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
            if (version < 1 || version > VersionPersistenceJoueur) return;
            bool lireExtras = version >= 2;

            MainGauche = LireSlot(r, lireExtras);
            MainDroite = LireSlot(r, lireExtras);
            EquipementSacDos = LireSlot(r, lireExtras);
            EquipementCeinture = LireSlot(r, lireExtras);
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
            ChargerStockageDepuisSacEquipe();
            ChargerStockageDepuisCeintureSacochesEquipe();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur chargement inventaire joueur : {ex.Message}");
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
            IndexTailleLameRoche = (item != null && item.HasMeta(MetaTailleLameRoche)) ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32() : 2
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

    private void SauvegarderObjetsPosesMonde(SceneTree tree)
    {
        try
        {
            if (tree == null) return;
            string dossier = ObtenirCheminDossierSauvegardeMonde();
            Directory.CreateDirectory(dossier);
            string chemin = Path.Combine(dossier, "placed_objects.dat");
            var aSauver = new List<(SlotInventaire slot, Vector3 pos, Vector3 rot, SlotInventaire[] atelier, SlotInventaire[] coffre)>();
            foreach (Node n in tree.GetNodesInGroup("BlocsPoses"))
            {
                if (EssayerConstruireSlotObjetPose(n, out var s, out var p, out var r))
                {
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
            }

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
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur sauvegarde objets posés : {ex.Message}");
        }
    }

    private void ChargerObjetsPosesMonde()
    {
        try
        {
            string chemin = Path.Combine(ObtenirCheminDossierSauvegardeMonde(), "placed_objects.dat");
            if (!File.Exists(chemin)) return;

            if (IsInsideTree())
            {
                foreach (Node n in GetTree().GetNodesInGroup("BlocsPoses"))
                    n.QueueFree();
            }

            using var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
            int version = r.ReadInt32();
            if (version < 1 || version > VersionPersistenceObjetsPoses) return;
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
                if (s.EstVide) continue;
                Node3D n = CreerBlocPose(p, s);
                if (n != null)
                {
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
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur chargement objets posés : {ex.Message}");
        }
    }

    private void SauvegarderBlocsChutantsMonde(SceneTree tree)
    {
        try
        {
            if (tree == null) return;
            string dossier = ObtenirCheminDossierSauvegardeMonde();
            Directory.CreateDirectory(dossier);
            string chemin = Path.Combine(dossier, "dropped_blocks.dat");
            var aSauver = new List<(byte id, Vector3 pos, Vector3 rot)>();
            foreach (Node n in tree.GetNodesInGroup("PersistantsBlocChutant"))
            {
                if (n is not BlocChutant bc || !GodotObject.IsInstanceValid(bc) || !bc.IsInsideTree()) continue;
                int id = bc.HasMeta("ID_Matiere") ? bc.GetMeta("ID_Matiere").AsInt32() : 0;
                if (id <= 0 || id > 255 || !EstBlocChutantPersistable(id)) continue;
                aSauver.Add(((byte)id, bc.GlobalPosition, bc.GlobalRotationDegrees));
            }
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
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur sauvegarde blocschutants : {ex.Message}");
        }
    }

    private void ChargerBlocsChutantsMonde()
    {
        try
        {
            string chemin = Path.Combine(ObtenirCheminDossierSauvegardeMonde(), "dropped_blocks.dat");
            if (!File.Exists(chemin)) return;

            if (IsInsideTree())
            {
                foreach (Node n in GetTree().GetNodesInGroup("PersistantsBlocChutant"))
                    n.QueueFree();
            }

            using var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
            int version = r.ReadInt32();
            if (version < 1 || version > VersionPersistenceObjetsPoses) return;
            int count = r.ReadInt32();
            Material matTerrain = _gestionnaireMonde?.MaterielTerrain ?? GD.Load<Material>("res://Manteau_Planetaire.tres");
            for (int i = 0; i < count; i++)
            {
                byte id = r.ReadByte();
                Vector3 pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                Vector3 rot = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                if (!EstBlocChutantPersistable(id)) continue;
                BlocChutant bloc = id == BlocChutant.ID_FEUILLE_ARRACHEE
                    ? BlocChutant.CreerFeuillageArrache(pos, null)
                    : BlocChutant.Creer(pos, id, matTerrain);
                if (bloc == null) continue;
                if (GetParent() != null)
                {
                    GetParent().AddChild(bloc);
                    bloc.GlobalPosition = pos;
                    bloc.GlobalRotationDegrees = rot;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Erreur chargement blocschutants : {ex.Message}");
        }
    }
}
