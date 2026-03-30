using Godot;

/// <summary>Ébauche CAO / modelage : API attendue par <see cref="Joueur"/>. Ouverture plus liée au clavier (K retiré) ; fermer avec Échap quand visible.</summary>
public partial class Modelisateur_UI : CanvasLayer
{
	public bool EstOuvert { get; private set; }

	/// <summary>True si un champ texte du CAO a le focus (évite de fermer avec Q pendant la saisie).</summary>
	public bool SaisieTexteEnCours => false;

	private Joueur _joueur;

	public override void _Ready()
	{
		Layer = 99;
		Visible = false;
		EstOuvert = false;
	}

	public void Initialiser(Joueur joueur)
	{
		_joueur = joueur;
	}

	public void BasculerVisibilite()
	{
		EstOuvert = !EstOuvert;
		Visible = EstOuvert;
		if (EstOuvert)
			Input.MouseMode = Input.MouseModeEnum.Visible;
		else if (_joueur != null)
			Input.MouseMode = Input.MouseModeEnum.Captured;
	}
}
