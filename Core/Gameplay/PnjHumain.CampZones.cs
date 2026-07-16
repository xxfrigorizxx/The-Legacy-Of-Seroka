using Godot;

/// <summary>Comportement structuré au camp : déposer les baies, manger au stock commun, se retrouver au point de réunion.</summary>
public partial class PnjHumain : CharacterBody3D
{
	private const float PorteeZoneCamp = 2.1f;
	private const float PorteeRepasCamp = 2.4f;
	private const float SeuilFaimRepasCampReserve = 0.38f;

	private CampPnjStructure _campPerso;
	private enum TacheCamp { Aucune, DeposerStock, DeposerRoches, AllerRepas, Reunion, RamasserRoches, IdentifierBaies }
	private TacheCamp _tacheCamp = TacheCamp.Aucune;
	private float _cooldownTacheCamp;

	private CampPnjStructure ObtenirStructureCamp()
	{
		if (_societe != null && _societe.StructureCamp != null && _societe.StructureCamp.EstInitialise)
			return _societe.StructureCamp;
		if (_campPerso != null && _campPerso.EstInitialise)
			return _campPerso;
		return null;
	}

	internal CampPnjStructure ObtenirCampPersoStructure() => _campPerso;

	internal bool DoitCueillirPourReserveColonie()
	{
		if (_societe != null)
			return _societe.DoitRemplirReserveColonie;
		return _campPerso != null && _campPerso.DoitRemplirReserveColonie;
	}

	internal void InitialiserStructureCamp(Vector2 ancre)
	{
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		if (_societe != null)
		{
			if (_societe.ChefActuel() != this)
				return;
			_societe.InitialiserStructureCamp(ancre, seed, gm);
			_campPerso = null;
		}
		else
		{
			if (!CampPnjStructure.EstEmplacementLibre(ancre))
				return;
			_campPerso = CampPnjStructure.Creer(ancre, seed);
			_campPerso.MaterialiserMarqueurs(gm, seed);
			_campPerso.DefinirObjectifReserve(15);
			CampPnjStructure.EnregistrerAncre(ancre);
		}
		_tacheCamp = TacheCamp.Aucune;
		_cooldownTacheCamp = 0.5f;
		DiagForage("structure de camp initialisée (zones réunion / stock / repas)");
	}

	/// <summary>Annule un camp solo en cours quand le PNJ rejoint une société (évite N camps au spawn).</summary>
	internal void OnRejointSociete()
	{
		if (_phaseCampChef == PhaseCampChef.Evaluation)
			AnnulerEvaluationCamp();
		if (_campPerso != null)
		{
			CampPnjStructure.RetirerAncre(_ancreCamp);
			_campPerso.LibererMarqueurs();
			_campPerso = null;
		}
		if (_societe != null && _societe.ChefActuel() != this && !_campRebelleSepare)
		{
			_enPauseCamp = false;
			_phaseCampChef = PhaseCampChef.Aucune;
		}
		_aCibleMigrationAbsolue = false;
		_cibleMigrationAbsolue = Vector3.Zero;
		Callable.From(InstinctMigrationInitialeSiBesoin).CallDeferred();
	}

	private void NotifierDepotReserveColonie(bool comestibleConnue, int quantite = 1)
	{
		_societe?.NotifierBaieDeposeeReserve(comestibleConnue, quantite);
		_campPerso?.NotifierBaieDeposeeReserve(comestibleConnue, quantite);
		MettreAJourEtiquetteCamp();
	}

	/// <summary>Priorités camp : 1 déposer stock, 2 manger au commun, 3 réunion proche, 4 cueillette réserve.</summary>
	private bool ExecuterComportementCampStructure(float dt, out Vector3 direction)
	{
		direction = Vector3.Zero;
		if (!_enPauseCamp || ObéitOrdreChefActif())
			return false;

		CampPnjStructure camp = ObtenirStructureCamp();
		if (camp == null)
			return false;

		_cooldownTacheCamp -= dt;
		ChoisirTacheCamp(camp);

		switch (_tacheCamp)
		{
			case TacheCamp.DeposerStock:
				return ExecuterDepotStockCamp(camp, out direction);
			case TacheCamp.IdentifierBaies:
				return ExecuterIdentificationBaiesCamp(camp, out direction);
			case TacheCamp.DeposerRoches:
				return ExecuterDepotRocheCamp(camp, out direction);
			case TacheCamp.AllerRepas:
				return ExecuterRepasCommunCamp(camp, out direction);
			case TacheCamp.Reunion:
				return ExecuterReunionCamp(camp, out direction);
			case TacheCamp.RamasserRoches:
				return ExecuterRamassageRocheCamp(camp, out direction);
			default:
				return false;
		}
	}

