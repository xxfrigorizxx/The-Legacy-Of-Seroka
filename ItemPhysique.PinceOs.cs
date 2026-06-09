using Godot;
using System;

public partial class ItemPhysique
{
	public const string PrefixGenomePinceOsCharge = "PINCEOS:";

	public static bool EstPinceOsPeutSaisirResultat(SlotInventaire s) =>
		!s.EstVide && (s.ID == Joueur.IdObjetBolCeramique || s.ID == Joueur.IdObjetMouleCeramique);

	public static bool EstPinceOsPorteObjet(SlotInventaire pince) =>
		!pince.EstVide
		&& pince.ID == Joueur.IdObjetPinceOs
		&& (pince.GenomeAssemblage ?? "").StartsWith(PrefixGenomePinceOsCharge, StringComparison.Ordinal);

	public static bool EstPinceOsPorteBol(SlotInventaire pince) =>
		EstPinceOsPorteObjet(pince)
		&& EssayerLireObjetPortePinceOs(pince, out SlotInventaire objet)
		&& objet.ID == Joueur.IdObjetBolCeramique;

	public static bool EstPinceOsPorteMouleCeramique(SlotInventaire pince) =>
		EstPinceOsPorteObjet(pince)
		&& EssayerLireObjetPortePinceOs(pince, out SlotInventaire objet)
		&& objet.ID == Joueur.IdObjetMouleCeramique;

	public static bool EssayerLireObjetPortePinceOs(SlotInventaire pince, out SlotInventaire objet)
	{
		objet = new SlotInventaire();
		string g = pince.GenomeAssemblage ?? "";
		if (!g.StartsWith(PrefixGenomePinceOsCharge, StringComparison.Ordinal))
			return false;
		objet = DecoderSlotCompactPinceOs(g.Substring(PrefixGenomePinceOsCharge.Length));
		return !objet.EstVide;
	}

	public static void ChargerObjetSurPinceOs(ref SlotInventaire pince, SlotInventaire objet)
	{
		if (pince.ID != Joueur.IdObjetPinceOs || objet.EstVide || !EstPinceOsPeutSaisirResultat(objet))
			return;
		if (EstPinceOsPorteObjet(pince))
			return;
		SlotInventaire copie = objet;
		copie.Quantite = 1;
		pince.GenomeAssemblage = $"{PrefixGenomePinceOsCharge}{EncoderSlotCompactPinceOs(copie)}";
	}

	public static void ViderChargePinceOs(ref SlotInventaire pince)
	{
		if (pince.ID != Joueur.IdObjetPinceOs)
			return;
		pince.GenomeAssemblage = "";
	}

	public static string EncoderSlotCompactPinceOs(SlotInventaire s)
	{
		if (s.EstVide)
			return "";
		string baseEnc = $"{s.ID},{Joueur.ObtenirQuantiteSlot(s)},{s.IndexBotanique},{s.IndexChimique},{s.IndexMorphologique}";
		if (!string.IsNullOrEmpty(s.GenomeAssemblage))
			return $"{baseEnc}~{s.GenomeAssemblage}";
		return baseEnc;
	}

	public static SlotInventaire DecoderSlotCompactPinceOs(string part)
	{
		if (string.IsNullOrEmpty(part))
			return new SlotInventaire();
		string[] partiesGenome = part.Split('~', 2);
		string[] m = partiesGenome[0].Split(',');
		if (m.Length < 2 || !int.TryParse(m[0], out int id) || id <= 0)
			return new SlotInventaire();
		int.TryParse(m[1], out int q);
		byte.TryParse(m.Length > 2 ? m[2] : "0", out byte bot);
		int.TryParse(m.Length > 3 ? m[3] : "0", out int chi);
		int.TryParse(m.Length > 4 ? m[4] : "0", out int mor);
		string genome = partiesGenome.Length > 1 ? partiesGenome[1] : "";
		if (id == Joueur.IdObjetBolCeramique
			&& chi == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique
			&& string.IsNullOrEmpty(genome))
		{
			genome = $"{PrefixGenomeBolCeramique}{chi}:0";
		}
		if (id == Joueur.IdObjetMouleCeramique
			&& chi == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique
			&& string.IsNullOrEmpty(genome))
		{
			genome = $"{PrefixGenomeMouleCeramique}{chi}:0";
		}
		return new SlotInventaire
		{
			ID = id,
			Quantite = Mathf.Max(1, q),
			IndexBotanique = bot,
			IndexChimique = chi,
			IndexMorphologique = mor,
			GenomeAssemblage = genome
		};
	}
}
