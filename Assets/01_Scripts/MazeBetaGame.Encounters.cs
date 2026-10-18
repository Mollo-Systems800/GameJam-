using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class MazeBetaGame
{
    [Header("Enemigos y monedas")]
    public GameObject spadeEnemyPrefab;
    public GameObject turretEnemyPrefab;
    public GameObject projectilePrefab;
    public GameObject coinPrefab;
    [Header("Modelos de torretas, monedas y proyectiles")]
    public GameObject turretBaseVisualPrefab;
    public GameObject turretBarrelVisualPrefab;
    public GameObject coinVisualPrefab;
    public GameObject projectileVisualPrefab;
    [Header("Senoras sentadas y tienda")]
    [UnityEngine.Serialization.FormerlySerializedAs("shopkeeperPrefab")]
    public GameObject seatedWomanPrefab;
    [Min(3)] public int seatedWomanCount = 3;
    [Range(-180,180)] public float seatedWomanYaw = 0;
    [Range(-180,180)] public float seatedChairYaw = 180;
    public Vector3 seatedWomanOffset = new Vector3(0,0,0.25f);
    public GameObject storePrefab;
    public GameObject chairPrefab;
    [Range(0, 30)] public int chairCount = 10;
    [Range(0, 20)] public int spadeCount = 6;
    [Range(0, 20)] public int turretCount = 4;
    [Range(0, 100)] public int coinCount = 36;
    [System.NonSerialized] public bool recordResults = true;
    public RunProgress Progress { get; private set; } = new RunProgress();
    public float BestTime { get; private set; } = -1;
    public int BestScore { get; private set; }
    public bool ShopOpen { get; private set; }
    public MazeEncounters Encounters { get; private set; }
    bool pausedBeforeShop;
    Material enemyMaterial, coinMaterial, spadeMaterial;

    void InitializeRunFeatures()
    {
        enemyMaterial = MakeMaterial(new Color(0.85f, 0.15f, 0.1f));
        coinMaterial = MakeMaterial(new Color(1f, 0.8f, 0.08f));
        spadeMaterial = MakeMaterial(new Color(0.65f, 0.69f, 0.72f));
        if (spadeMaterial.HasProperty("_Metallic")) spadeMaterial.SetFloat("_Metallic", 0.45f);
        BestTime = PlayerPrefs.GetFloat("MazeBeta.BestTime", -1);
        BestScore = PlayerPrefs.GetInt("MazeBeta.BestScore", 0);
    }

    void SpawnEncounters(int bossCell)
    {
        Encounters = new MazeEncounters(Grid, ActiveSeed, obstacleCells, bossCell, spadeCount, turretCount, coinCount, residentCells);
        for (int i = 0; i < Encounters.Spades.Count; i++)
        {
            int cell = Encounters.Spades[i];
            var item = Spawn(spadeEnemyPrefab, "Spade " + cell, CellPosition(cell));
            var enemy = item.GetComponent<SpadeEnemy>();
            if (!enemy) enemy = item.AddComponent<SpadeEnemy>();
            enemy.Initialize(this, cell, (i * 137 + (ActiveSeed & 255)) % 360);
        }
        foreach (int cell in Encounters.Turrets)
        {
            Vector3 offset = Vector3.zero;
            for (int d = 0; d < 4; d++)
                if (!Grid.IsOpen(cell, d)) { offset = Direction(d) * Mathf.Max(0, cellSize * 0.5f - WallThickness * 0.5f - 0.4f); break; }
            var item = Spawn(turretEnemyPrefab, "Turret " + cell, CellPosition(cell) + offset);
            var turret = item.GetComponent<TurretEnemy>();
            if (!turret) turret = item.AddComponent<TurretEnemy>();
            turret.Initialize(this);
        }
        foreach (int cell in Encounters.Coins)
        {
            var item = Spawn(coinPrefab, "Coin " + cell, CellPosition(cell) + new Vector3(0.45f, 0.75f, 0.35f));
            var coin = item.GetComponent<CoinPickup>();
            if (!coin) coin = item.AddComponent<CoinPickup>();
            coin.Initialize(this, cell);
        }
    }

    GameObject Spawn(GameObject prefab, string objectName, Vector3 position)
    {
        var item = prefab ? Instantiate(prefab, position, Quaternion.identity, generated) : new GameObject(objectName);
        item.name = objectName;
        item.transform.SetParent(generated, true);
        item.transform.position = position;
        return item;
    }

    void SpawnSuppliedProps(int bossCell)
    {
        SpawnStoreTerminal();
        SpawnSeatedWomen();
        if (!EnvChair || chairCount <= 0) return;
        var reserved = new System.Collections.Generic.HashSet<int>(obstacleCells) { 0, bossCell };
        foreach (int cell in Grid.ParkourCells) reserved.Add(cell);
        foreach (int cell in residentCells) reserved.Add(cell);
        foreach (var exit in Grid.Exits) reserved.Add(exit.Cell);
        foreach (int cell in Encounters.Spades) reserved.Add(cell);
        foreach (int cell in Encounters.Turrets) reserved.Add(cell);
        foreach (int cell in Encounters.Coins) reserved.Add(cell);
        var cells = new System.Collections.Generic.List<int>();
        for (int c = 1; c < Grid.Count; c++)
            if (!reserved.Contains(c)) cells.Add(c);
        var random = new System.Random(ActiveSeed ^ 54709);
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int temp = cells[i]; cells[i] = cells[j]; cells[j] = temp;
        }
        int placed = 0;
        foreach (int cell in cells)
        {
            if (placed >= chairCount) break;
            int start = random.Next(4);
            for (int i = 0; i < 4; i++)
            {
                int d = (start + i) % 4;
                if (Grid.IsOpen(cell, d)) continue;
                // Keep a clear central lane even at the minimum supported cell size.
                float width = Mathf.Min(0.8f, cellSize * 0.5f - WallThickness * 0.5f - 0.5f);
                if (width < 0.3f) break;
                var chair = SpawnWallProp(EnvChair, "Inter Chair " + cell, cell, d,
                    new Vector3(width, 1, width), WallThickness * 0.5f + width * 0.5f + 0.08f);
                var shape = chair.AddComponent<BoxCollider>();
                shape.size = new Vector3(width, 0.8f, width);
                shape.center = Vector3.up * 0.4f;
                placed++;
                break;
            }
        }
    }

    GameObject SpawnWallProp(GameObject prefab, string objectName, int cell, int side, Vector3 size, float wallInset)
    {
        var root = new GameObject(objectName);
        root.transform.SetParent(generated, false);
        root.transform.position = CellPosition(cell) + Direction(side) * (cellSize * 0.5f - wallInset);
        FitVisual(prefab, root.transform, size, true);
        root.transform.rotation = Quaternion.LookRotation(-Direction(side));
        return root;
    }

    public void FireProjectile(Vector3 origin, Vector3 direction, float speed, int damage, Transform shooter)
    {
        if (!IsPlaying) return;
        var item = Spawn(projectilePrefab, "Turret projectile", origin);
        var projectile = item.GetComponent<EnemyProjectile>();
        if (!projectile) projectile = item.AddComponent<EnemyProjectile>();
        projectile.Initialize(this, direction, speed, damage, shooter);
    }

    public GameObject CreatePrimitiveVisual(PrimitiveType shape, Transform parent, Vector3 size, Vector3 offset, bool gold, GameObject visualPrefab = null)
    {
        if (visualPrefab)
        {
            var wrapper = new GameObject("Prefab visual");
            wrapper.transform.SetParent(parent, false);
            FitVisual(visualPrefab, wrapper.transform, size, true);
            wrapper.transform.localPosition = offset - Vector3.up * size.y * 0.5f;
            return wrapper;
        }
        var visual = GameObject.CreatePrimitive(shape);
        visual.name = gold ? "Coin visual" : "Enemy visual";
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = offset;
        visual.transform.localScale = size;
        visual.GetComponent<Collider>().enabled = false;
        visual.GetComponent<Renderer>().sharedMaterial = gold ? coinMaterial : enemyMaterial;
        return visual;
    }

    public void ApplyEnemyMaterialFallback(GameObject visual)
    {
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
        {
            var assigned = renderer.sharedMaterials;
            for (int i = 0; i < assigned.Length; i++)
                if (!assigned[i] || !assigned[i].mainTexture) assigned[i] = spadeMaterial;
            renderer.sharedMaterials = assigned;
        }
    }

    public bool TouchesPlayer(Vector3 centre, float radius)
    {
        var controller = Player.GetComponent<CharacterController>();
        Vector3 axis = Player.transform.position + controller.center;
        float halfSegment = Mathf.Max(0, controller.height * 0.5f - controller.radius);
        axis.y = Mathf.Clamp(centre.y, axis.y - halfSegment, axis.y + halfSegment);
        return (axis - centre).sqrMagnitude <= (controller.radius + radius) * (controller.radius + radius);
    }

    public bool HitPlayer(int damage, string reason)
    {
        if (!IsPlaying || !Progress.TakeDamage(damage)) return false;
        if (Progress.Health == 0)
        {
            DefeatReason = reason;
            Finish(false);
        }
        return true;
    }

    public void SetShopOpen(bool open)
    {
        if (!running || ShopOpen == open) return;
        if (open)
        {
            pausedBeforeShop = paused;
            ShopOpen = true;
        }
        else
        {
            ShopOpen = false;
            paused = pausedBeforeShop;
        }
        RefreshActivity();
    }

    void RefreshActivity()
    {
        Player.CanMove = IsPlaying;
        Boss.IsActive = IsPlaying;
    }

    public bool BuyPowerUp(PowerUpKind kind)
    {
        if (!running || !ShopOpen || !Progress.TryBuy(kind)) return false;
        Player.SpeedMultiplier = Progress.SpeedMultiplier;
        return true;
    }

    void RecordWin()
    {
        if (!recordResults) return;
        if (BestTime < 0 || Elapsed < BestTime)
        {
            BestTime = Elapsed;
            PlayerPrefs.SetFloat("MazeBeta.BestTime", BestTime);
        }
        if (Progress.Score > BestScore)
        {
            BestScore = Progress.Score;
            PlayerPrefs.SetInt("MazeBeta.BestScore", BestScore);
        }
        PlayerPrefs.Save();
    }

}
