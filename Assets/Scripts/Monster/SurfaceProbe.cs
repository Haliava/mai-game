using UnityEngine;

public static class SurfaceProbe
{
    public static bool TryFindNearestSurface(
        Vector3 origin,
        Vector3 preferredDirection,
        Vector3 currentUp,
        Transform ignoredRoot,
        LayerMask mask,
        float probeRadius,
        float probeDistance,
        out RaycastHit bestHit)
    {
        bestHit = default(RaycastHit);
        float bestScore = float.MaxValue;

        Vector3 up = currentUp.sqrMagnitude > 0.001f ? currentUp.normalized : Vector3.up;
        Vector3 forward = preferredDirection.sqrMagnitude > 0.001f ? preferredDirection.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(up, forward);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(up, Vector3.right);
        if (right.sqrMagnitude < 0.001f) right = Vector3.forward;
        right.Normalize();

        Vector3[] directions = new Vector3[]
        {
            -up,
            (forward - up).normalized,
            forward,
            -forward,
            right,
            -right,
            up,
            Vector3.down,
            Vector3.up
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 direction = directions[i];
            if (direction.sqrMagnitude < 0.001f) continue;
            RaycastHit[] hits = Physics.SphereCastAll(
                origin - direction * 0.2f,
                Mathf.Max(0.01f, probeRadius),
                direction,
                probeDistance,
                mask,
                QueryTriggerInteraction.Ignore);

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int j = 0; j < hits.Length; j++)
            {
                if (!IsUsableHit(hits[j], ignoredRoot)) continue;
                float alignmentPenalty = 1f - Mathf.Clamp01(Vector3.Dot(direction.normalized, -hits[j].normal));
                float score = hits[j].distance + alignmentPenalty * 0.35f;
                if (score >= bestScore) continue;

                bestScore = score;
                bestHit = hits[j];
                break;
            }
        }

        return bestHit.collider != null;
    }

    public static bool TryFindSurfaceAround(
        Vector3 origin,
        Transform ignoredRoot,
        LayerMask mask,
        float probeRadius,
        float searchRadius,
        out RaycastHit bestHit)
    {
        bestHit = default(RaycastHit);
        float bestDistance = float.MaxValue;
        Vector3[] directions = new Vector3[]
        {
            Vector3.down,
            Vector3.up,
            Vector3.right,
            Vector3.left,
            Vector3.forward,
            Vector3.back
        };

        for (int i = 0; i < directions.Length; i++)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                origin - directions[i] * searchRadius * 0.5f,
                Mathf.Max(0.01f, probeRadius),
                directions[i],
                searchRadius * 2f,
                mask,
                QueryTriggerInteraction.Ignore);

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int j = 0; j < hits.Length; j++)
            {
                if (!IsUsableHit(hits[j], ignoredRoot)) continue;
                float distance = Vector3.Distance(origin, hits[j].point);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestHit = hits[j];
                break;
            }
        }

        return bestHit.collider != null;
    }

    public static bool IsUsableHit(RaycastHit hit, Transform ignoredRoot)
    {
        if (hit.collider == null) return false;
        if (hit.collider.isTrigger) return false;
        if (!hit.collider.enabled) return false;
        if (ignoredRoot != null && hit.collider.transform.IsChildOf(ignoredRoot)) return false;
        return true;
    }
}
