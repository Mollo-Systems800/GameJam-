using UnityEngine;

public sealed class ItemPickup : MonoBehaviour
{
    public ItemData itemData;
    MazeBetaGame game;
    int cell;
    Vector3 origin;
    float age;

    public void Initialize(MazeBetaGame owner, int mazeCell, ItemData data)
    {
        game = owner;
        cell = mazeCell;
        itemData = data;
        origin = transform.position;
        if (GetComponentsInChildren<Renderer>().Length == 0) BuildVisual();
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
    }

    void BuildVisual()
    {
        if (itemData && itemData.itemPrefab)
        {
            game.CreatePrimitiveVisual(PrimitiveType.Sphere, transform, Vector3.one * 0.5f, Vector3.zero, true, itemData.itemPrefab);
            return;
        }
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Item visual";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localScale = Vector3.one * 0.4f;
        sphere.GetComponent<Collider>().enabled = false;
        Color color = Color.white;
        if (itemData)
            switch (itemData.itemType)
            {
                case ItemType.Curacion: color = new Color(0.2f, 0.9f, 0.3f); break;
                case ItemType.Escudo: color = new Color(0.2f, 0.8f, 1f); break;
                case ItemType.Velocidad: color = new Color(1f, 0.55f, 0.1f); break;
                case ItemType.SaltoDoble: color = new Color(0.8f, 0.3f, 1f); break;
            }
        sphere.GetComponent<Renderer>().material.color = color;
    }

    void Update()
    {
        if (!game || !game.IsPlaying) return;
        age += Time.deltaTime;
        transform.position = origin + Vector3.up * (Mathf.Sin(age * 2.5f) * 0.1f);
        transform.Rotate(Vector3.up, 60 * Time.deltaTime, Space.World);
        if (game.WorldCell(game.Player.transform.position) == cell && game.TouchesPlayer(transform.position, 0.5f))
        {
            game.CollectItem(itemData);
            Destroy(gameObject);
        }
    }
}