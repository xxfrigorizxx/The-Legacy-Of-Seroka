using Godot;
using System;
using System.Collections.Generic;

public partial class ItemPhysique : RigidBody3D
{
	/// <summary>Seuil de rupture (Loi du Rebond). En dessous de cette force d'impact, dégâts strictement zéro.</summary>
	public float ObtenirSeuilRupture()
	{
		if (EstMatiereSilexParIdObjet(ID_Objet)) return 80f;
		if (EstIdRocheMatiere(ID_Objet)) return 50f;
		if (ID_Objet == 30 || ID_Objet == 32 || ID_Objet == BlocChutant.ID_BRANCHE || ID_Objet == Joueur.IdObjetPitFeu || ID_Objet == Joueur.IdObjetPitFeuRoche || ID_Objet == Joueur.IdObjetTorche || ID_Objet == Joueur.IdObjetFenetreBois || ID_Objet == Joueur.IdObjetTableBoisDecorative || ID_Objet == Joueur.IdObjetTableArtisanaTier1) return 40f; // Bois mort durci
		if (ID_Objet == Joueur.IdObjetAllumeFeu) return 44f;
		return 10f; // Matières souples ou organiques
	}

	/// <summary>Applique les dégâts selon la Loi du Rebond : en dessous du seuil, zéro dégât.</summary>
	/// <returns>0 = Rebond (Zéro dégât), 1 = Endommagé, 2 = Fracturé/Détruit</returns>
	public int SubirDegats(float forceImpact, Vector3 dirVue, Vector3 pointImpact)
	{
		float seuil = ObtenirSeuilRupture();
		if (forceImpact < seuil)
			return 0;

		float degats = forceImpact;
		float capPourcent;
		if (EstIdRocheMatiere(ID_Objet))
		{
			degats *= 0.060f;
			capPourcent = 0.26f;
		}
		else if (ID_Objet == 30 || ID_Objet == 32 || ID_Objet == BlocChutant.ID_BRANCHE || ID_Objet == Joueur.IdObjetPitFeu || ID_Objet == Joueur.IdObjetPitFeuRoche || ID_Objet == Joueur.IdObjetTorche || ID_Objet == Joueur.IdObjetFenetreBois || ID_Objet == Joueur.IdObjetTableBoisDecorative || ID_Objet == Joueur.IdObjetTableArtisanaTier1)
		{
			degats *= 0.080f;
			capPourcent = 0.34f;
		}
		else if (ID_Objet == Joueur.IdObjetAllumeFeu)
		{
			degats *= 0.068f;
			capPourcent = 0.28f;
		}
		else if (EstMatiereSilexParIdObjet(ID_Objet))
		{
			degats *= 0.065f;
			capPourcent = 0.28f;
		}
		else
		{
			degats *= 0.10f;
			capPourcent = 0.36f;
		}
		float capParCoup = Mathf.Max(4f, ResistanceActuelle * capPourcent);
		degats = Mathf.Min(degats, capParCoup);
		ResistanceActuelle -= degats;
		if (ResistanceActuelle <= 0)
		{
			FracturerPublic(dirVue, pointImpact);
			return 2;
		}
		return 1;
	}

