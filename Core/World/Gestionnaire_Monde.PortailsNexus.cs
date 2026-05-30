using Godot;
using System;

/// <summary>
/// Portails Nexus (Portaille.glb) : placement par dimension, diffusion de la surface sol autoritaire, alignement raycast,
/// recherche/visibilite des portails et priorisation des chunks autour. Partie de <see cref="Gestionnaire_Monde"/>.
/// CONTRAT: liaisons cardinales fixes, repere XZ (0,0), RPC de sol et timings d'alignement identiques a l'historique.
/// </summary>
public partial class Gestionnaire_Monde : Node3D
{
	/// <summary>
	/// Modèle <c>Portaille.glb</c> : un portail à l’origine <c>(0, surface, 0)</c> par monde Alpha / Beta / Omega / Delta (vers APISARA) ;
	/// quatre portails sur la prairie extérieure APISARA (axes N, E, S, O ~1280 m). Liaisons fixes : Nord↔Alpha, Est↔Beta, Sud↔Omega, Ouest↔Delta (voir <see cref="NexusPortailsApisara"/>).
	/// </summary>
	private void InitialiserPortailsNexusSiNecessaire()
	{
		if (_portailsNexusPlaces || !UseArchitectureReseau) return;
		_portailsNexusPlaces = true;
		const string cheminPortaille = "res://Modeles/structure/portaille/Portaille.glb";
		var scene = GD.Load<PackedScene>("res://Scenes/PortailNexus.tscn");
		if (scene == null)
		{
			GD.PrintErr("ZERO-K : impossible de charger res://Scenes/PortailNexus.tscn.");
			return;
		}

		foreach (var info in ConstantesDimensions.ToutesAlphaLike())
		{
			if (!_racineParDimension.TryGetValue(info.Id, out Node3D racine) || racine == null) continue;
			var p = scene.Instantiate() as Portail;
			if (p == null) continue;
			p.Name = $"PortailVersApisara_{info.NomCanonique}";
			p.CheminScenePortaille = cheminPortaille;
			p.Liaison = NexusPortailsApisara.ObtenirCardinalPourDimensionAlphaLike(info.Id);
			p.AncreSurApisara = false;
			p.IdDimensionConteneur = info.Id;
			p.ForcerAttenteAffichageJusquaSolConfirmeVersApisara();
			racine.AddChild(p);
			var xz = ObtenirMeilleurXZPortailOrigineAlphaLike(info.Id, SeedTerrain);
			_xzPortailVersApisaraParDimension[info.Id] = xz;
			// Hauteur procédurale tout de suite (évite une frame à Y=0) ; au prochain idle : raycast vers le sol pour coller au mesh.
			float yInit = EstimerAltitudeTerrainPortail(xz.X, xz.Y, info.Id);
			float enf = Mathf.Max(0f, p.EnfoncementBaseAuSolMetres);
			p.GlobalPosition = new Vector3(xz.X, yInit - enf, xz.Y);
			Vector2 xzCapture = xz;
			int idDimCapture = info.Id;
			Callable.From(() => AffinerPortailVersApisaraSolParRaycast(p, xzCapture, idDimCapture)).CallDeferred();
		}

		if (_racineParDimension.TryGetValue((int)DimensionJeu.Abysse, out Node3D racineAb) && racineAb != null)
		{
			foreach (PointCardinal c in Enum.GetValues(typeof(PointCardinal)))
			{
				var p = scene.Instantiate() as Portail;
				if (p == null) continue;
				p.Name = $"PortailDepuisApisara_{c}";
				p.CheminScenePortaille = cheminPortaille;
				p.Liaison = c;
				p.AncreSurApisara = true;
				p.IdDimensionConteneur = (int)DimensionJeu.Abysse;
				racineAb.AddChild(p);
				var a = NexusCoords.ObtenirAncreApisara(c);
				float y = EstimerAltitudeTerrainPortail(a.X, a.Z, (int)DimensionJeu.Abysse);
				p.Position = new Vector3(a.X, y - Mathf.Max(0f, p.EnfoncementBaseAuSolMetres), a.Z);
				Callable.From(() => p.AlignerPortailSurSurface()).CallDeferred();
			}
		}

		PrioriserChunksClientAutourPortailsDimension(_dimensionLocaleActive);
		MettreAJourVisibilitePortailsParDimension(_dimensionLocaleActive);
		GD.Print("ZERO-K : portails Nexus (Portaille.glb) — 4 mondes à (0,0) + 4 sur plaine extérieure APISARA (N,E,S,O).");
		Callable.From(DiffuserSolPortailsNexusVersApisaraApresInitDepuisVoxelsServeur).CallDeferred();
	}

