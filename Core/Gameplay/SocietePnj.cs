using Godot;
using System.Collections.Generic;

/// <summary>
/// Société (colonie) de PNJ : se forme naturellement quand des PNJ se rencontrent et collaborent.
/// AUCUN chef au tout début — il émerge naturellement (assez de membres + temps) : le plus savant parmi les « gentils »
/// (les menteurs sont mis de côté, jamais chefs). Les sociétés différentes restent séparées (diplomatie = brique future).
/// </summary>
public sealed class SocietePnj
{
	private const int MembresMinPourChef = 2;
	private const ulong DelaiAvantChefMs = 8000; // ~8 s après formation de la société

	public string Nom { get; }
	private readonly List<PnjHumain> _membres = new();
	private readonly ulong _creationMs;

	private static readonly List<SocietePnj> _toutes = new();
	private static readonly RandomNumberGenerator _rng = new();
	private static readonly string[] Prefixes = { "Kael", "Mor", "Syl", "Thar", "Vael", "Eld", "Orn", "Brae", "Fen", "Lyr", "Garn", "Vol", "Aru", "Tys" };
	private static readonly string[] Suffixes = { "ia", "or", "heim", "wyn", "dor", "gar", "eth", "une", "ara", "is", "oth", "el" };

	public SocietePnj(string nom)
	{
		Nom = string.IsNullOrWhiteSpace(nom) ? GenererNom() : nom;
		_creationMs = Time.GetTicksMsec();
		_toutes.Add(this);
	}

	public IReadOnlyList<PnjHumain> Membres => _membres;
	public int NombreMembres { get { Nettoyer(); return _membres.Count; } }

	public void Ajouter(PnjHumain p)
	{
		if (p == null || _membres.Contains(p))
			return;
		_membres.Add(p);
		p.DefinirSociete(this);
		p.OnRejointSociete();
	}

	public void Retirer(PnjHumain p) => _membres.Remove(p);

	private void Nettoyer()
	{
		for (int i = _membres.Count - 1; i >= 0; i--)
			if (_membres[i] == null || !GodotObject.IsInstanceValid(_membres[i]))
				_membres.RemoveAt(i);
	}

	/// <summary>Chef émergent : null tant que la société est jeune/petite, sinon le plus savant parmi les gentils.</summary>
	public PnjHumain ChefActuel()
	{
		Nettoyer();
		if (_membres.Count < MembresMinPourChef)
			return null;
		if (Time.GetTicksMsec() - _creationMs < DelaiAvantChefMs)
			return null;
		PnjHumain meilleur = null;
		int max = -1;
		foreach (PnjHumain m in _membres)
		{
			if (!m.EstGentil)
				continue; // les menteurs sont mis de côté : jamais chef
			int c = m.NombreConnaissances;
			if (c > max) { max = c; meilleur = m; }
		}
		return meilleur;
	}

	public string RangDe(PnjHumain p)
	{
		if (ChefActuel() == p)
			return "Chef";
		return p.RoleVillageois switch
		{
			RoleVillageoisPnj.Cueilleur => "Cueilleur",
			RoleVillageoisPnj.Chasseur => "Chasseur",
			RoleVillageoisPnj.Garde => "Garde",
			_ => "Membre"
		};
	}

	// ----- Ordres du chef (sans ordre actif = PNJ libres) -----

	private OrdreChefPnj _ordreActif;
	private int _baiesDeposeesOrdre;
	private float _cooldownAvantProchainOrdre;

	public bool AOrdreActif => _ordreActif != null && _ordreActif.Actif;
	public OrdreChefPnj OrdreActif => _ordreActif;
	public int BaiesDeposeesOrdre => _baiesDeposeesOrdre;

	// ----- Structure du camp (zones réunion / stock / repas) -----

	private CampPnjStructure _structureCamp;
	public CampPnjStructure StructureCamp => _structureCamp;
	public bool CampSocieteEtabli => _structureCamp != null && _structureCamp.EstInitialise;

	// ----- Objectif campement collectif (migration partagée avant installation) -----

	private Vector2 _cibleCampementColonie;
	private bool _aCibleCampementColonie;

