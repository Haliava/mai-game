using UnityEngine;

public class PlayerHealthHud : MonoBehaviour
{
    [SerializeField] PlayerDamageController damageController;
    [SerializeField] Vector2 screenPosition = new Vector2(20f, 20f);
    [SerializeField] Vector2 size = new Vector2(220f, 26f);
    [SerializeField] Color barColor = new Color(0.85f, 0.08f, 0.06f, 1f);
    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);

    GUIStyle labelStyle;
    Texture2D whiteTexture;

    void Awake()
    {
        if (damageController == null) damageController = GetComponent<PlayerDamageController>();
        whiteTexture = Texture2D.whiteTexture;
        labelStyle = new GUIStyle();
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontSize = 16;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;
    }

    void OnGUI()
    {
        if (damageController == null) return;

        float health01 = damageController.MaxHealth > 0f ? Mathf.Clamp01(damageController.CurrentHealth / damageController.MaxHealth) : 0f;
        Rect background = new Rect(screenPosition.x, screenPosition.y, size.x, size.y);
        Rect fill = new Rect(screenPosition.x, screenPosition.y, size.x * health01, size.y);

        DrawRect(background, backgroundColor);
        DrawRect(fill, barColor);
        GUI.Label(background, "Health " + Mathf.CeilToInt(damageController.CurrentHealth) + " / " + Mathf.CeilToInt(damageController.MaxHealth), labelStyle);
    }

    void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, whiteTexture);
        GUI.color = previous;
    }
}
