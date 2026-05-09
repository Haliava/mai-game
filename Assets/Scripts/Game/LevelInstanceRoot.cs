using System.Collections.Generic;
using UnityEngine;

public sealed class LevelInstanceRoot : MonoBehaviour
{
    public int LevelIndex;
    public float BaseY;
    // Top Y of the generated content (world space)
    public float TopY;
    // Cached bounds of generated content in world space
    public Bounds LevelBounds;
    // Legacy: optional explicit entry point inside this level (may be set by manager)
    public Transform EntryPoint;

    // Explicit anchors provided by the generator (preferred authoritative anchors)
    public Transform EntryAnchor;
    public Transform ExitAnchor;
    public Transform FinalArenaRoot;

    // Gameplay bounds (may differ from LevelBounds in future revisions)
    public Bounds GameplayBounds;

    // Optional centipede spawn anchor transforms produced by the generator
    public List<Transform> CentipedeSpawnAnchors = new();
}
