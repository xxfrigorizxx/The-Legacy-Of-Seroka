using Godot;
using System;

/// <summary>
/// Portail nexus APISARA ↔ quadrants temporels. Téléportation après <see cref="DureeStationnairePourTeleporterSecondes"/> s dans la zone (serveur).
/// Vers APISARA : invisible tant que le chunk (0,0) n’a pas collision+voxels pour la dimension active ; puis Y par raycast ciel→sol, sinon voxels client, sinon hint serveur. APISARA : ancrages inchangés.
/// </summary>
public partial class Portail : Node3D
{
	[Export] public PointCardinal Liaison { get; set; } = PointCardinal.NORD;
	/// <summary>Vrai si l’instance est une ancre sur la prairie APISARA (retour vers le quadrant). Faux si le portail est dans Alpha/Beta/Omega/Delta (vers APISARA).</summary>
	[Export] public bool AncreSurApisara { get; set; }
	[Export] public float HauteurDepartRayonMetres { get; set; } = 2600f;
	/// <summary>Masque raycast sol : calque 1 = terrain uniquement. Le cadre <see cref="CollisionsCadrePortaille"/> est sur le calque 2 pour ne pas masquer le sol sous le portail.</summary>
	[Export] public uint MasqueCollisionSol { get; set; } = 1u;
	/// <summary>Calque physique du cadre GLB (hors sol) — doit rester disjoint de <see cref="MasqueCollisionSol"/>.</summary>
	public const uint CalqueCollisionCadrePortail = 2u;
	/// <summary>Profondeur d’enfoncement : l’origine du nœud (base du cadre) est placée <b>sous</b> le point d’impact du raycast (niveau sol voxel), typ. ~10 cm.</summary>
	[Export] public float EnfoncementBaseAuSolMetres { get; set; } = 0.10f;
	/// <summary>Rayon horizontal du disque de remblai au sol (voxels ≈ mètres monde 1:1).</summary>
	[Export] public int RayonSocleVoxelsRemblai { get; set; } = 20;
	/// <summary>Profondeur max (voxels) pour chercher le sous-sol et combler uniquement l’air au-dessus (pas de colonne pleine dans le ciel).</summary>
	[Export] public int ProfondeurSocleVoxelsRemblai { get; set; } = 24;
	[Export] public float CooldownPortailSecondes { get; set; } = 2.0f;
	[Export] public float RayonTriggerMetres { get; set; } = 4.8f;
	/// <summary>Temps resté dans la zone membrane après l’entrée : assombrissement dès le contact, TP à l’échéance si toujours présent (serveur).</summary>
	[Export] public float DureeStationnairePourTeleporterSecondes { get; set; } = 3f;
	/// <summary>Point d'arrivée: distance en mètres devant le portail destination (pour éviter réapparition dans la membrane).</summary>
	[Export] public float DistanceApparitionDevantPortailMetres { get; set; } = 20f;
	/// <summary>Durée du fondu overlay (solo / compat.) ; en jeu normal on utilise <see cref="DureeStationnairePourTeleporterSecondes"/> pour overlay + délai TP.</summary>
	[Export] public float DureeAssombrissementAvantTpSecondes { get; set; } = 3f;
	/// <summary>Si vrai : <c>Area3D</c> sur le quad membrane (boîte). Si faux : sphère à l’origine du portail (rayon <see cref="RayonTriggerMetres"/>).</summary>
	[Export] public bool TriggerSurMembrane { get; set; } = true;
	/// <summary>UV du bas du quad membrane à masquer (0–0.5 typ.) pour éviter le quad sous le socle du portail.</summary>
	[Export] public float MembraneClipBasUv { get; set; } = 0.12f;
	/// <summary>Scène du cadre 3D (GLB importé). Utilisé si aucun enfant <c>PortailleVisuel</c> n’est présent.</summary>
	[Export] public string CheminScenePortaille { get; set; } = "res://Modeles/structure/portaille/Portaille.glb";
	/// <summary>Échelle du cadre <c>Portaille.glb</c> dans le monde voxel.</summary>
	[Export] public float EchelleModelePortaille { get; set; } = 48f;
	/// <summary>Si faux : après l’échelle, décale <c>PortailleVisuel</c> en Y pour que le bas de l’AABB des meshes soit à l’origine du portail (pivot GLB souvent au centre, pas aux pieds).</summary>
	[Export] public bool AjusterBasMaillageSurOriginePortail { get; set; } = true;
	/// <summary>Décalage Y additionnel (m) après l’alignement AABB (réglage fin éditeur).</summary>
	[Export] public float DecalageVerticalSupplementaireApresAabbMetres { get; set; } = 0f;
	/// <summary>Vrai : collision maillage (trimesh) = ouverture traversable, matière alignée au GLB. Faux : convexes rapides mais souvent bloquants au centre.</summary>
	[Export] public bool CollisionPortailleTrimeshPrecis { get; set; } = true;
	/// <summary>Rotation locale de la membrane (défaut 90° Y) : évite le même plan que l’arc du GLB (croix « + » visuelle).</summary>
	[Export] public Vector3 RotationMembraneDegres { get; set; } = new Vector3(0f, 90f, 0f);
	/// <summary>Largeur × hauteur du quad (m) : augmenter Y pour couvrir le haut et le bas de l’arche du GLB.</summary>
	[Export] public Vector2 TailleMembraneMetres { get; set; } = new Vector2(22f, 38f);
	/// <summary>Centre local du quad membrane (ajuster Y pour centrer dans l’ouverture).</summary>
	[Export] public Vector3 PositionLocaleMembrane { get; set; } = new Vector3(0f, 16f, 0f);
	/// <summary>Position locale de la lueur (suit la membrane).</summary>
	[Export] public Vector3 PositionLocaleLueur { get; set; } = new Vector3(0f, 17.5f, 0f);
	/// <summary>Dimension parente (racine monde) : sert à l’altitude procédurale si le raycast sol n’a pas encore de collision.</summary>
	public int IdDimensionConteneur { get; set; } = (int)DimensionJeu.Alpha;