	private void ChoisirTacheCamp(CampPnjStructure camp)
	{
		if (_cooldownTacheCamp > 0f && _tacheCamp != TacheCamp.Aucune)
			return;

		if (CompterBaiesConnuesDeposables() > 0)
		{
			_tacheCamp = TacheCamp.DeposerStock;
			return;
		}

		if (CompterBaiesInconnuesInventaire() > 0)
		{
			_tacheCamp = TacheCamp.IdentifierBaies;
			return;
		}

		if (CompterRochesInventaire() > 0)
		{
			_tacheCamp = TacheCamp.DeposerRoches;
			return;
		}

		if (RatioFaim() < (DoitCueillirPourReserveColonie() ? SeuilFaimRepasCampReserve : SeuilFaimForage))
		{
			if (camp.TotalStockComestible() > 0)
			{
				_tacheCamp = TacheCamp.AllerRepas;
				return;
			}
			_tacheCamp = TacheCamp.Aucune;
			return;
		}

		if (DoitCueillirPourReserveColonie() || ObéitOrdreChefActif())
		{
			if (!RoleAutoriseCueilletteReserve() && !ObéitOrdreChefActif())
			{
				_tacheCamp = TacheCamp.Aucune;
				return;
			}
			_tacheCamp = TacheCamp.Aucune;
			return;
		}

		if (InventaireAPlacePourRoche() && CompterRochesInventaire() < 2
			&& (_roleVillageois == RoleVillageoisPnj.Chasseur || _rngPnj.Randf() < 0.42f))
		{
			_tacheCamp = TacheCamp.RamasserRoches;
			return;
		}

		if (_rngPnj.Randf() < 0.28f)
			_tacheCamp = TacheCamp.Reunion;
		else
			_tacheCamp = TacheCamp.Aucune;
	}

	private bool ExecuterDepotStockCamp(CampPnjStructure camp, out Vector3 direction)
	{
		direction = Vector3.Zero;
		int couleur;
		bool comestible;
		if (_indexSlotBaiePourDepot < 0)
		{
			if (!EssayerReserverSlotBaiePourDepot(out couleur, out comestible))
			{
				_tacheCamp = TacheCamp.Aucune;
				return false;
			}
		}
		else if (!LireSlotBaieReservee(out couleur, out comestible))
		{
			AnnulerReservationDepotBaie();
			_tacheCamp = TacheCamp.Aucune;
			return false;
		}

		Vector2 zone = comestible ? camp.ZoneStockBon : camp.ZoneStockMauvais;
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		Vector3 cible;
		if (camp.EssayerPointApprocheDepotBaie(gm, comestible, seed, out Vector3 pointApproche, out _))
			cible = pointApproche;
		else
			cible = new Vector3(zone.X, GlobalPosition.Y, zone.Y);
		float dist = GlobalPosition.DistanceTo(cible);
		if (dist > 1.65f)
		{
			Vector3 vers = cible - GlobalPosition;
			vers.Y = 0f;
			direction = vers.Normalized();
			_etatPnj = EtatPnj.Marche;
			return true;
		}

		if (!LireSlotBaieReservee(out couleur, out comestible))
		{
			AnnulerReservationDepotBaie();
			_tacheCamp = TacheCamp.Aucune;
			return false;
		}
		SlotInventaire slotPose = Inventaire[_indexSlotBaiePourDepot];
		slotPose.Quantite = 1;

		if (!ConfirmerPrelevementBaieReservee())
		{
			AnnulerReservationDepotBaie();
			_tacheCamp = TacheCamp.Aucune;
			return false;
		}

		bool objetPhysiquePose = camp.EssayerPoserBaiePhysiqueStock(gm, seed, couleur, comestible, out _);
		if (!objetPhysiquePose)
		{
			DiagForage("dépôt stock : échec pose physique");
			_cooldownTacheCamp = 0.6f;
			_etatPnj = EtatPnj.Idle;
			return true;
		}
		if (comestible)
		{
			NotifierDepotReserveColonie(true, 1);
			if (ObéitOrdreChefActif())
				_societe?.DeposerBaiesOrdre(1);
		}
		DiagForage($"déposé 1 baie {NomCouleurBaie(couleur)} -> stock {(comestible ? "bon" : "toxique")}");
		_cooldownTacheCamp = 0.4f;
		if (CompterBaiesInventairePourCamp() <= 0)
			_tacheCamp = TacheCamp.Aucune;
		_etatPnj = EtatPnj.Idle;
		return true;
	}

