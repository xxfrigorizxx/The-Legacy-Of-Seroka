using Godot;
using System.Collections.Generic;

/// <summary>
/// État « pure data » d'un PNJ hors-chunk : vitaux, inventaire, carnet, cible de migration.
/// Avance virtuellement tant que le joueur n'a pas chargé sa zone.
/// </summary>
public sealed class PnjHumainEtatVirtuel
{
	public SexeJoueur Sexe;
	public float PosX, PosY, PosZ;
	public float CibleMigrX, CibleMigrZ;
	public float Faim;
	public float Stamina;
	public int[] PvMembres;
	public SlotInventaire[] Inventaire;
	public readonly List<string> Carnet = new();
	public string Nom = "";
	public bool Rebelle;
	public int ActesBons;
	public int ActesMauvais;
	public string SocieteNom = "";
	public bool ACibleMigration;
	public bool EnPauseCamp;
	public float CampX, CampZ;
	public int Intelligence = 10;
	public int XpAnalyse;

	public Vector3 Position => new Vector3(PosX, PosY, PosZ);
	public Vector2 CibleMigration => new Vector2(CibleMigrX, CibleMigrZ);

	public void DefinirPosition(Vector3 p)
	{
		PosX = p.X;
		PosY = p.Y;
		PosZ = p.Z;
	}

	public void DefinirCamp(Vector2 xz)
	{
		CampX = xz.X;
		CampZ = xz.Y;
		EnPauseCamp = true;
		ACibleMigration = false;
	}

	public void LeverCamp()
	{
		EnPauseCamp = false;
	}

	public void DefinirCibleMigration(Vector2 xz)
	{
		CibleMigrX = xz.X;
		CibleMigrZ = xz.Y;
		ACibleMigration = true;
		EnPauseCamp = false;
	}
}
