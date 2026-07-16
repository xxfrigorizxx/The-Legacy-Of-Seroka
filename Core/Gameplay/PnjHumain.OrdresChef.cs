using Godot;

/// <summary>Ordres du chef : sans ordre actif le PNJ est libre (baies, analyse, social). Avec ordre, il exécute puis rapporte au chef.</summary>
public partial class PnjHumain : CharacterBody3D
{
	private const float ProbabiliteDesobeissanceRebelle = 0.52f;
	private const float ProbabiliteDesobeissanceGentil = 0.07f;

	private int _idOrdreConnu;
	private bool _desobeitOrdreCourant;
	private bool _rapportOrdreTermine;

	/// <summary>True si ce PNJ doit suivre l'ordre actif de sa société.</summary>
	public bool ObéitOrdreChefActif()
	{
		if (_desobeitOrdreCourant || _societe == null || !_societe.AOrdreActif)
			return false;
		if (_societe.ChefActuel() == this)
			return false;
		return _idOrdreConnu == _societe.OrdreActif.Id;
	}

	public void NotifierNouvelOrdreChef(OrdreChefPnj ordre)
	{
		if (ordre == null)
			return;
		_idOrdreConnu = ordre.Id;
		_desobeitOrdreCourant = false;
		_rapportOrdreTermine = false;
		_forageRoche = false;
		if (_societe != null && _societe.ChefActuel() == this)
			return;
		bool desobeir = _estRebelle
			? _rngPnj.Randf() < ProbabiliteDesobeissanceRebelle
			: _rngPnj.Randf() < ProbabiliteDesobeissanceGentil;
		if (desobeir)
		{
			_desobeitOrdreCourant = true;
			EnregistrerActe(false);
			DiagForage("désobéit à l'ordre du chef -> reste libre");
		}
		MettreAJourEtiquetteCamp();
	}

	public void ReinitialiserEtatOrdreChef()
	{
		AnnulerReservationDepotBaie();
		_rapportOrdreTermine = false;
		_desobeitOrdreCourant = false;
		_idOrdreConnu = 0;
		_forageRoche = false;
		MettreAJourEtiquetteCamp();
	}

	private bool SousOrdreRamenerBaiesActif()
		=> ObéitOrdreChefActif()
			&& _societe.OrdreActif.Type == OrdreChefPnj.TypeOrdre.RamenerBaies;

	private void TickOrdresSociete(float dt)
	{
		if (_societe == null)
			return;
		PnjHumain chef = _societe.ChefActuel();
		if (chef == this)
		{
			_societe.TickOrdres(dt, this);
			MettreAJourEtiquetteCamp();
		}
	}

	/// <summary>Gère déplacement sous ordre. Retourne true si le mouvement est géré ici.</summary>
	private bool ExecuterOrdreChefActif(float dt, out Vector3 direction)
	{
		direction = Vector3.Zero;
		if (!SousOrdreRamenerBaiesActif())
			return false;

		if (RatioFaim() < FaimCritique)
			return false;

		if (_societe.OrdreActif.EstComplete(_societe.BaiesDeposeesOrdre) || _rapportOrdreTermine)
			return false;

		CampPnjStructure camp = ObtenirStructureCamp();
		if (camp != null && CompterBaiesConnuesDeposables() > 0)
		{
			if (ExecuterDepotStockCamp(camp, out direction))
				return true;
		}

		if (_etatPnj != EtatPnj.Forage && _cooldownRechercheBaie <= 0f)
		{
			_cooldownRechercheBaie = 0.85f;
			EssayerCiblerBuissonComestible();
		}

		switch (_etatPnj)
		{
			case EtatPnj.Forage when !_forageRoche:
			{
				Vector3 versB = _posBuissonCible - GlobalPosition;
				versB.Y = 0f;
				if (versB.Length() < 1.5f)
				{
					TenterRecolterBuissonPourOrdre();
					ApresForageSousOrdre();
					return true;
				}
				if (_cooldownEtatPnj <= 0f)
				{
					ApresForageSousOrdre();
					return true;
				}
				direction = versB.Normalized();
				return true;
			}
			case EtatPnj.Marche:
			{
				Vector3 versM = _ciblePnj - GlobalPosition;
				versM.Y = 0f;
				if (versM.Length() < 0.6f || _cooldownEtatPnj <= 0f)
				{
					_etatPnj = EtatPnj.Idle;
					_cooldownEtatPnj = 0.35f;
					return false;
				}
				direction = versM.Normalized();
				return true;
			}
			default:
				if (_cooldownEtatPnj <= 0f)
					EntrerEnMarcheCueilletteOrdre();
				if (_etatPnj == EtatPnj.Marche)
				{
					Vector3 versM = _ciblePnj - GlobalPosition;
					versM.Y = 0f;
					if (versM.Length() > 0.6f && _cooldownEtatPnj > 0f)
					{
						direction = versM.Normalized();
						return true;
					}
				}
				return false;
		}
	}

	private void ApresForageSousOrdre()
	{
		_etatPnj = EtatPnj.Idle;
		_cooldownEtatPnj = 0.35f;
		_cooldownRechercheBaie = 0f;
	}

	private void TenterRecolterBuissonPourOrdre()
	{
		if (!EssayerRecolterBaiesVersInventaire(_posBuissonCible))
			_cooldownRechercheBaie = 3f;
	}

	private int CompterBaiesInventaireTotal()
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

	private string ObtenirTexteEtiquetteOrdre()
	{
		if (_societe == null || !_societe.AOrdreActif)
			return null;
		OrdreChefPnj o = _societe.OrdreActif;
		if (_societe.ChefActuel() == this)
			return $"{o.ResumeCourt()} ({_societe.BaiesDeposeesOrdre}/{o.QuantiteCible})";
		if (ObéitOrdreChefActif())
		{
			if (o.EstComplete(_societe.BaiesDeposeesOrdre))
				return "Ordre rempli";
			if (CompterBaiesInventaireTotal() > 0)
				return "Depot reserve";
			return _etatPnj == EtatPnj.Forage ? "Cueillette" : "Ordre en cours";
		}
		if (_desobeitOrdreCourant)
			return "Libre (defi)";
		return null;
	}
}
