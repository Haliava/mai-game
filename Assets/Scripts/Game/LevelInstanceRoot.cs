using System.Collections.Generic;
using UnityEngine;

public sealed class LevelInstanceRoot : MonoBehaviour
{
    public int LevelIndex;
    public float BaseY;
    
    public float TopY;
    
    public Bounds LevelBounds;
    
    public Transform EntryPoint;

    
    public Transform EntryAnchor;
    public Transform ExitAnchor;
    public Transform FinalArenaRoot;

    
    public Bounds GameplayBounds;

    
    public List<Transform> CentipedeSpawnAnchors = new();
}
