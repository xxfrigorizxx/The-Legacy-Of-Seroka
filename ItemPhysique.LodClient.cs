using Godot;

/// <summary>LOD client pour butin / objets au sol : masque le rendu et coupe la collision hors rayon (sauvegarde intacte).</summary>
public partial class ItemPhysique : RigidBody3D
{
	private uint _coucheCollisionAvantLod = 1;
	private bool _lodCollisionCapture;
	private bool _lodMeshVisible = true;
	private bool _lodCollisionActive = true;

	/// <summary>Applique visibilité mesh + collision selon la distance joueur (meubles statiques ignorés).</summary>
	internal void AppliquerLodClientObjetAuSol(bool afficherMesh, bool activerCollision)
	{
		if (EstMeublePoseStatique(ID_Objet))
			return;
		if (!_lodCollisionCapture)
		{
			_coucheCollisionAvantLod = CollisionLayer != 0 ? CollisionLayer : 1u;
			_lodCollisionCapture = true;
		}
		if (_lodMeshVisible != afficherMesh)
		{
			_lodMeshVisible = afficherMesh;
			foreach (Node enfant in GetChildren())
			{
				if (enfant is MeshInstance3D mi)
					mi.Visible = afficherMesh;
				else if (enfant is Node3D n3d && enfant is not CollisionShape3D && enfant is not OccluderInstance3D)
					n3d.Visible = afficherMesh;
			}
		}
		if (_lodCollisionActive != activerCollision)
		{
			_lodCollisionActive = activerCollision;
			CollisionLayer = activerCollision ? _coucheCollisionAvantLod : 0u;
		}
	}

	internal void ReveillerLodClientPourInteraction()
	{
		AppliquerLodClientObjetAuSol(afficherMesh: true, activerCollision: true);
	}
}
