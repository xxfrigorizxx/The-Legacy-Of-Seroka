using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Cablage des serveurs par dimension, distribution/replication reseau des chunks/voxels, commandes admin,
/// transferts de dimension via portail et changement de dimension locale. Partie de <see cref="Gestionnaire_Monde"/>.
/// CONTRAT: noms/signatures RPC, ordre des etapes de changement de dimension et protocole reseau identiques a l'historique.
/// </summary>
public partial class Gestionnaire_Monde : Node3D
{
	private void InitialiserDimensionServeur(Monde_Serveur serveur, int dimensionId)
	{
		if (serveur == null) return;
		var nodeArbres = new Node3D { Name = $"Arbres_{dimensionId}" };
		AddChild(nodeArbres);
		_arbresParDimension[dimensionId] = nodeArbres;
		serveur.Initialiser(
			this,
			nodeArbres,
			(coord, coordChunkY, sections) =>
			{
				if (_dimensionLocaleActive == dimensionId)
					_mondeClient.RecevoirChunkModifie(coord, coordChunkY, sections);
			},
			(coord, donnees) => DistribuerChunkDimensionAuxPeers(dimensionId, coord, donnees),
			(coord, coordChunkY, inventaireFlore) =>
			{
				if (_dimensionLocaleActive == dimensionId)
					_mondeClient.RecevoirFloreModifie(coord, coordChunkY, inventaireFlore);
			},
			(pos, id) =>
			{
				serveur.RepliquerPaddingVoisins(pos, id);
				if (_dimensionLocaleActive == dimensionId)
					_mondeClient.AppliquerVoxel(pos, id);
				if (Multiplayer.IsServer())
					DiffuserVoxelDimension(dimensionId, pos, id);
			},
			(coord) =>
			{
				if (Multiplayer.IsServer())
					DiffuserDestructionChunkDimension(dimensionId, coord);
			},
			ObtenirPositionJoueurOuSpawn,
			() => _dimensionLocaleActive,
			dimensionId
		);
	}

	private void MettreAJourVisibiliteArbresParDimension(int dimensionIdActif)
	{
		foreach (var kv in _arbresParDimension)
		{
			if (kv.Value == null || !GodotObject.IsInstanceValid(kv.Value))
				continue;
			kv.Value.Visible = (kv.Key == dimensionIdActif);
		}
	}

	/// <summary>Seule la dimension visitée simule terrain / eau / décharge ; les autres restent sur disque jusqu'au retour.</summary>
	private void MettreAJourSuspensionServeursDimensions(int dimensionActiveId)
	{
		foreach (var kv in _serveurParDimension)
		{
			if (kv.Value == null || !GodotObject.IsInstanceValid(kv.Value))
				continue;
			kv.Value.DefinirSimulationSuspendue(kv.Key != dimensionActiveId);
		}
	}

	private void SurPeerConnecteDimensions(long peerId)
	{
		DefinirDimensionPeer(peerId, (int)DimensionJeu.Alpha);
	}

	private void SurPeerDeconnecteDimensions(long peerId)
	{
		_dimensionParPeer.Remove(peerId);
		foreach (var kv in _attenteChunksParDimension)
		{
			foreach (var entree in kv.Value)
				entree.Value.Remove(peerId);
		}
	}

	private void SurDemandeChunkDimensionDemandee(int coordX, int coordY, int coordZ, int dimensionId, float obsX, float obsY, float obsZ, long peerId)
	{
		if (!UseArchitectureReseau || !Multiplayer.IsServer()) return;
		Monde_Serveur serveur = ObtenirServeurDimension(dimensionId);
		if (serveur == null) return;
		DefinirDimensionPeer(peerId, dimensionId);
		Vector2I coord = new Vector2I(coordX, coordZ);
		Vector3I coord3D = new Vector3I(coordX, coordY, coordZ);
		if (!_attenteChunksParDimension.TryGetValue(dimensionId, out var attentes))
		{
			attentes = new Dictionary<Vector3I, HashSet<long>>();
			_attenteChunksParDimension[dimensionId] = attentes;
		}
		if (!attentes.TryGetValue(coord3D, out var peers))
		{
			peers = new HashSet<long>();
			attentes[coord3D] = peers;
		}
		peers.Add(peerId);
		serveur.EnregistrerDemandeChunk(coord, coordY, new Vector3(obsX, obsY, obsZ));
	}

