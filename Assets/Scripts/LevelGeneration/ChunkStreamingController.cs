using System.Collections.Generic;
using UnityEngine;

public class ChunkStreamingController : MonoBehaviour
{
    [SerializeField] int chunksAhead = 3;
    [SerializeField] int chunksBehind = 1;
    [SerializeField] float chunkHeight = 40f;
    [SerializeField] Transform player;
    [SerializeField] ProceduralLevelGenerator generator;

    readonly HashSet<int> activeChunks = new HashSet<int>();

    void Start()
    {
        if (generator == null) generator = GetComponent<ProceduralLevelGenerator>();
        UpdateChunks(true);
    }

    void Update()
    {
        UpdateChunks(false);
    }

    void UpdateChunks(bool force)
    {
        if (player == null || generator == null) return;

        int currentChunkIndex = Mathf.FloorToInt(-player.position.y / chunkHeight);
        int min = Mathf.Max(0, currentChunkIndex - chunksBehind);
        int max = currentChunkIndex + chunksAhead;

        for (int i = min; i <= max; i++)
        {
            generator.GenerateChunk(i);
            generator.SetChunkActive(i, true);
            activeChunks.Add(i);
        }

        List<int> deactivate = new List<int>();
        foreach (int index in activeChunks)
        {
            if (index < min || index > max) deactivate.Add(index);
        }

        for (int i = 0; i < deactivate.Count; i++)
        {
            generator.SetChunkActive(deactivate[i], false);
            activeChunks.Remove(deactivate[i]);
        }
    }
}
