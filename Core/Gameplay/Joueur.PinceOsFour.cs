using Godot;

public partial class Joueur
{
	/// <summary>Pince en os dans la main active uniquement (slot sélectionné).</summary>
	public bool EssayerObtenirPinceOsEnMain(out bool mainGauche)
	{
		mainGauche = MainGaucheEstActive;
		SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
		return mainActive.ID == IdObjetPinceOs;
	}

	public ref SlotInventaire RefMainPinceOs(bool mainGauche) =>
		ref (mainGauche ? ref MainGauche : ref MainDroite);

	/// <summary>Dépose l'objet porté par la pince dans le premier slot résultat libre du four ouvert.</summary>
	public bool EssayerDeposerDepuisPinceSurPremierSlotResultatFour(bool mainGauchePince)
	{
		if (!FourTorchieOuvertValide())
			return false;
		for (int i = ItemPhysique.FourTorchiePremierSlotResultat;
			i < ItemPhysique.FourTorchiePremierSlotResultat + ItemPhysique.FourTorchieNbCuisson;
			i++)
		{
			ref SlotInventaire slot = ref RefSlotFourTorchie(i);
			if (EssayerDeposerBolDepuisPinceSurSlotFour(ref slot, mainGauchePince))
				return true;
		}
		return false;
	}

	/// <summary>Clic droit monde : dépose le bol (four ouvert → slot résultat, sinon au sol). Les pinces restent en main.</summary>
	public bool EssayerDeposerChargePinceEnMain(bool mainGauchePince)
	{
		ref SlotInventaire pince = ref RefMainPinceOs(mainGauchePince);
		if (!ItemPhysique.EstPinceOsPorteObjet(pince))
			return false;
		if (!ItemPhysique.EssayerLireObjetPortePinceOs(pince, out SlotInventaire objet))
			return false;

		if (FourTorchieOuvertValide() && EssayerDeposerDepuisPinceSurPremierSlotResultatFour(mainGauchePince))
			return true;

		_rayon.ForceRaycastUpdate();
		if (!_rayon.IsColliding())
			return false;
		Vector3 pointImpact = _rayon.GetCollisionPoint();
		Vector3 normaleImpact = _rayon.GetCollisionNormal();
		Vector3 pointDeChute = pointImpact + normaleImpact * 0.1f;
		if (GlobalPosition.DistanceTo(pointDeChute) < 0.55f)
			return false;

		Node3D nePose = CreerBlocPose(pointDeChute, objet);
		if (nePose == null)
			return false;
		ItemPhysique.ViderChargePinceOs(ref pince);
		if (!Engine.IsEditorHint())
			SauvegarderEtatPersistantMonde(GetTree());
		return true;
	}

	public bool EssayerDeposerBolDepuisPinceSurSlotFour(ref SlotInventaire slotFour, bool mainGauchePince)
	{
		ref SlotInventaire pince = ref RefMainPinceOs(mainGauchePince);
		if (!ItemPhysique.EstPinceOsPorteObjet(pince))
			return false;
		if (!ItemPhysique.EssayerLireObjetPortePinceOs(pince, out SlotInventaire objet))
			return false;

		if (slotFour.EstVide)
		{
			slotFour = objet;
			ItemPhysique.ViderChargePinceOs(ref pince);
			return true;
		}

		if (!SontEmpilables(slotFour, objet))
			return false;
		int maxPile = Mathf.Max(1, ObtenirPileMax(slotFour));
		if (ObtenirQuantiteSlot(slotFour) >= maxPile)
			return false;
		slotFour.Quantite = ObtenirQuantiteSlot(slotFour) + 1;
		ItemPhysique.ViderChargePinceOs(ref pince);
		return true;
	}

	public bool EssayerSaisirResultatFourAvecPince(ref SlotInventaire slotFour, bool mainGauchePince)
	{
		if (slotFour.EstVide || !ItemPhysique.EstPinceOsPeutSaisirResultat(slotFour))
			return false;

		ref SlotInventaire pince = ref RefMainPinceOs(mainGauchePince);
		if (ItemPhysique.EstPinceOsPorteObjet(pince))
			return false;

		SlotInventaire prise = slotFour;
		prise.Quantite = 1;
		int q = ObtenirQuantiteSlot(slotFour);
		if (q <= 1)
			slotFour = new SlotInventaire();
		else
			slotFour.Quantite = q - 1;

		ItemPhysique.ChargerObjetSurPinceOs(ref pince, prise);
		return true;
	}
}
