using UnityEngine;

public sealed class PillPickup : MonoBehaviour
{
    MazeBetaGame game;
    Vector3 origin;
    int cell;
    public void Initialize(MazeBetaGame owner, int mazeCell)
    {
        game = owner;
        cell = mazeCell;
        origin = transform.position;
    }
    void Update()
    {
        if (!game || !game.IsPlaying) return;
        transform.position = origin + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.1f);
        transform.Rotate(Vector3.up, 35 * Time.deltaTime, Space.World);
        Vector3 delta = game.Player.transform.position - origin;
        delta.y = 0;
        if (game.WorldCell(game.Player.transform.position) == cell && delta.sqrMagnitude < 1f
            && Mathf.Abs(game.Player.transform.position.y - origin.y) < 1.5f)
        {
            game.Player.CollectPills();
            Destroy(gameObject);
        }
    }
}