	/// <summary>Ordre fixe : Alpha, Beta, Omega, Delta — aligné sur <see cref="ConstantesDimensions.ToutesAlphaLike"/> usuel.</summary>
	private static readonly int[] _ordreDimensionsPortailVersApisara =
	{
		(int)DimensionJeu.Alpha,
		(int)DimensionJeu.Beta,
		(int)DimensionJeu.Omega,
		(int)DimensionJeu.Delta
	};

	/// <summary>
	/// Serveur ou solo : lit la surface voxel à (0,0) par dimension, applique aux portails, envoie aux clients distants.
	/// Les portails APISARA (<see cref="Portail.AncreSurApisara"/>) ne sont pas concernés.
	/// </summary>
	private void DiffuserSolPortailsNexusVersApisaraApresInitDepuisVoxelsServeur()
	{
		if (!UseArchitectureReseau)
			return;
		bool serveurOuSolo = !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
		float yAlpha = -1f, yBeta = -1f, yOmega = -1f, yDelta = -1f;
		if (serveurOuSolo)
		{
			for (int i = 0; i < _ordreDimensionsPortailVersApisara.Length; i++)
			{
				int dim = _ordreDimensionsPortailVersApisara[i];
				Monde_Serveur srv = ObtenirServeurDimension(dim);
				float y = -1f;
				if (srv != null && srv.EssayerObtenirYSurfaceMondeDepuisVoxels(0, 0, out float ySurf))
					y = ySurf;
				switch (i)
				{
					case 0: yAlpha = y; break;
					case 1: yBeta = y; break;
					case 2: yOmega = y; break;
					case 3: yDelta = y; break;
				}
			}
			AppliquerYSolPortailsNexusVersApisaraAuxInstances(yAlpha, yBeta, yOmega, yDelta);
			if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
			{
				foreach (long peerId in Multiplayer.GetPeers())
					RpcId((int)peerId, nameof(RpcRecevoirYSolPortailsNexusVersApisara), yAlpha, yBeta, yOmega, yDelta);
			}
		}
	}

