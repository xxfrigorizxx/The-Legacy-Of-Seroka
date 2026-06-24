using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public partial class BoeufSauvage : CharacterBody3D
{
	/// <summary>1) Lecteur sur le squelette 2) Fusion scenes -> <c>locomotion_faune</c> 3) Noms de clips 4) <see cref="AnimationTree"/> ou lecture directe.</summary>
	private void InitialiserAnimations()
	{
		DetruireAnimationTreeFaune();
		_machineAPorteBroutage = false;
		_machineAPorteMort = false;
		_machineAPorteNage = false;
		_machineAPorteSaut = false;
		_machineAPorteSautGalop = false;
		_machineAPorteAttaqueKick = false;
		_machineAPorteAttaqueTete = false;
		_clipIdle = _clipMarche = _clipCourse = _clipTrot = _clipNage = _clipManger = _clipMort = "";
		_clipSaut = _clipSautGalop = _clipAttaqueKick = _clipAttaqueTete = "";

		if (!ResoudreLecteurAnimationPrincipalSurSquelette())
			return;

		ChargerScenesAnimationEtFusionnerSurBibliothequeFaune();
		List<string> tous = CollecterCheminsAnimation(_animationPlayer);
		DiagnosticListeClipsSiDemande(tous);
		ResoudreNomsClipsLocomotionDepuisBibliothequeEtListe(tous);
		InitialiserSelectionEvolutionnaireAnimations(tous);
		_timerCycleIdleSecondes = (IntervalleMinCycleIdleSecondes + IntervalleMaxCycleIdleSecondes) * 0.5f;

		if (string.IsNullOrEmpty(_clipIdle))
		{
			GD.PrintErr("ZERO-K Faune : aucun clip d'animation exploitable sur le squelette.");
			_animationPlayer = null;
			return;
		}

		AppliquerBouclesSurClipsLocomotion();
		DemarrerArbreAnimationOuLectureDirecte();
		_squelletteModele = TrouverPremierNoeudDeType<Skeleton3D>(_modeleVisuel != null ? _modeleVisuel : this);
		AnalyserCompensationsEnfoncementClipsLocomotion();
	}

	private bool ResoudreLecteurAnimationPrincipalSurSquelette()
	{
		_animationPlayer = ChoisirMeilleurAnimationPlayer(_modeleVisuel != null ? _modeleVisuel : this);
		if (_animationPlayer == null || CompterClipsAnimation(_animationPlayer) == 0)
			_animationPlayer = ChoisirMeilleurAnimationPlayer(this);

		if (_animationPlayer != null && CompterClipsAnimation(_animationPlayer) > 0)
			return true;

		// GLB « squelette seul » (ex. Tripo) : pas d'AnimationPlayer importé — secours + message explicite.
		if (EssayerCreerLecteurEtFallbackLocomotionVisuelle())
			return true;

		GD.PrintErr("ZERO-K Faune : aucun AnimationPlayer avec clips sous le bovin (pas de nœud Modele ?).");
		return false;
	}

	/// <summary>
	/// Cree un lecteur sur le corps et des clips <c>locomotion_faune</c> minimaux (bob vertical du nœud Modele)
	/// quand le fichier glTF ne contient aucune animation. Pour un vrai cycle de marche, importez un GLB avec clips
	/// ou renseignez <see cref="CheminSceneGltfAnimationsExternesMemeRig"/> / les exports de scenes par action.
	/// </summary>
	private bool EssayerCreerLecteurEtFallbackLocomotionVisuelle()
	{
		if (_modeleVisuel == null)
			return false;

		AnimationPlayer ap = GetNodeOrNull<AnimationPlayer>("AnimationPlayerFauneCorps");
		if (ap == null || !GodotObject.IsInstanceValid(ap))
		{
			ap = new AnimationPlayer { Name = "AnimationPlayerFauneCorps" };
			AddChild(ap);
		}

		_animationPlayer = ap;
		Vector3 p0 = _modeleVisuel.Position;
		bool squelette = TrouverPremierNoeudDeType<Skeleton3D>(_modeleVisuel) != null;
		float m = squelette ? 0.22f : 1f;

		var lib = new AnimationLibrary();
		lib.AddAnimation("Idle", CreerAnimationBobPositionModele(p0, 0.028f * m, 2.6f));
		lib.AddAnimation("Marche", CreerAnimationBobPositionModele(p0, 0.065f * m, 0.52f, 1.15f, 1.00f));
		lib.AddAnimation("Course", CreerAnimationBobPositionModele(p0, 0.07f * m, 0.30f, 0.75f, 0.45f));
		lib.AddAnimation("Broutage", CreerAnimationBobPositionModele(p0, 0.018f * m, 3.1f, 0.45f, 0.35f));
		lib.AddAnimation("Mort", CreerAnimationBobPositionModele(p0, 0.006f * m, 1.35f, 0.12f, 0.08f, false));

		if (_animationPlayer.HasAnimationLibrary(NomBibliothequeLocomotionFaune))
			_animationPlayer.RemoveAnimationLibrary(NomBibliothequeLocomotionFaune);
		_animationPlayer.AddAnimationLibrary(NomBibliothequeLocomotionFaune, lib);

		GD.Print($"ZERO-K Faune : le mesh n'inclut pas d'animations glTF — fallback local minimal active ({lib.GetAnimationList().Count} clips). " +
			"Pour des clips reels, ajoutez un .glb avec animations ou renseignez CheminSceneGltfAnimationsExternesMemeRig.");
		return CompterClipsAnimation(_animationPlayer) > 0;
	}

	/// <summary>Vrai si seuls les clips <c>locomotion_faune</c> de secours (bob sur Modele) sont utilises — on peut alors animer le squelette en code.</summary>
	private bool EstFallbackLocomotionBobSeulement()
	{
		if (_animationPlayer == null || !_animationPlayer.HasAnimationLibrary(NomBibliothequeLocomotionFaune))
			return false;
		AnimationLibrary lib = _animationPlayer.GetAnimationLibrary(NomBibliothequeLocomotionFaune);
		if (lib == null || !lib.HasAnimation("Idle"))
			return false;
		Animation anim = lib.GetAnimation("Idle");
		if (anim == null || anim.GetTrackCount() < 1)
			return false;
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			if (!anim.TrackGetPath(i).ToString().Contains("Modele", StringComparison.Ordinal))
				return false;
		}
		return true;
	}

	/// <summary>Locomotion approximative sur les os (mesh Tripo sans clips glTF).</summary>
	private void AppliquerLocomotionSquelettiqueProcedural(float dt, float vitesseHoriz)
	{
		if (_squelletteModele == null || !GodotObject.IsInstanceValid(_squelletteModele))
			return;

		float rythme = 1f;
		if (_etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge)
			rythme = 1.5f;
		else if (vitesseHoriz > 0.12f)
			rythme = Mathf.Lerp(1f, 1.38f, Mathf.Clamp(vitesseHoriz / Mathf.Max(0.01f, VitesseMarche), 0f, 1f));
		else if (_etat == EtatBoeuf.Broutage)
			rythme = 0.62f;

		_phaseLocomotionSqueletteProcedurale += dt * Mathf.Tau * 0.88f * rythme;
		float walk = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.12f, VitesseMarche), 0f, 1.85f);
		if (_etat == EtatBoeuf.Broutage)
			walk = 0.2f;
		if (_etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge)
			walk = Mathf.Max(walk, 1.1f);

		_squelletteModele.ResetBonePoses();
		int n = _squelletteModele.GetBoneCount();
		for (int i = 1; i < n; i++)
		{
			Transform3D rest = _squelletteModele.GetBoneRest(i);
			Quaternion baseR = Quaternion.FromEuler(rest.Basis.Orthonormalized().GetEuler());
			float alt = (i & 1) == 0 ? 1f : -1f;
			float mag = Mathf.DegToRad(6.5f) * walk * alt;
			if (_etat == EtatBoeuf.Broutage)
				mag *= 0.4f;
			float ph = _phaseLocomotionSqueletteProcedurale + i * 0.48f;
			Quaternion swing = Quaternion.FromEuler(new Vector3(
				Mathf.Sin(ph) * mag,
				Mathf.Sin(ph * 0.48f) * mag * 0.3f,
				Mathf.Cos(ph * 0.82f) * mag * 0.22f));
			_squelletteModele.SetBonePoseRotation(i, baseR * swing);
		}
	}

	private static Animation CreerAnimationBobPositionModele(
		Vector3 baseLocal,
		float amplitudeY,
		float duree,
		float pitchMul = 1f,
		float rollMul = 1f,
		bool loop = true)
	{
		float len = Mathf.Max(0.35f, duree);
		var anim = new Animation
		{
			LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None,
			Length = len
		};
		int trPos = anim.AddTrack(Animation.TrackType.Position3D);
		anim.TrackSetPath(trPos, new NodePath("Modele"));
		anim.PositionTrackInsertKey(trPos, 0.0, baseLocal);
		anim.PositionTrackInsertKey(trPos, len * 0.25, baseLocal + new Vector3(0f, amplitudeY * 0.55f, 0f));
		anim.PositionTrackInsertKey(trPos, len * 0.5, baseLocal + new Vector3(0f, amplitudeY, 0f));
		anim.PositionTrackInsertKey(trPos, len * 0.75, baseLocal + new Vector3(0f, amplitudeY * 0.45f, 0f));
		anim.PositionTrackInsertKey(trPos, len, baseLocal);

		// Donne une impression de pas/respiration meme sans squelette.
		int trRot = anim.AddTrack(Animation.TrackType.Rotation3D);
		anim.TrackSetPath(trRot, new NodePath("Modele"));
		Vector3 r0 = Vector3.Zero;
		Vector3 r1 = new Vector3(Mathf.DegToRad(amplitudeY * 85f * pitchMul), 0f, Mathf.DegToRad(amplitudeY * 35f * rollMul));
		Vector3 r2 = new Vector3(Mathf.DegToRad(-amplitudeY * 95f * pitchMul), 0f, Mathf.DegToRad(-amplitudeY * 28f * rollMul));
		anim.RotationTrackInsertKey(trRot, 0.0, Quaternion.FromEuler(r0));
		anim.RotationTrackInsertKey(trRot, len * 0.25, Quaternion.FromEuler(r1));
		anim.RotationTrackInsertKey(trRot, len * 0.5, Quaternion.FromEuler(r0));
		anim.RotationTrackInsertKey(trRot, len * 0.75, Quaternion.FromEuler(r2));
		anim.RotationTrackInsertKey(trRot, len, Quaternion.FromEuler(r0));
		return anim;
	}

	private void ChargerScenesAnimationEtFusionnerSurBibliothequeFaune()
	{
		PreparerLecteurEtBibliothequeLocomotionFaune();
		if (FusionnerAutomatiquementAnimationsDuGlbReference && !string.IsNullOrWhiteSpace(CheminGlbSqueletteReference) && ResourceLoader.Exists(CheminGlbSqueletteReference))
			FusionnerAnimationsRemappeesDepuisSceneReference(CheminGlbSqueletteReference);
		FusionnerUneSceneAnimationVersBibliothequeFaune(CheminSceneAnimationIdle, "Idle");
		FusionnerUneSceneAnimationVersBibliothequeFaune(CheminSceneAnimationMarche, "Marche");
		FusionnerUneSceneAnimationVersBibliothequeFaune(CheminSceneAnimationCourse, "Course");
		FusionnerUneSceneAnimationVersBibliothequeFaune(CheminSceneAnimationBroutage, "Broutage");
		FusionnerUneSceneAnimationVersBibliothequeFaune(CheminSceneAnimationMort, "Mort");
		FusionnerBibliothequesDepuisGltfExterneMemeRig(); // export ou decouverte auto dans Modeles/Entites/Boeufs/
		FusionnerBibliothequesDepuisDossierAnimationsCompatibles();
	}

	private void DiagnosticListeClipsSiDemande(List<string> tous)
	{
		if (!AfficherDiagnosticClipsUneFois || tous.Count == 0) return;
		if (DiagnosticListeClipsDejaAffichePourProcessus)
			return;
		DiagnosticListeClipsDejaAffichePourProcessus = true;
		_diagnosticClipsAffiche = true;
		GD.Print($"ZERO-K Faune : lecteur {GetPathTo(_animationPlayer)} — {tous.Count} clip(s) : {string.Join(", ", tous)}");
	}

	private void ResoudreNomsClipsLocomotionDepuisBibliothequeEtListe(List<string> tous)
	{
		AppliquerClipsBibliothequeLocomotionFauneEnPriorite();

		string candidatGallopPur = "";
		string candidatCourseGenerique = "";

		foreach (string nomComplet in tous)
		{
			if (EstClipSystemeOuVide(nomComplet)) continue;
			string n = nomComplet.ToLowerInvariant();
			if (string.IsNullOrEmpty(_clipIdle) && !NomClipSembleMort(n) && !NomClipSembleReactionOuNonAmbiant(n) && (n.Contains("idle") || n.Contains("stand") || n.Contains("repos") || n.Contains("survey")))
				_clipIdle = nomComplet;
			if (string.IsNullOrEmpty(_clipMarche) && !NomClipSembleMort(n) && (n.Contains("walk") || n.Contains("marche") || n.Contains("locomotion") || n.Contains("cycle")))
				_clipMarche = nomComplet;
			if (string.IsNullOrEmpty(_clipSautGalop) && NomClipSembleSautGalop(n))
				_clipSautGalop = nomComplet;
			if (string.IsNullOrEmpty(_clipSaut) && n.Contains("jump") && !n.Contains("toidle") && !NomClipSembleSautGalop(n) && !NomClipSembleMort(n))
				_clipSaut = nomComplet;
			if (string.IsNullOrEmpty(_clipAttaqueKick) && !NomClipSembleMort(n) && ResoudreClipSembleAttaqueDerriere(n))
				_clipAttaqueKick = nomComplet;
			if (string.IsNullOrEmpty(_clipAttaqueTete) && !NomClipSembleMort(n) && ResoudreClipSembleAttaqueDevant(n))
				_clipAttaqueTete = nomComplet;
			if (string.IsNullOrEmpty(_clipTrot) && !NomClipSembleMort(n) && (n.Contains("trot") || n.Contains("jog") || n.Contains("lope")))
				_clipTrot = nomComplet;
			if (string.IsNullOrEmpty(_clipNage) && !NomClipSembleMort(n) && (n.Contains("swim") || n.Contains("paddle") || n.Contains("nage")))
				_clipNage = nomComplet;
			if (string.IsNullOrEmpty(_clipManger) && !NomClipSembleMort(n) && (n.Contains("eat") || n.Contains("eating") || n.Contains("graze") || n.Contains("chew") || n.Contains("manger") || n.Contains("browse")))
				_clipManger = nomComplet;
			if (string.IsNullOrEmpty(_clipMort) && (n.Contains("death") || n.Contains("dead") || n.Contains("die") || n.Contains("mort")))
				_clipMort = nomComplet;

			bool ressembleCourse = n.Contains("run") || n.Contains("gallop") || n.Contains("course") || n.Contains("charge");
			if (!NomClipSembleMort(n) && ressembleCourse
				&& !NomClipSembleSautGalop(n)
				&& !ResoudreClipSembleAttaqueDevant(n)
				&& !ResoudreClipSembleAttaqueDerriere(n)
				&& !(n.Contains("jump") && !n.Contains("gallop")))
			{
				if (string.IsNullOrEmpty(candidatGallopPur) && n.Contains("gallop") && !NomClipSembleSautGalop(n))
					candidatGallopPur = nomComplet;
				if (string.IsNullOrEmpty(candidatCourseGenerique))
					candidatCourseGenerique = nomComplet;
			}
		}

		if (string.IsNullOrEmpty(_clipCourse))
			_clipCourse = !string.IsNullOrEmpty(candidatGallopPur) ? candidatGallopPur : candidatCourseGenerique;

		if (string.IsNullOrEmpty(_clipMarche) || NomClipSembleMort(_clipMarche))
			_clipMarche = PremierClipLocomotionUtileNonMortel(tous);
		if (string.IsNullOrEmpty(_clipIdle) || NomClipSembleMort(_clipIdle))
			_clipIdle = !string.IsNullOrEmpty(_clipMarche) ? _clipMarche : PremierClipLocomotionUtileNonMortel(tous);
		if (string.IsNullOrEmpty(_clipCourse))
			_clipCourse = _clipMarche;
		if (string.IsNullOrEmpty(_clipTrot))
			_clipTrot = _clipMarche;
		if (string.IsNullOrEmpty(_clipNage))
			_clipNage = _clipCourse;
		if (string.IsNullOrEmpty(_clipManger) || NomClipSembleMort(_clipManger))
		{
			if (!string.IsNullOrEmpty(_clipIdle) && !NomClipSembleMort(_clipIdle))
				_clipManger = _clipIdle;
			else
				_clipManger = !string.IsNullOrEmpty(_clipMarche) ? _clipMarche : PremierClipLocomotionUtileNonMortel(tous);
		}
		if (!string.IsNullOrEmpty(_clipMort) && NomClipSembleMort(_clipManger) && _clipManger == _clipMort)
			_clipManger = !string.IsNullOrEmpty(_clipMarche) ? _clipMarche : _clipIdle;

		AppliquerClipsAttaqueBovinExacts(tous);

		// Secours : pack avec un seul clip "kick" / "headbutt" sans mots-cles directionnels.
		if (string.IsNullOrEmpty(_clipAttaqueKick))
		{
			foreach (string nomComplet in tous)
			{
				if (EstClipSystemeOuVide(nomComplet)) continue;
				string n = nomComplet.ToLowerInvariant();
				if (NomClipSembleMort(n) || ResoudreClipSembleAttaqueDevant(n)) continue;
				if (n.Contains("kick") && !n.Contains("walk") && !n.Contains("sidekick"))
				{
					_clipAttaqueKick = nomComplet;
					break;
				}
			}
		}
		if (string.IsNullOrEmpty(_clipAttaqueTete))
		{
			foreach (string nomComplet in tous)
			{
				if (EstClipSystemeOuVide(nomComplet)) continue;
				string n = nomComplet.ToLowerInvariant();
				if (NomClipSembleMort(n)) continue;
				if (n.Contains("headbutt") || (n.Contains("attack") && n.Contains("head")))
				{
					_clipAttaqueTete = nomComplet;
					break;
				}
			}
		}

		AppliquerNomsClipsExactsBovins(tous);
	}

	/// <summary>
	/// Liaison EXACTE des clips du pack bovin (Bull/Cow Quaternius) : garantit que les Strings envoyés à
	/// <see cref="AnimationNodeStateMachinePlayback.Travel"/> pointent sur le bon clip, sans dépendre uniquement
	/// des heuristiques de nom (source du « coup de tête à la place du repas »). Priorité finale sur la résolution floue.
	/// </summary>
	private void AppliquerNomsClipsExactsBovins(List<string> tous)
	{
		if (_animationPlayer == null || tous == null || tous.Count == 0)
			return;

		string TrouverParNomCourt(string nomCourtCible)
		{
			foreach (string nomComplet in tous)
			{
				if (EstClipSystemeOuVide(nomComplet))
					continue;
				if (string.Equals(ExtraireNomCourtClip(nomComplet), nomCourtCible, StringComparison.OrdinalIgnoreCase))
					return nomComplet;
			}
			return "";
		}

		void Lier(ref string cible, string nomCourt)
		{
			string trouve = TrouverParNomCourt(nomCourt);
			if (!string.IsNullOrEmpty(trouve) && _animationPlayer.HasAnimation(trouve))
				cible = trouve;
		}

		Lier(ref _clipMarche, "Walk");
		Lier(ref _clipCourse, "Gallop");
		Lier(ref _clipManger, "Eating");
		Lier(ref _clipMort, "Death");
		Lier(ref _clipSautGalop, "Gallop_Jump");
		Lier(ref _clipAttaqueTete, ClipAttaqueTeteCanonique); // Attack_Headbutt
		Lier(ref _clipAttaqueKick, ClipAttaqueKickCanonique); // Attack_Kick

		// Idle ambiant uniquement : Idle > Idle_Headlow > Idle_2. Jamais HitReact ni Jump_toIdle (clips parasites).
		string idle = TrouverParNomCourt("Idle");
		if (string.IsNullOrEmpty(idle)) idle = TrouverParNomCourt("Idle_Headlow");
		if (string.IsNullOrEmpty(idle)) idle = TrouverParNomCourt("Idle_2");
		if (!string.IsNullOrEmpty(idle) && _animationPlayer.HasAnimation(idle))
			_clipIdle = idle;

		// « Jump_toIdle » est une transition (atterrissage), pas un saut : ne jamais l'employer comme clip de saut.
		if (!string.IsNullOrEmpty(_clipSaut) && _clipSaut.ToLowerInvariant().Contains("toidle"))
			_clipSaut = "";
	}

	private static bool NomClipSembleSautGalop(string n)
	{
		if (string.IsNullOrEmpty(n)) return false;
		return n.Contains("gallop_jump") || n.Contains("gallopjump") || n.Contains("run_jump");
	}

	private static string ExtraireNomCourtClip(string nomComplet)
	{
		if (string.IsNullOrEmpty(nomComplet))
			return "";
		string n = nomComplet.Replace('\\', '/');
		int slash = n.LastIndexOf('/');
		return slash >= 0 ? n.Substring(slash + 1) : n;
	}

	private static bool NomClipEstAttaqueKickExact(string nomComplet)
		=> string.Equals(ExtraireNomCourtClip(nomComplet), ClipAttaqueKickCanonique, StringComparison.OrdinalIgnoreCase);

	private static bool NomClipEstAttaqueTeteExact(string nomComplet)
		=> string.Equals(ExtraireNomCourtClip(nomComplet), ClipAttaqueTeteCanonique, StringComparison.OrdinalIgnoreCase);

	private void EssayerAssignerClipAttaqueExact(ref string clipCible, string nomComplet)
	{
		if (EstClipSystemeOuVide(nomComplet) || _animationPlayer == null || !_animationPlayer.HasAnimation(nomComplet))
			return;
		if (!string.IsNullOrEmpty(clipCible))
		{
			bool actuelBull = clipCible.ToLowerInvariant().Contains("bull");
			bool nouveauBull = nomComplet.ToLowerInvariant().Contains("bull");
			if (actuelBull && !nouveauBull)
				return;
		}
		clipCible = nomComplet;
	}

	/// <summary>Force <see cref="ClipAttaqueKickCanonique"/> / <see cref="ClipAttaqueTeteCanonique"/> si presents dans le lecteur.</summary>
	private void AppliquerClipsAttaqueBovinExacts(List<string> tous)
	{
		if (_animationPlayer == null)
			return;

		if (_animationPlayer.HasAnimationLibrary(NomBibliothequeLocomotionFaune))
		{
			AnimationLibrary lib = _animationPlayer.GetAnimationLibrary(NomBibliothequeLocomotionFaune);
			string pref = $"{NomBibliothequeLocomotionFaune}/";
			if (lib != null)
			{
				if (lib.HasAnimation(ClipAttaqueKickCanonique))
					_clipAttaqueKick = pref + ClipAttaqueKickCanonique;
				if (lib.HasAnimation(ClipAttaqueTeteCanonique))
					_clipAttaqueTete = pref + ClipAttaqueTeteCanonique;
			}
		}

		foreach (string nomComplet in tous)
		{
			if (NomClipEstAttaqueKickExact(nomComplet))
				EssayerAssignerClipAttaqueExact(ref _clipAttaqueKick, nomComplet);
			if (NomClipEstAttaqueTeteExact(nomComplet))
				EssayerAssignerClipAttaqueExact(ref _clipAttaqueTete, nomComplet);
		}
	}

	private static bool ResoudreClipSembleAttaqueDerriere(string n)
	{
		if (n.Contains("headbutt") || (n.Contains("attack") && n.Contains("head")))
			return false;
		return n.Contains("attack_kick") || n.Contains("kick_back") || n.Contains("kick_rear") || n.Contains("rear_kick")
			|| n.Contains("back_kick") || (n.Contains("kick") && (n.Contains("back") || n.Contains("rear") || n.Contains("behind") || n.Contains("derriere")));
	}

	private static bool ResoudreClipSembleAttaqueDevant(string n)
	{
		return n.Contains("headbutt") || n.Contains("attack_head") || n.Contains("attack_headbutt")
			|| (n.Contains("attack") && n.Contains("head")) || n.Contains("coup_de_tete") || n.Contains("ram");
	}

	private void InitialiserSelectionEvolutionnaireAnimations(List<string> tous)
	{
		_poolsAnimationsEvolutives.Clear();
		InitialiserPoolCategorie("idle");
		InitialiserPoolCategorie("walk");
		InitialiserPoolCategorie("run");
		InitialiserPoolCategorie("trot");
		InitialiserPoolCategorie("graze");
		InitialiserPoolCategorie("swim");
		InitialiserPoolCategorie("death");
		InitialiserPoolCategorie("jump");
		InitialiserPoolCategorie("gallop_jump");
		InitialiserPoolCategorie("attack_kick");
		InitialiserPoolCategorie("attack_head");

		AjouterClipAuPool("idle", _clipIdle);
		AjouterClipAuPool("walk", _clipMarche);
		AjouterClipAuPool("run", _clipCourse);
		AjouterClipAuPool("trot", _clipTrot);
		AjouterClipAuPool("graze", _clipManger);
		AjouterClipAuPool("swim", _clipNage);
		AjouterClipAuPool("death", _clipMort);
		AjouterClipAuPool("jump", _clipSaut);
		AjouterClipAuPool("gallop_jump", _clipSautGalop);
		AjouterClipAuPool("attack_kick", _clipAttaqueKick);
		AjouterClipAuPool("attack_head", _clipAttaqueTete);

		if (tous != null)
		{
			foreach (string c in tous)
			{
				if (EstClipSystemeOuVide(c))
					continue;
				string n = c.ToLowerInvariant();
				if (!NomClipSembleMort(n) && (n.Contains("idle") || n.Contains("stand") || n.Contains("repos") || n.Contains("survey")))
					AjouterClipAuPool("idle", c);
				if (!NomClipSembleMort(n) && (n.Contains("walk") || n.Contains("marche") || n.Contains("locomotion") || n.Contains("cycle")))
					AjouterClipAuPool("walk", c);
				bool ressembleCourse = n.Contains("run") || n.Contains("gallop") || n.Contains("course") || n.Contains("charge");
				if (!NomClipSembleMort(n) && ressembleCourse
					&& !NomClipSembleSautGalop(n)
					&& !ResoudreClipSembleAttaqueDevant(n)
					&& !ResoudreClipSembleAttaqueDerriere(n)
					&& !(n.Contains("jump") && !n.Contains("gallop")))
					AjouterClipAuPool("run", c);
				if (!NomClipSembleMort(n) && (n.Contains("trot") || n.Contains("jog") || n.Contains("lope")))
					AjouterClipAuPool("trot", c);
				if (!NomClipSembleMort(n) && (n.Contains("eat") || n.Contains("eating") || n.Contains("graze") || n.Contains("chew") || n.Contains("manger") || n.Contains("browse")))
					AjouterClipAuPool("graze", c);
				if (!NomClipSembleMort(n) && (n.Contains("swim") || n.Contains("paddle") || n.Contains("nage")))
					AjouterClipAuPool("swim", c);
				if (n.Contains("death") || n.Contains("dead") || n.Contains("die") || n.Contains("mort"))
					AjouterClipAuPool("death", c);
				if (!NomClipSembleMort(n) && n.Contains("jump") && !n.Contains("toidle") && !NomClipSembleSautGalop(n))
					AjouterClipAuPool("jump", c);
				if (!NomClipSembleMort(n) && NomClipSembleSautGalop(n))
					AjouterClipAuPool("gallop_jump", c);
				if (!NomClipSembleMort(n) && ResoudreClipSembleAttaqueDerriere(n))
					AjouterClipAuPool("attack_kick", c);
				if (!NomClipSembleMort(n) && ResoudreClipSembleAttaqueDevant(n))
					AjouterClipAuPool("attack_head", c);
			}
		}

		ChargerRegistryAnimationsEvolutivesDepuisJson(tous ?? new List<string>());
		RemplirClipsSpeciauxDepuisPoolsSiEncoreVides();
		_signatureContexteAnimation = int.MinValue;
		AppliquerSelectionAnimationEvolutive(forceReconfigurerArbre: false);
		_signatureContexteAnimation = CalculerSignatureContexteAnimation(0f);
		_cooldownVariationAnimation = IntervalleVariationAnimationSecondes;
	}

	private void InitialiserPoolCategorie(string categorie)
	{
		if (!_poolsAnimationsEvolutives.ContainsKey(categorie))
			_poolsAnimationsEvolutives[categorie] = new List<string>();
	}

	private void AjouterClipAuPool(string categorie, string clip)
	{
		if (string.IsNullOrWhiteSpace(clip) || _animationPlayer == null || !_animationPlayer.HasAnimation(clip))
			return;
		// Le pool "idle" se cycle automatiquement quand le bovin est calme (et alimente le point de blend 0 de l'état Déplacement).
		// On en exclut donc tout clip de réaction/combat/saut (ex. Idle_HitReact1/2, Jump_toIdle) : sinon le bovin
		// joue un sursaut / coup de tête / saut « sans raison » au repos ou à faible vitesse. C'est la source des « animations parasites ».
		if (string.Equals(categorie, "idle", StringComparison.OrdinalIgnoreCase) && NomClipSembleReactionOuNonAmbiant(clip))
			return;
		InitialiserPoolCategorie(categorie);
		List<string> pool = _poolsAnimationsEvolutives[categorie];
		if (!pool.Contains(clip))
			pool.Add(clip);
	}

	/// <summary>Apres le JSON <see cref="CheminRegistryAnimationsFaune"/>, complete les clips sauts / attaques si la detection par nom seul les a rates.</summary>
	private void RemplirClipsSpeciauxDepuisPoolsSiEncoreVides()
	{
		if (_animationPlayer == null || !GodotObject.IsInstanceValid(_animationPlayer))
			return;

		void ChercherPremierClipValide(string categorie, ref string cible)
		{
			if (!string.IsNullOrEmpty(cible) && _animationPlayer.HasAnimation(cible))
				return;
			if (!_poolsAnimationsEvolutives.TryGetValue(categorie, out List<string> pool) || pool == null)
				return;
			for (int i = 0; i < pool.Count; i++)
			{
				string clip = pool[i];
				if (!string.IsNullOrEmpty(clip) && _animationPlayer.HasAnimation(clip))
				{
					cible = clip;
					return;
				}
			}
		}

		ChercherPremierClipValide("jump", ref _clipSaut);
		ChercherPremierClipValide("gallop_jump", ref _clipSautGalop);
		ChercherPremierClipValide("attack_kick", ref _clipAttaqueKick);
		ChercherPremierClipValide("attack_head", ref _clipAttaqueTete);
	}

	private void ChargerRegistryAnimationsEvolutivesDepuisJson(List<string> tous)
	{
		if (!ActiverSelectionEvolutionnaireAnimations || string.IsNullOrWhiteSpace(CheminRegistryAnimationsFaune))
			return;
		if (!FileAccess.FileExists(CheminRegistryAnimationsFaune))
			return;

		try
		{
			string contenu = FileAccess.GetFileAsString(CheminRegistryAnimationsFaune);
			if (string.IsNullOrWhiteSpace(contenu))
				return;
			using JsonDocument doc = JsonDocument.Parse(contenu);
			if (doc.RootElement.ValueKind != JsonValueKind.Object)
				return;
			if (!doc.RootElement.TryGetProperty("categories", out JsonElement categories) || categories.ValueKind != JsonValueKind.Object)
				return;

			foreach (JsonProperty entree in categories.EnumerateObject())
			{
				string categorie = entree.Name.ToLowerInvariant();
				if (entree.Value.ValueKind != JsonValueKind.Array)
					continue;
				foreach (JsonElement item in entree.Value.EnumerateArray())
				{
					if (item.ValueKind != JsonValueKind.String)
						continue;
					string motif = (item.GetString() ?? "").Trim();
					if (string.IsNullOrEmpty(motif))
						continue;
					string clip = ResoudreNomClipDepuisMotif(motif, tous);
					if (!string.IsNullOrEmpty(clip))
						AjouterClipAuPool(categorie, clip);
				}
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K Faune : registre d'animations invalide ({CheminRegistryAnimationsFaune}) : {ex.Message}");
		}
	}

	private static string ResoudreNomClipDepuisMotif(string motif, List<string> tous)
	{
		if (string.IsNullOrWhiteSpace(motif) || tous == null || tous.Count == 0)
			return "";
		for (int i = 0; i < tous.Count; i++)
		{
			if (string.Equals(tous[i], motif, StringComparison.OrdinalIgnoreCase))
				return tous[i];
		}

		string m = motif.ToLowerInvariant();
		string mUnderscore = m.Replace(" ", "_");
		for (int i = 0; i < tous.Count; i++)
		{
			string c = tous[i];
			if (c.EndsWith("/" + motif, StringComparison.OrdinalIgnoreCase))
				return c;
			string n = c.ToLowerInvariant();
			if (n.Contains(m, StringComparison.Ordinal))
				return c;
			if (mUnderscore != m && n.Contains(mUnderscore, StringComparison.Ordinal))
				return c;
		}
		return "";
	}

	private void CalculerScoresContexteAnimation(
		out float scoreCalme, out float scoreDynamique, out float scoreBroutage, out float scoreNage, out float stress)
	{
		stress = (_tempsFuite > 0f || _memoireDetectionJoueur > 0f || _etat == EtatBoeuf.Charge) ? 1f : 0f;
		float faim = RatioFaimCourant();
		float cohesion = CalculerRatioCohesionTroupeau();
		scoreCalme = Mathf.Clamp(_geneConfiance * 0.55f + faim * 0.30f + cohesion * 0.15f - stress * 0.35f, 0f, 1f);
		scoreDynamique = Mathf.Clamp(_geneReflexeAttaque * 0.45f + _geneReflexeFuite * 0.35f + stress * 0.30f, 0f, 1f);
		if (_etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge)
			scoreDynamique = Mathf.Max(scoreDynamique, 0.82f);
		scoreBroutage = Mathf.Clamp(faim * 0.55f + _geneConfiance * 0.30f - stress * 0.45f, 0f, 1f);
		if (_etat == EtatBoeuf.Broutage)
			scoreBroutage = Mathf.Max(scoreBroutage, 0.88f);
		scoreNage = Mathf.Clamp(stress * 0.30f + (1f - faim) * 0.25f + _geneReflexeFuite * 0.25f + cohesion * 0.20f, 0f, 1f);
		if (_dansEau)
			scoreNage = Mathf.Max(scoreNage, 0.85f);
	}

	private int CalculerSignatureContexteAnimation(float vitesseHoriz)
	{
		int etat = (int)_etat;
		int eau = _dansEau ? 1 : 0;
		int stress = (_tempsFuite > 0f || _memoireDetectionJoueur > 0f || _etat == EtatBoeuf.Charge) ? 1 : 0;
		int faimBucket = Mathf.Clamp(Mathf.FloorToInt(RatioFaimCourant() * 4f), 0, 3);
		int vitBucket = vitesseHoriz <= 0.15f ? 0 : (vitesseHoriz <= 0.55f ? 1 : (vitesseHoriz <= 2.2f ? 2 : 3));
		return etat | (eau << 4) | (stress << 5) | (faimBucket << 6) | (vitBucket << 8);
	}

	private bool ContexteCalmeStablePourVariationClip(float vitesseHoriz)
	{
		if (_etat == EtatBoeuf.Mort || _etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge)
			return false;
		if (_tempsVerrouAnimationCombat > 0.01f || _impactChargeJoueurPlanifie)
			return false;
		if (_memoireDetectionJoueur > 0f || _tempsFuite > 0f)
			return false;
		if (vitesseHoriz > 0.18f)
			return false;
		return true;
	}

	private void MettreAJourVariationClipsContextuelle(float dt, float vitesseHoriz)
	{
		if (!ActiverSelectionEvolutionnaireAnimations || _animationPlayer == null)
			return;

		if (_etat != _etatPourClipsLocomotion)
		{
			EtatBoeuf ancien = _etatPourClipsLocomotion;
			_etatPourClipsLocomotion = _etat;
			if (ancien != (EtatBoeuf)(-1))
				AppliquerClipsLocomotionSelonEtat(ancien, _etat, forceReconfigurerArbre: UtiliserAnimationTreeLocomotion);
			_signatureContexteAnimation = CalculerSignatureContexteAnimation(vitesseHoriz);
			_tempsStableCalmePourClip = 0f;
			return;
		}

		bool calme = ContexteCalmeStablePourVariationClip(vitesseHoriz);
		if (calme)
			_tempsStableCalmePourClip += dt;
		else
			_tempsStableCalmePourClip = 0f;

		int signature = CalculerSignatureContexteAnimation(vitesseHoriz);
		bool contexteChange = signature != _signatureContexteAnimation;
		if (!contexteChange || _tempsStableCalmePourClip < TempsStabiliteAvantChangementClipSec)
			return;
		if (_cooldownVariationAnimation > 0f)
			return;

		_signatureContexteAnimation = signature;
		AppliquerSelectionAnimationEvolutive(forceReconfigurerArbre: UtiliserAnimationTreeLocomotion);
		_cooldownVariationAnimation = Mathf.Max(8f, IntervalleVariationAnimationSecondes);
	}

	private void AppliquerClipsLocomotionSelonEtat(EtatBoeuf ancien, EtatBoeuf nouveau, bool forceReconfigurerArbre)
	{
		if (_animationPlayer == null)
			return;
		CalculerScoresContexteAnimation(out float scoreCalme, out float scoreDynamique, out float scoreBroutage, out float scoreNage, out _);
		float intensite = Mathf.Clamp(IntensiteSelectionAnimationEvolutive, 0f, 1f);
		float MixScore(float s, float defaut) => s * intensite + (1f - intensite) * defaut;

		switch (nouveau)
		{
			case EtatBoeuf.Fuite:
			case EtatBoeuf.Charge:
				_clipCourse = ChoisirClipDepuisPoolEvolutif("run", _clipCourse, MixScore(scoreDynamique, 0.85f));
				_clipTrot = ChoisirClipDepuisPoolEvolutif("trot", _clipTrot, MixScore(scoreDynamique, 0.75f));
				break;
			case EtatBoeuf.Broutage:
				_clipManger = ChoisirClipDepuisPoolEvolutif("graze", _clipManger, MixScore(scoreBroutage, 0.9f));
				break;
			default:
				if (ancien == EtatBoeuf.Fuite || ancien == EtatBoeuf.Charge)
				{
					_clipMarche = ChoisirClipDepuisPoolEvolutif("walk", _clipMarche, MixScore(scoreCalme, 0.45f));
					_clipIdle = ChoisirClipDepuisPoolEvolutif("idle", _clipIdle, MixScore(scoreCalme, 0.5f));
				}
				break;
		}

		if (_dansEau)
			_clipNage = ChoisirClipDepuisPoolEvolutif("swim", _clipNage, MixScore(scoreNage, 0.85f));

		AppliquerBouclesSurClipsLocomotion();
		if (forceReconfigurerArbre)
			DemanderReconfigurationAnimationTree();
	}

	private void AppliquerSelectionAnimationEvolutive(bool forceReconfigurerArbre)
	{
		if (!ActiverSelectionEvolutionnaireAnimations || _animationPlayer == null)
			return;

		CalculerScoresContexteAnimation(out float scoreCalme, out float scoreDynamique, out float scoreBroutage, out float scoreNage, out _);
		float intensite = Mathf.Clamp(IntensiteSelectionAnimationEvolutive, 0f, 1f);

		string ancienIdle = _clipIdle;
		string ancienMarche = _clipMarche;
		string ancienCourse = _clipCourse;
		string ancienTrot = _clipTrot;
		string ancienManger = _clipManger;
		string ancienNage = _clipNage;

		bool idleMultiples = _poolsAnimationsEvolutives.TryGetValue("idle", out List<string> poolIdle) && poolIdle != null && poolIdle.Count >= 2;
		if (!idleMultiples)
			_clipIdle = ChoisirClipDepuisPoolEvolutif("idle", _clipIdle, scoreCalme * intensite + (1f - intensite) * 0.5f);
		_clipMarche = ChoisirClipDepuisPoolEvolutif("walk", _clipMarche, (scoreCalme * 0.45f + scoreDynamique * 0.55f) * intensite + (1f - intensite) * 0.5f);
		_clipCourse = ChoisirClipDepuisPoolEvolutif("run", _clipCourse, scoreDynamique * intensite + (1f - intensite) * 0.5f);
		_clipTrot = ChoisirClipDepuisPoolEvolutif("trot", _clipTrot, (scoreCalme * 0.3f + scoreDynamique * 0.7f) * intensite + (1f - intensite) * 0.5f);
		_clipManger = ChoisirClipDepuisPoolEvolutif("graze", _clipManger, scoreBroutage * intensite + (1f - intensite) * 0.5f);
		_clipNage = ChoisirClipDepuisPoolEvolutif("swim", _clipNage, scoreNage * intensite + (1f - intensite) * 0.5f);
		_clipMort = ChoisirClipDepuisPoolEvolutif("death", _clipMort, 0.5f);

		bool change =
			(!idleMultiples && ancienIdle != _clipIdle) ||
			ancienMarche != _clipMarche ||
			ancienCourse != _clipCourse ||
			ancienTrot != _clipTrot ||
			ancienManger != _clipManger ||
			ancienNage != _clipNage;

		if (!change)
			return;

		AppliquerBouclesSurClipsLocomotion();
		if (forceReconfigurerArbre && UtiliserAnimationTreeLocomotion)
			DemanderReconfigurationAnimationTree();
	}

	/// <summary>Choix déterministe : le score (0–1) mappe à un index du pool, sans bruit aléatoire.</summary>
	private string ChoisirClipDepuisPoolEvolutif(string categorie, string fallback, float score)
	{
		if (!_poolsAnimationsEvolutives.TryGetValue(categorie, out List<string> pool) || pool.Count == 0)
			return fallback;
		if (pool.Count == 1)
			return pool[0];

		float s = Mathf.Clamp(score, 0f, 1f);
		int idx = Mathf.Clamp(Mathf.RoundToInt(s * (pool.Count - 1)), 0, pool.Count - 1);
		string choisi = pool[idx];
		return string.IsNullOrEmpty(choisi) ? fallback : choisi;
	}

	/// <summary>Nœud AnimationTree placé dans la scène (éditeur) : nom attendu, typo fréquente, ou premier enfant direct.</summary>
	private AnimationTree TrouverAnimationTreeConfigureDansLaScene()
	{
		AnimationTree t = GetNodeOrNull<AnimationTree>(NomNoeudAnimationTreeFauneEditeur);
		if (t != null && GodotObject.IsInstanceValid(t))
			return t;
		t = GetNodeOrNull<AnimationTree>(NomNoeudAnimationTreeFauTypo);
		if (t != null && GodotObject.IsInstanceValid(t))
			return t;
		foreach (Node c in GetChildren())
		{
			if (c is AnimationTree at && GodotObject.IsInstanceValid(at))
				return at;
		}
		return null;
	}

	private void DemarrerArbreAnimationOuLectureDirecte()
	{
		if (UtiliserAnimationTreeLocomotion)
			ConfigurerAnimationTreeFaune();
		else
		{
			DetruireAnimationTreeFaune();
			_animationPlayer.ProcessMode = ProcessModeEnum.Always;
			_animationPlayer.Active = true;
			_animationPlayer.Play(new StringName(_clipIdle), 0.12f);
		}
	}

	private void DemanderReconfigurationAnimationTree()
	{
		if (_etat == EtatBoeuf.Mort)
			return;
		if (!UtiliserAnimationTreeLocomotion)
			return;
		if (_cooldownReconfigurationArbreAnimation <= 0f)
		{
			ConfigurerAnimationTreeFaune();
			_cooldownReconfigurationArbreAnimation = Mathf.Max(0.05f, CooldownReconfigurationAnimationTreeSec);
			_reconfigurationArbreAnimationEnAttente = false;
			return;
		}
		_reconfigurationArbreAnimationEnAttente = true;
	}

	private void ConfigurerAnimationTreeFaune()
	{
		if (_etat == EtatBoeuf.Mort || _cadavreLootDistribue)
		{
			if (!_cadavreLootDistribue)
				AppliquerAnimationMort();
			return;
		}
		DetruireAnimationTreeFaune();
		if (_animationPlayer == null)
			return;

		AnimationTree arbreEditeur = TrouverAnimationTreeConfigureDansLaScene();
		if (arbreEditeur != null && GodotObject.IsInstanceValid(arbreEditeur))
		{
			_animationTreeFaune = arbreEditeur;
			_animationTreeCreeParScript = false;
			if (_animationTreeFaune.Name != NomNoeudAnimationTreeFauneEditeur)
				_animationTreeFaune.Name = NomNoeudAnimationTreeFauneEditeur;
		}
		else
		{
			_animationTreeFaune = new AnimationTree { Name = NomNoeudAnimationTreeFauneEditeur };
			Node parentArbre = _animationPlayer.GetParent() ?? _modeleVisuel ?? (Node)this;
			parentArbre.AddChild(_animationTreeFaune);
			_animationTreeCreeParScript = true;
		}

		var blend = new AnimationNodeBlendSpace1D { MinSpace = 0f, MaxSpace = 1f };
		blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipIdle) }, 0f);
		bool marcheDiff = !string.IsNullOrEmpty(_clipMarche) && _clipMarche != _clipIdle;
		bool courseDiff = !string.IsNullOrEmpty(_clipCourse) && _clipCourse != _clipMarche;
		bool trotDiff = !string.IsNullOrEmpty(_clipTrot) && _clipTrot != _clipMarche && _clipTrot != _clipCourse;
		if (marcheDiff)
			blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipMarche) }, 0.55f);
		if (trotDiff)
			blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipTrot) }, 0.78f);
		if (courseDiff)
			blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipCourse) }, 1f);
		else if (!marcheDiff)
			blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipIdle) }, 1f);
		else
			blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipMarche) }, 1f);

		var machine = new AnimationNodeStateMachine();
		machine.AddNode(NomNoeudDeplacement, blend, new Vector2(220f, 120f));

		bool PorteClip(string c) => !string.IsNullOrEmpty(c) && _animationPlayer.HasAnimation(c);

		_machineAPorteBroutage = !string.IsNullOrEmpty(_clipManger) && _clipManger != _clipIdle;
		_machineAPorteMort = !string.IsNullOrEmpty(_clipMort);
		_machineAPorteNage = !string.IsNullOrEmpty(_clipNage) && _clipNage != _clipMarche && _clipNage != _clipCourse;
		_machineAPorteSaut = PorteClip(_clipSaut);
		_machineAPorteSautGalop = PorteClip(_clipSautGalop);
		_machineAPorteAttaqueKick = PorteClip(_clipAttaqueKick);
		_machineAPorteAttaqueTete = PorteClip(_clipAttaqueTete);

		if (_machineAPorteBroutage)
			machine.AddNode(NomNoeudBroutage, new AnimationNodeAnimation { Animation = new StringName(_clipManger) }, new Vector2(460f, 40f));
		if (_machineAPorteMort)
			machine.AddNode(NomNoeudMort, new AnimationNodeAnimation { Animation = new StringName(_clipMort) }, new Vector2(460f, 220f));
		if (_machineAPorteNage)
			machine.AddNode("Nage", new AnimationNodeAnimation { Animation = new StringName(_clipNage) }, new Vector2(460f, 320f));
		if (_machineAPorteSaut)
			machine.AddNode(NomNoeudSaut, new AnimationNodeAnimation { Animation = new StringName(_clipSaut) }, new Vector2(40f, 0f));
		if (_machineAPorteSautGalop)
			machine.AddNode(NomNoeudSautGalop, new AnimationNodeAnimation { Animation = new StringName(_clipSautGalop) }, new Vector2(40f, 72f));
		if (_machineAPorteAttaqueKick)
			machine.AddNode(NomNoeudAttaqueKick, new AnimationNodeAnimation { Animation = new StringName(_clipAttaqueKick) }, new Vector2(680f, 100f));
		if (_machineAPorteAttaqueTete)
			machine.AddNode(NomNoeudAttaqueTete, new AnimationNodeAnimation { Animation = new StringName(_clipAttaqueTete) }, new Vector2(680f, 200f));

		const float xfade = 0.14f;
		var depuisStart = new AnimationNodeStateMachineTransition
		{
			XfadeTime = xfade,
			SwitchMode = AnimationNodeStateMachineTransition.SwitchModeEnum.Immediate
		};
		machine.AddTransition("Start", NomNoeudDeplacement, depuisStart);

		var allerBroutage = new AnimationNodeStateMachineTransition { XfadeTime = xfade };
		var quitterBroutage = new AnimationNodeStateMachineTransition { XfadeTime = xfade };
		if (_machineAPorteBroutage)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudBroutage, allerBroutage);
			machine.AddTransition(NomNoeudBroutage, NomNoeudDeplacement, quitterBroutage);
		}
		if (_machineAPorteNage)
		{
			machine.AddTransition(NomNoeudDeplacement, "Nage", new AnimationNodeStateMachineTransition { XfadeTime = 0.12f });
			machine.AddTransition("Nage", NomNoeudDeplacement, new AnimationNodeStateMachineTransition { XfadeTime = 0.12f });
		}

		var xfdSaut = new AnimationNodeStateMachineTransition { XfadeTime = 0.11f };
		var xfdSautRetour = new AnimationNodeStateMachineTransition { XfadeTime = 0.14f };
		if (_machineAPorteSaut)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudSaut, xfdSaut);
			machine.AddTransition(NomNoeudSaut, NomNoeudDeplacement, xfdSautRetour);
		}
		if (_machineAPorteSautGalop)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudSautGalop, xfdSaut);
			machine.AddTransition(NomNoeudSautGalop, NomNoeudDeplacement, xfdSautRetour);
		}
		var xfdAttaque = new AnimationNodeStateMachineTransition { XfadeTime = 0.08f };
		var xfdAttaqueRetour = new AnimationNodeStateMachineTransition { XfadeTime = 0.1f };
		if (_machineAPorteAttaqueKick)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudAttaqueKick, xfdAttaque);
			machine.AddTransition(NomNoeudAttaqueKick, NomNoeudDeplacement, xfdAttaqueRetour);
			if (_machineAPorteSaut)
				machine.AddTransition(NomNoeudSaut, NomNoeudAttaqueKick, xfdAttaque);
			if (_machineAPorteSautGalop)
				machine.AddTransition(NomNoeudSautGalop, NomNoeudAttaqueKick, xfdAttaque);
		}
		if (_machineAPorteAttaqueTete)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudAttaqueTete, xfdAttaque);
			machine.AddTransition(NomNoeudAttaqueTete, NomNoeudDeplacement, xfdAttaqueRetour);
			if (_machineAPorteSaut)
				machine.AddTransition(NomNoeudSaut, NomNoeudAttaqueTete, xfdAttaque);
			if (_machineAPorteSautGalop)
				machine.AddTransition(NomNoeudSautGalop, NomNoeudAttaqueTete, xfdAttaque);
		}

		var versMort = new AnimationNodeStateMachineTransition { XfadeTime = 0.1f, SwitchMode = AnimationNodeStateMachineTransition.SwitchModeEnum.Immediate };
		if (_machineAPorteMort)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudMort, versMort);
			if (_machineAPorteBroutage)
				machine.AddTransition(NomNoeudBroutage, NomNoeudMort, versMort);
			if (_machineAPorteSaut)
				machine.AddTransition(NomNoeudSaut, NomNoeudMort, versMort);
			if (_machineAPorteSautGalop)
				machine.AddTransition(NomNoeudSautGalop, NomNoeudMort, versMort);
			if (_machineAPorteAttaqueKick)
				machine.AddTransition(NomNoeudAttaqueKick, NomNoeudMort, versMort);
			if (_machineAPorteAttaqueTete)
				machine.AddTransition(NomNoeudAttaqueTete, NomNoeudMort, versMort);
		}

		_animationTreeFaune.ProcessMode = ProcessModeEnum.Always;
		_animationTreeFaune.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
		_animationTreeFaune.TreeRoot = machine;
		_animationTreeFaune.AnimPlayer = _animationTreeFaune.GetPathTo(_animationPlayer);
		_animationTreeFaune.Active = false;
		_blendLocomotionActif = false;
		_playbackEtatFaune = null;
		_etatCourantMachineAnimation = "";
		_tentativesLiaisonPlaybackArbre = 0;
		Callable.From(ApresAnimationTreePretFaune).CallDeferred();
	}

	private void ApresAnimationTreePretFaune()
	{
		if (_animationTreeFaune == null || !GodotObject.IsInstanceValid(_animationTreeFaune) || _animationPlayer == null)
			return;

		if (_etat == EtatBoeuf.Mort || _cadavreLootDistribue || IsQueuedForDeletion())
		{
			_tentativesLiaisonPlaybackArbre = 0;
			DesactiverAnimationTreePourCadavre();
			if (_etat == EtatBoeuf.Mort && !_cadavreLootDistribue && !_animationMortFigee)
				AppliquerAnimationMort();
			return;
		}

		_animationTreeFaune.Active = true;
		_playbackEtatFaune = ExtrairePlaybackMachineEtatFaune();
		if (_playbackEtatFaune == null)
		{
			_animationTreeFaune.Active = false;
			if (_tentativesLiaisonPlaybackArbre++ > MaxTentativesLiaisonPlaybackArbre)
			{
				GD.PrintErr("ZERO-K Faune : AnimationTree — playback introuvable apres plusieurs frames, bascule lecture directe (desactivez UtiliserAnimationTreeLocomotion si besoin).");
				DetruireAnimationTreeFaune();
				if (!string.IsNullOrEmpty(_clipIdle))
					_animationPlayer.Play(new StringName(_clipIdle), 0.12f);
				return;
			}

			Callable.From(ApresAnimationTreePretFaune).CallDeferred();
			return;
		}

		_tentativesLiaisonPlaybackArbre = 0;
		_blendLocomotionActif = true;
		_playbackEtatFaune.Start(NomNoeudDeplacementString);
		_etatCourantMachineAnimation = NomNoeudDeplacement;
	}

	private AnimationNodeStateMachinePlayback ExtrairePlaybackMachineEtatFaune()
	{
		if (_animationTreeFaune == null) return null;
		Variant v = _animationTreeFaune.Get("parameters/playback");
		if (v.VariantType == Variant.Type.Nil) return null;
		return v.AsGodotObject() as AnimationNodeStateMachinePlayback;
	}

	private void DetruireAnimationTreeFaune()
	{
		if (_animationTreeFaune != null && GodotObject.IsInstanceValid(_animationTreeFaune))
		{
			_animationTreeFaune.Active = false;
			_animationTreeFaune.TreeRoot = null;
			if (_animationTreeCreeParScript)
				_animationTreeFaune.QueueFree();
		}

		_animationTreeFaune = null;
		_playbackEtatFaune = null;
		_etatCourantMachineAnimation = "";
		_blendLocomotionActif = false;
		_animationTreeCreeParScript = false;
		_dernierBlendAnimation = float.NaN;
		_derniereVitesseAnimation = float.NaN;
	}

	private StringName ObtenirStringNameAnimation(string nom)
	{
		if (string.IsNullOrEmpty(nom)) return default;
		if (_cacheStringNameAnimations.TryGetValue(nom, out StringName value))
			return value;
		value = new StringName(nom);
		_cacheStringNameAnimations[nom] = value;
		return value;
	}

	private static StringName ObtenirNomEtatAnimation(string etat)
	{
		return etat switch
		{
			NomNoeudDeplacement => NomNoeudDeplacementString,
			NomNoeudBroutage => NomNoeudBroutageString,
			NomNoeudMort => NomNoeudMortString,
			"Nage" => NomNoeudNageString,
			NomNoeudSaut => NomNoeudSautString,
			NomNoeudSautGalop => NomNoeudSautGalopString,
			NomNoeudAttaqueKick => NomNoeudAttaqueKickString,
			NomNoeudAttaqueTete => NomNoeudAttaqueTeteString,
			_ => new StringName(etat)
		};
	}

	/// <summary>En errance calme, change d'idle selon le score de calme (pas de cycle aléatoire).</summary>
	private void MettreAJourCycleIdleMultiples(float dt, float vitesseHoriz)
	{
		if (_animationPlayer == null || !GodotObject.IsInstanceValid(_animationPlayer))
			return;
		if (!_poolsAnimationsEvolutives.TryGetValue("idle", out List<string> pool) || pool == null || pool.Count < 2)
			return;
		if (!ContexteCalmeStablePourVariationClip(vitesseHoriz))
		{
			_timerCycleIdleSecondes = 0f;
			return;
		}
		_timerCycleIdleSecondes -= dt;
		if (_timerCycleIdleSecondes > 0f)
			return;
		float imin = Mathf.Max(3f, IntervalleMinCycleIdleSecondes);
		float imax = Mathf.Max(imin + 1f, IntervalleMaxCycleIdleSecondes);
		_timerCycleIdleSecondes = (imin + imax) * 0.5f;

		CalculerScoresContexteAnimation(out float scoreCalme, out _, out _, out _, out _);
		int idx = Mathf.Clamp(Mathf.RoundToInt(scoreCalme * (pool.Count - 1)), 0, pool.Count - 1);
		string suivant = pool[idx];
		if (string.IsNullOrEmpty(suivant) || suivant == _clipIdle)
			return;
		_clipIdle = suivant;
		_indexCycleIdle = idx;
		AppliquerBouclesSurClipsLocomotion();
		if (UtiliserAnimationTreeLocomotion)
			DemanderReconfigurationAnimationTree();
	}

	private void MettreAJourAnimation(float dt, float vitesseHoriz)
	{
		if (_animationPlayer == null) return;
		_tempsVerrouAnimationCombat = Mathf.Max(0f, _tempsVerrouAnimationCombat - dt);
		if (_tempsVerrouAnimationCombat <= 0f && !string.IsNullOrEmpty(_noeudAnimationCombatVerrou)
			&& _blendLocomotionActif && _playbackEtatFaune != null && _animationTreeFaune != null)
		{
			if (!_animationTreeFaune.Active)
				_animationTreeFaune.Active = true;
			_playbackEtatFaune.Travel(NomNoeudDeplacementString);
			_etatCourantMachineAnimation = NomNoeudDeplacement;
			_noeudAnimationCombatVerrou = "";
		}

		float vitesseMarcheActuelle = VitesseMarche * MultiplicateurNiveau * (VitesseStatActuelle / Mathf.Max(0.1f, VitesseBase));
		float vitesseFuiteActuelle = VitesseFuite * MultiplicateurNiveau * (VitesseStatActuelle / Mathf.Max(0.1f, VitesseBase));
		float seuilIdle = 0.12f;
		float seuilMarche = 0.25f;
		bool sautAscendant = !_dansEau && !IsOnFloor()
			&& (_fenetreAnimSautStrategique > 0f || Velocity.Y > 0.42f);
		bool sprintAnime = (_etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge) && SprintAutoriseParStamina();
		bool clipsSautDedies = _machineAPorteSaut || _machineAPorteSautGalop;

		if (_blendLocomotionActif && _playbackEtatFaune != null && _animationTreeFaune != null && _animationTreeFaune.Active)
		{
			string etatVoulu = NomNoeudDeplacement;
			if (_dansEau && _machineAPorteNage)
				etatVoulu = "Nage";
			else if (!_dansEau && _etat == EtatBoeuf.Broutage && _machineAPorteBroutage
				&& (vitesseHoriz <= seuilMarche || _verrouMouvementMorsure > 0f))
				etatVoulu = NomNoeudBroutage; // Tête baissée seulement à l'arrêt ; en route vers l'herbe → locomotion (plus de « glisse en mangeant »).
			else if (_tempsVerrouAnimationCombat > 0f && !string.IsNullOrEmpty(_noeudAnimationCombatVerrou))
			{
				if (_noeudAnimationCombatVerrou == NomNoeudAttaqueKick
					&& (_machineAPorteAttaqueKick || !string.IsNullOrEmpty(_clipAttaqueKick)))
					etatVoulu = NomNoeudAttaqueKick;
				else if (_noeudAnimationCombatVerrou == NomNoeudAttaqueTete
					&& (_machineAPorteAttaqueTete || !string.IsNullOrEmpty(_clipAttaqueTete)))
					etatVoulu = NomNoeudAttaqueTete;
			}
			else if (!_dansEau && sautAscendant && sprintAnime && _machineAPorteSautGalop
				&& _tempsVerrouAnimationCombat <= 0f)
				etatVoulu = NomNoeudSautGalop;
			else if (!_dansEau && sautAscendant && _machineAPorteSaut && (!_machineAPorteSautGalop || !sprintAnime)
				&& _tempsVerrouAnimationCombat <= 0f)
				etatVoulu = NomNoeudSaut;
			else if (!_dansEau && IsOnFloor() && (_etatCourantMachineAnimation == NomNoeudSaut || _etatCourantMachineAnimation == NomNoeudSautGalop))
				etatVoulu = NomNoeudDeplacement;

			if (etatVoulu != _etatCourantMachineAnimation)
			{
				_playbackEtatFaune.Travel(ObtenirNomEtatAnimation(etatVoulu));
				_etatCourantMachineAnimation = etatVoulu;
			}

			if (etatVoulu == NomNoeudDeplacement && _animationTreeFaune != null)
			{
				float blend = 0f;
				if (sprintAnime)
					blend = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.01f, vitesseFuiteActuelle), 0f, 1f);
				else if (vitesseHoriz > seuilMarche)
					blend = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.01f, vitesseMarcheActuelle) * 0.65f, 0f, 0.95f);
				if (sautAscendant && !clipsSautDedies)
				{
					blend = vitesseHoriz > seuilMarche ? Mathf.Max(blend, 0.80f) : 0f;
				}
				bool locomotionErranceCalme = _etat == EtatBoeuf.Errance && _memoireDetectionJoueur <= 0f && _tempsFuite <= 0f;
				if (locomotionErranceCalme && IntensiteMicroVivaciteAnimation > 0.0001f
					&& vitesseHoriz > seuilIdle && vitesseHoriz < 0.32f)
				{
					float phase = (float)_ageSecondes * 0.85f + (GetInstanceId() & 2047) * 0.0015f;
					blend = Mathf.Clamp(blend + IntensiteMicroVivaciteAnimation * 0.08f * Mathf.Sin(phase), 0f, 0.98f);
				}
				if (float.IsNaN(_dernierBlendAnimation) || Mathf.Abs(_dernierBlendAnimation - blend) > 0.0001f)
				{
					_animationTreeFaune.Set(ParamBlendDeplacement, blend);
					_dernierBlendAnimation = blend;
				}
			}

			float speed = 1f;
			if (_dansEau)
				speed = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, VitesseNageHorizontale), 0.75f, 1.25f);
			else if (etatVoulu == NomNoeudBroutage)
				speed = 0.9f;
			else if (etatVoulu == NomNoeudSautGalop || (etatVoulu == NomNoeudDeplacement && sprintAnime))
				speed = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, vitesseFuiteActuelle), 0.85f, 1.75f);
			else if (etatVoulu == NomNoeudSaut)
				speed = vitesseHoriz > seuilMarche ? Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, vitesseMarcheActuelle), 0.85f, 1.35f) : 0.92f;
			else if (etatVoulu == NomNoeudAttaqueKick || etatVoulu == NomNoeudAttaqueTete)
				speed = 1f;
			else if (vitesseHoriz > seuilMarche)
				speed = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, vitesseMarcheActuelle), 0.8f, 1.45f);
			if (sautAscendant && !clipsSautDedies && etatVoulu == NomNoeudDeplacement)
				speed = vitesseHoriz > seuilMarche ? Mathf.Max(speed, 1.05f) : 0.92f;
			bool locomotionErranceCalmeVitesse = _etat == EtatBoeuf.Errance && _memoireDetectionJoueur <= 0f && _tempsFuite <= 0f;
			if (locomotionErranceCalmeVitesse && IntensiteMicroVivaciteAnimation > 0.0001f
				&& etatVoulu == NomNoeudDeplacement && vitesseHoriz > seuilIdle && vitesseHoriz < 0.32f)
			{
				float phase2 = (float)_ageSecondes * 1.9f + (GetInstanceId() & 1023) * 0.002f;
				speed *= 1f + IntensiteMicroVivaciteAnimation * 0.35f * Mathf.Sin(phase2);
			}
			float vitesseAppliquee = speed * Mathf.Clamp(MultiplicateurVitesseAnimation, 0.2f, 2.0f) * FacteurAnimationContextuelle();
			if (float.IsNaN(_derniereVitesseAnimation) || Mathf.Abs(_derniereVitesseAnimation - vitesseAppliquee) > 0.0001f)
			{
				_animationPlayer.SpeedScale = vitesseAppliquee;
				_derniereVitesseAnimation = vitesseAppliquee;
			}
			return;
		}

		string cible = _clipIdle;
		float speedDirect = 1f;
		if (_dansEau)
		{
			cible = !string.IsNullOrEmpty(_clipNage) ? _clipNage : (!string.IsNullOrEmpty(_clipCourse) ? _clipCourse : (!string.IsNullOrEmpty(_clipMarche) ? _clipMarche : _clipIdle));
			speedDirect = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, VitesseNageHorizontale), 0.75f, 1.25f);
		}
		else if (_etat == EtatBoeuf.Broutage && (vitesseHoriz <= seuilMarche || _verrouMouvementMorsure > 0f))
		{
			cible = !string.IsNullOrEmpty(_clipManger) ? _clipManger : _clipIdle;
			speedDirect = 0.9f;
		}
		else if ((_etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge) && SprintAutoriseParStamina())
		{
			cible = !string.IsNullOrEmpty(_clipCourse) ? _clipCourse : (!string.IsNullOrEmpty(_clipTrot) ? _clipTrot : _clipMarche);
			speedDirect = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, vitesseFuiteActuelle), 0.85f, 1.75f);
		}
		else if (sautAscendant)
		{
			bool sautAvecElan = vitesseHoriz > seuilMarche;
			if (sprintAnime && !string.IsNullOrEmpty(_clipSautGalop) && _animationPlayer.HasAnimation(_clipSautGalop))
			{
				cible = _clipSautGalop;
				speedDirect = 1.12f;
			}
			else if (!string.IsNullOrEmpty(_clipSaut) && _animationPlayer.HasAnimation(_clipSaut))
			{
				cible = _clipSaut;
				speedDirect = sautAvecElan ? 1.05f : 0.92f;
			}
			else
			{
				cible = sautAvecElan
					? (!string.IsNullOrEmpty(_clipCourse) ? _clipCourse : (!string.IsNullOrEmpty(_clipTrot) ? _clipTrot : _clipMarche))
					: _clipIdle;
				speedDirect = sautAvecElan ? 1.05f : 0.92f;
			}
		}
		else if (vitesseHoriz > seuilMarche)
		{
			cible = !string.IsNullOrEmpty(_clipTrot) && vitesseHoriz > vitesseMarcheActuelle * 0.74f ? _clipTrot : (!string.IsNullOrEmpty(_clipMarche) ? _clipMarche : _clipIdle);
			speedDirect = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, vitesseMarcheActuelle), 0.8f, 1.45f);
		}

		if (!string.IsNullOrEmpty(cible))
		{
			StringName nom = ObtenirStringNameAnimation(cible);
			if (_animationPlayer.CurrentAnimation != nom || !_animationPlayer.IsPlaying())
				_animationPlayer.Play(nom, 0.16f);
		}
		float vitesseDirecte = speedDirect * Mathf.Clamp(MultiplicateurVitesseAnimation, 0.2f, 2.0f) * FacteurAnimationContextuelle();
		if (float.IsNaN(_derniereVitesseAnimation) || Mathf.Abs(_derniereVitesseAnimation - vitesseDirecte) > 0.0001f)
		{
			_animationPlayer.SpeedScale = vitesseDirecte;
			_derniereVitesseAnimation = vitesseDirecte;
		}
	}

	private void PreparerLecteurEtBibliothequeLocomotionFaune()
	{
		if (_animationPlayer == null) return;
		if (!_animationPlayer.HasAnimationLibrary(NomBibliothequeLocomotionFaune))
			_animationPlayer.AddAnimationLibrary(NomBibliothequeLocomotionFaune, new AnimationLibrary());
		_animationPlayer.RootNode = new NodePath("..");
		_animationPlayer.ProcessMode = ProcessModeEnum.Always;
		_animationPlayer.Active = true;
	}

	private void FusionnerAnimationsRemappeesDepuisSceneReference(string cheminScene)
	{
		if (_animationPlayer == null || _modeleVisuel == null) return;
		if (string.IsNullOrWhiteSpace(cheminScene) || !ResourceLoader.Exists(cheminScene)) return;

		var sc = GD.Load<PackedScene>(cheminScene);
		Node temp = sc?.Instantiate();
		if (temp == null) return;

		try
		{
			AnimationPlayer apRef = ChoisirMeilleurAnimationPlayer(temp);
			Skeleton3D skRef = TrouverPremierNoeudDeType<Skeleton3D>(temp);
			Skeleton3D skLive = TrouverPremierNoeudDeType<Skeleton3D>(_modeleVisuel);
			if (apRef == null || skRef == null || skLive == null)
				return;

			Node racineRef = apRef.GetParent() ?? temp;
			Node racineLive = _animationPlayer.GetParent() ?? _modeleVisuel;
			string prefixRef = racineRef.GetPathTo(skRef).ToString();
			string prefixLive = racineLive.GetPathTo(skLive).ToString();

			AnimationLibrary libLoc = _animationPlayer.GetAnimationLibrary(NomBibliothequeLocomotionFaune);
			if (libLoc == null) return;

			foreach (string nomComplet in CollecterCheminsAnimation(apRef))
			{
				if (EstClipSystemeOuVide(nomComplet)) continue;
				string std = DeriverNomStandardClipOuNull(nomComplet);
				if (std == null) continue;
				if (libLoc.HasAnimation(std)) continue;

				Animation source = apRef.GetAnimation(new StringName(nomComplet));
				if (source == null) continue;
				var anim = (Animation)source.Duplicate(true);
				RemapperPrefixSquelette(anim, prefixRef, prefixLive);
				RemapperCheminsParMarqueurSquelette(anim, prefixLive);
				anim.LoopMode = Animation.LoopModeEnum.Linear;
				libLoc.AddAnimation(std, anim);
			}

			GD.Print($"ZERO-K Faune : pistes remappees depuis {cheminScene} vers {NomBibliothequeLocomotionFaune} ({prefixRef} -> {prefixLive}).");
		}
		finally
		{
			temp.QueueFree();
		}
	}

	private void FusionnerUneSceneAnimationVersBibliothequeFaune(string cheminScene, string nomClipStandard)
	{
		if (_animationPlayer == null || _modeleVisuel == null) return;
		if (string.IsNullOrWhiteSpace(cheminScene) || !ResourceLoader.Exists(cheminScene)) return;
		if (!_animationPlayer.HasAnimationLibrary(NomBibliothequeLocomotionFaune)) return;

		AnimationLibrary libLoc = _animationPlayer.GetAnimationLibrary(NomBibliothequeLocomotionFaune);
		if (libLoc == null || libLoc.HasAnimation(nomClipStandard)) return;

		var sc = GD.Load<PackedScene>(cheminScene);
		Node temp = sc?.Instantiate();
		if (temp == null) return;

		try
		{
			AnimationPlayer apExt = ChoisirMeilleurAnimationPlayer(temp);
			Skeleton3D skExt = TrouverPremierNoeudDeType<Skeleton3D>(temp);
			Skeleton3D skLive = TrouverPremierNoeudDeType<Skeleton3D>(_modeleVisuel);
			if (apExt == null || skExt == null || skLive == null)
				return;

			Node racineExt = apExt.GetParent() ?? temp;
			Node racineLive = _animationPlayer.GetParent() ?? _modeleVisuel;
			string prefixExt = racineExt.GetPathTo(skExt).ToString();
			string prefixLive = racineLive.GetPathTo(skLive).ToString();

			Animation anim = ExtrairePremiereAnimationDepuisLecteur(apExt);
			if (anim == null) return;

			if (nomClipStandard is "Idle" or "Marche" or "Broutage")
				anim.LoopMode = Animation.LoopModeEnum.Linear;

			RemapperPrefixSquelette(anim, prefixExt, prefixLive);
			RemapperCheminsParMarqueurSquelette(anim, prefixLive);
			libLoc.AddAnimation(nomClipStandard, anim);
			GD.Print($"ZERO-K Faune : scene {cheminScene} -> {NomBibliothequeLocomotionFaune}/{nomClipStandard}.");
		}
		finally
		{
			temp.QueueFree();
		}
	}

	private void AppliquerClipsBibliothequeLocomotionFauneEnPriorite()
	{
		if (_animationPlayer == null || !_animationPlayer.HasAnimationLibrary(NomBibliothequeLocomotionFaune))
			return;
		AnimationLibrary lib = _animationPlayer.GetAnimationLibrary(NomBibliothequeLocomotionFaune);
		if (lib == null) return;

		string Pref(string c) => $"{NomBibliothequeLocomotionFaune}/{c}";
		if (lib.HasAnimation("Idle")) _clipIdle = Pref("Idle");
		if (lib.HasAnimation("Marche")) _clipMarche = Pref("Marche");
		if (lib.HasAnimation("Course")) _clipCourse = Pref("Course");
		if (lib.HasAnimation("Broutage")) _clipManger = Pref("Broutage");
		if (lib.HasAnimation("Mort")) _clipMort = Pref("Mort");
		if (lib.HasAnimation("Walk") && string.IsNullOrEmpty(_clipMarche)) _clipMarche = Pref("Walk");
		if (lib.HasAnimation("Gallop") && string.IsNullOrEmpty(_clipCourse)) _clipCourse = Pref("Gallop");
		if (lib.HasAnimation("Jump") && string.IsNullOrEmpty(_clipSaut)) _clipSaut = Pref("Jump");
		if (lib.HasAnimation("GallopJump") && string.IsNullOrEmpty(_clipSautGalop)) _clipSautGalop = Pref("GallopJump");
		if (lib.HasAnimation("Eating") && string.IsNullOrEmpty(_clipManger)) _clipManger = Pref("Eating");
		if (lib.HasAnimation(ClipAttaqueKickCanonique))
			_clipAttaqueKick = Pref(ClipAttaqueKickCanonique);
		else if (lib.HasAnimation("AttaqueKick") && string.IsNullOrEmpty(_clipAttaqueKick))
			_clipAttaqueKick = Pref("AttaqueKick");
		if (lib.HasAnimation(ClipAttaqueTeteCanonique))
			_clipAttaqueTete = Pref(ClipAttaqueTeteCanonique);
		else if (lib.HasAnimation("AttaqueTete") && string.IsNullOrEmpty(_clipAttaqueTete))
			_clipAttaqueTete = Pref("AttaqueTete");
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

	private static Animation ExtrairePremiereAnimationDepuisLecteur(AnimationPlayer ap)
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
					return (Animation)source.Duplicate(true);
			}
		}

		foreach (StringName nom in ap.GetAnimationList())
		{
			Animation source = ap.GetAnimation(nom);
			if (source != null)
				return (Animation)source.Duplicate(true);
		}

		return null;
	}

	private static void RemapperPrefixSquelette(Animation anim, string prefixeExterne, string prefixeCible)
	{
		if (anim == null || string.IsNullOrEmpty(prefixeExterne) || prefixeCible == null) return;
		if (string.Equals(prefixeExterne, prefixeCible, StringComparison.Ordinal)) return;
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			string s = anim.TrackGetPath(i).ToString();
			if (s.StartsWith(prefixeExterne, StringComparison.Ordinal))
				anim.TrackSetPath(i, new NodePath(prefixeCible + s.Substring(prefixeExterne.Length)));
		}
	}

	private static void RemapperCheminsParMarqueurSquelette(Animation anim, string cheminNoeudSqueletteCible)
	{
		if (anim == null || string.IsNullOrEmpty(cheminNoeudSqueletteCible)) return;
		const string marqueur = "Skeleton3D";
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			string s = anim.TrackGetPath(i).ToString();
			int idx = s.IndexOf(marqueur, StringComparison.Ordinal);
			if (idx < 0) continue;
			string queue = s.Substring(idx + marqueur.Length);
			anim.TrackSetPath(i, new NodePath(cheminNoeudSqueletteCible + queue));
		}
	}

	private static string DeriverNomStandardClipOuNull(string nomComplet)
	{
		string n = nomComplet.ToLowerInvariant();
		if (n.Contains("death") || n.Contains("dead") || n.Contains("die") || n.Contains("mort")) return "Mort";
		if (NomClipSembleSautGalop(n) || (n.Contains("jump") && !n.Contains("gallop")))
			return null;
		if (n.Contains("attack") || n.Contains("headbutt") || (n.Contains("kick") && !n.Contains("walk")))
			return null;
		if (n.Contains("idle") || n.Contains("stand") || n.Contains("repos")) return "Idle";
		if (n.Contains("walk") || n.Contains("marche") || n.Contains("locomotion") || n.Contains("cycle")) return "Marche";
		if (n.Contains("run") || n.Contains("gallop") || n.Contains("course") || n.Contains("charge")) return "Course";
		if (n.Contains("eat") || n.Contains("eating") || n.Contains("graze") || n.Contains("manger") || n.Contains("browse")) return "Broutage";
		return null;
	}

	private void FusionnerBibliothequesDepuisGltfExterneMemeRig()
	{
		string chemin = (CheminSceneGltfAnimationsExternesMemeRig ?? "").Trim();
		if (string.IsNullOrEmpty(chemin))
			chemin = ResoudreCheminAnimationsExternesAutomatique();
		FusionnerBibliothequesDepuisCheminExterne(chemin, "externe_unique");
	}

	private void FusionnerBibliothequesDepuisDossierAnimationsCompatibles()
	{
		if (string.IsNullOrWhiteSpace(DossierAnimationsAnimalesCompatibles))
			return;
		string dossierNorm = DossierAnimationsAnimalesCompatibles.Trim();
		List<string> chemins;
		lock (VerrouCacheBibliothequesAnimExternes)
		{
			if (CacheListeCheminsDossierAnimationsCompatibles != null
				&& string.Equals(DossierListeCheminsCache, dossierNorm, StringComparison.OrdinalIgnoreCase))
				chemins = CacheListeCheminsDossierAnimationsCompatibles;
			else
			{
				chemins = ListerFichiersAnimationsRecursifs(dossierNorm);
				CacheListeCheminsDossierAnimationsCompatibles = chemins;
				DossierListeCheminsCache = dossierNorm;
			}
		}
		int ajoutes = 0;
		foreach (string chemin in chemins)
		{
			if (string.IsNullOrWhiteSpace(chemin))
				continue;
			if (string.Equals(chemin, CheminSceneGltfAnimationsExternesMemeRig, StringComparison.OrdinalIgnoreCase))
				continue;
			if (string.Equals(chemin, CheminGlbSqueletteReference, StringComparison.OrdinalIgnoreCase))
				continue;
			string sourceKey = NettoyerCleLibrairie(System.IO.Path.GetFileNameWithoutExtension(chemin));
			if (string.IsNullOrWhiteSpace(sourceKey))
				sourceKey = $"pool_{ajoutes}";
			FusionnerBibliothequesDepuisCheminExterne(chemin, sourceKey);
			ajoutes++;
		}
		if (ajoutes > 0 && !LogScanDossierAnimationsCompatiblesEffectue)
		{
			LogScanDossierAnimationsCompatiblesEffectue = true;
			GD.Print($"ZERO-K Faune : scan dossier animations compatibles -> {ajoutes} fichier(s) (cache partage entre individus).");
		}
	}

	private static List<string> ListerFichiersAnimationsRecursifs(string dossierRes)
	{
		var resultats = new List<string>();
		var visites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(dossierRes))
			return resultats;

		var pile = new Stack<string>();
		pile.Push(dossierRes);
		while (pile.Count > 0)
		{
			string courant = pile.Pop();
			if (!visites.Add(courant))
				continue;
			DirAccess d = DirAccess.Open(courant);
			if (d == null)
				continue;

			d.ListDirBegin();
			while (true)
			{
				string nom = d.GetNext();
				if (nom == "")
					break;
				if (nom == "." || nom == "..")
					continue;
				string chemin = $"{courant.TrimEnd('/')}/{nom}";
				if (d.CurrentIsDir())
				{
					pile.Push(chemin);
					continue;
				}

				if (nom.EndsWith(".import", StringComparison.OrdinalIgnoreCase))
					continue;
				if (!nom.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase) && !nom.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
					continue;
				if (!ResourceLoader.Exists(chemin))
					continue;
				resultats.Add(chemin);
			}
			d.ListDirEnd();
		}

		return resultats;
	}

	private static string NettoyerCleLibrairie(string valeur)
	{
		if (string.IsNullOrWhiteSpace(valeur))
			return "";
		string s = valeur.ToLowerInvariant();
		var chars = new char[s.Length];
		int e = 0;
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
			chars[e++] = ok ? c : '_';
		}
		return new string(chars, 0, e).Trim('_');
	}

	private static HashSet<string> ExtraireNomsOsNormalises(Skeleton3D sk)
	{
		var os = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (sk == null)
			return os;
		int n = sk.GetBoneCount();
		for (int i = 0; i < n; i++)
		{
			string nom = sk.GetBoneName(i).ToString().Trim().ToLowerInvariant();
			if (!string.IsNullOrWhiteSpace(nom))
				os.Add(nom);
		}
		return os;
	}

	private static bool AnimationCompatibleAvecSkeleton(Animation anim, HashSet<string> osLive)
	{
		if (anim == null || osLive == null || osLive.Count == 0)
			return true;

		int totalPistesOs = 0;
		int correspondances = 0;
		var vus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			string p = anim.TrackGetPath(i).ToString();
			int idx = p.IndexOf(':');
			if (idx < 0 || idx + 1 >= p.Length)
				continue;
			string os = p.Substring(idx + 1).Trim().ToLowerInvariant();
			if (string.IsNullOrWhiteSpace(os))
				continue;
			if (!vus.Add(os))
				continue;
			totalPistesOs++;
			if (osLive.Contains(os))
				correspondances++;
		}

		if (totalPistesOs == 0)
			return true;
		return correspondances >= 2;
	}

	private static string ResoudreCheminAnimationsExternesAutomatique()
	{
		foreach (string c in CheminsAnimationExterneAuto)
		{
			if (ResourceLoader.Exists(c))
				return c;
		}

		const string dossier = "res://Modeles/Entites/Boeufs/";
		var d = DirAccess.Open(dossier);
		if (d == null)
			return "";

		d.ListDirBegin();
		while (true)
		{
			string f = d.GetNext();
			if (f == "")
				break;
			if (f == "." || f == "..")
				continue;
			if (f.EndsWith(".import", StringComparison.Ordinal))
				continue;
			string fl = f.ToLowerInvariant();
			if (!(fl.EndsWith(".glb", StringComparison.Ordinal) || fl.EndsWith(".gltf", StringComparison.Ordinal)))
				continue;
			if (fl == "boeufsauvage.glb")
				continue;
			if (!fl.Contains("anim", StringComparison.Ordinal))
				continue;
			string chemin = dossier + f;
			if (ResourceLoader.Exists(chemin))
			{
				d.ListDirEnd();
				return chemin;
			}
		}
		d.ListDirEnd();
		return "";
	}

	private void FusionnerBibliothequesDepuisCheminExterne(string chemin, string sourceKey)
	{
		if (string.IsNullOrEmpty(chemin) || !ResourceLoader.Exists(chemin) || _animationPlayer == null)
			return;
		sourceKey = NettoyerCleLibrairie(sourceKey);
		if (string.IsNullOrWhiteSpace(sourceKey))
			sourceKey = "source";

		Skeleton3D skLivePrecalc = TrouverPremierNoeudDeType<Skeleton3D>(_modeleVisuel != null ? _modeleVisuel : this);
		Node racineLivePrecalc = _animationPlayer.GetParent() ?? (_modeleVisuel ?? (Node)this);
		string prefixLivePrecalc = skLivePrecalc != null ? racineLivePrecalc.GetPathTo(skLivePrecalc).ToString() : "";
		string cleCache = $"{chemin}|{sourceKey}|{prefixLivePrecalc}";

		lock (VerrouCacheBibliothequesAnimExternes)
		{
			if (CacheBibliothequesExternesRemappees.TryGetValue(cleCache, out List<(string libDest, AnimationLibrary lib)> enCache)
				&& enCache != null && enCache.Count > 0)
			{
				foreach ((string libDest, AnimationLibrary lib) in enCache)
				{
					if (lib == null) continue;
					var instLib = (AnimationLibrary)lib.Duplicate(true);
					if (_animationPlayer.HasAnimationLibrary(libDest))
						_animationPlayer.RemoveAnimationLibrary(libDest);
					_animationPlayer.AddAnimationLibrary(libDest, instLib);
				}
				return;
			}
		}

		var ps = GD.Load<PackedScene>(chemin);
		Node inst = ps?.Instantiate();
		if (inst == null)
			return;

		var snapshotPourCache = new List<(string libDest, AnimationLibrary lib)>();
		try
		{
			AnimationPlayer apExt = ChoisirMeilleurAnimationPlayer(inst);
			if (apExt == null)
				return;

			Skeleton3D skExt = TrouverPremierNoeudDeType<Skeleton3D>(inst);
			Skeleton3D skLive = skLivePrecalc;
			HashSet<string> osLive = ExtraireNomsOsNormalises(skLive);
			string prefixExt = "";
			string prefixLive = "";
			if (skExt != null && skLive != null)
			{
				Node racineExt = apExt.GetParent() ?? inst;
				Node racineLive = racineLivePrecalc;
				prefixExt = racineExt.GetPathTo(skExt).ToString();
				prefixLive = racineLive.GetPathTo(skLive).ToString();
			}

			int libsAjoutees = 0;
			foreach (StringName libName in apExt.GetAnimationLibraryList())
			{
				AnimationLibrary source = apExt.GetAnimationLibrary(libName);
				if (source == null) continue;
				var copie = new AnimationLibrary();
				foreach (StringName n in source.GetAnimationList())
				{
					Animation a = source.GetAnimation(n);
					if (a == null) continue;
					var c = (Animation)a.Duplicate(true);
					if (!string.IsNullOrEmpty(prefixExt) && !string.IsNullOrEmpty(prefixLive))
					{
						RemapperPrefixSquelette(c, prefixExt, prefixLive);
						RemapperCheminsParMarqueurSquelette(c, prefixLive);
					}
					if (!AnimationCompatibleAvecSkeleton(c, osLive))
						continue;
					if (ReequilibrerClipsYAlImport)
						ReequilibrerEnfoncementVerticalClip(c, n.ToString());
					copie.AddAnimation(n.ToString(), c);
				}
				if (copie.GetAnimationList().Count == 0) continue;
				string libDest = $"externe_rig_{sourceKey}_{libName}";
				if (_animationPlayer.HasAnimationLibrary(libDest))
					_animationPlayer.RemoveAnimationLibrary(libDest);
				_animationPlayer.AddAnimationLibrary(libDest, copie);
				snapshotPourCache.Add((libDest, (AnimationLibrary)copie.Duplicate(true)));
				libsAjoutees++;
			}

			// Lecteur avec clips a la racine (sans AnimationLibrary), frequent apres certains exports.
			if (libsAjoutees == 0)
			{
				var libLegacy = new AnimationLibrary();
				foreach (StringName n in apExt.GetAnimationList())
				{
					Animation a = apExt.GetAnimation(n);
					if (a != null)
					{
						var c = (Animation)a.Duplicate(true);
						if (!string.IsNullOrEmpty(prefixExt) && !string.IsNullOrEmpty(prefixLive))
						{
							RemapperPrefixSquelette(c, prefixExt, prefixLive);
							RemapperCheminsParMarqueurSquelette(c, prefixLive);
						}
						if (!AnimationCompatibleAvecSkeleton(c, osLive))
							continue;
						if (ReequilibrerClipsYAlImport)
							ReequilibrerEnfoncementVerticalClip(c, n.ToString());
						libLegacy.AddAnimation(n.ToString(), c);
					}
				}
				if (libLegacy.GetAnimationList().Count > 0)
				{
					string libDestLegacy = $"externe_rig_{sourceKey}_legacy";
					if (_animationPlayer.HasAnimationLibrary(libDestLegacy))
						_animationPlayer.RemoveAnimationLibrary(libDestLegacy);
					_animationPlayer.AddAnimationLibrary(libDestLegacy, libLegacy);
					snapshotPourCache.Add((libDestLegacy, (AnimationLibrary)libLegacy.Duplicate(true)));
					libsAjoutees++;
				}
			}

			if (snapshotPourCache.Count > 0)
			{
				lock (VerrouCacheBibliothequesAnimExternes)
				{
					if (!CacheBibliothequesExternesRemappees.ContainsKey(cleCache))
						CacheBibliothequesExternesRemappees[cleCache] = snapshotPourCache;
				}
			}

			if (libsAjoutees > 0)
				GD.Print($"ZERO-K Faune : animations externes fusionnees depuis {chemin} ({libsAjoutees} bibliotheque(s)) [cache pour prochains individus].");
		}
		finally
		{
			inst.QueueFree();
		}
	}

	private static bool EstClipSystemeOuVide(string nomComplet)
	{
		if (string.IsNullOrWhiteSpace(nomComplet)) return true;
		string n = nomComplet.ToLowerInvariant();
		return n.Contains("reset") || n.Contains("rest_pose") || n.Contains("t-pose") || n.Contains("tpose")
			|| n.EndsWith("/reset") || n == "reset";
	}

	private static bool NomClipSembleMort(string nomComplet)
	{
		if (string.IsNullOrWhiteSpace(nomComplet)) return false;
		string n = nomComplet.ToLowerInvariant();
		return n.Contains("death") || n.Contains("dead") || n.Contains("die") || n.Contains("mort")
			|| n.Contains("ragdoll") || n.Contains("corpse");
	}

	private static bool NomClipSembleCombatOuSaut(string nomComplet)
	{
		if (string.IsNullOrWhiteSpace(nomComplet)) return false;
		string n = nomComplet.ToLowerInvariant();
		if (NomClipSembleSautGalop(n))
			return false;
		return n.Contains("attack") || n.Contains("headbutt") || n.Contains("kick") || n.Contains("jump")
			|| n.Contains("bite") || n.Contains("hit");
	}

	/// <summary>
	/// Clip de réaction (sursaut, dégâts) ou non-ambiant : à NE JAMAIS mettre dans le pool idle qui se cycle tout seul,
	/// sinon le bovin « sursaute / baisse la tête / joue un combat » sans raison quand il est calme (et, via le point de blend 0
	/// de l'état Déplacement, aussi quand il marche lentement). Ex. Quaternius : Idle_HitReact1/2, Jump_toIdle.
	/// </summary>
	private static bool NomClipSembleReactionOuNonAmbiant(string nomComplet)
	{
		if (string.IsNullOrWhiteSpace(nomComplet)) return false;
		string n = nomComplet.ToLowerInvariant();
		if (n.Contains("react") || n.Contains("flinch") || n.Contains("hurt") || n.Contains("hit")
			|| n.Contains("stagger") || n.Contains("stun") || n.Contains("damage") || n.Contains("impact"))
			return true;
		return NomClipSembleCombatOuSaut(n);
	}

	private static string PremierClipLocomotionUtileNonMortel(List<string> tous)
	{
		// 1) Idéal : un clip ni système, ni mort, ni combat/saut.
		// CRITIQUE : sans l'exclusion combat/saut, « marche » (et par cascade idle/course/trot/broutage)
		// pouvait tomber sur le premier clip de la liste = souvent l'attaque → le bovin fait des coups de tête au lieu de marcher.
		foreach (string c in tous)
		{
			if (!EstClipSystemeOuVide(c) && !NomClipSembleMort(c) && !NomClipSembleCombatOuSaut(c))
				return c;
		}
		// 2) Repli : ni système, ni mort (peut être un saut/attaque seulement si le modèle n'a vraiment rien d'autre).
		foreach (string c in tous)
		{
			if (!EstClipSystemeOuVide(c) && !NomClipSembleMort(c))
				return c;
		}
		// 3) Dernier recours : au moins pas un clip système.
		foreach (string c in tous)
		{
			if (!EstClipSystemeOuVide(c))
				return c;
		}
		return "";
	}
}