	/// <summary>
	/// Portails vers APISARA : à appeler avant <c>AddChild</c> — pas d’affichage tant que le sol n’est pas confirmé (voxels / raycast),
	/// pour éviter un cadre à l’altitude procédurale « devinée » dans le ciel.
	/// </summary>
	public void ForcerAttenteAffichageJusquaSolConfirmeVersApisara()
	{
		if (AncreSurApisara)
			return;
		_solSurfaceConfirmePourAffichage = false;
		RafraichirVisibiliteCombinee();
	}

	/// <summary>Visibilité par dimension (appelé par <see cref="Gestionnaire_Monde"/>). Combiné avec la confirmation sol pour les portails vers APISARA.</summary>
	public void DefinirVisibiliteSelonDimensionActive(bool dimensionEstActivePourCeRacine)
	{
		_visibiliteDemandeeParGestionnaireDimension = dimensionEstActivePourCeRacine;
		RafraichirVisibiliteCombinee();
	}

	/// <summary>Vrai si le serveur a envoyé une surface voxel (hint remblai / repli) — n’affiche pas le portail seul.</summary>
	public bool EstSolNexusVerrouilleParServeur => _hintServeurSurfaceRecu;

	/// <summary>Le gestionnaire veut ce racine visible (dimension locale du joueur).</summary>
	private bool _visibiliteDemandeeParGestionnaireDimension = true;
	/// <summary>Vers APISARA : faux tant que seul l’estimé procédural ou rien n’a confirmé le sol (évite apparition dans le ciel).</summary>
	private bool _solSurfaceConfirmePourAffichage = true;

	private Area3D _zone;
	private double _cooldownRestant;
	private MeshInstance3D _membrane;
	private OmniLight3D _lueur;
	private float _phaseLueur;
	private Joueur _joueurStationnaireDansZone;
	private bool _sequenceTpVisuelleEnCours;
	private Godot.Timer _timerApresAssombrissement;
	/// <summary>True dès qu’au moins une enveloppe convexe a été posée (évite de recalculer à chaque alignement).</summary>
	private bool _collisionsPortailleConstruites;
	private float _echellePortailleAppliqueePourAjustementAabb;
	/// <summary>Y monde de référence du sol au XZ du portail (impact raycast ou estimation procédurale).</summary>
	private float _ySurfaceSolMondeRaycast;
	/// <summary>Pour les portails « vers APISARA » : remblai voxel uniquement après impact sur le mesh terrain (évite pilier sous un cadre encore trop haut).</summary>
	private bool _alignementYSolConfirmeParRaycastMesh;
	/// <summary>Hint Y depuis les voxels serveur (RPC) : repli si le mesh n’est pas intersecté ; n’ouvre pas l’affichage sans terrain client prêt.</summary>
	private bool _hintServeurSurfaceRecu;
	private float _yHintServeurSurfaceNexus;
	private float _cooldownAlignementPhysique;

	/// <summary>Protège contre la boucle TP instantanée à l'arrivée (portail destination).</summary>
	public void ArmerCooldownPortailArrivee(float secondes)
	{
		_cooldownRestant = Math.Max(_cooldownRestant, Mathf.Max(0.05f, secondes));
	}

