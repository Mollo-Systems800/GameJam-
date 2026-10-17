using UnityEngine;

public sealed class EnemyProjectile : MonoBehaviour
{
    public float radius = 0.12f;
    public float lifetime = 5;
    MazeBetaGame game;
    Vector3 velocity;
    Transform source;
    int damage;

    public void Initialize(MazeBetaGame owner, Vector3 direction, float speed, int hitDamage, Transform shooter)
    {
        game = owner;
        velocity = direction.normalized * Mathf.Max(0.1f, speed);
        source = shooter;
        damage = hitDamage;
        game.CreatePrimitiveVisual(PrimitiveType.Sphere, transform, Vector3.one * radius * 2, Vector3.zero, false);
    }

    void Update()
    {
        if (!game || !game.IsPlaying) return;
        lifetime -= Time.deltaTime;
        if (lifetime <= 0) { Destroy(gameObject); return; }
        foreach (var collider in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (source && collider.transform.IsChildOf(source)) continue;
            if (collider.GetComponentInParent<Player>() == game.Player) game.HitPlayer(damage, "TE ALCANZO UNA TORRETA");
            Destroy(gameObject);
            return;
        }
        Vector3 step = velocity * Time.deltaTime;
        // Swept collision prevents fast bullets tunnelling through walls or the player.
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, velocity.normalized, step.magnitude,
            ~0, QueryTriggerInteraction.Ignore);
        RaycastHit? first = null;
        foreach (var hit in hits)
        {
            if (source && hit.collider.transform.IsChildOf(source)) continue;
            if (!first.HasValue || hit.distance < first.Value.distance) first = hit;
        }
        if (first.HasValue)
        {
            if (first.Value.collider.GetComponentInParent<Player>() == game.Player)
                game.HitPlayer(damage, "TE ALCANZO UNA TORRETA");
            Destroy(gameObject);
            return;
        }
        transform.position += step;
    }
}
