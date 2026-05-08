using System.Collections.Generic;
using UnityEngine;

public class RopeCollisionTracker : MonoBehaviour
{
    struct RopeContactSample
    {
        public Collider Collider;
        public Vector3 Point;
        public Vector3 Normal;
        public int SegmentIndex;
        public float Time;
    }

    [SerializeField] float contactMemoryTime = 1.25f;
    [SerializeField] float minWrapNormalAngle = 55f;
    [SerializeField] float minRopeTensionForWrapAttach = 2f;
    [SerializeField] int maxStoredContacts = 96;

    readonly List<RopeContactSample> contacts = new List<RopeContactSample>();
    float currentRopeTension;
    Vector3 currentTensionDirection;

    public float CurrentRopeTension { get { return currentRopeTension; } }
    public Vector3 CurrentTensionDirection { get { return currentTensionDirection; } }
    public int ContactCount { get { PruneOldContacts(); return contacts.Count; } }

    public void Clear()
    {
        contacts.Clear();
        currentRopeTension = 0f;
        currentTensionDirection = Vector3.zero;
    }

    public void SetTension(float tension, Vector3 direction)
    {
        currentRopeTension = Mathf.Max(0f, tension);
        currentTensionDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
        PruneOldContacts();
    }

    public void ReportContact(Collider collider, Vector3 point, Vector3 normal, int segmentIndex)
    {
        if (collider == null || collider.isTrigger) return;

        PruneOldContacts();
        contacts.Add(new RopeContactSample
        {
            Collider = collider,
            Point = point,
            Normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up,
            SegmentIndex = segmentIndex,
            Time = Time.time
        });

        while (contacts.Count > maxStoredContacts)
        {
            contacts.RemoveAt(0);
        }
    }

    public bool HasConfirmedWrap(Collider collider, float externalTension)
    {
        if (collider == null) return false;
        if (Mathf.Max(currentRopeTension, externalTension) < minRopeTensionForWrapAttach) return false;

        PruneOldContacts();
        for (int i = 0; i < contacts.Count; i++)
        {
            if (contacts[i].Collider != collider) continue;

            for (int j = i + 1; j < contacts.Count; j++)
            {
                if (contacts[j].Collider != collider) continue;
                if (contacts[i].SegmentIndex == contacts[j].SegmentIndex) continue;

                float angle = Vector3.Angle(contacts[i].Normal, contacts[j].Normal);
                if (angle >= minWrapNormalAngle) return true;
            }
        }

        return false;
    }

    public bool TryGetBestWrappedCollider(out Collider collider, out Vector3 averagePoint, out Vector3 averageNormal)
    {
        PruneOldContacts();
        collider = null;
        averagePoint = Vector3.zero;
        averageNormal = Vector3.zero;

        int bestCount = 0;
        for (int i = 0; i < contacts.Count; i++)
        {
            Collider candidate = contacts[i].Collider;
            if (candidate == null || !HasConfirmedWrap(candidate, currentRopeTension)) continue;

            int count = 0;
            Vector3 pointSum = Vector3.zero;
            Vector3 normalSum = Vector3.zero;
            for (int j = 0; j < contacts.Count; j++)
            {
                if (contacts[j].Collider != candidate) continue;
                count++;
                pointSum += contacts[j].Point;
                normalSum += contacts[j].Normal;
            }

            if (count <= bestCount) continue;

            bestCount = count;
            collider = candidate;
            averagePoint = pointSum / count;
            averageNormal = normalSum.sqrMagnitude > 0.001f ? normalSum.normalized : Vector3.up;
        }

        return collider != null;
    }

    public void DrawDebugGizmos()
    {
        PruneOldContacts();
        Gizmos.color = Color.cyan;
        for (int i = 0; i < contacts.Count; i++)
        {
            Gizmos.DrawRay(contacts[i].Point, contacts[i].Normal * 0.65f);
        }
    }

    void PruneOldContacts()
    {
        float minTime = Time.time - contactMemoryTime;
        for (int i = contacts.Count - 1; i >= 0; i--)
        {
            if (contacts[i].Time < minTime || contacts[i].Collider == null)
            {
                contacts.RemoveAt(i);
            }
        }
    }
}
