using UnityEngine;
using System.Collections.Generic;

public sealed class SpadeEnemy : MonoBehaviour
{
    public GameObject visualPrefab;
    public Transform visualRoot;
    [Min(0.3f)] public float visualHeight = 1.25f;
    [Min(0)] public float chaseSpeed = 2.6f;
    public float turnSpeed = 240;
    public float gracePeriod = 2;
    public float contactRadius = 0.28f;
    public int damage = 1;
    public Vector3 SpawnPosition { get; private set; }
    MazeBetaGame game;
    Rigidbody body;
    int cell;
    int nextCell = -1;
    float elapsed;
    float soundTime;
    readonly HashSet<int> blocked = new HashSet<int>();

    public void Initialize(MazeBetaGame owner, int mazeCell, float initialYaw)
    {
        game = owner;
        cell = mazeCell;
        SpawnPosition = game.CellPosition(cell);
        transform.position = SpawnPosition;
        transform.rotation = Quaternion.identity;
        contactRadius = Mathf.Clamp(contactRadius, 0.15f, 0.35f);
        foreach (int pit in game.ObstacleCells) blocked.Add(pit);
        if (game.Encounters != null) foreach (int turret in game.Encounters.Turrets) blocked.Add(turret);
        if (!GetComponent<Collider>())
        {
            var shape = gameObject.AddComponent<SphereCollider>();
            shape.center = Vector3.up * 0.55f;
            shape.radius = contactRadius;
        }
        body = GetComponent<Rigidbody>();
        if (!body) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        bool suppliedVisual = !visualRoot && GetComponentsInChildren<Renderer>().Length > 0;
        if (!visualRoot && !suppliedVisual && visualPrefab) visualRoot = Instantiate(visualPrefab, transform).transform;
        if (visualRoot)
        {
            NormalizeShovel();
            game.ApplyEnemyMaterialFallback(visualRoot.gameObject);
        }
        else if (!suppliedVisual) game.CreatePrimitiveVisual(PrimitiveType.Cube, transform, new Vector3(0.35f, 0.8f, 0.2f), Vector3.up * 0.55f, false);
        body.rotation = Quaternion.Euler(0, initialYaw, 0);
    }

    void NormalizeShovel()
    {
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
        var renderers = visualRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        if (bounds.size.x > bounds.size.y * 1.2f && bounds.size.x > bounds.size.z)
            visualRoot.localRotation = Quaternion.Euler(0, 0, 90);
        else if (bounds.size.z > bounds.size.y * 1.2f)
            visualRoot.localRotation = Quaternion.Euler(-90, 0, 0);
        bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        float scale = visualHeight / Mathf.Max(0.001f, bounds.size.y);
        Vector3 offset = bounds.center - transform.position;
        visualRoot.localScale = Vector3.one * scale;
        visualRoot.localPosition = new Vector3(-offset.x * scale, 0.15f - (bounds.min.y - transform.position.y) * scale, -offset.z * scale);
        foreach (var collider in visualRoot.GetComponentsInChildren<Collider>()) collider.enabled = false;
    }

    void FixedUpdate()
    {
        if (!game || !game.IsPlaying) return;
        elapsed += Time.fixedDeltaTime;
        if (elapsed < gracePeriod) return;
        Vector3 position = body.position;
        Vector3 initial = position;
        float budget = chaseSpeed * Time.fixedDeltaTime;
        int targetCell = game.WorldCell(game.Player.transform.position);
        for (int step = 0; step < 8 && budget > 0.0001f; step++)
        {
            Vector3 destination;
            if (nextCell >= 0) destination = game.CellPosition(nextCell);
            else if (targetCell == cell)
            {
                destination = game.Player.transform.position;
                destination.y = 0;
            }
            else if ((position - game.CellPosition(cell)).sqrMagnitude > 0.00001f)
                destination = game.CellPosition(cell);
            else
            {
                // Replan at cell centres. Never cut diagonally through a wall or across a pit.
                var path = game.Grid.FindPath(cell, targetCell, blocked, true);
                if (path.Count == 0) break;
                nextCell = path[0];
                destination = game.CellPosition(nextCell);
            }
            float distance = Vector3.Distance(position, destination);
            float travelled = Mathf.Min(distance, budget);
            position = Vector3.MoveTowards(position, destination, budget);
            budget -= travelled;
            if (nextCell >= 0 && position == destination) { cell = nextCell; nextCell = -1; }
            else if (targetCell == cell || travelled < distance) break;
        }
        Vector3 direction = position - initial;
        soundTime+=Time.fixedDeltaTime;
        if(direction.sqrMagnitude>.00001f && soundTime>.85f)
        { soundTime=0;MazeAudioDirector.Play(MazeSound.ShovelMove,position+Vector3.up*.5f,.24f); }
        body.MovePosition(position);
        if (direction.sqrMagnitude > 0.00001f)
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.fixedDeltaTime));
        int enemyCell = game.WorldCell(position);
        bool sharedPassage = enemyCell == targetCell;
        for (int d = 0; d < 4 && !sharedPassage; d++)
            sharedPassage = game.Grid.Neighbor(enemyCell, d) == targetCell && game.Grid.IsOpen(enemyCell, d);
        if (sharedPassage && game.TouchesPlayer(position + Vector3.up * 0.55f, contactRadius))
        {
            if(game.HitPlayer(damage, "TE ALCANZO UN SPADE", hitSource: position))MazeAudioDirector.Play(MazeSound.ShovelHit,null,.55f);
        }
    }
}
