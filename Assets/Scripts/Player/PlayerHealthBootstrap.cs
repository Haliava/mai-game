using UnityEngine;





[DefaultExecutionOrder(-10)]
public sealed class PlayerHealthBootstrap : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj == null) return;

        var ph = playerObj.GetComponent<PlayerHealth>();
        if (ph == null) ph = playerObj.AddComponent<PlayerHealth>();

        var pdr = playerObj.GetComponent<PlayerDamageReceiver>();
        if (pdr == null) pdr = playerObj.AddComponent<PlayerDamageReceiver>();

        var ui = PlayerHealthUI.EnsureInScene();
        if (ui != null) ui.RegisterHealth(ph);

        
        if (EndlessDescentGameManager.Instance == null)
        {
            GameObject gm = new GameObject("EndlessDescentManager");
            gm.AddComponent<EndlessDescentGameManager>();
        }
    }
}
