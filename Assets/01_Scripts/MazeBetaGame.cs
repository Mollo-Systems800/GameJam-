using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class MazeBetaGame : MonoBehaviour
{
    [Header("Laberinto")]
    [Range(3, 40)] public int width = 12;
    [Range(3, 40)] public int height = 12;
    [Min(2)] public float cellSize = 3f;
    [Min(0.35f)] public float wallThickness = 0.75f;
    [Min(2)] public float wallHeight = 2.6f;
    public float WallThickness => Mathf.Clamp(wallThickness, 0.35f, Mathf.Max(0.35f, cellSize - 0.9f));
    [HideInInspector] public int exitCount = 1;
    [Range(0, 0.6f)] public float alternativeRoutes = 0.25f;
    public bool randomSeed = true;
    public int seed = 12345;
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    [Header("Piezas generadas del escenario")]
    public GameObject wallCorePrefab;
    public GameObject wallCapPrefab;
    public GameObject exitFloorPrefab;
    public GameObject exitMarkerPrefab;
    public GameObject pitVisualPrefab;
    public TextMesh worldLabelPrefab;
    public Camera cameraPrefab;
    [Header("Jugador y recursos visuales opcionales")]
    public GameObject playerPrefab;
    public GameObject bossVisualPrefab;
    public GameObject pillVisualPrefab;
    public GameObject bossPrefab;
    public GameObject pillPrefab;
    [Header("Plataformas y salto")]
    public GameObject platformPrefab;
    public GameObject jumpTakeoffPrefab;
    public GameObject jumpLandingPrefab;
    [Range(0, 30)] public int obstacleCount = 10;
    public float jumpHeight = 1.1f;
    public float fallDeathHeight = -3f;
    public float playerSpeed = 4f;
    public float slowdownEvery = 60f;
    public float slowdownAmount = 0.35f;
    [Header("Boss")]
    public float bossSpeed = 2.2f;
    public float bossIncrease = 0.1f;
    public float bossIncreaseEvery = 30f;
    public float bossGracePeriod = 5f;
    [Header("Partida")]
    [Range(0, 100)] public int pillCount = 12;
    [Header("Camara detras del jugador")]
    public float cameraTargetHeight = 1.3f;
    public float cameraDistance = 2.6f;
    public float mouseSensitivity = 0.10f;
    public float cameraFieldOfView = 65;
    public MazeGrid Grid { get; private set; }
    public Player Player { get; private set; }
    public MazeBoss Boss { get; private set; }
    public bool IsPlaying => running && !paused && !ShopOpen && !IsCinematicPlaying;
    public bool HasWon { get; private set; }
    public float Elapsed => Progress.Elapsed;
    public int ActiveSeed { get; private set; }
    public IReadOnlyList<int> ObstacleCells => obstacleCells;
    public string DefeatReason { get; private set; }
    public bool DefeatedByBoss { get; private set; }
    public MazeAudioSettingsPanel AudioSettings { get; set; }

    Transform generated;
    Camera view;
    PlayerFollowCamera followCamera;
    List<int> obstacleCells = new List<int>();
    bool running;
    bool paused;
    readonly List<TextMesh> labels = new List<TextMesh>();
    readonly List<Material> materials = new List<Material>();
    Material wallMaterial, floorMaterial, paperMaterial, pillMaterial, exitMaterial, platformMaterial;

    void Start()
    {
        view = Camera.main;
        if (!view)
        {
            if (cameraPrefab) view = Instantiate(cameraPrefab);
            else view = new GameObject("Beta Camera", typeof(Camera)).GetComponent<Camera>();
            view.gameObject.tag = "MainCamera";
            if (!view.GetComponent<AudioListener>()) view.gameObject.AddComponent<AudioListener>();
        }
        view.nearClipPlane = 0.04f;
        view.farClipPlane = 150;
        view.fieldOfView = cameraFieldOfView;
        followCamera = view.GetComponent<PlayerFollowCamera>();
        if (!followCamera) followCamera = view.gameObject.AddComponent<PlayerFollowCamera>();
        wallMaterial = MakeMaterial(new Color(0.25f, 0.3f, 0.35f));
        floorMaterial = MakeMaterial(new Color(0.35f, 0.38f, 0.4f));
        paperMaterial = MakeMaterial(new Color(1f, 0.86f, 0.7f));
        pillMaterial = MakeMaterial(new Color(0.65f, 0.9f, 1f));
        exitMaterial = MakeMaterial(new Color(0.15f, 0.85f, 0.45f));
        platformMaterial = MakeMaterial(new Color(1f, 0.65f, 0.1f));
        InitializeRunFeatures();
        if (MazeCampaignSave.ConsumeLaunch(out var launch, out bool newCampaign))
        {
            checkpointEnabled = true;
            StartCampaign(launch.CampaignSeed, launch.Level, launch.MazeSeed, launch.Seconds, launch.CompletedLevels, launch.RankingEligible);
            if (newCampaign) PlayNewGameIntro();
        }
        else
        {
            checkpointEnabled = !Application.isEditor;
            StartCampaign(randomSeed ? NewSeed() : seed, startLevel);
        }
    }

    static int NewSeed() => Guid.NewGuid().GetHashCode();

    public void Begin(int newSeed)
    {
        if (intro && intro.IsActive) intro.Cancel();
        if (newSeed != ActiveSeed) LossesOnCurrentMaze = 0;
        RetrySecondsRemaining = 0;
        WaitingForRetry = false;
        if (generated)
        {
            generated.gameObject.SetActive(false);
            Destroy(generated.gameObject);
        }
        labels.Clear();
        ActiveSeed = newSeed;
        exitCount = 1;
        int mazeWidth = Mathf.Clamp(width, 3, 40), mazeHeight = Mathf.Clamp(height, 3, 40);
        int minimumExit = Mathf.CeilToInt((mazeWidth + mazeHeight - 2) * Mathf.Clamp(minimumExitDistanceRatio, 0.5f, 1));
        Grid = new MazeGrid(mazeWidth, mazeHeight, newSeed, 1, alternativeRoutes, minimumExit, parkourEnabled ? parkourCellLength : 0);
        ApplyAreaTheme();
        residentCells = MazeSeatedResidents.Select(Grid, newSeed, seatedWomanCount, null);
        residentZones.Clear();
        obstacleCells = MazeObstacles.Select(Grid, newSeed, obstacleCount, residentCells);
        spikeHazards.Clear();
        parkourPlatforms.Clear();
        foreach (int cell in Grid.ParkourCells)
            if (Grid.IsParkourPit(cell)) obstacleCells.Add(cell);
        generated = new GameObject("Beta Generated Maze").transform;
        generated.SetParent(transform, false);
        if (wallTopJunctions == null) wallTopJunctions = new HashSet<Vector2Int>();
        else wallTopJunctions.Clear();
        Progress = new RunProgress();
        ShopOpen = false;
        pausedBeforeShop = false;
        paused = false;
        HasWon = false;
        DefeatReason = "SIN VIDA";
        DefeatedByBoss = false;
        running = true;

        for (int cell = 0; cell < Grid.Count; cell++)
        {
            Vector3 position = CellPosition(cell);
            if (!Grid.IsParkourPit(cell))
            {
                if (obstacleCells.Contains(cell)) BuildJumpSection(cell);
                else Module(EnvFloor, "Floor " + cell, position, new Vector3(cellSize, 0.2f, cellSize), false);
            }
            for (int d = 0; d < 4; d++)
            {
                if (Grid.IsOpen(cell, d)) continue;
                int neighbor = Grid.Neighbor(cell, d);
                if (neighbor >= 0 && neighbor < cell) continue;
                Vector3 direction = Direction(d);
                var wall = Module(EnvWall, "Wall " + cell + " " + d,
                    position + direction * cellSize * 0.5f,
                    new Vector3(cellSize + WallThickness, Mathf.Max(2, wallHeight), WallThickness), true);
                wall.rotation = Quaternion.Euler(0, d % 2 == 0 ? 0 : 90, 0);
                BuildWallCoping(wall);
            }
        }

        BuildParkourZone();

        foreach (var exit in Grid.Exits)
        {
            Vector3 exitPosition = CellPosition(exit.Cell) + Direction(exit.Direction) * cellSize;
            Module(EnvExitFloor, "Exit platform " + exit.Cell, exitPosition, new Vector3(cellSize, 0.2f, cellSize), false);
            var marker = Cube("Exit marker", generated, exitMaterial, EnvExitMarker);
            marker.transform.position = exitPosition + Vector3.up * 0.025f;
            marker.transform.localScale = new Vector3(cellSize * 0.8f, 0.05f, cellSize * 0.8f);
        }

        if (!playerPrefab || !playerPrefab.GetComponent<Player>())
            throw new InvalidOperationException("Asigna Assets/03_Prefabs/Player.prefab en Player Prefab.");
        var playerObject = Instantiate(playerPrefab, CellPosition(0) + Vector3.up * 0.1f, Quaternion.identity, generated);
        playerObject.name = "Player";
        Player = playerObject.GetComponent<Player>();
        Player.Game = this;
        Player.baseSpeed = playerSpeed;
        Player.minimumSpeed = Mathf.Min(2.6f, playerSpeed);
        Player.slowdownEvery = slowdownEvery;
        Player.slowdownAmount = slowdownAmount;
        Player.jumpHeight = jumpHeight;
        // Awake runs during Instantiate; apply scene tuning afterwards.
        Player.InitializeSpeed();
        Player.view = view;

        int[] distances = Grid.DistancesFrom(0);
        int bossCell = 1;
        int desiredDistance = Mathf.Max(6, distances[Grid.ExitCell] / 3);
        int bestDifference = int.MaxValue;
        for (int i = 1; i < Grid.Count; i++)
        {
            if (Grid.IsExitCell(i) || obstacleCells.Contains(i) || Grid.IsParkourCell(i) || residentCells.Contains(i)) continue;
            int difference = Mathf.Abs(distances[i] - desiredDistance);
            if (difference < bestDifference) { bossCell = i; bestDifference = difference; }
        }
        var bossObject = Spawn(bossPrefab, "Boss - Se busca empleo", CellPosition(bossCell));
        Boss = bossObject.GetComponent<MazeBoss>();
        if (!Boss) Boss = bossObject.AddComponent<MazeBoss>();
        Boss.startingSpeed = bossSpeed;
        Boss.speedIncrease = bossIncrease;
        Boss.increaseEvery = bossIncreaseEvery;
        Boss.gracePeriod = bossGracePeriod;
        Boss.Initialize(this, bossCell);
        bool hasBossVisual = bossObject.GetComponentsInChildren<Renderer>().Length > 0;
        if (!hasBossVisual && bossVisualPrefab)
        {
            float playerHeight = Player.GetComponent<CharacterController>().height;
            FitVisual(bossVisualPrefab, bossObject.transform, new Vector3(playerHeight * .8f, playerHeight, playerHeight * .1f), true);
        }
        else if (!hasBossVisual)
        {
            var paper = Cube("Employment sheet", bossObject.transform, paperMaterial);
            paper.transform.localPosition = Vector3.up;
            paper.transform.localScale = new Vector3(1.2f, 1.8f, 0.12f);
        }

        var candidates = new List<int>();
        for (int i = 1; i < Grid.Count; i++)
            if (i != bossCell && !Grid.IsExitCell(i) && !obstacleCells.Contains(i) && !Grid.IsParkourCell(i) && !residentCells.Contains(i)) candidates.Add(i);
        var random = new System.Random(newSeed ^ 7919);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int temp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = temp;
        }
        for (int i = 0; i < Mathf.Min(pillCount, candidates.Count); i++)
        {
            int cell = candidates[i];
            var pickup = Spawn(pillPrefab, "Antidepresivos " + cell, CellPosition(cell) + Vector3.up * 0.65f);
            bool hasPillVisual = pickup.GetComponentsInChildren<Renderer>().Length > 0;
            if (!hasPillVisual && pillVisualPrefab) FitVisual(pillVisualPrefab, pickup.transform, new Vector3(0.75f, 0.46f, 0.18f), true);
            else if (!hasPillVisual)
            {
                var box = Cube("Pill box", pickup.transform, pillMaterial);
                box.transform.localScale = new Vector3(0.65f, 0.4f, 0.35f);
            }
            var pill = pickup.GetComponent<PillPickup>();
            if (!pill) pill = pickup.AddComponent<PillPickup>();
            pill.Initialize(this, cell);
        }
        SpawnEncounters(bossCell);
        SpawnSuppliedProps(bossCell);
        Physics.SyncTransforms();
        followCamera.targetHeight = cameraTargetHeight;
        followCamera.followDistance = cameraDistance;
        followCamera.mouseSensitivity = mouseSensitivity;
        followCamera.fieldOfView = cameraFieldOfView;
        followCamera.Initialize(this, Player);
        BuildNightLighting();
        SaveCampaignCheckpoint();
        MazeAudioDirector.LevelStarted(this);
    }

    public Vector3 CellPosition(int cell) => new Vector3(cell % Grid.Width * cellSize, 0, cell / Grid.Width * cellSize);
    static Vector3 Direction(int d) => new Vector3(MazeGrid.DX[d], 0, MazeGrid.DY[d]);
    public int WorldCell(Vector3 position)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt(position.x / cellSize + 0.5f), 0, Grid.Width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(position.z / cellSize + 0.5f), 0, Grid.Height - 1);
        return y * Grid.Width + x;
    }

    void Update()
    {
        if (IsCinematicPlaying) return;
        if(AudioSettings&&AudioSettings.IsOpen)
        {
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)AudioSettings.Close();
            return;
        }
        TickDefeatRestart();
        var keyboard = Keyboard.current;
        if (running && ((keyboard != null && keyboard.bKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame))) SetShopOpen(!ShopOpen);
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && running)
        {
            if (ShopOpen) SetShopOpen(false);
            else { paused = !paused; RefreshActivity(); }
        }
        if (!IsPlaying) return;
        Progress.Advance(Time.deltaTime);
        CampaignClock.Advance(Time.deltaTime);
        Player.SpeedMultiplier = Progress.SpeedMultiplier;
        if (CheckSpikeContact()) return;
        if (Player.transform.position.y < fallDeathHeight)
        {
            DefeatReason = "CAISTE AL PRECIPICIO";
            Finish(false);
            return;
        }
        foreach (var exit in Grid.Exits)
        {
            Vector3 exitDirection = Direction(exit.Direction);
            Vector3 fromDoor = Player.transform.position - (CellPosition(exit.Cell) + exitDirection * cellSize * 0.5f);
            float outside = Vector3.Dot(fromDoor, exitDirection);
            float lateral = Mathf.Abs(Vector3.Dot(fromDoor, Vector3.Cross(Vector3.up, exitDirection)));
            if (WorldCell(Player.transform.position) == exit.Cell && outside > 0.4f && lateral < cellSize * 0.45f
                && Player.transform.position.y > -0.2f && Player.transform.position.y < jumpHeight + 0.6f)
            { Finish(true); break; }
        }
    }

    void LateUpdate()
    {
        if (!Player || !view) return;
        foreach (var label in labels)
            if (label) label.transform.rotation = view.transform.rotation;
    }

    public void Finish(bool won)
    {
        if (!IsPlaying) return;
        running = false;
        HasWon = won;
        Player.CanMove = false;
        Boss.IsActive = false;
        if (won)
        {
            FinishCampaignLevel();
            RecordWin();
            if (checkpointEnabled && recordResults)
            {
                if (CampaignComplete) MazeCampaignSave.Complete();
                else SaveCampaignCheckpoint();
            }
        }
        else { ScheduleDefeatRestart(); SaveCampaignCheckpoint(); }
        MazeAudioDirector.Play(won ? (CampaignComplete?MazeSound.Victory:CurrentLevelInArea==2?MazeSound.AreaWin:MazeSound.LevelWin)
            : DefeatedByBoss?MazeSound.DefeatBoss:MazeSound.DefeatHealth);
        PlayOutcomeCinematic();
    }

    Transform Module(GameObject prefab, string objectName, Vector3 position, Vector3 size, bool wall)
    {
        var root = new GameObject(objectName).transform;
        root.SetParent(generated);
        root.position = position;
        if (prefab)
        {
            var visual = FitVisual(prefab, root, size, false);
            if (!wall) visual.transform.localPosition -= Vector3.up * size.y;
        }
        else
        {
            var cube = Cube("Visual", root, wall ? wallMaterial : floorMaterial);
            cube.transform.localScale = size;
            cube.transform.localPosition = Vector3.up * (wall ? size.y * 0.5f : -size.y * 0.5f);
        }
        if (wall)
        {
            // The kit's relief is thin between its horizontal strips. Give it a
            // closed solid body and a full-width top so thickness reads from every side.
            if (prefab)
            {
                var core = Cube("Wall solid core", root, wallMaterial, EnvCore);
                core.transform.localPosition = Vector3.up * size.y * 0.5f;
                core.transform.localScale = new Vector3(size.x, size.y, size.z * 0.8f);
            }
        }
        // Stable collision independent of the imported mesh pivot and zero-thickness kit floors.
        var collider = root.gameObject.AddComponent<BoxCollider>();
        collider.size = size;
        collider.center = Vector3.up * (wall ? size.y * 0.5f : -size.y * 0.5f);
        return root;
    }

    void BuildJumpSection(int cell)
    {
        Vector3 centre = CellPosition(cell);
        bool vertical = Grid.IsOpen(cell, 0);
        // A real edge-to-edge gap, too wide for the controller to step over.
        float gap = Mathf.Min(EffectiveParkourGap, cellSize * 0.65f);
        float lip = (cellSize - gap) * 0.5f;
        Vector3 axis = vertical ? Vector3.forward : Vector3.right;
        Vector3 lipSize = vertical ? new Vector3(cellSize, 0.2f, lip) : new Vector3(lip, 0.2f, cellSize);
        ParkourModule(EnvTakeoff, "Jump takeoff " + cell, centre - axis * (cellSize - lip) * 0.5f, lipSize);
        ParkourModule(EnvLanding, "Jump landing " + cell, centre + axis * (cellSize - lip) * 0.5f, lipSize);
        BuildSpikeBed(centre, vertical, cellSize, "Jump spikes " + cell);
    }

    public static GameObject FitVisual(GameObject prefab, Transform root, Vector3 size, bool uniform)
    {
        var instance = Instantiate(prefab, root);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        foreach (var collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
        foreach (var body in instance.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
        var renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return instance;
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        Vector3 scale = new Vector3(size.x / Mathf.Max(0.001f, bounds.size.x),
            bounds.size.y < 0.01f ? 1 : size.y / bounds.size.y,
            size.z / Mathf.Max(0.001f, bounds.size.z));
        if (uniform) scale = Vector3.one * Mathf.Min(scale.x, scale.y, scale.z);
        instance.transform.localScale = scale;
        Vector3 offset = bounds.center - root.position;
        instance.transform.localPosition = new Vector3(-offset.x * scale.x,
            -(bounds.min.y - root.position.y) * scale.y, -offset.z * scale.z);
        return instance;
    }

    static GameObject Cube(string objectName, Transform parent, Material material, GameObject prefab = null)
    {
        var cube = prefab ? new GameObject(objectName) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent, false);
        if (prefab) FitVisual(prefab, cube.transform, Vector3.one, false);
        else
        {
            cube.GetComponent<Collider>().enabled = false;
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }
        return cube;
    }

    void Label(string text, Vector3 position, Transform parent, Color color, float size)
    {
        var item = EnvLabel ? Instantiate(EnvLabel) : new GameObject(text).AddComponent<TextMesh>();
        item.name = text;
        item.transform.SetParent(parent);
        item.transform.position = position;
        item.text = text;
        if (!EnvLabel)
        {
            item.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            item.GetComponent<MeshRenderer>().sharedMaterial = item.font.material;
            item.fontSize = 48;
            item.characterSize = size;
            item.anchor = TextAnchor.MiddleCenter;
            item.alignment = TextAlignment.Center;
            item.color = color;
        }
        labels.Add(item);
    }

    Material MakeMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        var material = new Material(shader) { color = color };
        materials.Add(material);
        return material;
    }

    void OnDestroy()
    {
        if (followCamera) followCamera.enabled = false;
        foreach (var material in materials) if (material) Destroy(material);
        if (spikeMesh) Destroy(spikeMesh);
    }

}