	private void DistribuerChunkDimensionAuxPeers(int dimensionId, Vector2I coord, DonneesChunk donnees)
	{
		if (!_attenteChunksParDimension.TryGetValue(dimensionId, out var attentes)) return;
		Vector3I cleExacte = new Vector3I(coord.X, donnees?.CoordChunkY ?? 0, coord.Y);
		HashSet<long> peers = null;
		if (!attentes.TryGetValue(cleExacte, out peers))
			return;
		if (peers == null || peers.Count == 0) return;
		var destinataires = new List<long>(peers);
		attentes.Remove(cleExacte);
		foreach (long peerId in destinataires)
		{
			if (ObtenirDimensionPeer(peerId) != dimensionId)
				continue;
			if (peerId == Multiplayer.GetUniqueId())
			{
				if (_dimensionLocaleActive == dimensionId)
					_mondeClient?.RecevoirDonneesChunk(coord, donnees);
				continue;
			}
			RpcId((int)peerId, nameof(RecevoirChunkDimensionRPC), dimensionId,
				coord.X, donnees?.CoordChunkY ?? 0, coord.Y, donnees.TailleChunk, donnees.HauteurMax,
				donnees.DensitiesQuantifiees ?? Array.Empty<byte>(),
				donnees.MaterialsFlat ?? Array.Empty<byte>(),
				donnees.DensitiesEauQuantifiees ?? Array.Empty<byte>(),
				donnees?.EstVideIntegral ?? false);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RecevoirChunkDimensionRPC(int dimensionId, int coordX, int coordY, int coordZ, int tailleChunk, int hauteurMax, byte[] densitiesPlates, byte[] materialsFlat, byte[] densitiesEauPlates, bool estVideIntegral)
	{
		if (_dimensionLocaleActive != dimensionId || _mondeClient == null) return;
		_mondeClient.RecevoirChunkDuServeurRPC(coordX, coordY, coordZ, tailleChunk, hauteurMax, densitiesPlates, materialsFlat, densitiesEauPlates, estVideIntegral);
	}

	private void DiffuserVoxelDimension(int dimensionId, Vector3I pos, byte id)
	{
		foreach (var kv in _dimensionParPeer)
		{
			long peerId = kv.Key;
			if (kv.Value != dimensionId) continue;
			if (peerId == Multiplayer.GetUniqueId())
				continue;
			_mondeClient?.RpcId((int)peerId, nameof(Monde_Client.AppliquerVoxelRPC), pos.X, pos.Y, pos.Z, (int)id);
		}
	}

	private void DiffuserDestructionChunkDimension(int dimensionId, Vector2I coord)
	{
		foreach (var kv in _dimensionParPeer)
		{
			long peerId = kv.Key;
			if (kv.Value != dimensionId) continue;
			if (peerId == Multiplayer.GetUniqueId())
			{
				if (_dimensionLocaleActive == dimensionId)
					_mondeClient?.OrdonnerDestructionChunkRPC(coord.X, coord.Y);
				continue;
			}
			_mondeClient?.RpcId((int)peerId, nameof(Monde_Client.OrdonnerDestructionChunkRPC), coord.X, coord.Y);
		}
	}

	private Vector3 ObtenirPointTeleportDimension(int dimensionId)
	{
		return ConstantesDimensions.ObtenirInfoOuAlpha(dimensionId).PointTeleportDefaut;
	}

	/// <summary>Retourne la position où réapparaître dans la dimension cible : si une position y a été mémorisée
	/// (visite précédente sauvegardée dans <see cref="_positionsSauvegardeesParDimension"/>), on la réutilise ;
	/// sinon on tombe sur le point canonique de téléportation.</summary>
	private Vector3 ObtenirPointTeleportAvecMemoireDimension(int dimensionId)
	{
		if (_positionsSauvegardeesParDimension.TryGetValue(dimensionId, out Vector3 positionMemorisee))
			return positionMemorisee;
		return ObtenirPointTeleportDimension(dimensionId);
	}

	public bool EnvoyerCommandeAdminChat(string commande)
	{
		if (!UseArchitectureReseau || _networkManager == null) return false;
		string cmd = (commande ?? "").Trim();
		if (string.IsNullOrEmpty(cmd)) return false;
		_networkManager.EnvoyerCommandeAdminAuServeur(cmd);
		return true;
	}

	public bool DemanderInjectionItemCreatif(SlotInventaire slot)
	{
		if (!UseArchitectureReseau || _networkManager == null || slot.EstVide) return false;
		_networkManager.EnvoyerDemandeInjectionItemCreatif(slot);
		return true;
	}

	private void SurCommandeAdminDemandee(string commande, long peerId)
	{
		if (!UseArchitectureReseau || !Multiplayer.IsServer()) return;
		string cmd = (commande ?? "").Trim();
		Monde_Serveur serveurCourant = ObtenirServeurDimension(ObtenirDimensionPeer(peerId)) ?? _mondeServeur;
		if (serveurCourant == null) return;

		if (serveurCourant.EssayerBootstrapAdmin(peerId, cmd, out bool succesBootstrap, out string msgBootstrap))
		{
			if (succesBootstrap)
			{
				SynchroniserPeerAdminToutesDimensions(peerId);
				Monde_Serveur pourPersist = _mondeServeurAlpha ?? serveurCourant;
				pourPersist.PersisterWhitelistAdmin();
			}
			EnvoyerMessageAdminAuPeer(peerId, msgBootstrap);
			return;
		}

		if (cmd.StartsWith("/DIMANASIO", StringComparison.OrdinalIgnoreCase))
		{
			if (!serveurCourant.EstPeerAdmin(peerId))
			{
				EnvoyerMessageAdminAuPeer(peerId, "Accès refusé: vous n'êtes pas admin.");
				return;
			}
			if (string.Equals(cmd, "/DIMANASIO APISARA", StringComparison.OrdinalIgnoreCase))
			{
				_dimensionCoordinator.TransfererPeerVersDimension(peerId, (int)DimensionJeu.Abysse, ObtenirPointTeleportAvecMemoireDimension((int)DimensionJeu.Abysse), $"Transfert vers {ConstantesDimensionAbysse.Apisara}.");
				return;
			}
			if (string.Equals(cmd, "/DIMANASIO ARAPA", StringComparison.OrdinalIgnoreCase))
			{
				_dimensionCoordinator.TransfererPeerVersDimension(peerId, (int)DimensionJeu.Alpha, ObtenirPointTeleportAvecMemoireDimension((int)DimensionJeu.Alpha), $"Retour vers {ConstantesDimensions.NomAlpha}.");
				return;
			}
			if (string.Equals(cmd, "/DIMANASIO PETA", StringComparison.OrdinalIgnoreCase))
			{
				_dimensionCoordinator.TransfererPeerVersDimension(peerId, (int)DimensionJeu.Beta, ObtenirPointTeleportAvecMemoireDimension((int)DimensionJeu.Beta), $"Transfert vers {ConstantesDimensions.NomBeta}.");
				return;
			}
			if (string.Equals(cmd, "/DIMANASIO OMEGA", StringComparison.OrdinalIgnoreCase))
			{
				_dimensionCoordinator.TransfererPeerVersDimension(peerId, (int)DimensionJeu.Omega, ObtenirPointTeleportAvecMemoireDimension((int)DimensionJeu.Omega), $"Transfert vers {ConstantesDimensions.NomOmega}.");
				return;
			}
			if (string.Equals(cmd, "/DIMANASIO DERATA", StringComparison.OrdinalIgnoreCase))
			{
				_dimensionCoordinator.TransfererPeerVersDimension(peerId, (int)DimensionJeu.Delta, ObtenirPointTeleportAvecMemoireDimension((int)DimensionJeu.Delta), $"Transfert vers {ConstantesDimensions.NomDelta}.");
				return;
			}
			EnvoyerMessageAdminAuPeer(peerId, "Commande dimension inconnue.");
			return;
		}

		if (!serveurCourant.EssayerTraiterCommandeAdmin(peerId, commande, out bool modeCreatif, out bool noclip, out string messageServeur))
		{
			if (!string.IsNullOrWhiteSpace(messageServeur))
				EnvoyerMessageAdminAuPeer(peerId, messageServeur);
			return;
		}

		if (peerId == Multiplayer.GetUniqueId())
			AppliquerEtatModeCreatifLocal(modeCreatif, noclip, messageServeur);
		else
		{
			RpcId((int)peerId, nameof(RecevoirEtatModeCreatifRPC), modeCreatif ? 1 : 0, noclip ? 1 : 0, messageServeur ?? "");
		}
	}

	/// <summary>Réplique l’ID admin sur chaque <see cref="Monde_Serveur"/> (dimensions distinctes, même fichier whitelist).</summary>
	private void SynchroniserPeerAdminToutesDimensions(long peerId)
	{
		foreach (var kv in _serveurParDimension)
			kv.Value?.AjouterPeerAdmin(peerId);
	}

	private void EnvoyerMessageAdminAuPeer(long peerId, string message)
	{
		if (string.IsNullOrWhiteSpace(message)) return;
		if (peerId == Multiplayer.GetUniqueId())
			Joueur.AlerteSqueletteBoiteNoire(message);
		else
			RpcId((int)peerId, nameof(RecevoirMessageChatAdminRPC), message ?? "");
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RecevoirMessageChatAdminRPC(string message)
	{
		if (!string.IsNullOrWhiteSpace(message))
			Joueur.AlerteSqueletteBoiteNoire(message);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RecevoirEtatModeCreatifRPC(int modeCreatif, int noclip, string messageServeur)
	{
		AppliquerEtatModeCreatifLocal(modeCreatif != 0, noclip != 0, messageServeur);
	}

	private void AppliquerEtatModeCreatifLocal(bool actif, bool noclip, string messageServeur)
	{
		if (_joueur is Joueur j)
			j.DefinirModeCreatifDepuisServeur(actif, noclip);
		if (!string.IsNullOrWhiteSpace(messageServeur))
			Joueur.AlerteSqueletteBoiteNoire(messageServeur);
	}

	private static bool EstGenomeVoxelTerrainValide(string genome)
	{
		if (string.IsNullOrWhiteSpace(genome)) return false;
		string g = genome.Trim();
		if (!g.StartsWith("VOXEL_TERRAIN:", StringComparison.OrdinalIgnoreCase))
			return false;
		string brut = g.Substring("VOXEL_TERRAIN:".Length).Trim();
		if (!int.TryParse(brut, out int idVoxel))
			return false;
		return (idVoxel >= 10 && idVoxel <= 29) || (idVoxel >= 32 && idVoxel <= 48);
	}

	private void SurInjectionItemCreatifDemandee(int id, int indexMorphologique, int indexChimique, int indexTaille, int indexBotanique, string genomeAssemblage, long peerId)
	{
		if (!UseArchitectureReseau || !Multiplayer.IsServer()) return;
		Monde_Serveur serveurCourant = ObtenirServeurDimension(ObtenirDimensionPeer(peerId)) ?? _mondeServeur;
		if (serveurCourant == null) return;
		if (!serveurCourant.EssayerConstruireSlotInjectionCreatif(peerId, id, indexMorphologique, indexChimique, indexTaille, indexBotanique, out SlotInventaire slot, out string messageServeur))
		{
			if (!string.IsNullOrWhiteSpace(messageServeur))
				EnvoyerMessageAdminAuPeer(peerId, messageServeur);
			return;
		}

		// Préserve le tag voxel terrain pour les entrées créatives "proxy ID 2".
		// Sans ce champ, le client reçoit une pierre standard et la pose de minerais devient impossible.
		if (EstGenomeVoxelTerrainValide(genomeAssemblage))
			slot.GenomeAssemblage = genomeAssemblage.Trim();

		if (peerId == Multiplayer.GetUniqueId())
			AppliquerInjectionItemCreatifLocale(slot, messageServeur);
		else
		{
			RpcId((int)peerId, nameof(RecevoirInjectionItemCreatifRPC),
				slot.ID, slot.IndexMorphologique, slot.IndexChimique, slot.IndexTaille, (int)slot.IndexBotanique,
				slot.IndexTailleLameRoche, slot.Quantite, slot.GenomeAssemblage ?? "", messageServeur ?? "");
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RecevoirInjectionItemCreatifRPC(int id, int indexMorphologique, int indexChimique, int indexTaille, int indexBotanique, int indexTailleLameRoche, int quantite, string genomeAssemblage, string messageServeur)
	{
		SlotInventaire slot = new SlotInventaire
		{
			ID = id,
			IndexMorphologique = indexMorphologique,
			IndexChimique = indexChimique,
			IndexTaille = indexTaille,
			IndexBotanique = (byte)Mathf.Clamp(indexBotanique, 0, 255),
			IndexTailleLameRoche = indexTailleLameRoche,
			GenomeAssemblage = EstGenomeVoxelTerrainValide(genomeAssemblage) ? genomeAssemblage.Trim() : "",
			Quantite = quantite
		};
		AppliquerInjectionItemCreatifLocale(slot, messageServeur);
	}

	private void AppliquerInjectionItemCreatifLocale(SlotInventaire slot, string messageServeur)
	{
		if (_joueur is Joueur j)
			j.InjecterSlotCreatifAdmin(slot);
		if (!string.IsNullOrWhiteSpace(messageServeur))
			Joueur.AlerteSqueletteBoiteNoire(messageServeur);
	}

	private void TransfererPeerVersDimension(long peerId, int dimensionCible, Vector3 positionCible, string messageServeur)
	{
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;
		int dimensionActuelle = ObtenirDimensionPeer(peerId);
		if (peerId == Multiplayer.GetUniqueId())
		{
			Vector3 positionAvantTp = JoueurReferenceValide() ? _joueur.GlobalPosition : positionCible;
			GameState.Instance?.SauvegarderPositionJoueur(positionAvantTp);
			// Mémorise la position actuelle dans la dim qu'on quitte (clé = dimensionActuelle).
			_positionsSauvegardeesParDimension[dimensionActuelle] = positionAvantTp;
			SauvegarderSessionJoueur(dimensionActuelle, positionAvantTp);
			if (_joueur is Joueur j && ConstantesDimensions.EssayerObtenirInfo(dimensionActuelle, out var infoCourante))
				SauvegarderPersistanceCompleteMonde($"TransfererPeer.quit.{infoCourante.NomCanonique}");
			else if (_joueur is Joueur jFallback)
				jFallback.SauvegarderEtatPersistantJoueurSeulement();
		}
		DefinirDimensionPeer(peerId, dimensionCible);
		if (peerId == Multiplayer.GetUniqueId())
		{
			_dimensionCoordinator.AppliquerChangementDimensionLocale(dimensionCible, positionCible, messageServeur);
			return;
		}
		RpcId((int)peerId, nameof(RecevoirTransfertDimensionRPC), dimensionCible, positionCible.X, positionCible.Y, positionCible.Z, messageServeur ?? "");
	}

	/// <summary>Peer réseau associé au nœud joueur (autorité), ou l’identifiant local en solo.</summary>
	public long ObtenirPeerIdPourNoeudJoueur(Joueur j)
	{
		if (j == null) return 1;
		if (!Multiplayer.HasMultiplayerPeer())
			return Multiplayer.GetUniqueId();
		int auth = j.GetMultiplayerAuthority();
		if (auth >= 0)
			return auth;
		return Multiplayer.GetUniqueId();
	}

	/// <summary>Remblai voxel sous un portail (serveur / solo) : uniquement l’air entre le sol existant et les pieds, sur une profondeur max (pas de colonne pleine).</summary>
	public void DemanderRemplissageSocleSousPortail(Vector3 centrePortailMonde, int dimensionId, float ySurfaceTerrain, int rayonDemiCoteVoxels, int profondeurMaxVersLeBas)
	{
		if (!UseArchitectureReseau)
			return;
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer())
			return;
		Monde_Serveur serveur = ObtenirServeurDimension(dimensionId);
		serveur?.RemplirSocleSousPortail(centrePortailMonde, ySurfaceTerrain, rayonDemiCoteVoxels, profondeurMaxVersLeBas);
	}

	/// <summary>Transfert déclenché par un <see cref="Portail"/> : dimension cible + XZ logique (Y affiné par raycast vertical après coup).</summary>
	public void TransfererJoueurViaPortail(Node3D joueur, int dimensionIdCible, Vector3 positionCibleXZ, string messageServeur = null)
	{
		if (joueur is not Joueur j || !GodotObject.IsInstanceValid(j)) return;
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;

		float yRef = ConstantesDimensions.ObtenirInfoOuAlpha(dimensionIdCible).PointTeleportDefaut.Y;
		var posInitiale = new Vector3(positionCibleXZ.X, yRef, positionCibleXZ.Z);
		long peerId = ObtenirPeerIdPourNoeudJoueur(j);
		_dimensionCoordinator.TransfererPeerVersDimension(peerId, dimensionIdCible, posInitiale, messageServeur ?? "Transit dimensionnel.");
		if (peerId == Multiplayer.GetUniqueId())
		{
			float ax = positionCibleXZ.X;
			float az = positionCibleXZ.Z;
			Callable.From(() => AlignerJoueurPortailSurSolDeferred(ax, az)).CallDeferred();
		}
	}

	private void AlignerJoueurPortailSurSolDeferred(float mondeX, float mondeZ, int tentative = 0)
	{
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur)) return;
		var approx = new Vector3(mondeX, 0f, mondeZ);
		if (EssayerTrouverSolParRaycast(approx, out Vector3 pointSol))
		{
			// Même règle que <see cref="FinaliserSpawnInitialAuSol"/> : pieds sur la surface du raycast, sans décalage arbitraire.
			if (_joueur is Joueur jo)
				_joueur.GlobalPosition = new Vector3(mondeX, jo.CalculerYOriginePourPiedsSurSurface(pointSol.Y), mondeZ);
			else
				_joueur.GlobalPosition = pointSol + Vector3.Up * 1.2f;
			_joueur.Velocity = Vector3.Zero;
			FinaliserSortiePortailApresAlignement();
			return;
		}
		// Tant que la collision n’est pas prête : même repli hauteur voxel qu’au spawn initial (évite rester sous le portail / dans le vide).
		Vector3 repliTerrain = AssurerSpawnAuDessusDuSol(new Vector3(mondeX, ConstantesDimensions.ObtenirInfoOuAlpha(_dimensionLocaleActive).PointTeleportDefaut.Y, mondeZ));
		_joueur.GlobalPosition = repliTerrain;
		_joueur.Velocity = Vector3.Zero;
		if (tentative < 18 && GetTree() != null)
		{
			float delai = 0.12f + tentative * 0.07f;
			GetTree().CreateTimer(delai).Timeout += () => AlignerJoueurPortailSurSolDeferred(mondeX, mondeZ, tentative + 1);
			return;
		}
		GD.PushWarning("ZERO-K Portail : raycast sol sans impact après attente, position hauteur voxel conservée.");
	}

	private void FinaliserSortiePortailApresAlignement()
	{
		// Dès qu'un alignement sol est validé, on relâche immédiatement le verrou de TP.
		_gateTpDimensionActif = false;
		_secondesGateTpDimension = 0.0;
		EjecterJoueurHorsMembranePortailSiNecessaire();
	}

	private void EjecterJoueurHorsMembranePortailSiNecessaire()
	{
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur))
			return;
		if (!_racineParDimension.TryGetValue(_dimensionLocaleActive, out Node3D racine) || racine == null)
			return;

		Portail portailProche = null;
		float meilleureDistance2 = float.MaxValue;
		foreach (Node enfant in racine.GetChildren())
		{
			if (enfant is not Portail p || !GodotObject.IsInstanceValid(p))
				continue;
			Vector3 centreMembrane = p.GlobalTransform * p.PositionLocaleMembrane;
			float d2 = centreMembrane.DistanceSquaredTo(_joueur.GlobalPosition);
			if (d2 < meilleureDistance2)
			{
				meilleureDistance2 = d2;
				portailProche = p;
			}
		}

		if (portailProche == null)
			return;

		Vector3 centre = portailProche.GlobalTransform * portailProche.PositionLocaleMembrane;
		Vector2 deltaJoueur = new Vector2(_joueur.GlobalPosition.X - centre.X, _joueur.GlobalPosition.Z - centre.Z);
		float rayonSecurite = Mathf.Max(6f, portailProche.RayonTriggerMetres * 0.95f);
		if (deltaJoueur.LengthSquared() > rayonSecurite * rayonSecurite)
			return;

		Vector3 axeSortie3 = portailProche.GlobalTransform.Basis.Z;
		Vector2 axeSortie = new Vector2(axeSortie3.X, axeSortie3.Z);
		if (axeSortie.LengthSquared() < 1e-6f)
			axeSortie = Vector2.Right;
		else
			axeSortie = axeSortie.Normalized();

		float signe = Mathf.Sign(deltaJoueur.Dot(axeSortie));
		if (Mathf.IsZeroApprox(signe))
			signe = 1f;

		float distanceSortie = Mathf.Max(22f, Mathf.Max(portailProche.DistanceApparitionDevantPortailMetres, portailProche.RayonTriggerMetres + 6f));
		Vector3 xzCible = new Vector3(
			centre.X + axeSortie.X * distanceSortie * signe,
			_joueur.GlobalPosition.Y,
			centre.Z + axeSortie.Y * distanceSortie * signe);

		if (EssayerTrouverSolParRaycast(new Vector3(xzCible.X, 0f, xzCible.Z), out Vector3 pointSol))
		{
			if (_joueur is Joueur jo)
				_joueur.GlobalPosition = new Vector3(xzCible.X, jo.CalculerYOriginePourPiedsSurSurface(pointSol.Y), xzCible.Z);
			else
				_joueur.GlobalPosition = pointSol + Vector3.Up * 1.2f;
		}
		else
		{
			_joueur.GlobalPosition = AssurerSpawnAuDessusDuSol(new Vector3(xzCible.X, _joueur.GlobalPosition.Y, xzCible.Z));
		}

		_joueur.Velocity = Vector3.Zero;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RecevoirTransfertDimensionRPC(int dimensionId, float posX, float posY, float posZ, string messageServeur)
	{
		_dimensionCoordinator.AppliquerChangementDimensionLocale(dimensionId, new Vector3(posX, posY, posZ), messageServeur);
		Callable.From(() => AlignerJoueurPortailSurSolDeferred(posX, posZ)).CallDeferred();
	}

	private void AppliquerChangementDimensionLocale(int dimensionId, Vector3 positionCible, string messageServeur, bool rechargerPersistanceDimension = true)
	{
		_dimensionLocaleActive = dimensionId;
		DefinirDimensionPeer(Multiplayer.GetUniqueId(), dimensionId);
		_mondeServeur = ObtenirServeurDimension(dimensionId) ?? _mondeServeurAlpha;
		MettreAJourSuspensionServeursDimensions(dimensionId);
		_mondeServeur?.ForcerPulseReveilPierres();
		_mondeClient?.DefinirDimensionReseauActive(dimensionId);
		_positionReferenceTransfertDimension = positionCible;
		_gateTpDimensionActif = true;
		_secondesGateTpDimension = 0.0;
		_cooldownPulseReveilPierresTp = 0.0;
		MarquerPortailsDimensionPourRealignementSol(dimensionId);
		PrioriserChunksClientAutourPortailsDimension(dimensionId);
		ReinitialiserEmerukedesiParotaromaStage1();
		MettreAJourVisibiliteArbresParDimension(dimensionId);
		MettreAJourVisibilitePortailsParDimension(dimensionId);
		if (_joueur != null && GodotObject.IsInstanceValid(_joueur))
		{
			ReparenterNoeudDansDimension(_joueur, dimensionId, positionCible);
			_joueur.Velocity = Vector3.Zero;
		}
		_mondeClient?.ReinitialiserTousLesChunksLocaux();
		// Après reset des chunks : respawn objets/faune (portail). Au boot hors Alpha, la phase B le fait quand le sol est prêt.
		if (rechargerPersistanceDimension && _joueur is Joueur jDiffere)
			jDiffere.CallDeferred(Joueur.NomMethodeRechargerPersistanceDimensionDifferee);
		_chargementAbysseEnCours = dimensionId == (int)DimensionJeu.Abysse;
		_chargementAbysseEnCours = false; // Abysse suit le chargement Alpha (pas de verrou dédié).
		_secondesStabiliteAbyssePret = 0.0;
		_secondesVerrouAbysse = 0.0;
		_cooldownRearmementVerrouAbysse = 0.0;
		_verrouMarcheAbysseActif = false;
		_secondesVerrouMarcheAbysse = 0.0;
		_secondesStabiliteMarcheAbysse = 0.0;
		if (_overlayChargement != null)
		{
			_overlayChargement.Visible = true;
			_secondesOverlayChargement = 0.0;
		}
		if (_labelChargementPrincipal != null)
			_labelChargementPrincipal.Text = "Chargement du monde...";
		if (_mondeClient != null)
		{
			Vector2I chunkSpawn = WorldToChunkCoord(positionCible, TailleChunk);
			_mondeClient.ReserverChunkSpawnPrioritaire(chunkSpawn);
		}
		EnvoyerFuseauHoraireAuPeer(Multiplayer.GetUniqueId());
		MettreAJourAtmosphereAbysseLocale(dimensionId);
		if (!string.IsNullOrWhiteSpace(messageServeur))
			Joueur.AlerteSqueletteBoiteNoire(messageServeur);
	}

	private void MettreAJourAtmosphereAbysseLocale(int dimensionIdActif)
	{
		if (_mondeServeurAbysse is not Gestionnaire_Abysse gestionnaireAbysse)
			return;

		bool apisara = dimensionIdActif == (int)DimensionJeu.Abysse;
		gestionnaireAbysse.DefinirAtmosphereAbysseActive(apisara);

		var we = GetParent()?.GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
		if (we == null)
			return;

		Godot.Environment envApisara = gestionnaireAbysse.ObtenirEnvironmentAbysse();
		if (apisara)
		{
			if (_environnementSauvegardeHorsApisara == null && we.Environment != null && !ReferenceEquals(we.Environment, envApisara))
				_environnementSauvegardeHorsApisara = we.Environment;
			we.Environment = envApisara;
		}
		else if (_environnementSauvegardeHorsApisara != null)
		{
			we.Environment = _environnementSauvegardeHorsApisara;
		}
	}

	/// <summary>Volume océan dédié à la détection (remous/éclaboussures), sans override physique global.</summary>
	private void EnvoyerFuseauHoraireAuPeer(long peerId)
	{
		if (!Multiplayer.IsServer()) return;
		var soleil = GetParent()?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		if (soleil == null) return;
		int dimension = ObtenirDimensionPeer(peerId);
		double offset = ObtenirServeurDimension(dimension)?.FuseauHoraireHeures ?? 0.0;
		soleil.RpcId(peerId, nameof(Cycle_Solaire.DefinirDecalageHoraire), offset);
		bool forcerJour = dimension == (int)DimensionJeu.Abysse;
		soleil.RpcId(peerId, nameof(Cycle_Solaire.ConfigurerHeureFixeDimension), forcerJour ? 1 : 0, 13.5);
	}
}
