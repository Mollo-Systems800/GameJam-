using System.Collections.Generic;
using UnityEngine;

public sealed class MazeBoss : MonoBehaviour
{
    public float startingSpeed = 2.2f;
    public float speedIncrease = 0.1f;
    public float increaseEvery = 30f;
    public float gracePeriod = 5f;
    public float CurrentSpeed { get; private set; }
    public float NextIncrease => Mathf.Max(0, increaseEvery - elapsed % Mathf.Max(0.1f, increaseEvery));
    public bool IsActive { get; set; } = true;
    MazeBetaGame game;
    int cell;
    int targetCell = -1;
    List<int> path = new List<int>();
    int pathIndex;
    float elapsed;
    float rustleTime;
    int speedStep;

    public void Initialize(MazeBetaGame owner, int start)
    {
        game = owner;
        cell = start;
        CurrentSpeed = startingSpeed;
        transform.position = game.CellPosition(cell);
    }

    void Update()
    {
        if (!game || !IsActive) return;
        elapsed += Time.deltaTime;
        int step=increaseEvery>0?Mathf.FloorToInt(elapsed/increaseEvery):0;
        CurrentSpeed = startingSpeed + step * speedIncrease;
        if(step>speedStep){speedStep=step;MazeAudioDirector.Play(MazeSound.BossAccelerate,transform.position,.65f);}
        if (elapsed < gracePeriod) return;
        rustleTime+=Time.deltaTime;
        if(rustleTime>.7f){rustleTime=0;MazeAudioDirector.Play(MazeSound.BossRustle,transform.position,.4f);}
        // Repath at cell centres so the boss cannot cut diagonally through corridor walls.
        if (pathIndex >= path.Count)
        {
            int target = game.WorldCell(game.Player.transform.position);
            if (target == cell)
            {
                Vector3 playerTarget = game.Player.transform.position;
                playerTarget.y = 0;
                transform.position = Vector3.MoveTowards(transform.position, playerTarget, CurrentSpeed * Time.deltaTime);
            }
            else if (Vector3.Distance(transform.position, game.CellPosition(cell)) > 0.001f)
            {
                // Return to the centre before changing corridors after a within-cell chase.
                transform.position = Vector3.MoveTowards(transform.position, game.CellPosition(cell), CurrentSpeed * Time.deltaTime);
            }
            else if (target != targetCell || path.Count == 0)
            {
                targetCell = target;
                path = game.Grid.FindPath(cell, target);
                pathIndex = 0;
            }
        }
        float remaining = CurrentSpeed * Time.deltaTime;
        while (remaining > 0 && pathIndex < path.Count)
        {
            Vector3 destination = game.CellPosition(path[pathIndex]);
            float distance = Vector3.Distance(transform.position, destination);
            Vector3 direction = destination - transform.position;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
            transform.position = Vector3.MoveTowards(transform.position, destination, remaining);
            remaining -= distance;
            if (transform.position == destination)
            {
                cell = path[pathIndex++];
                if (game.WorldCell(game.Player.transform.position) != targetCell)
                {
                    path.Clear();
                    pathIndex = 0;
                    targetCell = -1;
                    break;
                }
            }
        }
        Vector3 playerPosition = game.Player.transform.position;
        Vector3 bossPosition = transform.position;
        playerPosition.y = bossPosition.y = 0;
        int playerCell = game.WorldCell(playerPosition);
        int bossCell = game.WorldCell(bossPosition);
        bool sharedPassage = playerCell == bossCell;
        for (int d = 0; d < 4 && !sharedPassage; d++)
            sharedPassage = game.Grid.Neighbor(bossCell, d) == playerCell && game.Grid.IsOpen(bossCell, d);
        if (sharedPassage && Vector3.Distance(playerPosition, bossPosition) < 0.85f
            && game.Player.transform.position.y > -0.5f)
            game.HitPlayer(RunProgress.MaxHealth, "EL EMPLEO TE ATRAPO", true);
    }
}
