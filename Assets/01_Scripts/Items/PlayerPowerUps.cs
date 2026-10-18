using System.Collections;
using UnityEngine;

public class PlayerPowerUps : MonoBehaviour
{
    [Header("Vida")]
    public float health = 100f;
    public float maxHealth = 100f;

    [Header("Movimiento")]
    public float moveSpeed = 5f;

    [Header("Power-ups activos")]
    public bool hasShield;
    public bool hasDoubleJump;

    public void UseItem(ItemData item)
    {
        if (item == null) return;

        switch (item.itemType)
        {
            case ItemType.Curacion:
                health = Mathf.Min(
                    health + item.effectValue,
                    maxHealth
                );
                break;

            case ItemType.Escudo:
                StartCoroutine(ActivateShield(item.duration));
                break;

            case ItemType.Velocidad:
                StartCoroutine(ActivateSpeed(
                    item.effectValue,
                    item.duration
                ));
                break;

            case ItemType.SaltoDoble:
                hasDoubleJump = true;
                break;
        }
    }

    IEnumerator ActivateShield(float duration)
    {
        hasShield = true;

        yield return new WaitForSeconds(duration);

        hasShield = false;
    }

    IEnumerator ActivateSpeed(float multiplier, float duration)
    {
        moveSpeed *= multiplier;

        yield return new WaitForSeconds(duration);

        moveSpeed /= multiplier;
    }
}
