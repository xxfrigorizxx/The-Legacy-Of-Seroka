using Godot;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Chunk_Client : Node3D
{
	private static void MettreAJourInteractionGazon(Vector3 positionObservation)
	{
		if (_cacheMaterielGazonSymbiotique is not ShaderMaterial shaderMat) return;
	ulong frame = Engine.GetPhysicsFrames();
	if (_frameDerniereApplicationShader == frame) return;
	MettreAJourContactsGazonDepuisRigides(positionObservation, frame);
		for (int i = 0; i < MAX_CONTACTS_GAZON; i++)
	{
			shaderMat.SetShaderParameter($"contact_pos_{i}", _contactsGazonMonde[i]);
		shaderMat.SetShaderParameter($"contact_pow_{i}", _contactsGazonIntensite[i]);
	}
	_frameDerniereApplicationShader = frame;
	}

private static void MettreAJourContactsGazonDepuisRigides(Vector3 positionObservation, ulong frame)
	{
	MettreAJourDecroissanceTraces(frame);
		_rigidesActifsCeScan.Clear();
		float vitesseObservation = CalculerVitesseObservation(positionObservation, frame);
		float boostReveil = Mathf.Clamp(vitesseObservation / VITESSE_OBSERVATION_REVEIL_MAX, 0f, 1f);
		float rayonReveil = RAYON_REVEIL_INTERACTION_GAZON + BONUS_RAYON_REVEIL_SI_JOUEUR_RAPIDE * boostReveil;
		float rayonReveilCarre = rayonReveil * rayonReveil;

		for (int i = 0; i < MAX_CONTACTS_GAZON; i++)
	{
			_contactsGazonMonde[i] = new Vector3(0f, -99999f, 0f);
		_contactsGazonIntensite[i] = 0f;
	}

	// Joueur : contact principal, intensité guidée par masse/vitesse.
	float masseJoueur = 82f;
	float intensiteJoueur = CalculerIntensiteContact(masseJoueur, vitesseObservation);
	AjouterTraceContact(positionObservation, intensiteJoueur);

	// Le scan lourd des rigid bodies reste limité pour préserver les perfs.
	bool scanAutorise = _frameDernierScanContactsGazon == ulong.MaxValue || frame - _frameDernierScanContactsGazon >= 1;
	if (scanAutorise) _frameDernierScanContactsGazon = frame;

		SceneTree tree = Engine.GetMainLoop() as SceneTree;
	if (tree?.CurrentScene != null && scanAutorise)
	{
		var meilleurs = new List<(Node3D body, float dist2, float masse, float vitesseHoriz, RigidBody3D rigid)>(MAX_CONTACTS_RIGIDES_GAZON_SCAN);
		foreach (Node n in ObtenirTousLesNoeuds(tree.CurrentScene))
		{
			if (!EssayerConstruireContactCorps(n, positionObservation, rayonReveilCarre, out var body3D, out float dist2, out float masse, out float vitesseHoriz, out RigidBody3D rb))
				continue;
			if (meilleurs.Count < MAX_CONTACTS_RIGIDES_GAZON_SCAN)
			{
				meilleurs.Add((body3D, dist2, masse, vitesseHoriz, rb));
				continue;
			}
			int idxPlusLoin = 0;
			float plusLoin = meilleurs[0].dist2;
			for (int i = 1; i < meilleurs.Count; i++)
			{
				if (meilleurs[i].dist2 <= plusLoin) continue;
				plusLoin = meilleurs[i].dist2;
				idxPlusLoin = i;
			}
			if (dist2 < plusLoin) meilleurs[idxPlusLoin] = (body3D, dist2, masse, vitesseHoriz, rb);
		}

		for (int i = 0; i < meilleurs.Count && i < MAX_CONTACTS_RIGIDES_GAZON_SCAN; i++)
			{
			var contact = meilleurs[i];
			float intensite = CalculerIntensiteContact(contact.masse, contact.vitesseHoriz);
			AjouterTraceContact(contact.body.GlobalPosition, intensite);
			if (contact.rigid != null)
			{
				_rigidesActifsCeScan.Add(contact.rigid);
				AppliquerFreinageGazonSurRigidBody(contact.rigid, vitesseObservation);
			}
			}

		RestaurerRigidesHorsZone();
	}

	RemplirContactsDepuisTraces(positionObservation);
	}

	private static bool EssayerConstruireContactCorps(Node n, Vector3 positionObservation, float rayonReveilCarre,
		out Node3D body3D, out float dist2, out float masse, out float vitesseHoriz, out RigidBody3D rigid)
	{
		body3D = null;
		dist2 = 0f;
		masse = 0f;
		vitesseHoriz = 0f;
		rigid = null;

		if (n is RigidBody3D rb)
		{
			if (!rb.IsInsideTree() || !rb.Visible) return false;
			dist2 = rb.GlobalPosition.DistanceSquaredTo(positionObservation);
			if (dist2 > rayonReveilCarre) return false;
			body3D = rb;
			masse = Mathf.Max(0.2f, rb.Mass);
			vitesseHoriz = new Vector2(rb.LinearVelocity.X, rb.LinearVelocity.Z).Length();
			rigid = rb;
			return true;
		}

		if (n is CharacterBody3D cb)
		{
			if (!cb.IsInsideTree() || !cb.Visible) return false;
			dist2 = cb.GlobalPosition.DistanceSquaredTo(positionObservation);
			if (dist2 > rayonReveilCarre) return false;
			body3D = cb;
			masse = 82f; // masse gameplay du joueur/PNJ
			vitesseHoriz = new Vector2(cb.Velocity.X, cb.Velocity.Z).Length();
			return true;
		}

		return false;
	}

	private static float CalculerIntensiteContact(float masse, float vitesseHoriz)
	{
		float masseNorm = Mathf.Clamp(Mathf.Log(masse + 1f) / 4.0f, 0.12f, 1.25f);
		float vitesseNorm = Mathf.Clamp(vitesseHoriz / 8.5f, 0f, 1f);
		return Mathf.Clamp(0.22f + masseNorm * 0.95f + vitesseNorm * 0.55f, 0.18f, 1.6f);
	}

	private static void MettreAJourDecroissanceTraces(ulong frame)
	{
		if (_frameDerniereMajTraces == ulong.MaxValue)
		{
			_frameDerniereMajTraces = frame;
			return;
		}
		ulong dFrames = frame > _frameDerniereMajTraces ? frame - _frameDerniereMajTraces : 1UL;
		_frameDerniereMajTraces = frame;
		float dt = (float)dFrames / 60f;
		float perte = DECROISSANCE_TRACE_PAR_SECONDE * dt;
		for (int i = _tracesContactsGazon.Count - 1; i >= 0; i--)
		{
			var t = _tracesContactsGazon[i];
			t.Intensite -= perte;
			if (t.Intensite <= 0.01f) _tracesContactsGazon.RemoveAt(i);
			else _tracesContactsGazon[i] = t;
		}
	}

	private static void AjouterTraceContact(Vector3 posMonde, float intensite)
	{
		intensite = Mathf.Clamp(intensite, 0f, 1.6f);
		for (int i = 0; i < _tracesContactsGazon.Count; i++)
		{
			var t = _tracesContactsGazon[i];
			if (t.PosMonde.DistanceSquaredTo(posMonde) > 0.85f * 0.85f) continue;
			t.PosMonde = t.PosMonde.Lerp(posMonde, 0.5f);
			t.Intensite = Mathf.Max(t.Intensite, intensite);
			_tracesContactsGazon[i] = t;
			return;
		}
		if (_tracesContactsGazon.Count >= MAX_TRACES_CONTACT_GAZON)
		{
			int idxMin = 0;
			float minI = _tracesContactsGazon[0].Intensite;
			for (int i = 1; i < _tracesContactsGazon.Count; i++)
			{
				if (_tracesContactsGazon[i].Intensite >= minI) continue;
				minI = _tracesContactsGazon[i].Intensite;
				idxMin = i;
			}
			_tracesContactsGazon[idxMin] = new TraceContactGazon { PosMonde = posMonde, Intensite = intensite };
			return;
		}
		_tracesContactsGazon.Add(new TraceContactGazon { PosMonde = posMonde, Intensite = intensite });
	}

	private static void RemplirContactsDepuisTraces(Vector3 positionObservation)
	{
		_contactsGazonMonde[0] = positionObservation;
		_contactsGazonIntensite[0] = 1.60f;
		var retenus = new List<TraceContactGazon>(MAX_CONTACTS_GAZON - 1);
		foreach (var t in _tracesContactsGazon)
		{
			if (t.Intensite <= 0.01f) continue;
			if (t.PosMonde.DistanceSquaredTo(positionObservation) < 0.1f * 0.1f) continue;
			if (retenus.Count < MAX_CONTACTS_GAZON - 1)
			{
				retenus.Add(t);
				continue;
			}
			int idxFaible = 0;
			float scoreFaible = retenus[0].Intensite;
			for (int i = 1; i < retenus.Count; i++)
			{
				if (retenus[i].Intensite >= scoreFaible) continue;
				scoreFaible = retenus[i].Intensite;
				idxFaible = i;
			}
			if (t.Intensite > scoreFaible) retenus[idxFaible] = t;
		}
		for (int i = 0; i < retenus.Count; i++)
		{
			_contactsGazonMonde[i + 1] = retenus[i].PosMonde;
			_contactsGazonIntensite[i + 1] = retenus[i].Intensite;
		}
	}

private static bool EstCorpsAuSol(PhysicsBody3D body)
	{
		if (body is CharacterBody3D cb && cb.IsOnFloor()) return true;
		var monde = body.GetWorld3D();
		if (monde == null) return false;
		var from = body.GlobalPosition + new Vector3(0f, 0.2f, 0f);
		var to = body.GlobalPosition + new Vector3(0f, -1.35f, 0f);
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };
		var hit = monde.DirectSpaceState.IntersectRay(query);
		return hit.Count > 0;
	}

	private static float CalculerVitesseObservation(Vector3 positionObservation, ulong frame)
	{
		if (float.IsNaN(_dernierePositionObservation.X) || _frameDerniereObservation == ulong.MaxValue)
		{
			_dernierePositionObservation = positionObservation;
			_frameDerniereObservation = frame;
			return 0f;
		}
		ulong dFrames = frame > _frameDerniereObservation ? frame - _frameDerniereObservation : 1UL;
		float distance = positionObservation.DistanceTo(_dernierePositionObservation);
		_dernierePositionObservation = positionObservation;
		_frameDerniereObservation = frame;
		// Approximation : vitesse en unités/secondes en supposant ~60 Hz physique.
		return distance * (60f / Mathf.Max(1f, (float)dFrames));
	}

	private static void AppliquerFreinageGazonSurRigidBody(RigidBody3D rb, float vitesseObservation)
	{
		if (!_dampBaseRigides.ContainsKey(rb))
			_dampBaseRigides[rb] = rb.LinearDamp;

		float vitesseHoriz = new Vector2(rb.LinearVelocity.X, rb.LinearVelocity.Z).Length();
		float t = Mathf.Clamp(vitesseHoriz / 7.5f, 0f, 1f);
		// Petites masses : plus de résistance de l'herbe. Grosses masses : moins de freinage.
		float facteurMasse = Mathf.Clamp(1.25f - Mathf.Log(rb.Mass + 1f) * 0.35f, 0.45f, 1.4f);
		float supplement = Mathf.Lerp(FREINAGE_GAZON_MIN, FREINAGE_GAZON_MAX, t) * facteurMasse;
		rb.LinearDamp = _dampBaseRigides[rb] + supplement;
		if (rb.Sleeping && (vitesseHoriz > 0.12f || vitesseObservation > 8.5f))
			rb.Sleeping = false;
	}

	private static void RestaurerRigidesHorsZone()
	{
		if (_dampBaseRigides.Count == 0) return;
		var aRetirer = new List<RigidBody3D>();
		foreach (var kv in _dampBaseRigides)
		{
			RigidBody3D rb = kv.Key;
			if (rb == null || !rb.IsInsideTree())
			{
				aRetirer.Add(rb);
				continue;
			}
			if (_rigidesActifsCeScan.Contains(rb)) continue;
			rb.LinearDamp = kv.Value;
			float vitesseHoriz = new Vector2(rb.LinearVelocity.X, rb.LinearVelocity.Z).Length();
			if (vitesseHoriz < 0.05f) rb.Sleeping = true;
			aRetirer.Add(rb);
		}
		for (int i = 0; i < aRetirer.Count; i++)
			_dampBaseRigides.Remove(aRetirer[i]);
	}
}
