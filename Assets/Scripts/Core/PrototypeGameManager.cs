using UnityEngine;
using UnityEngine.SceneManagement;

public class PrototypeGameManager : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] PlayerDamageController damageController;
    [SerializeField] Vector3 lastSafePosition = new Vector3(0f, 3f, 0f);
    [SerializeField] float deathY = -380f;
    [SerializeField] float winDepth = -300f;
    [SerializeField] bool reloadOnDeath = false;

    bool won;

    void Start()
    {
        if (player == null)
        {
            PlayerDamageController p = FindAnyObjectByType<PlayerDamageController>();
            if (p != null)
            {
                player = p.transform;
                damageController = p;
            }
        }

        if (damageController != null) damageController.OnDied.AddListener(HandleDeath);
    }

    void Update()
    {
        if (player == null) return;

        if (player.position.y < deathY) HandleDeath();
        if (!won && player.position.y <= winDepth)
        {
            won = true;
            Debug.Log("Depth objective reached. Prototype win.");
        }
    }

    public void SetLastSafePosition(Vector3 position)
    {
        lastSafePosition = position;
    }

    void HandleDeath()
    {
        if (reloadOnDeath)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        if (player == null) return;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;
        player.position = lastSafePosition;
        if (damageController != null) damageController.Revive();
    }
}
