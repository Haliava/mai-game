using UnityEngine;
using UnityEngine.UI;

public sealed class VictoryPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Text messageText;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }

        Hide();
    }

    private void Update()
    {
        if (root != null && root.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
        }
    }

    public void Show(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }

        if (root != null)
        {
            root.SetActive(true);
        }
    }

    public void Hide()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    public static VictoryPopupUI EnsureInScene()
    {
        VictoryPopupUI existing = FindAnyObjectByType<VictoryPopupUI>();
        if (existing != null)
        {
            return existing;
        }

        GameObject canvasObject = new("Victory Popup Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new("Victory Popup Panel");
        panel.transform.SetParent(canvasObject.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject textObject = new("Victory Text");
        textObject.transform.SetParent(panel.transform, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 44;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.15f, 0.38f);
        textRect.anchorMax = new Vector2(0.85f, 0.68f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        GameObject buttonObject = new("Close Button");
        buttonObject.transform.SetParent(panel.transform, false);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.12f, 0.2f, 0.24f, 0.95f);
        Button button = buttonObject.AddComponent<Button>();
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.42f, 0.26f);
        buttonRect.anchorMax = new Vector2(0.58f, 0.34f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        GameObject buttonTextObject = new("Close Button Text");
        buttonTextObject.transform.SetParent(buttonObject.transform, false);
        Text buttonText = buttonTextObject.AddComponent<Text>();
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.text = "Закрыть";
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.fontSize = 22;
        buttonText.color = Color.white;
        RectTransform buttonTextRect = buttonText.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        VictoryPopupUI popup = canvasObject.AddComponent<VictoryPopupUI>();
        popup.root = panel;
        popup.messageText = text;
        popup.closeButton = button;
        popup.Hide();
        return popup;
    }
}
