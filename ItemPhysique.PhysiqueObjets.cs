using Godot;
using System;
using System.Collections.Generic;

public partial class ItemPhysique : RigidBody3D
{
	public static float RayonBaseRochesJoueur(int indexTaille) => indexTaille switch
	{
		0 => 0.08f,
		1 => 0.15f,
		2 => 0.25f,
		3 => 0.40f,
		4 => 0.65f,
		_ => 0.2f
	};

	public static Vector3 EchelleMorphologieRoche(int morph) => morph switch
	{
		1 => new Vector3(1f, 0.4f, 1f),
		2 => new Vector3(1f, 0.7f, 1.4f),
		3 => new Vector3(0.6f, 1.3f, 0.6f),
		_ => Vector3.One
	};

	/// <summary>Boîte alignée sur la sphère déformée du mesh : pas d’échelle non uniforme sur le <see cref="RigidBody3D"/> (Jolt).</summary>
	public static BoxShape3D CreerBoxCollisionRocheMatiere(float rayonSphereBase, Vector3 echelleMorph)
	{
		return new BoxShape3D
		{
			Size = new Vector3(
				rayonSphereBase * 2f * echelleMorph.X,
				rayonSphereBase * 2f * echelleMorph.Y,
				rayonSphereBase * 2f * echelleMorph.Z)
		};
	}

	/// <summary>Morph 0 = sphère (roule) ; 1–3 = boîte épousant le mesh déformé (plate / ovale / pointe).</summary>
	public static Shape3D CreerShapeCollisionRocheMatiere(float rayonSphereBase, int morphologie)
	{
		morphologie = Mathf.Clamp(morphologie, 0, 3);
		if (morphologie == 1 || morphologie == 2 || morphologie == 3)
			return CreerBoxCollisionRocheMatiere(rayonSphereBase, EchelleMorphologieRoche(morphologie));
		return new SphereShape3D { Radius = rayonSphereBase };
	}

	/// <summary>Plus la roche est grosse (index 0–4), plus elle encaisse avant fracture (résistance de base × facteur).</summary>
	public static float FacteurSoliditeRochesParTaille(int indexTailleRoche)
	{
		int t = Mathf.Clamp(indexTailleRoche, 0, 4);
		return 0.68f + t * 0.13f;
	}

	/// <summary>Roche posée : CCD. Ronde (morph 0) = sphère + faible amortissement pour rouler ; déformée = boîte + amortissement plus fort (stabilité).</summary>
	public static void AppliquerPhysiqueRochePortee(ItemPhysique rb)
	{
		if (rb == null || !EstIdRocheMatiere(rb.ID_Objet)) return;
		int m = Mathf.Clamp(rb.IndexCacheMemoire, 0, 3);
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.ContinuousCd = true;
		if (m == 0) // ronde
		{
			rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.82f, Bounce = 0.06f };
			rb.LinearDamp = 0.2f;
			rb.AngularDamp = 0.35f;
		}
		else if (m == 1) // plate
		{
			rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.96f, Bounce = 0.04f };
			rb.LinearDamp = 0.4f;
			rb.AngularDamp = 1.05f;
		}
		else if (m == 2) // ovale
		{
			// Ovale : conserve de l'inertie et roule plus naturellement.
			rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.76f, Bounce = 0.04f };
			rb.LinearDamp = 0.16f;
			rb.AngularDamp = 0.28f;
		}
		else // m == 3, pointe
		{
			// Pointe : peut rouler/tanguer puis se stabiliser, sans arrêt "net" immédiat.
			rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.88f, Bounce = 0.03f };
			rb.LinearDamp = 0.24f;
			rb.AngularDamp = 0.72f;
		}
	}

	/// <summary>Dague posée/lancée : CCD + amortissement pour limiter traverse-sol et vrilles infinies.</summary>
	public static void AppliquerPhysiqueDague105(ItemPhysique rb)
	{
		if (rb == null || rb.ID_Objet != 105) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.22f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.9f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.65f, Bounce = 0.04f };
	}

	/// <summary>Hachette primitive (106) : même esprit que la dague, masse plus élevée, CCD.</summary>
	public static void AppliquerPhysiqueHachette106(ItemPhysique rb)
	{
		if (rb == null || (rb.ID_Objet != 106 && rb.ID_Objet != Joueur.IdObjetHachePierreTier1)) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.2f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.82f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.62f, Bounce = 0.05f };
	}

	/// <summary>Pelle pierre tier0 (107) : physique proche hachette, un peu plus stable au sol.</summary>
	public static void AppliquerPhysiquePelle107(ItemPhysique rb)
	{
		if (rb == null || rb.ID_Objet != Joueur.IdObjetPellePierreTier0) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.24f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.92f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.68f, Bounce = 0.04f };
	}

	/// <summary>Pioche pierre tier0 (108) : outil plus lourd, stabilité proche hachette.</summary>
	public static void AppliquerPhysiquePioche108(ItemPhysique rb)
	{
		if (rb == null || rb.ID_Objet != Joueur.IdObjetPiochePierreTier0) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.22f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.88f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.66f, Bounce = 0.04f };
	}

	/// <summary>Lance pierre tier0 (111) : plus allongée, orientée attaque/lancer.</summary>
	public static void AppliquerPhysiqueLance111(ItemPhysique rb)
	{
		if (rb == null || rb.ID_Objet != Joueur.IdObjetLancePierreTier0) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.18f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.62f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.58f, Bounce = 0.05f };
	}

	/// <summary>Faux primitive (112) : même esprit que la dague, légèrement plus amortie (mesh épée).</summary>
	public static void AppliquerPhysiqueFaux112(ItemPhysique rb)
	{
		if (rb == null || rb.ID_Objet != Joueur.IdObjetFauxPierreTier0) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.23f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.88f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.64f, Bounce = 0.045f };
	}
}