	private bool ExecuterDepotRocheCamp(CampPnjStructure camp, out Vector3 direction)
	{
		direction = Vector3.Zero;
		if (!EssayerExtraireRocheInventaire(out int matiere))
		{
			_tacheCamp = TacheCamp.Aucune;
			return false;
		}
		Vector3 cible = new Vector3(camp.ZoneStockRoches.X, GlobalPosition.Y, camp.ZoneStockRoches.Y);
		float dist = GlobalPosition.DistanceTo(cible);
		if (dist > PorteeZoneCamp)
		{
			Vector3 vers = cible - GlobalPosition;
			vers.Y = 0f;
			direction = vers.Normalized();
			_etatPnj = EtatPnj.Marche;
			return true;
		}
		camp.DeposerRoche(matiere, 1);
		DiagForage($"déposé 1 roche ({ItemPhysique.TableGeologique[matiere].Nom}) -> stock matériaux");
		_cooldownTacheCamp = 0.45f;
		if (CompterRochesInventaire() <= 0)
			_tacheCamp = TacheCamp.Aucune;
		_etatPnj = EtatPnj.Idle;
		return true;
	}

	private bool ExecuterRamassageRocheCamp(CampPnjStructure camp, out Vector3 direction)
	{
		direction = Vector3.Zero;
		_ = camp;
		if (_etatPnj == EtatPnj.Forage && _forageRoche)
		{
			Vector3 vers = _posRocheCible - GlobalPosition;
			vers.Y = 0f;
			if (vers.Length() < 1.4f)
			{
				TenterRamasserRocheCible();
				_forageRoche = false;
				_etatPnj = EtatPnj.Idle;
				_cooldownTacheCamp = 0.5f;
				return true;
			}
			direction = vers.Normalized();
			return true;
		}
		if (EssayerCiblerRocheProche())
		{
			_forageRoche = true;
			_etatPnj = EtatPnj.Forage;
			_cooldownEtatPnj = 10f;
			Vector3 vers = _posRocheCible - GlobalPosition;
			vers.Y = 0f;
			direction = vers.Normalized();
			return true;
		}
		_tacheCamp = TacheCamp.Aucune;
		return false;
	}

	private int CompterRochesInventaire()
	{
		if (Inventaire == null)
			return 0;
		int n = 0;
		for (int i = 0; i < Inventaire.Length; i++)
		{
			if (ItemPhysique.EstIdRocheMatiere(Inventaire[i].ID) && Inventaire[i].Quantite > 0)
				n += Inventaire[i].Quantite;
		}
		return n;
	}

	private bool InventaireAPlacePourRoche()
	{
		if (Inventaire == null)
			return false;
		for (int i = 0; i < Inventaire.Length; i++)
		{
			if (Inventaire[i].EstVide)
				return true;
		}
		return false;
	}

	private bool EssayerExtraireRocheInventaire(out int indexMatiere)
	{
		indexMatiere = 0;
		if (Inventaire == null)
			return false;
		for (int i = Inventaire.Length - 1; i >= 0; i--)
		{
			if (!ItemPhysique.EstIdRocheMatiere(Inventaire[i].ID) || Inventaire[i].Quantite <= 0)
				continue;
			indexMatiere = ItemPhysique.IndexChimiqueDepuisIdRoche(Inventaire[i].ID);
			SlotInventaire s = Inventaire[i];
			s.Quantite--;
			Inventaire[i] = s.Quantite <= 0 ? default : s;
			return true;
		}
		return false;
	}

	private bool AjouterRocheInventaire(SlotInventaire slot)
	{
		if (Inventaire == null || slot.Quantite <= 0 || !ItemPhysique.EstIdRocheMatiere(slot.ID))
			return false;
		for (int i = 0; i < Inventaire.Length; i++)
		{
			if (Inventaire[i].EstVide)
			{
				Inventaire[i] = slot;
				return true;
			}
		}
		return false;
	}

