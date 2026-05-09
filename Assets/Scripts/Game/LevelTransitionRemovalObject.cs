using UnityEngine;

public sealed class LevelTransitionRemovalObject : MonoBehaviour
{
    public enum RemovalPhase
    {
        OnLevelCompleted,
        AfterNextLevelReady,
        OnOldLevelCleanup
    }

    public RemovalPhase Phase = RemovalPhase.AfterNextLevelReady;
}
