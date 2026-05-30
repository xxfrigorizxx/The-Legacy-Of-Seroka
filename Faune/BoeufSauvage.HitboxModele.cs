using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	private void AppliquerGeneTailleVisuelleEtPhysique()
	{
		if (!_geneTailleInitialise)
			return;
		float min = Mathf.Min(TailleGeneMin, TailleGeneMax);
		float max = Mathf.Max(TailleGeneMin, TailleGeneMax);
		_geneTaille = Mathf.Clamp(_geneTaille, min, max);
		if (_modeleVisuel != null)
		{
			Transform3D baseT = _transformModeleBase;
			baseT.Basis = baseT.Basis.Scaled(Vector3.One * TailleEffective);
			baseT.Origin += Vector3.Up * (_offsetVisuelSolActuel * TailleEffective);
			_modeleVisuel.Transform = baseT;
		}

		CollisionShape3D col = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col != null)
			AjusterHitboxDepuisModele(col);
	}

	private void AjusterHitboxDepuisModele(CollisionShape3D col)
	{
		if (!AjusterHitboxSurModele || !EssayerMesurerBoiteModele(out Vector3 minB, out Vector3 maxB))
		{
			if (col.Shape is not BoxShape3D boxFallback)
			{
				boxFallback = new BoxShape3D();
				col.Shape = boxFallback;
			}
			float taille = Mathf.Clamp(TailleEffective * Mathf.Clamp(MultiplicateurHitbox, 0.6f, 1.4f), 0.1f, 2.5f);
			boxFallback.Size = new Vector3(0.35f * taille, 0.7f * taille, 0.9f * taille);
			col.Position = new Vector3(0f, boxFallback.Size.Y * 0.5f, 0f);
			SynchroniserHitboxSecondairesDesactivees();
			return;
		}

		Vector3 size = maxB - minB;
		float mul = Mathf.Clamp(MultiplicateurHitbox, 0.6f, 1.4f);
		Vector3 centre = (minB + maxB) * 0.5f;
		Vector3 sizeMul = size * mul;

		if (col.Shape is not BoxShape3D box)
		{
			box = new BoxShape3D();
			col.Shape = box;
		}
		box.Size = new Vector3(
			Mathf.Clamp(sizeMul.X * 0.82f, 0.16f, 4.0f),
			Mathf.Clamp(sizeMul.Y * 0.82f, 0.22f, 4.0f),
			Mathf.Clamp(sizeMul.Z * 0.82f, 0.16f, 4.5f));
		col.Position = new Vector3(centre.X, centre.Y, centre.Z);

		if (!UtiliserHitboxComposite)
		{
			SynchroniserHitboxSecondairesDesactivees();
			return;
		}

		float dimLong = Mathf.Max(sizeMul.X, sizeMul.Z);
		float dimLarge = Mathf.Min(sizeMul.X, sizeMul.Z);
		float signeAvant = (_modeleVisuel != null && _modeleVisuel.Transform.Basis.Z.Z > 0f) ? -1f : 1f;

		_hitboxTete ??= ObtenirOuCreerCollisionShape("CollisionShape3D_Tete");
		if (_hitboxTete.Shape is not SphereShape3D sphereTete)
		{
			sphereTete = new SphereShape3D();
			_hitboxTete.Shape = sphereTete;
		}
		sphereTete.Radius = Mathf.Clamp(dimLarge * 0.24f * Mathf.Clamp(MultiplicateurHitboxTete, 0.4f, 1.6f), 0.08f, 0.9f);
		float offsetLongTete = dimLong * 0.34f * signeAvant;
		_hitboxTete.Position = new Vector3(
			centre.X + (sizeMul.X >= sizeMul.Z ? offsetLongTete : 0f),
			minB.Y + sizeMul.Y * 0.58f,
			centre.Z + (sizeMul.Z > sizeMul.X ? offsetLongTete : 0f));
		_hitboxTete.Disabled = false;

		_hitboxVentre ??= ObtenirOuCreerCollisionShape("CollisionShape3D_Ventre");
		if (_hitboxVentre.Shape is not SphereShape3D sphereVentre)
		{
			sphereVentre = new SphereShape3D();
			_hitboxVentre.Shape = sphereVentre;
		}
		sphereVentre.Radius = Mathf.Clamp(dimLarge * 0.28f * Mathf.Clamp(MultiplicateurHitboxVentre, 0.4f, 1.6f), 0.1f, 1.1f);
		_hitboxVentre.Position = new Vector3(centre.X, minB.Y + sizeMul.Y * 0.36f, centre.Z);
		_hitboxVentre.Disabled = false;
	}

	private CollisionShape3D ObtenirOuCreerCollisionShape(string nom)
	{
		CollisionShape3D n = GetNodeOrNull<CollisionShape3D>(nom);
		if (n != null)
			return n;
		n = new CollisionShape3D { Name = nom };
		AddChild(n);
		return n;
	}

	private void SynchroniserHitboxSecondairesDesactivees()
	{
		_hitboxTete ??= GetNodeOrNull<CollisionShape3D>("CollisionShape3D_Tete");
		_hitboxVentre ??= GetNodeOrNull<CollisionShape3D>("CollisionShape3D_Ventre");
		if (_hitboxTete != null) _hitboxTete.Disabled = true;
		if (_hitboxVentre != null) _hitboxVentre.Disabled = true;
	}

	private bool EssayerMesurerBoiteModele(out Vector3 minB, out Vector3 maxB)
	{
		minB = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		maxB = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		if (_modeleVisuel == null || !GodotObject.IsInstanceValid(_modeleVisuel))
			return false;

		bool touche = false;
		AccumulerBoiteModeleRecursif(_modeleVisuel, _modeleVisuel.Transform, ref minB, ref maxB, ref touche);
		return touche;
	}

	private void AccumulerBoiteModeleRecursif(Node node, Transform3D toBody, ref Vector3 minB, ref Vector3 maxB, ref bool touche)
	{
		if (node is MeshInstance3D mi && mi.Mesh != null)
		{
			Aabb aabb = mi.Mesh.GetAabb();
			Vector3[] coins =
			{
				new Vector3(aabb.Position.X, aabb.Position.Y, aabb.Position.Z),
				new Vector3(aabb.End.X, aabb.Position.Y, aabb.Position.Z),
				new Vector3(aabb.Position.X, aabb.End.Y, aabb.Position.Z),
				new Vector3(aabb.Position.X, aabb.Position.Y, aabb.End.Z),
				new Vector3(aabb.End.X, aabb.End.Y, aabb.Position.Z),
				new Vector3(aabb.End.X, aabb.Position.Y, aabb.End.Z),
				new Vector3(aabb.Position.X, aabb.End.Y, aabb.End.Z),
				new Vector3(aabb.End.X, aabb.End.Y, aabb.End.Z),
			};

			foreach (Vector3 c in coins)
			{
				Vector3 p = toBody * c;
				minB = new Vector3(Mathf.Min(minB.X, p.X), Mathf.Min(minB.Y, p.Y), Mathf.Min(minB.Z, p.Z));
				maxB = new Vector3(Mathf.Max(maxB.X, p.X), Mathf.Max(maxB.Y, p.Y), Mathf.Max(maxB.Z, p.Z));
			}
			touche = true;
		}

		foreach (Node enfant in node.GetChildren())
		{
			if (enfant is not Node3D n3) continue;
			AccumulerBoiteModeleRecursif(n3, toBody * n3.Transform, ref minB, ref maxB, ref touche);
		}
	}
}
