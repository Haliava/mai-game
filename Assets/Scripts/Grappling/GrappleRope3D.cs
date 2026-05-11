using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class GrappleRope3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Runtime")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField, Min(0f)] private float maxRopeLength = 15f;
    [SerializeField, Min(0f)] private float ropeExtendSpeed = 36f;
    [SerializeField, Min(0f)] private float currentRopeLength;

    private bool ropeActive;
    private bool lengthLocked;

    public bool IsActive => ropeActive;
    public bool IsLengthLocked => lengthLocked;
    public Transform StartPoint => startPoint;
    public Transform EndPoint => endPoint;
    public float MaxRopeLength => maxRopeLength;
    public float RopeExtendSpeed => ropeExtendSpeed;
    public float CurrentRopeLength => currentRopeLength;

    public bool IsExtensionBlocked => false;
    public bool IsFullyExtended => ropeActive && currentRopeLength >= maxRopeLength - 0.001f;

    // Compatibility with old code.
    public int WrapPointCount => 0;
    public float RopeRadius => 0.035f;

    public Vector3 PlayerConstraintTarget => endPoint != null ? endPoint.position : transform.position;
    public Vector3 ConstraintOrigin => startPoint != null ? startPoint.position : transform.position;
    public float RemainingLengthForStartSegment => currentRopeLength;
    public float RemainingLengthForEndSegment => currentRopeLength;

    public float CurrentDistance
    {
        get
        {
            if (startPoint == null || endPoint == null)
            {
                return 0f;
            }

            return Vector3.Distance(startPoint.position, endPoint.position);
        }
    }

    public void Begin(
        Transform start,
        Transform end,
        float maxLength,
        float extendSpeed,
        float initialLength,
        float visualWidth,
        Material material)
    {
        CacheReferences();

        startPoint = start;
        endPoint = end;
        maxRopeLength = Mathf.Max(0f, maxLength);
        ropeExtendSpeed = Mathf.Max(0f, extendSpeed);
        currentRopeLength = Mathf.Clamp(initialLength, 0f, maxRopeLength);
        lengthLocked = false;
        ropeActive = startPoint != null && endPoint != null;

        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = ropeActive;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = Mathf.Max(0.001f, visualWidth);
        lineRenderer.endWidth = Mathf.Max(0.001f, visualWidth);
        lineRenderer.numCapVertices = 6;
        lineRenderer.numCornerVertices = 0;

        if (material != null)
        {
            lineRenderer.sharedMaterial = material;
        }

        UpdateRopeVisual();
    }

    // Compatibility method. No-op now.
    public void ConfigureWrap(
        LayerMask collisionMask,
        float radius,
        float detectionRadius,
        float unwrapAngle,
        float minPointDistance,
        int maxPoints)
    {
    }

    public float AdvanceLength(float deltaTime)
    {
        if (!ropeActive)
        {
            return 0f;
        }

        if (lengthLocked)
        {
            return currentRopeLength;
        }

        if (maxRopeLength <= 0f)
        {
            currentRopeLength = 0f;
            return currentRopeLength;
        }

        currentRopeLength = Mathf.Min(
            maxRopeLength,
            currentRopeLength + ropeExtendSpeed * Mathf.Max(0f, deltaTime));

        return currentRopeLength;
    }

    public void LockLengthToCurrentDirectDistance()
    {
        if (!ropeActive)
        {
            lengthLocked = true;
            currentRopeLength = 0f;
            return;
        }

        currentRopeLength = Mathf.Clamp(CurrentDistance, 0f, maxRopeLength);
        lengthLocked = true;
        UpdateRopeVisual();
    }

    // Compatibility with old controller.
    public void LockLengthToCurrentPathWithoutStretch()
    {
        LockLengthToCurrentDirectDistance();
    }

    public float SetCurrentLength(float length)
    {
        currentRopeLength = Mathf.Clamp(length, 0f, maxRopeLength);

        if (ropeActive)
        {
            lengthLocked = true;
        }

        UpdateRopeVisual();
        return currentRopeLength;
    }

    public float AdjustCurrentLength(float deltaLength, float minimumLength)
    {
        float safeMinimum = Mathf.Max(0f, minimumLength);
        return SetCurrentLength(Mathf.Clamp(currentRopeLength + deltaLength, safeMinimum, maxRopeLength));
    }

    // Compatibility method. No-op: rope no longer wraps.
    public void UpdateWrapPoints(
        LayerMask collisionMask,
        float radius,
        float detectionRadius,
        float unwrapAngle,
        float minPointDistance,
        int maxPoints,
        float edgeNormalThreshold,
        float minEdgeAngle,
        bool allowBottomEdgeLatch,
        bool allowTopSideLatch,
        bool allowBottomSideLatch)
    {
        UpdateRopeVisual();
    }

    public void Clear()
    {
        ropeActive = false;
        startPoint = null;
        endPoint = null;
        currentRopeLength = 0f;
        lengthLocked = false;

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }
    }

    public Vector3 GetPointPosition(int index)
    {
        if (index <= 0)
        {
            return startPoint != null ? startPoint.position : transform.position;
        }

        return endPoint != null ? endPoint.position : transform.position;
    }

    public readonly struct WrapPointInfo
    {
        public Vector3 Position => Vector3.zero;
        public Vector3 EdgeStart => Vector3.zero;
        public Vector3 EdgeEnd => Vector3.zero;
        public Vector3 NormalA => Vector3.up;
        public Vector3 NormalB => Vector3.up;
        public Vector3 SurfaceNormal => Vector3.up;
        public Collider Collider => null;
        public GrappleEdgeType EdgeType => GrappleEdgeType.Unsupported;
        public bool IsMantleEligible => false;
    }

    public bool TryGetWrapPointInfo(int wrapPointIndex, out WrapPointInfo info)
    {
        info = default;
        return false;
    }

    private void Reset()
    {
        CacheReferences();
        ConfigureDefaultLineRenderer();
    }

    private void OnValidate()
    {
        maxRopeLength = Mathf.Max(0f, maxRopeLength);
        ropeExtendSpeed = Mathf.Max(0f, ropeExtendSpeed);
        currentRopeLength = Mathf.Clamp(currentRopeLength, 0f, maxRopeLength);
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureDefaultLineRenderer();
        Clear();
    }

    private void LateUpdate()
    {
        UpdateRopeVisual();
    }

    private void CacheReferences()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
    }

    private void ConfigureDefaultLineRenderer()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.enabled = false;
    }

    private void UpdateRopeVisual()
    {
        if (!ropeActive || lineRenderer == null || startPoint == null || endPoint == null)
        {
            return;
        }

        if (lineRenderer.positionCount != 2)
        {
            lineRenderer.positionCount = 2;
        }

        lineRenderer.SetPosition(0, startPoint.position);
        lineRenderer.SetPosition(1, endPoint.position);
    }
}
