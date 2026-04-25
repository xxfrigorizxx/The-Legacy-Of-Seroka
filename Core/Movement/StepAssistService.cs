using Godot;

public static class StepAssistService
{
    public static bool TryApplyStepAssist(
        CharacterBody3D body,
        Vector3 vitesseHorizontaleSouhaitee,
        float delta,
        float hauteurMaxEnjambement,
        float distanceProbeAvant,
        float vitesseHorizontaleMin,
        float normalYMinSol,
        float normalYMaxObstacle)
    {
        if (body == null || !GodotObject.IsInstanceValid(body))
            return false;
        if (delta <= 0f || !body.IsOnFloor())
            return false;
        if (body.Velocity.Y > 0.16f)
            return false;

        Vector3 dirHoriz = vitesseHorizontaleSouhaitee;
        dirHoriz.Y = 0f;
        float vitesseHoriz = dirHoriz.Length();
        if (vitesseHoriz < Mathf.Max(0.05f, vitesseHorizontaleMin))
            return false;
        dirHoriz /= vitesseHoriz;

        float hStep = Mathf.Clamp(hauteurMaxEnjambement, 0.05f, 0.95f);
        float distAvant = Mathf.Clamp(distanceProbeAvant, 0.2f, 1.8f);
        float minNormalSol = Mathf.Clamp(normalYMinSol, 0.25f, 1f);
        float maxNormalObstacle = Mathf.Clamp(normalYMaxObstacle, -1f, 0.95f);

        if (!AUneCollisionBloquanteRecente(body, dirHoriz, maxNormalObstacle))
            return false;

        World3D monde = body.GetWorld3D();
        if (monde?.DirectSpaceState == null)
            return false;

        uint masqueCollision = body.CollisionMask == 0u ? 0xFFFFFFFFu : body.CollisionMask;
        var exclusions = new Godot.Collections.Array<Rid>();
        if (body.GetRid().IsValid)
            exclusions.Add(body.GetRid());

        if (!Raycast(monde, body.GlobalPosition + Vector3.Up * (hStep + 0.45f), body.GlobalPosition + Vector3.Down * 1.6f, masqueCollision, exclusions, out var solActuel))
            return false;
        if (!solActuel.ContainsKey("position"))
            return false;
        float ySolActuel = ((Vector3)solActuel["position"]).Y;

        Vector3 origineBas = new Vector3(body.GlobalPosition.X, ySolActuel + 0.08f, body.GlobalPosition.Z);
        Vector3 finBas = origineBas + dirHoriz * distAvant;
        if (!Raycast(monde, origineBas, finBas, masqueCollision, exclusions, out var obstacle))
            return false;
        if (obstacle.ContainsKey("normal"))
        {
            Vector3 nObs = ((Vector3)obstacle["normal"]).Normalized();
            if (nObs.Y > maxNormalObstacle)
                return false;
        }

        Vector3 origineHaut = new Vector3(body.GlobalPosition.X, ySolActuel + hStep + 0.12f, body.GlobalPosition.Z);
        Vector3 finHaut = origineHaut + dirHoriz * distAvant;
        if (Raycast(monde, origineHaut, finHaut, masqueCollision, exclusions, out _))
            return false;

        Vector3 pointDevant = body.GlobalPosition + dirHoriz * (distAvant + 0.04f);
        Vector3 originePose = new Vector3(pointDevant.X, ySolActuel + hStep + 0.7f, pointDevant.Z);
        Vector3 finPose = new Vector3(pointDevant.X, ySolActuel - 0.45f, pointDevant.Z);
        if (!Raycast(monde, originePose, finPose, masqueCollision, exclusions, out var solPose))
            return false;
        if (!solPose.ContainsKey("position"))
            return false;

        if (solPose.ContainsKey("normal"))
        {
            Vector3 nSol = ((Vector3)solPose["normal"]).Normalized();
            if (nSol.Y < minNormalSol)
                return false;
        }

        float ySolPose = ((Vector3)solPose["position"]).Y;
        float deltaY = ySolPose - ySolActuel;
        if (deltaY < 0.025f || deltaY > hStep)
            return false;

        body.GlobalPosition += Vector3.Up * deltaY;
        Vector3 v = body.Velocity;
        if (v.Y < 0f)
            v.Y = 0f;
        body.Velocity = v;
        return true;
    }

    private static bool AUneCollisionBloquanteRecente(CharacterBody3D body, Vector3 dirHoriz, float normalYMaxObstacle)
    {
        int nb = body.GetSlideCollisionCount();
        if (nb <= 0)
            return false;
        for (int i = 0; i < nb; i++)
        {
            KinematicCollision3D c = body.GetSlideCollision(i);
            if (c == null)
                continue;
            Vector3 n = c.GetNormal().Normalized();
            if (n.Y > normalYMaxObstacle)
                continue;
            float faceObstacle = -n.Dot(dirHoriz);
            if (faceObstacle > 0.12f)
                return true;
        }
        return false;
    }

    private static bool Raycast(
        World3D monde,
        Vector3 origine,
        Vector3 fin,
        uint masqueCollision,
        Godot.Collections.Array<Rid> exclusions,
        out Godot.Collections.Dictionary resultat)
    {
        resultat = null;
        if (monde?.DirectSpaceState == null)
            return false;
        var q = PhysicsRayQueryParameters3D.Create(origine, fin);
        q.CollideWithBodies = true;
        q.CollideWithAreas = false;
        q.CollisionMask = masqueCollision;
        q.Exclude = exclusions;
        var hit = monde.DirectSpaceState.IntersectRay(q);
        if (hit == null || hit.Count == 0)
            return false;
        resultat = hit;
        return true;
    }
}
