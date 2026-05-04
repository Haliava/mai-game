using UnityEngine;

public class PrototypeSceneBootstrapper : MonoBehaviour
{
    static readonly Vector3 SideStartPosition = new Vector3(72f, 3f, 0f);

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
        RopeController rope = player.GetComponentInChildren<RopeController>();
        GrapplingHookController hook = player.GetComponent<GrapplingHookController>();
        if (hook != null)
        {
            typeof(GrapplingHookController).GetField("hookPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(hook, hookPrefab.GetComponent<HookProjectile>());
        }

        EnsureLighting(player, hookPrefab);

        GameObject level = GameObject.Find("LevelSystem");
        if (level == null) level = CreateLevelSystem(player.transform);

        GameObject monster = GameObject.Find("CentipedeMonster");
        if (monster == null) monster = CreateMonster(player.transform);
        EnsureMonsterStartsOnWall(monster);

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
        line.widthMultiplier = 0.04f;

        RopeController rope = player.AddComponent<RopeController>();
        SetPrivate(rope, "player", player.transform);
        SetPrivate(rope, "playerRb", rb);
        SetPrivate(rope, "ropeOrigin", ropeOrigin.transform);
        SetPrivate(rope, "cameraTransform", cam.transform);
        SetPrivate(rope, "ropeLine", line);

        FirstPersonController movement = player.AddComponent<FirstPersonController>();
        SetPrivate(movement, "ropeController", rope);
        PlayerCameraController look = player.AddComponent<PlayerCameraController>();
        SetPrivate(look, "cameraHolder", holder.transform);
        player.AddComponent<PlayerMomentumController>();
        player.AddComponent<PlayerDamageController>();
        player.AddComponent<FallDamageDetector>();
        player.AddComponent<CollisionDamageDetector>();
        player.AddComponent<PlayerHealthHud>();
        Light playerLight = player.AddComponent<Light>();
        playerLight.type = LightType.Point;
        playerLight.range = 130f;
        playerLight.intensity = 1.8f;
        playerLight.color = new Color(0.82f, 0.9f, 1f);
        playerLight.shadows = LightShadows.None;
        player.AddComponent<PlayerLightController>();
        GrapplingHookController hook = player.AddComponent<GrapplingHookController>();
        SetPrivate(hook, "cameraTransform", cam.transform);
        SetPrivate(hook, "ropeOrigin", ropeOrigin.transform);
        SetPrivate(hook, "ropeController", rope);

        return player;
    }

    GameObject CreateHookPrefab()
    {
        GameObject existing = GameObject.Find("HookProjectile_RuntimePrefab");
        if (existing != null) return existing;

        GameObject hook = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hook.name = "HookProjectile_RuntimePrefab";
        hook.transform.localScale = Vector3.one * 0.18f;
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
        MonsterPathfinder pathfinder = monster.AddComponent<MonsterPathfinder>();
        MonsterJumpController jump = monster.AddComponent<MonsterJumpController>();
        CentipedeBodyController body = monster.AddComponent<CentipedeBodyController>();
        SetPrivate(body, "bodyMaterial", monsterMaterial);
        body.CreateBody();
        CentipedeMonsterController controller = monster.AddComponent<CentipedeMonsterController>();
        SetPrivate(controller, "player", player);
        SetPrivate(controller, "pathfinder", pathfinder);
        SetPrivate(controller, "jumpController", jump);
        SetPrivate(controller, "bodyController", body);

        GameObject attack = new GameObject("AttackZone");
        attack.transform.SetParent(monster.transform, false);
        SphereCollider trigger = attack.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 2.5f;
        attack.AddComponent<DamageSource>();
        return monster;
    }

    Vector3 GetMonsterWallStartPosition()
    {
        return new Vector3(154f, 8f, 10f);
    }

    void EnsureMonsterStartsOnWall(GameObject monster)
    {
        if (monster == null) return;
        Vector2 planar = new Vector2(monster.transform.position.x, monster.transform.position.z);
        if (planar.magnitude < 80f)
        {
            monster.transform.position = GetMonsterWallStartPosition();
        }

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
        playerLight.range = 130f;
        playerLight.intensity = 1.8f;
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
        RenderSettings.ambientLight = new Color(0.075f, 0.082f, 0.09f);
        RenderSettings.reflectionIntensity = 0.18f;

        Light[] lights = FindObjectsByType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type != LightType.Directional) continue;
            lights[i].intensity = 0.45f;
            lights[i].color = new Color(0.74f, 0.82f, 1f);
            lights[i].shadows = LightShadows.Soft;
        }
    }

    void EnsurePlayerStartsNearSidePlatform(GameObject player)
    {
        Vector2 planar = new Vector2(player.transform.position.x, player.transform.position.z);
        if (planar.magnitude > 20f) return;

        player.transform.position = SideStartPosition;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        PrototypeGameManager manager = FindAnyObjectByType<PrototypeGameManager>();
        if (manager != null) manager.SetLastSafePosition(SideStartPosition);
    }

    void EnsureSideStartPlatform()
    {
        if (GameObject.Find("StartPlatform") != null) return;

        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "StartPlatform";
        platform.transform.position = new Vector3(SideStartPosition.x, 0f, SideStartPosition.z);
        platform.transform.localScale = new Vector3(18f, 2f, 18f);

        Renderer renderer = platform.GetComponent<Renderer>();
        if (renderer != null && stoneMaterial != null) renderer.sharedMaterial = stoneMaterial;

        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        point.name = "StartGrapplePoint";
        point.transform.SetParent(platform.transform, true);
        point.transform.position = new Vector3(SideStartPosition.x + 7f, 3.2f, SideStartPosition.z + 3f);
        point.transform.localScale = Vector3.one * 0.65f;
        if (point.GetComponent<GrapplePoint>() == null) point.AddComponent<GrapplePoint>();

        Renderer pointRenderer = point.GetComponent<Renderer>();
        if (pointRenderer != null && grappleMaterial != null) pointRenderer.sharedMaterial = grappleMaterial;
    }
}
