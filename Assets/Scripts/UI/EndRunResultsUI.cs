using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public sealed class EndRunResultsUI : MonoBehaviour
{
    private GameObject root;
    private Text titleText;
    private Text statsText;
    private Button restartButton;

    public void ShowResults(int completedLevels, int descentMeters)
    {
        EnsureUI();

        if (titleText != null)
        {
            titleText.text = "Вы погибли";
        }

        if (statsText != null)
        {
            statsText.text = $"Этаж: {completedLevels}\nГлубина спуска: {descentMeters} м";
        }

        if (root != null)
        {
            root.SetActive(true);
        }
    }

    private void EnsureUI()
    {
        if (root != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("EndRunResults Canvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("EndRun Panel");
        panel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.25f, 0.25f);
        panelRect.anchorMax = new Vector2(0.75f, 0.75f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        GameObject textGroup = new GameObject("Centered Text Group");
        textGroup.transform.SetParent(panel.transform, false);

        RectTransform groupRect = textGroup.AddComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0.05f, 0.28f);
        groupRect.anchorMax = new Vector2(0.95f, 0.78f);
        groupRect.offsetMin = Vector2.zero;
        groupRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = textGroup.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 14f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = textGroup.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(textGroup.transform, false);

        Text title = titleObj.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 32;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        title.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0f, 44f);

        GameObject statsObj = new GameObject("Stats");
        statsObj.transform.SetParent(textGroup.transform, false);

        Text stats = statsObj.AddComponent<Text>();
        stats.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        stats.fontSize = 20;
        stats.alignment = TextAnchor.MiddleCenter;
        stats.color = Color.white;
        stats.horizontalOverflow = HorizontalWrapMode.Overflow;
        stats.verticalOverflow = VerticalWrapMode.Overflow;
        stats.lineSpacing = 1.15f;

        RectTransform statsRect = stats.GetComponent<RectTransform>();
        statsRect.sizeDelta = new Vector2(0f, 64f);

        GameObject restartObj = new GameObject("Restart Button");
        restartObj.transform.SetParent(panel.transform, false);

        RectTransform bRect = restartObj.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.32f, 0.08f);
        bRect.anchorMax = new Vector2(0.68f, 0.22f);
        bRect.offsetMin = Vector2.zero;
        bRect.offsetMax = Vector2.zero;

        Image btnImg = restartObj.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.6f, 1f, 1f);

        Button btn = restartObj.AddComponent<Button>();

        GameObject btnTextObj = new GameObject("Restart Text");
        btnTextObj.transform.SetParent(restartObj.transform, false);

        Text btnText = btnTextObj.AddComponent<Text>();
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.text = "Restart (R)";
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;

        RectTransform btRect = btnText.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.offsetMin = Vector2.zero;
        btRect.offsetMax = Vector2.zero;

        btn.onClick.AddListener(RestartRun);

        root = panel;
        titleText = title;
        statsText = stats;
        restartButton = btn;

        root.SetActive(false);
    }

    private void RestartRun()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void Update()
    {
        if (root != null &&
            root.activeSelf &&
            Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartRun();
        }
    }

    public static EndRunResultsUI EnsureInScene()
    {
        EndRunResultsUI existing = FindAnyObjectByType<EndRunResultsUI>();

        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject("EndRunResultsUI");
        return go.AddComponent<EndRunResultsUI>();
    }
}