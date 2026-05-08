using UnityEngine;

public class PrototypeSceneBootstrapper : MonoBehaviour
{
    static readonly Vector3 SideStartPosition = new Vector3(46f, 2.1f, 0f);

    [SerializeField] bool buildOnStart = true;
    [SerializeField] Material stoneMaterial;
    [SerializeField] Material grappleMaterial;
    [SerializeField] Material monsterMaterial;

    void Start()
    {
        if (buildOnStart) BuildMissingSceneObjects();
    }

    public void BuildMissingSceneObjects()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) player = CreatePlayer();
        EnsurePlayerStartsNearSidePlatform(player);
        EnsureSideStartPlatform();

        GameObject hookPrefab = CreateHookPrefab();
        EnsurePlayerGrappleSetup(player, hookPrefab);
        ConfigurePhysicsCollisionMatrix();

        EnsureLighting(player, hookPrefab);

        GameObject level = GameObject.Find("LevelSystem");
        if (level == null) level = CreateLevelSystem(player.transform);

        GameObject monster = GameObject.Find("CentipedeMonster");
        if (monster == null) monster = CreateMonster(player.transform);
        EnsureMonsterStartsOnWall(monster);
        EnsureMonsterVisibleAndBuilt(monster);

        if (FindAnyObjectByType<PrototypeGameManager>() == null)
        {
            GameObject gm = new GameObject("GameManager");
            PrototypeGameManager manager = gm.AddComponent<PrototypeGameManager>();
            typeof(PrototypeGameManager).GetField("player", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(manager, player.transform);
            typeof(PrototypeGameManager).GetField("damageController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(manager, player.GetComponent<PlayerDamageController>());
        }

        ApplyDarkRenderSettings();
    }

    GameObject CreatePlayer()
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = SideStartPosition;
        player.layer = LayerMask.NameToLayer("Player") >= 0 ? LayerMask.NameToLayer("Player") : 0;

        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0.15f;
        rb.angularDamping = 0.05f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        GameObject holder = new GameObject("CameraHolder");
        holder.transform.SetParent(player.transform, false);
        holder.transform.localPosition = new Vector3(0f, 0.65f, 0f);

        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
        }
        cam.transform.SetParent(holder.transform, false);
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.identity;

        GameObject ropeOrigin = new GameObject("RopeOrigin");
        ropeOrigin.transform.SetParent(holder.transform, false);
        ropeOrigin.transform.localPosition = new Vector3(0.35f, -0.25f, 0.25f);

        LineRenderer line = player.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.widthMultiplier = 0.018f;

        RopeController rope = player.AddComponent<RopeController>();
        RopeCollisionTracker tracker = player.GetComponent<RopeCollisionTracker>();
        if (tracker == null) tracker = player.AddComponent<RopeCollisionTracker>();
        SetPrivate(rope, "player", player.transform);
        SetPrivate(rope, "playerRb", rb);
        SetPrivate(rope, "ropeOrigin", ropeOrigin.transform);
        SetPrivate(rope, "cameraTransform", cam.transform);
        SetPrivate(rope, "ropeLine", line);
        SetPrivate(rope, "collisionTracker", tracker);
        SetPrivate(rope, "ropeCollisionMask", BuildWorldGeometryMask());
        SetPrivate(rope, "visualRopeWidth", 0.018f);
        SetPrivate(rope, "showPhysicalSegmentRenderers", false);
        SetPrivate(rope, "physicalSegmentLength", 0.4f);
        SetPrivate(rope, "physicalSegmentRadius", 0.085f);
        SetPrivate(rope, "maxPhysicalSegments", 90);

        FirstPersonController movement = player.AddComponent<FirstPersonController>();
        SetPrivate(movement, "ropeController", rope);
        PlayerCameraController look = player.AddComponent<PlayerCameraController>();
        SetPrivate(look, "cameraHolder", holder.transform);
        player.AddComponent<PlayerMomentumController>();
        PlayerLedgeMountAssist ledgeAssist = player.AddComponent<PlayerLedgeMountAssist>();
        SetPrivate(rope, "ledgeMountAssist", ledgeAssist);
        player.AddComponent<PlayerDamageController>();
        player.AddComponent<FallDamageDetector>();
        player.AddComponent<CollisionDamageDetector>();
        player.AddComponent<PlayerHealthHud>();
        Light playerLight = player.AddComponent<Light>();
        playerLight.type = LightType.Point;
        playerLight.range = 2800f;
        playerLight.intensity = 16f;
        playerLight.color = new Color(0.82f, 0.9f, 1f);
        playerLight.shadows = LightShadows.None;
        player.AddComponent<PlayerLightController>();
        HookEdgeDetector edgeDetector = player.AddComponent<HookEdgeDetector>();
        HookAttachValidator attachValidator = player.AddComponent<HookAttachValidator>();
        GrapplingHookController hook = player.AddComponent<GrapplingHookController>();
        SetPrivate(hook, "cameraTransform", cam.transform);
        SetPrivate(hook, "ropeOrigin", ropeOrigin.transform);
        SetPrivate(hook, "ropeController", rope);
        SetPrivate(hook, "edgeDetector", edgeDetector);
        SetPrivate(hook, "attachValidator", attachValidator);
        SetPrivate(hook, "ropeCollisionTracker", tracker);
        SetPrivate(hook, "grappleMask", BuildWorldGeometryMask());
        SetPrivate(hook, "ropeCollisionMask", BuildWorldGeometryMask());

        return player;
    }

    GameObject CreateHookPrefab()
    {
        GameObject existing = GameObject.Find("HookProjectile_RuntimePrefab");
        if (existing != null) return existing;

        GameObject hook = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hook.name = "HookProjectile_RuntimePrefab";
        hook.transform.localScale = Vector3.one * 0.18f;
        SetLayerIfExists(hook, "Hook");
        hook.SetActive(false);
        Rigidbody rb = hook.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Light hookLight = hook.AddComponent<Light>();
        hookLight.type = LightType.Point;
        hookLight.range = 18f;
        hookLight.intensity = 2.2f;
        hookLight.color = new Color(0.55f, 0.82f, 1f);
        hookLight.shadows = LightShadows.None;
        hookLight.enabled = false;
        hook.AddComponent<HookProjectile>();
        return hook;
    }

    void EnsurePlayerGrappleSetup(GameObject player, GameObject hookPrefab)
    {
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        RopeController rope = player.GetComponentInChildren<RopeController>();
        if (rope == null) rope = player.AddComponent<RopeController>();

        RopeCollisionTracker tracker = player.GetComponent<RopeCollisionTracker>();
        if (tracker == null) tracker = player.AddComponent<RopeCollisionTracker>();

        LineRenderer line = player.GetComponent<LineRenderer>();
        if (line == null)
        {
            line = player.AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.widthMultiplier = 0.018f;
        }

        Camera cam = Camera.main;
        Transform holder = player.transform.Find("CameraHolder");
        Transform ropeOrigin = holder != null ? holder.Find("RopeOrigin") : player.transform.Find("RopeOrigin");
        if (ropeOrigin == null)
        {
            GameObject ropeOriginObject = new GameObject("RopeOrigin");
            ropeOriginObject.transform.SetParent(holder != null ? holder : player.transform, false);
            ropeOriginObject.transform.localPosition = new Vector3(0.35f, -0.25f, 0.25f);
            ropeOrigin = ropeOriginObject.transform;
        }

        SetPrivate(rope, "player", player.transform);
        SetPrivate(rope, "playerRb", rb);
        SetPrivate(rope, "ropeOrigin", ropeOrigin);
        if (cam != null) SetPrivate(rope, "cameraTransform", cam.transform);
        SetPrivate(rope, "ropeLine", line);
        SetPrivate(rope, "collisionTracker", tracker);
        SetPrivate(rope, "ropeCollisionMask", BuildWorldGeometryMask());
        SetPrivate(rope, "visualRopeWidth", 0.018f);
        SetPrivate(rope, "showPhysicalSegmentRenderers", false);
        SetPrivate(rope, "physicalSegmentLength", 0.4f);
        SetPrivate(rope, "physicalSegmentRadius", 0.085f);
        SetPrivate(rope, "maxPhysicalSegments", 90);

        FirstPersonController movement = player.GetComponent<FirstPersonController>();
        if (movement != null) SetPrivate(movement, "ropeController", rope);
        PlayerLedgeMountAssist ledgeAssist = player.GetComponent<PlayerLedgeMountAssist>();
        if (ledgeAssist == null) ledgeAssist = player.AddComponent<PlayerLedgeMountAssist>();
        SetPrivate(rope, "ledgeMountAssist", ledgeAssist);

        HookEdgeDetector edgeDetector = player.GetComponent<HookEdgeDetector>();
        if (edgeDetector == null) edgeDetector = player.AddComponent<HookEdgeDetector>();
        HookAttachValidator attachValidator = player.GetComponent<HookAttachValidator>();
        if (attachValidator == null) attachValidator = player.AddComponent<HookAttachValidator>();

        GrapplingHookController hook = player.GetComponent<GrapplingHookController>();
        if (hook == null) hook = player.AddComponent<GrapplingHookController>();
        if (hookPrefab != null) SetPrivate(hook, "hookPrefab", hookPrefab.GetComponent<HookProjectile>());
        if (cam != null) SetPrivate(hook, "cameraTransform", cam.transform);
        SetPrivate(hook, "ropeOrigin", ropeOrigin);
        SetPrivate(hook, "ropeController", rope);
        SetPrivate(hook, "edgeDetector", edgeDetector);
        SetPrivate(hook, "attachValidator", attachValidator);
        SetPrivate(hook, "ropeCollisionTracker", tracker);
        SetPrivate(hook, "grappleMask", BuildWorldGeometryMask());
        SetPrivate(hook, "ropeCollisionMask", BuildWorldGeometryMask());
    }

    GameObject CreateLevelSystem(Transform player)
    {
        GameObject level = new GameObject("LevelSystem");
        ProceduralLevelGenerator generator = level.AddComponent<ProceduralLevelGenerator>();
        ChunkStreamingController streaming = level.AddComponent<ChunkStreamingController>();
        MegastructureBoundaryBuilder boundary = level.AddComponent<MegastructureBoundaryBuilder>();
        SetPrivate(streaming, "player", player);
        SetPrivate(streaming, "generator", generator);
        SetPrivate(boundary, "generator", generator);
        SetPrivate(generator, "stoneMaterial", stoneMaterial);
        SetPrivate(generator, "grappleMaterial", grappleMaterial);
        return level;
    }

    GameObject CreateMonster(Transform player)
    {
        GameObject monster = new GameObject("CentipedeMonster");
        monster.transform.position = GetMonsterWallStartPosition();
        monster.transform.rotation = Quaternion.LookRotation(Vector3.down, -new Vector3(monster.transform.position.x, 0f, monster.transform.position.z).normalized);
        Rigidbody rb = monster.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        SurfaceNavigationGraph graph = monster.AddComponent<SurfaceNavigationGraph>();
        CentipedePathfinder pathfinder = monster.AddComponent<CentipedePathfinder>();
        JumpPlanner jumpPlanner = monster.AddComponent<JumpPlanner>();
        JumpExecutor jumpExecutor = monster.AddComponent<JumpExecutor>();
        CentipedeBody body = monster.AddComponent<CentipedeBody>();
        SetPrivate(body, "bodyMaterial", monsterMaterial);
        SetPrivate(body, "legMaterial", monsterMaterial);
        body.CreateBody();
        CentipedeBrain brain = monster.AddComponent<CentipedeBrain>();
        SetPrivate(brain, "player", player);
        SetPrivate(brain, "body", body);
        SetPrivate(brain, "graph", graph);
        SetPrivate(brain, "pathfinder", pathfinder);
        SetPrivate(brain, "jumpPlanner", jumpPlanner);
        SetPrivate(brain, "jumpExecutor", jumpExecutor);

        GameObject attack = new GameObject("AttackZone");
        attack.transform.SetParent(monster.transform, false);
        SphereCollider trigger = attack.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 2.5f;
        attack.AddComponent<MonsterDamageSource>();
        return monster;
    }

    Vector3 GetMonsterWallStartPosition()
    {
        return new Vector3(76f, 8f, 10f);
    }

    void EnsureMonsterStartsOnWall(GameObject monster)
    {
        if (monster == null) return;
        Vector2 planar = new Vector2(monster.transform.position.x, monster.transform.position.z);
        monster.transform.position = GetMonsterWallStartPosition();

        Vector3 radial = new Vector3(monster.transform.position.x, 0f, monster.transform.position.z);
        if (radial.sqrMagnitude > 0.01f)
        {
            monster.transform.rotation = Quaternion.LookRotation(Vector3.down, -radial.normalized);
        }

        Rigidbody rb = monster.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void EnsureMonsterVisibleAndBuilt(GameObject monster)
    {
        if (monster == null) return;

        CentipedeMonsterController oldController = monster.GetComponent<CentipedeMonsterController>();
        if (oldController != null) oldController.enabled = false;
        MonsterPathfinder oldPathfinder = monster.GetComponent<MonsterPathfinder>();
        if (oldPathfinder != null) oldPathfinder.enabled = false;
        MonsterJumpController oldJump = monster.GetComponent<MonsterJumpController>();
        if (oldJump != null) oldJump.enabled = false;
        CentipedeBodyController oldBody = monster.GetComponent<CentipedeBodyController>();
        if (oldBody != null) oldBody.enabled = false;

        SurfaceNavigationGraph graph = monster.GetComponent<SurfaceNavigationGraph>();
        if (graph == null) graph = monster.AddComponent<SurfaceNavigationGraph>();
        CentipedePathfinder pathfinder = monster.GetComponent<CentipedePathfinder>();
        if (pathfinder == null) pathfinder = monster.AddComponent<CentipedePathfinder>();
        JumpPlanner jumpPlanner = monster.GetComponent<JumpPlanner>();
        if (jumpPlanner == null) jumpPlanner = monster.AddComponent<JumpPlanner>();
        JumpExecutor jumpExecutor = monster.GetComponent<JumpExecutor>();
        if (jumpExecutor == null) jumpExecutor = monster.AddComponent<JumpExecutor>();
        CentipedeBody body = monster.GetComponent<CentipedeBody>();
        if (body == null) body = monster.AddComponent<CentipedeBody>();
        SetPrivate(body, "bodyMaterial", monsterMaterial);
        SetPrivate(body, "legMaterial", monsterMaterial);
        body.CreateBody();
        CentipedeBrain brain = monster.GetComponent<CentipedeBrain>();
        if (brain == null) brain = monster.AddComponent<CentipedeBrain>();
        PlayerDamageController player = FindAnyObjectByType<PlayerDamageController>();
        if (player != null) SetPrivate(brain, "player", player.transform);
        SetPrivate(brain, "body", body);
        SetPrivate(brain, "graph", graph);
        SetPrivate(brain, "pathfinder", pathfinder);
        SetPrivate(brain, "jumpPlanner", jumpPlanner);
        SetPrivate(brain, "jumpExecutor", jumpExecutor);

        Transform attack = monster.transform.Find("AttackZone");
        if (attack == null)
        {
            GameObject attackObject = new GameObject("AttackZone");
            attackObject.transform.SetParent(monster.transform, false);
            attack = attackObject.transform;
        }
        SphereCollider trigger = attack.GetComponent<SphereCollider>();
        if (trigger == null) trigger = attack.gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 2.5f;
        DamageSource legacyDamage = attack.GetComponent<DamageSource>();
        if (legacyDamage != null) legacyDamage.enabled = false;
        if (attack.GetComponent<MonsterDamageSource>() == null) attack.gameObject.AddComponent<MonsterDamageSource>();

        Renderer[] renderers = monster.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
        }
    }

    void SetPrivate(object target, string fieldName, object value)
    {
        System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(target, value);
    }

    void EnsureLighting(GameObject player, GameObject hookPrefab)
    {
        Light playerLight = player.GetComponent<Light>();
        if (playerLight == null) playerLight = player.AddComponent<Light>();
        playerLight.type = LightType.Point;
        playerLight.range = 2800f;
        playerLight.intensity = 16f;
        playerLight.color = new Color(0.82f, 0.9f, 1f);
        playerLight.shadows = LightShadows.None;
        if (player.GetComponent<PlayerLightController>() == null) player.AddComponent<PlayerLightController>();

        Light hookLight = hookPrefab.GetComponent<Light>();
        if (hookLight == null) hookLight = hookPrefab.AddComponent<Light>();
        hookLight.type = LightType.Point;
        hookLight.range = 18f;
        hookLight.intensity = 2.2f;
        hookLight.color = new Color(0.55f, 0.82f, 1f);
        hookLight.shadows = LightShadows.None;
        hookLight.enabled = false;
    }

    void ApplyDarkRenderSettings()
    {
        RenderSettings.fog = false;
        RenderSettings.fogDensity = 0f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.2f);
        RenderSettings.reflectionIntensity = 0.28f;

        Light[] lights = FindObjectsByType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type != LightType.Directional) continue;
            lights[i].intensity = 0.25f;
            lights[i].color = new Color(0.74f, 0.82f, 1f);
            lights[i].shadows = LightShadows.Soft;
        }
    }

    void EnsurePlayerStartsNearSidePlatform(GameObject player)
    {
        Vector2 planar = new Vector2(player.transform.position.x, player.transform.position.z);
        if (Vector3.Distance(player.transform.position, SideStartPosition) < 4f) return;
        if (planar.magnitude > 20f && planar.magnitude < 58f) return;

        player.transform.position = SideStartPosition;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        PrototypeGameManager manager = FindAnyObjectByType<PrototypeGameManager>();
        if (manager != null) manager.SetLastSafePosition(SideStartPosition);
    }

    void EnsureSideStartPlatform()
    {
        GameObject platform = GameObject.Find("StartPlatform");
        if (platform == null)
        {
            platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "StartPlatform";
        }
        platform.transform.position = new Vector3(SideStartPosition.x, 0f, SideStartPosition.z);
        platform.transform.localScale = new Vector3(16f, 2f, 16f);
        PrepareGrappleGeometry(platform);

        Renderer renderer = platform.GetComponent<Renderer>();
        if (renderer != null && stoneMaterial != null) renderer.sharedMaterial = stoneMaterial;

        GameObject point = GameObject.Find("StartGrapplePoint");
        if (point == null)
        {
            point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            point.name = "StartGrapplePoint";
        }
        point.transform.SetParent(platform.transform, true);
        point.transform.position = new Vector3(SideStartPosition.x + 6f, 3.2f, SideStartPosition.z + 3f);
        point.transform.localScale = Vector3.one * 0.65f;
        if (point.GetComponent<GrapplePoint>() == null) point.AddComponent<GrapplePoint>();

        Renderer pointRenderer = point.GetComponent<Renderer>();
        if (pointRenderer != null && grappleMaterial != null) pointRenderer.sharedMaterial = grappleMaterial;
    }

    void PrepareGrappleGeometry(GameObject go)
    {
        if (go == null) return;
        SetLayerIfExists(go, "GrappleGeometry", "GrappleSurface");
        if (go.GetComponent<GrappleSurface>() == null) go.AddComponent<GrappleSurface>();
        if (go.GetComponent<RopeCollisionWrapper>() == null) go.AddComponent<RopeCollisionWrapper>();
    }

    void SetLayerIfExists(GameObject go, params string[] layerNames)
    {
        if (go == null || layerNames == null) return;
        for (int i = 0; i < layerNames.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layerNames[i]);
            if (layer < 0) continue;
            go.layer = layer;
            return;
        }
    }

    LayerMask BuildMask(params string[] layerNames)
    {
        int mask = 0;
        for (int i = 0; i < layerNames.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layerNames[i]);
            if (layer >= 0) mask |= 1 << layer;
        }

        return mask == 0 ? ~0 : mask;
    }

    LayerMask BuildWorldGeometryMask()
    {
        int mask = ~0;
        RemoveLayerFromMask(ref mask, "Player");
        RemoveLayerFromMask(ref mask, "Hook");
        RemoveLayerFromMask(ref mask, "Rope");
        RemoveLayerFromMask(ref mask, "IgnoreRopeSelfCollision");
        return mask;
    }

    void RemoveLayerFromMask(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) mask &= ~(1 << layer);
    }

    void ConfigurePhysicsCollisionMatrix()
    {
        IgnoreLayerPair("Hook", "Player", true);
        IgnoreLayerPair("Rope", "Player", true);
        IgnoreLayerPair("Rope", "Rope", true);
        IgnoreLayerPair("Hook", "Rope", true);
    }

    void IgnoreLayerPair(string a, string b, bool ignore)
    {
        int layerA = LayerMask.NameToLayer(a);
        int layerB = LayerMask.NameToLayer(b);
        if (layerA < 0 || layerB < 0) return;
        Physics.IgnoreLayerCollision(layerA, layerB, ignore);
    }
}
