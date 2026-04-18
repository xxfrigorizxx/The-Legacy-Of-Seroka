using Godot;

public partial class Joueur
{
    // Le squelette ne répond plus au joueur directement : canal conversation désactivé.
    public bool ChatInGameOuvert() => false;
    public void InitialiserChatInGame() { }
    public bool EssayerBasculerChatInGameDepuisInput(InputEvent @event) => false;
    public void OuvrirChatInGame() { }
    public void FermerChatInGame() { }

    public static void AlerteSqueletteBoiteNoire(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        GD.Print($"ZERO-K Squelette : {message}");
    }
}
