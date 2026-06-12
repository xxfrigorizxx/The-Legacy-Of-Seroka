using Godot;

public partial class ItemPhysique : RigidBody3D
{
	/// <summary>False si un rayon caméra est bloqué par le terrain / un meuble (rendu seulement).</summary>
	public bool OcclusionVisible { get; internal set; } = true;
	/// <summary>Boîte locale pour tests d'occlusion (mise à jour au repos).</summary>
	public Aabb OcclusionBoiteLocale { get; private set; } = new Aabb(Vector3.Zero, Vector3.One * 0.3f);
	private OccluderInstance3D _occludeurObjetStatique;

	internal void NotifierEnregistrementOcclusionObjetPose()
	{
		if (!IsInGroup("BlocsPoses"))
			return;
		Monde_Client monde = TrouverMondeClientPourOcclusion();
		monde?.EnregistrerObjetPoseOcclusion(this);
		ActualiserBoiteOcclusionLocale();
	}

	internal void NotifierRetraitOcclusionObjetPose()
	{
		Monde_Client monde = TrouverMondeClientPourOcclusion();
		monde?.RetirerObjetPoseOcclusion(this);
		RetirerOccludeurStatiqueObjet();
	}

	/// <summary>Visibilité rendu : ne masque que les meshes (jamais le RigidBody entier — sinon tout disparaît / plus d'interaction).</summary>
	public void AppliquerVisibiliteRenduObjetPose()
	{
		bool visible = OcclusionVisible;
		Visible = true;
		foreach (Node enfant in GetChildren())
		{
			if (enfant is OccluderInstance3D)
				continue;
			if (enfant is MeshInstance3D mi)
			{
				if (mi.Visible != visible)
					mi.Visible = visible;
			}
			else if (enfant is Node3D n3d && enfant is not CollisionShape3D)
			{
				if (n3d.Visible != visible)
					n3d.Visible = visible;
			}
		}
	}

	/// <summary>Rétablit la visibilité complète (correctif après bug d'occlusion).</summary>
	public void ForcerVisibiliteRenduComplete()
	{
		OcclusionVisible = true;
		Visible = true;
		RetirerOccludeurStatiqueObjet();
		foreach (Node enfant in GetChildren())
		{
			if (enfant is MeshInstance3D mi)
				mi.Visible = true;
			else if (enfant is Node3D n3d && enfant is not CollisionShape3D && enfant is not OccluderInstance3D)
				n3d.Visible = true;
		}
	}

	internal void ActualiserBoiteOcclusionLocale()
	{
		OcclusionBoiteLocale = CalculerBoiteOcclusionLocale();
	}

	internal void GererOccludeurStatiqueObjet(bool actif)
	{
		if (!actif)
		{
			RetirerOccludeurStatiqueObjet();
			return;
		}
		Monde_Client monde = TrouverMondeClientPourOcclusion();
		if (monde == null || !monde.EstOcclusionVisuelleActivee)
			return;
		if (!ItemPhysique.EstMeublePoseStatique(ID_Objet) && !EstEnReposAuSolOptimise)
			return;
		if (_occludeurObjetStatique != null && GodotObject.IsInstanceValid(_occludeurObjetStatique))
			return;
		ActualiserBoiteOcclusionLocale();
		Vector3 taille = OcclusionBoiteLocale.Size;
		taille = new Vector3(
			Mathf.Max(taille.X, 0.12f),
			Mathf.Max(taille.Y, 0.12f),
			Mathf.Max(taille.Z, 0.12f));
		var box = new BoxOccluder3D { Size = taille };
		_occludeurObjetStatique = new OccluderInstance3D
		{
			Occluder = box,
			Position = OcclusionBoiteLocale.GetCenter()
		};
		AddChild(_occludeurObjetStatique);
	}

	private void RetirerOccludeurStatiqueObjet()
	{
		if (_occludeurObjetStatique == null)
			return;
		if (GodotObject.IsInstanceValid(_occludeurObjetStatique))
			_occludeurObjetStatique.QueueFree();
		_occludeurObjetStatique = null;
	}

	private Aabb CalculerBoiteOcclusionLocale()
	{
		bool any = false;
		Vector3 min = Vector3.Zero;
		Vector3 max = Vector3.Zero;
		void Inclure(Vector3 p)
		{
			if (!any)
			{
				min = max = p;
				any = true;
			}
			else
			{
				min = min.Min(p);
				max = max.Max(p);
			}
		}
		foreach (Node enfant in GetChildren())
		{
			if (enfant is MeshInstance3D mi && mi.Mesh != null)
			{
				Aabb b = mi.GetAabb();
				Inclure(b.Position);
				Inclure(b.Position + b.Size);
			}
			else if (enfant is CollisionShape3D cs && cs.Shape is BoxShape3D box)
			{
				Vector3 demi = box.Size * 0.5f;
				Vector3 p = cs.Position;
				Inclure(p - demi);
				Inclure(p + demi);
			}
		}
		if (!any)
			return new Aabb(Vector3.Zero, Vector3.One * 0.3f);
		return new Aabb(min, max - min);
	}

	private Monde_Client TrouverMondeClientPourOcclusion()
	{
		var arbre = GetTree();
		if (arbre == null)
			return null;
		foreach (Node n in arbre.GetNodesInGroup("MondeClient"))
		{
			if (n is Monde_Client mc)
				return mc;
		}
		return null;
	}

	public override void _ExitTree()
	{
		NotifierRetraitOcclusionObjetPose();
		base._ExitTree();
	}
}