	public override void _Ready()
	{
		AppliquerLiaisonNexusCanoniqueSiApplicable();
		if (!AncreSurApisara)
		{
			ProcessMode = ProcessModeEnum.Always;
			SetPhysicsProcess(true);
		}

		RafraichirVisibiliteCombinee();

		AssurerModelePortailleCharge();

		_zone = GetNodeOrNull<Area3D>("PortailTrigger");
		if (_zone == null)
		{
			_zone = new Area3D { Name = "PortailTrigger", CollisionLayer = 0u, CollisionMask = 1u };
			_zone.AddChild(new CollisionShape3D { Name = "FormeTriggerPortail" });
			AddChild(_zone);
			_zone.Position = Vector3.Zero;
		}
		else
		{
			_zone.CollisionMask = 1u;
			_zone.CollisionLayer = 0u;
			if (_zone.GetNodeOrNull<CollisionShape3D>("FormeTriggerPortail") == null && _zone.GetChildCount() > 0 && _zone.GetChild(0) is CollisionShape3D csLegacy)
				csLegacy.Name = "FormeTriggerPortail";
		}
		_zone.Monitoring = true;
		_zone.Monitorable = false;
		_zone.BodyEntered += SurCorpsEntreDansTrigger;
		_zone.BodyExited += SurCorpsSortiDuTrigger;

		CreerVisuelMembraneEtLueur();
		AppliquerEchelleModeleVisuel();
		Callable.From(AlignerPortailSurSurface).CallDeferred();
		if (GetTree() != null)
		{
			GetTree().CreateTimer(0.35f).Timeout += AlignerPortailSurSurface;
			GetTree().CreateTimer(1.2f).Timeout += AlignerPortailSurSurface;
			GetTree().CreateTimer(2.5f).Timeout += AlignerPortailSurSurface;
			GetTree().CreateTimer(5f).Timeout += AlignerPortailSurSurface;
			GetTree().CreateTimer(8f).Timeout += AlignerPortailSurSurface;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (AncreSurApisara || !IsInsideTree())
			return;
		if (!AncreSurApisara && _alignementYSolConfirmeParRaycastMesh)
			return;
		_cooldownAlignementPhysique -= (float)delta;
		if (_cooldownAlignementPhysique <= 0f)
		{
			_cooldownAlignementPhysique = 0.12f;
			AlignerPortailSurSurface();
		}
	}

	public override void _Process(double delta)
	{
		_cooldownRestant = Mathf.Max(0.0, _cooldownRestant - delta);
		if (_lueur != null && GodotObject.IsInstanceValid(_lueur))
		{
			_phaseLueur += (float)delta * 2.1f;
			float pulse = 0.55f + 0.35f * Mathf.Sin(_phaseLueur);
			_lueur.LightEnergy = pulse;
		}
	}

	/// <summary>Changement de dimension : replacer sur l’estimé procédural puis ré-affiner au sol (hitbox inchangée).</summary>
	public void MarquerAttenteNouveauRaycastSol()
	{
		_hintServeurSurfaceRecu = false;
		if (!AncreSurApisara)
			_solSurfaceConfirmePourAffichage = false;
		_collisionsPortailleConstruites = false;
		_alignementYSolConfirmeParRaycastMesh = false;
		_cooldownAlignementPhysique = 0.02f;
		float x = GlobalPosition.X;
		float z = GlobalPosition.Z;
		float yEst = Gestionnaire_Monde.EstimerAltitudeTerrainPortail(x, z, IdDimensionConteneur);
		_ySurfaceSolMondeRaycast = yEst;
		float enfoncement = Mathf.Max(0f, EnfoncementBaseAuSolMetres);
		GlobalPosition = new Vector3(x, yEst - enfoncement, z);
		RafraichirVisibiliteCombinee();
		Callable.From(AlignerPortailSurSurface).CallDeferred();
	}

	/// <summary>
	/// Alpha / Beta / Omega / Delta uniquement : impose le Y de surface depuis les voxels serveur (RPC hôte).
	/// No-op si <see cref="AncreSurApisara"/> (portails APISARA inchangés).
	/// </summary>
	public void AppliquerSurfaceSolAutoritaireServeur(float ySurfaceMonde)
	{
		if (AncreSurApisara)
			return;
		_hintServeurSurfaceRecu = true;
		_yHintServeurSurfaceNexus = ySurfaceMonde;
		float x = GlobalPosition.X;
		float z = GlobalPosition.Z;
		float enf = Mathf.Max(0f, EnfoncementBaseAuSolMetres);
		_ySurfaceSolMondeRaycast = ySurfaceMonde;
		GlobalPosition = new Vector3(x, ySurfaceMonde - enf, z);
		_solSurfaceConfirmePourAffichage = false;
		_alignementYSolConfirmeParRaycastMesh = false;
		RafraichirVisibiliteCombinee();
		Callable.From(AlignerPortailSurSurface).CallDeferred();
	}

	/// <summary>
	/// APISARA : inchangé. Vers APISARA : attend chunk collision+voxels (dimension active), puis raycast ciel→sol (priorité),
	/// puis surface voxel client, puis hint serveur — jamais seulement l’estimé procédural pour l’affichage.
	/// </summary>
	public void AlignerPortailSurSurface()
	{
		float x = GlobalPosition.X;
		float z = GlobalPosition.Z;
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		float enf = Mathf.Max(0f, EnfoncementBaseAuSolMetres);

		if (!AncreSurApisara)
		{
			if (gm == null || !gm.EstTerrainClientPretPourPortailVersApisara(x, z, IdDimensionConteneur))
			{
				float yAtt = Gestionnaire_Monde.EstimerAltitudeTerrainPortail(x, z, IdDimensionConteneur);
				if (_hintServeurSurfaceRecu)
					yAtt = _yHintServeurSurfaceNexus;
				_ySurfaceSolMondeRaycast = yAtt;
				GlobalPosition = new Vector3(x, yAtt - enf, z);
				_solSurfaceConfirmePourAffichage = false;
				_alignementYSolConfirmeParRaycastMesh = false;
				RafraichirVisibiliteCombinee();
				return;
			}

			World3D world = GetWorld3D();
			if (world?.DirectSpaceState != null)
			{
				Vector3 debut = new Vector3(x, HauteurDepartRayonMetres, z);
				Vector3 fin = new Vector3(x, ConstantesDimensionAbysse.FondAbsolu, z);
				var query = PhysicsRayQueryParameters3D.Create(debut, fin);
				query.CollisionMask = MasqueCollisionSol;
				query.CollideWithAreas = false;
				query.CollideWithBodies = true;
				Godot.Collections.Array<Rid> exclus = ObtenirRidsCorpsAExclureDuRaycastSol();
				if (exclus.Count > 0)
					query.Exclude = exclus;
				var hit = world.DirectSpaceState.IntersectRay(query);
				if (hit.Count > 0 && hit.ContainsKey("position"))
				{
					var p = (Vector3)hit["position"];
					FinaliserPortailAuSolConfirme(p.X, p.Y, p.Z, arretAlignementAutomatique: true);
					return;
				}
			}

			if (ConstantesDimensions.EssayerObtenirInfo(IdDimensionConteneur, out var infoDim) && infoDim.EstAlphaLike
				&& gm.EssayerObtenirYSurfaceTerrainDepuisVoxelsChunk(x, z, IdDimensionConteneur, out float yVox))
			{
				FinaliserPortailAuSolConfirme(x, yVox, z, arretAlignementAutomatique: true);
				return;
			}

			if (_hintServeurSurfaceRecu)
			{
				FinaliserPortailAuSolConfirme(x, _yHintServeurSurfaceNexus, z, arretAlignementAutomatique: true);
				return;
			}

			float yProc = Gestionnaire_Monde.EstimerAltitudeTerrainPortail(x, z, IdDimensionConteneur);
			_ySurfaceSolMondeRaycast = yProc;
			GlobalPosition = new Vector3(x, yProc - enf, z);
			_solSurfaceConfirmePourAffichage = false;
			_alignementYSolConfirmeParRaycastMesh = false;
			PlanifierReconstructionCollisionsPortaille();
			Callable.From(ConfigurerFormeEtPositionZoneTrigger).CallDeferred();
			RafraichirVisibiliteCombinee();
			return;
		}

		World3D worldA = GetWorld3D();
		if (worldA?.DirectSpaceState != null)
		{
			Vector3 debut = new Vector3(x, HauteurDepartRayonMetres, z);
			Vector3 fin = new Vector3(x, ConstantesDimensionAbysse.FondAbsolu, z);
			var query = PhysicsRayQueryParameters3D.Create(debut, fin);
			query.CollisionMask = MasqueCollisionSol;
			query.CollideWithAreas = false;
			query.CollideWithBodies = true;
			Godot.Collections.Array<Rid> exclus = ObtenirRidsCorpsAExclureDuRaycastSol();
			if (exclus.Count > 0)
				query.Exclude = exclus;
			var hit = worldA.DirectSpaceState.IntersectRay(query);
			if (hit.Count > 0 && hit.ContainsKey("position"))
			{
				var p = (Vector3)hit["position"];
				FinaliserPortailAuSolConfirme(p.X, p.Y, p.Z, arretAlignementAutomatique: true);
				return;
			}
		}

		float yProcA = Gestionnaire_Monde.EstimerAltitudeTerrainPortail(x, z, IdDimensionConteneur);
		_ySurfaceSolMondeRaycast = yProcA;
		GlobalPosition = new Vector3(x, yProcA - enf, z);
		PlanifierReconstructionCollisionsPortaille();
		if (AncreSurApisara)
			PlanifierRemblaiSocleSousPortail();
		Callable.From(ConfigurerFormeEtPositionZoneTrigger).CallDeferred();
		RafraichirVisibiliteCombinee();
	}

	private void RafraichirVisibiliteCombinee()
	{
		if (AncreSurApisara)
			Visible = _visibiliteDemandeeParGestionnaireDimension;
		else
			Visible = _visibiliteDemandeeParGestionnaireDimension && _solSurfaceConfirmePourAffichage;
	}

	/// <summary>Impact raycast ou surface lue depuis les voxels : pieds au sol, remblai, trigger.</summary>
	/// <param name="arretAlignementAutomatique">Faux après Y serveur seul : le raycast pourra encore affiner sur le mesh.</param>
	private void FinaliserPortailAuSolConfirme(float x, float ySolMonde, float z, bool arretAlignementAutomatique = true)
	{
		_ySurfaceSolMondeRaycast = ySolMonde;
		if (!AncreSurApisara)
		{
			_alignementYSolConfirmeParRaycastMesh = arretAlignementAutomatique;
			_solSurfaceConfirmePourAffichage = true;
		}
		float enfoncement = Mathf.Max(0f, EnfoncementBaseAuSolMetres);
		GlobalPosition = new Vector3(x, ySolMonde - enfoncement, z);
		PlanifierReconstructionCollisionsPortaille();
		AppliquerEchelleModeleVisuel();
		CorrigerEnfoncementSiMaillagePlaneAuDessusDuRaycastSol();
		PlanifierRemblaiSocleSousPortail();
		Callable.From(ConfigurerFormeEtPositionZoneTrigger).CallDeferred();
		RafraichirVisibiliteCombinee();
	}

	/// <summary>Zone de détection sur la membrane (grande arche) ou sphère à l’origine ; à rappeler après changement d’échelle / alignement.</summary>
	private void ConfigurerFormeEtPositionZoneTrigger()
	{
		if (_zone == null) return;
		CollisionShape3D cs = _zone.GetNodeOrNull<CollisionShape3D>("FormeTriggerPortail");
		if (cs == null && _zone.GetChildCount() > 0 && _zone.GetChild(0) is CollisionShape3D cs0)
			cs = cs0;
		if (cs == null) return;

		if (TriggerSurMembrane)
		{
			_zone.Position = PositionLocaleMembrane;
			_zone.RotationDegrees = RotationMembraneDegres;
			cs.Position = Vector3.Zero;
			cs.RotationDegrees = Vector3.Zero;
			float echelleRef = Mathf.Max(1f, EchelleModelePortaille);
			float epaisseur = Mathf.Max(echelleRef * 0.14f, 7f);
			float largeur = Mathf.Max(TailleMembraneMetres.X * 1.05f, echelleRef * 0.42f);
			float hauteur = Mathf.Max(TailleMembraneMetres.Y * 1.05f, echelleRef * 0.55f);
			cs.Shape = new BoxShape3D { Size = new Vector3(epaisseur, hauteur, largeur) };
		}
		else
		{
			_zone.Position = Vector3.Zero;
			_zone.RotationDegrees = Vector3.Zero;
			cs.Position = Vector3.Zero;
			cs.RotationDegrees = Vector3.Zero;
			cs.Shape = new SphereShape3D { Radius = Mathf.Max(0.5f, RayonTriggerMetres) };
		}
	}

	/// <summary>Évite que le rayon sol touche le cadre <c>PortailleVisuel</c> (même calque que le terrain).</summary>
	private Godot.Collections.Array<Rid> ObtenirRidsCorpsAExclureDuRaycastSol()
	{
		var arr = new Godot.Collections.Array<Rid>();
		AjouterRidsCollisionRecursif(this, arr);
		return arr;
	}

	private static void AjouterRidsCollisionRecursif(Node noeud, Godot.Collections.Array<Rid> cible)
	{
		if (noeud is CollisionObject3D co)
		{
			Rid rid = co.GetRid();
			if (rid.IsValid)
				cible.Add(rid);
		}

		foreach (Node enfant in noeud.GetChildren())
			AjouterRidsCollisionRecursif(enfant, cible);
	}

	private void PlanifierRemblaiSocleSousPortail()
	{
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		if (gm == null) return;
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;
		if (!AncreSurApisara && !_alignementYSolConfirmeParRaycastMesh)
			return;
		float ySol = _ySurfaceSolMondeRaycast;
		int r = Mathf.Max(0, RayonSocleVoxelsRemblai);
		int prof = Mathf.Max(1, ProfondeurSocleVoxelsRemblai);
		Callable.From(() =>
			gm.DemanderRemplissageSocleSousPortail(GlobalPosition, IdDimensionConteneur, ySol, r, prof)).CallDeferred();
	}

	private void PlanifierReconstructionCollisionsPortaille()
	{
		if (_collisionsPortailleConstruites) return;
		Callable.From(ReconstruireCollisionsCadrePortaille).CallDeferred();
	}

	private void CreerVisuelMembraneEtLueur()
	{
		if (GetNodeOrNull("MembraneNeant") != null) return;
		var shader = GD.Load<Shader>("res://MembraneNeant.gdshader");
		if (shader == null) return;
		var mat = new ShaderMaterial { Shader = shader };
		mat.SetShaderParameter("albedo_color", new Color(0.02f, 0.05f, 0.14f, 0.88f));
		mat.SetShaderParameter("emission_color", new Color(0.06f, 0.1f, 0.28f));
		mat.SetShaderParameter("emission_energy", 0.32f);
		mat.SetShaderParameter("clip_bas_uv", Mathf.Clamp(MembraneClipBasUv, 0f, 0.49f));

		// Quad vertical (plan XY) ; rotation Y 90° par défaut = plan YZ, souvent orthogonal à l’axe « avant » du cadre importé (évite l’effet +).
		var taille = new Vector2(
			Mathf.Max(0.5f, TailleMembraneMetres.X),
			Mathf.Max(0.5f, TailleMembraneMetres.Y));
		_membrane = new MeshInstance3D
		{
			Name = "MembraneNeant",
			Mesh = new QuadMesh { Size = taille },
			MaterialOverride = mat,
			Position = PositionLocaleMembrane,
			RotationDegrees = RotationMembraneDegres
		};
		AddChild(_membrane);

		_lueur = new OmniLight3D
		{
			Name = "LueurPortail",
			LightColor = new Color(0.25f, 0.45f, 0.95f),
			LightEnergy = 0.55f,
			OmniRange = 84f,
			OmniAttenuation = 0.35f,
			ShadowEnabled = false,
			Position = PositionLocaleLueur
		};
		AddChild(_lueur);
	}

	private void SurCorpsEntreDansTrigger(Node3D body)
	{
		if (body is not Joueur j) return;
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;
		if (_cooldownRestant > 0.0) return;
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		if (gm == null) return;
		if (gm.EstVerrouSecuriteAbysseActif()) return;

		AnnulerSequenceTpSiEnCours();
		_joueurStationnaireDansZone = j;
		DemarrerSequenceAssombrissementPuisTp(gm);
	}

	private void SurCorpsSortiDuTrigger(Node3D body)
	{
		if (body is Joueur j && ReferenceEquals(j, _joueurStationnaireDansZone))
		{
			AnnulerSequenceTpSiEnCours();
			_joueurStationnaireDansZone = null;
		}
	}

	private void AnnulerSequenceTpSiEnCours()
	{
		if (_timerApresAssombrissement != null && GodotObject.IsInstanceValid(_timerApresAssombrissement))
		{
			_timerApresAssombrissement.Timeout -= SurTimerTpApresAssombrissement;
			_timerApresAssombrissement.QueueFree();
			_timerApresAssombrissement = null;
		}

		_sequenceTpVisuelleEnCours = false;
	}

	private void DemarrerSequenceAssombrissementPuisTp(Gestionnaire_Monde gm)
	{
		if (_joueurStationnaireDansZone == null || !GodotObject.IsInstanceValid(_joueurStationnaireDansZone)) return;
		_sequenceTpVisuelleEnCours = true;
		float d = Mathf.Max(0.35f, DureeStationnairePourTeleporterSecondes);
		long peerId = gm.ObtenirPeerIdPourNoeudJoueur(_joueurStationnaireDansZone);
		gm.DiffuserAssombrissementPortailAuxClients(peerId, d);
		float delaiTeleport = gm.ObtenirDelaiTeleportPendantTransitionPortail(d);

		_timerApresAssombrissement = new Godot.Timer { OneShot = true, WaitTime = delaiTeleport };
		AddChild(_timerApresAssombrissement);
		_timerApresAssombrissement.Timeout += SurTimerTpApresAssombrissement;
		_timerApresAssombrissement.Start();
	}

	private void SurTimerTpApresAssombrissement()
	{
		if (_timerApresAssombrissement != null && GodotObject.IsInstanceValid(_timerApresAssombrissement))
		{
			_timerApresAssombrissement.QueueFree();
			_timerApresAssombrissement = null;
		}

		_sequenceTpVisuelleEnCours = false;
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		if (gm == null || _joueurStationnaireDansZone == null || !GodotObject.IsInstanceValid(_joueurStationnaireDansZone))
		{
			_joueurStationnaireDansZone = null;
			return;
		}

		if (gm.EstVerrouSecuriteAbysseActif())
		{
			_joueurStationnaireDansZone = null;
			return;
		}

		ExecuterTeleporterVersDimension(_joueurStationnaireDansZone, gm);
		_joueurStationnaireDansZone = null;
	}

	private void ExecuterTeleporterVersDimension(Joueur joueur, Gestionnaire_Monde gm)
	{
		ObtenirDimensionEtPositionCible(out int dimCible, out Vector3 cibleXZ);
		string nom = ConstantesDimensions.ObtenirNomCanonique(dimCible);
		string cleLore = NexusPortailsApisara.MappingPortailsApisara.TryGetValue(Liaison, out string lore) ? lore : "?";
		string msg = AncreSurApisara
			? $"Retour vers {nom}."
			: $"Transfert vers {ConstantesDimensionAbysse.Apisara} ({cleLore}).";

		gm.TransfererJoueurViaPortail(joueur, dimCible, cibleXZ, msg);
		gm.ArmerCooldownPortailNexus(dimCible, Liaison, versApisara: !AncreSurApisara, Mathf.Max(0.25f, CooldownPortailSecondes + 0.8f));
		_cooldownRestant = CooldownPortailSecondes;
	}

	/// <summary>
	/// Garantit la bijection canonique Nexus : NORD↔Alpha, EST↔Beta, SUD↔Omega, OUEST↔Delta (évite une <see cref="Liaison"/> erronée depuis l’éditeur ou une scène).
	/// </summary>
	private void AppliquerLiaisonNexusCanoniqueSiApplicable()
	{
		if (AncreSurApisara)
		{
			if (IdDimensionConteneur != (int)DimensionJeu.Abysse) return;
			string nom = Name.ToString();
			const string prefix = "PortailDepuisApisara_";
			if (!nom.StartsWith(prefix, StringComparison.Ordinal)) return;
			string suffix = nom[prefix.Length..];
			if (Enum.TryParse(suffix, ignoreCase: false, out PointCardinal c))
				Liaison = c;
		}
		else if (ConstantesDimensions.EssayerObtenirInfo(IdDimensionConteneur, out var inf) && inf.EstAlphaLike)
			Liaison = NexusPortailsApisara.ObtenirCardinalPourDimensionAlphaLike(IdDimensionConteneur);
	}

	private void ObtenirDimensionEtPositionCible(out int dimensionId, out Vector3 positionXZLogique)
	{
		AppliquerLiaisonNexusCanoniqueSiApplicable();
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		// Au moins 20 m : évite de réapparaître sous l’arche ; l’export peut augmenter au-delà.
		float distanceDevant = Mathf.Max(20f, DistanceApparitionDevantPortailMetres);
		if (AncreSurApisara)
		{
			dimensionId = NexusPortailsApisara.ObtenirIdDimensionQuadrant(Liaison);
			if (gm != null)
			{
				if (gm.EssayerObtenirPointArriveeDevantPortailNexus(dimensionId, Liaison, versApisara: false, distanceDevant, out Vector3 pointArrivee))
				{
					positionXZLogique = pointArrivee;
					return;
				}
				Vector2 xz = gm.ObtenirXZPortailVersApisaraPourDimension(dimensionId);
				float d = distanceDevant;
				switch (Liaison)
				{
					case PointCardinal.NORD:
						positionXZLogique = new Vector3(xz.X, 5f, xz.Y + d);
						break;
					case PointCardinal.SUD:
						positionXZLogique = new Vector3(xz.X, 5f, xz.Y - d);
						break;
					case PointCardinal.EST:
						positionXZLogique = new Vector3(xz.X + d, 5f, xz.Y);
						break;
					case PointCardinal.OUEST:
						positionXZLogique = new Vector3(xz.X - d, 5f, xz.Y);
						break;
					default:
						positionXZLogique = new Vector3(xz.X, 5f, xz.Y + d);
						break;
				}
			}
			else
			{
				var b = NexusCoords.BaseZero;
				positionXZLogique = new Vector3(b.X + distanceDevant, b.Y, b.Z);
			}
		}
		else
		{
			dimensionId = (int)DimensionJeu.Abysse;
			if (gm != null && gm.EssayerObtenirPointArriveeDevantPortailNexus(dimensionId, Liaison, versApisara: true, distanceDevant, out Vector3 pointArrivee))
			{
				positionXZLogique = pointArrivee;
				return;
			}
			var a = NexusCoords.ObtenirAncreApisara(Liaison);
			Vector3 centrePrairie = NexusCoords.BaseZero;
			Vector3 delta = new Vector3(centrePrairie.X - a.X, 0f, centrePrairie.Z - a.Z);
			if (delta.LengthSquared() < 0.25f)
				delta = Vector3.Forward;
			Vector3 dirVersCentre = delta.Normalized();
			positionXZLogique = a + dirVersCentre * distanceDevant;
			positionXZLogique.Y = a.Y;
		}
	}

	/// <summary>Instancie le GLB du cadre si la scène n’a pas de nœud <c>PortailleVisuel</c> (éditeur ou scène minimale).</summary>
	private void AssurerModelePortailleCharge()
	{
		if (GetNodeOrNull("PortailleVisuel") != null) return;
		if (string.IsNullOrWhiteSpace(CheminScenePortaille)) return;
		var ps = GD.Load<PackedScene>(CheminScenePortaille);
		if (ps == null)
		{
			GD.PrintErr($"ZERO-K Portail '{Name}' : échec du chargement du modèle ({CheminScenePortaille}).");
			return;
		}
		var visuel = ps.Instantiate<Node>();
		visuel.Name = "PortailleVisuel";
		AddChild(visuel);
		MoveChild(visuel, 0);
	}

	private void AppliquerEchelleModeleVisuel()
	{
		var v = GetNodeOrNull<Node3D>("PortailleVisuel");
		if (v == null) return;
		float s = Mathf.Max(0.01f, EchelleModelePortaille);
		if (!Mathf.IsEqualApprox(_echellePortailleAppliqueePourAjustementAabb, s))
		{
			_echellePortailleAppliqueePourAjustementAabb = s;
			Vector3 p = v.Position;
			v.Position = new Vector3(p.X, 0f, p.Z);
		}

		v.Scale = new Vector3(s, s, s);
		AjusterPortailleVisuelPourPiedAuSolDansParent();
		ConfigurerFormeEtPositionZoneTrigger();
	}

	/// <summary>Compense un pivot GLB haut : le point le plus bas des meshes (espace local du portail) est ramené à Y≈0.</summary>
	private void AjusterPortailleVisuelPourPiedAuSolDansParent()
	{
		if (!AjusterBasMaillageSurOriginePortail || !IsInsideTree()) return;
		var v = GetNodeOrNull<Node3D>("PortailleVisuel");
		if (v == null) return;

		float minY = float.MaxValue;
		CollecterMinYMeshesPortailLocalDepuisNoeud(v, ref minY);
		if (minY >= float.MaxValue - 1e30f) return;

		Vector3 p = v.Position;
		float decal = DecalageVerticalSupplementaireApresAabbMetres;
		v.Position = new Vector3(p.X, p.Y - minY + decal, p.Z);
	}

	private void CollecterMinYMeshesPortailLocalDepuisNoeud(Node noeud, ref float minY)
	{
		if (noeud.Name == "CollisionsCadrePortaille")
			return;

		if (noeud is MeshInstance3D mi && mi.Visible && mi.Mesh != null)
			AjouterMinYLocalDepuisAabbMesh(mi, ref minY);

		foreach (Node enfant in noeud.GetChildren())
		{
			if (enfant.Name == "CollisionsCadrePortaille")
				continue;
			CollecterMinYMeshesPortailLocalDepuisNoeud(enfant, ref minY);
		}
	}

	private void AjouterMinYLocalDepuisAabbMesh(MeshInstance3D mi, ref float minY)
	{
		Aabb aabb = mi.GetAabb();
		Vector3 mn = aabb.Position;
		Vector3 mx = mn + aabb.Size;
		for (int ix = 0; ix < 2; ix++)
		{
			float cx = ix == 0 ? mn.X : mx.X;
			for (int iy = 0; iy < 2; iy++)
			{
				float cy = iy == 0 ? mn.Y : mx.Y;
				for (int iz = 0; iz < 2; iz++)
				{
					float cz = iz == 0 ? mn.Z : mx.Z;
					Vector3 coinLocalMesh = new Vector3(cx, cy, cz);
					Vector3 monde = mi.GlobalTransform * coinLocalMesh;
					minY = Mathf.Min(minY, ToLocal(monde).Y);
				}
			}
		}
	}

	/// <summary>Plus bas Y monde des vertices d’AABB des meshes visibles du cadre (hors collisions).</summary>
	private float ObtenirYMinimumMaillagePortailleMonde()
	{
		var v = GetNodeOrNull<Node3D>("PortailleVisuel");
		if (v == null) return float.NaN;
		float minW = float.MaxValue;
		CollecterMinYMondeMeshesPortaille(v, ref minW);
		return minW >= float.MaxValue - 1e30f ? float.NaN : minW;
	}

	private void CollecterMinYMondeMeshesPortaille(Node noeud, ref float minW)
	{
		if (noeud.Name == "CollisionsCadrePortaille")
			return;

		if (noeud is MeshInstance3D mi && mi.Visible && mi.Mesh != null)
		{
			Aabb aabb = mi.GetAabb();
			Vector3 mn = aabb.Position;
			Vector3 mx = mn + aabb.Size;
			for (int ix = 0; ix < 2; ix++)
			{
				float cx = ix == 0 ? mn.X : mx.X;
				for (int iy = 0; iy < 2; iy++)
				{
					float cy = iy == 0 ? mn.Y : mx.Y;
					for (int iz = 0; iz < 2; iz++)
					{
						float cz = iz == 0 ? mn.Z : mx.Z;
						Vector3 monde = mi.GlobalTransform * new Vector3(cx, cy, cz);
						minW = Mathf.Min(minW, monde.Y);
					}
				}
			}
		}

		foreach (Node enfant in noeud.GetChildren())
		{
			if (enfant.Name == "CollisionsCadrePortaille")
				continue;
			CollecterMinYMondeMeshesPortaille(enfant, ref minW);
		}
	}

	/// <summary>Après raycast sol : si le bas du maillage reste nettement au-dessus de la surface détectée, abaisse le nœud (GLB pivot / AABB).</summary>
	private void CorrigerEnfoncementSiMaillagePlaneAuDessusDuRaycastSol()
	{
		if (AncreSurApisara || !_alignementYSolConfirmeParRaycastMesh) return;
		float ySurface = _ySurfaceSolMondeRaycast;
		const float toleranceMetres = 0.22f;
		for (int passe = 0; passe < 5; passe++)
		{
			float minMesh = ObtenirYMinimumMaillagePortailleMonde();
			if (float.IsNaN(minMesh)) return;
			if (minMesh <= ySurface + toleranceMetres)
				return;
			GlobalPosition += new Vector3(0f, ySurface - minMesh, 0f);
		}
	}

	/// <summary>Collisions statiques par mesh du GLB (trimesh par défaut : suit la matière, ouverture praticable). Parentées au <c>PortailleVisuel</c>.</summary>
	private void ReconstruireCollisionsCadrePortaille()
	{
		if (_collisionsPortailleConstruites) return;
		var visuel = GetNodeOrNull<Node3D>("PortailleVisuel");
		if (visuel == null) return;

		// Ancienne version : StaticBody sous le nœud Portail — retirer si présent.
		var legacy = GetNodeOrNull<Node>("CollisionsCadrePortaille");
		if (legacy != null && legacy.GetParent() == this)
		{
			RemoveChild(legacy);
			legacy.Free();
		}

		var existant = visuel.GetNodeOrNull<StaticBody3D>("CollisionsCadrePortaille");
		if (existant != null)
		{
			visuel.RemoveChild(existant);
			existant.Free();
		}

		var meshes = new System.Collections.Generic.List<MeshInstance3D>();
		if (visuel is MeshInstance3D miRacine && miRacine.Visible && miRacine.Mesh != null)
			meshes.Add(miRacine);

		var pile = new System.Collections.Generic.List<Node> { visuel };
		for (int i = 0; i < pile.Count; i++)
		{
			foreach (Node enfant in pile[i].GetChildren())
			{
				if (enfant.Name == "CollisionsCadrePortaille")
					continue;
				pile.Add(enfant);
				if (enfant is MeshInstance3D mi && mi.Visible && mi.Mesh != null)
					meshes.Add(mi);
			}
		}

		if (meshes.Count == 0) return;

		var corps = new StaticBody3D
		{
			Name = "CollisionsCadrePortaille",
			CollisionLayer = CalqueCollisionCadrePortail,
			CollisionMask = 0u,
			Position = Vector3.Zero,
			Rotation = Vector3.Zero,
			Scale = Vector3.One
		};
		visuel.AddChild(corps);

		int nb = 0;
		foreach (MeshInstance3D mi in meshes)
		{
			Shape3D shape = CreerShapeCollisionDepuisMeshPortaille(mi.Mesh);
			if (shape == null) continue;
			Transform3D relCorps = corps.GlobalTransform.AffineInverse() * mi.GlobalTransform;
			var cs = new CollisionShape3D
			{
				Name = $"HitPortaille_{nb}",
				Shape = shape,
				Transform = relCorps
			};
			corps.AddChild(cs);
			nb++;
		}

		if (nb > 0)
			_collisionsPortailleConstruites = true;
		else
			corps.QueueFree();
	}

	/// <summary>Trimesh = concave alignée sur les faces du GLB (ouverture du portail). Convex en secours si maillage dégénéré.</summary>
	private Shape3D CreerShapeCollisionDepuisMeshPortaille(Mesh mesh)
	{
		if (mesh == null) return null;
		if (CollisionPortailleTrimeshPrecis)
		{
			try
			{
				Vector3[] faces = mesh.GetFaces();
				if (faces != null && faces.Length >= 9)
				{
					Shape3D tris = mesh.CreateTrimeshShape();
					if (tris != null)
						return tris;
				}
			}
			catch
			{
				// secours convexe ci-dessous
			}
		}

		try
		{
			return mesh.CreateConvexShape(true, true);
		}
		catch
		{
			return null;
		}
	}

	private Gestionnaire_Monde ObtenirGestionnaireMonde()
	{
		Node p = GetParent();
		while (p != null)
		{
			var gm = p.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
			if (gm != null) return gm;
			p = p.GetParent();
		}
		return GetTree()?.Root?.FindChild("Gestionnaire_Monde", recursive: true, owned: false) as Gestionnaire_Monde;
	}
}
