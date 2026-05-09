using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

#pragma warning disable 414
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
    [SerializeField] private bool removeCompletedLevelImmediately = true;
    [SerializeField, Min(0f)] private float postCompleteDropClearance = 3f;
    [SerializeField] private bool playCentipedeIntroOnlyOnFirstLevel = true;
    [SerializeField] private bool allowCentipedeIntroOnGeneratedLevels = false;
    [SerializeField] private bool destroyPreviousCentipedeOnNewLevel = true;

    [Header("Transition")]
    [SerializeField] private bool repositionPlayerToNextLevelStart = true;
    [SerializeField] private float nextLevelStartYOffset = -5f;
    [SerializeField] private float nextLevelStartClearanceRadius = 2f;
    [SerializeField] private float nextLevelStartForwardOffset = 0f;
    [SerializeField] private bool resetPlayerVelocityOnLevelTransition = true;
    [SerializeField] private bool preserveSomeDownwardVelocity = true;
    [SerializeField] private float transitionDownwardVelocity = -3f;
    [SerializeField] private bool alignNewLevelBelowPlayer = true;
    [SerializeField] private float nextLevelEntryGap = 8f;
    [SerializeField] private float minDropBeforeFirstStructure = 5f;
    [SerializeField] private float maxAllowedLevelAlignmentError = 2f;
    [SerializeField] private float entryPointDownOffset = 2f;
    [SerializeField] private float entryPointClearanceRadius = 2f;
    [SerializeField] private bool keepPlayerWorldPositionOnLevelTransition = true;
    [SerializeField] private bool onlyResetPlayerVelocityOnTransition = true;
    [SerializeField] private bool cancelActiveGrappleOnLevelTransition = true;
    [SerializeField] private float centipedeSpawnSurfaceOffset = 0.5f;
    [Header("Endless Mode Alignment")]
    [SerializeField] private float verticalGapBetweenLevels = 12f;
    [SerializeField] private float firstPlatformTargetDrop = 20f;
    [SerializeField] private float maxFirstPlatformDrop = 35f;
    [SerializeField] private bool keepPlayerOnOldPedestalUntilNextLevelReady = true;
    [SerializeField] private bool deleteFinalArenaAfterNextLevelReady = true;
    [SerializeField] private bool splitCompletedLevelOnTransition = true;
    [SerializeField, Range(0f,1f)] private float completedLevelUpperHalfDeleteRatio = 0.5f;
    [SerializeField] private bool deleteAllLevelsOlderThanPrevious = true;

    [Header("First Landing Validation")]
    [SerializeField] private bool ensureFirstLandingPlatform = true;
    [SerializeField] private float firstLandingSearchRadius = 10f;
    [SerializeField] private float firstLandingMaxDrop = 35f;
    [SerializeField] private bool addEmergencyLandingPlatformIfMissing = true;
    [SerializeField] private LayerMask firstLandingLayerMask = ~0;

    [Header("Centipede Spawning")]
    [SerializeField, Min(1)] private int baseCentipedesPerLevel = 1;
    [SerializeField, Min(0)] private int additionalCentipedesPerLevel = 1;
    [SerializeField, Min(1)] private int maxCentipedesPerLevel = 8;
    [SerializeField] private bool scaleCentipedeCountWithLevel = true;
    [SerializeField] private float centipedeMinSpawnDistanceFromPlayer = 25f;
    [SerializeField] private float centipedeMaxSpawnDistanceFromPlayer = 120f;
    [SerializeField] private float centipedeMinDistanceBetweenSpawns = 20f;
    [SerializeField] private int centipedeSpawnAttemptsPerMonster = 30;
    [SerializeField] private bool preferShaftWallSpawns = true;
    [SerializeField] private bool allowStructureSurfaceSpawns = true;
    [SerializeField] private LayerMask centipedeSpawnSurfaceMask = ~0;
    [SerializeField] private bool playIntroOnlyForFirstCentipedeOfRun = true;

    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private float startY;
    private RunState state = RunState.RunStarting;

    private readonly List<GameObject> levelRoots = new();
    private readonly List<GameObject> spawnedCentipedes = new();
    private readonly List<GameObject> activeCentipedes = new();
    private int currentLevelIndex = 0;
    private GameObject activeCentipede;
    private bool introPlayedThisRun = false;

    public bool IsEndlessMode => true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"EndlessDescentGameManager duplicate detected, destroying duplicate on {gameObject.name}");
            Destroy(this);
            return;
        }
        Instance = this;
        Debug.Log($"EndlessDescentGameManager Awake: instance set on {gameObject.name}");
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        playerHealth = playerTransform != null ? playerTransform.GetComponent<PlayerHealth>() : null;
        startY = playerTransform != null ? playerTransform.position.y : 0f;

        Debug.Log($"EndlessDescent: startY={startY}, baseSeed={baseSeed}");

        Debug.Log($"EndlessDescent Start: state={state}, playerFound={(playerTransform!=null)}, playerHealth={(playerHealth!=null)}, generateNextLevelOnSphereTouch={generateNextLevelOnSphereTouch}");

        // debug: list any DescentSphereTrigger instances in scene
        var triggers = UnityEngine.Object.FindObjectsByType<DescentSphereTrigger>();
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

        // detect any existing active centipede in scene and register as activeCentipede
        var existingControllers = UnityEngine.Object.FindObjectsByType<CentipedeController>();
        if (existingControllers != null && existingControllers.Length > 0)
        {
            activeCentipede = existingControllers[0].gameObject;
            spawnedCentipedes.Add(activeCentipede);
            Debug.Log($"EndlessDescent: registered existing active centipede '{activeCentipede.name}'");
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
        Debug.Log($"EndlessDescent: CompleteCurrentLevel called. state={state}, generateNextLevelOnSphereTouch={generateNextLevelOnSphereTouch}, source={source?.gameObject?.name}");
        bool accepted = TryCompleteCurrentLevel(source);
        if (!accepted)
        {
            Debug.LogWarning("EndlessDescent: CompleteCurrentLevel request not accepted.");
        }
    }

    public bool TryCompleteCurrentLevel(DescentSphereTrigger source)
    {
        Debug.Log($"EndlessDescent: TryCompleteCurrentLevel called. state={state}, generateNextLevelOnSphereTouch={generateNextLevelOnSphereTouch}, source={source?.gameObject?.name}");
        if (!generateNextLevelOnSphereTouch)
        {
            Debug.LogWarning("EndlessDescent: generation from sphere touch disabled.");
            return false;
        }

        if (state == RunState.GeneratingNextLevel || state == RunState.Dead || state == RunState.CompletingLevel)
        {
            Debug.LogWarning($"EndlessDescent: cannot accept completion, current state={state}");
            return false;
        }

        Debug.Log("EndlessDescent: starting DoCompleteLevel coroutine");
        try
        {
            StartCoroutine(DoCompleteLevel(source));
        }
        catch (Exception ex)
        {
            Debug.LogError($"EndlessDescent: exception starting DoCompleteLevel: {ex}");
            state = RunState.Playing;
            return false;
        }
        return true;
    }

    private System.Collections.IEnumerator DoCompleteLevel(DescentSphereTrigger source)
    {
        Debug.Log("EndlessDescent: DoCompleteLevel started");

        state = RunState.CompletingLevel;

        // disable source sphere to avoid double-activation (DescentSphereTrigger also handles this)
        if (source != null)
        {
            var col = source.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        // identify which level was completed
        GameObject completedLevelRoot = FindOwningLevelRoot(source);
        Debug.Log($"EndlessDescent: completed level root = {(completedLevelRoot!=null?completedLevelRoot.name:"<null>")}");

        int before = completedLevels;
        completedLevels++;
        Debug.Log($"EndlessDescent: completedLevels {before} -> {completedLevels}");

        if (healPlayerOnLevelComplete)
        {
            if (playerHealth != null && playerHealth.IsAlive)
            {
                playerHealth.FullHeal();
                Debug.Log("EndlessDescent: healed player on level complete.");
            }
            else
            {
                Debug.LogWarning("EndlessDescent: heal requested but playerHealth missing or player not alive.");
            }
        }

        LevelProgressionUI.EnsureInScene()?.UpdateProgress(completedLevels, GetDescentMeters());

        // compute next baseY based on the completed level
        float completedBaseY = 0f;
        if (completedLevelRoot != null)
        {
            var lr = completedLevelRoot.GetComponent<LevelInstanceRoot>();
            if (lr != null) completedBaseY = lr.BaseY;
            else completedBaseY = completedLevelRoot.transform.position.y;
        }
        else if (levelRoots.Count > 0 && levelRoots[^1] != null)
        {
            completedBaseY = levelRoots[^1].transform.position.y;
        }

        float nextBaseY = completedBaseY - levelHeight;
        int nextIndex = currentLevelIndex + 1;

        Debug.Log($"EndlessDescent: completedBaseY={completedBaseY}, nextBaseY={nextBaseY}, nextIndex={nextIndex}");

        // compute completed bounds for diagnostics (do not treat as authoritative)
        Bounds completedBounds = new Bounds(completedLevelRoot != null ? completedLevelRoot.transform.position : Vector3.zero, Vector3.one);
        if (completedLevelRoot != null)
        {
            if (TryCalculateLevelBounds(completedLevelRoot, out Bounds cb))
            {
                completedBounds = cb;
            }
            else
            {
                var lr = completedLevelRoot.GetComponent<LevelInstanceRoot>();
                if (lr != null)
                {
                    completedBounds = lr.LevelBounds;
                }
            }
        }

        GameObject finalArenaRoot = null;
        if (completedLevelRoot != null)
        {
            finalArenaRoot = FindFinalArenaRoot(completedLevelRoot);
            if (finalArenaRoot != null)
            {
                Debug.Log($"EndlessDescent: found final arena '{finalArenaRoot.name}' (deletion deferred until new level validated)");
            }
            else
            {
                Debug.Log("EndlessDescent: no explicit final arena found (will attempt to remove pedestal/sphere after alignment)");
            }
                // Defer removal of Shaft Bottom Floor until after the next level is successfully generated and validated.
                // Removal will be performed together with final arena and other transition-removal objects.
        }

        Vector3 oldExitPosition = GetLevelExitPosition(source, completedLevelRoot);
        float oldExitY = oldExitPosition.y;
        Debug.Log($"EndlessDescent: source/exit position = {oldExitPosition} (oldExitY={oldExitY:F2})");

        state = RunState.GeneratingNextLevel;

        if (pauseDuringLevelGeneration)
        {
            Time.timeScale = 0f;
        }

        int levelSeed = baseSeed + nextIndex * 1009;
        Debug.Log($"EndlessDescent: generating level {nextIndex} seed={levelSeed}");

        GameObject newRoot = null;
        try
        {
            // generate next level without auto-shifting based on player
            newRoot = GenerateLevelInstance(nextIndex, nextBaseY, levelSeed, false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"EndlessDescent: exception during GenerateLevelInstance: {ex}");
        }

        if (pauseDuringLevelGeneration)
        {
            Time.timeScale = 1f;
        }

        if (newRoot == null)
        {
            Debug.LogError("EndlessDescent: GenerateLevelInstance returned null. Restoring state and re-enabling trigger if present.");
            // re-enable trigger collider so player can try again
            if (source != null)
            {
                var col = source.GetComponent<Collider>();
                if (col != null) col.enabled = true;
            }
            state = RunState.Playing;
            yield break;
        }
        // successfully generated new level
        currentLevelIndex = nextIndex;
        Debug.Log($"EndlessDescent: GenerateLevelInstance returned '{newRoot.name}'");

        LevelInstanceRoot newLevelComp = newRoot.GetComponent<LevelInstanceRoot>();

        // calculate bounds of new level before alignment
        if (!TryCalculateLevelBounds(newRoot, out Bounds boundsBefore))
        {
            Debug.LogError($"EndlessDescent: failed to calculate bounds for generated root '{newRoot.name}'. Aborting transition.");
            if (source != null)
            {
                var col = source.GetComponent<Collider>(); if (col != null) col.enabled = true;
            }
            state = RunState.Playing;
            yield break;
        }
        Debug.Log($"EndlessDescent: generated '{newRoot.name}' bounds before alignment min={boundsBefore.min}, max={boundsBefore.max}, center={boundsBefore.center}");

        // delete upper half of the completed level to free memory
        if (splitCompletedLevelOnTransition && completedLevelRoot != null)
        {
            DeleteUpperHalfOfCompletedLevel(completedLevelRoot.GetComponent<LevelInstanceRoot>(), completedBounds);
        }

        // align next level using explicit EntryAnchor -> ExitAnchor anchors (avoid using global bounds)
        var entryT = EnsureLevelEntryAnchor(newRoot);
        if (entryT == null)
        {
            Debug.LogError($"EndlessDescent: no EntryAnchor found on new level '{newRoot.name}'. Aborting transition.");
            if (source != null)
            {
                var col = source.GetComponent<Collider>(); if (col != null) col.enabled = true;
            }
            state = RunState.Playing;
            yield break;
        }

        Debug.Log($"EndlessDescent: Level '{newRoot.name}' EntryAnchor before alignment = {entryT.position}");

        float desiredEntryY = oldExitPosition.y - verticalGapBetweenLevels;
        float deltaY = desiredEntryY - entryT.position.y;
        newRoot.transform.position += Vector3.up * deltaY;
        Debug.Log($"EndlessDescent: desiredEntryY={desiredEntryY:F2}, applied deltaY={deltaY:F2} to '{newRoot.name}'");

        // update entry anchor reference after move
        entryT = EnsureLevelEntryAnchor(newRoot);
        float actualGap = oldExitPosition.y - entryT.position.y;
        Debug.Log($"EndlessDescent: Level '{newRoot.name}' EntryAnchor after alignment = {entryT.position}; transition gap = {actualGap:F2}");

        if (Mathf.Abs(actualGap - verticalGapBetweenLevels) > maxAllowedLevelAlignmentError)
        {
            Debug.LogError($"EndlessDescent: alignment gap too large: actualGap={actualGap:F2}, expected~{verticalGapBetweenLevels:F2}");
            if (source != null)
            {
                var col = source.GetComponent<Collider>(); if (col != null) col.enabled = true;
            }
            state = RunState.Playing;
            yield break;
        }

        // recalc bounds for diagnostics and caching (do not use for alignment decisions)
        if (TryCalculateLevelBounds(newRoot, out Bounds boundsAfter))
        {
            Debug.Log($"EndlessDescent: '{newRoot.name}' bounds after alignment min={boundsAfter.min}, max={boundsAfter.max}, center={boundsAfter.center}");
            if (newLevelComp != null)
            {
                newLevelComp.LevelBounds = boundsAfter;
                newLevelComp.TopY = boundsAfter.max.y;
            }
        }

        // validate first landing surface below the player in the new level
        bool landingOk = true;
        Vector3 landingPoint = Vector3.zero;
        Collider landingCollider = null;
        float landingDrop = 0f;
        if (ensureFirstLandingPlatform && playerTransform != null)
        {
            if (!TryFindFirstLandingSurfaceBelowPlayer(newRoot, playerTransform.position, out landingPoint, out landingCollider, out landingDrop))
            {
                Debug.LogWarning($"EndlessDescent: no first landing platform found under player within {firstLandingMaxDrop}m in new level '{newRoot.name}'");
                if (addEmergencyLandingPlatformIfMissing)
                {
                    GameObject ep = CreateEmergencyLandingPlatform(newRoot, playerTransform.position);
                    if (ep != null)
                    {
                        landingOk = true;
                        landingPoint = ep.transform.position;
                        landingCollider = ep.GetComponent<Collider>();
                        landingDrop = Mathf.Abs(playerTransform.position.y - landingPoint.y);
                        Debug.Log($"EndlessDescent: created emergency landing platform at {landingPoint} drop={landingDrop:F2}");
                    }
                    else
                    {
                        landingOk = false;
                    }
                }
                else landingOk = false;
            }
            else
            {
                Debug.Log($"EndlessDescent: first landing found at {landingPoint} drop={landingDrop:F2}, collider={landingCollider?.name}");
            }
        }

        if (!landingOk)
        {
            Debug.LogError("EndlessDescent: first landing validation failed; aborting transition and preserving final arena.");
            if (source != null)
            {
                var col = source.GetComponent<Collider>(); if (col != null) col.enabled = true;
            }
            state = RunState.Playing;
            yield break;
        }

        // spawn centipedes now that next level is validated and aligned
        if (spawnNewCentipedeEachLevel)
        {
            var spawned = SpawnCentipedesForLevel(newLevelComp, nextIndex, playerTransform != null ? playerTransform.position : Vector3.zero);
            Debug.Log($"EndlessDescent: SpawnCentipedesForLevel requested, spawned {spawned.Count}.");
            if (spawned != null && spawned.Count > 0)
            {
                activeCentipede = spawned[^1];
            }
        }

        // remove completed final arena / pedestal / sphere now that new level is validated and aligned
        if (completedLevelRoot != null)
        {
            try
            {
                RemoveCompletedFinalArena(completedLevelRoot, source);
                Debug.Log($"EndlessDescent: removed completed final arena from '{completedLevelRoot.name}'");
                try
                {
                    int removedBottomCount = RemoveCompletedBottomFloors(completedLevelRoot, source);
                    int removedTransitionCount = RemoveTransitionRemovalObjects(completedLevelRoot);
                    Debug.Log($"EndlessDescent: removed {removedBottomCount} bottom floor(s) and {removedTransitionCount} transition object(s) from '{completedLevelRoot.name}'");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"EndlessDescent: failed to remove bottom floors/transition objects cleanly: {ex}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"EndlessDescent: failed to remove final arena cleanly: {ex}");
            }
        }
        // ensure sphere object itself is also destroyed if still present
        if (source != null && source.gameObject != null)
        {
            try { if (Application.isPlaying) Destroy(source.gameObject); else DestroyImmediate(source.gameObject); } catch { }
        }

        // ensure grapple is released so player falls cleanly
        if (cancelActiveGrappleOnLevelTransition && playerTransform != null)
        {
            var grapple = playerTransform.GetComponentInChildren<GrapplingHookController3D>();
            if (grapple != null)
            {
                try { grapple.ReleaseGrapple(); } catch { }
            }
        }

        // optionally give a small downward push to ensure falling
        if (playerTransform != null)
        {
            var fps = playerTransform.GetComponentInParent<FPSCharacterController3D>();
            if (fps != null)
            {
                try { fps.SetCurrentVelocity(new Vector3(0f, transitionDownwardVelocity, 0f)); } catch { }
            }
            else
            {
                var rb = playerTransform.GetComponentInParent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, transitionDownwardVelocity), rb.linearVelocity.z);
                }
            }
        }

        // cleanup old levels older than previous as requested
        if (deleteAllLevelsOlderThanPrevious)
        {
            CleanupLevelsOlderThanPrevious(nextIndex);
        }

        // update current level index and manager state
        currentLevelIndex = nextIndex;
        state = RunState.Playing;

        LogSceneCounts("after transition");

        LevelProgressionUI.EnsureInScene()?.UpdateProgress(completedLevels, GetDescentMeters());

        Debug.Log("EndlessDescent: DoCompleteLevel finished");
        yield break;
    }

    private GameObject GenerateLevelInstance(int levelIndex, float baseY, int seed, bool autoShiftByPlayer = true)
    {
        ProceduralMegastructureGenerator generator = FindAnyObjectByType<ProceduralMegastructureGenerator>();
        if (generator == null)
        {
            Debug.LogError("EndlessDescent: No ProceduralMegastructureGenerator found in scene.");
            return null;
        }

        Debug.Log($"EndlessDescent: found ProceduralMegastructureGenerator on {generator.gameObject.name}");

        LevelInstanceRoot newLevelComp = null;
        try
        {
            newLevelComp = generator.GenerateLevelInstance(levelIndex, baseY, seed, levelsParent);
        }
        catch (Exception ex)
        {
            Debug.LogError($"EndlessDescent: generator.GenerateLevelInstance threw exception: {ex}");
            return null;
        }

        if (newLevelComp == null)
        {
            Debug.LogError("EndlessDescent: generator returned null for GenerateLevelInstance.");
            return null;
        }

        GameObject root = newLevelComp.gameObject;

        // optionally adjust position so top is below player (preserve compatibility with previous behavior)
        if (autoShiftByPlayer && playerTransform != null)
        {
            float playerY = playerTransform.position.y;
            float topY = newLevelComp.TopY != 0f ? newLevelComp.TopY : (newLevelComp.LevelBounds.size != Vector3.zero ? newLevelComp.LevelBounds.max.y : root.transform.position.y);
            float requiredTop = playerY - postCompleteDropClearance;
            if (topY > requiredTop)
            {
                float shift = topY - requiredTop;
                root.transform.position = root.transform.position - new Vector3(0f, shift, 0f);
                newLevelComp.BaseY = root.transform.position.y;
                newLevelComp.TopY = newLevelComp.TopY - shift;
                Bounds b = newLevelComp.LevelBounds;
                b.min = b.min - new Vector3(0f, shift, 0f);
                b.max = b.max - new Vector3(0f, shift, 0f);
                newLevelComp.LevelBounds = b;
                Debug.Log($"EndlessDescent: adjusted generated level down by {shift} to ensure clearance; new baseY={root.transform.position.y}");
            }
        }

        // ensure transition anchors exist (generator should have created them, but keep a safe fallback)
        var anchorsComp = root.GetComponent<LevelTransitionAnchors>();
        if (anchorsComp == null) anchorsComp = root.AddComponent<LevelTransitionAnchors>();

        if (anchorsComp.EntryAnchor == null)
        {
            // find named child entry anchor if generator created it
            Transform foundEntry = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (t.name.Equals("Level Entry Anchor", StringComparison.OrdinalIgnoreCase))
                {
                    foundEntry = t; break;
                }
            }
            if (foundEntry != null)
            {
                anchorsComp.EntryAnchor = foundEntry;
                if (newLevelComp != null && newLevelComp.EntryPoint == null) newLevelComp.EntryPoint = foundEntry;
                Debug.Log($"EndlessDescent: found existing EntryAnchor '{foundEntry.name}' for '{root.name}' at {foundEntry.position}");
            }
        }

        levelRoots.Add(root);
        Debug.Log($"EndlessDescent: generated level root '{root.name}' at Y={baseY}");
        return root;
    }

    private void SpawnCentipedeForLevel(Transform levelRoot, int levelIndex)
    {
        CentipedeSpawner template = FindAnyObjectByType<CentipedeSpawner>();
        if (template == null)
        {
            Debug.LogWarning("EndlessDescent: no CentipedeSpawner template found in scene to spawn centipede.");
            return;
        }
        // destroy previous active centipede if requested
        if (destroyPreviousCentipedeOnNewLevel && activeCentipede != null)
        {
            if (Application.isPlaying) Destroy(activeCentipede);
            else DestroyImmediate(activeCentipede);
            Debug.Log("EndlessDescent: destroyed previous active centipede before spawning new one.");
            activeCentipede = null;
        }

        GameObject inst = GameObject.Instantiate(template.gameObject);
        inst.name = $"Centipede_Level_{levelIndex}";
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

            // disable intro focus on spawned prefab for subsequent levels
            bool allowIntro = (!playCentipedeIntroOnlyOnFirstLevel) || (levelIndex == 0) || allowCentipedeIntroOnGeneratedLevels;
            var introComp = inst.GetComponentInChildren<CentipedeIntroFocus>(true);
            if (introComp != null)
            {
                try
                {
                    var fi = typeof(CentipedeIntroFocus).GetField("playIntroOnStart", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (fi != null)
                    {
                        fi.SetValue(introComp, allowIntro);
                        Debug.Log($"EndlessDescent: set CentipedeIntroFocus.playIntroOnStart={allowIntro} on '{inst.name}'");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"EndlessDescent: failed to set CentipedeIntroFocus.playIntroOnStart via reflection: {ex}");
                }
            }

            // generic attempt: set private 'playIntroOnStart' on any component that has it
            var comps = inst.GetComponentsInChildren<Component>(true);
            foreach (var c in comps)
            {
                if (c == null) continue;
                try
                {
                    var f = c.GetType().GetField("playIntroOnStart", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (f != null)
                    {
                        f.SetValue(c, allowIntro);
                        Debug.Log($"EndlessDescent: set playIntroOnStart={allowIntro} on component {c.GetType().Name} of '{inst.name}'");
                    }
                }
                catch { }
            }

            // ensure auto-spawn and call spawn
            MethodInfo spawnMethod = typeof(CentipedeSpawner).GetMethod("SpawnOnWall", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (spawnMethod != null)
            {
                spawnMethod.Invoke(sp, null);
            }

            spawnedCentipedes.Add(inst);
            activeCentipede = inst;
            Debug.Log($"EndlessDescent: spawned centipede for new level '{inst.name}' (introAllowed={allowIntro}).");
        }
    }

    private bool TryRepositionPlayerToLevelStart(GameObject newRoot, LevelInstanceRoot levelComp)
    {
        if (!repositionPlayerToNextLevelStart)
        {
            return false;
        }

        if (playerTransform == null)
        {
            Debug.LogWarning("EndlessDescent: cannot reposition player - playerTransform missing.");
            return false;
        }

        Vector3 oldPos = playerTransform.position;
        Transform rootT = newRoot != null ? newRoot.transform : null;
        Vector3 center = rootT != null ? rootT.position : Vector3.zero;
        Bounds bounds = levelComp != null ? levelComp.LevelBounds : new Bounds(center, Vector3.one * (levelHeight > 0f ? levelHeight : 100f));
        float topY = levelComp != null && levelComp.TopY != 0f ? levelComp.TopY : bounds.max.y;

        Vector3 desired = new Vector3(center.x, topY + nextLevelStartYOffset, center.z + nextLevelStartForwardOffset);

        var fps = playerTransform.GetComponentInParent<FPSCharacterController3D>();
        CharacterController cc = fps != null ? fps.CharacterController : playerTransform.GetComponentInChildren<CharacterController>();
        var grapple = playerTransform.GetComponentInChildren<GrapplingHookController3D>();

        // release active grapple and pause grapple control
        if (grapple != null)
        {
            try { grapple.ReleaseGrapple(); } catch { }
        }
        if (fps != null)
        {
            try { fps.SetGrappleMovementPaused(true); fps.SetGrappleMovementControlActive(false); } catch { }
        }

        bool ccWasEnabled = true;
        if (cc != null)
        {
            ccWasEnabled = cc.enabled;
            cc.enabled = false;
        }

        // clearance check
        float radius = 0.5f;
        float height = 1.8f;
        if (cc != null)
        {
            radius = Mathf.Max(0.1f, cc.radius);
            height = Mathf.Max(0.5f, cc.height);
        }

        Vector3 finalPos = desired;
        bool foundFree = false;
        Vector3 capBottom = finalPos + Vector3.up * radius;
        Vector3 capTop = finalPos + Vector3.up * (height - radius);

        int attempts = 12;
        if (!Physics.CheckCapsule(capBottom, capTop, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            foundFree = true;
        }
        else
        {
            for (int i = 0; i < attempts; i++)
            {
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float off = UnityEngine.Random.Range(0f, nextLevelStartClearanceRadius);
                Vector3 cand = desired + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * off;
                capBottom = cand + Vector3.up * radius;
                capTop = cand + Vector3.up * (height - radius);
                if (!Physics.CheckCapsule(capBottom, capTop, radius, ~0, QueryTriggerInteraction.Ignore))
                {
                    finalPos = cand;
                    foundFree = true;
                    break;
                }
            }
        }

        // move player
        try
        {
            playerTransform.position = finalPos;
            Debug.Log($"EndlessDescent: repositioned player from {oldPos} to {finalPos} (free={foundFree})");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"EndlessDescent: failed to set player position: {ex}");
        }

        // reset velocities
        if (fps != null)
        {
            try
            {
                if (resetPlayerVelocityOnLevelTransition)
                {
                    float down = preserveSomeDownwardVelocity ? Mathf.Abs(transitionDownwardVelocity) * -1f : 0f;
                    fps.SetCurrentVelocity(new Vector3(0f, down, 0f));
                }
            }
            catch { }
        }
        else
        {
            var rb = playerTransform.GetComponentInParent<Rigidbody>();
            if (rb != null && resetPlayerVelocityOnLevelTransition)
            {
                rb.linearVelocity = preserveSomeDownwardVelocity ? Vector3.up * transitionDownwardVelocity : Vector3.zero;
            }
        }

        // restore controller and grapple control
        if (cc != null)
        {
            cc.enabled = ccWasEnabled;
        }
        if (fps != null)
        {
            try { fps.SetGrappleMovementControlActive(true); fps.SetGrappleMovementPaused(false); } catch { }
        }

        return true;
    }

    private List<GameObject> SpawnCentipedesForLevel(LevelInstanceRoot levelRoot, int levelIndex, Vector3 playerPosition)
    {
        List<GameObject> created = new List<GameObject>();
        CentipedeSpawner template = FindAnyObjectByType<CentipedeSpawner>();
        if (template == null)
        {
            Debug.LogWarning("EndlessDescent: no CentipedeSpawner template found in scene to spawn centipedes.");
            return created;
        }

        int count = baseCentipedesPerLevel;
        if (scaleCentipedeCountWithLevel)
        {
            count = baseCentipedesPerLevel + levelIndex * additionalCentipedesPerLevel;
        }
        count = Mathf.Clamp(count, 1, maxCentipedesPerLevel);

        Bounds bounds = levelRoot != null ? levelRoot.LevelBounds : new Bounds(levelRoot != null ? levelRoot.transform.position : Vector3.zero, Vector3.one * levelHeight);
        Vector3 center = levelRoot != null ? levelRoot.transform.position : Vector3.zero;

        List<Vector3> existingSpawns = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            if (!TryFindCentipedeSpawnOnLevelSurface(levelRoot != null ? levelRoot.gameObject : null, bounds, playerPosition, existingSpawns, out Vector3 spawnPos, out Vector3 spawnNormal))
            {
                Debug.LogWarning($"EndlessDescent: failed to find valid spawn position for centipede #{i} in level {levelIndex} after {centipedeSpawnAttemptsPerMonster} attempts.");
                continue;
            }

            GameObject inst = GameObject.Instantiate(template.gameObject, spawnPos, Quaternion.identity);
            inst.name = $"Centipede_Level_{levelIndex}_{i:00}";

            // prefer setting controller directly and attaching to surface so we always anchor on the real collider
            var controller = inst.GetComponent<CentipedeController>();
            if (controller != null)
            {
                controller.Player = playerTransform;
                Vector3 preferredForward = Vector3.ProjectOnPlane(playerPosition - spawnPos, spawnNormal).normalized;
                if (preferredForward.sqrMagnitude < 0.001f)
                {
                    preferredForward = Vector3.Cross(spawnNormal, Vector3.up).normalized;
                }
                controller.AttachToSurface(spawnPos, spawnNormal, preferredForward);
            }

            // disable intro for spawned prefabs depending on run state
            bool allowIntro = !playIntroOnlyForFirstCentipedeOfRun || !introPlayedThisRun;
            if (allowIntro && playIntroOnlyForFirstCentipedeOfRun)
            {
                introPlayedThisRun = true;
            }

            var introComp = inst.GetComponentInChildren<CentipedeIntroFocus>(true);
            if (introComp != null)
            {
                try
                {
                    var fi = typeof(CentipedeIntroFocus).GetField("playIntroOnStart", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (fi != null) fi.SetValue(introComp, allowIntro);
                }
                catch { }
            }

            spawnedCentipedes.Add(inst);
            activeCentipedes.Add(inst);
            created.Add(inst);
            existingSpawns.Add(spawnPos);
            Debug.Log($"EndlessDescent: spawned centipede #{i} for level {levelIndex} at {spawnPos} (introAllowed={allowIntro}).");
        }

        return created;
    }

    private GameObject FindOwningLevelRoot(Component source)
    {
        if (source == null)
        {
            return levelRoots.Count > 0 ? levelRoots[^1] : null;
        }

        Transform t = source.transform;
        while (t != null)
        {
            var lr = t.GetComponent<LevelInstanceRoot>();
            if (lr != null) return t.gameObject;
            t = t.parent;
        }

        // fallback: find nearest Level_* root from levelRoots by Y
        GameObject best = null;
        float bestDist = float.MaxValue;
        foreach (var root in levelRoots)
        {
            if (root == null) continue;
            float dist = Mathf.Abs(root.transform.position.y - source.transform.position.y);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = root;
            }
        }
        return best;
    }

    private bool TryCalculateLevelBounds(GameObject root, out Bounds outBounds)
    {
        outBounds = default;
        if (root == null) return false;

        bool found = false;
        Bounds b = new Bounds(root.transform.position, Vector3.zero);

        // aggregate renderer bounds first
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!found)
            {
                b = r.bounds;
                found = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        // then collider bounds (non-trigger)
        var colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            if (c == null) continue;
            if (c.isTrigger) continue;
            if (!found)
            {
                b = c.bounds;
                found = true;
            }
            else
            {
                b.Encapsulate(c.bounds);
            }
        }

        if (!found) return false;
        outBounds = b;
        return true;
    }

    private bool TryFindCentipedeSpawnOnLevelSurface(GameObject levelRoot, Bounds levelBounds, Vector3 playerPos, List<Vector3> existingSpawns, out Vector3 spawnPosition, out Vector3 spawnNormal)
    {
        spawnPosition = Vector3.zero;
        spawnNormal = Vector3.up;
        if (levelRoot == null)
        {
            return false;
        }

        var colliders = new List<Collider>(levelRoot.GetComponentsInChildren<Collider>(true));
        colliders.RemoveAll(c => c == null || c.isTrigger || ((centipedeSpawnSurfaceMask.value & (1 << c.gameObject.layer)) == 0));
        if (colliders.Count == 0) return false;

        for (int attempt = 0; attempt < centipedeSpawnAttemptsPerMonster; attempt++)
        {
            var c = colliders[UnityEngine.Random.Range(0, colliders.Count)];
            if (c == null) continue;

            Bounds cb = c.bounds;

            // choose face axis (prefer walls X/Z if requested)
            int axis;
            if (preferShaftWallSpawns)
            {
                axis = UnityEngine.Random.value > 0.5f ? 0 : 2; // 0=X, 2=Z
            }
            else
            {
                axis = UnityEngine.Random.Range(0, 3);
            }

            int sign = UnityEngine.Random.value > 0.5f ? 1 : -1;
            Vector3 faceNormal = Vector3.right;
            Vector3 sample = cb.center;
            if (axis == 0)
            {
                faceNormal = sign == 1 ? Vector3.right : -Vector3.right;
                sample.x = sign == 1 ? cb.max.x : cb.min.x;
                sample.y = UnityEngine.Random.Range(cb.min.y + 0.1f, cb.max.y - 0.1f);
                sample.z = UnityEngine.Random.Range(cb.min.z, cb.max.z);
            }
            else if (axis == 1)
            {
                faceNormal = sign == 1 ? Vector3.up : -Vector3.up;
                sample.y = sign == 1 ? cb.max.y : cb.min.y;
                sample.x = UnityEngine.Random.Range(cb.min.x, cb.max.x);
                sample.z = UnityEngine.Random.Range(cb.min.z, cb.max.z);
            }
            else
            {
                faceNormal = sign == 1 ? Vector3.forward : -Vector3.forward;
                sample.z = sign == 1 ? cb.max.z : cb.min.z;
                sample.x = UnityEngine.Random.Range(cb.min.x, cb.max.x);
                sample.y = UnityEngine.Random.Range(cb.min.y + 0.1f, cb.max.y - 0.1f);
            }

            Vector3 origin = sample + faceNormal * 0.5f;
            Vector3 dir = -faceNormal;
            float distance = Mathf.Max(cb.extents.x, Mathf.Max(cb.extents.y, cb.extents.z)) * 2f + 0.5f;

                RaycastHit hit = default;
            bool hitOk = false;
            // try collider-specific raycast first
            try
            {
                if (c.Raycast(new Ray(origin, dir), out hit, distance))
                {
                    hitOk = true;
                }
            }
            catch { hitOk = false; }

            // fallback: raycast from inside bounds towards center
            if (!hitOk)
            {
                Vector3 inside = new Vector3(UnityEngine.Random.Range(cb.min.x, cb.max.x), UnityEngine.Random.Range(cb.min.y, cb.max.y), UnityEngine.Random.Range(cb.min.z, cb.max.z));
                Vector3 dir2 = (inside - cb.center).normalized;
                if (dir2.sqrMagnitude < 0.001f) dir2 = Vector3.down;
                if (Physics.Raycast(inside, dir2, out hit, distance, centipedeSpawnSurfaceMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider == c) hitOk = true;
                }
            }

            if (!hitOk) continue;

            // validate hit point sits in level bounds
            if (!levelBounds.Contains(hit.point)) continue;

            float distToPlayer = Vector3.Distance(hit.point, playerPos);
            if (distToPlayer < centipedeMinSpawnDistanceFromPlayer || distToPlayer > centipedeMaxSpawnDistanceFromPlayer) continue;

            bool tooClose = false;
            if (existingSpawns != null)
            {
                foreach (var ex in existingSpawns)
                {
                    if (Vector3.Distance(hit.point, ex) < centipedeMinDistanceBetweenSpawns)
                    {
                        tooClose = true; break;
                    }
                }
            }
            if (tooClose) continue;

            // basic clearance check: ensure overlaps belong only to this level or are triggers
            Collider[] overlaps = Physics.OverlapSphere(hit.point + hit.normal * centipedeSpawnSurfaceOffset, entryPointClearanceRadius, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            foreach (var ov in overlaps)
            {
                if (ov == null) continue;
                if (ov.transform.IsChildOf(levelRoot.transform)) continue;
                if (playerTransform != null && ov.transform.IsChildOf(playerTransform)) { blocked = true; break; }
                blocked = true; break;
            }
            if (blocked) continue;

            spawnPosition = hit.point + hit.normal * centipedeSpawnSurfaceOffset;
            spawnNormal = hit.normal;
            return true;
        }

        return false;
    }

    private void RemoveCompletedLevelAfterTransition(GameObject completedLevelRoot)
    {
        if (completedLevelRoot == null)
        {
            Debug.LogWarning("EndlessDescent: RemoveCompletedLevelAfterTransition called with null");
            return;
        }

        var lr = completedLevelRoot.GetComponent<LevelInstanceRoot>();
        if (lr == null && !completedLevelRoot.name.StartsWith("Level_"))
        {
            Debug.LogWarning($"EndlessDescent: RemoveCompletedLevelAfterTransition: object '{completedLevelRoot.name}' does not look like a level root. Skipping.");
            return;
        }

        if (levelRoots.Contains(completedLevelRoot))
        {
            levelRoots.Remove(completedLevelRoot);
        }

        Debug.Log($"EndlessDescent: removed completed level {completedLevelRoot.name}");
        if (Application.isPlaying) Destroy(completedLevelRoot);
        else DestroyImmediate(completedLevelRoot);
    }

    private void LogSceneCounts(string context)
    {
        int levels = UnityEngine.Object.FindObjectsByType<LevelInstanceRoot>().Length;
        int levelRootsInList = levelRoots.Count;
        int generatedRoot = GameObject.Find("GeneratedLevel") != null ? 1 : 0;
        int centipedes = UnityEngine.Object.FindObjectsByType<CentipedeController>().Length;
        int spawners = UnityEngine.Object.FindObjectsByType<CentipedeSpawner>().Length;
        int triggers = UnityEngine.Object.FindObjectsByType<DescentSphereTrigger>().Length;
        int colliders = UnityEngine.Object.FindObjectsByType<Collider>().Length;
        Debug.Log($"EndlessDescent SceneCounts {context}: levelsInList={levelRootsInList}, LevelInstanceRoot={levels}, GeneratedRootPresent={generatedRoot}, CentipedeControllers={centipedes}, CentipedeSpawners={spawners}, DescentSphereTriggers={triggers}, CollidersTotal={colliders}");
    }

    private GameObject FindFinalArenaRoot(GameObject levelRoot)
    {
        if (levelRoot == null) return null;

        // prefer an object that contains DescentSphereTrigger
        var triggers = levelRoot.GetComponentsInChildren<DescentSphereTrigger>(true);
        if (triggers != null && triggers.Length > 0)
        {
            var t = triggers[0];
            Transform cur = t.transform;
            while (cur.parent != null && cur.parent.gameObject != levelRoot)
            {
                cur = cur.parent;
            }
            return cur.gameObject;
        }

        // fallback: search by name hints
        string[] hints = new[] { "Final", "Pedestal", "Victory", "Altar", "Column" };
        var all = levelRoot.GetComponentsInChildren<Transform>(true);
        foreach (var tr in all)
        {
            if (tr == null || tr.gameObject == levelRoot) continue;
            foreach (var h in hints)
            {
                if (tr.name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Transform cur = tr;
                    while (cur.parent != null && cur.parent.gameObject != levelRoot)
                    {
                        cur = cur.parent;
                    }
                    return cur.gameObject;
                }
            }
        }

        return null;
    }

    private void DeleteUpperHalfOfCompletedLevel(LevelInstanceRoot levelRootComp, Bounds levelBounds)
    {
        if (levelRootComp == null) return;
        var root = levelRootComp.gameObject;
        float cutoffY = Mathf.Lerp(levelBounds.min.y, levelBounds.max.y, completedLevelUpperHalfDeleteRatio);
        Debug.Log($"EndlessDescent: deleting upper half of '{root.name}' above Y={cutoffY:F2}");

        List<Transform> children = new List<Transform>();
        foreach (Transform t in root.transform) children.Add(t);

        foreach (var child in children)
        {
            if (child == null) continue;

            // preserve anything that looks like final arena or contains the descent sphere
            if (child.GetComponentInChildren<DescentSphereTrigger>(true) != null) continue;
            if (child.name.IndexOf("Pedestal", StringComparison.OrdinalIgnoreCase) >= 0) continue;

            // preserve if child contains player
            if (playerTransform != null && child.GetComponentInChildren<Transform>(true) != null)
            {
                if (TryCalculateLevelBounds(child.gameObject, out Bounds cb))
                {
                    if (cb.Contains(playerTransform.position))
                    {
                        Debug.Log($"EndlessDescent: preserving child '{child.name}' because it contains player.");
                        continue;
                    }
                }
            }

            if (!TryCalculateLevelBounds(child.gameObject, out Bounds childBounds)) continue;
            if (childBounds.min.y >= cutoffY)
            {
                Debug.Log($"EndlessDescent: deleting child '{child.name}' (minY={childBounds.min.y:F2} >= cutoffY={cutoffY:F2})");
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }
    }

    private bool TryFindFirstLandingSurfaceBelowPlayer(GameObject nextLevelRoot, Vector3 playerPosition, out Vector3 landingPoint, out Collider landingCollider, out float dropDistance)
    {
        landingPoint = Vector3.zero; landingCollider = null; dropDistance = 0f;
        if (nextLevelRoot == null) return false;

        // straight-down ray
        RaycastHit hit;
        Vector3 origin = new Vector3(playerPosition.x, playerPosition.y - 0.1f, playerPosition.z);
        if (Physics.Raycast(origin, Vector3.down, out hit, firstLandingMaxDrop, firstLandingLayerMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && hit.collider.transform.IsChildOf(nextLevelRoot.transform))
            {
                landingPoint = hit.point; landingCollider = hit.collider; dropDistance = playerPosition.y - hit.point.y; return true;
            }
        }

        // spiral / radial search
        int ringSteps = 8;
        int rings = Mathf.Max(1, Mathf.CeilToInt(firstLandingSearchRadius));
        for (int r = 1; r <= rings; r++)
        {
            float radius = (firstLandingSearchRadius / rings) * r;
            for (int s = 0; s < ringSteps; s++)
            {
                float ang = (s / (float)ringSteps) * Mathf.PI * 2f;
                Vector3 sampleXZ = new Vector3(playerPosition.x + Mathf.Cos(ang) * radius, playerPosition.y, playerPosition.z + Mathf.Sin(ang) * radius);
                Vector3 sOrigin = new Vector3(sampleXZ.x, playerPosition.y + 1f, sampleXZ.z);
                if (Physics.Raycast(sOrigin, Vector3.down, out hit, firstLandingMaxDrop + 2f, firstLandingLayerMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && hit.collider.transform.IsChildOf(nextLevelRoot.transform))
                    {
                        landingPoint = hit.point; landingCollider = hit.collider; dropDistance = playerPosition.y - hit.point.y;
                        if (dropDistance <= firstLandingMaxDrop) return true;
                    }
                }
            }
        }

        return false;
    }

    private GameObject CreateEmergencyLandingPlatform(GameObject nextLevelRoot, Vector3 playerPosition)
    {
        if (nextLevelRoot == null) return null;
        LevelInstanceRoot lr = nextLevelRoot.GetComponent<LevelInstanceRoot>();
        Bounds b = lr != null ? lr.LevelBounds : new Bounds(nextLevelRoot.transform.position, Vector3.one * levelHeight);

        float targetY = b.max.y - firstPlatformTargetDrop;
        // clamp drop to allowed range
        float drop = Mathf.Clamp(playerPosition.y - targetY, 0f, maxFirstPlatformDrop);
        float platformY = playerPosition.y - drop;

        GameObject plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plat.name = "EmergencyLandingPlatform";
        plat.transform.SetParent(nextLevelRoot.transform, true);
        plat.transform.position = new Vector3(playerPosition.x, platformY, playerPosition.z);
        plat.transform.localScale = new Vector3(3f, 0.25f, 3f);
        var col = plat.GetComponent<Collider>(); if (col != null) col.isTrigger = false;
        // optional: make platform kinematic rigidbody for physics stability
        var rb = plat.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;

        return plat;
    }

    private void RemoveCompletedFinalArena(LevelInstanceRoot levelRootComp)
    {
        if (levelRootComp == null) return;
        var root = levelRootComp.gameObject;
        var final = FindFinalArenaRoot(root);
        if (final == null)
        {
            Debug.LogWarning($"EndlessDescent: RemoveCompletedFinalArena: no final arena found in '{root.name}'");
            return;
        }

        Debug.Log($"EndlessDescent: destroying final arena '{final.name}' in '{root.name}'");
        if (Application.isPlaying) Destroy(final);
        else DestroyImmediate(final);
    }

    // Overload: remove final arena using the completed level root and the source sphere that triggered completion.
    private void RemoveCompletedFinalArena(GameObject completedLevelRoot, DescentSphereTrigger source)
    {
        if (completedLevelRoot == null)
        {
            Debug.LogWarning("EndlessDescent: RemoveCompletedFinalArena called with null completedLevelRoot");
            return;
        }

        // First try: walk up from the source sphere and look for a parent that matches typical final arena naming
        if (source != null)
        {
            Transform t = source.transform;
            while (t != null && t != completedLevelRoot.transform)
            {
                if (t.name.IndexOf("Final Pedestal Arena", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.name.IndexOf("Final Pedestal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.name.IndexOf("FinalArena", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log($"EndlessDescent: destroying final arena parent '{t.name}' found from source");
                    if (Application.isPlaying) Destroy(t.gameObject); else DestroyImmediate(t.gameObject);
                    return;
                }
                t = t.parent;
            }
        }

        // Second try: use existing helper that heuristically finds final arena root
        var final = FindFinalArenaRoot(completedLevelRoot);
        if (final != null)
        {
            Debug.Log($"EndlessDescent: destroying final arena '{final.name}' from heuristic search");
            if (Application.isPlaying) Destroy(final); else DestroyImmediate(final);
            return;
        }

        // Last resort: search children for names and delete top-most parent matching hints
        string[] hints = new[] { "Pedestal", "Victory", "Altar", "Final" };
        var all = completedLevelRoot.GetComponentsInChildren<Transform>(true);
        foreach (var tr in all)
        {
            if (tr == null || tr.gameObject == completedLevelRoot) continue;
            foreach (var h in hints)
            {
                if (tr.name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Transform cur = tr;
                    while (cur.parent != null && cur.parent.gameObject != completedLevelRoot)
                    {
                        cur = cur.parent;
                    }
                    Debug.Log($"EndlessDescent: destroying fallback arena root '{cur.name}'");
                    if (Application.isPlaying) Destroy(cur.gameObject); else DestroyImmediate(cur.gameObject);
                    return;
                }
            }
        }

        // final fallback: destroy the source sphere itself
        if (source != null && source.gameObject != null)
        {
            Debug.Log($"EndlessDescent: destroying source sphere '{source.gameObject.name}' as last resort");
            if (Application.isPlaying) Destroy(source.gameObject); else DestroyImmediate(source.gameObject);
        }
    }

    // Helper: disable renderers/colliders immediately and destroy object (or DestroyImmediate in editor)
    private static void DisableAndDestroy(GameObject go)
    {
        if (go == null) return;
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            try { if (c != null) c.enabled = false; } catch { }
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            try { if (r != null) r.enabled = false; } catch { }
        }
        try { go.SetActive(false); } catch { }
        if (Application.isPlaying) UnityEngine.Object.Destroy(go);
        else UnityEngine.Object.DestroyImmediate(go);
    }

    private bool IsChildOf(Transform child, Transform parent)
    {
        if (child == null || parent == null) return false;
        while (child != null)
        {
            if (child == parent) return true;
            child = child.parent;
        }
        return false;
    }

    private string GetTransformPath(Transform t)
    {
        if (t == null) return "<null>";
        var parts = new List<string>();
        Transform cur = t;
        while (cur != null)
        {
            parts.Add(cur.name);
            cur = cur.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private int RemoveTransitionRemovalObjects(GameObject completedLevelRoot)
    {
        int removed = 0;
        if (completedLevelRoot == null) return removed;
        var markers = completedLevelRoot.GetComponentsInChildren<LevelTransitionRemovalObject>(true);
        if (markers == null || markers.Length == 0)
        {
            Debug.Log($"EndlessDescent: no transition removal markers found in '{completedLevelRoot.name}'");
            return removed;
        }
        foreach (var marker in markers)
        {
            if (marker == null) continue;
            if (marker.Phase != LevelTransitionRemovalObject.RemovalPhase.AfterNextLevelReady) continue;
            Debug.Log($"EndlessDescent: removing transition object '{marker.name}' path={GetTransformPath(marker.transform)}");
            DisableAndDestroy(marker.gameObject);
            removed++;
        }
        Debug.Log($"EndlessDescent: removed {removed} transition removal object(s) from '{completedLevelRoot.name}'");
        return removed;
    }

    private int RemoveCompletedBottomFloors(GameObject completedLevelRoot, DescentSphereTrigger source)
    {
        if (completedLevelRoot == null) return 0;
        float sourceY = source != null ? source.transform.position.y : float.NegativeInfinity;
        int removed = 0;

        Debug.Log($"EndlessDescent: searching bottom floors in completed level {completedLevelRoot.name}");

        var transforms = completedLevelRoot.GetComponentsInChildren<Transform>(true);
        foreach (var tr in transforms)
        {
            if (tr == null || tr.gameObject == completedLevelRoot) continue;

            string name = tr.name ?? string.Empty;
            bool looksLikeBottomFloor =
                name.IndexOf("Shaft Bottom Floor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Bottom Floor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("ShaftBottom", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Bottom", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!looksLikeBottomFloor) continue;

            // Ensure this candidate is a child of the completed level root
            if (!IsChildOf(tr, completedLevelRoot.transform))
            {
                Debug.LogWarning($"EndlessDescent: candidate '{tr.name}' is not a child of '{completedLevelRoot.name}' (path={GetTransformPath(tr)}). Skipping.");
                continue;
            }

            // For old bottom floor it should be at or below the source sphere Y (within some leeway)
            if (source != null && tr.position.y > sourceY + 10f) continue;

            bool hasCollider = false;
            bool hasRenderer = false;
            float sizeMetric = 0f;
            foreach (var c in tr.GetComponentsInChildren<Collider>(true))
            {
                if (c == null) continue;
                hasCollider = true;
                try { sizeMetric = Mathf.Max(sizeMetric, c.bounds.size.magnitude); } catch { }
            }
            foreach (var r in tr.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                hasRenderer = true;
                try { sizeMetric = Mathf.Max(sizeMetric, r.bounds.size.magnitude); } catch { }
            }

            Debug.Log($"EndlessDescent: bottom floor candidate name={tr.name}, path={GetTransformPath(tr)}, y={tr.position.y}, hasCollider={hasCollider}, hasRenderer={hasRenderer}");

            // Heuristics to avoid deleting small incidental objects
            if (!hasCollider && !hasRenderer) continue;
            if (sizeMetric < 0.5f) continue;

            // Immediately disable physics/visuals to avoid blocking the player
            foreach (var c in tr.GetComponentsInChildren<Collider>(true)) { try { if (c != null) c.enabled = false; } catch { } }
            foreach (var r in tr.GetComponentsInChildren<Renderer>(true)) { try { if (r != null) r.enabled = false; } catch { } }
            try { tr.gameObject.SetActive(false); } catch { }

            if (Application.isPlaying) UnityEngine.Object.Destroy(tr.gameObject); else UnityEngine.Object.DestroyImmediate(tr.gameObject);
            Debug.Log($"EndlessDescent: removed completed Shaft Bottom Floor '{tr.name}' from {completedLevelRoot.name}");
            removed++;
        }

        if (removed == 0) Debug.LogWarning("EndlessDescent: no Shaft Bottom Floor found under completed level. Check parenting: floor may not be child of Level_X.");
        Debug.Log($"EndlessDescent: removed {removed} completed bottom floor object(s).");
        return removed;
    }

    private void CleanupLevelsOlderThanPrevious(int currentNextIndex)
    {
        int threshold = currentNextIndex - 1;
        List<GameObject> toRemove = new List<GameObject>();
        foreach (var r in levelRoots)
        {
            if (r == null) continue;
            var lr = r.GetComponent<LevelInstanceRoot>();
            if (lr == null) continue;
            if (lr.LevelIndex < threshold)
            {
                toRemove.Add(r);
            }
        }
        foreach (var r in toRemove)
        {
            if (levelRoots.Contains(r)) levelRoots.Remove(r);
            Debug.Log($"EndlessDescent: CleanupLevelsOlderThanPrevious deleting '{r.name}' (index < {threshold})");
            if (Application.isPlaying) Destroy(r);
            else DestroyImmediate(r);
        }
    }

    private Vector3 GetLevelExitPosition(DescentSphereTrigger source, GameObject completedLevelRoot)
    {
        if (source != null) return source.transform.position;
        if (completedLevelRoot != null)
        {
            var anchors = completedLevelRoot.GetComponent<LevelTransitionAnchors>();
            if (anchors != null && anchors.ExitAnchor != null) return anchors.ExitAnchor.position;
            var trig = completedLevelRoot.GetComponentInChildren<DescentSphereTrigger>(true);
            if (trig != null) return trig.transform.position;
            var final = FindFinalArenaRoot(completedLevelRoot);
            if (final != null) return final.transform.position;
            return completedLevelRoot.transform.position;
        }
        if (playerTransform != null) return playerTransform.position;
        return Vector3.zero;
    }

    private Transform EnsureLevelEntryAnchor(GameObject levelRoot)
    {
        if (levelRoot == null) return null;
        var anchors = levelRoot.GetComponent<LevelTransitionAnchors>();
        if (anchors == null) anchors = levelRoot.AddComponent<LevelTransitionAnchors>();
        if (anchors.EntryAnchor != null) return anchors.EntryAnchor;

        // try to find named child
        foreach (var t in levelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (t.name.Equals("Level Entry Anchor", StringComparison.OrdinalIgnoreCase))
            {
                anchors.EntryAnchor = t;
                var lr = levelRoot.GetComponent<LevelInstanceRoot>();
                if (lr != null && lr.EntryPoint == null) lr.EntryPoint = t;
                return t;
            }
        }

        // heuristic: choose top-most non-boundary renderer/collider as anchor
        float bestY = float.NegativeInfinity;
        Vector3 center = levelRoot.transform.position;
        var rends = levelRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends)
        {
            if (r == null) continue;
            string n = r.gameObject.name;
            if (n.IndexOf("Boundary", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (n.IndexOf("Shaft", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (n.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (r.bounds.max.y > bestY)
            {
                bestY = r.bounds.max.y;
                center = r.bounds.center;
            }
        }

        if (bestY == float.NegativeInfinity)
        {
            // fallback to LevelInstanceRoot bounds or root position
            var lr = levelRoot.GetComponent<LevelInstanceRoot>();
            if (lr != null && lr.LevelBounds.size != Vector3.zero)
            {
                bestY = lr.LevelBounds.max.y;
                center = lr.LevelBounds.center;
            }
            else
            {
                bestY = levelRoot.transform.position.y + Mathf.Max(10f, levelHeight * 0.6f);
            }
        }

        Vector3 entryPos = new Vector3(center.x, bestY - entryPointDownOffset, center.z);
        GameObject ep = new GameObject("Level Entry Anchor");
        ep.transform.SetParent(levelRoot.transform, true);
        ep.transform.position = entryPos;
        anchors.EntryAnchor = ep.transform;
        var lr2 = levelRoot.GetComponent<LevelInstanceRoot>();
        if (lr2 != null) lr2.EntryPoint = ep.transform;
        Debug.Log($"EndlessDescent: created fallback EntryAnchor at {entryPos} for '{levelRoot.name}'");
        return ep.transform;
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

#pragma warning restore 414
