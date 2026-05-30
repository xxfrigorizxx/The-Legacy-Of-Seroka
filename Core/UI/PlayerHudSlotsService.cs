using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private void MettreAJourSlotUI(Panel slot, SlotInventaire slotData, bool selectionne)
    {
        if (slot == null || !GodotObject.IsInstanceValid(slot))
            return;

        int cleStyle = HashCode.Combine(slotData.ID, slotData.IndexChimique, selectionne ? 1 : 0);
        if (!_cacheStyleSlotsHud.TryGetValue(cleStyle, out StyleBoxFlat style) || !GodotObject.IsInstanceValid(style))
        {
            style = CreerStyleSlotHud(slotData, selectionne);
            _cacheStyleSlotsHud[cleStyle] = style;
        }

        slot.AddThemeStyleboxOverride("panel", style);
    }

    private StyleBoxFlat CreerStyleSlotHud(SlotInventaire slotData, bool selectionne)
    {
        int idMatiere = slotData.ID;
        var style = new StyleBoxFlat();
        if (idMatiere == 0)
            style.BgColor = new Color(0.2f, 0.2f, 0.2f);
        else if (idMatiere == 1)
            style.BgColor = new Color(0.5f, 0.3f, 0.1f); // Marron (Terre)
        else if (idMatiere == 2)
            style.BgColor = new Color(0.4f, 0.4f, 0.4f); // Gris foncÃ© (Roche)
        else if (idMatiere == 3)
            style.BgColor = new Color(0.9f, 0.8f, 0.5f); // Jaune pÃ¢le (Sable)
        else if (idMatiere == 4)
            style.BgColor = new Color(0.9f, 0.9f, 0.9f); // Blanc (Neige)
        else if (idMatiere == 5)
            style.BgColor = new Color(0.9f, 0.95f, 1f); // Blanc bleutÃ© (Neige/Glace)
        else if (idMatiere == 6)
            style.BgColor = new Color(0.6f, 0.45f, 0.25f); // Terre aride (Arid earth)
        else if (idMatiere == 7)
            style.BgColor = new Color(0.35f, 0.25f, 0.15f); // Boue (Mud)
        else if (idMatiere == 8)
            style.BgColor = new Color(0.3f, 0.5f, 0.2f); // Terre tropicale
        else if (idMatiere == 9)
            style.BgColor = new Color(0.7f, 0.75f, 0.8f); // Terre gelÃ©e
        else if (ItemPhysique.EstIdRocheMatiere(idMatiere))
        {
            int idx = ItemPhysique.IndexChimiqueDepuisIdRoche(idMatiere);
            style.BgColor = ItemPhysique.TableGeologique[idx].CouleurBase;
        }
        else if (idMatiere == 999)
            style.BgColor = new Color(0.1f, 0.8f, 0.2f); // Vert (Objet/Buisson)
        else if (idMatiere == 30)
            style.BgColor = new Color(0.4f, 0.25f, 0.15f); // Marron (BÃ»che)
        else if (idMatiere == 32)
            style.BgColor = new Color(0.5f, 0.35f, 0.2f); // Marron clair (BÃ¢ton)
        else if (idMatiere == 34)
            style.BgColor = new Color(0.2f, 0.55f, 0.15f); // Vert feuillage
        else if (idMatiere == 100)
            style.BgColor = new Color(0.85f, 0.65f, 0.2f); // Or (outil forgÃ© CAO)
        else if (idMatiere == 105)
        {
            int ir = Mathf.Clamp(slotData.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            style.BgColor = ItemPhysique.TableGeologique[ir].CouleurBase.Lerp(new Color(0.35f, 0.28f, 0.2f), 0.35f);
        }
        else if (idMatiere == 106 || idMatiere == IdObjetHachePierreTier1)
        {
            int ir = Mathf.Clamp(slotData.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            style.BgColor = ItemPhysique.TableGeologique[ir].CouleurBase.Lerp(new Color(0.42f, 0.32f, 0.18f), 0.28f);
        }
        else if (idMatiere == IdObjetLancePierreTier0)
        {
            int ir = Mathf.Clamp(slotData.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            style.BgColor = ItemPhysique.TableGeologique[ir].CouleurBase.Lerp(new Color(0.46f, 0.34f, 0.2f), 0.24f);
        }
        else if (idMatiere == IdObjetFauxPierreTier0)
        {
            int ir = Mathf.Clamp(slotData.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            style.BgColor = ItemPhysique.TableGeologique[ir].CouleurBase.Lerp(new Color(0.4f, 0.32f, 0.22f), 0.3f);
        }
        else if (idMatiere == IdObjetCarnetSavoir)
            style.BgColor = new Color(0.39f, 0.24f, 0.14f);
        else
            style.BgColor = new Color(0.4f, 0.4f, 0.6f); // Violet (Autre)

        if (selectionne)
        {
            style.BorderColor = new Color(1f, 0.9f, 0.2f);
            style.SetBorderWidthAll(3);
        }
        return style;
    }

    private void MettreAJourLibellesNomsHud()
    {
        bool preview3dG = !MainGauche.EstVide && InventaireSlotAunVisuel3D(MainGauche)
            && _viewportSlotGauche != null && GodotObject.IsInstanceValid(_viewportSlotGauche) && _viewportSlotGauche.Visible;
        bool preview3dD = !MainDroite.EstVide && InventaireSlotAunVisuel3D(MainDroite)
            && _viewportSlotDroite != null && GodotObject.IsInstanceValid(_viewportSlotDroite) && _viewportSlotDroite.Visible;
        if (_lblHudNomMainG != null)
        {
            string n = Atlas_Matiere.ObtenirNomObjet(MainGauche);
            _lblHudNomMainG.Text = MainGauche.EstVide ? "" : n;
            _lblHudNomMainG.Visible = !MainGauche.EstVide && !preview3dG && !string.IsNullOrEmpty(n);
        }
        if (_lblHudNomMainD != null)
        {
            string n = Atlas_Matiere.ObtenirNomObjet(MainDroite);
            _lblHudNomMainD.Text = MainDroite.EstVide ? "" : n;
            _lblHudNomMainD.Visible = !MainDroite.EstVide && !preview3dD && !string.IsNullOrEmpty(n);
        }
    }

    /// <summary>SubViewport + MeshInstance3D dans chaque slot pour afficher la pierre exacte en 2D.</summary>
    private void CreerPreviewsInventaire3D()
    {
        _viewportSlotGauche = CreerSubViewportPourSlot(_slotGauche, out _meshPreviewGauche);
        _viewportSlotDroite = CreerSubViewportPourSlot(_slotDroite, out _meshPreviewDroite);
        if (_slotCarnet != null && GodotObject.IsInstanceValid(_slotCarnet))
            _viewportSlotCarnet = CreerSubViewportPourSlot(_slotCarnet, out _meshPreviewCarnet);
    }

    private SubViewportContainer CreerSubViewportPourSlot(Panel slot, out MeshInstance3D meshPreview)
    {
        var container = new SubViewportContainer();
        container.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        container.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        container.Stretch = true;
        slot.AddChild(container);
        slot.MoveChild(container, slot.GetChildCount() - 1);

        var viewport = new SubViewport();
        viewport.Size = new Vector2I(64, 64);
        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible;
        viewport.World3D = new World3D();
        viewport.TransparentBg = true;
        container.AddChild(viewport);

        var cam = new Camera3D();
        cam.SetOrthogonal(0.5f, 0.01f, 10f);
        cam.Position = new Vector3(0, 0, 1.2f);
        cam.Current = true;
        viewport.AddChild(cam);

        var meshNode = new MeshInstance3D();
        meshNode.Position = Vector3.Zero;
        meshNode.RotationDegrees = new Vector3(-20, 25, 0);
        viewport.AddChild(meshNode);
        meshPreview = meshNode;

        var light = new DirectionalLight3D();
        light.RotationDegrees = new Vector3(-45, 30, 0);
        light.Set("sky_mode", 1); // LightOnly : pas de disque dans le ciel (Ã©vite 2e soleil blanc dans SubViewport)
        viewport.AddChild(light);

        return container;
    }
}
