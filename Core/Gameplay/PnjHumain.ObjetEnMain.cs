using Godot;

/// <summary>
/// Affichage de l'objet TENU EN MAIN par le PNJ (baie, et plus tard outils craftés).
/// On accroche un point porte-objet sur l'os de chaque main (BoneAttachment3D) et on y instancie le modèle
/// correspondant au slot de la main. Mis à jour seulement quand le contenu de la main change (léger).
/// </summary>
public partial class PnjHumain : CharacterBody3D
{
	private Node3D _porteObjetMainD, _porteObjetMainG;
	private int _idAfficheMainD = -1, _idAfficheMainG = -1;
	private int _couleurAfficheMainD = -1, _couleurAfficheMainG = -1;

	private void ConfigurerAttachesMains()
	{
		Skeleton3D sk = TrouverPremier<Skeleton3D>(_rig);
		if (sk == null)
			return;
		int osD = TrouverOsMain(sk, droite: true);
		int osG = TrouverOsMain(sk, droite: false);
		if (osD >= 0)
			_porteObjetMainD = CreerPorteObjetSurOs(sk, osD, "PorteObjetMainDPnj");
		if (osG >= 0 && osG != osD)
			_porteObjetMainG = CreerPorteObjetSurOs(sk, osG, "PorteObjetMainGPnj");
	}

	private static Node3D CreerPorteObjetSurOs(Skeleton3D sk, int osIdx, string nom)
	{
		var attache = new BoneAttachment3D { Name = nom, BoneIdx = osIdx };
		sk.AddChild(attache);
		// Petit décalage pour poser l'objet dans la paume (approximatif, ajustable).
		var porte = new Node3D { Name = "Porte", Position = new Vector3(0f, 0.02f, 0.05f) };
		attache.AddChild(porte);
		return porte;
	}

	private static int TrouverOsMain(Skeleton3D sk, bool droite)
	{
		string cote = droite ? "right" : "left";
		string init = droite ? "r" : "l";
		// 1) os contenant "hand" + côté explicite.
		for (int i = 0; i < sk.GetBoneCount(); i++)
		{
			string n = sk.GetBoneName(i).ToString().ToLowerInvariant();
			if (n.Contains("hand") && (n.Contains(cote) || n.Contains("_" + init) || n.Contains("." + init) || n.EndsWith(init)))
				return i;
		}
		// 2) repli : n'importe quel os "hand" (mono-main).
		for (int i = 0; i < sk.GetBoneCount(); i++)
			if (sk.GetBoneName(i).ToString().ToLowerInvariant().Contains("hand"))
				return i;
		return -1;
	}

	private void MettreAJourObjetsEnMain()
	{
		SyncObjetMain(_porteObjetMainD, MainDroite, ref _idAfficheMainD, ref _couleurAfficheMainD);
		SyncObjetMain(_porteObjetMainG, MainGauche, ref _idAfficheMainG, ref _couleurAfficheMainG);
	}

	private static void SyncObjetMain(Node3D porte, SlotInventaire slot, ref int idAffiche, ref int couleurAffiche)
	{
		if (porte == null || !GodotObject.IsInstanceValid(porte))
			return;
		int id = slot.EstVide ? -1 : slot.ID;
		int couleur = slot.IndexChimique;
		if (id == idAffiche && couleur == couleurAffiche)
			return; // rien n'a changé
		idAffiche = id;
		couleurAffiche = couleur;

		if (id == Joueur.IdObjetBaie)
		{
			// InstancierModeleBaie (méthode statique de Joueur) nettoie déjà les enfants et instancie la baie teintée.
			Joueur.InstancierModeleBaie(porte, slot, 0.1f);
			return;
		}

		// Vide / autre objet non géré : on retire le visuel.
		foreach (Node c in porte.GetChildren())
			c.QueueFree();
	}
}
