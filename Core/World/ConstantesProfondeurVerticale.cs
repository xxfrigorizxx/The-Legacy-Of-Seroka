using Godot;

/// <summary>
/// Découpage vertical du mode profondeur étendue (tranches de 100 m, fenêtre ±1 autour du joueur : courante + dessus + dessous).
/// L'Abysse et le legacy 720 m utilisent <see cref="Gestionnaire_Monde.HauteurMax"/> à part.
/// </summary>
public static class ConstantesProfondeurVerticale
{
	public const int HauteurTrancheMetres = 100;
	public const int HauteurSectionMetres = 16;
	/// <summary>Tranches visuelles / réseau : courante ±1 (3 couches) — suit le joueur à chaque descente/remontée d'une tranche.</summary>
	public const int DemiFenetreTranches = 1;
	/// <summary>Fenêtre physique Jolt : alignée sur le visuel (±1 tranche, ≈300 m sous les pieds).</summary>
	public const int DemiFenetrePhysiqueTranches = 1;
	/// <summary>Voxels (~m) près d'une jonction Y=0, ±100… (streaming, chargement).</summary>
	public const int MargeJonctionTrancheVoxels = 8;
	/// <summary>Padding vertical répliqué au minage (voxels réels touchant ly=0 / ly=h).</summary>
	public const int MargePaddingMinageVoxels = 3;

	/// <summary>Vrai si le joueur / voxel est dans la zone sensible d'une couture verticale.</summary>
	public static bool EstProcheJonctionTranche(int localY, int hauteurTranche = HauteurTrancheMetres)
		=> localY >= 0
			&& (localY <= MargeJonctionTrancheVoxels
				|| localY >= hauteurTranche - MargeJonctionTrancheVoxels - 1);

	public static bool EstProcheJonctionTrancheMonde(float yMonde, int hauteurTranche = HauteurTrancheMetres)
		=> EstProcheJonctionTranche(LocalYDepuisMondeY((int)Mathf.Floor(yMonde)), hauteurTranche);
	/// <summary>Plafond Y monde des tranches positives (aligné sur <see cref="Gestionnaire_Monde.HauteurMax"/>).</summary>
	public const int HauteurMondeMaxMetres = 720;
	/// <summary>v1 = tranches 720 m legacy ; v2 = tranches 100 m.</summary>
	public const byte VersionChunkProfondeur = 2;
	/// <summary>Niveau mer Alpha (Y monde). Une seule surface d'eau doit apparaître à cette hauteur, pas aux jonctions de tranches.</summary>
	public const int NiveauEauMondeAlpha = 103;

	/// <summary>Rôle d'une tranche 100 m pour la mer (passage 2.5D → 3D vertical).</summary>
	public enum RoleTrancheEauMer
	{
		/// <summary>Tranche entièrement au-dessus de la mer (pas d'eau).</summary>
		Aucun,
		/// <summary>Tranche entièrement sous la surface (ex. coordY=0, Y=0–99) — volume plein sans test « ciel » 2D.</summary>
		Corps,
		/// <summary>Tranche qui coupe la surface (ex. coordY=1, Y=100–103).</summary>
		Chapeau
	}

	public static RoleTrancheEauMer ObtenirRoleTrancheEauMer(int coordY, int hauteurTranche, int niveauEauMonde = NiveauEauMondeAlpha)
	{
		int yBase = coordY * hauteurTranche;
		int ySommet = yBase + hauteurTranche - 1;
		if (yBase >= niveauEauMonde)
			return RoleTrancheEauMer.Aucun;
		if (ySommet < niveauEauMonde)
			return RoleTrancheEauMer.Corps;
		if (yBase > 0 && yBase < niveauEauMonde)
			return RoleTrancheEauMer.Chapeau;
		return RoleTrancheEauMer.Corps;
	}

	public static int ObtenirNbSections(int hauteurTranche)
		=> Mathf.Max(1, Mathf.CeilToInt(hauteurTranche / (float)HauteurSectionMetres));

	public static int CoordYDepuisMondeY(float yMonde)
		=> Mathf.FloorToInt(yMonde / HauteurTrancheMetres);

	public static int LocalYDepuisMondeY(int yMonde)
	{
		int coordY = CoordYDepuisMondeY(yMonde);
		return yMonde - coordY * HauteurTrancheMetres;
	}

	public static int CoordYMaxSurface()
		=> CoordYDepuisMondeY(HauteurMondeMaxMetres - 1);

