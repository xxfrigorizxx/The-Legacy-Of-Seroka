using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	private void InitialiserAffichageFaim3D()
	{
		if (!AfficherFaimAuDessusBovin)
			return;
		_labelFaim3D = GetNodeOrNull<Label3D>("UI_Faim");
		if (_labelFaim3D != null)
			return;

		_labelFaim3D = new Label3D
		{
			Name = "UI_Faim",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = false,
			FontSize = 36,
			OutlineSize = 8,
			PixelSize = 0.0026f,
			Modulate = new Color(0.76f, 1f, 0.76f, 1f)
		};
		AddChild(_labelFaim3D);
	}

	private void AssurerBarresUIDessusTete()
	{
		InitialiserAffichageFaim3D();
		InitialiserAffichageStamina3D();
		InitialiserAffichageVie3D();
		SupprimerLabelsUIDessusTeteSiDesactives();
	}

	/// <summary>Retire les labels 3D si les exports sont faux (ex. nœuds laissés dans la scène pour le debug).</summary>
	private void SupprimerLabelsUIDessusTeteSiDesactives()
	{
		if (!AfficherFaimAuDessusBovin)
		{
			Label3D f = GetNodeOrNull<Label3D>("UI_Faim");
			if (f != null && GodotObject.IsInstanceValid(f))
				f.QueueFree();
			_labelFaim3D = null;
		}
		if (!AfficherStaminaAuDessusBovin)
		{
			Label3D s = GetNodeOrNull<Label3D>("UI_Stamina");
			if (s != null && GodotObject.IsInstanceValid(s))
				s.QueueFree();
			_labelStamina3D = null;
		}
		if (!AfficherVieAuDessusBovin)
		{
			Label3D v = GetNodeOrNull<Label3D>("UI_Vie");
			if (v != null && GodotObject.IsInstanceValid(v))
				v.QueueFree();
			_labelVie3D = null;
		}
	}

	private void InitialiserAffichageStamina3D()
	{
		if (!AfficherStaminaAuDessusBovin)
			return;
		_labelStamina3D = GetNodeOrNull<Label3D>("UI_Stamina");
		if (_labelStamina3D != null)
			return;

		_labelStamina3D = new Label3D
		{
			Name = "UI_Stamina",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = false,
			FontSize = 34,
			OutlineSize = 8,
			PixelSize = 0.0024f,
			Modulate = new Color(0.48f, 0.79f, 1f, 1f)
		};
		AddChild(_labelStamina3D);
	}

	private void InitialiserAffichageVie3D()
	{
		if (!AfficherVieAuDessusBovin)
			return;
		_labelVie3D = GetNodeOrNull<Label3D>("UI_Vie");
		if (_labelVie3D != null)
			return;

		_labelVie3D = new Label3D
		{
			Name = "UI_Vie",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = false,
			FontSize = 34,
			OutlineSize = 8,
			PixelSize = 0.0024f,
			Modulate = new Color(1f, 0.35f, 0.35f, 1f)
		};
		AddChild(_labelVie3D);
	}

	private string ConstruireBarreRatio(float ratio, int segments = 10)
	{
		segments = Mathf.Clamp(segments, 4, 20);
		int pleins = Mathf.Clamp(Mathf.RoundToInt(ratio * segments), 0, segments);
		if (!_cacheBarresRatio.TryGetValue(segments, out var cacheSegment))
		{
			cacheSegment = new string[segments + 1];
			for (int i = 0; i <= segments; i++)
				cacheSegment[i] = "[" + new string('|', i) + new string('.', segments - i) + "]";
			_cacheBarresRatio[segments] = cacheSegment;
		}
		return cacheSegment[pleins];
	}

	private void MettreAJourAffichageStamina3D()
	{
		if (!AfficherStaminaAuDessusBovin || _labelStamina3D == null || !GodotObject.IsInstanceValid(_labelStamina3D))
		{
			MettreAJourAffichageVie3D();
			return;
		}
		float ratio = RatioStaminaCourant();
		int pct = Mathf.RoundToInt(ratio * 100f);
		_labelStamina3D.Text = $"Stamina {pct}% {ConstruireBarreRatio(ratio, 10)}";
		float baseY = Mathf.Max(0.6f, HauteurAffichageFaim * _geneTaille);
		_labelStamina3D.Position = new Vector3(0f, baseY + Mathf.Max(0.08f, DecalageVerticalBarreStamina), 0f);
		if (ratio <= 0.20f)
			_labelStamina3D.Modulate = new Color(1f, 0.39f, 0.39f, 1f);
		else if (ratio <= 0.50f)
			_labelStamina3D.Modulate = new Color(1f, 0.87f, 0.37f, 1f);
		else
			_labelStamina3D.Modulate = new Color(0.48f, 0.79f, 1f, 1f);
		MettreAJourAffichageVie3D();
	}

	private void MettreAJourAffichageVie3D()
	{
		if (!AfficherVieAuDessusBovin || _labelVie3D == null || !GodotObject.IsInstanceValid(_labelVie3D))
			return;
		float ratio = _vieMaxActuelle > 0.001f ? Mathf.Clamp(_vieCourante / _vieMaxActuelle, 0f, 1f) : 0f;
		int pct = Mathf.RoundToInt(ratio * 100f);
		_labelVie3D.Text = $"Vie {pct}% {ConstruireBarreRatio(ratio, 10)}";
		float baseY = Mathf.Max(0.6f, HauteurAffichageFaim * _geneTaille);
		float yStamina = baseY + Mathf.Max(0.08f, DecalageVerticalBarreStamina);
		_labelVie3D.Position = new Vector3(0f, yStamina + Mathf.Max(0.08f, DecalageVerticalBarreVie), 0f);
		if (ratio <= 0.20f)
			_labelVie3D.Modulate = new Color(1f, 0.28f, 0.28f, 1f);
		else if (ratio <= 0.50f)
			_labelVie3D.Modulate = new Color(1f, 0.76f, 0.32f, 1f);
		else
			_labelVie3D.Modulate = new Color(0.48f, 1f, 0.48f, 1f);
	}

	private void MettreAJourAffichageFaim3D()
	{
		if (!AfficherFaimAuDessusBovin || _labelFaim3D == null || !GodotObject.IsInstanceValid(_labelFaim3D))
		{
			MettreAJourAffichageStamina3D();
			return;
		}

		float ratio = _faimMaxActuelle > 0.001f ? Mathf.Clamp(_faimCourante / _faimMaxActuelle, 0f, 1f) : 0f;
		int pct = Mathf.RoundToInt(ratio * 100f);
		string infoTroupeau = "";
		if (_deblocageAffichageTroupeau)
		{
			ulong now = Time.GetTicksMsec();
			ulong intervalle = (ulong)Mathf.Clamp(IntervalleMajCohesionUiSec * 1000f, 50f, 1000f);
			if (_tickDerniereMajCohesionUi == 0 || now - _tickDerniereMajCohesionUi >= intervalle)
			{
				_cohesionUiCachee = CalculerRatioCohesionTroupeau();
				_tickDerniereMajCohesionUi = now;
			}
			int cohesion = Mathf.RoundToInt(_cohesionUiCachee * 100f);
			infoTroupeau = $" | Troupe {cohesion}%";
		}
		_labelFaim3D.Text = $"Faim {pct}%{infoTroupeau}";
		_labelFaim3D.Position = new Vector3(0f, Mathf.Max(0.6f, HauteurAffichageFaim * TailleEffective), 0f);

		if (ratio <= 0.25f)
			_labelFaim3D.Modulate = new Color(1f, 0.34f, 0.34f, 1f);
		else if (ratio <= 0.50f)
			_labelFaim3D.Modulate = new Color(1f, 0.86f, 0.34f, 1f);
		else
			_labelFaim3D.Modulate = new Color(0.76f, 1f, 0.76f, 1f);
		MettreAJourAffichageStamina3D();
	}
}
