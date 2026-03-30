using Godot;

/// <summary>Sculpture procédurale des roches / bois en main (affûtage). Stub minimal si la logique complète n’est pas dans le dépôt.</summary>
public static class SculpteurPrimitif
{
	/// <param name="meshActuel">Mesh source (cache ou éclat).</param>
	/// <param name="directionVersCamera">Axe de travail relatif à l’objet en main.</param>
	/// <param name="id">ID matière (roche, bois…).</param>
	/// <param name="affutageLateral">True = passe latérale, false = pointe.</param>
	/// <returns>Nouveau mesh ou null si échec.</returns>
	public static Mesh TaillerRoche(Mesh meshActuel, Vector3 directionVersCamera, int id, bool affutageLateral)
	{
		if (meshActuel == null) return null;
		// Copie distincte pour que l’inventaire / le rigide ne partagent pas la même ressource mesh.
		var copie = meshActuel.Duplicate();
		return copie as Mesh;
	}
}
