using Godot;

/// <summary>Ordre émis par le chef de société — les membres obéissants l'exécutent ; sans ordre actif, chaque PNJ est libre.</summary>
public sealed class OrdreChefPnj
{
	public enum TypeOrdre { RamenerBaies }

	private static int _prochainId;

	public int Id { get; }
	public TypeOrdre Type { get; init; }
	public int QuantiteCible { get; init; }
	public float DureeMaxSec { get; init; }
	public float TempsEcoule { get; set; }
	public bool Actif { get; set; } = true;

	private OrdreChefPnj(int id) => Id = id;

	public static OrdreChefPnj CreerRamenerBaies(int quantite, float dureeSec)
	{
		return new OrdreChefPnj(++_prochainId)
		{
			Type = TypeOrdre.RamenerBaies,
			QuantiteCible = Mathf.Max(1, quantite),
			DureeMaxSec = Mathf.Max(30f, dureeSec)
		};
	}

	public bool EstComplete(int baiesDeposees) => baiesDeposees >= QuantiteCible;
	public bool EstExpire => TempsEcoule >= DureeMaxSec;
	public float TempsRestantSec => Mathf.Max(0f, DureeMaxSec - TempsEcoule);

	public string ResumeCourt()
		=> Type == TypeOrdre.RamenerBaies ? $"Ramener {QuantiteCible} baies" : "Ordre";
}
