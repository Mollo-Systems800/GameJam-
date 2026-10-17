using UnityEngine;

public sealed class SpadeEnemy : MonoBehaviour
{
    public GameObject visualPrefab;
    public float orbitRadius = 0.75f;
    public float degreesPerSecond = 100;
    public float contactRadius = 0.28f;
    public int damage = 1;
    public Vector3 Anchor { get; private set; }
    MazeBetaGame game;
    Rigidbody body;
    int cell;
    float angle;

    public void Initialize(MazeBetaGame owner, int mazeCell, float initialAngle)
    {
        game = owner;
        cell = mazeCell;
        Anchor = game.CellPosition(cell);
        angle = initialAngle;
        contactRadius = Mathf.Clamp(contactRadius, 0.15f, 0.35f);
        orbitRadius = Mathf.Clamp(orbitRadius, 0.1f, Mathf.Max(0.1f, game.cellSize * 0.5f - contactRadius - 0.25f));
        var shape = gameObject.AddComponent<SphereCollider>();
        shape.center = Vector3.up * 0.55f;
        shape.radius = contactRadius;
        body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        if (visualPrefab)
        {
            var visual = MazeBetaGame.FitVisual(visualPrefab, transform, new Vector3(0.5f, 0.85f, 0.5f), true);
            game.ApplyEnemyMaterialFallback(visual);
        }
        else game.CreatePrimitiveVisual(PrimitiveType.Cube, transform, new Vector3(0.35f, 0.8f, 0.2f), Vector3.up * 0.55f, false);
        transform.position = OrbitPosition();
    }

    Vector3 OrbitPosition()
    {
        float radians = angle * Mathf.Deg2Rad;
        return Anchor + new Vector3(Mathf.Cos(radians), 0, Mathf.Sin(radians)) * orbitRadius;
    }

    void FixedUpdate()
    {
        if (!game || !game.IsPlaying) return;
        angle = Mathf.Repeat(angle + degreesPerSecond * Time.fixedDeltaTime, 360);
        Vector3 next = OrbitPosition();
        body.MovePosition(next);
        body.MoveRotation(Quaternion.Euler(0, -angle, 0));
        if (game.WorldCell(game.Player.transform.position) == cell && game.TouchesPlayer(next + Vector3.up * 0.55f, contactRadius))
            game.HitPlayer(damage, "TE ALCANZO UN SPADE");
    }
}
