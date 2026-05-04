using UnityEngine;

public class PrototypeSceneBootstrapper : MonoBehaviour
{
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
        player.transform.position = new Vector3(0f, 4f, 0f);
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
        playerLight.range = 360f;
        playerLight.intensity = 7f;
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
        SetPrivate(streaming, "player", player);
        SetPrivate(streaming, "generator", generator);
        SetPrivate(generator, "stoneMaterial", stoneMaterial);
        SetPrivate(generator, "grappleMaterial", grappleMaterial);
        return level;
    }

    GameObject CreateMonster(Transform player)
    {
        GameObject monster = new GameObject("CentipedeMonster");
        monster.transform.position = new Vector3(0f, 8f, 12f);
        Rigidbody rb = monster.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
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
        playerLight.range = 360f;
        playerLight.intensity = 7f;
        playerLight.color = new Color(0.72f, 0.82f, 1f);
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
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.01f, 0.012f, 0.014f);
        RenderSettings.fogDensity = 0.025f;
        RenderSettings.ambientLight = new Color(0.005f, 0.006f, 0.008f);
        RenderSettings.reflectionIntensity = 0.05f;

        Light[] lights = FindObjectsByType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type != LightType.Directional) continue;
            lights[i].intensity = 0.03f;
            lights[i].shadows = LightShadows.Soft;
        }
    }
}
