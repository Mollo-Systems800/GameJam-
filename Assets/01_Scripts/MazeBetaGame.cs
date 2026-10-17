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
    [Range(1, 8)] public int exitCount = 4;
    [Range(0, 0.6f)] public float alternativeRoutes = 0.25f;
    public bool randomSeed = true;
    public int seed = 12345;
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    [Header("Jugador y recursos visuales opcionales")]
    public GameObject playerPrefab;
    public GameObject bossVisualPrefab;
    public GameObject pillVisualPrefab;
    [Header("Plataformas y salto")]
    public GameObject platformPrefab;
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
    public bool IsPlaying => running && !paused && !ShopOpen;
    public bool HasWon { get; private set; }
    public float Elapsed => Progress.Elapsed;
    public int ActiveSeed { get; private set; }
    public IReadOnlyList<int> ObstacleCells => obstacleCells;
    public string DefeatReason { get; private set; }

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
            var cameraObject = new GameObject("Beta Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            view = cameraObject.GetComponent<Camera>();
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
        Begin(randomSeed ? NewSeed() : seed);
    }

    static int NewSeed() => Guid.NewGuid().GetHashCode();

    public void Begin(int newSeed)
    {
        if (generated)
        {
            generated.gameObject.SetActive(false);
            Destroy(generated.gameObject);
        }
        labels.Clear();
        ActiveSeed = newSeed;
        Grid = new MazeGrid(Mathf.Clamp(width, 3, 40), Mathf.Clamp(height, 3, 40), newSeed, exitCount, alternativeRoutes);
        obstacleCells = MazeObstacles.Select(Grid, newSeed, obstacleCount);
        generated = new GameObject("Beta Generated Maze").transform;
        generated.SetParent(transform, false);
        Progress = new RunProgress();
        ShopOpen = false;
        pausedBeforeShop = false;
        paused = false;
        HasWon = false;
        DefeatReason = "EL EMPLEO TE ATRAPO";
        running = true;

        for (int cell = 0; cell < Grid.Count; cell++)
        {
            Vector3 position = CellPosition(cell);
            if (obstacleCells.Contains(cell)) BuildJumpSection(cell);
            else Module(floorPrefab, "Floor " + cell, position, new Vector3(cellSize, 0.2f, cellSize), false);
            for (int d = 0; d < 4; d++)
            {
                if (Grid.IsOpen(cell, d)) continue;
                int neighbor = Grid.Neighbor(cell, d);
                if (neighbor >= 0 && neighbor < cell) continue;
                Vector3 direction = Direction(d);
                var wall = Module(wallPrefab, "Wall " + cell + " " + d,
                    position + direction * cellSize * 0.5f,
                    new Vector3(cellSize + WallThickness, Mathf.Max(2, wallHeight), WallThickness), true);
                wall.rotation = Quaternion.Euler(0, d % 2 == 0 ? 0 : 90, 0);
            }
        }

        foreach (var exit in Grid.Exits)
        {
            Vector3 exitPosition = CellPosition(exit.Cell) + Direction(exit.Direction) * cellSize;
            Module(floorPrefab, "Exit platform " + exit.Cell, exitPosition, new Vector3(cellSize, 0.2f, cellSize), false);
            var marker = Cube("Exit marker", generated, exitMaterial);
            marker.transform.position = exitPosition + Vector3.up * 0.025f;
            marker.transform.localScale = new Vector3(cellSize * 0.8f, 0.05f, cellSize * 0.8f);
            Label("SALIDA", exitPosition + Vector3.up * 1.7f, generated, Color.green, 0.12f);
        }

        if (!playerPrefab || !playerPrefab.GetComponent<Player>())
            throw new InvalidOperationException("Asigna Assets/03_Prefabs/Player.prefab en Player Prefab.");
        var playerObject = Instantiate(playerPrefab, CellPosition(0) + Vector3.up * 0.1f, Quaternion.identity, generated);
        playerObject.name = "Player";
        Player = playerObject.GetComponent<Player>();
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
            if (Grid.IsExitCell(i) || obstacleCells.Contains(i)) continue;
            int difference = Mathf.Abs(distances[i] - desiredDistance);
            if (difference < bestDifference) { bossCell = i; bestDifference = difference; }
        }
        var bossObject = new GameObject("Boss - Se busca empleo");
        bossObject.transform.SetParent(generated);
        Boss = bossObject.AddComponent<MazeBoss>();
        Boss.startingSpeed = bossSpeed;
        Boss.speedIncrease = bossIncrease;
        Boss.increaseEvery = bossIncreaseEvery;
        Boss.gracePeriod = bossGracePeriod;
        Boss.Initialize(this, bossCell);
        if (bossVisualPrefab)
        {
            var bossVisual = FitVisual(bossVisualPrefab, bossObject.transform, new Vector3(1.2f, 1.8f, 0.2f), true);
            bool missingTexture = false;
            foreach (var renderer in bossVisual.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                    missingTexture |= !material || !material.mainTexture;
            if (missingTexture)
            {
                foreach (var renderer in bossVisual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = paperMaterial;
                Label("SE BUSCA\nEMPLEO", bossObject.transform.position + Vector3.up * 1.2f,
                    bossObject.transform, Color.black, 0.07f);
            }
        }
        else
        {
            var paper = Cube("Employment sheet", bossObject.transform, paperMaterial);
            paper.transform.localPosition = Vector3.up;
            paper.transform.localScale = new Vector3(1.2f, 1.8f, 0.12f);
            Label("SE BUSCA\nEMPLEO", bossObject.transform.position + Vector3.up * 1.15f,
                bossObject.transform, Color.black, 0.07f);
        }

        var candidates = new List<int>();
        for (int i = 1; i < Grid.Count; i++)
            if (i != bossCell && !Grid.IsExitCell(i) && !obstacleCells.Contains(i)) candidates.Add(i);
        var random = new System.Random(newSeed ^ 7919);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int temp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = temp;
        }
        for (int i = 0; i < Mathf.Min(pillCount, candidates.Count); i++)
        {
            int cell = candidates[i];
            var pickup = new GameObject("Antidepresivos " + cell);
            pickup.transform.SetParent(generated);
            pickup.transform.position = CellPosition(cell) + Vector3.up * 0.65f;
            if (pillVisualPrefab) FitVisual(pillVisualPrefab, pickup.transform, new Vector3(0.65f, 0.4f, 0.35f), true);
            else
            {
                var box = Cube("Pill box", pickup.transform, pillMaterial);
                box.transform.localScale = new Vector3(0.65f, 0.4f, 0.35f);
                Label("PASTILLAS", pickup.transform.position + Vector3.up * 0.35f,
                    pickup.transform, Color.white, 0.035f);
            }
            pickup.AddComponent<PillPickup>().Initialize(this, cell);
        }
        SpawnEncounters(bossCell);
        SpawnSuppliedProps(bossCell);
        Physics.SyncTransforms();
        followCamera.targetHeight = cameraTargetHeight;
        followCamera.followDistance = cameraDistance;
        followCamera.mouseSensitivity = mouseSensitivity;
        followCamera.fieldOfView = cameraFieldOfView;
        followCamera.Initialize(this, Player);
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
        Player.SpeedMultiplier = Progress.SpeedMultiplier;
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
        if (won) RecordWin();
    }

    Transform Module(GameObject prefab, string objectName, Vector3 position, Vector3 size, bool wall)
    {
        var root = new GameObject(objectName).transform;
        root.SetParent(generated);
        root.position = position;
        if (prefab) FitVisual(prefab, root, size, false);
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
                var core = Cube("Wall solid core", root, wallMaterial);
                core.transform.localPosition = Vector3.up * size.y * 0.5f;
                core.transform.localScale = new Vector3(size.x, size.y, size.z * 0.8f);
            }
            var cap = Cube("Wall top cap", root, wallMaterial);
            cap.transform.localPosition = Vector3.up * (size.y - 0.07f);
            cap.transform.localScale = new Vector3(size.x, 0.14f, size.z);
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
        // Two safe lips and a raised stepping platform. No full floor collider exists underneath.
        float lip = cellSize * 0.15f;
        Vector3 axis = vertical ? Vector3.forward : Vector3.right;
        Vector3 lipSize = vertical ? new Vector3(cellSize, 0.2f, lip) : new Vector3(lip, 0.2f, cellSize);
        Module(floorPrefab, "Jump takeoff " + cell, centre - axis * (cellSize - lip) * 0.5f, lipSize, false);
        Module(floorPrefab, "Jump landing " + cell, centre + axis * (cellSize - lip) * 0.5f, lipSize, false);
        Vector3 size = vertical ? new Vector3(cellSize * 0.55f, 0.35f, cellSize * 0.18f)
                                    : new Vector3(cellSize * 0.18f, 0.35f, cellSize * 0.55f);
        var platform = Module(platformPrefab, "Jump platform " + cell, centre + Vector3.up * 0.35f, size, false);
        foreach (var renderer in platform.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = platformMaterial;
        var pit = Cube("Precipicio " + cell, generated, wallMaterial);
        pit.transform.position = centre + Vector3.down * 5;
        pit.transform.localScale = new Vector3(cellSize, 0.1f, cellSize);
        Label("SALTA", centre + Vector3.up * 1.5f, generated, new Color(1, 0.75f, 0.2f), 0.08f);
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

    static GameObject Cube(string objectName, Transform parent, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent, false);
        cube.GetComponent<Collider>().enabled = false;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    void Label(string text, Vector3 position, Transform parent, Color color, float size)
    {
        var item = new GameObject(text).AddComponent<TextMesh>();
        item.transform.SetParent(parent);
        item.transform.position = position;
        item.text = text;
        item.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        item.GetComponent<MeshRenderer>().sharedMaterial = item.font.material;
        item.fontSize = 48;
        item.characterSize = size;
        item.anchor = TextAnchor.MiddleCenter;
        item.alignment = TextAlignment.Center;
        item.color = color;
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
    }

    void OnGUI()
    {
        if (!Player || !Boss) return;
        float scale = Mathf.Max(0.55f, Mathf.Min(Screen.width / 1100f, Screen.height / 700f));
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float screenWidth = Screen.width / scale, screenHeight = Screen.height / scale;
        GUI.Box(new Rect(12, 12, 570, 190), "ESCAPA DEL EMPLEO - BETA");
        GUI.Label(new Rect(25, 40, 440, 25), $"Tiempo: {Elapsed:0}s   Semilla: {ActiveSeed}   Cajas: {Player.Collected}");
        GUI.Label(new Rect(25, 66, 440, 25), $"Tu velocidad: {Player.CurrentSpeed:0.00}   Boss: {Boss.CurrentSpeed:0.00}");
        GUI.Label(new Rect(25, 92, 440, 25), $"Boss acelera en {Boss.NextIncrease:0}s | Fatiga en {Player.NextSlowdown:0}s");
        GUI.Label(new Rect(25, 118, 540, 25), "WASD / flechas / stick: mover | Shift / L3: correr | Esc: pausa");
        GUI.Label(new Rect(25, 144, 540, 25), "Raton / stick: mirar | Espacio / A: saltar | C / Ctrl / B mando: rodar");
        GUI.Label(new Rect(25, 170, 540, 25), $"Salidas: {Grid.Exits.Count} | Tramos de salto: {obstacleCells.Count} | Pastillas: restauran velocidad");
        DrawRunHUD(screenWidth, screenHeight);
        if ((!running || paused) && !ShopOpen)
        {
            Rect panel = new Rect(screenWidth / 2 - 235, screenHeight / 2 - 115, 470, 230);
            GUI.Box(panel, paused ? "PAUSA" : HasWon ? "ESCAPASTE" : DefeatReason);
            GUI.Label(new Rect(panel.x + 24, panel.y + 35, 425, 40), paused ? "Pulsa Esc para continuar."
                : HasWon ? $"Tiempo: {Elapsed:0.0}s | Score final: {Progress.Score}" : "Prueba otra ruta y usa monedas en la tienda (B).");
            if (GUI.Button(new Rect(panel.x + 35, panel.y + 95, 400, 40), "Reintentar el mismo laberinto")) Begin(ActiveSeed);
            if (GUI.Button(new Rect(panel.x + 35, panel.y + 150, 400, 40), "Nuevo laberinto aleatorio")) Begin(NewSeed());
        }
        GUI.matrix = previous;
    }
}
