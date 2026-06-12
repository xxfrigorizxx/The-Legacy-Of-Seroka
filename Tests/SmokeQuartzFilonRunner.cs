using Godot;
using System.IO;
using System.Text;

/// <summary>
/// Smoke test headless : génère des tranches profondes et vérifie la présence de minerai quartz (ID 19) entre Y=-300 et Y=-100.
/// Lancement : godot --path . res://Tests/SmokeQuartzFilon.tscn --headless
/// </summary>
public partial class SmokeQuartzFilonRunner : Node
{
	private const int SeedTest = 424242;
	private const int TailleChunk = 16;
	private const int HauteurTranche = 100;
	private const int FondMondeY = -1000;
	private const int SeuilQuartzParTranche = 40;

	public override void _Ready()
	{
		int totalQuartz = 0;
		int totalPierre = 0;
		var log = new StringBuilder();
		log.AppendLine("SmokeQuartzFilon: démarrage");

		for (int coordY = -3; coordY <= -1; coordY++)
		{
			var chunk = new Chunk_Serveur(
				0, coordY, 0, TailleChunk, HauteurTranche, SeedTest,
				(_, _, _, _) => { }, _ => false, _ => { },
				generationAbysse: false, dossierChunksSauvegarde: "",
				profondeurEtendueActive: true, fondMondeY: FondMondeY);
			chunk.GenererDonneesVoxel();

			int quartz = CompterMateriel(chunk, 19);
			int pierre = CompterMateriel(chunk, 2);
			totalQuartz += quartz;
			totalPierre += pierre;
			log.AppendLine($"  tranche coordY={coordY} : quartz={quartz} pierre={pierre}");
		}

		bool ok = totalQuartz >= SeuilQuartzParTranche * 3;
		log.AppendLine($"TOTAL quartz={totalQuartz} pierre={totalPierre}");
		log.AppendLine(ok ? "SMOKE_QUARTZ_FILON_RESULT=OK" : "SMOKE_QUARTZ_FILON_RESULT=ECHEC");

		string chemin = ProjectSettings.GlobalizePath("res://artifacts/smoke_quartz_filon.log");
		Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);
		File.WriteAllText(chemin, log.ToString());
		GD.Print(log.ToString());
		GetTree().Quit(ok ? 0 : 1);
	}

	private static int CompterMateriel(Chunk_Serveur chunk, byte id)
	{
		var donnees = chunk.ObtenirDonneesPourClient();
		if (donnees?.MaterialsFlat == null)
			return 0;
		int compte = 0;
		foreach (byte m in donnees.MaterialsFlat)
			if (m == id)
				compte++;
		return compte;
	}
}
