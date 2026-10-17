using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class MazeBetaGame
{
    [Header("Enemigos y monedas")]
    public GameObject spadeEnemyPrefab;
    public GameObject turretEnemyPrefab;
    public GameObject projectilePrefab;
    public GameObject coinPrefab;
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
    Material enemyMaterial, coinMaterial;

    void InitializeRunFeatures()
    {
        enemyMaterial = MakeMaterial(new Color(0.85f, 0.15f, 0.1f));
        coinMaterial = MakeMaterial(new Color(1f, 0.8f, 0.08f));
        BestTime = PlayerPrefs.GetFloat("MazeBeta.BestTime", -1);
        BestScore = PlayerPrefs.GetInt("MazeBeta.BestScore", 0);
    }

    void SpawnEncounters(int bossCell)
    {
        Encounters = new MazeEncounters(Grid, ActiveSeed, obstacleCells, bossCell, spadeCount, turretCount, coinCount);
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
                if (!Grid.IsOpen(cell, d)) { offset = Direction(d) * (cellSize * 0.5f - 0.55f); break; }
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

    public void FireProjectile(Vector3 origin, Vector3 direction, float speed, int damage, Transform shooter)
    {
        if (!IsPlaying) return;
        var item = Spawn(projectilePrefab, "Turret projectile", origin);
        var projectile = item.GetComponent<EnemyProjectile>();
        if (!projectile) projectile = item.AddComponent<EnemyProjectile>();
        projectile.Initialize(this, direction, speed, damage, shooter);
    }

    public GameObject CreatePrimitiveVisual(PrimitiveType shape, Transform parent, Vector3 size, Vector3 offset, bool gold)
    {
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
                if (!assigned[i] || !assigned[i].mainTexture) assigned[i] = enemyMaterial;
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

    void DrawRunHUD(float screenWidth, float screenHeight)
    {
        float x = screenWidth - 310;
        GUI.Box(new Rect(x, 12, 298, 190), "PARTIDA Y TIENDA");
        GUI.Label(new Rect(x + 12, 38, 280, 24), $"Vida: {Progress.Health}/{RunProgress.MaxHealth} | Monedas: {Progress.Coins}");
        GUI.Label(new Rect(x + 12, 62, 280, 24), $"Score por tiempo + monedas: {Progress.Score}");
        GUI.Label(new Rect(x + 12, 86, 280, 24), $"Mejor score: {BestScore} | Mejor tiempo: {(BestTime < 0 ? "--" : BestTime.ToString("0.0") + "s")}");
        GUI.Label(new Rect(x + 12, 110, 280, 24), $"Velocidad x1.5: {Progress.SpeedSeconds:0}s | Escudo: {Progress.ShieldSeconds:0}s");
        if (running && GUI.Button(new Rect(x + 12, 145, 274, 32), ShopOpen ? "Cerrar tienda (B / Esc)" : "Tienda (B / Select)"))
            SetShopOpen(!ShopOpen);
        if (!ShopOpen) return;
        Rect panel = new Rect(screenWidth / 2 - 260, screenHeight / 2 - 160, 520, 320);
        GUI.Box(panel, "TIENDA - PARTIDA PAUSADA");
        GUI.Label(new Rect(panel.x + 22, panel.y + 30, 476, 26), $"Saldo: {Progress.Coins} monedas. Los power-ups se activan al comprar.");
        DrawOffer(new Rect(panel.x + 22, panel.y + 72, 476, 44), PowerUpKind.Speed, "Velocidad x1.5 durante 10s");
        DrawOffer(new Rect(panel.x + 22, panel.y + 126, 476, 44), PowerUpKind.Shield, "Escudo contra enemigos durante 8s");
        DrawOffer(new Rect(panel.x + 22, panel.y + 180, 476, 44), PowerUpKind.Heal, "Recuperar 1 punto de vida");
        GUI.Label(new Rect(panel.x + 22, panel.y + 232, 476, 28), "Las monedas y mejoras se reinician en cada intento.");
        if (GUI.Button(new Rect(panel.x + 22, panel.y + 270, 476, 34), "Volver al laberinto")) SetShopOpen(false);
    }

    void DrawOffer(Rect rect, PowerUpKind kind, string title)
    {
        bool previous = GUI.enabled;
        GUI.enabled = Progress.CanBuy(kind);
        if (GUI.Button(rect, title + " - " + RunProgress.Price(kind) + " monedas")) BuyPowerUp(kind);
        GUI.enabled = previous;
    }
}
