using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class ScreenPixelationFilter : MonoBehaviour
{
    [Header("Pixelation")]
    [SerializeField, Min(1)] private int pixelSize = 4;
    [SerializeField, Min(64)] private int minInternalWidth = 160;
    [SerializeField, Min(64)] private int minInternalHeight = 90;
    [SerializeField] private bool pixelPerfect = true;

    [Header("UI")]
    [Tooltip("Если true, существующий UI будет отображаться поверх пиксельного 3D-изображения и останется чётким.")]
    [SerializeField] private bool keepExistingUiCrisp = true;

    [Tooltip("Низкий sorting order, чтобы обычный HUD был поверх пиксельного изображения.")]
    [SerializeField] private int pixelCanvasSortingOrder = -1000;

    [Header("Runtime")]
    [SerializeField] private bool enablePixelation = true;

    private Camera targetCamera;
    private RenderTexture renderTexture;
    private Canvas pixelCanvas;
    private RawImage outputImage;

    private int lastScreenWidth;
    private int lastScreenHeight;
    private int lastPixelSize;

    public int PixelSize
    {
        get => pixelSize;
        set
        {
            pixelSize = Mathf.Max(1, value);
            RebuildRenderTexture();
        }
    }

    public bool EnablePixelation
    {
        get => enablePixelation;
        set
        {
            enablePixelation = value;
            ApplyEnabledState();
        }
    }

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        CreateOutputCanvas();
        RebuildRenderTexture();
        ApplyEnabledState();
    }

    private void Update()
    {
        if (Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight ||
            pixelSize != lastPixelSize)
        {
            RebuildRenderTexture();
        }
    }

    private void OnDestroy()
    {
        if (targetCamera != null && targetCamera.targetTexture == renderTexture)
        {
            targetCamera.targetTexture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }

        if (pixelCanvas != null)
        {
            Destroy(pixelCanvas.gameObject);
        }
    }

    private void OnValidate()
    {
        pixelSize = Mathf.Max(1, pixelSize);
        minInternalWidth = Mathf.Max(64, minInternalWidth);
        minInternalHeight = Mathf.Max(64, minInternalHeight);

        if (Application.isPlaying && targetCamera != null)
        {
            RebuildRenderTexture();
            ApplyEnabledState();
        }
    }

    private void CreateOutputCanvas()
    {
        GameObject canvasObject = new GameObject("Pixelation Output Canvas");
        canvasObject.transform.SetParent(transform, false);

        pixelCanvas = canvasObject.AddComponent<Canvas>();
        pixelCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pixelCanvas.overrideSorting = true;
        pixelCanvas.sortingOrder = keepExistingUiCrisp ? pixelCanvasSortingOrder : 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        GameObject imageObject = new GameObject("Pixelated Screen");
        imageObject.transform.SetParent(canvasObject.transform, false);

        outputImage = imageObject.AddComponent<RawImage>();
        outputImage.raycastTarget = false;

        RectTransform rect = outputImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void RebuildRenderTexture()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (outputImage == null)
        {
            CreateOutputCanvas();
        }

        lastScreenWidth = Mathf.Max(1, Screen.width);
        lastScreenHeight = Mathf.Max(1, Screen.height);
        lastPixelSize = Mathf.Max(1, pixelSize);

        int width = Mathf.Max(minInternalWidth, lastScreenWidth / lastPixelSize);
        int height = Mathf.Max(minInternalHeight, lastScreenHeight / lastPixelSize);

        if (renderTexture != null &&
            renderTexture.width == width &&
            renderTexture.height == height)
        {
            return;
        }

        if (targetCamera != null && targetCamera.targetTexture == renderTexture)
        {
            targetCamera.targetTexture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = $"Pixelated Screen RT {width}x{height}",
            filterMode = pixelPerfect ? FilterMode.Point : FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false
        };

        renderTexture.Create();

        outputImage.texture = renderTexture;

        if (enablePixelation && targetCamera != null)
        {
            targetCamera.targetTexture = renderTexture;
        }
    }

    private void ApplyEnabledState()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (pixelCanvas != null)
        {
            pixelCanvas.gameObject.SetActive(enablePixelation);
        }

        if (targetCamera != null)
        {
            targetCamera.targetTexture = enablePixelation ? renderTexture : null;
        }
    }
}