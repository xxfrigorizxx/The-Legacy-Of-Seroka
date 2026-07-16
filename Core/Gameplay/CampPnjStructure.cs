using Godot;
using System.Collections.Generic;

/// <summary>
/// Camp matérialisé : point de réunion, décharge baies bonnes (au soleil), décharge toxiques, zone repas commune.
/// Donne aux PNJ un « quoi faire en premier » après l'établissement du camp.
/// </summary>
public sealed class CampPnjStructure
{
	private const float DistStockBon = 6.5f;
	private const float DistStockMauvais = 5.5f;
	private const float DistRepas = 3.8f;
	private const float DistStockRoches = 4.6f;
	private const int MaxBaiesVisuellesParZone = 96;
	/// <summary>Rayon autour du centre de zone stock : une baie au-delà n'est plus comptée (perdue du stock).</summary>
	private const float RayonStockBaiesBon = 5.2f;
	private const float RayonStockBaiesMauvais = 4.6f;
	private const float RayonScanBaiesAutourCamp = 28f;
	private static float _cooldownReconcilGlobal;

	public Vector2 Ancre { get; private set; }
	public Vector2 ZoneReunion { get; private set; }
	public Vector2 ZoneStockBon { get; private set; }
	public Vector2 ZoneStockMauvais { get; private set; }
	public Vector2 ZoneRepas { get; private set; }
	public Vector2 ZoneStockRoches { get; private set; }

	private readonly Dictionary<int, int> _stockBonParCouleur = new();
	private readonly Dictionary<int, int> _stockMauvaisParCouleur = new();
	private readonly Dictionary<int, int> _stockRochesParMatiere = new();
	private int _baiesVisuellesBon;
	private int _baiesVisuellesMauvais;
	private Node3D _racineMarqueurs;
	private bool _initialise;
	private Label3D _labelStockBon;
	private Label3D _labelStockMauvais;
	private Label3D _labelStockRoches;

	private int _objectifReserveBaies;
	private int _baiesDeposeesReserve;

	public bool DoitRemplirReserveColonie => _objectifReserveBaies > 0 && _baiesDeposeesReserve < _objectifReserveBaies;
	public int ObjectifReserveBaies => _objectifReserveBaies;
	public int BaiesDeposeesReserve => _baiesDeposeesReserve;

	public void DefinirObjectifReserve(int quantite)
	{
		_objectifReserveBaies = Mathf.Max(8, quantite);
		_baiesDeposeesReserve = 0;
	}

	public void NotifierBaieDeposeeReserve(bool comestibleConnue, int quantite = 1)
	{
		if (!comestibleConnue || quantite <= 0 || _objectifReserveBaies <= 0)
			return;
		_baiesDeposeesReserve = Mathf.Min(_objectifReserveBaies, _baiesDeposeesReserve + quantite);
		if (_baiesDeposeesReserve >= _objectifReserveBaies)
			_objectifReserveBaies = 0;
	}

	public bool EstInitialise => _initialise;

	/// <summary>Rayon minimal entre deux camps (société ou rebelle).</summary>
	public const float RayonExclusionEntreCamps = 18f;

	private static readonly List<Vector2> _ancresEnregistrees = new();

	public static bool EstEmplacementLibre(Vector2 ancre, float marge = 0f)
	{
		float rayon = RayonExclusionEntreCamps + marge;
		foreach (Vector2 existant in _ancresEnregistrees)
		{
			if (existant.DistanceTo(ancre) < rayon)
				return false;
		}
		return true;
	}

	public static void EnregistrerAncre(Vector2 ancre)
	{
		if (_ancresEnregistrees.Exists(a => a.DistanceTo(ancre) < 0.5f))
			return;
		_ancresEnregistrees.Add(ancre);
	}

	public static void RetirerAncre(Vector2 ancre)
	{
		for (int i = _ancresEnregistrees.Count - 1; i >= 0; i--)
		{
			if (_ancresEnregistrees[i].DistanceTo(ancre) < 0.5f)
				_ancresEnregistrees.RemoveAt(i);
		}
	}

