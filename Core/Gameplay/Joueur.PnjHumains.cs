using Godot;

/// <summary>Intégration joueur : chargement et invocation des PNJ humains.</summary>
public partial class Joueur : CharacterBody3D
{
	private void ChargerPnjHumainsMonde()
	{
		Node parent = GetParent();
		if (parent != null && GodotObject.IsInstanceValid(parent))
			PnjHumainPersistance.Charger(parent);
	}

	private int FaireApparaitrePnjHumains(int nombre) => PnjHumainSpawner.FaireApparaitreAutourDe(this, nombre);

	/// <summary>Téléporte le joueur vers un PNJ (physique ou virtuel). Retourne false si échec.</summary>
	public bool TeleporterVersPnjHumain(string filtreNom, out string message)
	{
		message = "";
		Gestionnaire_Monde gm = _gestionnaireMonde;
		if (gm == null || !GodotObject.IsInstanceValid(gm))
			gm = GetParent()?.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
		if (gm == null || !gm.JoueurReferenceValide())
		{
			message = "Monde ou joueur introuvable.";
			return false;
		}
		if (!PnjHumainLocalisationService.EssayerResoudreCible(gm, filtreNom, out PnjHumainLocalisationService.InfoPnj info))
		{
			message = string.IsNullOrWhiteSpace(filtreNom)
				? "Aucun PNJ humain actif."
				: $"Aucun PNJ ne correspond à « {filtreNom.Trim()} ».";
			return false;
		}
		if (!gm.TeleporterJoueurVers(info.Position))
		{
			message = "Téléportation impossible.";
			return false;
		}
		if (info.Virtuel)
			PnjHumainContinuiteService.DeclencherRematerialisationUrgente(gm, info.Position, info.Nom);
		string etat = info.Virtuel ? "virtuel (reapparition en cours)"
			: info.EnEvaluationCamp ? "choisit un camp"
			: info.EnCamp ? "au camp"
			: info.EnMigration ? "en migration"
			: "sur place";
		message = $"Téléporté vers {info.Nom} ({etat}) — ({info.Position.X:0},{info.Position.Z:0}).";
		if (info.EnMigration && info.CibleMigration.LengthSquared() > 1f)
			message += $" Destination ({info.CibleMigration.X:0},{info.CibleMigration.Y:0}).";
		return true;
	}
}
