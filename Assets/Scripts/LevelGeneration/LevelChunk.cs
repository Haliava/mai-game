using System.Collections.Generic;
using UnityEngine;

public class LevelChunk : MonoBehaviour
{
    public Vector3 chunkOrigin;
    public Vector3 chunkSize = new Vector3(80f, 40f, 80f);
    public int chunkIndex;
    public readonly List<NavigationNode> navigationNodes = new List<NavigationNode>();
    public readonly List<Transform> grapplePoints = new List<Transform>();

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireCube(chunkOrigin + Vector3.down * chunkSize.y * 0.5f, chunkSize);
    }
}