	private void AppliquerYSolPortailsNexusVersApisaraAuxInstances(float yAlpha, float yBeta, float yOmega, float yDelta)
	{
		float[] ys = { yAlpha, yBeta, yOmega, yDelta };
		for (int i = 0; i < _ordreDimensionsPortailVersApisara.Length; i++)
		{
			if (ys[i] < 0f)
				continue;
			int dim = _ordreDimensionsPortailVersApisara[i];
			if (!_racineParDimension.TryGetValue(dim, out Node3D racine) || racine == null) continue;
			foreach (Node enfant in racine.GetChildren())
			{
				if (enfant is not Portail portail || portail.AncreSurApisara)
					continue;
				if (!portail.Name.ToString().StartsWith("PortailVersApisara_", StringComparison.Ordinal))
					continue;
				portail.AppliquerSurfaceSolAutoritaireServeur(ys[i]);
				break;
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RpcRecevoirYSolPortailsNexusVersApisara(float yAlpha, float yBeta, float yOmega, float yDelta)
	{
		AppliquerYSolPortailsNexusVersApisaraAuxInstances(yAlpha, yBeta, yOmega, yDelta);
	}

	/// <summary>
	/// Après chargement : raycast vertical (ciel → fond) au XZ du portail « vers APISARA » pour obtenir la vraie hauteur du terrain mesh ;
	/// sinon on garde l’estimé procédural. Repositionne le nœud puis réaligne trigger / remblai.
	/// </summary>
	private void AffinerPortailVersApisaraSolParRaycast(Portail p, Vector2 xz, int dimensionId)
	{
		if (p == null || !GodotObject.IsInstanceValid(p) || p.AncreSurApisara) return;
		// Placement réel : <see cref="Portail.AlignerPortailSurSurface"/> (attente chunk + raycast ciel→sol).
		float enf = Mathf.Max(0f, p.EnfoncementBaseAuSolMetres);
		float yProc = EstimerAltitudeTerrainPortail(xz.X, xz.Y, dimensionId);
		p.GlobalPosition = new Vector3(xz.X, yProc - enf, xz.Y);
		p.AlignerPortailSurSurface();
	}

	/// <summary>Raycast monde vers le bas (même principe que <see cref="Portail.AlignerPortailSurSurface"/>), sans exclure de corps.</summary>
	private static bool EssayerObtenirAltitudeSolParRaycastXZ(Node3D noeudReferenceMonde, float x, float z, int dimensionId, out float ySol)
	{
		ySol = 0f;
		World3D world = noeudReferenceMonde.GetWorld3D();
		if (world?.DirectSpaceState == null) return false;

		float yRef = EstimerAltitudeTerrainPortail(x, z, dimensionId);
		float debutY = Mathf.Max(3200f, yRef + 500f);
		Vector3 debut = new Vector3(x, debutY, z);
		Vector3 fin = new Vector3(x, ConstantesDimensionAbysse.FondAbsolu, z);
		var query = PhysicsRayQueryParameters3D.Create(debut, fin);
		query.CollisionMask = 1u;
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		var hit = world.DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0 && hit.ContainsKey("position"))
		{
			ySol = ((Vector3)hit["position"]).Y;
			return true;
		}

		return false;
	}

	/// <summary>Surface à partir des voxels déjà chargés côté client (comme la flore) ; uniquement si <paramref name="dimensionIdPortail"/> est la dimension <b>localement</b> affichée.</summary>
	public bool EssayerObtenirYSurfaceTerrainDepuisVoxelsChunk(float mondeX, float mondeZ, int dimensionIdPortail, out float ySurface)
	{
		ySurface = 0f;
		if (_mondeClient == null || dimensionIdPortail != _dimensionLocaleActive) return false;
		return _mondeClient.EssayerObtenirYSurfaceMondeDepuisDonneesVoxel(mondeX, mondeZ, out ySurface);
	}

	/// <summary>Altitude monde approximative du sol (bruit procédural), même logique que le placement initial des portails.</summary>
	public static float EstimerAltitudeTerrainPortail(float x, float z, int dimensionId)
	{
		int seed = GameState.Instance?.SeedTerrainActuel ?? 19847;
		if (dimensionId == (int)DimensionJeu.Abysse)
		{
			int h = ApisaraHauteurTerrain.ObtenirHauteurSolMonde(Mathf.FloorToInt(x), Mathf.FloorToInt(z), seed);
			return h + 1f;
		}

		// Même ordre de grandeur qu’APISARA (+1) : la face du voxel surface est à h ; +10 plaçait le portail dans le ciel avant raycast.
		int hAlpha = Generateur_Voxel.ObtenirHauteurTerrainMonde(Mathf.FloorToInt(x), Mathf.FloorToInt(z), seed);
		return hAlpha + 1f;
	}

	/// <summary>Repère XZ du portail « vers APISARA » : toujours <b>(0,0)</b> monde (chunk 0,0 chargé en priorité — alignement sol / raycast fiables).</summary>
	public static Vector2 ObtenirMeilleurXZPortailOrigineAlphaLike(int dimensionId, int seedTerrain)
	{
		_ = dimensionId;
		_ = seedTerrain;
		return Vector2.Zero;
	}

	/// <summary>Position XZ du portail vers APISARA pour une dimension Alpha-like (retour depuis l’ancre APISARA).</summary>
	public Vector2 ObtenirXZPortailVersApisaraPourDimension(int dimensionId)
	{
		if (_xzPortailVersApisaraParDimension.TryGetValue(dimensionId, out Vector2 v))
			return v;
		return ObtenirMeilleurXZPortailOrigineAlphaLike(dimensionId, SeedTerrain);
	}

	/// <summary>Delai de TP pendant la transition immersive portail (noir + vitesse), aligné avec l'orchestration visuelle client.</summary>
	public float ObtenirDelaiTeleportPendantTransitionPortail(float dureeTotaleSec)
	{
		float d = Mathf.Max(0.35f, dureeTotaleSec);
		float fadeIn = Mathf.Clamp(d * 0.30f, 0.22f, 1.0f);
		float fadeOut = Mathf.Clamp(d * 0.26f, 0.20f, 0.85f);
		float phaseVitesse = Mathf.Max(0.10f, d - fadeIn - fadeOut);
		return Mathf.Clamp(fadeIn + phaseVitesse * 0.50f, 0.20f, d);
	}

	/// <summary>Retourne un point d'arrivée à distance fixe devant l’ouverture du portail (membrane), sur le plan horizontal — évite un spawn sous l’arche ou « dans » le cadre.</summary>
	public bool EssayerObtenirPointArriveeDevantPortailNexus(int dimensionIdCible, PointCardinal liaison, bool versApisara, float distanceMetres, out Vector3 pointMonde)
	{
		pointMonde = Vector3.Zero;
		Portail portailCible = TrouverPortailNexusDimension(dimensionIdCible, liaison, versApisara);
		if (portailCible == null)
			return false;

		Transform3D gt = portailCible.GlobalTransform;
		// Pivot à l’ouverture (aligné trigger / visuel), pas seulement l’origine au sol du nœud.
		Vector3 pivotMembrane = gt * portailCible.PositionLocaleMembrane;
		Vector3 basisZ = gt.Basis.Z;
		// Côté « monde » après traversée : opposé à la direction d’entrée (−Z local = regard à travers le portail). Sortie = +Z local → projection horizontale de +Basis.Z (évite −Z qui replaçait sous l’arche / vers Apisara).
		Vector3 dirHoriz = new Vector3(basisZ.X, 0f, basisZ.Z);
		if (dirHoriz.LengthSquared() < 1e-8f)
		{
			Vector3 dir3 = basisZ;
			if (dir3.LengthSquared() < 1e-8f)
				dir3 = Vector3.Back;
			dirHoriz = dir3.Normalized();
		}
		else
			dirHoriz = dirHoriz.Normalized();

		// Sécurité globale Nexus: jamais moins de 20 m devant la membrane pour éviter une réapparition dans/près du portail.
		float distance = Mathf.Max(20f, distanceMetres);
		Vector3 cible = pivotMembrane + dirHoriz * distance;
		pointMonde = new Vector3(cible.X, pivotMembrane.Y, cible.Z);
		return true;
	}

	/// <summary>Applique un cooldown sur le portail de destination pour éviter les boucles TP immédiates.</summary>
	public void ArmerCooldownPortailNexus(int dimensionIdCible, PointCardinal liaison, bool versApisara, float cooldownSec)
	{
		Portail portailCible = TrouverPortailNexusDimension(dimensionIdCible, liaison, versApisara);
		if (portailCible != null)
			portailCible.ArmerCooldownPortailArrivee(cooldownSec);
	}

	private Portail TrouverPortailNexusDimension(int dimensionIdCible, PointCardinal liaison, bool versApisara)
	{
		if (!_racineParDimension.TryGetValue(dimensionIdCible, out Node3D racine) || racine == null)
			return null;
		Portail meilleur = null;
		float meilleureDistance2 = float.MaxValue;
		Vector3 cibleCanonique = Vector3.Zero;
		bool cibleCanoniqueValide = false;
		if (versApisara)
		{
			cibleCanonique = NexusCoords.ObtenirAncreApisara(liaison);
			cibleCanoniqueValide = true;
		}
		else if (ConstantesDimensions.EssayerObtenirInfo(dimensionIdCible, out var infoDim) && infoDim.EstAlphaLike)
		{
			Vector2 xz = ObtenirXZPortailVersApisaraPourDimension(dimensionIdCible);
			cibleCanonique = new Vector3(xz.X, 0f, xz.Y);
			cibleCanoniqueValide = true;
		}
		foreach (Node enfant in racine.GetChildren())
		{
			if (enfant is not Portail p || !GodotObject.IsInstanceValid(p))
				continue;
			if (versApisara)
			{
				if (!p.AncreSurApisara || p.Liaison != liaison)
					continue;
			}
			else
			{
				if (p.AncreSurApisara || p.Liaison != liaison)
					continue;
			}
			if (cibleCanoniqueValide)
			{
				Vector3 pp = p.GlobalPosition;
				Vector3 pc = new Vector3(pp.X, 0f, pp.Z);
				Vector3 cc = new Vector3(cibleCanonique.X, 0f, cibleCanonique.Z);
				float d2 = pc.DistanceSquaredTo(cc);
				if (d2 < meilleureDistance2)
				{
					meilleureDistance2 = d2;
					meilleur = p;
				}
				continue;
			}
			return p;
		}
		return meilleur;
	}

	private void MettreAJourVisibilitePortailsParDimension(int dimensionIdActif)
	{
		if (!UseArchitectureReseau || _racineParDimension.Count == 0) return;
		foreach (var kv in _racineParDimension)
		{
			if (kv.Value == null || !GodotObject.IsInstanceValid(kv.Value)) continue;
			foreach (Node enfant in kv.Value.GetChildren())
			{
				if (enfant is Portail portail)
					portail.DefinirVisibiliteSelonDimensionActive(kv.Key == dimensionIdActif);
			}
		}
	}

	private void MarquerPortailsDimensionPourRealignementSol(int dimensionId)
	{
		if (!_racineParDimension.TryGetValue(dimensionId, out Node3D racine) || racine == null) return;
		foreach (Node enfant in racine.GetChildren())
		{
			if (enfant is Portail p)
				p.MarquerAttenteNouveauRaycastSol();
		}
	}

	/// <summary>Chunks du client : priorité au sol sous les portails Nexus de la dimension (collision raycast).</summary>
	private void PrioriserChunksClientAutourPortailsDimension(int dimensionId)
	{
		if (_mondeClient == null || TailleChunk <= 0) return;
		if (dimensionId == (int)DimensionJeu.Abysse)
		{
			foreach (PointCardinal c in Enum.GetValues(typeof(PointCardinal)))
			{
				Vector3 a = NexusCoords.ObtenirAncreApisara(c);
				_mondeClient.ReserverChunkSpawnPrioritaire(WorldToChunkCoord(a.X, a.Z, TailleChunk));
			}
			return;
		}

		Vector2 xz = ObtenirXZPortailVersApisaraPourDimension(dimensionId);
		_mondeClient.ReserverChunkSpawnPrioritaire(WorldToChunkCoord(xz.X, xz.Y, TailleChunk));
	}
}