	private bool EssayerCiblerRocheProche()
	{
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		if (gm == null)
			return false;
		float rayon = _enPauseCamp ? ObtenirRayonCueilletteMaxEffectifCamp() : RayonRechercheBaie;
		Vector3 centreScan = _enPauseCamp
			? new Vector3(_ancreCamp.X, GlobalPosition.Y, _ancreCamp.Y)
			: GlobalPosition;
		if (!gm.EssayerDetecterRochePourPnj(centreScan, rayon, out Vector3 pos, out _, out _, out _))
			return false;
		if (_enPauseCamp)
		{
			Vector2 xz = new Vector2(pos.X, pos.Z);
			if (xz.DistanceTo(_ancreCamp) > rayon + MargePerimetreCueilletteCamp)
				return false;
		}
		_posRocheCible = pos;
		return true;
	}

	private void TenterRamasserRocheCible()
	{
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		if (gm == null)
			return;
		if (gm.RamasserRochePourPnj(_posRocheCible, 2f, out SlotInventaire slot) && AjouterRocheInventaire(slot))
			DiagForage($"ramassé roche {ItemPhysique.TableGeologique[slot.IndexChimique].Nom}");
	}

	private bool ExecuterRepasCommunCamp(CampPnjStructure camp, out Vector3 direction)
	{
		direction = Vector3.Zero;
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		Vector3 cible = new Vector3(camp.ZoneRepas.X, GlobalPosition.Y, camp.ZoneRepas.Y);
		float dist = GlobalPosition.DistanceTo(cible);
		if (dist > PorteeRepasCamp)
		{
			Vector3 vers = cible - GlobalPosition;
			vers.Y = 0f;
			direction = vers.Normalized();
			_etatPnj = EtatPnj.Marche;
			return true;
		}

		if (camp.PreleverBaieComestible(gm, out int couleur))
		{
			MangerBaie(couleur);
			DiagForage($"repas commun : baie {NomCouleurBaie(couleur)} du stock camp");
			_cooldownTacheCamp = 1.2f;
			_tacheCamp = RatioFaim() < SeuilFaimForage && camp.TotalStockComestible() > 0
				? TacheCamp.AllerRepas
				: TacheCamp.Aucune;
			_etatPnj = EtatPnj.Idle;
			MettreAJourEtiquetteCamp();
			return true;
		}

		_tacheCamp = TacheCamp.Aucune;
		return false;
	}

	private bool ExecuterReunionCamp(CampPnjStructure camp, out Vector3 direction)
	{
		direction = Vector3.Zero;
		Vector3 cible = new Vector3(camp.ZoneReunion.X, GlobalPosition.Y, camp.ZoneReunion.Y);
		float dist = GlobalPosition.DistanceTo(cible);
		if (dist > 1.8f)
		{
			Vector3 vers = cible - GlobalPosition;
			vers.Y = 0f;
			direction = vers.Normalized();
			_etatPnj = EtatPnj.Marche;
			return true;
		}
		_tacheCamp = TacheCamp.Aucune;
		_cooldownTacheCamp = _rngPnj.RandfRange(2f, 4.5f);
		_etatPnj = EtatPnj.Idle;
		return true;
	}

	private int CompterBaiesInventairePourCamp()
	{
		if (Inventaire == null)
			return 0;
		int total = 0;
		for (int i = 0; i < Inventaire.Length; i++)
		{
			if (Inventaire[i].ID == Joueur.IdObjetBaie && Inventaire[i].Quantite > 0)
				total += Inventaire[i].Quantite;
		}
		return total;
	}

	private int CompterBaiesInconnuesInventaire()
	{
		if (Inventaire == null)
			return 0;
		int total = 0;
		for (int i = 0; i < Inventaire.Length; i++)
		{
			if (Inventaire[i].ID != Joueur.IdObjetBaie || Inventaire[i].Quantite <= 0)
				continue;
			int couleur = Joueur.ClampIndexCouleurBaie(Inventaire[i].IndexChimique);
			if (!ConnaissanceBaie(couleur))
				total += Inventaire[i].Quantite;
		}
		return total;
	}

