using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	private void BasculerEnMort()
	{
		_etat = EtatBoeuf.Mort;
		_vieCourante = 0f;
		_cadavreAttendDepecage = true;
		_cadavreLootDistribue = false;
		_coupsDepecageDagueValides = 0;
		_tempsMort = float.MaxValue;
		_horodatageMortUnixSec = Time.GetUnixTimeFromSystem();
		Velocity = Vector3.Zero;
		EmitSignal(SignalName.EvolutionEvenement, "mort_faim", 1f, _niveau, _ageSecondes / 3600f);
		AppliquerAnimationMort();
	}

	/// <summary>True si le cadavre a dépassé <see cref="DureeCadavreAvantSuppression"/> depuis la mort (temps réel).</summary>
	public bool EstCadavreExpireParTempsReel()
	{
		if (_horodatageMortUnixSec <= 0.0)
			return false;
		return Time.GetUnixTimeFromSystem() - _horodatageMortUnixSec >= DureeCadavreAvantSuppression;
	}

	public static bool EstProfilCadavreExpire(Godot.Collections.Dictionary profil, float dureeCadavreSec)
	{
		if (profil == null)
			return false;
		if (profil.TryGetValue("cadavre_loot_distribue", out Variant lootV) && lootV.AsBool())
			return true;
		int etat = profil.TryGetValue("etat", out Variant etatV) ? etatV.AsInt32() : -1;
		if (etat != (int)EtatBoeuf.Mort)
			return false;
		if (!profil.TryGetValue("cadavre_heure_mort_unix", out Variant hmV))
			return false;
		double horodatage = hmV.AsDouble();
		if (horodatage <= 0.0)
			return false;
		return Time.GetUnixTimeFromSystem() - horodatage >= dureeCadavreSec;
	}

	/// <summary>Pose mort (clip direct uniquement) sans réactiver l'AnimationTree — évite cadavre debout / replay Mort / dépeçage infini.</summary>
	private void AppliquerAnimationMort()
	{
		if (_cadavreLootDistribue || !IsInsideTree() || IsQueuedForDeletion())
			return;
		if (_animationMortFigee)
			return;

		_animationMortDoitEtreFigee = true;
		_reconfigurationArbreAnimationEnAttente = false;
		DesactiverAnimationTreePourCadavre();

		if (string.IsNullOrEmpty(_clipMort) || _animationPlayer == null || !_animationPlayer.HasAnimation(_clipMort))
			return;

		ConfigurerClipMortEnOneShot();
		_animationMortFigee = false;
		_animationPlayer.Play(ObtenirStringNameAnimation(_clipMort), 0.12f);
	}

	private void DesactiverAnimationTreePourCadavre()
	{
		_blendLocomotionActif = false;
		_playbackEtatFaune = null;
		_etatCourantMachineAnimation = NomNoeudMort;
		if (_animationTreeFaune != null && GodotObject.IsInstanceValid(_animationTreeFaune))
			_animationTreeFaune.Active = false;
	}

	private void ConfigurerClipMortEnOneShot()
	{
		if (string.IsNullOrEmpty(_clipMort) || _animationPlayer == null || !_animationPlayer.HasAnimation(_clipMort))
			return;
		Animation animMort = _animationPlayer.GetAnimation(_clipMort);
		if (animMort != null)
			animMort.LoopMode = Animation.LoopModeEnum.None;
	}

	private void MettreAJourAnimationMortEtFigerSiTerminee()
	{
		if (!_animationMortDoitEtreFigee || _animationMortFigee || string.IsNullOrEmpty(_clipMort))
			return;
		if (_animationPlayer == null || !GodotObject.IsInstanceValid(_animationPlayer) || !_animationPlayer.HasAnimation(_clipMort))
			return;

		Animation animMort = _animationPlayer.GetAnimation(_clipMort);
		if (animMort == null)
			return;
		double longueur = animMort.Length;
		if (longueur <= 0.0)
			return;

		string animationCourante = _animationPlayer.CurrentAnimation.ToString();
		bool litClipMort = string.Equals(animationCourante, _clipMort, StringComparison.Ordinal);
		if (!litClipMort)
			return;

		double position = _animationPlayer.CurrentAnimationPosition;
		bool animationTerminee = !_animationPlayer.IsPlaying() || position >= longueur - EpsilonFinAnimationMortSec;
		if (!animationTerminee)
			return;

		double positionFinale = Math.Max(0.0, longueur - 0.001);
		_animationPlayer.Seek(positionFinale, true);
		_animationPlayer.Pause();
		_animationMortFigee = true;
	}

	private void GererMort(float dt)
	{
		if (_cadavreLootDistribue)
			return;
		if (EstCadavreExpireParTempsReel())
		{
			SupprimerCadavreApresExpiration();
			return;
		}
		_tempsMort -= dt;
		if (!_cadavreAttendDepecage && _tempsMort <= 0f)
			SupprimerCadavreApresExpiration();
	}

	private void SupprimerCadavreApresExpiration()
	{
		if (_cadavreLootDistribue)
			return;
		_cadavreLootDistribue = true;
		_cadavreAttendDepecage = false;
		_gestionnaireFaune?.NotifierCadavreRetireDeLaPersistance(this);
		RetirerCadavreDeLaScene();
	}

	/// <summary>Cadavre encore présent (pas looté).</summary>
	public bool EstCadavreDepecable()
		=> _etat == EtatBoeuf.Mort
			&& !_cadavreLootDistribue
			&& Visible
			&& IsInsideTree()
			&& !IsQueuedForDeletion();

	/// <summary>Enregistre un coup de dague valide sur le cadavre. Retourne true uniquement au dernier coup requis (déclencher le loot).</summary>
	public bool EnregistrerCoupDepecageDagueValide()
	{
		if (_etat != EtatBoeuf.Mort || !_cadavreAttendDepecage || _cadavreLootDistribue)
			return false;
		int requis = Mathf.Max(3, CoupsDagueRequisPourFinDepecage);
		if (_coupsDepecageDagueValides >= requis)
			return false;
		_coupsDepecageDagueValides++;
		return _coupsDepecageDagueValides == requis;
	}

	/// <summary>Marque le cadavre comme traité et le retire de la scène (après spawn du loot).</summary>
	public void FinaliserCadavreApresDepecage()
	{
		if (_cadavreLootDistribue)
			return;
		_vieCourante = 0f;
		_cadavreLootDistribue = true;
		_cadavreAttendDepecage = false;
		_reconfigurationArbreAnimationEnAttente = false;
		_animationMortDoitEtreFigee = true;
		_animationMortFigee = true;
		DesactiverAnimationTreePourCadavre();
		if (_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer))
			_animationPlayer.Stop();
		_gestionnaireFaune?.NotifierCadavreRetireDeLaPersistance(this);
		RetirerCadavreDeLaScene();
	}

	/// <summary>Pose mort figée au dernier frame — rechargement chunk / sauvegarde sans rejouer l'animation.</summary>
	private void AppliquerPoseCadavreFigee()
	{
		if (_cadavreLootDistribue || !IsInsideTree() || IsQueuedForDeletion())
			return;
		_animationMortDoitEtreFigee = true;
		_animationMortFigee = true;
		_reconfigurationArbreAnimationEnAttente = false;
		DesactiverAnimationTreePourCadavre();
		if (string.IsNullOrEmpty(_clipMort) || _animationPlayer == null || !_animationPlayer.HasAnimation(_clipMort))
			return;
		ConfigurerClipMortEnOneShot();
		Animation animMort = _animationPlayer.GetAnimation(_clipMort);
		double positionFinale = animMort != null && animMort.Length > 0.0
			? Math.Max(0.0, animMort.Length - 0.001)
			: 0.0;
		_animationPlayer.Play(ObtenirStringNameAnimation(_clipMort), -1.0);
		_animationPlayer.Seek(positionFinale, true);
		_animationPlayer.Pause();
	}

	private void RetirerCadavreDeLaScene()
	{
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
		SetPhysicsProcess(false);
		SetProcess(false);
		CollisionLayer = 0;
		CollisionMask = 0;
		DesactiverAnimationTreePourCadavre();
		if (IsInsideTree())
			QueueFree();
	}

	/// <summary>Indique au streaming/persist que cet individu ne doit plus jamais être rechargé.</summary>
	public bool DoitEtreExcluPersistanceFaune() => _cadavreLootDistribue;

	/// <summary>Première texture d’albedo trouvée sur le mesh du bovin (cuir dérivé de la peau).</summary>
	public Texture2D EssayerObtenirTexturePeauPourCuir()
	{
		Node racine = _modeleVisuel != null && GodotObject.IsInstanceValid(_modeleVisuel) ? _modeleVisuel : (Node)this;
		return ChercherPremiereAlbedoTextureRecursif(racine);
	}

	private static Texture2D ChercherPremiereAlbedoTextureRecursif(Node n)
	{
		if (n is MeshInstance3D mi && mi.Mesh != null)
		{
			int nSurf = mi.Mesh.GetSurfaceCount();
			for (int s = 0; s < nSurf; s++)
			{
				Material ov = mi.GetSurfaceOverrideMaterial(s);
				if (ov is BaseMaterial3D bmOv && bmOv.AlbedoTexture != null)
					return bmOv.AlbedoTexture;
			}
			for (int s = 0; s < nSurf; s++)
			{
				Material mSurf = mi.Mesh.SurfaceGetMaterial(s);
				if (mSurf is BaseMaterial3D bmSurf && bmSurf.AlbedoTexture != null)
					return bmSurf.AlbedoTexture;
			}
		}
		foreach (Node enfant in n.GetChildren())
		{
			Texture2D t = ChercherPremiereAlbedoTextureRecursif(enfant);
			if (t != null)
				return t;
		}
		return null;
	}

	/// <summary>Clé pour <see cref="SlotInventaire.GenomeAssemblage"/> : empilement cuir selon la même « peau ».</summary>
	public string ConstruireGenomePeauPourSlotCuir(Texture2D texPeau)
	{
		if (texPeau != null && !string.IsNullOrEmpty(texPeau.ResourcePath))
			return "PEAU:" + texPeau.ResourcePath;
		return EstTaureau ? "PEAU:TAUREAU" : "PEAU:VACHE";
	}

	public bool RecevoirImpactCombat(
		float intensiteImpact,
		Vector3 pointImpactMonde,
		Vector3 directionImpactMonde,
		bool estTranchant,
		bool estPerforant,
		string nomZoneImpact = "",
		ulong sourceId = 0UL)
	{
		if (_etat == EtatBoeuf.Mort || intensiteImpact <= 0.0001f)
			return false;

		double maintenant = Time.GetTicksMsec() / 1000.0;
		if (sourceId != 0UL)
		{
			if (_horodatageDernierDegatParSource.TryGetValue(sourceId, out double dernier)
				&& (maintenant - dernier) < Mathf.Max(0.02f, CooldownDegatsParSourceSecondes))
				return false;
			_horodatageDernierDegatParSource[sourceId] = maintenant;
		}

		float degats = Mathf.Max(DegatsMinImpact, intensiteImpact * Mathf.Max(0.01f, MultiplicateurDegatsImpact));
		float multiplicateurZone = ObtenirMultiplicateurZoneImpact(nomZoneImpact, pointImpactMonde);
		degats *= multiplicateurZone;
		if (estTranchant)
			degats *= 1.12f;
		if (estPerforant)
			degats *= 1.30f;

		float capCoup = Mathf.Max(2f, _vieMaxActuelle * Mathf.Clamp(CapDegatsParImpactRatioVieMax, 0.05f, 0.8f));
		degats = Mathf.Clamp(degats, 0f, capCoup);
		if (degats <= 0.0001f)
			return false;

		_vieCourante = Mathf.Max(0f, _vieCourante - degats);
		JouerCriDegats(degats);
		MettreAJourAffichageFaim3D();
		if (_vieCourante <= 0.0001f)
		{
			BasculerEnMort();
			return true;
		}

		if (_etat != EtatBoeuf.Mort && _etat != EtatBoeuf.Charge)
		{
			_etat = EtatBoeuf.Fuite;
			_tempsFuite = Mathf.Max(_tempsFuite, estPerforant ? 2.4f : 1.35f);
		}
		return true;
	}

	private float ObtenirMultiplicateurZoneImpact(string nomZoneImpact, Vector3 pointImpactMonde)
	{
		string nom = (nomZoneImpact ?? string.Empty).ToLowerInvariant();
		if (nom.Contains("tete"))
			return 1.55f;
		if (nom.Contains("ventre"))
			return 1.25f;

		Vector3 local = ToLocal(pointImpactMonde);
		if (local.Y > 0.95f)
			return 1.45f;
		if (local.Y > 0.32f && local.Y < 0.85f)
			return 1.2f;
		return 1f;
	}
}