	// ----- MOTEUR DE FRACTURE (SurImpactPhysique → Fracturer → SpawnEclatVrai) -----
	/// <summary>Appelé à chaque contact physique. body peut être null (terrain PhysicsServer3D bas-niveau) → traité comme sol.</summary>
	private void SurImpactPhysique(Node body)
	{
		if (EstIdRocheMatiere(ID_Objet) && _frameFinGraceImpactLancer != 0 && Engine.GetPhysicsFrames() < _frameFinGraceImpactLancer)
			return;

		BoeufSauvage boeufTouche = ResoudreBoeufDepuisNoeud(body);
		if (boeufTouche != null)
		{
			float vitesseImpact = LinearVelocity.Length();
			float masseImpact = Mathf.Max(0.01f, Mass);
			float energieImpactCinetique = 0.5f * masseImpact * vitesseImpact * vitesseImpact;
			float impulsion = masseImpact * vitesseImpact;
			float energieImpact = (energieImpactCinetique * 0.44f + impulsion * 3.4f)
				* CoefficientMorphologieImpact()
				* CoefficientMateriauImpactFaune();

			bool tranchant = EstObjetTranchantPourImpactFaune();
			bool perforant = tranchant && vitesseImpact > 3.3f && EstObjetPointeBienAligneeVers(boeufTouche);
			string zone = DeterminerZoneImpactBovin(boeufTouche);

			bool applique = boeufTouche.RecevoirImpactCombat(
				energieImpact,
				GlobalPosition,
				LinearVelocity,
				tranchant,
				perforant,
				zone,
				(ulong)GetInstanceId());

			if (applique)
			{
				LinearVelocity *= 0.4f;
				AngularVelocity *= 0.35f;
				if (perforant && vitesseImpact > 4.9f)
					TenterPlanterDansBovin(boeufTouche);
			}
		}

		// Objets légers/petits: au contact joueur, on les réveille et on les repousse légèrement
		// pour éviter un blocage dur sur de petits objets au sol.
		if (body is CharacterBody3D personnage && EstObjetLegerEtPetitReactif())
		{
			if (Freeze || EstEnReposAuSolOptimise)
				ReveillerPhysiqueAuSol();
			Vector3 dirPoussee = GlobalPosition - personnage.GlobalPosition;
			dirPoussee.Y = 0f;
			if (dirPoussee.LengthSquared() < 0.0001f)
			{
				dirPoussee = LinearVelocity;
				dirPoussee.Y = 0f;
			}
			if (dirPoussee.LengthSquared() > 0.0001f)
				dirPoussee = dirPoussee.Normalized();
			else
				dirPoussee = Vector3.Forward;

			float impulsionHoriz = Mathf.Clamp(0.8f + Mass * 0.25f, 0.8f, 3.5f);
			ApplyCentralImpulse(dirPoussee * impulsionHoriz + Vector3.Up * 0.18f);
		}

		// 1. Détection du corps fantôme (terrain bas-niveau)
		bool frappeLeSol = (body == null);

		// 2. Calcul de l'énergie cinétique
		float velociteRelative = LinearVelocity.Length();
		if (!frappeLeSol && body is RigidBody3D rigidBody)
			velociteRelative += rigidBody.LinearVelocity.Length();

		float masseCourante = Mathf.Max(0.01f, Mass);
		float energieCinetique = 0.5f * masseCourante * velociteRelative * velociteRelative;
		// Roches : seuil haut + grâce au lancer — évite fracture « dans le vide » au départ.
		float seuilEnergie = EstIdRocheMatiere(ID_Objet) ? 85f : 8f;
		if (energieCinetique < seuilEnergie) return;

		// Choc contre un personnage (sortie de main / frottement) : pas de casse sauf très gros choc.
		if (EstIdRocheMatiere(ID_Objet) && body is CharacterBody3D && energieCinetique < 220f)
			return;

		// Choc roche↔roche en chute libre (grottes / sol pas encore solidifié) : pas de fracture en cascade.
		if (EstIdRocheMatiere(ID_Objet) && body is ItemPhysique autreRoche && !frappeLeSol)
		{
			if (EstEclatFracture || autreRoche.EstEclatFracture)
				return;
			if (energieCinetique < 180f)
				return;
		}

		// 3. Dureté adverse
		float dureteAdverse = 50f;
		if (frappeLeSol)
			dureteAdverse = 80f;
		else if (body is ItemPhysique autreRocheContact)
		{
			int idxAutre = Mathf.Clamp(autreRocheContact.IndexChimique, 0, TableGeologique.Length - 1);
			dureteAdverse = TableGeologique[idxAutre].ResistanceFuture;
		}

		// 4. Calcul des dégâts internes
		int idxMoi = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
		float maDurete = TableGeologique[idxMoi].ResistanceFuture;
		float degatsSubis = (Mathf.Sqrt(energieCinetique) * 9.5f * dureteAdverse) / Mathf.Max(1f, maDurete);
		if (EstIdRocheMatiere(ID_Objet))
		{
			degatsSubis *= Mathf.Clamp((energieCinetique - seuilEnergie) / 78f, 0.1f, 1.18f);
			if (body is CharacterBody3D)
				degatsSubis *= 0.12f;
			// Un seul contact ne peut pas vider toute la résistance (lancer violent sur sol dur).
			degatsSubis = Mathf.Min(degatsSubis, Mathf.Max(6f, ResistanceActuelle * 0.38f));
		}
		ResistanceActuelle -= degatsSubis;

		if (!frappeLeSol && EstMatiereSilexParIdObjet(ID_Objet) && dureteAdverse > 70f && energieCinetique > 30f)
			GenererParticulesEtincelle();

		// 5. La fracture : direction de la vélocité (choc réel) + centre du corps → plan de coupe stable (évite plan aléatoire sur roche plate).
		if (ResistanceActuelle <= 0)
		{
			Vector3 v = LinearVelocity;
			Vector3? dirChoc = v.LengthSquared() > 0.04f ? v.Normalized() : (Vector3?)null;
			Fracturer(dirChoc, GlobalPosition);
		}
	}

