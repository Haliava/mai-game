using UnityEngine;

public sealed class LevelInstanceRoot : MonoBehaviour
{
    public int LevelIndex;
    public float BaseY;
    // Top Y of the generated content (world space)
    public float TopY;
    // Cached bounds of generated content in world space
    public Bounds LevelBounds;
    // Optional explicit entry point inside this level (set by manager)
    public Transform EntryPoint;
}
