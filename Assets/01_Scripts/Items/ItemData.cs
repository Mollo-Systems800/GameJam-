using UnityEngine;

public enum ItemType
{
    Curacion,
    Escudo,
    Velocidad,
    SaltoDoble
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Items/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Información")]
    public string itemName;
    [TextArea] public string description;

    [Header("Prefab del ítem")]
    public GameObject itemPrefab;

    [Header("Configuración del efecto")]
    public ItemType itemType;
    public float duration = 5f;
    public float effectValue = 2f;
}
