using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    public override void _Input(InputEvent @event)
    {
        if (_menuFutureState != null && _menuFutureState.EstOuvert)
        {
            if (@event.IsActionPressed("ui_cancel")
                || (@event is InputEventKey ekEsc && ekEsc.Pressed && !ekEsc.Echo && ekEsc.Keycode == Key.Escape)
                || EstToggleFutureState(@event))
            {
                _menuFutureState.BasculerVisibilite();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event is InputEventMouseButton || @event is InputEventMouseMotion)
                return;
            if (@event is InputEventKey keBlocFuture && keBlocFuture.Pressed && !keBlocFuture.Echo)
                return;
            if (@event is InputEventAction actionFuture && actionFuture.Pressed)
            {
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (EstToggleFutureState(@event) && _menuFutureState != null)
        {
            if (_menuFutureState.EstOuvert)
                _menuFutureState.BasculerVisibilite();
            else
            {
                _menuFutureState.DefinirModeFutureStates();
                OuvrirFutureStateDepuisMenu();
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (GererInputSelectionAtelleJambe(@event))
            return;
        if (GererInputSelectionAtelleBras(@event))
            return;
        if (GererInputSelectionBandage(@event))
            return;
        if (GererInputSelectionAloeBrulure(@event))
            return;

        if (_menuAnatomie != null && @event.IsActionPressed("inventaire"))
        {
            if (EstModePlacementGhostActif())
                AnnulerModePlacementStructure(reinitialiserRotation: false);
            if (ChatInGameOuvert())
                FermerChatInGame();
            if (!_menuAnatomie.EstOuvert)
            {
                CraftGrille3x3AuTable = false;
                IdStationCraftOuverte = 0;
                AtelierPlanTravailOuvert = null;
                StockageRackBatonsOuvert = false;
                RackBatonsOuvert = null;
            }
            _menuAnatomie.BasculerVisibilite();
            MettreAJourVisibiliteChatSelonUiBloquante();
            RafraichirHUD();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Le chat ne doit jamais voler le focus pendant que le menu inventaire/creatif est affiché.
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
        {
            MettreAJourVisibiliteChatSelonUiBloquante();
            return;
        }

        MettreAJourVisibiliteChatSelonUiBloquante();

        if (EssayerBasculerChatInGameDepuisInput(@event))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (ChatInGameOuvert())
        {
            if (@event.IsActionPressed("ui_cancel")
                || (@event is InputEventKey ekChatEsc && ekChatEsc.Pressed && !ekChatEsc.Echo && ekChatEsc.Keycode == Key.Escape))
            {
                FermerChatInGame();
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (EssayerBasculerCarnetDepuisInput(@event))
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (CarnetSavoirOuvert())
        {
            if (@event.IsActionPressed("ui_cancel"))
            {
                FermerCarnetSavoirUI();
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (EstModePlacementGhostActif()
            && (@event.IsActionPressed("ui_cancel")
                || (@event is InputEventKey ekEscPlacement && ekEscPlacement.Pressed && !ekEscPlacement.Echo && ekEscPlacement.Keycode == Key.Escape)))
        {
            AnnulerModePlacementStructure(reinitialiserRotation: false);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
        {
            // Ã‰chap / ui_cancel : fermer lâ€™UI et revenir en jeu immÃ©diatement.
            if (@event.IsActionPressed("ui_cancel") ||
                (@event is InputEventKey ekEsc && ekEsc.Pressed && !ekEsc.Echo && ekEsc.Keycode == Key.Escape))
            {
                FermerUIJoueurSiOuverte();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event is InputEventMouseButton || @event is InputEventMouseMotion)
                return;
            // Laisser Tab (changer_main) descendre jusquâ€™au handler ; bloquer le reste du clavier (minage, etc.).
            if (@event.IsActionPressed("changer_main"))
            {
                // no-op ici : traitÃ© plus bas
            }
            else if (@event is InputEventKey keBloc && keBloc.Pressed && !keBloc.Echo)
                return;
        }

        // Menu CAO (stub) : bloquer le jeu ; Ã‰chap ferme â€” plus de touche K.
        bool caoOuvert = _modelisateur != null && _modelisateur.EstOuvert;
        if (caoOuvert)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape)
            {
                if (_modelisateur == null || !_modelisateur.SaisieTexteEnCours)
                {
                    _modelisateur.BasculerVisibilite();
                    GetViewport().SetInputAsHandled();
                }
            }
            return;
        }

        if (@event.IsActionPressed("clic_gauche"))
        {
            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            _gaucheMaintenu = true;
            _mouvementSourisCumule = Vector2.Zero;
            if (mainActive.EstVide)
                ReinitialiserMinageMainNueProgression();
        }
        else if (@event.IsActionReleased("clic_gauche") && _gaucheMaintenu)
        {
            _gaucheMaintenu = false;
            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            if (EssayerBasculerPorteSousVisee())
            {
                ReinitialiserMinageMainNueProgression();
                return;
            }
            if (_bloquerActionClicGaucheApresMinageBuisson || _bloquerActionClicGaucheApresDepecage)
            {
                _bloquerActionClicGaucheApresMinageBuisson = false;
                _bloquerActionClicGaucheApresDepecage = false;
                ReinitialiserMinageMainNueProgression();
                return;
            }
            TypeMouvementFrappe mouv = TypeMouvementFrappe.Estoc;
            if (_mouvementSourisCumule.Length() > 40f)
            {
                if (Mathf.Abs(_mouvementSourisCumule.X) > Mathf.Abs(_mouvementSourisCumule.Y))
                    mouv = _mouvementSourisCumule.X > 0 ? TypeMouvementFrappe.GaucheADroite : TypeMouvementFrappe.DroiteAGauche;
                else
                    mouv = _mouvementSourisCumule.Y > 0 ? TypeMouvementFrappe.DeHautEnBas : TypeMouvementFrappe.DeBasEnHaut;
            }

            if (!mainActive.EstVide && PeutUtiliserFrappe(mainActive))
            {
                if (EssayerEteindrePitFeuRocheSousVisee(mainActive))
                {
                    ReinitialiserMinageMainNueProgression();
                    return;
                }
                ExecuterAction(1.0f, mouv);
                JouerAnimationFrappe(mouv);
            }
            else if (!mainActive.EstVide && mainActive.ID == IdObjetAllumeFeu)
            {
                if (EssayerAllumerPitFeuSousVisee(ref mainActive))
                {
                    if (MainGaucheEstActive) MainGauche = mainActive;
                    else MainDroite = mainActive;
                    RafraichirHUD();
                    ReinitialiserMinageMainNueProgression();
                    return;
                }
            }
            else if (!mainActive.EstVide && EstIdTorche(mainActive.ID))
            {
                if (EssayerAllumerTorcheEnMain(ref mainActive))
                {
                    if (MainGaucheEstActive) MainGauche = mainActive;
                    else MainDroite = mainActive;
                    RafraichirHUD();
                    ReinitialiserMinageMainNueProgression();
                    return;
                }
            }
            else if (mainActive.EstVide)
            {
                ExecuterActionMainNue(1.0f, mouv);
            }
            ReinitialiserMinageMainNueProgression();
        }
        else if (@event.IsActionPressed("clic_droit"))
        {
            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            if (!mainActive.EstVide)
            {
                if (!EstModePlacementGhostActifPourSlot(mainActive))
                    _forceLancer = 0f; // MAIN PLEINE = DÃ‰BUT CHARGE LANCER/POSER
            }
            else if (_cooldownGainFaimClicDroit <= 0f)
            {
                _faimJoueur = Mathf.Min(FaimMaxJoueur, _faimJoueur + GainFaimClicDroitMainVide);
                _cooldownGainFaimClicDroit = CooldownGainFaimClicDroitSec;
                MettreAJourHudStatsSurvie();
            }
        }
        else if (@event.IsActionReleased("clic_droit"))
        {
            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            bool estStructureModePlacement = !mainActive.EstVide && EstStructureSupporteeModePlacement(mainActive.ID);
            bool estObjetLancable = !mainActive.EstVide && EstObjetLancableAuMaintien(mainActive);
            bool shiftMaintenu = Input.IsPhysicalKeyPressed(Key.Shift);
            if (estStructureModePlacement && _modePlacementStructureActif)
            {
                MettreAJourGhostPlacementStructure(mainActive);
                if (_ghostPlacementValide)
                {
                    ExecuterPlacement();
                    AnnulerModePlacementStructure(reinitialiserRotation: false);
                }
                _forceLancer = 0f;
                GetViewport().SetInputAsHandled();
                return;
            }
            if (estObjetLancable && _modePlacementLancerShiftActif)
            {
                MettreAJourGhostPlacementStructure(mainActive);
                if (_ghostPlacementValide)
                {
                    ExecuterPlacementModeGhostLancer(mainActive);
                    AnnulerModePlacementStructure(reinitialiserRotation: false);
                }
                _forceLancer = 0f;
                GetViewport().SetInputAsHandled();
                return;
            }

            // PRIORITÃ‰ ABSOLUE : si la visÃ©e touche un atelier posÃ©, on ouvre le plan 3x3
            // avant toute logique de pose/lancer de l'objet en main.
            if (EssayerOuvrirAtelierSousVisee())
            {
                _forceLancer = 0f;
                return;
            }

            if (!mainActive.EstVide)
            {
                if (shiftMaintenu && EstObjetSoinPosableShift(mainActive.ID))
                {
                    _forceLancer = 0f;
                    ExecuterPlacement();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (mainActive.ID == IdObjetAtelleJambe)
                {
                    _forceLancer = 0f;
                    if (TraiterClicDroitAtelleJambe())
                    {
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
                if (mainActive.ID == IdObjetAtelleBras)
                {
                    _forceLancer = 0f;
                    if (TraiterClicDroitAtelleBras())
                    {
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
                if (mainActive.ID == IdObjetBandageTier1)
                {
                    _forceLancer = 0f;
                    if (TraiterClicDroitBandageTier1())
                    {
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
                if (mainActive.ID == IdObjetAloeVera)
                {
                    _forceLancer = 0f;
                    if (TraiterClicDroitAloeVeraSoinBrulure())
                    {
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }

                // Priorité gameplay : clic droit sur pit à feu roche avec bâton/branche = ajout de combustible,
                // même si le clic a été maintenu assez longtemps pour entrer en mode lancer.
                if (EssayerAjouterCombustiblePitFeuRocheSousVisee(ref mainActive))
                {
                    _forceLancer = 0f;
                    return;
                }

                // IDENTIFICATION DE LA MATIÃˆRE : Est-ce du terrain (Voxel) ?
                bool estTerrainVoxel = mainActive.ID >= 1 && mainActive.ID <= 9;
                bool estAtelierEnMain = mainActive.ID == 200 || mainActive.ID == IdObjetTableArtisanaTier1;
                bool estTableAnalyseEnMain = mainActive.ID == IdObjetTableAnalyseTier1;
                bool estRackBatonsEnMain = mainActive.ID == IdObjetRackBatons || mainActive.ID == IdObjetRackBuches;
                bool estCoffreEnMain = mainActive.ID == IdObjetCoffreBoisTier0;
        bool estPitFeuEnMain = EstIdPitFeu(mainActive.ID) || EstIdFondation(mainActive.ID) || EstIdPlancher(mainActive.ID) || EstIdMuret(mainActive.ID) || EstIdMurBois(mainActive.ID) || EstIdPorteBois(mainActive.ID) || EstIdToitChaume(mainActive.ID) || EstIdTorche(mainActive.ID) || EstIdTableBoisDecorative(mainActive.ID);
                bool estBuissonEnMain = mainActive.ID == 10 || mainActive.ID == 11;
                if (shiftMaintenu && estObjetLancable)
                {
                    DemarrerModePlacementLancerShift(mainActive);
                    _forceLancer = 0f;
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (estStructureModePlacement)
                {
                    DemarrerModePlacementStructure(mainActive);
                    _forceLancer = 0f;
                    GetViewport().SetInputAsHandled();
                    return;
                }
                // Clic bref = poser. Maintien du clic = lancer (seuil 0,5 s).
                // Atelier + rack (structures fixes) : jamais de lancer.
                if (estAtelierEnMain || estTableAnalyseEnMain || estRackBatonsEnMain || estCoffreEnMain || estPitFeuEnMain || estBuissonEnMain || estTerrainVoxel || _forceLancer < 0.5f)
                {
                    // Clic droit court + lame / roche plate / pointe + sol : fauchage (le gauche le fait aussi).
                    // Objet lançable : le clic droit court sert à poser sous la visée — pas de vol du fauchage.
                    if (!estAtelierEnMain && !estTableAnalyseEnMain && !estRackBatonsEnMain && !estCoffreEnMain && !estPitFeuEnMain && !estTerrainVoxel && _forceLancer < 0.5f
                        && !EstObjetLancableAuMaintien(mainActive)
                        && ExecuterFauchageSolPrioritaireClicDroit())
                    {
                        _forceLancer = 0f;
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                    ExecuterPlacement();
                }
                else
                {
                    // Baie : maintien clic droit = manger une unité (effets variables selon la teinte).
                    if (mainActive.ID == IdObjetBaie)
                    {
                        int couleurBaie = mainActive.IndexChimique;
                        ConsommerUneUniteMainActive();
                        AppliquerEffetsConsommationBaie(couleurBaie);
                        RafraichirHUD();
                        ReinitialiserRotationManuelle();
                        GetViewport().SetInputAsHandled();
                    }
                    else if (mainActive.ID == IdObjetSteakCru || mainActive.ID == IdObjetSteakCuit)
                    {
                        bool steakCuit = mainActive.ID == IdObjetSteakCuit;
                        ConsommerUneUniteMainActive();
                        AppliquerEffetsConsommationSteak(steakCuit);
                        RafraichirHUD();
                        ReinitialiserRotationManuelle();
                        GetViewport().SetInputAsHandled();
                    }
                    else
                        ExecuterLancer(Mathf.Clamp(_forceLancer, 0.5f, 5.0f));
                }
                _forceLancer = 0f;
            }
            else
            {
                // Main vide : clic droit court sur atelier posÃ© => ouvrir.
                EssayerOuvrirAtelierSousVisee();
            }
        }
        else if (@event.IsActionPressed("interagir"))
        {
            // E : main pleine â†’ corde accrochÃ©e / dÃ©pÃ´t flexible ou rigide ; main vide â†’ ramasser
            ExecuterToucheInteragir();
        }
        else if (@event.IsActionPressed("changer_main"))
        {
            if (EstModePlacementGhostActif())
                AnnulerModePlacementStructure(reinitialiserRotation: false);
            MainGaucheEstActive = !MainGaucheEstActive;
            ReinitialiserRotationManuelle();
            MettreAJourObjetTenueTps();
            RafraichirHUD();
            _menuAnatomie?.RafraichirMenu();
            GD.Print(MainGaucheEstActive ? "ZERO-K : Main Gauche sÃ©lectionnÃ©e (Tab)." : "ZERO-K : Main Droite sÃ©lectionnÃ©e (Tab).");
        }
        else if (@event is InputEventMouseButton { Pressed: true } mbFondation)
        {
            SlotInventaire mainPose = MainGaucheEstActive ? MainGauche : MainDroite;
            if (EstModePlacementGhostActifPourSlot(mainPose) && EstIdFondation(mainPose.ID))
            {
                if (mbFondation.ButtonIndex == MouseButton.WheelUp)
                    AjusterOffsetEtagesFondation(+1);
                else if (mbFondation.ButtonIndex == MouseButton.WheelDown)
                    AjusterOffsetEtagesFondation(-1);
                else
                    return;
                MettreAJourGhostPlacementStructure(mainPose);
                GetViewport().SetInputAsHandled();
            }
            else if (EstModePlacementGhostActifPourSlot(mainPose) && EstIdMuret(mainPose.ID))
            {
                if (mbFondation.ButtonIndex == MouseButton.WheelUp)
                    AjusterModeSnapMuret(+1);
                else if (mbFondation.ButtonIndex == MouseButton.WheelDown)
                    AjusterModeSnapMuret(-1);
                else
                    return;
                MettreAJourGhostPlacementStructure(mainPose);
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventKey keyEvent)
        {
            if (keyEvent.Pressed && !keyEvent.Echo)
            {
                SlotInventaire mainPose = MainGaucheEstActive ? MainGauche : MainDroite;
                if (EstModePlacementGhostActifPourSlot(mainPose) && EstIdFondation(mainPose.ID))
                {
                    if (keyEvent.Keycode == Key.Pageup)
                    {
                        AjusterOffsetEtagesFondation(+1);
                        MettreAJourGhostPlacementStructure(mainPose);
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                    if (keyEvent.Keycode == Key.Pagedown)
                    {
                        AjusterOffsetEtagesFondation(-1);
                        MettreAJourGhostPlacementStructure(mainPose);
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
                if (keyEvent.Keycode == Key.R)
                {
                    if (keyEvent.CtrlPressed)
                    {
                        _rotationManuelleZ += 90f;
                        if (_rotationManuelleZ >= 360f) _rotationManuelleZ -= 360f;
                    }
                    else if (keyEvent.ShiftPressed)
                    {
                        _rotationManuelleX += 90f;
                        if (_rotationManuelleX >= 360f) _rotationManuelleX -= 360f;
                    }
                    else
                    {
                        _rotationManuelleY += (EstIdMuret(mainPose.ID) || EstIdMurBois(mainPose.ID)) ? 10f : 90f;
                        if (_rotationManuelleY >= 360f) _rotationManuelleY -= 360f;
                    }
                    MettreAJourObjetEnMain();
                    GD.Print($"ZERO-K : Rotation manuelle â€” Y (R) {_rotationManuelleY}Â°, X (Maj+R) {_rotationManuelleX}Â°, Z (Ctrl+R) {_rotationManuelleZ}Â°.");
                }
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey diagKey && diagKey.Pressed && !diagKey.Echo && diagKey.Keycode == Key.F10)
        {
            DiagnostiquerVisuelsFpsRuntime();
            GetViewport()?.SetInputAsHandled();
            return;
        }

        if (EstToggleCameraF5(@event))
        {
            BasculerModeCamera();
            GetViewport()?.SetInputAsHandled();
            return;
        }

        if (CarnetSavoirOuvert())
            return;

        if (_modelisateur != null && _modelisateur.EstOuvert)
            return;

        if (_menuFutureState != null && _menuFutureState.EstOuvert)
            return;

        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            return;

        if (ChatInGameOuvert())
            return;

        // Tampon saut : captÃ© ici pour ne pas perdre la frame si un autre nÅ“ud consomme lâ€™input avant _PhysicsProcess.
        if ((@event.IsActionPressed("jump") || @event.IsActionPressed("ui_accept"))
            && ((@event is InputEventKey k && k.Pressed && !k.Echo)
                || (@event is InputEventJoypadButton jb && jb.Pressed)
                || @event is InputEventAction))
            _tamponSautRestant = Mathf.Max(_tamponSautRestant, DureeTamponSautSecondes);

        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (_gaucheMaintenu) _mouvementSourisCumule += mouseMotion.Relative;

            RotateY(-mouseMotion.Relative.X * MouseSensitivity);
            _pitchCamera = Mathf.Clamp(
                _pitchCamera - mouseMotion.Relative.Y * MouseSensitivity,
                Mathf.DegToRad(PitchSourisMinDeg),
                Mathf.DegToRad(PitchSourisMaxDeg));
            float pitchAbsolu = _pitchCameraBaseRad + _pitchCamera;
            if (_cameraFps != null)
                _cameraFps.Rotation = new Vector3(pitchAbsolu, _yawCorrectionCameraFpsRad, 0f);
            if (_pivotCameraTps != null)
                _pivotCameraTps.Rotation = new Vector3(pitchAbsolu * 0.82f, 0f, 0f);
        }

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }
}