	public static int ClampCoordYProfond(int coordY, int profondeurMaxMetres)
	{
		int coordYMin = CoordYDepuisMondeY(-Mathf.Max(0, profondeurMaxMetres));
		return Mathf.Clamp(coordY, coordYMin, CoordYMaxSurface());
	}

	public static void RemplirFenetreCoordYAutourJoueur(float yMonde, int profondeurMaxMetres, System.Collections.Generic.HashSet<int> sortie, int demiFenetre = DemiFenetreTranches)
	{
		if (sortie == null) return;
		sortie.Clear();
		int cy = CoordYDepuisMondeY(yMonde);
		int demi = Mathf.Clamp(demiFenetre, 0, DemiFenetreTranches);
		for (int d = -demi; d <= demi; d++)
			sortie.Add(ClampCoordYProfond(cy + d, profondeurMaxMetres));
	}

	public static int ObtenirYMaxEauLocalTranche(int coordY, int hauteurTranche, int niveauEauMonde = NiveauEauMondeAlpha)
	{
		int yBaseMonde = coordY * hauteurTranche;
		int plafond = Mathf.Clamp(niveauEauMonde - yBaseMonde, 0, hauteurTranche);
		// Mer au-dessus du sommet de tranche : pas d'eau sur ly=h (jonction Y=100,200…) — la tranche du dessus porte le volume.
		if (niveauEauMonde > yBaseMonde + hauteurTranche - 1)
			plafond = Mathf.Min(plafond, hauteurTranche - 1);
		return plafond;
	}

	/// <summary>Indice local max pour le mesh MC (évite surface fantôme sur la face ly=h partagée).</summary>
	public static int ObtenirYMaxEauMaillageLocal(int coordY, int hauteurTranche, int niveauEauMonde = NiveauEauMondeAlpha)
		=> ObtenirYMaxEauLocalTranche(coordY, hauteurTranche, niveauEauMonde);

	/// <summary>Tranche qui contient le « chapeau » sous la mer (ex. coordY=1, Y=100–103).</summary>
	public static bool EstTrancheChapeauOcean(int coordY, int hauteurTranche, int niveauEauMonde = NiveauEauMondeAlpha)
		=> ObtenirRoleTrancheEauMer(coordY, hauteurTranche, niveauEauMonde) == RoleTrancheEauMer.Chapeau;

	/// <summary>Y monde du voxel pour un indice local (padding MC inclus).</summary>
	public static int MondeYDepuisLocal(int coordY, int hauteurTranche, int lyLocal)
		=> coordY * hauteurTranche + lyLocal;

	/// <summary>Vrai si le voxel est sous la surface de la mer (volume océan continu en 3D).</summary>
	public static bool EstSousNiveauMer(int yMonde, int niveauEauMonde = NiveauEauMondeAlpha)
		=> yMonde <= niveauEauMonde;

	/// <summary>Y monde de la jonction vers la tranche du dessus (ex. coordY=0 → 100).</summary>
	public static int MondeYJonctionTrancheSup(int coordY, int hauteurTranche)
		=> (coordY + 1) * hauteurTranche;

	/// <summary>
	/// Tranche corps : pas de mesh eau à partir de la jonction (Y≥100) — le chapeau porte 100–103 et la surface ~104.
	/// Évite la 2e surface MC à Y=100 alors que l'eau du chapeau descend déjà jusqu'à 101.
	/// </summary>
	public static bool CorpsOmetMaillageEauALaJonction(int coordY, int hauteurTranche, int lyLocal, int niveauEauMonde = NiveauEauMondeAlpha)
	{
		if (ObtenirRoleTrancheEauMer(coordY, hauteurTranche, niveauEauMonde) != RoleTrancheEauMer.Corps)
			return false;
		int yJonction = MondeYJonctionTrancheSup(coordY, hauteurTranche);
		if (niveauEauMonde < yJonction)
			return false;
		return MondeYDepuisLocal(coordY, hauteurTranche, lyLocal) >= yJonction;
	}

	/// <summary>Fenêtre verticale client : ±1 tranche (courante + dessus + dessous).</summary>
	public static int DemiFenetreTranchesStreaming(float vitesseYMonde)
		=> DemiFenetreTranches;

	/// <summary>Indices de sections du bas / haut de tranche (couture verticale client).</summary>
	public static void MarquerSectionsBordTranche(int hauteurTranche, System.Collections.Generic.HashSet<int> sections)
	{
		if (sections == null) return;
		int nb = ObtenirNbSections(hauteurTranche);
		for (int s = 0; s <= 1 && s < nb; s++)
			sections.Add(s);
		for (int s = nb - 2; s < nb; s++)
			if (s >= 0) sections.Add(s);
	}
}
