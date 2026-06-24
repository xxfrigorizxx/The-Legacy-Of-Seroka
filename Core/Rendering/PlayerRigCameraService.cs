using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private void MettreAJourAnimationHumain(float dt, Vector3 vitesse, Vector2 entreeWasd, bool auSolPourAnim, bool sprintActif, bool dansEau)
    {
        if (_rigHumain == null || !GodotObject.IsInstanceValid(_rigHumain)) return;
        if (_animationHumain == null || _fallbackAnimProcedural)
            return;

        float vitesseHoriz = new Vector2(vitesse.X, vitesse.Z).Length();
        bool veutMarcher = entreeWasd.LengthSquared() > 0.02f;
        bool etaitAuSol = _etaitAuSolAnimPrecedent;

        string cibleClip = _clipIdleHumain;
        if ((veutMarcher || vitesseHoriz > 0.04f) && !string.IsNullOrEmpty(_clipWalkHumain))
            cibleClip = _clipWalkHumain;

        if (_playbackLocomotion != null && _animationTreeHumain != null && GodotObject.IsInstanceValid(_animationTreeHumain))
        {
            if (!_animationTreeHumain.Active)
                _animationTreeHumain.Active = true;

            StringName noeudNom = _playbackLocomotion.GetCurrentNode();
            string noeud = noeudNom.ToString();
            bool noeudVide = string.IsNullOrEmpty(noeud);

            if (_animationTreeContientSaut)
            {
                if (dansEau && noeud == NomEtatSautLocomotion)
                {
                    _playbackLocomotion.Travel(NomEtatDeplacementBlendString);
                    _dernierEtatLocomotionTree = NomEtatDeplacementBlend;
                }
                else if (!dansEau)
                {
                    if (noeud == NomEtatSautLocomotion)
                    {
                        if (auSolPourAnim || vitesse.Y <= 0.04f)
                        {
                            _playbackLocomotion.Travel(NomEtatDeplacementBlendString);
                            _dernierEtatLocomotionTree = NomEtatDeplacementBlend;
                        }
                    }
                    else if (noeud == NomEtatDeplacementBlend || noeudVide)
                    {
                        if (!auSolPourAnim && etaitAuSol && vitesse.Y > 0.08f)
                        {
                            _playbackLocomotion.Travel(NomEtatSautLocomotionString);
                            _dernierEtatLocomotionTree = NomEtatSautLocomotion;
                        }
                    }
                }
            }

            if (_animationTreeUtiliseBlendDeplacement)
            {
                if (noeud == NomEtatDeplacementBlend || noeudVide)
                {
                    float blendPos;
                    if (_locomotionBlendTroisPoints)
                    {
                        if (sprintActif)
                            blendPos = 1f;
                        else if (veutMarcher || vitesseHoriz > 0.04f)
                        {
                            float t = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.001f, Speed * 1.12f), 0f, 1f);
                            blendPos = t * BlendLocomotionMarcheMaxAvecCourse;
                        }
                        else
                            blendPos = 0f;
                    }
                    else
                        blendPos = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.001f, Speed), 0f, 1f);
                    if (float.IsNaN(_dernierBlendLocomotion) || Mathf.Abs(_dernierBlendLocomotion - blendPos) > 0.0001f)
                    {
                        _animationTreeHumain.Set(ParamBlendDeplacementLocomotion, blendPos);
                        _dernierBlendLocomotion = blendPos;
                    }
                }
            }
        }
        else
        {
            if (_animationTreeHumain != null && GodotObject.IsInstanceValid(_animationTreeHumain) && _animationTreeHumain.Active)
                _animationTreeHumain.Active = false;
            if (!string.IsNullOrEmpty(cibleClip) && (_animationHumain.CurrentAnimation != cibleClip || !_animationHumain.IsPlaying()))
                _animationHumain.Play(cibleClip, 0.12f);
        }

        _etaitAuSolAnimPrecedent = auSolPourAnim;

        bool arbrePilote = _playbackLocomotion != null && _animationTreeHumain != null && _animationTreeHumain.Active;
        float vitesseAnimation = arbrePilote
            ? 1f
            : Mathf.Lerp(0.92f, 1.35f, Mathf.Clamp(vitesseHoriz / Mathf.Max(0.001f, Speed), 0f, 1f));
        if (float.IsNaN(_derniereVitesseAnimationHumain) || Mathf.Abs(_derniereVitesseAnimationHumain - vitesseAnimation) > 0.0001f)
        {
            _animationHumain.SpeedScale = vitesseAnimation;
            _derniereVitesseAnimationHumain = vitesseAnimation;
        }
    }

    /// <summary>Avance / strafe alignÃ©s sur la vue camÃ©ra (plan XZ) : Ã©vite W qui part Â« sur le cÃ´tÃ© Â» quand le mesh a un yaw Mixamo diffÃ©rent du corps.</summary>
    private Vector3 CalculerDirectionMouvementAuSol(Vector2 inputDir)
    {
        if (inputDir.LengthSquared() < 1e-6f)
            return Vector3.Zero;

        Camera3D cam = _camera;
        if (cam == null)
            return (Transform.Basis * new Vector3(inputDir.X, 0f, inputDir.Y)).Normalized();

        Vector3 forward = -cam.GlobalTransform.Basis.Z;
        forward.Y = 0f;
        if (forward.LengthSquared() < 1e-6f)
            forward = -GlobalTransform.Basis.Z;
        forward = forward.Normalized();

        Vector3 right = cam.GlobalTransform.Basis.X;
        right.Y = 0f;
        if (right.LengthSquared() < 1e-6f)
            right = GlobalTransform.Basis.X;
        right = right.Normalized();

        // GetVector : Y nÃ©gatif = avant (W / move_forward).
        Vector3 dir = forward * (-inputDir.Y) + right * inputDir.X;
        return dir.LengthSquared() < 1e-6f ? Vector3.Zero : dir.Normalized();
    }

    /// <summary>Hitboxes sÃ©parÃ©es : si elles sont dÃ©jÃ  dans la scÃ¨ne (<c>HitboxCorps</c>), on les garde (Ã©diteur + jeu identiques).</summary>
    private void ConstruireHitboxesCompositeJoueur()
    {
        if (GetNodeOrNull("HitboxCorps") is CollisionShape3D deja && deja.Shape != null)
            return;

        foreach (Node c in GetChildren())
        {
            if (c is CollisionShape3D ancien)
            {
                RemoveChild(ancien);
                ancien.Free();
            }
        }

        void Ajouter(string nom, Shape3D forme, Vector3 pos, Vector3 rotDeg)
        {
            var cs = new CollisionShape3D { Name = nom, Shape = forme, Position = pos, RotationDegrees = rotDeg };
            AddChild(cs);
        }

        Ajouter("HitboxJambeG", new CapsuleShape3D { Radius = 0.075f, Height = 0.56f }, new Vector3(-0.11f, -0.44f, 0f), Vector3.Zero);
        Ajouter("HitboxJambeD", new CapsuleShape3D { Radius = 0.075f, Height = 0.56f }, new Vector3(0.11f, -0.44f, 0f), Vector3.Zero);
        Ajouter("HitboxCorps", new CapsuleShape3D { Radius = 0.19f, Height = 0.4f }, new Vector3(0f, 0.12f, 0f), Vector3.Zero);
        Ajouter("HitboxTete", new SphereShape3D { Radius = 0.105f }, new Vector3(0f, 0.58f, 0f), Vector3.Zero);
        Ajouter("HitboxBrasG", new CapsuleShape3D { Radius = 0.055f, Height = 0.34f }, new Vector3(-0.27f, 0.05f, 0f), new Vector3(0f, 0f, 72f));
        Ajouter("HitboxBrasD", new CapsuleShape3D { Radius = 0.055f, Height = 0.34f }, new Vector3(0.27f, 0.05f, 0f), new Vector3(0f, 0f, -72f));
    }

    /// <summary>Référence ~humain 1,7 m ; Orc +30 cm hauteur, +10 cm largeur/profondeur sur les collisions.</summary>
    private void RedimensionnerHitboxesSiOrc()
    {
        if (GameState.Instance?.RaceJoueurCourante != RaceJoueur.Orc) return;
        const float refH = 1.7f;
        const float refW = 0.45f;
        float fxz = (refW + 0.1f) / refW;
        float fy = (refH + 0.3f) / refH;
        foreach (Node c in GetChildren())
        {
            if (c is not CollisionShape3D cs || cs.Shape == null) continue;
            Vector3 p = cs.Position;
            cs.Position = new Vector3(p.X * fxz, p.Y * fy, p.Z * fxz);
            switch (cs.Shape)
            {
                case CapsuleShape3D cap0:
                {
                    var cap = new CapsuleShape3D
                    {
                        Radius = cap0.Radius * fxz,
                        Height = cap0.Height * fy
                    };
                    cs.Shape = cap;
                    break;
                }
                case SphereShape3D sp0:
                {
                    var sp = new SphereShape3D { Radius = sp0.Radius * Mathf.Max(fxz, fy) };
                    cs.Shape = sp;
                    break;
                }
                case BoxShape3D box0:
                {
                    var box = new BoxShape3D { Size = new Vector3(box0.Size.X * fxz, box0.Size.Y * fy, box0.Size.Z * fxz) };
                    cs.Shape = box;
                    break;
                }
            }
        }
    }

    /// <summary>Point bas (Y local) dâ€™une forme sous sa transform ; <see cref="float.MaxValue"/> si non gÃ©rÃ©e.</summary>
    private static float CalculerBasYLocalPourCollisionShape(CollisionShape3D cs)
    {
        if (cs?.Shape == null) return float.MaxValue;
        Transform3D t = cs.Transform;
        switch (cs.Shape)
        {
            case CapsuleShape3D cap:
            {
                float half = cap.Height * 0.5f + cap.Radius;
                return (t * new Vector3(0f, -half, 0f)).Y;
            }
            case SphereShape3D sph:
                return (t * new Vector3(0f, -sph.Radius, 0f)).Y;
            case BoxShape3D box:
            {
                float minY = float.MaxValue;
                Vector3 e = box.Size * 0.5f;
                for (int i = 0; i < 8; i++)
                {
                    float sx = (i & 1) != 0 ? e.X : -e.X;
                    float sy = (i & 2) != 0 ? e.Y : -e.Y;
                    float sz = (i & 4) != 0 ? e.Z : -e.Z;
                    minY = Mathf.Min(minY, (t * new Vector3(sx, sy, sz)).Y);
                }
                return minY;
            }
            default:
                return float.MaxValue;
        }
    }

    /// <summary>Bas local pour poser les pieds du mesh : capsule <see cref="NomCollisionReferencePieds"/> si prÃ©sente (mÃªme dÃ©sactivÃ©e), sinon hitboxes actives.</summary>
    private float CalculerBasPourAlignementPiedsDuMesh()
    {
        if (!float.IsNaN(ForcerBasCollisionLocalPourAlignementPieds))
            return ForcerBasCollisionLocalPourAlignementPieds;
        var csRef = GetNodeOrNull<CollisionShape3D>(NomCollisionReferencePieds);
        if (csRef != null && csRef.Shape != null)
        {
            float y = CalculerBasYLocalPourCollisionShape(csRef);
            if (y != float.MaxValue) return y;
        }
        return CalculerBasCollisionLocalJoueur();
    }

    /// <summary>Point le plus bas (Y local) des <see cref="CollisionShape3D"/> activÃ©es â€” physique / snap sol.</summary>
    private float CalculerBasCollisionLocalJoueur()
    {
        float minY = float.MaxValue;
        foreach (Node c in GetChildren())
        {
            if (c is not CollisionShape3D cs || cs.Disabled || cs.Shape == null) continue;
            float y = CalculerBasYLocalPourCollisionShape(cs);
            if (y != float.MaxValue) minY = Mathf.Min(minY, y);
        }

        return minY == float.MaxValue ? -0.9f : minY;
    }

    private void RetryLierPlaybackAnimationTreeHumain()
    {
        if (_essaisLiaisonPlaybackAnimationTree++ > 8) return;
        if (_animationTreeHumain == null || !GodotObject.IsInstanceValid(_animationTreeHumain) || _animationHumain == null) return;
        if (_playbackLocomotion != null) return;
        ApresAnimationTreePretLocomotion();
        if (_playbackLocomotion == null)
            Callable.From(RetryLierPlaybackAnimationTreeHumain).CallDeferred();
    }

    private void ConstruireRigCameraTps()
    {
        _pivotCameraTps = new Node3D
        {
            Name = "CameraPivotTPS",
            Position = new Vector3(0f, 1.55f, 0f),
            Rotation = new Vector3(_pitchCamera, 0f, 0f)
        };
        AddChild(_pivotCameraTps);

        _brasCameraTps = new SpringArm3D
        {
            Name = "SpringArmTPS",
            SpringLength = 3.35f,
            Margin = 0.08f,
            CollisionMask = 0xFFFFFFFF,
            Shape = new SphereShape3D { Radius = 0.2f }
        };
        _pivotCameraTps.AddChild(_brasCameraTps);

        _cameraTps = new Camera3D
        {
            Name = "CameraTPS",
            Current = false,
            Fov = 74f,
            Near = 0.03f,
            Far = 1600f
        };
        _brasCameraTps.AddChild(_cameraTps);

        _rayonTps = new RayCast3D
        {
            Name = "RayCastTPS",
            TargetPosition = new Vector3(0f, 0f, -14f),
            CollisionMask = 0xFFFFFFFF,
            Enabled = true
        };
        _cameraTps.AddChild(_rayonTps);
        _rayonTps.AddException(this);
    }

    /// <summary>Aligne le plan de coupe lointain sur la distance de rendu (chunks × taille) pour limiter le vide au bord du monde chargé.</summary>
    public void ConfigurerFarClipPourRenderDistance(int renderDistanceChunks, int tailleChunk)
    {
        float far = Mathf.Clamp(renderDistanceChunks * tailleChunk * 1.35f, 800f, 12000f);
        if (_cameraFps != null)
            _cameraFps.Far = far;
        if (_cameraTps != null)
            _cameraTps.Far = far;
    }

    private int TrouverOsParMotifs(Skeleton3D sk, params string[][] motifs)
    {
        if (sk == null) return -1;
        for (int m = 0; m < motifs.Length; m++)
        {
            string[] tokens = motifs[m];
            for (int i = 0; i < sk.GetBoneCount(); i++)
            {
                string nom = sk.GetBoneName(i).ToString().ToLowerInvariant();
                bool ok = true;
                for (int t = 0; t < tokens.Length; t++)
                {
                    if (!nom.Contains(tokens[t])) { ok = false; break; }
                }
                if (ok) return i;
            }
        }
        return -1;
    }

    private int TrouverOsParNomsAlternatifs(Skeleton3D sk, params string[] motifsOuNoms)
    {
        if (sk == null) return -1;
        for (int i = 0; i < sk.GetBoneCount(); i++)
        {
            string nom = sk.GetBoneName(i).ToString().ToLowerInvariant();
            for (int m = 0; m < motifsOuNoms.Length; m++)
            {
                string p = motifsOuNoms[m].ToLowerInvariant();
                if (nom.Contains(p)) return i;
            }
        }
        return -1;
    }

    private int TrouverRacineIkDepuisMainDroite(int osMainDroite)
    {
        if (_squeletteHumain == null || osMainDroite < 0) return -1;
        int parent = _squeletteHumain.GetBoneParent(osMainDroite);
        int fallback = -1;
        while (parent >= 0)
        {
            if (parent < osMainDroite && fallback < 0)
                fallback = parent;
            if (parent < osMainDroite)
            {
                string nom = _squeletteHumain.GetBoneName(parent).ToString().ToLowerInvariant();
                if (nom.Contains("forearm") || nom.Contains("lowerarm") || nom.Contains("upperarm") || nom.Contains("arm") || nom.Contains("shoulder") || nom.Contains("clavicle"))
                    return parent;
            }
            parent = _squeletteHumain.GetBoneParent(parent);
        }
        return fallback;
    }

    private void InitialiserSqueletteHumain()
    {
        _squeletteHumain = TrouverPremierNoeudDeType<Skeleton3D>(_rigHumain);
        if (_squeletteHumain == null) return;

        _osBrasDroit = TrouverOsParMotifs(_squeletteHumain, new[] { "right", "arm" }, new[] { "r", "upperarm" });
        _osAvantBrasDroit = TrouverOsParMotifs(_squeletteHumain, new[] { "right", "forearm" }, new[] { "right", "lowerarm" }, new[] { "r", "forearm" });
        _osMainDroite = TrouverOsParMotifs(_squeletteHumain, new[] { "right", "hand" }, new[] { "r", "hand" });
        _osEpauleDroite = TrouverOsParMotifs(_squeletteHumain, new[] { "right", "shoulder" }, new[] { "r", "shoulder" }, new[] { "clavicle", "right" });
        int osMainD = TrouverOsParMotifs(_squeletteHumain, new[] { "right", "hand" }, new[] { "r", "hand" });
        int osMainG = TrouverOsParMotifs(_squeletteHumain, new[] { "left", "hand" }, new[] { "l", "hand" });

        if (osMainD >= 0)
        {
            _attacheMainDroiteTps = new BoneAttachment3D { Name = "AttacheMainDroiteTPS", BoneIdx = osMainD };
            _squeletteHumain.AddChild(_attacheMainDroiteTps);
        }
        if (osMainG >= 0)
        {
            _attacheMainGaucheTps = new BoneAttachment3D { Name = "AttacheMainGaucheTPS", BoneIdx = osMainG };
            _squeletteHumain.AddChild(_attacheMainGaucheTps);
        }

        InitialiserAttachesEquipementsCorps();

        Node3D attacheActive = _attacheMainDroiteTps ?? _attacheMainGaucheTps;
        if (attacheActive != null)
        {
            _objetEnMain = new MeshInstance3D
            {
                Name = "ObjetEnMain",
                Position = new Vector3(0.035f, -0.01f, 0.065f),
                RotationDegrees = new Vector3(8f, 92f, -16f),
                Scale = Vector3.One * 0.9f
            };
            attacheActive.AddChild(_objetEnMain);
        }

        if (_ikBrasDroitFps != null && GodotObject.IsInstanceValid(_ikBrasDroitFps))
        {
            _ikBrasDroitFps.Stop();
            _ikBrasDroitFps.QueueFree();
            _ikBrasDroitFps = null;
        }
        if (_aimantIkMainDroite != null && GodotObject.IsInstanceValid(_aimantIkMainDroite))
        {
            _aimantIkMainDroite.QueueFree();
            _aimantIkMainDroite = null;
        }

        RafraichirVisuelsEquipementsCorps();

        if (_cameraFps != null && _osMainDroite >= 0)
        {
            _aimantIkMainDroite = new Marker3D { Name = "AimantMainDroiteIK" };
            _cameraFps.AddChild(_aimantIkMainDroite);
            _aimantIkMainDroite.Position = OffsetAimantMainDroiteFpsLocal;

            int osRacineIk = TrouverRacineIkDepuisMainDroite(_osMainDroite);
            if (osRacineIk < 0 || osRacineIk >= _osMainDroite)
            {
                GD.PrintErr($"ZERO-K : IK bras droit ignorÃ© â€” chaÃ®ne invalide (root={osRacineIk}, tip={_osMainDroite}).");
                return;
            }

            _ikBrasDroitFps = new SkeletonIK3D { Name = "IK_BrasDroitFPS" };
            _ikBrasDroitFps.RootBone = _squeletteHumain.GetBoneName(osRacineIk);
            _ikBrasDroitFps.TipBone = _squeletteHumain.GetBoneName(_osMainDroite);
            _squeletteHumain.AddChild(_ikBrasDroitFps);
            _ikBrasDroitFps.TargetNode = _ikBrasDroitFps.GetPathTo(_aimantIkMainDroite);
            _ikBrasDroitFps.Influence = 0f;
            _ikBrasDroitFps.Start();
        }
    }

    private void InitialiserAttachesEquipementsCorps()
    {
        if (_squeletteHumain == null) return;

        if (_attacheCeintureCorps != null && GodotObject.IsInstanceValid(_attacheCeintureCorps))
            _attacheCeintureCorps.QueueFree();
        if (_attacheDosCorps != null && GodotObject.IsInstanceValid(_attacheDosCorps))
            _attacheDosCorps.QueueFree();
        _attacheCeintureCorps = null;
        _attacheDosCorps = null;
        _supportVisuelCeinture = null;
        _supportVisuelSacDos = null;
        _signatureVisuelleCeintureEquipe = int.MinValue;
        _signatureVisuelleSacDosEquipe = int.MinValue;

        int osCeinture = TrouverOsParNomsAlternatifs(_squeletteHumain, "hips", "pelvis", "hanche", "bassin", "spine");
        int osDos = TrouverOsParNomsAlternatifs(_squeletteHumain, "spine2", "spine1", "spine", "chest", "upperchest", "torso");
        if (osDos < 0) osDos = osCeinture;

        if (osCeinture >= 0)
        {
            _attacheCeintureCorps = new BoneAttachment3D { Name = "AttacheCeintureCorps", BoneIdx = osCeinture };
            _squeletteHumain.AddChild(_attacheCeintureCorps);
            _supportVisuelCeinture = new Node3D { Name = "SupportVisuelCeinture" };
            _attacheCeintureCorps.AddChild(_supportVisuelCeinture);
        }
        else if (_rigHumain != null)
        {
            // Fallback si le rig importé n'expose pas d'os pelvis/hips correctement nommé.
            _supportVisuelCeinture = new Node3D { Name = "SupportVisuelCeintureFallback", Position = new Vector3(0f, -0.72f, 0f) };
            _rigHumain.AddChild(_supportVisuelCeinture);
        }
        if (osDos >= 0)
        {
            _attacheDosCorps = new BoneAttachment3D { Name = "AttacheDosCorps", BoneIdx = osDos };
            _squeletteHumain.AddChild(_attacheDosCorps);
            _supportVisuelSacDos = new Node3D { Name = "SupportVisuelSacDos" };
            _attacheDosCorps.AddChild(_supportVisuelSacDos);
        }
        else if (_rigHumain != null)
        {
            // Fallback dos sans os explicite.
            _supportVisuelSacDos = new Node3D { Name = "SupportVisuelSacDosFallback", Position = new Vector3(0f, -0.45f, -0.16f) };
            _rigHumain.AddChild(_supportVisuelSacDos);
        }
    }

    private void RafraichirVisuelsEquipementsCorps()
    {
        if (_supportVisuelCeinture != null && GodotObject.IsInstanceValid(_supportVisuelCeinture))
        {
            _supportVisuelCeinture.Position = OffsetCeintureEquipeLocal;
            _supportVisuelCeinture.RotationDegrees = RotationCeintureEquipeDeg;
        }
        if (_supportVisuelSacDos != null && GodotObject.IsInstanceValid(_supportVisuelSacDos))
        {
            _supportVisuelSacDos.Position = OffsetSacDosEquipeLocal;
            _supportVisuelSacDos.RotationDegrees = RotationSacDosEquipeDeg;
        }

        RafraichirVisuelCeintureEquipe();
        RafraichirVisuelSacDosEquipe();
    }

    private void RafraichirVisuelCeintureEquipe()
    {
        if (_supportVisuelCeinture == null || !GodotObject.IsInstanceValid(_supportVisuelCeinture))
            return;

        SlotInventaire ceinture = EquipementCeinture;
        int sig = ceinture.ID switch
        {
            IdObjetCeinturePoches => SignatureSlotCeinture102(ceinture),
            IdObjetCeintureSacoches => SignatureSlotCeinture104(ceinture),
            IdObjetPochetteTier0 => SignatureSlotPochette103(ceinture),
            _ => -1
        };
        bool modelePresent = _supportVisuelCeinture.FindChild("ModeleArme", true, false) != null;
        if (sig < 0)
        {
            if (_signatureVisuelleCeintureEquipe != -1 || modelePresent)
                NettoyerModelesEnfants(_supportVisuelCeinture);
            _signatureVisuelleCeintureEquipe = -1;
            return;
        }
        if (sig == _signatureVisuelleCeintureEquipe && modelePresent)
            return;

        if (ceinture.ID == IdObjetCeintureSacoches)
            InstancierModeleCeintureSacoches(_supportVisuelCeinture, ceinture, 0.24f);
        else if (ceinture.ID == IdObjetPochetteTier0)
            InstancierModelePochetteTier0(_supportVisuelCeinture, ceinture, 0.18f);
        else
            InstancierModeleCeinturePoches(_supportVisuelCeinture, ceinture, 0.22f);
        _signatureVisuelleCeintureEquipe = sig;
    }

    private void RafraichirVisuelSacDosEquipe()
    {
        if (_supportVisuelSacDos == null || !GodotObject.IsInstanceValid(_supportVisuelSacDos))
            return;

        SlotInventaire sac = EquipementSacDos;
        int sig = sac.ID == IdObjetSacTier0 ? SignatureSlotSac101(sac) : -1;
        bool modelePresent = _supportVisuelSacDos.FindChild("ModeleArme", true, false) != null;
        if (sig < 0)
        {
            if (_signatureVisuelleSacDosEquipe != -1 || modelePresent)
                NettoyerModelesEnfants(_supportVisuelSacDos);
            _signatureVisuelleSacDosEquipe = -1;
            return;
        }
        if (sig == _signatureVisuelleSacDosEquipe && modelePresent)
            return;

        InstancierModeleSacTier0(_supportVisuelSacDos, sac, 0.26f);
        _signatureVisuelleSacDosEquipe = sig;
    }

    /// <summary>Cou puis tÃªte (sans HeadTop) : camÃ©ra FPS sur le mÃªme squelette que la vue TPS.</summary>
    private int TrouverOsSupportCameraFps()
    {
        if (_squeletteHumain == null) return -1;
        int cou = TrouverOsParMotifs(_squeletteHumain, new[] { "neck" });
        if (cou < 0) cou = TrouverOsParNomsAlternatifs(_squeletteHumain, "neck", "cou");
        if (cou >= 0) return cou;
        for (int i = 0; i < _squeletteHumain.GetBoneCount(); i++)
        {
            string nom = _squeletteHumain.GetBoneName(i).ToString().ToLowerInvariant();
            if (nom.Contains("headtop")) continue;
            if (nom.Contains("head") || nom.Contains("tete")) return i;
        }
        return -1;
    }

    private void BrancherCameraFpsSurSquelette()
    {
        if (_cameraFps == null || _squeletteHumain == null) return;
        if (_attacheCameraFps != null && GodotObject.IsInstanceValid(_attacheCameraFps))
        {
            _attacheCameraFps.QueueFree();
            _attacheCameraFps = null;
        }

        // CamÃ©ra FPS volontairement dÃ©solidarisÃ©e du squelette pour Ã©viter les secousses d'animation.
        // RÃ©fÃ©rence visage : lÃ©gÃ¨rement en avant et un peu sous la ligne des yeux (proche bouche).
        if (_cameraFps.GetParent() != this)
            _cameraFps.Reparent(this);
        // AvanceCameraFpsMetres = 0 : camera sur l'axe du corps a hauteur des yeux, SANS poussee vers l'avant.
        // Tete/cou/cheveux etant masques pour la camera FPS (CalqueRenduTeteFpsCachee), inutile de la pousser
        // devant le visage : elle reste derriere la capsule (~0.19 m) -> on ne voit plus a travers les murs.
        float avanceCamera = Mathf.Max(0f, AvanceCameraFpsMetres);
        _cameraFps.Position = new Vector3(
            _positionLocaleBaseCameraFps.X,
            _positionLocaleBaseCameraFps.Y + 0.50f,
            _positionLocaleBaseCameraFps.Z - avanceCamera);
        _pitchCameraBaseRad = 0f;
        _yawCorrectionCameraFpsRad = 0f;
        _cameraFps.Rotation = new Vector3(_pitchCameraBaseRad + _pitchCamera, _yawCorrectionCameraFpsRad, 0f);
        // Near reduit : marge anti-clipping en plus contre les murs (Forward+/reverse-Z gere bien un near faible).
        _cameraFps.Near = 0.05f;
    }

    private static T TrouverPremierNoeudDeType<T>(Node racine) where T : Node
    {
        if (racine == null) return null;
        if (racine is T t) return t;
        foreach (Node enfant in racine.GetChildren())
        {
            T trouve = TrouverPremierNoeudDeType<T>(enfant);
            if (trouve != null) return trouve;
        }
        return null;
    }

    /// <summary>Le GLB peut contenir un AnimationPlayer interne : on le coupe pour que seul <see cref="NomNoeudAnimationPlayerLocomotion"/> pilote le rig.</summary>
    private static void DesactiverAutresAnimationPlayers(Node racine, AnimationPlayer garder)
    {
        if (racine == null) return;
        foreach (Node enfant in racine.GetChildren())
        {
            if (enfant is AnimationPlayer ap && ap != garder)
                ap.ProcessMode = ProcessModeEnum.Disabled;
            DesactiverAutresAnimationPlayers(enfant, garder);
        }
    }

    private void SecuriserMateriauxModeleHumain(Node n)
    {
        if (n is MeshInstance3D mi && mi.Mesh != null)
        {
            Color peau = CouleurPeauHumain;
            string nom = mi.Name.ToString().ToLowerInvariant();
            if (AppliquerMateriauxParSurfaceHumain(mi, peau))
            {
                mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
                foreach (Node c in n.GetChildren())
                    SecuriserMateriauxModeleHumain(c);
                return;
            }

            bool estSousVetement = EstNomMailleSousVetementHumain(nom);
            bool estPeau = EstNomMaillePeauHumain(nom);
            if (estSousVetement)
            {
                mi.MaterialOverride = CreerMateriauHumain(TextureSousVetementHumain ?? ObtenirTextureSousVetementProcedurale(), CouleurSousVetementHumain, 0.92f);
            }
            else if (estPeau)
            {
                mi.MaterialOverride = CreerMateriauHumain(TexturePeauHumain ?? ObtenirTexturePeauProcedurale(), peau, 0.88f);
            }
            else if (nom.Contains("eye"))
            {
                mi.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.14f, 0.2f, 0.25f, 1f),
                    Roughness = 0.6f,
                    Metallic = 0f
                };
            }
            else if (nom.Contains("lip") || nom.Contains("mouth"))
            {
                mi.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.6f, 0.34f, 0.34f, 1f),
                    Roughness = 0.82f,
                    Metallic = 0f
                };
            }
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        }
        foreach (Node c in n.GetChildren())
            SecuriserMateriauxModeleHumain(c);
    }

    /// <summary>Si le mesh contient plusieurs surfaces, on force la convention: 0=peau, 1=sous-vêtement, autres=peau.</summary>
    private bool AppliquerMateriauxParSurfaceHumain(MeshInstance3D mi, Color peau)
    {
        if (mi?.Mesh == null) return false;
        int surfaces = mi.Mesh.GetSurfaceCount();
        if (surfaces <= 1) return false;

        Texture2D texPeau = TexturePeauHumain ?? ObtenirTexturePeauProcedurale();
        Texture2D texSous = TextureSousVetementHumain ?? ObtenirTextureSousVetementProcedurale();
        for (int i = 0; i < surfaces; i++)
        {
            bool estSousVetement = i == 1;
            mi.SetSurfaceOverrideMaterial(i, CreerMateriauHumain(
                estSousVetement ? texSous : texPeau,
                estSousVetement ? CouleurSousVetementHumain : peau,
                estSousVetement ? 0.92f : 0.88f));
        }

        // Important: garder null sinon Godot écrase les matériaux de surface.
        mi.MaterialOverride = null;
        return true;
    }

    private static bool ContientUnMotCle(string nomLower, params string[] mots)
    {
        if (string.IsNullOrEmpty(nomLower)) return false;
        foreach (string mot in mots)
        {
            if (!string.IsNullOrEmpty(mot) && nomLower.Contains(mot))
                return true;
        }
        return false;
    }

    private static bool EstNomMailleSousVetementHumain(string nomLower)
    {
        return ContientUnMotCle(nomLower,
            "underwear", "under", "brief", "short", "panty", "culotte", "slip", "boxer", "sous", "vetement", "clothe", "cloth", "pants");
    }

    private static bool EstNomMaillePeauHumain(string nomLower)
    {
        return ContientUnMotCle(nomLower, "body", "skin", "head", "face", "arm", "leg", "hand", "foot", "torso", "neck", "ear", "nose");
    }

    private static StandardMaterial3D CreerMateriauHumain(Texture2D texture, Color couleur, float roughness)
    {
        return new StandardMaterial3D
        {
            AlbedoTexture = texture,
            AlbedoColor = couleur,
            Roughness = roughness,
            Metallic = 0f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
    }

    private Texture2D ObtenirTexturePeauProcedurale()
    {
        if (_texturePeauProcedurale != null) return _texturePeauProcedurale;
        Color c0 = CouleurPeauHumain;
        Color c1 = CouleurPeauHumain.Darkened(0.06f);
        Color c2 = CouleurPeauHumain.Lightened(0.05f);
        _texturePeauProcedurale = CreerTextureChecker4x4(c0, c1, c2, c1);
        return _texturePeauProcedurale;
    }

    private Texture2D ObtenirTextureSousVetementProcedurale()
    {
        if (_textureSousVetementProcedurale != null) return _textureSousVetementProcedurale;
        Color baseCol = CouleurSousVetementHumain;
        Color stripe = baseCol.Lightened(0.16f);
        _textureSousVetementProcedurale = CreerTextureChecker4x4(baseCol, stripe, baseCol.Darkened(0.1f), stripe.Darkened(0.08f));
        return _textureSousVetementProcedurale;
    }

    private static Texture2D CreerTextureChecker4x4(Color a, Color b, Color c, Color d)
    {
        Image img = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                bool left = x < 2;
                bool top = y < 2;
                Color col = (left, top) switch
                {
                    (true, true) => a,
                    (false, true) => b,
                    (true, false) => c,
                    _ => d
                };
                img.SetPixel(x, y, col);
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    private static bool EstNomMailleTeteOuCouPourFps(string nomLower)
    {
        if (string.IsNullOrEmpty(nomLower) || nomLower.Contains("headtop")) return false;
        if (nomLower.Contains("head") || nomLower.Contains("tete")) return true;
        if (nomLower.Contains("hair") || nomLower.Contains("scalp") || nomLower.Contains("cheveu")) return true;
        if (nomLower.Contains("face") || nomLower.Contains("visage")) return true;
        if (nomLower.Contains("skull") || nomLower.Contains("crane")) return true;
        if (nomLower.Contains("eye") || nomLower.Contains("oeil") || nomLower.Contains("lash") || nomLower.Contains("brow") || nomLower.Contains("tear"))
            return true;
        if (nomLower.Contains("teeth") || nomLower.Contains("tooth") || nomLower.Contains("dent") || nomLower.Contains("tongue") || nomLower.Contains("langue"))
            return true;
        if (nomLower.Contains("lip") || nomLower.Contains("mouth") || nomLower.Contains("bouche") || nomLower.Contains("gum")) return true;
        if (nomLower.Contains("ear") || nomLower.Contains("oreille")) return true;
        if (nomLower.Contains("nose") || nomLower.Contains("nez")) return true;
        if (nomLower.Contains("neck") || nomLower.Contains("cou") && !nomLower.Contains("accou")) return true;
        if (nomLower.Contains("beard") || nomLower.Contains("barbe") || nomLower.Contains("mustache") || nomLower.Contains("moustache"))
            return true;
        return false;
    }

    /// <summary>Place tous les visuels du rig local sur le calque cache FPS (2).</summary>
    private static void AssignerCalquesTetePourVueFps(Node n)
    {
        if (n is VisualInstance3D vi)
            vi.Layers = CalqueRenduTeteFpsCachee;
        foreach (Node c in n.GetChildren())
            AssignerCalquesTetePourVueFps(c);
    }

    private void AppliquerCullMasksCamerasJoueur()
    {
        if (_cameraFps != null)
            _cameraFps.CullMask = CalqueRenduCorpsEtMondeFps;
        if (_cameraTps != null)
            _cameraTps.CullMask = uint.MaxValue;
    }

    private void DetecterClipsAnimationHumain()
    {
        _animationHumain = _rigHumain?.GetNodeOrNull<AnimationPlayer>(NomNoeudAnimationPlayerLocomotion)
            ?? TrouverPremierNoeudDeType<AnimationPlayer>(_rigHumain);
        _clipIdleHumain = _clipWalkHumain = _clipRunHumain = _clipJumpHumain = "";
        _fallbackAnimProcedural = true;
        if (_animationHumain == null) return;

        var noms = _animationHumain.GetAnimationList();
        if (noms == null || noms.Length == 0)
            return;

        if (_animationHumain.HasAnimationLibrary(BibliothequeLocomotionMixamo))
        {
            var libLoc = _animationHumain.GetAnimationLibrary(BibliothequeLocomotionMixamo);
            string Pref(string clip) => $"{BibliothequeLocomotionMixamo}/{clip}";
            if (libLoc.HasAnimation("Idle"))
                _clipIdleHumain = Pref("Idle");
            if (libLoc.HasAnimation("Marche"))
                _clipWalkHumain = Pref("Marche");
            if (libLoc.HasAnimation("Run"))
                _clipRunHumain = Pref("Run");
            if (libLoc.HasAnimation("Saut"))
                _clipJumpHumain = Pref("Saut");
        }

        for (int i = 0; i < noms.Length; i++)
        {
            string nom = noms[i];
            string l = nom.ToLowerInvariant();
            if (string.IsNullOrEmpty(_clipIdleHumain) && (l.Contains("idle") || l.Contains("attente") || l.Contains("stand") || l.Contains("breathing")))
                _clipIdleHumain = nom;
            if (string.IsNullOrEmpty(_clipWalkHumain) && (l.Contains("walk") || l.Contains("marche") || l.Contains("jog") || l.Contains("stride")))
                _clipWalkHumain = nom;
            if (string.IsNullOrEmpty(_clipRunHumain) && (l.Contains("run") || l.Contains("course") || l.Contains("sprint") || l.Contains("courir")))
                _clipRunHumain = nom;
            if (string.IsNullOrEmpty(_clipJumpHumain) && (l.Contains("jump") || l.Contains("saut") || l.Contains("fall") || l.Contains("air")))
                _clipJumpHumain = nom;
        }

        if (string.IsNullOrEmpty(_clipIdleHumain))
            _clipIdleHumain = noms[0];

        if (string.IsNullOrEmpty(_clipWalkHumain))
            _clipWalkHumain = !string.IsNullOrEmpty(_clipRunHumain) ? _clipRunHumain : _clipIdleHumain;
        if (string.IsNullOrEmpty(_clipRunHumain))
            _clipRunHumain = _clipWalkHumain;

        _fallbackAnimProcedural = false;
        if (_playbackLocomotion == null && !string.IsNullOrEmpty(_clipIdleHumain))
            _animationHumain.Play(_clipIdleHumain);
    }

    private static Animation ExtrairePremiereAnimationDepuisJoueur(AnimationPlayer ap)
    {
        if (ap == null) return null;
        foreach (StringName nomLib in ap.GetAnimationLibraryList())
        {
            AnimationLibrary lib = ap.GetAnimationLibrary(nomLib);
            if (lib == null) continue;
            foreach (StringName nomAnim in lib.GetAnimationList())
            {
                Animation source = lib.GetAnimation(nomAnim);
                if (source != null)
                    return (Animation)source.Duplicate();
            }
        }
        return null;
    }

    private static void RemapperCheminsAnimationVersSqueletteHumain(Animation anim, string prefixeSqueletteFbx, string prefixeSqueletteHumain)
    {
        if (anim == null || string.IsNullOrEmpty(prefixeSqueletteFbx) || prefixeSqueletteHumain == null) return;
        for (int i = 0; i < anim.GetTrackCount(); i++)
        {
            string s = anim.TrackGetPath(i).ToString();
            if (s.StartsWith(prefixeSqueletteFbx, StringComparison.Ordinal))
                anim.TrackSetPath(i, new NodePath(prefixeSqueletteHumain + s.Substring(prefixeSqueletteFbx.Length)));
        }
    }

    /// <summary>Si le prÃ©fixe FBX ne matche pas (autre hiÃ©rarchie), recolle tout ce qui suit Â« Skeleton3D Â» au chemin du squelette sur le rig joueur.</summary>
    private static void RemapperCheminsAnimationParMarqueurSquelette(Animation anim, string cheminNoeudSqueletteHumain)
    {
        if (anim == null || string.IsNullOrEmpty(cheminNoeudSqueletteHumain)) return;
        const string marqueur = "Skeleton3D";
        for (int i = 0; i < anim.GetTrackCount(); i++)
        {
            string s = anim.TrackGetPath(i).ToString();
            int idx = s.IndexOf(marqueur, StringComparison.Ordinal);
            if (idx < 0) continue;
            string queue = s.Substring(idx + marqueur.Length);
            anim.TrackSetPath(i, new NodePath(cheminNoeudSqueletteHumain + queue));
        }
    }

    /// <summary>Retire les translations sur la racine du lecteur dâ€™anim : Ã©vite que le saut dÃ©place le corps (la hauteur reste 100% physique).</summary>
    private static void SupprimerPistesDeplacementRacinePourAnimSaut(Animation anim)
    {
        if (anim == null) return;
        for (int i = anim.GetTrackCount() - 1; i >= 0; i--)
        {
            string chemin = anim.TrackGetPath(i).ToString();
            if (chemin != ".." && !chemin.StartsWith("../", StringComparison.Ordinal))
                continue;
            if (anim.TrackGetType(i) == Animation.TrackType.Position3D)
                anim.RemoveTrack(i);
        }
    }

    /// <summary>Charge imobile / Marcher / Jump depuis les FBX et les enregistre dans la bibliothÃ¨que Â« locomotion Â» du rig (sans passage par lâ€™Ã©diteur Â« Save to File Â»).</summary>
    private void FusionnerAnimationsFbxVersRigHumain()
    {
        if (_rigHumain == null || _squeletteHumain == null) return;

        _animationHumain = _rigHumain.GetNodeOrNull<AnimationPlayer>(NomNoeudAnimationPlayerLocomotion);
        if (_animationHumain == null)
        {
            _animationHumain = new AnimationPlayer { Name = NomNoeudAnimationPlayerLocomotion };
            _rigHumain.AddChild(_animationHumain);
            _rigHumain.MoveChild(_animationHumain, 0);
        }

        // Les pistes sont remappÃ©es relativement au parent du lecteur (HumainRigRoot).
        _animationHumain.RootNode = new NodePath("..");
        _animationHumain.ProcessMode = ProcessModeEnum.Always;
        _animationHumain.Active = true;
        DesactiverAutresAnimationPlayers(_rigHumain, _animationHumain);

        if (!_animationHumain.HasAnimationLibrary(BibliothequeLocomotionMixamo))
            _animationHumain.AddAnimationLibrary(BibliothequeLocomotionMixamo, new AnimationLibrary());

        AnimationLibrary libLoc = _animationHumain.GetAnimationLibrary(BibliothequeLocomotionMixamo);
        if (libLoc == null) return;

        Node racineCheminsJoueur = _animationHumain.GetParent() ?? _rigHumain;
        string prefixHum = racineCheminsJoueur.GetPathTo(_squeletteHumain).ToString();
        GD.Print($"ZERO-K : AnimationPlayer Â« {NomNoeudAnimationPlayerLocomotion} Â» â€” pistes ciblent le squelette via Â« {prefixHum} Â» (parent lecteur = {racineCheminsJoueur.Name}).");

        void FusionnerUneSceneFbx(string cheminScene, StringName nomClip)
        {
            if (libLoc.HasAnimation(nomClip)) return;
            var sc = GD.Load<PackedScene>(cheminScene);
            if (sc == null)
            {
                GD.PrintErr($"ZERO-K : scÃ¨ne FBX introuvable : {cheminScene}");
                return;
            }
            Node temp = sc.Instantiate();
            var apFbx = TrouverPremierNoeudDeType<AnimationPlayer>(temp);
            Skeleton3D skFbx = TrouverPremierNoeudDeType<Skeleton3D>(temp);
            if (apFbx == null || skFbx == null)
            {
                GD.PrintErr($"ZERO-K : pas dâ€™AnimationPlayer ou Skeleton3D dans {cheminScene}");
                temp.QueueFree();
                return;
            }
            Node racineCheminsFbx = apFbx.GetParent() ?? temp;
            string prefixFbx = racineCheminsFbx.GetPathTo(skFbx).ToString();
            Animation anim = ExtrairePremiereAnimationDepuisJoueur(apFbx);
            temp.QueueFree();
            if (anim == null)
            {
                GD.PrintErr($"ZERO-K : aucune animation dans {cheminScene}");
                return;
            }
            // Les FBX Mixamo arrivent souvent sans boucle explicite : Idle/Marche doivent boucler en continu.
            if (nomClip == "Idle" || nomClip == "Marche" || nomClip == "Run")
                anim.LoopMode = Animation.LoopModeEnum.Linear;
            RemapperCheminsAnimationVersSqueletteHumain(anim, prefixFbx, prefixHum);
            RemapperCheminsAnimationParMarqueurSquelette(anim, prefixHum);
            if (nomClip == "Saut")
            {
                anim.LoopMode = Animation.LoopModeEnum.None;
                SupprimerPistesDeplacementRacinePourAnimSaut(anim);
            }
            libLoc.AddAnimation(nomClip, anim);
            GD.Print($"ZERO-K : clip Â« {nomClip} Â» fusionnÃ© ({cheminScene}) FBX:{prefixFbx} â†’ joueur:{prefixHum} â†’ {BibliothequeLocomotionMixamo}/{nomClip}.");
        }

        FusionnerUneSceneFbx("res://Modeles/Animations/imobile.fbx", "Idle");
        FusionnerUneSceneFbx("res://Modeles/Animations/Marcher.fbx", "Marche");
        if (ResourceLoader.Exists("res://Modeles/Animations/Courir.fbx"))
            FusionnerUneSceneFbx("res://Modeles/Animations/Courir.fbx", "Run");
        else if (ResourceLoader.Exists("res://Modeles/Animations/Run.fbx"))
            FusionnerUneSceneFbx("res://Modeles/Animations/Run.fbx", "Run");
        FusionnerUneSceneFbx("res://Modeles/Animations/Jump.fbx", "Saut");
    }

    private void ConfigurerAnimationTreeLocomotionHumain()
    {
        if (_animationTreeHumain != null && GodotObject.IsInstanceValid(_animationTreeHumain))
        {
            _animationTreeHumain.Active = false;
            _animationTreeHumain.QueueFree();
            _animationTreeHumain = null;
        }
        _playbackLocomotion = null;
        _dernierEtatLocomotionTree = "";
        _animationTreeContientSaut = false;
        _animationTreeUtiliseBlendDeplacement = false;
        _tentativesLecturePlaybackArbreLocomotion = 0;

        if (_animationHumain == null || _fallbackAnimProcedural) return;
        if (!_animationHumain.HasAnimationLibrary(BibliothequeLocomotionMixamo)) return;

        AnimationLibrary libLoc = _animationHumain.GetAnimationLibrary(BibliothequeLocomotionMixamo);
        if (libLoc == null || !libLoc.HasAnimation("Idle") || !libLoc.HasAnimation("Marche"))
            return;

        _animationTreeContientSaut = libLoc.HasAnimation("Saut");
        _locomotionBlendTroisPoints = libLoc.HasAnimation("Run");
        var nomIdle = new StringName($"{BibliothequeLocomotionMixamo}/Idle");
        var nomMarche = new StringName($"{BibliothequeLocomotionMixamo}/Marche");

        var blendIdle = new AnimationNodeAnimation { Animation = nomIdle };
        var blendMarche = new AnimationNodeAnimation { Animation = nomMarche };
        var blendDeplacement = new AnimationNodeBlendSpace1D { MinSpace = 0f, MaxSpace = 1f };
        blendDeplacement.AddBlendPoint(blendIdle, 0f);
        if (_locomotionBlendTroisPoints && libLoc.HasAnimation("Run"))
        {
            blendDeplacement.AddBlendPoint(blendMarche, BlendLocomotionMarcheMaxAvecCourse);
            var blendRun = new AnimationNodeAnimation { Animation = new StringName($"{BibliothequeLocomotionMixamo}/Run") };
            blendDeplacement.AddBlendPoint(blendRun, 1f);
        }
        else
            blendDeplacement.AddBlendPoint(blendMarche, 1f);

        var machine = new AnimationNodeStateMachine();
        machine.AddNode(NomEtatDeplacementBlend, blendDeplacement, new Vector2(240f, 120f));

        if (_animationTreeContientSaut)
        {
            var noeudSaut = new AnimationNodeAnimation { Animation = new StringName($"{BibliothequeLocomotionMixamo}/Saut") };
            machine.AddNode(NomEtatSautLocomotion, noeudSaut, new Vector2(240f, 280f));
        }

        const float XfadeLocomotion = 0.12f;
        var depuisStart = new AnimationNodeStateMachineTransition
        {
            XfadeTime = XfadeLocomotion,
            SwitchMode = AnimationNodeStateMachineTransition.SwitchModeEnum.Immediate
        };
        machine.AddTransition("Start", NomEtatDeplacementBlend, depuisStart);

        if (_animationTreeContientSaut)
        {
            machine.AddTransition(NomEtatDeplacementBlend, NomEtatSautLocomotion, new AnimationNodeStateMachineTransition { XfadeTime = 0.07f });
            // Retour avant la fin du clip (Ã©vite la pose Â« boule Â» en queue dâ€™anim) : dÃ¨s lâ€™apex ou le sol.
            machine.AddTransition(NomEtatSautLocomotion, NomEtatDeplacementBlend, new AnimationNodeStateMachineTransition { XfadeTime = 0.14f });
        }

        _animationTreeUtiliseBlendDeplacement = true;
        _animationTreeHumain = new AnimationTree { Name = "AnimationTreeLocomotion", ProcessMode = ProcessModeEnum.Always };
        _rigHumain.AddChild(_animationTreeHumain);
        _animationTreeHumain.TreeRoot = machine;
        _animationTreeHumain.AnimPlayer = _animationTreeHumain.GetPathTo(_animationHumain);
        _animationTreeHumain.Active = true;
        _playbackLocomotion = null;
        _dernierEtatLocomotionTree = "";
        Callable.From(ApresAnimationTreePretLocomotion).CallDeferred();
    }

    private void ApresAnimationTreePretLocomotion()
    {
        if (_animationTreeHumain == null || !GodotObject.IsInstanceValid(_animationTreeHumain) || _animationHumain == null)
            return;

        _animationTreeHumain.Active = true;
        _playbackLocomotion = ExtrairePlaybackMachineEtatAnimationTree();
        if (_playbackLocomotion == null)
        {
            if (++_tentativesLecturePlaybackArbreLocomotion > 15)
            {
                GD.PrintErr("ZERO-K : AnimationTree â€” Â« parameters/playback Â» introuvable. Lecture directe Idle sur AnimationPlayer.");
                _animationTreeHumain.QueueFree();
                _animationTreeHumain = null;
                _playbackLocomotion = null;
                if (!string.IsNullOrEmpty(_clipIdleHumain))
                    _animationHumain.Play(_clipIdleHumain, 0.08f);
                return;
            }

            Callable.From(ApresAnimationTreePretLocomotion).CallDeferred();
            return;
        }

        _tentativesLecturePlaybackArbreLocomotion = 0;
        _playbackLocomotion.Start(NomEtatDeplacementBlendString);
        _dernierEtatLocomotionTree = NomEtatDeplacementBlend;
    }

    private AnimationNodeStateMachinePlayback ExtrairePlaybackMachineEtatAnimationTree()
    {
        if (_animationTreeHumain == null) return null;
        Variant v = _animationTreeHumain.Get("parameters/playback");
        if (v.VariantType == Variant.Type.Nil) return null;
        return v.AsGodotObject() as AnimationNodeStateMachinePlayback;
    }

    private void InitialiserModeleHumainJoueur()
    {
        var capsuleVisuelle = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (capsuleVisuelle != null)
            capsuleVisuelle.Visible = false;

        RaceJoueur race = GameState.Instance?.RaceJoueurCourante ?? RaceJoueur.Humain;
        SexeJoueur sexe = GameState.Instance?.SexeJoueurCourante ?? SexeJoueur.Masculin;
        bool estOrc = race == RaceJoueur.Orc;
        string cheminVoulu = ObtenirCheminGlbCorpsJoueur(race, sexe);

        _rigHumain = GetNodeOrNull<Node3D>("HumainRigRoot");
        if (_rigHumain != null && GodotObject.IsInstanceValid(_rigHumain))
        {
            string cheminInstalle = _rigHumain.HasMeta(MetaCheminCorpsJoueurZk)
                ? _rigHumain.GetMeta(MetaCheminCorpsJoueurZk).AsString()
                : "";
            if (cheminInstalle != cheminVoulu)
            {
                RemoveChild(_rigHumain);
                _rigHumain.QueueFree();
                _rigHumain = null;
            }
        }

        if (_rigHumain == null)
        {
            PackedScene sceneCorps = GD.Load<PackedScene>(cheminVoulu);
            if (sceneCorps == null)
            {
                GD.PrintErr($"ZERO-K : Modèle joueur introuvable : {cheminVoulu}");
                return;
            }

            _rigHumain = sceneCorps.Instantiate<Node3D>();
            _rigHumain.Name = "HumainRigRoot";
            _rigHumain.SetMeta(MetaCheminCorpsJoueurZk, cheminVoulu);
            AddChild(_rigHumain);
        }

        AppliquerEchelleRigSelonRace(_rigHumain, estOrc ? RaceJoueur.Orc : RaceJoueur.Humain);

        _solCapsuleLocalY = CalculerBasCollisionLocalJoueur();
        float basPourPieds = CalculerBasPourAlignementPiedsDuMesh();
        float yRig = basPourPieds + HauteurPiedsSousPivotRigMixamo * _rigHumain.Scale.Y + DecalageYRigHumain;
        _rigHumain.Position = new Vector3(0f, yRig, 0f);
        _positionRigHumainVisible = _rigHumain.Position;
        _positionRigHumainVisibleInitialisee = true;

        Vector3 man = CorrectionManuelleEulerRigHumainDeg;
        _rigHumain.RotationDegrees = new Vector3(man.X, YawRigMixamoVersGodotDeg + man.Y, man.Z);

        if (race == RaceJoueur.Humain)
            SecuriserMateriauxModeleHumain(_rigHumain);
        AssignerCalquesTetePourVueFps(_rigHumain);
        InitialiserSqueletteHumain();
        BrancherCameraFpsSurSquelette();
        FusionnerAnimationsFbxVersRigHumain();
        DetecterClipsAnimationHumain();
        ConfigurerAnimationTreeLocomotionHumain();
        Callable.From(ForcerLectureAnimLocomotionSiArbreMort).CallDeferred();
    }

    /// <summary>Si lâ€™AnimationTree nâ€™a pas pris le relais, au moins jouer Idle sur le lecteur (Ã©vite T-pose figÃ©e).</summary>
    private void ForcerLectureAnimLocomotionSiArbreMort()
    {
        if (_animationHumain == null || !GodotObject.IsInstanceValid(_animationHumain)) return;
        bool arbreOk = _animationTreeHumain != null && GodotObject.IsInstanceValid(_animationTreeHumain) && _animationTreeHumain.Active && _playbackLocomotion != null;
        if (arbreOk) return;
        if (_animationTreeHumain != null && GodotObject.IsInstanceValid(_animationTreeHumain))
            _animationTreeHumain.Active = false;
        if (!string.IsNullOrEmpty(_clipIdleHumain))
            _animationHumain.Play(_clipIdleHumain, 0.08f);
    }

    /// <summary>Y global du <see cref="CharacterBody3D"/> pour que le bas des hitboxes soit juste au-dessus du contact sol (raycast / mesh).</summary>
    public float CalculerYOriginePourPiedsSurSurface(float yContactSolWorld, float epsilon = 0f)
    {
        if (epsilon <= 0f) epsilon = MargeEpsilonPiedsSurSol;
        return yContactSolWorld - CalculerBasCollisionLocalJoueur() + epsilon;
    }

    private void ConfigurerModeCamera(bool activerTps)
    {
        _vueTroisiemePersonne = activerTps;
        if (_cameraFps != null) _cameraFps.Current = !activerTps;
        if (_cameraTps != null) _cameraTps.Current = activerTps;
        if (_rayonFps != null) _rayonFps.Enabled = !activerTps;
        if (_rayonTps != null) _rayonTps.Enabled = activerTps;

        _camera = activerTps ? _cameraTps : _cameraFps;
        _rayon = activerTps ? _rayonTps : _rayonFps;

        AppliquerVisibiliteCorpsLocalSelonVue();

        AppliquerCullMasksCamerasJoueur();
        MettreAJourObjetTenueTps();
    }

    /// <summary>
    /// Désactive les caméras internes du joueur pour laisser une caméra externe (tests/cinématique) devenir active.
    /// N'affecte pas la partie normale tant qu'aucun runner de test ne l'appelle.
    /// </summary>
    public void DesactiverCamerasPourCameraExterne()
    {
        if (_cameraFps != null) _cameraFps.Current = false;
        if (_cameraTps != null) _cameraTps.Current = false;
        if (_rayonFps != null) _rayonFps.Enabled = false;
        if (_rayonTps != null) _rayonTps.Enabled = false;
    }

    private void BasculerModeCamera()
    {
        ConfigurerModeCamera(!_vueTroisiemePersonne);
        GD.Print(_vueTroisiemePersonne ? "ZERO-K : CamÃ©ra extÃ©rieure activÃ©e." : "ZERO-K : CamÃ©ra premiÃ¨re personne activÃ©e.");
    }

    private static bool EstToggleCameraF5(InputEvent e)
    {
        if (e == null) return false;
        if (e.IsActionPressed("toggle_camera_mode")) return true;
        if (e is InputEventKey k && k.Pressed && !k.Echo && (k.Keycode == Key.F5 || k.PhysicalKeycode == Key.F5))
            return true;
        return false;
    }

    private Node3D ObtenirAttacheMainActiveTps()
    {
        Node3D active = MainGaucheEstActive ? _attacheMainGaucheTps : _attacheMainDroiteTps;
        if (active == null) active = _attacheMainDroiteTps ?? _attacheMainGaucheTps;
        return active;
    }

    private void MettreAJourObjetTenueTps()
    {
        if (_objetEnMain == null || !GodotObject.IsInstanceValid(_objetEnMain)) return;
        bool vueFpsViewmodel = !_vueTroisiemePersonne && _cameraFps != null;
        Node3D parentCible = vueFpsViewmodel ? _cameraFps : ObtenirAttacheMainActiveTps();
        if (parentCible != null && _objetEnMain.GetParent() != parentCible)
        {
            _objetEnMain.Reparent(parentCible);
            _objetEnMain.Position = PositionObjetMainDefaut;
            _objetEnMain.RotationDegrees = RotationObjetMainDefautDeg;
            _objetEnMain.Scale = Vector3.One * 0.9f;
        }
        // Important: les objets procéduraux (ex: caillou) utilisent directement _objetEnMain.Mesh.
        // Si ce MeshInstance a été basculé sur le calque "tête cachée FPS" lors du masquage du rig,
        // il devient invisible en vue FPS (cull mask caméra = calque monde).
        _objetEnMain.Layers = CalqueRenduCorpsEtMondeFps;

        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        bool visible = !mainActive.EstVide && EstObjetAvecVisuel(mainActive.ID);
        bool frappeEnCours = _tweenFrappe != null && GodotObject.IsInstanceValid(_tweenFrappe) && _tweenFrappe.IsRunning();
        // IMPORTANT : MettreAJourObjetEnMain() recalcule rotation/scale selon le type d'objet.
        // Pendant le tween de frappe on Ã©vite de l'appeler pour ne pas Ã©craser la pose du coup.
        if (!frappeEnCours)
            MettreAJourObjetEnMain();
        _objetEnMain.Visible = visible;
        if (frappeEnCours)
            return;
        if (vueFpsViewmodel && visible)
        {
            // Viewmodel FPS : on garde la rotation dÃ©finie par MettreAJourObjetEnMain()
            // (inclut orientation par type + rotation manuelle X/Y/Z), sinon elle est Ã©crasÃ©e.
            _objetEnMain.Position = PositionObjetViewmodelFps;
        }
    }

    private bool DoitAfficherCorpsLocal()
    {
        // Règle stricte: en local, le corps est rendu uniquement en vue TPS.
        return _vueTroisiemePersonne;
    }

    private void AppliquerVisibiliteCorpsLocalSelonVue()
    {
        if (_rigHumain == null || !GodotObject.IsInstanceValid(_rigHumain))
            return;
        if (!_positionRigHumainVisibleInitialisee)
        {
            _positionRigHumainVisible = _rigHumain.Position;
            _positionRigHumainVisibleInitialisee = true;
        }
        // Sécurise le calque même si des sous-noeuds visuels sont ajoutés/rechargés.
        AssignerCalquesTetePourVueFps(_rigHumain);
        bool afficherCorps = DoitAfficherCorpsLocal();
        Vector3 positionCibleRig = afficherCorps
            ? _positionRigHumainVisible
            : _positionRigHumainVisible + new Vector3(0f, -1000f, 0f);
        if (_rigHumain.Position != positionCibleRig)
            _rigHumain.Position = positionCibleRig;
        if (_rigHumain.Visible != afficherCorps)
            _rigHumain.Visible = afficherCorps;
        AppliquerVisibiliteRecursiveRig(_rigHumain, afficherCorps);
        bool forcerMasquageGlobalFps = !_vueTroisiemePersonne && !afficherCorps;
        AppliquerMasquageVisuelsJoueurEnFps(forcerMasquageGlobalFps);
    }

    private static void AppliquerVisibiliteRecursiveRig(Node n, bool visible)
    {
        if (n is Node3D n3d)
            n3d.Visible = visible;
        foreach (Node enfant in n.GetChildren())
            AppliquerVisibiliteRecursiveRig(enfant, visible);
    }

    private void AppliquerMasquageVisuelsJoueurEnFps(bool activerMasquage)
    {
        Node racineMasquage = _rigHumain != null && GodotObject.IsInstanceValid(_rigHumain) ? _rigHumain : this;
        if (activerMasquage)
        {
            _instanceIdsVisuelsMasquesFps.Clear();
            // Ne jamais balayer tout l'arbre du Joueur: l'UI (SubViewport previews des slots)
            // vit sous ce même arbre et doit rester visible en FPS.
            MasquerVisuelsJoueurRecursif(racineMasquage);
            return;
        }
        RestaurerVisuelsMasquesFpsRecursif(racineMasquage);
        _instanceIdsVisuelsMasquesFps.Clear();
    }

    private void MasquerVisuelsJoueurRecursif(Node n)
    {
        foreach (Node enfant in n.GetChildren())
        {
            if (enfant is VisualInstance3D vi && GodotObject.IsInstanceValid(vi) && !DoitConserverVisualVisibleEnFps(vi))
            {
                if (vi.Visible)
                {
                    _instanceIdsVisuelsMasquesFps.Add(vi.GetInstanceId());
                    vi.Visible = false;
                }
            }
            MasquerVisuelsJoueurRecursif(enfant);
        }
    }

    private void RestaurerVisuelsMasquesFpsRecursif(Node n)
    {
        foreach (Node enfant in n.GetChildren())
        {
            if (enfant is VisualInstance3D vi && GodotObject.IsInstanceValid(vi))
            {
                if (_instanceIdsVisuelsMasquesFps.Contains(vi.GetInstanceId()))
                    vi.Visible = true;
            }
            RestaurerVisuelsMasquesFpsRecursif(enfant);
        }
    }

    private bool DoitConserverVisualVisibleEnFps(VisualInstance3D vi)
    {
        if (_objetEnMain == null || !GodotObject.IsInstanceValid(_objetEnMain))
            return false;
        return vi == _objetEnMain || vi.IsAncestorOf(_objetEnMain) || _objetEnMain.IsAncestorOf(vi);
    }

    private void DiagnostiquerVisuelsFpsRuntime()
    {
        if (_cameraFps == null || !GodotObject.IsInstanceValid(_cameraFps))
            return;

        uint cullMaskFps = _cameraFps.CullMask;
        var lignes = new List<string>();
        CollecterDiagnosticsVisuelsRecursif(this, cullMaskFps, lignes);
        GD.Print($"ZERO-K [DiagFPS] --- debut snapshot (fps={(!_vueTroisiemePersonne)}) ---");
        GD.Print($"ZERO-K [DiagFPS] cameraCullMask={cullMaskFps} visuelsTrouves={lignes.Count}");
        foreach (string l in lignes)
            GD.Print(l);
        GD.Print("ZERO-K [DiagFPS] --- fin snapshot ---");
    }

    private void CollecterDiagnosticsVisuelsRecursif(Node n, uint cullMaskFps, List<string> lignes)
    {
        foreach (Node enfant in n.GetChildren())
        {
            if (enfant is VisualInstance3D vi && GodotObject.IsInstanceValid(vi))
            {
                bool visibleArbre = vi.IsVisibleInTree();
                bool passeCullFps = (vi.Layers & cullMaskFps) != 0;
                bool sousRig = _rigHumain != null && GodotObject.IsInstanceValid(_rigHumain) && (_rigHumain == vi || _rigHumain.IsAncestorOf(vi));
                bool estObjetMain = DoitConserverVisualVisibleEnFps(vi);
                Vector3 pos = vi is Node3D n3d ? n3d.GlobalPosition : Vector3.Zero;
                lignes.Add($"ZERO-K [DiagFPS] node={vi.GetPath()} type={vi.GetType().Name} vis={visibleArbre} layers={vi.Layers} passeCullFps={passeCullFps} sousRig={sousRig} objetMain={estObjetMain} pos=({pos.X:F2},{pos.Y:F2},{pos.Z:F2})");
            }
            CollecterDiagnosticsVisuelsRecursif(enfant, cullMaskFps, lignes);
        }
    }

    private bool DetecterAnomalieVisuelleCorpsEnFps(out List<string> details)
    {
        details = new List<string>();
        if (_cameraFps == null || !GodotObject.IsInstanceValid(_cameraFps))
            return false;
        if (_rigHumain == null || !GodotObject.IsInstanceValid(_rigHumain))
            return false;

        uint cullMaskFps = _cameraFps.CullMask;
        CollecterAnomaliesRigRecursif(_rigHumain, cullMaskFps, details);
        return details.Count > 0;
    }

    private void CollecterAnomaliesRigRecursif(Node n, uint cullMaskFps, List<string> details)
    {
        foreach (Node enfant in n.GetChildren())
        {
            if (enfant is VisualInstance3D vi && GodotObject.IsInstanceValid(vi) && !DoitConserverVisualVisibleEnFps(vi))
            {
                bool visibleArbre = vi.IsVisibleInTree();
                bool passeCullFps = (vi.Layers & cullMaskFps) != 0;
                if (visibleArbre && passeCullFps)
                {
                    Vector3 pos = vi is Node3D n3d ? n3d.GlobalPosition : Vector3.Zero;
                    details.Add($"ZERO-K [DiagFPS-Alerte] node={vi.GetPath()} type={vi.GetType().Name} layers={vi.Layers} cullMask={cullMaskFps} pos=({pos.X:F2},{pos.Y:F2},{pos.Z:F2})");
                }
            }
            CollecterAnomaliesRigRecursif(enfant, cullMaskFps, details);
        }
    }

    private void ImpulserPoseBrasFrappe(TypeMouvementFrappe type)
    {
        _impulsionIkFrappePoids = 1f;
        _impulsionIkFrappeLocal = type switch
        {
            TypeMouvementFrappe.Estoc => new Vector3(0f, 0.02f, -0.24f),
            TypeMouvementFrappe.DeHautEnBas => new Vector3(0f, -0.18f, -0.18f),
            TypeMouvementFrappe.DeBasEnHaut => new Vector3(0f, 0.17f, -0.12f),
            TypeMouvementFrappe.GaucheADroite => new Vector3(0.18f, 0.02f, -0.12f),
            TypeMouvementFrappe.DroiteAGauche => new Vector3(-0.18f, 0.02f, -0.12f),
            _ => Vector3.Zero
        };
    }
}
