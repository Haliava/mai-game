using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public sealed class EndlessDescentGameManager : MonoBehaviour
{
    public static EndlessDescentGameManager Instance { get; private set; }

    public enum RunState { RunStarting, Playing, CompletingLevel, GeneratingNextLevel, Dead }

    [Header("Endless Descent")]
    [SerializeField] private int completedLevels = 0;
    [SerializeField] private float totalDescentMeters = 0f;
    [SerializeField, Min(1f)] private float levelHeight = 250f;
    [SerializeField, Min(0)] private int keepPreviousLevelsCount = 2;
    [SerializeField] private bool deleteOldLevels = true;
    [SerializeField] private bool spawnNewCentipedeEachLevel = true;
    [SerializeField] private bool destroyOldCentipedes = false;
    [SerializeField] private bool healPlayerOnLevelComplete = true;
    [SerializeField] private bool generateNextLevelOnSphereTouch = true;
    [SerializeField] private bool pauseDuringLevelGeneration = false;

    [Header("Death / Run End")]
    [SerializeField] private bool freezeGameOnDeath = true;
    [SerializeField] private bool showCursorOnDeath = true;
    [SerializeField] private bool lockPlayerInputOnDeath = true;

    [Header("Generation Seed")]
    [SerializeField] private int baseSeed = 1337;

    [Header("Runtime")]
    [SerializeField] private Transform levelsParent;

    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private float startY;
    private RunState state = RunState.RunStarting;

    private readonly List<GameObject> levelRoots = new();
    private readonly List<GameObject> spawnedCentipedes = new();
    private int currentLevelIndex = 0;

    public bool IsEndlessMode => true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        playerHealth = playerTransform != null ? playerTransform.GetComponent<PlayerHealth>() : null;
        startY = playerTransform != null ? playerTransform.position.y : 0f;

        Debug.Log($"EndlessDescent: startY={startY}, baseSeed={baseSeed}");

        // debug: list any DescentSphereTrigger instances in scene
        var triggers = FindObjectsOfType<DescentSphereTrigger>();
        Debug.Log($"EndlessDescent: found {triggers.Length} DescentSphereTrigger(s) in scene.");
        foreach (var t in triggers)
        {
            var col = t.GetComponent<Collider>();
            Debug.Log($" - Trigger: {t.gameObject.name}, pos={t.transform.position}, hasCollider={col!=null}, isTrigger={(col!=null?col.isTrigger:false)}");
        }

        // find existing Level_0 or generate first level
        GameObject existing = GameObject.Find("Level_0");
        if (existing != null)
        {
            levelRoots.Add(existing);
            currentLevelIndex = 0;
            Debug.Log("EndlessDescent: found existing Level_0 in scene.");
        }
        else
        {
            GameObject root = GenerateLevelInstance(0, 0f, baseSeed);
            if (root != null)
            {
                currentLevelIndex = 0;
            }
        }

        if (playerHealth != null)
        {
            playerHealth.OnDeath += OnPlayerHealthDeath;
        }

        state = RunState.Playing;
        LevelProgressionUI.EnsureInScene()?.UpdateProgress(completedLevels, GetDescentMeters());
    }

    private void Update()
    {
        if (playerTransform != null)
        {
            totalDescentMeters = Mathf.Max(0f, startY - playerTransform.position.y);
            if (state == RunState.Playing)
            {
                LevelProgressionUI.EnsureInScene()?.UpdateProgress(completedLevels, GetDescentMeters());
            }
        }
    }

    public float GetDescentMeters() => Mathf.Floor(Mathf.Max(0f, totalDescentMeters));

    public void CompleteCurrentLevel(DescentSphereTrigger source)
    {
        Debug.Log($"EndlessDescent: CompleteCurrentLevel called from source={source?.gameObject?.name}");
        if (!generateNextLevelOnSphereTouch)
        {
            Debug.Log("EndlessDescent: generation from sphere touch disabled.");
            return;
        }

        if (state == RunState.GeneratingNextLevel || state == RunState.Dead)
        {
            Debug.Log("EndlessDescent: ignoring sphere touch - already generating or dead.");
            return;
        }

        StartCoroutine(DoCompleteLevel());
    }

    private System.Collections.IEnumerator DoCompleteLevel()
    {
        state = RunState.CompletingLevel;
        completedLevels++;
        Debug.Log($"EndlessDescent: level completed. total completed={completedLevels}");

        if (healPlayerOnLevelComplete && playerHealth != null && playerHealth.IsAlive)
        {
            playerHealth.FullHeal();
            Debug.Log("EndlessDescent: healed player on level complete.");
        }

        LevelProgressionUI.EnsureInScene()?.UpdateProgress(completedLevels, GetDescentMeters());

        // compute next baseY
        float currentBaseY = 0f;
        if (levelRoots.Count > 0 && levelRoots[^1] != null)
        {
            currentBaseY = levelRoots[^1].transform.position.y;
        }
        float nextBaseY = currentBaseY - levelHeight;
        int nextIndex = currentLevelIndex + 1;

        state = RunState.GeneratingNextLevel;

        if (pauseDuringLevelGeneration)
        {
            Time.timeScale = 0f;
        }

        int levelSeed = baseSeed + nextIndex * 1009;
        Debug.Log($"EndlessDescent: generating level {nextIndex} at baseY={nextBaseY} seed={levelSeed}");

        GameObject newRoot = GenerateLevelInstance(nextIndex, nextBaseY, levelSeed);

        if (pauseDuringLevelGeneration)
        {
            Time.timeScale = 1f;
        }

        if (newRoot != null)
        {
            currentLevelIndex = nextIndex;
            // spawn centipede
            if (spawnNewCentipedeEachLevel)
            {
                SpawnCentipedeForLevel(newRoot.transform);
            }

            // cleanup old levels
            if (deleteOldLevels)
            {
                CleanupOldLevels();
            }
        }

        state = RunState.Playing;
        LevelProgressionUI.EnsureInScene()?.UpdateProgress(completedLevels, GetDescentMeters());
        yield break;
    }

    private GameObject GenerateLevelInstance(int levelIndex, float baseY, int seed)
    {
        ProceduralMegastructureGenerator generator = FindAnyObjectByType<ProceduralMegastructureGenerator>();
        if (generator == null)
        {
            Debug.LogError("EndlessDescent: No ProceduralMegastructureGenerator found in scene.");
            return null;
        }

        // set private fields via reflection
        Type genType = typeof(ProceduralMegastructureGenerator);
        FieldInfo seedField = genType.GetField("seed", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo randomizeField = genType.GetField("randomizeSeed", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo clearBeforeGenerateField = genType.GetField("clearBeforeGenerate", BindingFlags.Instance | BindingFlags.NonPublic);

        object prevSeed = null;
        object prevRandomize = null;
        if (seedField != null)
        {
            prevSeed = seedField.GetValue(generator);
            seedField.SetValue(generator, seed);
        }
        if (randomizeField != null)
        {
            prevRandomize = randomizeField.GetValue(generator);
            randomizeField.SetValue(generator, false);
        }
        if (clearBeforeGenerateField != null)
        {
            clearBeforeGenerateField.SetValue(generator, true);
        }

        // generate
        try
        {
            generator.GenerateLevel();
        }
        catch (Exception ex)
        {
            Debug.LogError($"EndlessDescent: generator threw exception: {ex}");
        }

        // restore fields
        if (seedField != null && prevSeed != null) seedField.SetValue(generator, prevSeed);
        if (randomizeField != null && prevRandomize != null) randomizeField.SetValue(generator, prevRandomize);

        // find produced root and rename/move it
        GameObject root = GameObject.Find("GeneratedLevel");
        if (root == null)
        {
            Debug.LogError("EndlessDescent: generated root not found after generation.");
            return null;
        }

        root.name = $"Level_{levelIndex}";
        root.transform.SetParent(levelsParent != null ? levelsParent : null, true);
        root.transform.position = new Vector3(0f, baseY, 0f);

        var levelComp = root.AddComponent<LevelInstanceRoot>();
        levelComp.LevelIndex = levelIndex;
        levelComp.BaseY = baseY;

        levelRoots.Add(root);
        Debug.Log($"EndlessDescent: generated level root '{root.name}' at Y={baseY}");
        return root;
    }

    private void SpawnCentipedeForLevel(Transform levelRoot)
    {
        CentipedeSpawner template = FindAnyObjectByType<CentipedeSpawner>();
        if (template == null)
        {
            Debug.LogWarning("EndlessDescent: no CentipedeSpawner template found in scene to spawn centipede.");
            return;
        }

        GameObject inst = GameObject.Instantiate(template.gameObject);
        inst.name = $"Centipede_Level_{levelRoot.GetInstanceID()}";
        CentipedeSpawner sp = inst.GetComponent<CentipedeSpawner>();
        if (sp != null)
        {
            // try to assign shaftCenter/player via reflection (fields may be private)
            FieldInfo shaftCenterField = typeof(CentipedeSpawner).GetField("shaftCenter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (shaftCenterField != null)
            {
                shaftCenterField.SetValue(sp, levelRoot);
            }
            FieldInfo playerField = typeof(CentipedeSpawner).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (playerField != null && playerTransform != null)
            {
                playerField.SetValue(sp, playerTransform);
            }
            // ensure auto-spawn and call spawn
            MethodInfo spawnMethod = typeof(CentipedeSpawner).GetMethod("SpawnOnWall", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (spawnMethod != null)
            {
                spawnMethod.Invoke(sp, null);
            }
            spawnedCentipedes.Add(inst);
            Debug.Log("EndlessDescent: spawned centipede for new level.");
        }
    }

    private void CleanupOldLevels()
    {
        if (levelRoots.Count <= keepPreviousLevelsCount + 1) return;
        int toKeep = keepPreviousLevelsCount + 1; // current + previous N
        int removeCount = levelRoots.Count - toKeep;
        for (int i = 0; i < removeCount; i++)
        {
            GameObject old = levelRoots[0];
            levelRoots.RemoveAt(0);
            if (old != null)
            {
                Debug.Log($"EndlessDescent: deleting old level {old.name}");
                if (Application.isPlaying)
                {
                    Destroy(old);
                }
                else
                {
                    DestroyImmediate(old);
                }
            }
            if (destroyOldCentipedes && spawnedCentipedes.Count > 0)
            {
                GameObject c = spawnedCentipedes[0];
                spawnedCentipedes.RemoveAt(0);
                if (c != null)
                {
                    Destroy(c);
                }
            }
        }
    }

    private void OnPlayerHealthDeath()
    {
        // legacy support - won't receive player reference
        OnPlayerDeath(playerHealth);
    }

    public void OnPlayerDeath(PlayerHealth who)
    {
        if (state == RunState.Dead) return;
        state = RunState.Dead;
        Debug.Log($"EndlessDescent: player died. completedLevels={completedLevels}, descentMeters={GetDescentMeters()}");

        if (freezeGameOnDeath)
        {
            Time.timeScale = 0f;
        }

        if (showCursorOnDeath)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (lockPlayerInputOnDeath && playerTransform != null)
        {
            var fps = playerTransform.GetComponent<FPSCharacterController3D>();
            if (fps != null)
            {
                fps.enabled = false;
            }
        }

        // stop centipedes
        foreach (var go in spawnedCentipedes)
        {
            if (go == null) continue;
            var controller = go.GetComponent<CentipedeController>();
            if (controller != null) controller.enabled = false;
        }

        // show results UI
        EndRunResultsUI.EnsureInScene()?.ShowResults(completedLevels, (int)GetDescentMeters());
    }
}