	public bool AObjectifCampementColonie => _aCibleCampementColonie && !CampSocieteEtabli;

	public bool EssayerObtenirCibleCampementColonie(out Vector2 cible)
	{
		if (AObjectifCampementColonie)
		{
			cible = _cibleCampementColonie;
			return true;
		}
		cible = Vector2.Zero;
		return false;
	}

	public bool PeutFixerCibleCampement(PnjHumain demandeur)
	{
		if (CampSocieteEtabli || demandeur == null)
			return false;
		PnjHumain chef = ChefActuel();
		return chef == null || chef == demandeur;
	}

	public Vector2 ObtenirCentroideMembres()
	{
		Nettoyer();
		Vector2 sum = Vector2.Zero;
		int n = 0;
		foreach (PnjHumain m in _membres)
		{
			if (m == null || !GodotObject.IsInstanceValid(m))
				continue;
			sum += new Vector2(m.GlobalPosition.X, m.GlobalPosition.Z);
			n++;
		}
		return n > 0 ? sum / n : Vector2.Zero;
	}

	/// <summary>Chef (ou premier membre avant élection) fixe UNE cible de campement pour toute la colonie.</summary>
	public bool CalculerEtPublierCibleCampement(int seed, Vector2 origine, PnjHumain demandeur)
	{
		if (!PeutFixerCibleCampement(demandeur))
			return AObjectifCampementColonie;

		Vector2 depart = ObtenirCentroideMembres();
		if (depart.LengthSquared() < 1f)
			depart = origine;

		int ox = Mathf.FloorToInt(depart.X);
		int oz = Mathf.FloorToInt(depart.Y);
		if (PnjHumainBiomeInstinct.EstBiomeFavorablePourCampement(ox, oz, seed))
		{
			_aCibleCampementColonie = false;
			return false;
		}

		if (PnjHumainBiomeInstinct.EssayerTrouverBiomePourCampement(seed, depart, out Vector2 cible)
			|| PnjHumainBiomeInstinct.EssayerTrouverMeilleureDirectionCampement(seed, depart, out cible))
		{
			_cibleCampementColonie = cible;
			_aCibleCampementColonie = true;
			GD.Print($"ZERO-K PNJ société [{Nom}] : objectif campement collectif ({cible.X:0},{cible.Y:0}) à {depart.DistanceTo(cible):0} m.");
			return true;
		}
		return false;
	}

	public void EffacerObjectifCampementColonie() => _aCibleCampementColonie = false;

	public int IndexMembre(PnjHumain p)
	{
		Nettoyer();
		if (p == null)
			return 0;
		int idx = _membres.IndexOf(p);
		return idx < 0 ? 0 : idx;
	}

	public void InitialiserStructureCamp(Vector2 ancre, int seedTerrain, Gestionnaire_Monde gm)
	{
		if (_structureCamp != null && _structureCamp.EstInitialise)
			return;
		if (!CampPnjStructure.EstEmplacementLibre(ancre))
			return;
		_structureCamp = CampPnjStructure.Creer(ancre, seedTerrain);
		_structureCamp.MaterialiserMarqueurs(gm, seedTerrain);
		CampPnjStructure.EnregistrerAncre(ancre);
		GD.Print($"ZERO-K PNJ société [{Nom}] : camp structuré à ({ancre.X:0},{ancre.Y:0}).");
	}

	public void NotifierCampEtabli()
	{
		EffacerObjectifCampementColonie();
		_cooldownAvantProchainOrdre = 8f;
		EmettreObjectifReserveColonie(Mathf.Clamp(10 + NombreMembres * 3, 12, 28));
		PnjHumain chef = ChefActuel();
		if (chef != null && GodotObject.IsInstanceValid(chef))
			RepartirRolesColonie(chef);
	}

	// ----- Objectif colonie : remplir la réserve de baies comestibles -----

	private int _objectifReserveBaies;
	private int _baiesDeposeesReserve;

	public bool DoitRemplirReserveColonie => _objectifReserveBaies > 0 && _baiesDeposeesReserve < _objectifReserveBaies;
	public int ObjectifReserveBaies => _objectifReserveBaies;
	public int BaiesDeposeesReserve => _baiesDeposeesReserve;

