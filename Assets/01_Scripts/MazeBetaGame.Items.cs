using System.Collections.Generic;
using UnityEngine;

public sealed partial class MazeBetaGame
{
    [Header("Power-ups recogibles")]
    public ItemData[] itemPool;
    [Range(0, 10)] public int itemCount = 4;
    public GameObject shieldEffectPrefab;

    MazeGrid itemsGrid;

    void FixedUpdate()
    {
        if (Grid == null || Player == null || Progress == null || Encounters == null) return;

        if (itemsGrid != Grid)
        {
            itemsGrid = Grid;
            int bossCell = Boss ? WorldCell(Boss.transform.position) : -1;
            SpawnItems(bossCell);
        }

        ApplyProgressToPlayer();
    }

    void SpawnItems(int bossCell)
    {
        if (itemPool == null || itemPool.Length == 0 || itemCount <= 0) return;
        var reserved = new HashSet<int>(obstacleCells) { 0, bossCell };
        reserved.UnionWith(residentCells);
        reserved.UnionWith(Grid.ParkourCells);
        foreach (var exit in Grid.Exits) reserved.Add(exit.Cell);
        foreach (int c in Encounters.Spades) reserved.Add(c);
        foreach (int c in Encounters.Turrets) reserved.Add(c);
        foreach (int c in Encounters.Coins) reserved.Add(c);
        var cells = new List<int>();
        for (int c = 1; c < Grid.Count; c++) if (!reserved.Contains(c)) cells.Add(c);
        var random = new System.Random(ActiveSeed ^ 99371);
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int t = cells[i]; cells[i] = cells[j]; cells[j] = t;
        }
        for (int i = 0; i < Mathf.Min(itemCount, cells.Count); i++)
        {
            var data = itemPool[random.Next(itemPool.Length)];
            if (!data) continue;
            var go = Spawn(null, "Item " + data.itemType + " " + cells[i],
                CellPosition(cells[i]) + new Vector3(-0.4f, 0.8f, -0.35f));
            go.AddComponent<ItemPickup>().Initialize(this, cells[i], data);
        }
    }

    public void CollectItem(ItemData item)
    {
        if (!IsPlaying || !item) return;
        Progress.ApplyItem(item.itemType, item.duration, item.effectValue);
        ApplyProgressToPlayer();
    }

    public void ApplyProgressToPlayer()
    {
        Player.SpeedMultiplier = Progress.SpeedMultiplier;
        Player.CanDoubleJump = Progress.HasDoubleJump;
        var shield = Player.GetComponent<PlayerShield>();
        if (!shield) shield = Player.gameObject.AddComponent<PlayerShield>();
        shield.Refresh(Progress.ShieldSeconds > 0, shieldEffectPrefab);
    }
}
