using System.Collections.Generic;
using UnityEngine;

public sealed partial class MazeBetaGame
{
    [Header("Parkour sobre puas")]
    public bool parkourEnabled = true;
    [Range(3, 10)] public int parkourCellLength = 5;
    [Tooltip("Metros de espacio vacio entre bordes. Se limita al alcance del salto con fatiga.")]
    [Range(1.25f, 2.5f)] public float parkourGap = 1.8f;
    [Range(1.4f, 2.5f)] public float spikeBedDepth = 1.9f;
    [Range(0.4f, 1)] public float spikeHeight = 0.85f;
    public GameObject spikePrefab;
    public GameObject spikeFloorPrefab;
    readonly List<BoxCollider> spikeHazards = new List<BoxCollider>();
    readonly List<BoxCollider> parkourPlatforms = new List<BoxCollider>();
    public IReadOnlyList<BoxCollider> SpikeHazards => spikeHazards;
    public IReadOnlyList<BoxCollider> ParkourPlatforms => parkourPlatforms;
    public float ActualParkourGap { get; private set; }
    Mesh spikeMesh;

    // Leave landing margin even at the slowest normal movement speed. Shift or
    // store upgrades are never required to complete the generated section.
    float EffectiveParkourGap => Mathf.Clamp(parkourGap, 1.25f,
        Mathf.Max(1.25f, Mathf.Min(2.6f, playerSpeed) *
        (2 * Mathf.Sqrt(2 * Mathf.Max(0.1f, jumpHeight) / Mathf.Max(0.1f, Mathf.Abs(Physics.gravity.y)))) * 0.78f));

    void BuildParkourZone()
    {
        ActualParkourGap = 0;
        if (Grid.ParkourCells.Count < 3) return;
        bool vertical = Grid.ParkourDirection == 0;
        Vector3 axis = Direction(Grid.ParkourDirection);
        int firstPit = Grid.ParkourCells[1];
        float length = (Grid.ParkourCells.Count - 2) * cellSize;
        Vector3 start = CellPosition(firstPit) - axis * cellSize * 0.5f;
        var layout = new MazeParkourLayout(length, EffectiveParkourGap);
        ActualParkourGap = layout.Gap;
        for (int i = 0; i < layout.Platforms.Count; i++)
        {
            var section = layout.Platforms[i];
            bool end = i == 0 || i == layout.Platforms.Count - 1;
            float width = end ? cellSize : Mathf.Max(0.9f, cellSize - WallThickness - 0.3f);
            Vector3 size = vertical ? new Vector3(width, 0.35f, section.Length) : new Vector3(section.Length, 0.35f, width);
            GameObject prefab = i == 0 ? EnvTakeoff : i == layout.Platforms.Count - 1 ? EnvLanding : EnvPlatform;
            var platform = ParkourModule(prefab, "Parkour platform " + i, start + axis * section.Centre, size);
            if (!prefab)
                foreach (var renderer in platform.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = platformMaterial;
            parkourPlatforms.Add(platform.GetComponent<BoxCollider>());
        }
        BuildSpikeBed(start + axis * length * 0.5f, vertical, length, "Parkour spike bed");
    }

    Transform ParkourModule(GameObject prefab, string name, Vector3 position, Vector3 size)
    {
        var root = Module(prefab, name, position, size, false);
        // Module aligns the imported visual with the walkable collider top.
        return root;
    }

    void BuildSpikeBed(Vector3 centre, bool vertical, float length, string name)
    {
        float depth = Mathf.Clamp(spikeBedDepth, 1.4f, 2.5f);
        float height = Mathf.Clamp(spikeHeight, 0.4f, Mathf.Min(1, depth - 0.65f));
        Vector3 size = vertical ? new Vector3(cellSize, 0.25f, length) : new Vector3(length, 0.25f, cellSize);
        var floor = ParkourModule(EnvSpikeFloor, name, centre + Vector3.down * depth, size);
        var hazard = new GameObject("Puas - contacto mortal").AddComponent<BoxCollider>();
        hazard.transform.SetParent(floor, false);
        hazard.center = Vector3.up * height * 0.5f;
        hazard.size = new Vector3(size.x, height, size.z);
        hazard.isTrigger = true;
        spikeHazards.Add(hazard);
        int across = Mathf.Max(2, Mathf.CeilToInt(cellSize / 0.55f));
        int along = Mathf.Max(2, Mathf.CeilToInt(length / 0.55f));
        for (int z = 0; z < along; z++)
            for (int x = 0; x < across; x++)
            {
                var spike = new GameObject("Pua").transform;
                spike.SetParent(floor, false);
                float side = (x + 0.5f) * cellSize / across - cellSize * 0.5f;
                float forward = (z + 0.5f) * length / along - length * 0.5f;
                spike.localPosition = vertical ? new Vector3(side, 0, forward) : new Vector3(forward, 0, side);
                Vector3 spikeSize = new Vector3(cellSize / across * 0.82f, height, length / along * 0.82f);
                if (!vertical) spikeSize = new Vector3(spikeSize.z, height, spikeSize.x);
                if (EnvSpike) FitVisual(EnvSpike, spike, spikeSize, false);
                else
                {
                    if (!spikeMesh) spikeMesh = CreateSpikeMesh();
                    spike.gameObject.AddComponent<MeshFilter>().sharedMesh = spikeMesh;
                    spike.gameObject.AddComponent<MeshRenderer>().sharedMaterial = platformMaterial;
                    spike.localScale = spikeSize;
                }
            }
        // Close the sides below floor level as well; a missed jump lands in the
        // bed instead of escaping sideways into the void underneath a wall.
        Vector3 cross = vertical ? Vector3.right : Vector3.forward;
        Vector3 sideSize = vertical ? new Vector3(0.2f, depth, length) : new Vector3(length, depth, 0.2f);
        for (int side = -1; side <= 1; side += 2)
            ParkourModule(EnvSpikeFloor, name + " side " + side,
                centre + cross * (cellSize * 0.5f) * side, sideSize);
    }

    bool CheckSpikeContact()
    {
        var body = Player.GetComponent<CharacterController>().bounds;
        foreach (var hazard in spikeHazards)
            if (hazard && hazard.bounds.Intersects(body))
            {
                DefeatReason = "CAISTE EN LAS PUAS";
                Player.ReactToDamage();
                MazeAudioDirector.Play(MazeSound.Hurt);
                Finish(false);
                return true;
            }
        return false;
    }

    static Mesh CreateSpikeMesh()
    {
        var a = new Vector3(-0.5f, 0, -0.5f); var b = new Vector3(0.5f, 0, -0.5f);
        var c = new Vector3(0.5f, 0, 0.5f); var d = new Vector3(-0.5f, 0, 0.5f);
        var tip = Vector3.up;
        var vertices = new[] { a, tip, b, b, tip, c, c, tip, d, d, tip, a, a, b, c, a, c, d };
        var triangles = new int[vertices.Length];
        for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
        var mesh = new Mesh { name = "Parkour spike pyramid", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
}