	public void RestaurerObjectifReserveColonie(int objectif, int deposees)
	{
		_objectifReserveBaies = Mathf.Max(0, objectif);
		_baiesDeposeesReserve = Mathf.Max(0, deposees);
	}

	public static IReadOnlyList<SocietePnj> ToutesPourSauvegarde() => _toutes;

	public void EmettreObjectifReserveColonie(int quantite)
	{
		_objectifReserveBaies = Mathf.Max(8, quantite);
		_baiesDeposeesReserve = 0;
		GD.Print($"ZERO-K PNJ société [{Nom}] : objectif colonie -> remplir la réserve ({_objectifReserveBaies} baies comestibles).");
	}

	public void NotifierBaieDeposeeReserve(bool comestibleConnue, int quantite = 1)
	{
		if (!comestibleConnue || quantite <= 0 || _objectifReserveBaies <= 0)
			return;
		_baiesDeposeesReserve = Mathf.Min(_objectifReserveBaies, _baiesDeposeesReserve + quantite);
		if (_baiesDeposeesReserve >= _objectifReserveBaies)
		{
			GD.Print($"ZERO-K PNJ société [{Nom}] : réserve colonie remplie ({_baiesDeposeesReserve}/{_objectifReserveBaies}).");
			_objectifReserveBaies = 0;
		}
	}

	public string ResumeObjectifReserve()
	{
		if (_objectifReserveBaies <= 0)
			return _baiesDeposeesReserve > 0 ? "Reserve: OK" : null;
		return $"Reserve:{_baiesDeposeesReserve}/{_objectifReserveBaies}";
	}

	public void TickOrdres(float dt, PnjHumain chef)
	{
		if (_ordreActif != null && _ordreActif.Actif)
		{
			_ordreActif.TempsEcoule += dt;
			if (_ordreActif.EstExpire || _ordreActif.EstComplete(_baiesDeposeesOrdre))
				TerminerOrdreActif();
			return;
		}

		_cooldownAvantProchainOrdre -= dt;
		if (_cooldownAvantProchainOrdre > 0f || chef == null || !GodotObject.IsInstanceValid(chef))
			return;
		if (!chef.EstEnPauseCamp)
			return;
		if (ChefActuel() != chef)
			return;

		RepartirRolesColonie(chef);
		EmettreOrdreRamenerBaies(10, 600f);
	}

	/// <summary>Le chef répartit cueilleurs / chasseurs / gardes selon la taille de la colonie et les objectifs.</summary>
	public void RepartirRolesColonie(PnjHumain chef)
	{
		if (!CampSocieteEtabli || chef == null)
			return;
		Nettoyer();
		var membres = new List<PnjHumain>();
		foreach (PnjHumain m in _membres)
		{
			if (m == null || !GodotObject.IsInstanceValid(m) || m == chef)
				continue;
			membres.Add(m);
		}
		if (membres.Count == 0)
			return;

		bool besoinCueilleurs = DoitRemplirReserveColonie || AOrdreActif;
		int nbGarde = membres.Count >= 3 ? 1 : 0;
		int nbChasseur = membres.Count >= 4 ? 1 : 0;
		int nbCueilleur = besoinCueilleurs
			? Mathf.Max(1, membres.Count - nbGarde - nbChasseur)
			: 0;

		for (int i = 0; i < membres.Count; i++)
		{
			RoleVillageoisPnj role = RoleVillageoisPnj.Libre;
			if (i < nbCueilleur)
				role = RoleVillageoisPnj.Cueilleur;
			else if (i < nbCueilleur + nbChasseur)
				role = RoleVillageoisPnj.Chasseur;
			else if (i < nbCueilleur + nbChasseur + nbGarde)
				role = RoleVillageoisPnj.Garde;
			membres[i].DefinirRoleVillageois(role);
		}
	}

