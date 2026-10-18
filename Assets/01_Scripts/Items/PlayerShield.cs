using UnityEngine;

public class PlayerShield : MonoBehaviour
{
    GameObject activeShield;
    public bool IsShieldActive => activeShield != null;

    public void Refresh(bool active, GameObject effectPrefab)
    {
        if (active && !activeShield && effectPrefab)
        {
            activeShield = Instantiate(effectPrefab, transform);
            activeShield.transform.localPosition = Vector3.up * 0.9f;
        }
        else if (!active && activeShield)
        {
            Destroy(activeShield);
            activeShield = null;
        }
    }
}