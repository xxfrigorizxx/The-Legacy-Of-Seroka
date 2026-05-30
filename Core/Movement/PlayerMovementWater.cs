using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private float _tempsAttenteSpawn;
    private bool _verrouSpawnActif = true;
    private bool _verrouAntiChuteAbysseActif;
    private float _cooldownSortieVerrouAbysse;
    private float _graceCollisionLocaleRestante;
    private bool _positionSolideAbysseValide;
    private Vector3 _dernierePositionSolideAbysse;
    private float _cooldownRetourSolAbysse;
    private readonly Dictionary<Vector3I, int> _cacheMatiereFrame = new Dictionary<Vector3I, int>(32);
    private ulong _frameCacheMatiere = ulong.MaxValue;

    /// <summary>Cache local de matière par frame physique (évite des lectures voxel redondantes pendant le déplacement).</summary>
    private int ObtenirMatiereExacteCachee(Vector3 positionGlobale)
    {
        if (_gestionnaireMonde == null)
            return 1;

        ulong frame = Engine.GetPhysicsFrames();
        if (_frameCacheMatiere != frame)
        {
            _frameCacheMatiere = frame;
            _cacheMatiereFrame.Clear();
        }

        Vector3I key = new Vector3I(
            Mathf.FloorToInt(positionGlobale.X),
            Mathf.FloorToInt(positionGlobale.Y),
            Mathf.FloorToInt(positionGlobale.Z));

        if (_cacheMatiereFrame.TryGetValue(key, out int idCache))
            return idCache;

        int id = _gestionnaireMonde.ObtenirMatiereExacte(positionGlobale);
        _cacheMatiereFrame[key] = id;
        return id;
    }

    private static readonly Vector3[] _echantillonsImmersionJoueur =
    {
        Vector3.Up * 0.08f,
        Vector3.Up * 0.28f,
        Vector3.Up * 0.48f,
        Vector3.Up * 0.68f,
        Vector3.Up * 0.88f,
        Vector3.Up * 1.08f,
        Vector3.Up * 1.28f
    };

    /// <summary>Recherche une couche d'eau dont la case au-dessus n'est pas de l'eau: donne la hauteur de surface (face haute voxel).</summary>
    private bool EssayerTrouverSurfaceEauY(Vector3 centreRecherche, out float surfaceY)
    {
        surfaceY = 0f;
        if (_gestionnaireMonde == null) return false;

        // Recherche locale verticale autour du joueur: robuste si le niveau d'eau varie lÃ©gÃ¨rement.
        for (int dy = 6; dy >= -8; dy--)
        {
            Vector3 p = centreRecherche + Vector3.Up * dy;
            int id = ObtenirMatiereExacteCachee(p);
            if (id != 4) continue;
            int idAuDessus = ObtenirMatiereExacteCachee(p + Vector3.Up);
            if (idAuDessus == 4) continue;
            surfaceY = Mathf.Floor(p.Y) + 1.0f;
            return true;
        }
        return false;
    }

    private bool PointImmergeJoueur(Vector3 p)
    {
        if (_gestionnaireMonde == null) return false;
        // Joueur: détection stricte pour éviter les faux positifs sur berge
        // (le voisinage 3x3 de EstPointDansEau déclenchait parfois la nage trop tôt).
        return ObtenirMatiereExacteCachee(p) == 4;
    }

    private bool EvaluerEtatEauJoueur(out float surfaceEau)
    {
        surfaceEau = _gestionnaireMonde?.ObtenirNiveauSurfaceEau() ?? 103.35f;
        if (_gestionnaireMonde == null) return false;

        if (EssayerTrouverSurfaceEauY(GlobalPosition + Vector3.Up * 0.3f, out float surfaceLocale))
            surfaceEau = surfaceLocale;

        // Mode eau uniquement si au moins 50% du corps est immergé.
        float ratioImmersion = _gestionnaireMonde.CalculerRatioImmersion(GlobalPosition, _echantillonsImmersionJoueur);
        return ratioImmersion >= 0.5f;
    }

    /// <summary>Détecte un bord de berge devant le joueur alors qu'il est encore dans l'eau.</summary>
    private bool DetecterBordBergeSortieEau(Vector3 directionHoriz, float surfaceEau)
    {
        if (_gestionnaireMonde == null)
            return false;

        Vector3 dir = directionHoriz;
        dir.Y = 0f;
        if (dir.LengthSquared() < 0.0001f)
            dir = -GlobalTransform.Basis.Z;
        dir.Y = 0f;
        if (dir.LengthSquared() < 0.0001f)
            return false;
        dir = dir.Normalized();

        Vector3 pAvantChevilles = GlobalPosition + dir * 0.62f + Vector3.Up * 0.16f;
        Vector3 pAvantBassin = GlobalPosition + dir * 0.62f + Vector3.Up * 0.62f;

        // Si l'eau est encore devant aux chevilles ou au bassin, ce n'est pas un bord de sortie.
        if (PointImmergeJoueur(pAvantChevilles) || PointImmergeJoueur(pAvantBassin))
            return false;

        int idSolAvant = ObtenirMatiereExacteCachee(pAvantChevilles + Vector3.Down * 0.62f);
        bool solBerge = idSolAvant != 0 && idSolAvant != 4;
        bool procheSurface = GlobalPosition.Y <= surfaceEau + 0.55f;
        return solBerge && procheSurface;
    }

    public override void _PhysicsProcess(double delta)
    {
        ulong debutFramePerfUs = ActiverProfilagePerfJoueur ? PerfBudgetMonitor.Begin() : 0UL;
        _cooldownDrainProfilageJoueur += (float)delta;
        ReinitialiserConteneurOuvertSiReferencePerdue();
        float dt = (float)delta;
        _cooldownEnjambementObstacle = Mathf.Max(0f, _cooldownEnjambementObstacle - dt);
        _cooldownGainFaimClicDroit = Mathf.Max(0f, _cooldownGainFaimClicDroit - dt);
        MettreAJourTimersAtellesJambes(dt);
        MettreAJourTimersAtellesBras(dt);
        MettreAJourEffetBandageTier1(dt);
        MettreAJourEffetAloeBrulure(dt);
        MettreAJourEffetsConsommationBaies(dt);
        MettreAJourDegatsBrulureFeu(dt);
        if (!_positionReferenceMetabolisteInitialisee)
            ReinitialiserReferencePositionMetaboliste();
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        bool uiBloquanteOuverte = EstUiJoueurBloquanteOuverte();
        if (EstModePlacementGhostActif() && uiBloquanteOuverte)
            AnnulerModePlacementStructure(reinitialiserRotation: false);

        if (!uiBloquanteOuverte)
        {
            bool bloquerChargePlacement = EstModePlacementGhostActifPourSlot(mainActive);
            if (!mainActive.EstVide && Input.IsActionPressed("clic_droit") && !bloquerChargePlacement)
            {
                bool shiftDeclenchePlacementLancer = Input.IsPhysicalKeyPressed(Key.Shift) && EstObjetLancableAuMaintien(mainActive);
                if (!shiftDeclenchePlacementLancer)
                    _forceLancer = Mathf.Min(5.0f, _forceLancer + (VitesseChargeBras * 2.5f) * dt);
            }
            if (_gaucheMaintenu && (mainActive.EstVide || mainActive.ID == 105 || mainActive.ID == 106 || mainActive.ID == IdObjetHachePierreTier1 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetPiochePierreTier0 || mainActive.ID == IdObjetLancePierreTier0 || mainActive.ID == IdObjetFauxPierreTier0))
                MettreAJourMinageMainNueOuAtelier(dt, mainActive);
            else
                ReinitialiserMinageMainNueProgression();
        }
        else
        {
            if (_gaucheMaintenu) _gaucheMaintenu = false;
            _forceLancer = 0f;
            ReinitialiserMinageMainNueProgression();
        }

        Vector3 velocity = Velocity;
        bool spawnPret = _gestionnaireMonde == null || _gestionnaireMonde.EstSpawnPret();
        bool spawnAligneAuSol = _gestionnaireMonde == null || _gestionnaireMonde.EstAlignementSpawnTermine();
        if (_verrouSpawnActif)
        {
            // Attendre aussi le raycast + pose au sol (Gestionnaire _Process), pas seulement la collision du chunk :
            // sinon une frame de physique avec Y Â« ciel Â» + gravitÃ© = traversÃ©e du mesh.
            if (!spawnPret || !spawnAligneAuSol)
            {
                _tempsAttenteSpawn += dt;
                // Anti soft-lock: si le sol/collision tarde trop, on rend le contrÃ´le au joueur.
                if (_tempsAttenteSpawn <= 8f)
                {
                    int idCorps = ObtenirMatiereExacteCachee(GlobalPosition + Vector3.Up * 0.8f);
                    bool eauCorps = idCorps == 4;
                    velocity.X = 0f;
                    velocity.Y = 0f;
                    velocity.Z = 0f;
                    Velocity = velocity;
                    MoveAndSlide();
                    AppliquerContrainteVerticaleHauteurTerrainMonde(eauCorps, ignorerSiMonteeSaut: false, dt);
                    ReinitialiserReferencePositionMetaboliste();
                    return;
                }
                GD.PrintErr("ZERO-K : DÃ©verrouillage dÃ©placement forcÃ© (spawn non prÃªt trop longtemps).");
                _verrouSpawnActif = false;
            }
            else
            {
                _verrouSpawnActif = false;
            }
            _tempsAttenteSpawn = 0f;
        }

        // Inclut le gate de TP + le verrou dynamique de marche si la croix de collision locale n'est pas prête.
        bool verrouAbysse = _gestionnaireMonde != null && _gestionnaireMonde.EstVerrouSecuriteAbysseActif();
        if (verrouAbysse)
        {
            _verrouAntiChuteAbysseActif = true;
            _cooldownSortieVerrouAbysse = 0.12f;
            velocity.X = 0f;
            velocity.Y = 0f;
            velocity.Z = 0f;
            Velocity = velocity;
            MoveAndSlide();
            return;
        }
        if (_verrouAntiChuteAbysseActif)
        {
            _cooldownSortieVerrouAbysse = Mathf.Max(0f, _cooldownSortieVerrouAbysse - dt);
            if (_cooldownSortieVerrouAbysse > 0f)
            {
                velocity.X = 0f;
                velocity.Y = 0f;
                velocity.Z = 0f;
                Velocity = velocity;
                MoveAndSlide();
                return;
            }
            _verrouAntiChuteAbysseActif = false;
        }

        bool enAbysseLocal = _gestionnaireMonde != null && _gestionnaireMonde.EstDimensionLocaleAbysse();

        if (_modeCreatifAdmin)
        {
            Vector2 inputDirVol = uiBloquanteOuverte ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
            Vector3 directionVolHoriz = CalculerDirectionMouvementAuSol(inputDirVol);
            bool sprintVol = !uiBloquanteOuverte && Input.IsPhysicalKeyPressed(Key.Shift);
            bool montee = !uiBloquanteOuverte && (Input.IsActionPressed("jump") || Input.IsActionPressed("ui_accept"));
            bool descente = !uiBloquanteOuverte && Input.IsPhysicalKeyPressed(Key.Ctrl);
            float axeY = (montee ? 1f : 0f) - (descente ? 1f : 0f);

            float vitesseHoriz = Mathf.Max(0.1f, VitesseVolCreatifBase) * (sprintVol ? 1.35f : 1f);
            float vitesseVert = Mathf.Max(0.1f, VitesseVolCreatifVerticale) * (sprintVol ? 1.2f : 1f);
            Vector3 velocityCible = directionVolHoriz * vitesseHoriz;
            velocityCible.Y = axeY * vitesseVert;

            velocity = velocity.MoveToward(velocityCible, Mathf.Max(0.1f, AccelerationVolCreatif) * dt);
            float cap = Mathf.Max(1f, CapVitesseVolCreatif);
            if (velocity.LengthSquared() > cap * cap)
                velocity = velocity.Normalized() * cap;

            // Mode créatif/admin: aucune consommation d'endurance.
            _enduranceJoueur = EnduranceMaxJoueur;
            bool auSolPourAnimVol = IsOnFloor() || _bufferSolCoyoteAnim > 0f;
            MettreAJourAnimationHumain(dt, velocity, inputDirVol, auSolPourAnimVol, sprintVol, false);
            MettreAJourObjetTenueTps();

            Velocity = velocity;
            MoveAndSlide();
            SuivreEtAppliquerRisquesChuteOsJambes(estDansEau: false);
            // Pas de contrainte verticale auto ni d'enjambement: liberté de vol stricte.
            MettreAJourProgressionMetabolisteParDeplacement();
            if (ActiverProfilagePerfJoueur)
            {
                PerfBudgetMonitor.End("Joueur/Frame", debutFramePerfUs);
                if (_cooldownDrainProfilageJoueur >= Mathf.Max(0.2f, IntervalleLogProfilageJoueurSec))
                {
                    _cooldownDrainProfilageJoueur = 0f;
                    PerfBudgetMonitor.FlushSiEchu("Joueur", IntervalleLogProfilageJoueurSec);
                }
            }
            return;
        }

        bool estDansEau = EvaluerEtatEauJoueur(out float surfaceEau);
        bool sautMaintenu = !uiBloquanteOuverte && (Input.IsActionPressed("ui_accept") || Input.IsActionPressed("jump"));

        if (IsOnFloor())
        {
            _bufferSolCoyoteAnim = 0.18f;
            _bufferCoyoteSaut = 0.28f;
            _sautsAeriensEffectues = 0;
        }
        else
        {
            _bufferSolCoyoteAnim = Mathf.Max(0f, _bufferSolCoyoteAnim - dt);
            _bufferCoyoteSaut = Mathf.Max(0f, _bufferCoyoteSaut - dt);
        }

        _tamponSautRestant = Mathf.Max(0f, _tamponSautRestant - dt);
        if (!uiBloquanteOuverte && (Input.IsActionJustPressed("jump") || Input.IsActionJustPressed("ui_accept")))
            _tamponSautRestant = Mathf.Max(_tamponSautRestant, DureeTamponSautSecondes);

        bool auSolPourAnim = IsOnFloor() || _bufferSolCoyoteAnim > 0f;
        bool solAccepteSaut = IsOnFloor() || (_bufferCoyoteSaut > 0f && velocity.Y <= 0.05f);

        if (estDansEau)
        {
            float facteurFrottementXZ = sautMaintenu ? 0.92f : 0.88f;
            velocity.X *= facteurFrottementXZ;
            velocity.Z *= facteurFrottementXZ;
            velocity.Y *= 0.92f;

            if (sautMaintenu)
            {
                // Nage active: on autorise une montée franche pour ressortir de l'eau.
                float cibleY = surfaceEau + 0.12f;
                float erreurY = cibleY - GlobalPosition.Y;
                float vYCible = Mathf.Clamp(erreurY * 5.2f, -1.65f, 3.2f);
                velocity.Y = Mathf.MoveToward(velocity.Y, vYCible, 9.2f * dt);
            }
            else if (!IsOnFloor())
            {
                // Pas de nage active: aucune flottabilité montante automatique.
                // On conserve seulement une gravité atténuée sous l'eau.
                velocity += GetGravity() * (0.32f * dt);
            }

            // Évite vitesses verticales extrêmes (nage + bord d’eau + clip) qui peuvent faire planter le moteur physique.
            velocity.Y = Mathf.Clamp(velocity.Y, -4.5f, 4.5f);
        }
        else if (!IsOnFloor())
        {
            velocity += GetGravity() * dt;
        }

        bool sautDepuisSolStable = !estDansEau
            && _tamponSautRestant > 0f
            && solAccepteSaut;
        int sautsAeriensMax = Mathf.Max(0, ObtenirNombreSautsMaxAgiliter() - 1);
        bool sautAerienDisponible = !estDansEau
            && !solAccepteSaut
            && _tamponSautRestant > 0f
            && _sautsAeriensEffectues < sautsAeriensMax;
        if (!uiBloquanteOuverte && (sautDepuisSolStable || sautAerienDisponible))
        {
            velocity.Y = JumpVelocity * ObtenirMultiplicateurSautConsommationBaies();
            _tamponSautRestant = 0f;
            _bufferCoyoteSaut = 0f;
            if (sautAerienDisponible)
                _sautsAeriensEffectues++;
            AjouterXpFutureState("Agiliter", 1UL);
        }

        Vector2 inputDir = uiBloquanteOuverte ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        Vector3 direction = CalculerDirectionMouvementAuSol(inputDir);
        bool ctrlCourse = Input.IsPhysicalKeyPressed(Key.Ctrl);
        bool sprintDemande = !uiBloquanteOuverte && !estDansEau && ctrlCourse && direction != Vector3.Zero;
        bool sprintActif = sprintDemande && PeutSprinter();
        bool effortIntense = !uiBloquanteOuverte && (sprintActif || sautMaintenu || _gaucheMaintenu || _forceLancer > 0.15f || (direction != Vector3.Zero && estDansEau));
        ulong debutMetaboUs = ActiverProfilagePerfJoueur ? PerfBudgetMonitor.Begin() : 0UL;
        AppliquerMetabolismeJoueur(dt, effortIntense, sprintActif);
        if (ActiverProfilagePerfJoueur)
            PerfBudgetMonitor.End("Joueur/MetabolismeHud", debutMetaboUs);
        float vitesseMouvement = estDansEau ? Speed * (sautMaintenu ? 0.58f : 0.4f) : Speed;
        if (!estDansEau)
            vitesseMouvement *= FacteurVitesseMouvementAuSol;
        if (sprintActif)
            vitesseMouvement *= MultiplicateurVitesseSprint;
        vitesseMouvement *= ObtenirFacteurVitesseSelonChargePortee();
        vitesseMouvement *= ObtenirMultiplicateurVitesseMetaboliste();
        vitesseMouvement *= Mathf.Lerp(0.62f, 1f, RatioEnduranceJoueur());
        vitesseMouvement *= ObtenirFacteurVitesseSelonEtatOsJambes();
        vitesseMouvement *= ObtenirMultiplicateurVitesseConsommationBaies();

        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * vitesseMouvement;
            velocity.Z = direction.Z * vitesseMouvement;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, vitesseMouvement);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0, vitesseMouvement);
        }

        bool zoneLocalePrete = _gestionnaireMonde == null || _gestionnaireMonde.EstDeplacementLocalPret();
        if (zoneLocalePrete)
            _graceCollisionLocaleRestante = 0.22f;
        else
            _graceCollisionLocaleRestante = Mathf.Max(0f, _graceCollisionLocaleRestante - dt);
        if (enAbysseLocal)
        {
            _cooldownRetourSolAbysse = Mathf.Max(0f, _cooldownRetourSolAbysse - dt);
            if (zoneLocalePrete && IsOnFloor())
            {
                _positionSolideAbysseValide = true;
                _dernierePositionSolideAbysse = GlobalPosition;
            }
        }
        if (!zoneLocalePrete)
        {
            // Uniformise le ressenti inter-dimensions: même garde-fou que l'Abysse
            // quand la collision locale n'est pas encore prête.
            bool auSolStable = IsOnFloor() || _bufferSolCoyoteAnim > 0f;
            bool graceFrontiereActive = auSolStable && _graceCollisionLocaleRestante > 0f;
            if (!graceFrontiereActive)
            {
                float freinHoriz = Mathf.Max(10f, vitesseMouvement * 4.0f);
                velocity.X = Mathf.MoveToward(velocity.X, 0f, freinHoriz * dt);
                velocity.Z = Mathf.MoveToward(velocity.Z, 0f, freinHoriz * dt);
            }
            if (velocity.Y < -1.2f)
                velocity.Y = -1.2f;
        }

        MettreAJourAnimationHumain(dt, velocity, inputDir, auSolPourAnim, sprintActif, estDansEau);
        MettreAJourObjetTenueTps();

        Velocity = velocity;
        MoveAndSlide();
        SuivreEtAppliquerRisquesChuteOsJambes(estDansEau);
        if (!estDansEau
            && ActiverEnjambementObstacle
            && _cooldownEnjambementObstacle <= 0f
            && vitesseMouvement > 0.05f)
        {
            bool enjambement = StepAssistService.TryApplyStepAssist(
                this,
                new Vector3(Velocity.X, 0f, Velocity.Z),
                dt,
                HauteurMaxEnjambementObstacle,
                DistanceAvantEnjambementObstacle,
                VitesseMinEnjambementObstacle,
                NormalYMinSolEnjambementObstacle,
                NormalYMaxObstacleEnjambement);
            if (enjambement)
                _cooldownEnjambementObstacle = Mathf.Max(0.01f, CooldownEnjambementObstacleSec);
        }
        // DÃ©sactivÃ© en jeu normal : peut provoquer un "TP au sol" en retombÃ©e.
        if (_verrouSpawnActif)
            EssayerCollerCapsuleAuSolTerrain(estDansEau);
        AppliquerContrainteVerticaleHauteurTerrainMonde(estDansEau, ignorerSiMonteeSaut: true, dt);
        MettreAJourProgressionMetabolisteParDeplacement();
        if (ActiverProfilagePerfJoueur)
        {
            PerfBudgetMonitor.End("Joueur/Frame", debutFramePerfUs);
            if (_cooldownDrainProfilageJoueur >= Mathf.Max(0.2f, IntervalleLogProfilageJoueurSec))
            {
                _cooldownDrainProfilageJoueur = 0f;
                PerfBudgetMonitor.FlushSiEchu("Joueur", IntervalleLogProfilageJoueurSec);
            }
        }
    }

    private void EssayerCollerCapsuleAuSolTerrain(bool dansEau)
    {
        if (dansEau) return;
        if (IsOnFloor()) return;
        // Ne pas tirer vers le sol tant quâ€™on nâ€™est pas en chute nette : sinon le saut est mangÃ© dÃ¨s que Vy redescend sous 2.
        if (Velocity.Y > -0.55f) return;

        World3D w = GetWorld3D();
        if (w?.DirectSpaceState == null) return;

        float basLocalY = CalculerBasCollisionLocalJoueur();
        float origY = GlobalPosition.Y + basLocalY + 0.55f;
        Vector3 orig = new Vector3(GlobalPosition.X, origY, GlobalPosition.Z);
        var q = PhysicsRayQueryParameters3D.Create(orig, orig + new Vector3(0f, -520f, 0f));
        q.CollisionMask = 1;
        q.CollideWithAreas = false;
        q.CollideWithBodies = true;
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = w.DirectSpaceState.IntersectRay(q);
        if (hit.Count == 0 || !hit.ContainsKey("position")) return;
        float solY = ((Vector3)hit["position"]).Y;
        float basCapsuleY = GlobalPosition.Y + basLocalY;
        float gap = basCapsuleY - solY;
        // Ignorer les micro-corrections : elles crÃ©ent un tremblement visible en vue FPS.
        if (gap <= 0.14f || gap >= 140f) return;

        GlobalPosition += new Vector3(0f, -(gap - 0.08f), 0f);
        if (Velocity.Y <= 0.2f)
            Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
    }

    /// <summary>Quand le sol collision nâ€™est pas encore actif, colle le corps au champ de hauteur procÃ©dural (Ã©vite de Â« voler Â» quelques mÃ¨tres au-dessus du terrain).</summary>
    private void AppliquerContrainteVerticaleHauteurTerrainMonde(bool estDansEau, bool ignorerSiMonteeSaut, float dt)
    {
        if (_gestionnaireMonde == null || estDansEau) return;
        // En jeu normal : pas de rabattement sur le bruit procÃ©dural (casse saut, pentes, rebonds). RÃ©servÃ© au chargement / spawn.
        if (ignorerSiMonteeSaut) return;
        if (IsOnFloor()) return;

        int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(
            Mathf.FloorToInt(GlobalPosition.X),
            Mathf.FloorToInt(GlobalPosition.Z),
            _gestionnaireMonde.SeedTerrain);
        float ySurface = h + MargeSurfaceVoxelAuDessusH;
        float yCible = CalculerYOriginePourPiedsSurSurface(ySurface, MargeEpsilonPiedsSurSol);

        float y = GlobalPosition.Y;
        if (y <= yCible + 0.42f) return;

        float ny = y > yCible + 14f
            ? yCible
            : Mathf.MoveToward(y, yCible, Mathf.Max(28f, 55f * (y - yCible)) * dt);
        GlobalPosition = new Vector3(GlobalPosition.X, ny, GlobalPosition.Z);
        if (ny <= yCible + 0.06f)
            Velocity = new Vector3(Velocity.X, Mathf.Min(Velocity.Y, 0f), Velocity.Z);
    }
}