	public void EmettreOrdreRamenerBaies(int quantite, float dureeSec)
	{
		_ordreActif = OrdreChefPnj.CreerRamenerBaies(quantite, dureeSec);
		_baiesDeposeesOrdre = 0;
		Nettoyer();
		foreach (PnjHumain m in _membres)
		{
			if (m == null || !GodotObject.IsInstanceValid(m))
				continue;
			m.NotifierNouvelOrdreChef(_ordreActif);
		}
		PnjHumain chef = ChefActuel();
		if (chef != null && GodotObject.IsInstanceValid(chef))
			GD.Print($"ZERO-K PNJ société [{Nom}] : ordre du chef -> {_ordreActif.ResumeCourt()} ({_ordreActif.DureeMaxSec:0}s).");
	}

	public void DeposerBaiesOrdre(int quantite)
	{
		if (quantite <= 0 || _ordreActif == null || !_ordreActif.Actif)
			return;
		_baiesDeposeesOrdre += quantite;
	}

	private void TerminerOrdreActif()
	{
		if (_ordreActif == null)
			return;
		GD.Print($"ZERO-K PNJ société [{Nom}] : ordre terminé ({_baiesDeposeesOrdre}/{_ordreActif.QuantiteCible} baies). Les membres sont libres.");
		_ordreActif.Actif = false;
		_ordreActif = null;
		_baiesDeposeesOrdre = 0;
		_cooldownAvantProchainOrdre = 90f;
		Nettoyer();
		foreach (PnjHumain m in _membres)
		{
			if (m == null || !GodotObject.IsInstanceValid(m))
				continue;
			m.ReinitialiserEtatOrdreChef();
		}
	}

	public int QuotaBaiesParMembreObéissant()
	{
		if (_ordreActif == null)
			return 0;
		Nettoyer();
		int obéissants = 0;
		foreach (PnjHumain m in _membres)
		{
			if (m == null || !GodotObject.IsInstanceValid(m) || m == ChefActuel())
				continue;
			if (m.ObéitOrdreChefActif())
				obéissants++;
		}
		obéissants = Mathf.Max(1, obéissants);
		return Mathf.Max(1, Mathf.CeilToInt((float)_ordreActif.QuantiteCible / obéissants));
	}

	public static SocietePnj TrouverOuCreerParNom(string nom)
	{
		if (!string.IsNullOrWhiteSpace(nom))
			foreach (SocietePnj s in _toutes)
				if (s.Nom == nom)
					return s;
		return new SocietePnj(nom);
	}

	/// <summary>Rencontre entre deux PNJ : forme une nouvelle société ou fait rejoindre la société existante (collaboration).</summary>
	public static void Rencontrer(PnjHumain a, PnjHumain b)
	{
		if (a == null || b == null || a == b)
			return;
		SocietePnj sa = a.Societe;
		SocietePnj sb = b.Societe;
		if (sa == null && sb == null)
		{
			var s = new SocietePnj(null);
			s.Ajouter(a);
			s.Ajouter(b);
		}
		else if (sa != null && sb == null)
			sa.Ajouter(b);
		else if (sa == null && sb != null)
			sb.Ajouter(a);
		// Deux sociétés différentes : on les laisse séparées (diplomatie inter-villages = brique future).
	}

	/// <summary>Regroupe les PNJ invoqués ensemble (/INVOCA HOMINA) en une seule société.</summary>
	public static SocietePnj FormerGroupeInvoque(IReadOnlyList<PnjHumain> pnjs)
	{
		if (pnjs == null || pnjs.Count == 0)
			return null;
		PnjHumain pivot = null;
		foreach (PnjHumain p in pnjs)
		{
			if (p == null || !GodotObject.IsInstanceValid(p))
				continue;
			if (pivot == null)
				pivot = p;
			else
				Rencontrer(pivot, p);
		}
		SocietePnj s = pivot?.Societe;
		if (s != null && s.NombreMembres >= 2)
			GD.Print($"ZERO-K PNJ société [{s.Nom}] : {s.NombreMembres} membres regroupés après invocation.");
		return s;
	}

	public static string GenererNom()
	{
		_rng.Randomize();
		return Prefixes[_rng.RandiRange(0, Prefixes.Length - 1)] + Suffixes[_rng.RandiRange(0, Suffixes.Length - 1)];
	}
}
