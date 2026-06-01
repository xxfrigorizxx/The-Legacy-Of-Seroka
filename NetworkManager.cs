using Godot;
using System;

/// <summary>Gestionnaire ENet pour Solo (Host local) et MMORPG. Héberger/Solo = serveur UDP (25565 par défaut) + client local.</summary>
public partial class NetworkManager : Node
{
	public const ushort PortServeur = 25565;
	private const int NombrePortsEssai = 16;

	private MultiplayerPeer _peer;
	private bool _estServeur;

	/// <summary>Port UDP réellement ouvert si ENet a réussi ; null si mode hors-ligne.</summary>
	public ushort? PortEnecoute { get; private set; }

	/// <summary>Vrai si un hôte ENet UDP est actif ; faux si <see cref="OfflineMultiplayerPeer"/> (pas de socket).</summary>
	public bool ENetActif { get; private set; }

	public bool EstServeur => _estServeur;
	public bool EstConnecte => Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

	/// <summary>Démarre en mode Héberger/Solo : tente plusieurs ports puis bascule hors-ligne si ENet est impossible.</summary>
	public void DemarrerHostSolo()
	{
		_peer?.Dispose();
		_peer = null;
		PortEnecoute = null;
		ENetActif = false;

		ushort portEssai = PortServeur;
		ENetMultiplayerPeer enet = null;
		Error err = Error.Failed;

		for (int i = 0; i < NombrePortsEssai; i++)
		{
			enet?.Dispose();
			enet = new ENetMultiplayerPeer();
			try
			{
				err = enet.CreateServer(portEssai);
			}
			catch (Exception ex)
			{
				err = Error.CantCreate;
				GD.PrintErr($"NetworkManager: CreateServer exception sur le port {portEssai}: {ex.Message}");
			}
			if (err == Error.Ok)
			{
				_peer = enet;
				PortEnecoute = portEssai;
				ENetActif = true;
				break;
			}

			GD.PrintErr($"NetworkManager: impossible d'ouvrir le port {portEssai} pour ENet ({err}). Cause fréquente : port déjà utilisé (autre instance du jeu, Minecraft Java, etc.).");
			// Si ENet n'arrive même pas à créer l'hôte, inutile d'insister sur 16 ports.
			if (err == Error.CantCreate)
				break;
			if (portEssai < ushort.MaxValue)
				portEssai++;
		}

		if (_peer == null)
		{
			enet?.Dispose();
			GD.PrintErr("NetworkManager: aucun port ENet libre après plusieurs essais. Basculement en multijoueur hors-ligne (solo sans UDP ; les RPC locaux restent disponibles).");
			_peer = new OfflineMultiplayerPeer();
		}

		Multiplayer.MultiplayerPeer = _peer;
		_estServeur = true;

		_peer.PeerConnected += (id) => GD.Print($"Client connecté: {id}");
		_peer.PeerDisconnected += (id) => GD.Print($"Client déconnecté: {id}");

		if (ENetActif)
			GD.Print($"NetworkManager: serveur ENet démarré (Solo/Host) sur le port {PortEnecoute}.");
		else
			GD.Print("NetworkManager: solo en mode OfflineMultiplayerPeer (aucune socket réseau).");
	}

	/// <summary>Connecte le client à une adresse. Pour rejoindre un serveur distant.</summary>
	public void ConnecterClient(string adresse = "127.0.0.1")
	{
		_peer?.Dispose();
		var enet = new ENetMultiplayerPeer();
		Error err = enet.CreateClient(adresse, PortServeur);
		if (err != Error.Ok)
		{
			GD.PrintErr($"NetworkManager: connexion client échouée ({err})");
			enet.Dispose();
			return;
		}
		_peer = enet;
		Multiplayer.MultiplayerPeer = _peer;
		_estServeur = false;
		ENetActif = true;
		PortEnecoute = null;
	}
	/// <summary>RPC appelé par le client pour demander la destruction d'un bloc.</summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void DemanderDestructionBloc(Vector3I pos, float rayon)
	{
		int idAppelant = Multiplayer.GetRemoteSenderId();
		// Le serveur traite et répond par ActualiserBlocClient
		EmitSignal(SignalName.DestructionDemandee, pos, rayon, idAppelant);
	}

	/// <summary>RPC envoyé par le serveur pour notifier les clients d'un changement de bloc.</summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	public void ActualiserBlocClient(Vector3I pos, int nouvelId)
	{
		EmitSignal(SignalName.BlocActualise, pos, nouvelId);
	}

	/// <summary>Client -> serveur : commande admin texte brute (validation stricte côté serveur uniquement).</summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SoumettreCommandeAdmin(string commande)
	{
		long idAppelant = Multiplayer.GetRemoteSenderId();
		EmitSignal(SignalName.CommandeAdminDemandee, commande ?? "", idAppelant);
	}

	public void EnvoyerCommandeAdminAuServeur(string commande)
	{
		string cmd = (commande ?? "").Trim();
		if (string.IsNullOrEmpty(cmd)) return;
		// Peer 1 = autorité serveur dans ce projet (solo host et serveur dédié).
		RpcId(1, nameof(SoumettreCommandeAdmin), cmd);
	}

	/// <summary>Client -> serveur : demande d'injection d'objet créatif (validée côté autorité).</summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void DemanderInjectionItemCreatif(int id, int indexMorphologique, int indexChimique, int indexTaille, int indexBotanique, string genomeAssemblage = "")
	{
		long idAppelant = Multiplayer.GetRemoteSenderId();
		EmitSignal(SignalName.InjectionItemCreatifDemandee, id, indexMorphologique, indexChimique, indexTaille, indexBotanique, genomeAssemblage ?? "", idAppelant);
	}

	/// <summary>Client -> serveur : demande explicite de chunk dans une dimension donnée.</summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void DemanderChunkDimension(int coordX, int coordY, int coordZ, int dimensionId, float obsX, float obsY, float obsZ)
	{
		long idAppelant = Multiplayer.GetRemoteSenderId();
		EmitSignal(SignalName.DemandeChunkDimensionDemandee, coordX, coordY, coordZ, dimensionId, obsX, obsY, obsZ, idAppelant);
	}

	public void EnvoyerDemandeInjectionItemCreatif(SlotInventaire slot)
	{
		if (slot.EstVide) return;
		RpcId(1, nameof(DemanderInjectionItemCreatif), slot.ID, slot.IndexMorphologique, slot.IndexChimique, slot.IndexTaille, (int)slot.IndexBotanique, slot.GenomeAssemblage ?? "");
	}

	public void EnvoyerDemandeChunkDimensionAuServeur(Vector2I coord, int coordY, int dimensionId, Vector3 positionObservation)
	{
		RpcId(1, nameof(DemanderChunkDimension), coord.X, coordY, coord.Y, dimensionId, positionObservation.X, positionObservation.Y, positionObservation.Z);
	}

	[Signal]
	public delegate void DestructionDemandeeEventHandler(Vector3I pos, float rayon, long peerId);

	[Signal]
	public delegate void BlocActualiseEventHandler(Vector3I pos, int nouvelId);

	[Signal]
	public delegate void CommandeAdminDemandeeEventHandler(string commande, long peerId);

	[Signal]
	public delegate void InjectionItemCreatifDemandeeEventHandler(int id, int indexMorphologique, int indexChimique, int indexTaille, int indexBotanique, string genomeAssemblage, long peerId);

	[Signal]
	public delegate void DemandeChunkDimensionDemandeeEventHandler(int coordX, int coordY, int coordZ, int dimensionId, float obsX, float obsY, float obsZ, long peerId);
}