	public static CampPnjStructure Creer(Vector2 ancre, int seedTerrain)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(Mathf.Abs(ancre.X) * 131f + Mathf.Abs(ancre.Y) * 719f + seedTerrain);
		float angleSoleil = rng.RandfRange(-0.35f, 0.35f);
		Vector2 dirSoleil = new Vector2(Mathf.Cos(angleSoleil), Mathf.Sin(angleSoleil)).Normalized();
		Vector2 perp = new Vector2(-dirSoleil.Y, dirSoleil.X);
		var s = new CampPnjStructure
		{
			Ancre = ancre,
			ZoneReunion = ancre,
			ZoneStockBon = ancre + dirSoleil * DistStockBon,
			ZoneStockMauvais = ancre - dirSoleil * DistStockMauvais + perp * 3.2f,
			ZoneRepas = ancre + dirSoleil * DistRepas + perp * 1.8f,
			ZoneStockRoches = ancre - perp * DistStockRoches + dirSoleil * 1.2f,
			_initialise = true
		};
		return s;
	}

	public void MaterialiserMarqueurs(Gestionnaire_Monde gm, int seedTerrain)
	{
		if (gm == null || !GodotObject.IsInstanceValid(gm))
			return;
		if (_racineMarqueurs != null && GodotObject.IsInstanceValid(_racineMarqueurs))
			return;

		_racineMarqueurs = new Node3D { Name = $"CampPnj_{Ancre.X:0}_{Ancre.Y:0}" };
		gm.AddChild(_racineMarqueurs);

		CreerMarqueur(gm, seedTerrain, ZoneReunion, "Reunion", new Color(0.9f, 0.95f, 1f));
		_labelStockBon = CreerMarqueur(gm, seedTerrain, ZoneStockBon, "Baies bonnes (vide)", new Color(0.45f, 1f, 0.5f));
		_labelStockMauvais = CreerMarqueur(gm, seedTerrain, ZoneStockMauvais, "Baies toxiques (vide)", new Color(1f, 0.45f, 0.4f));
		CreerMarqueur(gm, seedTerrain, ZoneRepas, "Zone repas", new Color(1f, 0.88f, 0.35f));
		_labelStockRoches = CreerMarqueur(gm, seedTerrain, ZoneStockRoches, "Roches (vide)", new Color(0.75f, 0.75f, 0.8f));
		RafraichirLabelsStock();
		if (TotalStockComestible() > 0 || TotalStockToxique() > 0)
			SynchroniserBaiesPhysiquesStock(gm, seedTerrain);
	}

	private Label3D CreerMarqueur(Gestionnaire_Monde gm, int seed, Vector2 xz, string texte, Color couleur)
	{
		float y = PnjHumainBiomeInstinct.HauteurSolMonde(xz.X, xz.Y, seed);
		var label = new Label3D
		{
			Text = texte,
			FontSize = 22,
			PixelSize = 0.0045f,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			Modulate = couleur,
			Position = new Vector3(xz.X, y + 1.6f, xz.Y)
		};
		_racineMarqueurs.AddChild(label);
		return label;
	}

	private void RafraichirLabelsStock()
	{
		if (_labelStockBon != null && GodotObject.IsInstanceValid(_labelStockBon))
		{
			int n = TotalStockComestible();
			_labelStockBon.Text = n > 0 ? $"Baies bonnes ({n})" : "Baies bonnes (vide)";
		}
		if (_labelStockMauvais != null && GodotObject.IsInstanceValid(_labelStockMauvais))
		{
			int n = TotalStockToxique();
			_labelStockMauvais.Text = n > 0 ? $"Baies toxiques ({n})" : "Baies toxiques (vide)";
		}
		if (_labelStockRoches != null && GodotObject.IsInstanceValid(_labelStockRoches))
		{
			int n = TotalStockRoches();
			_labelStockRoches.Text = n > 0 ? $"Roches ({n})" : "Roches (vide)";
		}
	}

	public void DeposerRoche(int indexMatiere, int quantite = 1)
	{
		if (quantite <= 0)
			return;
		_stockRochesParMatiere[indexMatiere] = _stockRochesParMatiere.GetValueOrDefault(indexMatiere, 0) + quantite;
		RafraichirLabelsStock();
	}

	public int TotalStockRoches()
	{
		int t = 0;
		foreach (KeyValuePair<int, int> kv in _stockRochesParMatiere)
			t += kv.Value;
		return t;
	}

	/// <summary>Calcule une position au sol dans la zone stock si la limite visuelle n'est pas atteinte.</summary>
	public bool EssayerPositionPoseBaieStock(bool comestibleConnue, int seedTerrain, int indexVisuel, out Vector3 positionPose)
	{
		positionPose = default;
		if (indexVisuel >= MaxBaiesVisuellesParZone)
			return false;
		Vector2 zone = comestibleConnue ? ZoneStockBon : ZoneStockMauvais;
		int anneau = indexVisuel / 10;
		int slot = indexVisuel % 10;
		float dist = 0.32f + anneau * 0.42f;
		float angle = slot * (Mathf.Tau / 10f) + anneau * 0.37f;
		float x = zone.X + Mathf.Cos(angle) * dist;
		float z = zone.Y + Mathf.Sin(angle) * dist;
		float y = PnjHumainBiomeInstinct.HauteurSolMonde(x, z, seedTerrain);
		positionPose = new Vector3(x, y + 0.08f, z);
		return true;
	}

	/// <summary>Point d'approche pour déposer sans pousser la pile (le PNJ s'arrête avant le tas).</summary>
	public bool EssayerPointApprocheDepotBaie(Gestionnaire_Monde gm, bool comestible, int seedTerrain, out Vector3 pointApproche, out Vector3 positionPose)
	{
		pointApproche = default;
		positionPose = default;
		int indexVisuel = CompterBaiesPhysiquesGroupe(gm, comestible);
		if (!EssayerPositionPoseBaieStock(comestible, seedTerrain, indexVisuel, out positionPose))
			return false;
		Vector2 zone = comestible ? ZoneStockBon : ZoneStockMauvais;
		Vector3 centreZone = new Vector3(zone.X, positionPose.Y, zone.Y);
		Vector3 versExterieur = positionPose - centreZone;
		versExterieur.Y = 0f;
		if (versExterieur.LengthSquared() < 0.04f)
			versExterieur = new Vector3(1f, 0f, 0f);
		pointApproche = positionPose + versExterieur.Normalized() * 1.75f;
		pointApproche.Y = PnjHumainBiomeInstinct.HauteurSolMonde(pointApproche.X, pointApproche.Z, seedTerrain);
		return true;
	}

	private string NomGroupeStockBaies() => $"CampStockBaie_{Mathf.RoundToInt(Ancre.X)}_{Mathf.RoundToInt(Ancre.Y)}";

	private static void FigelerBaieStock(ItemPhysique baie)
	{
		baie.LinearVelocity = Vector3.Zero;
		baie.AngularVelocity = Vector3.Zero;
		baie.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
		baie.Freeze = true;
	}

	private static void MarquerBaieStockCamp(ItemPhysique baie, CampPnjStructure camp, int couleur, bool comestible)
	{
		string groupe = camp.NomGroupeStockBaies();
		baie.AddToGroup(groupe);
		baie.SetMeta("CampStockBaie", true);
		baie.SetMeta("CampStockGroupe", groupe);
		baie.SetMeta("CampStockCouleur", couleur);
		baie.SetMeta("CampStockComestible", comestible);
		baie.SetMeta("CampStockAncreX", camp.Ancre.X);
		baie.SetMeta("CampStockAncreZ", camp.Ancre.Y);
		FigelerBaieStock(baie);
	}

	private static void RetirerMarqueStockCamp(ItemPhysique baie)
	{
		if (baie.HasMeta("CampStockGroupe"))
			baie.RemoveFromGroup(baie.GetMeta("CampStockGroupe").AsString());
		if (baie.HasMeta("CampStockBaie"))
			baie.RemoveMeta("CampStockBaie");
		if (baie.HasMeta("CampStockGroupe"))
			baie.RemoveMeta("CampStockGroupe");
		if (baie.HasMeta("CampStockCouleur"))
			baie.RemoveMeta("CampStockCouleur");
		if (baie.HasMeta("CampStockComestible"))
			baie.RemoveMeta("CampStockComestible");
		if (baie.HasMeta("CampStockAncreX"))
			baie.RemoveMeta("CampStockAncreX");
		if (baie.HasMeta("CampStockAncreZ"))
			baie.RemoveMeta("CampStockAncreZ");
	}

	/// <summary>Pose une baie 3D dans la zone stock (pillable par le joueur plus tard).</summary>
	public bool EssayerPoserBaiePhysiqueStock(Gestionnaire_Monde gm, int seedTerrain, int couleur, bool comestible, out ItemPhysique posee)
	{
		posee = null;
		if (gm == null || !GodotObject.IsInstanceValid(gm))
			return false;
		int indexVisuel = CompterBaiesPhysiquesGroupe(gm, comestible);
		if (!EssayerPositionPoseBaieStock(comestible, seedTerrain, indexVisuel, out Vector3 positionPose))
			return false;
		var slot = new SlotInventaire { ID = Joueur.IdObjetBaie, IndexChimique = couleur, Quantite = 1 };
		ItemPhysique baie = Joueur.CreerItemPhysiqueBaie(slot);
		if (!gm.PoserItemPhysiqueAuMonde(baie, positionPose))
		{
			baie.QueueFree();
			return false;
		}
		MarquerBaieStockCamp(baie, this, couleur, comestible);
		ReconstruireStockDepuisPhysique(gm);
		posee = baie;
		return true;
	}

	/// <summary>Aligne les modèles 3D au sol sur le stock logique (chargement sauvegarde, rattrapage).</summary>
	public void SynchroniserBaiesPhysiquesStock(Gestionnaire_Monde gm, int seedTerrain)
	{
		if (gm == null || !_initialise)
			return;
		SynchroniserZonePhysique(gm, seedTerrain, _stockBonParCouleur, comestible: true);
		SynchroniserZonePhysique(gm, seedTerrain, _stockMauvaisParCouleur, comestible: false);
		ReconstruireStockDepuisPhysique(gm);
	}

	/// <summary>Recompte les baies réellement présentes dans les zones stock (vol joueur, dépôt, baie expulsée).</summary>
	public void ReconcilierStockPhysique(Gestionnaire_Monde gm, int seedTerrain)
	{
		if (gm == null || !_initialise)
			return;
		IntegrerBaiesJoueurDansZones(gm, seedTerrain);
		PurgerBaiesHorsZoneStock(gm);
		ReconstruireStockDepuisPhysique(gm);
	}

	public static void TickReconcilierTousLesCamps(Gestionnaire_Monde gm, int seedTerrain, float dt)
	{
		if (gm == null || !GodotObject.IsInstanceValid(gm))
			return;
		_cooldownReconcilGlobal -= dt;
		if (_cooldownReconcilGlobal > 0f)
			return;
		_cooldownReconcilGlobal = 0.45f;
		foreach (SocietePnj soc in SocietePnj.ToutesPourSauvegarde())
			soc.StructureCamp?.ReconcilierStockPhysique(gm, seedTerrain);
		foreach (PnjHumain pnj in PnjHumain.Tous)
		{
			if (pnj == null || !GodotObject.IsInstanceValid(pnj))
				continue;
			pnj.ObtenirCampPersoStructure()?.ReconcilierStockPhysique(gm, seedTerrain);
		}
	}

	private void IntegrerBaiesJoueurDansZones(Gestionnaire_Monde gm, int seedTerrain)
	{
		Vector2 ancre = Ancre;
		foreach (Node node in gm.GetTree().GetNodesInGroup("BlocsPoses"))
		{
			if (node is not ItemPhysique ip || !GodotObject.IsInstanceValid(ip))
				continue;
			if (ip.ID_Objet != Joueur.IdObjetBaie)
				continue;
			Vector2 xz = new Vector2(ip.GlobalPosition.X, ip.GlobalPosition.Z);
			if (xz.DistanceTo(ancre) > RayonScanBaiesAutourCamp)
				continue;
			if (ip.HasMeta("CampStockBaie") && ip.GetMeta("CampStockBaie").AsBool())
				continue;
			if (!EssayerClasserBaieDansZone(xz, out bool comestible))
				continue;
			int couleur = Joueur.ClampIndexCouleurBaie(ip.IndexChimique);
			MarquerBaieStockCamp(ip, this, couleur, comestible);
			FigelerBaieStock(ip);
		}
	}

	private void PurgerBaiesHorsZoneStock(Gestionnaire_Monde gm)
	{
		foreach (Node node in gm.GetTree().GetNodesInGroup(NomGroupeStockBaies()))
		{
			if (node is not ItemPhysique ip || !GodotObject.IsInstanceValid(ip))
				continue;
			Vector2 xz = new Vector2(ip.GlobalPosition.X, ip.GlobalPosition.Z);
			if (EstBaieDansZoneStock(xz, ip.HasMeta("CampStockComestible") && ip.GetMeta("CampStockComestible").AsBool()))
				continue;
			RetirerMarqueStockCamp(ip);
		}
	}

	private bool EssayerClasserBaieDansZone(Vector2 xz, out bool comestible)
	{
		comestible = false;
		float distBon = xz.DistanceTo(ZoneStockBon);
		float distMauvais = xz.DistanceTo(ZoneStockMauvais);
		if (distBon <= RayonStockBaiesBon && distBon <= distMauvais)
		{
			comestible = true;
			return true;
		}
		if (distMauvais <= RayonStockBaiesMauvais)
		{
			comestible = false;
			return true;
		}
		return false;
	}

	private bool EstBaieDansZoneStock(Vector2 xz, bool comestible)
	{
		Vector2 zone = comestible ? ZoneStockBon : ZoneStockMauvais;
		float rayon = comestible ? RayonStockBaiesBon : RayonStockBaiesMauvais;
		return xz.DistanceTo(zone) <= rayon;
	}

	private void ReconstruireStockDepuisPhysique(Gestionnaire_Monde gm)
	{
		_stockBonParCouleur.Clear();
		_stockMauvaisParCouleur.Clear();
		if (gm == null || !GodotObject.IsInstanceValid(gm))
		{
			_baiesVisuellesBon = 0;
			_baiesVisuellesMauvais = 0;
			RafraichirLabelsStock();
			return;
		}
		foreach (Node node in gm.GetTree().GetNodesInGroup(NomGroupeStockBaies()))
		{
			if (node is not ItemPhysique ip || !GodotObject.IsInstanceValid(ip))
				continue;
			if (!ip.HasMeta("CampStockBaie") || !ip.GetMeta("CampStockBaie").AsBool())
				continue;
			bool comestible = ip.HasMeta("CampStockComestible") && ip.GetMeta("CampStockComestible").AsBool();
			Vector2 xz = new Vector2(ip.GlobalPosition.X, ip.GlobalPosition.Z);
			if (!EstBaieDansZoneStock(xz, comestible))
				continue;
			int couleur = ip.HasMeta("CampStockCouleur")
				? Joueur.ClampIndexCouleurBaie(ip.GetMeta("CampStockCouleur").AsInt32())
				: Joueur.ClampIndexCouleurBaie(ip.IndexChimique);
			var dict = comestible ? _stockBonParCouleur : _stockMauvaisParCouleur;
			dict[couleur] = dict.GetValueOrDefault(couleur, 0) + 1;
		}
		_baiesVisuellesBon = TotalStockComestible();
		_baiesVisuellesMauvais = TotalStockToxique();
		RafraichirLabelsStock();
	}

	private void SynchroniserZonePhysique(Gestionnaire_Monde gm, int seedTerrain, Dictionary<int, int> stock, bool comestible)
	{
		foreach (KeyValuePair<int, int> kv in stock)
		{
			int manquantes = kv.Value - CompterPhysiqueCouleur(gm, comestible, kv.Key);
			for (int i = 0; i < manquantes; i++)
			{
				if (!EssayerPoserBaiePhysiqueStock(gm, seedTerrain, kv.Key, comestible, out _))
					break;
			}
		}
	}

	private int CompterBaiesPhysiquesGroupe(Gestionnaire_Monde gm, bool comestible)
	{
		if (gm == null || !GodotObject.IsInstanceValid(gm))
			return 0;
		int n = 0;
		string groupe = NomGroupeStockBaies();
		foreach (Node node in gm.GetTree().GetNodesInGroup(groupe))
		{
			if (node is not ItemPhysique ip || !GodotObject.IsInstanceValid(ip))
				continue;
			if (!ip.HasMeta("CampStockComestible") || ip.GetMeta("CampStockComestible").AsBool() != comestible)
				continue;
			n++;
		}
		return n;
	}

	private int CompterPhysiqueCouleur(Gestionnaire_Monde gm, bool comestible, int couleur)
	{
		if (gm == null || !GodotObject.IsInstanceValid(gm))
			return 0;
		int n = 0;
		foreach (Node node in gm.GetTree().GetNodesInGroup(NomGroupeStockBaies()))
		{
			if (node is not ItemPhysique ip || !GodotObject.IsInstanceValid(ip))
				continue;
			if (!ip.HasMeta("CampStockComestible") || ip.GetMeta("CampStockComestible").AsBool() != comestible)
				continue;
			if (!ip.HasMeta("CampStockCouleur") || ip.GetMeta("CampStockCouleur").AsInt32() != couleur)
				continue;
			n++;
		}
		return n;
	}

	private void RetirerBaiePhysiqueStock(Gestionnaire_Monde gm, int couleur, bool comestible)
	{
		if (gm == null || !GodotObject.IsInstanceValid(gm))
			return;
		foreach (Node node in gm.GetTree().GetNodesInGroup(NomGroupeStockBaies()))
		{
			if (node is not ItemPhysique ip || !GodotObject.IsInstanceValid(ip))
				continue;
			if (!ip.HasMeta("CampStockComestible") || ip.GetMeta("CampStockComestible").AsBool() != comestible)
				continue;
			if (!ip.HasMeta("CampStockCouleur") || ip.GetMeta("CampStockCouleur").AsInt32() != couleur)
				continue;
			ip.QueueFree();
			return;
		}
	}

	/// <summary>Ancien point d'entrée : le stock est désormais dérivé des baies 3D présentes dans la zone.</summary>
	public void EnregistrerBaieDeposee(int couleur, int quantite, bool comestibleConnue, bool objetPhysiquePose)
	{
		if (quantite <= 0)
			return;
		// Le recomptage physique est fait à la pose ; ici on ne double plus le stock logique.
		RafraichirLabelsStock();
	}

	public bool PreleverBaieComestible(Gestionnaire_Monde gm, out int couleur)
	{
		couleur = 0;
		if (gm == null || !GodotObject.IsInstanceValid(gm))
			return false;
		ItemPhysique meilleure = null;
		float meilleureDist2 = float.MaxValue;
		Vector2 zone = ZoneStockBon;
		foreach (Node node in gm.GetTree().GetNodesInGroup(NomGroupeStockBaies()))
		{
			if (node is not ItemPhysique ip || !GodotObject.IsInstanceValid(ip))
				continue;
			if (!ip.HasMeta("CampStockComestible") || !ip.GetMeta("CampStockComestible").AsBool())
				continue;
			Vector2 xz = new Vector2(ip.GlobalPosition.X, ip.GlobalPosition.Z);
			if (!EstBaieDansZoneStock(xz, comestible: true))
				continue;
			float d2 = xz.DistanceSquaredTo(zone);
			if (meilleure != null && d2 >= meilleureDist2)
				continue;
			meilleure = ip;
			meilleureDist2 = d2;
			couleur = ip.HasMeta("CampStockCouleur")
				? Joueur.ClampIndexCouleurBaie(ip.GetMeta("CampStockCouleur").AsInt32())
				: Joueur.ClampIndexCouleurBaie(ip.IndexChimique);
		}
		if (meilleure == null)
			return false;
		meilleure.QueueFree();
		ReconstruireStockDepuisPhysique(gm);
		return true;
	}

	public int TotalStockComestible()
	{
		int t = 0;
		foreach (KeyValuePair<int, int> kv in _stockBonParCouleur)
			t += kv.Value;
		return t;
	}

	public int TotalStockToxique()
	{
		int t = 0;
		foreach (KeyValuePair<int, int> kv in _stockMauvaisParCouleur)
			t += kv.Value;
		return t;
	}

	public string ResumeEtiquette(string objectifReserve = null)
	{
		int bon = TotalStockComestible();
		int tox = TotalStockToxique();
		int roc = TotalStockRoches();
		string stock = bon <= 0 && tox <= 0 && roc <= 0 ? "stock vide" : $"Bon:{bon} | Tox:{tox} | Roc:{roc}";
		string txt = $"Camp etabli\n{stock}";
		if (!string.IsNullOrEmpty(objectifReserve))
			txt += $"\n{objectifReserve}";
		return txt;
	}

	public void LibererMarqueurs()
	{
		if (_racineMarqueurs != null && GodotObject.IsInstanceValid(_racineMarqueurs))
			_racineMarqueurs.QueueFree();
		_racineMarqueurs = null;
	}

	public void CopierStocksVers(List<(int couleur, int qty)> bon, List<(int couleur, int qty)> mauvais, out int visBon, out int visMauvais)
	{
		bon.Clear();
		mauvais.Clear();
		foreach (KeyValuePair<int, int> kv in _stockBonParCouleur)
			if (kv.Value > 0)
				bon.Add((kv.Key, kv.Value));
		foreach (KeyValuePair<int, int> kv in _stockMauvaisParCouleur)
			if (kv.Value > 0)
				mauvais.Add((kv.Key, kv.Value));
		visBon = _baiesVisuellesBon;
		visMauvais = _baiesVisuellesMauvais;
	}

	public void RestaurerStocksDepuisSauvegarde(
		int objectifReserve,
		int baiesDeposeesReserve,
		IReadOnlyList<(int couleur, int qty)> bon,
		IReadOnlyList<(int couleur, int qty)> mauvais,
		int visBon,
		int visMauvais)
	{
		_stockBonParCouleur.Clear();
		_stockMauvaisParCouleur.Clear();
		if (bon != null)
			foreach ((int couleur, int qty) in bon)
				_stockBonParCouleur[couleur] = qty;
		if (mauvais != null)
			foreach ((int couleur, int qty) in mauvais)
				_stockMauvaisParCouleur[couleur] = qty;
		_baiesVisuellesBon = Mathf.Max(0, visBon);
		_baiesVisuellesMauvais = Mathf.Max(0, visMauvais);
		_objectifReserveBaies = Mathf.Max(0, objectifReserve);
		_baiesDeposeesReserve = Mathf.Max(0, baiesDeposeesReserve);
		RafraichirLabelsStock();
	}

	public void FinaliserRestaurationStock(Gestionnaire_Monde gm, int seedTerrain)
	{
		SynchroniserBaiesPhysiquesStock(gm, seedTerrain);
		ReconcilierStockPhysique(gm, seedTerrain);
	}
}
