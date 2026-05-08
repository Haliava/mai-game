using UnityEngine;

[RequireComponent(typeof(SurfaceCrawlerMotor))]
public sealed class CentipedeController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;
    [SerializeField] private bool findPlayerByTag = true;

    [Header("Head Crawler")]
    [SerializeField, Min(0.05f)] private float headDiameter = 0.9f;
    [SerializeField, Min(0f)] private float moveSpeed = 1.7f;
    [SerializeField, Range(0.05f, 2f)] private float globalCentipedeSpeedMultiplier = 0.5f;
    [SerializeField] private Material headMaterial;
    [SerializeField] private bool createHeadVisual = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private SurfaceCrawlerMotor motor;
    private CentipedeBodyController body;
    private CentipedePathfinder pathfinder;
    private CentipedeJumpController jumpController;
    private CentipedeBurstController burstController;
    private CentipedeRecoveryController recoveryController;
    private Transform headVisual;

    public Transform Player
    {
        get => player;
        set
        {
            player = value;
            if (motor != null)
            {
                motor.IgnoredRoot = player;
            }
            if (pathfinder != null)
            {
                pathfinder.Target = player;
                pathfinder.IgnoredRoot = player;
            }
            if (jumpController != null)
            {
                jumpController.IgnoredRoot = player;
            }
            if (recoveryController != null)
            {
                recoveryController.IgnoredRoot = player;
            }
        }
    }

    public SurfaceCrawlerMotor Motor => motor;
    public Transform LookTarget => body != null && body.HeadSegment != null ? body.HeadSegment : headVisual != null ? headVisual : transform;

    private void Awake()
    {
        motor = GetComponent<SurfaceCrawlerMotor>();
        body = GetComponent<CentipedeBodyController>();
        pathfinder = GetComponent<CentipedePathfinder>();
        jumpController = GetComponent<CentipedeJumpController>();
        burstController = GetComponent<CentipedeBurstController>();
        recoveryController = GetComponent<CentipedeRecoveryController>();
        motor.MoveSpeed = moveSpeed;
        if (body == null)
        {
            EnsureHeadVisual();
        }
    }

    private void Start()
    {
        if (player == null && findPlayerByTag)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        motor.IgnoredRoot = player;
    }

    private void OnValidate()
    {
        headDiameter = Mathf.Max(0.05f, headDiameter);
        moveSpeed = Mathf.Max(0f, moveSpeed);
    }

    private void Update()
    {
        if (motor == null)
        {
            motor = GetComponent<SurfaceCrawlerMotor>();
        }
        if (body == null)
        {
            body = GetComponent<CentipedeBodyController>();
        }
        if (pathfinder == null)
        {
            pathfinder = GetComponent<CentipedePathfinder>();
        }
        if (jumpController == null)
        {
            jumpController = GetComponent<CentipedeJumpController>();
        }
        if (burstController == null)
        {
            burstController = GetComponent<CentipedeBurstController>();
        }
        if (recoveryController == null)
        {
            recoveryController = GetComponent<CentipedeRecoveryController>();
        }

        float targetDistance = player != null ? Vector3.Distance(transform.position, player.position) : 0f;
        bool isJumping = jumpController != null && jumpController.IsJumping;
        float burstMultiplier = burstController != null ? burstController.Tick(targetDistance, !isJumping) : 1f;
        motor.MoveSpeed = moveSpeed * globalCentipedeSpeedMultiplier * burstMultiplier;
        if (body != null)
        {
            body.FollowSpeedMultiplier = globalCentipedeSpeedMultiplier;
        }

        if (recoveryController != null)
        {
            recoveryController.IgnoredRoot = player;
            recoveryController.CrawlableSurfaceMask = motor.CrawlableSurfaceMask;
            if (recoveryController.Tick(player, motor, isJumping))
            {
                if (pathfinder != null)
                {
                    pathfinder.RebuildPathNow();
                }
            }
        }

        if (jumpController != null)
        {
            jumpController.IgnoredRoot = player;
            jumpController.CrawlableSurfaceMask = motor.CrawlableSurfaceMask;
            if (jumpController.IsJumping)
            {
                jumpController.Tick(Time.deltaTime);
                return;
            }
        }

        if (pathfinder != null)
        {
            pathfinder.Target = player;
            pathfinder.IgnoredRoot = player;
            pathfinder.CrawlableSurfaceMask = motor.CrawlableSurfaceMask;
            pathfinder.Tick(Time.deltaTime);
        }

        Vector3 targetPosition = player != null ? player.position : transform.position + transform.forward * 5f;
        bool pathTargetRequiresJump = false;
        if (pathfinder != null && pathfinder.TryGetMoveTarget(out Vector3 pathTarget, out pathTargetRequiresJump))
        {
            targetPosition = pathTarget;
        }

        if (pathTargetRequiresJump && jumpController != null && jumpController.TryStartJump(targetPosition))
        {
            jumpController.Tick(Time.deltaTime);
            return;
        }

        motor.TickToward(targetPosition, Time.deltaTime);
    }

    public void AttachToSurface(Vector3 surfacePoint, Vector3 normal, Vector3 preferredForward)
    {
        if (motor == null)
        {
            motor = GetComponent<SurfaceCrawlerMotor>();
        }

        motor.TryAttachToSurface(surfacePoint, normal, preferredForward);
    }

    private void EnsureHeadVisual()
    {
        if (!createHeadVisual)
        {
            return;
        }

        Transform existing = transform.Find("Head Segment");
        if (existing != null)
        {
            headVisual = existing;
            return;
        }

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Head Segment";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localRotation = Quaternion.identity;
        sphere.transform.localScale = Vector3.one * headDiameter;
        headVisual = sphere.transform;

        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();
        if (renderer != null && headMaterial != null)
        {
            renderer.sharedMaterial = headMaterial;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, headDiameter * 0.5f);
        if (player != null)
        {
            Gizmos.DrawLine(transform.position, player.position);
        }
    }
}
