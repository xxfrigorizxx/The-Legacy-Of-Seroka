using Godot;

/// <summary>Variante serveur dédiée à la dimension Abysse (génération insulaire).</summary>
public partial class Gestionnaire_Abysse : Monde_Serveur
{
	public override void _Ready()
	{
		base._Ready();
		NomDimension = "Dimension_Abysse";
		ActiverGenerationAbysse = true;
	}
}
