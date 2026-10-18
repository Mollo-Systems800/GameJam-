using UnityEngine;

public sealed class CoinPickup : MonoBehaviour
{
    public int value = 1;
    public GameObject visualPrefab;
    MazeBetaGame game;
    int cell;
    Vector3 origin;
    float age;
    public void Initialize(MazeBetaGame owner, int mazeCell)
    {
        game = owner;
        cell = mazeCell;
        origin = transform.position;
        if (GetComponentsInChildren<Renderer>().Length == 0)
            game.CreatePrimitiveVisual(PrimitiveType.Cylinder, transform, new Vector3(0.4f, 0.07f, 0.4f), Vector3.zero, true,
                visualPrefab ? visualPrefab : game.coinVisualPrefab);
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
    }
    void Update()
    {
        if (!game || !game.IsPlaying) return;
        age += Time.deltaTime;
        transform.position = origin + Vector3.up * (Mathf.Sin(age * 3) * 0.1f);
        transform.Rotate(Vector3.forward, 60 * Time.deltaTime, Space.Self);
        if (game.WorldCell(game.Player.transform.position) == cell && game.TouchesPlayer(transform.position, 0.45f))
        {
            game.Progress.AddCoins(value);
            MazeAudioDirector.Play(MazeSound.Coin);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