	/// <summary>Baies dont la couleur est connue (comestible ou toxique) — seules celles-ci vont au stock.</summary>
	private int CompterBaiesConnuesDeposables()
	{
		if (Inventaire == null)
			return 0;
		int total = 0;
		for (int i = 0; i < Inventaire.Length; i++)
		{
			if (Inventaire[i].ID != Joueur.IdObjetBaie || Inventaire[i].Quantite <= 0)
				continue;
			int couleur = Joueur.ClampIndexCouleurBaie(Inventaire[i].IndexChimique);
			if (ConnaissanceBaie(couleur))
				total += Inventaire[i].Quantite;
		}
		return total;
	}

	private bool ExecuterIdentificationBaiesCamp(CampPnjStructure camp, out Vector3 direction)
	{
		direction = Vector3.Zero;
		if (CompterBaiesInconnuesInventaire() <= 0)
		{
			_tacheCamp = TacheCamp.Aucune;
			return false;
		}
		Vector3 cible = new Vector3(camp.ZoneRepas.X, GlobalPosition.Y, camp.ZoneRepas.Y);
		float dist = GlobalPosition.DistanceTo(cible);
		if (dist > PorteeRepasCamp)
		{
			Vector3 vers = cible - GlobalPosition;
			vers.Y = 0f;
			direction = vers.Normalized();
			_etatPnj = EtatPnj.Marche;
			return true;
		}
		_etatPnj = EtatPnj.Idle;
		return false;
	}

	private bool EssayerReserverSlotBaiePourDepot(out int couleur, out bool comestibleConnue)
	{
		couleur = 0;
		comestibleConnue = false;
		AnnulerReservationDepotBaie();
		if (Inventaire == null)
			return false;
		int[] ordreSlots = { IdxMainDroite, IdxMainGauche, 2, 3, 4, 5 };
		foreach (int i in ordreSlots)
		{
			if (i < 0 || i >= Inventaire.Length)
				continue;
			if (Inventaire[i].ID != Joueur.IdObjetBaie || Inventaire[i].Quantite <= 0)
				continue;
			couleur = Joueur.ClampIndexCouleurBaie(Inventaire[i].IndexChimique);
			if (!ConnaissanceBaie(couleur))
				continue;
			comestibleConnue = CouleurApprisComestible(couleur) && !CouleurApprisToxique(couleur);
			_indexSlotBaiePourDepot = i;
			return true;
		}
		return false;
	}

	private bool LireSlotBaieReservee(out int couleur, out bool comestibleConnue)
	{
		couleur = 0;
		comestibleConnue = false;
		if (Inventaire == null || _indexSlotBaiePourDepot < 0 || _indexSlotBaiePourDepot >= Inventaire.Length)
			return false;
		SlotInventaire s = Inventaire[_indexSlotBaiePourDepot];
		if (s.ID != Joueur.IdObjetBaie || s.Quantite <= 0)
			return false;
		couleur = Joueur.ClampIndexCouleurBaie(s.IndexChimique);
		comestibleConnue = CouleurApprisComestible(couleur) && !CouleurApprisToxique(couleur);
		return true;
	}

	private bool ConfirmerPrelevementBaieReservee()
	{
		if (!LireSlotBaieReservee(out _, out _))
			return false;
		SlotInventaire s = Inventaire[_indexSlotBaiePourDepot];
		s.Quantite--;
		Inventaire[_indexSlotBaiePourDepot] = s.Quantite <= 0 ? default : s;
		AnnulerReservationDepotBaie();
		return true;
	}

	private bool EssayerExtraireBaieInventairePourDepot(out int couleur, out bool comestibleConnue)
	{
		if (EssayerReserverSlotBaiePourDepot(out couleur, out comestibleConnue))
			return ConfirmerPrelevementBaieReservee();
		return false;
	}

	internal string ObtenirTexteEtiquetteCampStructure()
	{
		CampPnjStructure camp = ObtenirStructureCamp();
		if (camp == null)
			return null;
		string reserve = _societe?.ResumeObjectifReserve();
		if (string.IsNullOrEmpty(reserve) && _campPerso != null && _campPerso.DoitRemplirReserveColonie)
			reserve = $"Reserve:{_campPerso.BaiesDeposeesReserve}/{_campPerso.ObjectifReserveBaies}";
		else if (string.IsNullOrEmpty(reserve) && _societe != null && !_societe.DoitRemplirReserveColonie && _societe.BaiesDeposeesReserve > 0)
			reserve = "Reserve: OK";
		return camp.ResumeEtiquette(reserve);
	}
}
