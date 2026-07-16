using Godot;
using System.Collections.Generic;

/// <summary>Oracle roches au sol pour PNJ (stase, file d'attente, items physiques).</summary>
public partial class Monde_Serveur : Node
{
	private const int TailleRocheMaxPnj = 2;

	private void AssurerRochesDetectablesAutour(Vector3 point, float rayon)
	{
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(point.X - rayon, point.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(point.X + rayon, point.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(point.X, point.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(point.X, point.Z + rayon, TailleChunk).Y;
		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
				AssurerRochesSurChunk(new Vector2I(cx, cz), 0);
	}

	private void AssurerRochesSurChunk(Vector2I coord, int coordY)
	{
		Vector3I cle = new Vector3I(coord.X, coordY, coord.Y);
		if (_rochesEnStase.ContainsKey(cle))
			return;
		var chunk = ObtenirOuCreerChunk(coord, coordY);
		if (chunk == null || !chunk.EstPret)
			return;
		var liste = CollecterPositionsEnsemencement(coord, chunk, TailleChunk);
		if (liste.Count > 0)
			MettreRochesEnStase(coord, coordY, liste);
	}

	public bool EssayerDetecterRochePourPnj(Vector3 point, float rayon, out Vector3 posRoche, out int idObjet, out int indexMorph, out int indexTaille)
	{
		posRoche = Vector3.Zero;
		idObjet = 0;
		indexMorph = 0;
		indexTaille = 0;
		AssurerRochesDetectablesAutour(point, rayon);
		float meilleureDist2 = rayon * rayon;
		bool trouve = false;
		Vector3 meilleurePos = Vector3.Zero;
		int meilleurId = 0, meilleurMorph = 0, meilleurTaille = 0;

		void Evaluer(Vector3 pos, int id, int morph, int taille)
		{
			if (!ItemPhysique.EstIdRocheMatiere(id) || taille > TailleRocheMaxPnj)
				return;
			float d2 = pos.DistanceSquaredTo(point);
			if (!trouve || d2 < meilleureDist2)
			{
				trouve = true;
				meilleureDist2 = d2;
				meilleurePos = pos;
				meilleurId = id;
				meilleurMorph = morph < 0 ? 0 : morph;
				meilleurTaille = taille;
			}
		}

		if (_parentPourBlocsChutants != null)
		{
			foreach (Node child in _parentPourBlocsChutants.GetChildren())
			{
				if (child is not ItemPhysique item || !ItemPhysique.EstIdRocheMatiere(item.ID_Objet))
					continue;
				Evaluer(item.GlobalPosition, item.ID_Objet, item.IndexCacheMemoire, item.IndexTailleRoche);
			}
		}

		foreach (KeyValuePair<Vector3I, List<(Vector3 pos, int id, int indexCache, int indexChimique)>> kv in _rochesEnStase)
		{
			foreach ((Vector3 pos, int id, int indexCache, int indexChimique) r in kv.Value)
				Evaluer(r.pos, r.id, r.indexCache, r.indexChimique);
		}

		foreach ((Vector3 pos, int id, int indexCache, int indexChimique) r in _filePierresAInstancier)
			Evaluer(r.pos, r.id, r.indexCache, r.indexChimique);

		if (!trouve)
			return false;
		posRoche = meilleurePos;
		idObjet = meilleurId;
		indexMorph = meilleurMorph;
		indexTaille = meilleurTaille;
		return true;
	}

	public bool RamasserRochePourPnj(Vector3 point, float rayon, out SlotInventaire slot)
	{
		slot = default;
		if (!EssayerDetecterRochePourPnj(point, rayon, out Vector3 pos, out int id, out int morph, out int taille))
			return false;

		// Priorité : roche physique déjà instanciée.
		if (_parentPourBlocsChutants != null)
		{
			ItemPhysique meilleur = null;
			float meilleureDist2 = rayon * rayon;
			foreach (Node child in _parentPourBlocsChutants.GetChildren())
			{
				if (child is not ItemPhysique item || !ItemPhysique.EstIdRocheMatiere(item.ID_Objet))
					continue;
				if (item.IndexTailleRoche > TailleRocheMaxPnj)
					continue;
				float d2 = item.GlobalPosition.DistanceSquaredTo(point);
				if (d2 > meilleureDist2)
					continue;
				meilleureDist2 = d2;
				meilleur = item;
			}
			if (meilleur != null && meilleur.GlobalPosition.DistanceTo(pos) < 0.6f)
			{
				slot = new SlotInventaire
				{
					ID = meilleur.ID_Objet,
					IndexMorphologique = meilleur.IndexCacheMemoire,
					IndexChimique = ItemPhysique.IndexChimiqueDepuisIdRoche(meilleur.ID_Objet),
					IndexTaille = meilleur.IndexTailleRoche,
					Quantite = 1
				};
				meilleur.QueueFree();
				return true;
			}
		}

		if (EssayerRetirerRocheStaseOuFile(pos, out id, out morph, out taille))
		{
			slot = new SlotInventaire
			{
				ID = id,
				IndexMorphologique = morph < 0 ? 0 : morph,
				IndexChimique = ItemPhysique.IndexChimiqueDepuisIdRoche(id),
				IndexTaille = taille,
				Quantite = 1
			};
			return true;
		}
		return false;
	}

	private bool EssayerRetirerRocheStaseOuFile(Vector3 pos, out int id, out int morph, out int taille)
	{
		id = 0;
		morph = 0;
		taille = 0;
		const float tol = 0.55f;
		foreach (KeyValuePair<Vector3I, List<(Vector3 pos, int id, int indexCache, int indexChimique)>> kv in _rochesEnStase)
		{
			List<(Vector3 pos, int id, int indexCache, int indexChimique)> liste = kv.Value;
			for (int i = 0; i < liste.Count; i++)
			{
				if (liste[i].pos.DistanceTo(pos) > tol)
					continue;
				var r = liste[i];
				id = r.id;
				morph = r.indexCache;
				taille = r.indexChimique;
				liste.RemoveAt(i);
				return true;
			}
		}

		var file = new Queue<(Vector3 pos, int id, int indexCache, int indexChimique)>();
		while (_filePierresAInstancier.Count > 0)
		{
			var r = _filePierresAInstancier.Dequeue();
			if (r.pos.DistanceTo(pos) <= tol && id == 0)
			{
				id = r.id;
				morph = r.indexCache;
				taille = r.indexChimique;
				while (file.Count > 0)
					_filePierresAInstancier.Enqueue(file.Dequeue());
				return true;
			}
			file.Enqueue(r);
		}
		while (file.Count > 0)
			_filePierresAInstancier.Enqueue(file.Dequeue());
		return false;
	}
}