	private static BoeufSauvage ResoudreBoeufDepuisNoeud(Node body)
	{
		for (Node n = body; n != null; n = n.GetParent())
			if (n is BoeufSauvage b)
				return b;
		return null;
	}

	private bool EstObjetTranchantPourImpactFaune()
	{
		if (ID_Objet == 105 || ID_Objet == 106 || ID_Objet == Joueur.IdObjetHachePierreTier1 || ID_Objet == Joueur.IdObjetPiochePierreTier0 || ID_Objet == Joueur.IdObjetPellePierreTier0 || ID_Objet == Joueur.IdObjetLancePierreTier0 || ID_Objet == Joueur.IdObjetFauxPierreTier0 || ID_Objet == 100)
			return true;
		if (EstUnEclat)
			return true;
		return EstIdRocheMatiere(ID_Objet) && IndexCacheMemoire == 3;
	}

	private float CoefficientMorphologieImpact()
	{
		if (!EstIdRocheMatiere(ID_Objet))
			return 1f;
		int morph = Mathf.Clamp(IndexCacheMemoire, 0, 3);
		return morph switch
		{
			1 => 0.88f, // plate : pénètre moins en lancer
			2 => 1.02f, // ovale : compromis
			3 => 1.16f, // pointe : transfert plus agressif
			_ => 0.97f  // ronde
		};
	}

	private float CoefficientMateriauImpactFaune()
	{
		if (EstMatiereSilexParIdObjet(ID_Objet))
			return 1.16f;
		if (ID_Objet == Joueur.IdObjetLancePierreTier0)
			return 1.22f;
		if (ID_Objet == 106 || ID_Objet == Joueur.IdObjetHachePierreTier1 || ID_Objet == Joueur.IdObjetPiochePierreTier0)
			return 1.08f;
		if (ID_Objet == 105 || ID_Objet == Joueur.IdObjetPellePierreTier0 || ID_Objet == Joueur.IdObjetFauxPierreTier0)
			return 0.96f;
		if (EstIdRocheMatiere(ID_Objet))
			return Mathf.Lerp(0.72f, 1.06f, Mathf.Clamp(IndexTailleRoche / 4f, 0f, 1f));
		return 1f;
	}

	private bool EstObjetPointeBienAligneeVers(BoeufSauvage cible)
	{
		if (cible == null || !GodotObject.IsInstanceValid(cible) || LinearVelocity.LengthSquared() < 0.01f)
			return false;
		Vector3 versCible = (cible.GlobalPosition - GlobalPosition).Normalized();
		Vector3 dirVitesse = LinearVelocity.Normalized();
		Vector3 axePointeA = (-GlobalTransform.Basis.Z).Normalized();
		Vector3 axePointeB = GlobalTransform.Basis.Y.Normalized();
		float alignPointe = Mathf.Max(axePointeA.Dot(dirVitesse), axePointeB.Dot(dirVitesse));
		float trajectoireVersCible = dirVitesse.Dot(versCible);
		return alignPointe > 0.38f && trajectoireVersCible > 0.35f;
	}

	private string DeterminerZoneImpactBovin(BoeufSauvage boeuf)
	{
		if (boeuf == null || !GodotObject.IsInstanceValid(boeuf))
			return "";
		Vector3 local = boeuf.ToLocal(GlobalPosition);
		if (local.Y > 0.95f) return "CollisionShape3D_Tete";
		if (local.Y > 0.32f && local.Y < 0.85f) return "CollisionShape3D_Ventre";
		return "CollisionShape3D";
	}

	private void TenterPlanterDansBovin(BoeufSauvage boeuf)
	{
		if (boeuf == null || !GodotObject.IsInstanceValid(boeuf) || _bovinPlante != null)
			return;
		_bovinPlante = boeuf;
		Vector3 dir = LinearVelocity.LengthSquared() > 0.001f ? LinearVelocity.Normalized() : -boeuf.GlobalTransform.Basis.Z.Normalized();
		_offsetLocalDansBovinPlante = boeuf.ToLocal(GlobalPosition + dir * 0.08f);
		Freeze = true;
		FreezeMode = FreezeModeEnum.Static;
		Sleeping = true;
		LinearVelocity = Vector3.Zero;
		AngularVelocity = Vector3.Zero;
		CollisionLayer = 0u;
		CollisionMask = 0u;
	}
}
