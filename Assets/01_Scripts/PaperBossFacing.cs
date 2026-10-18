using UnityEngine;

// Keep the printed sheet legible while the navigation root turns through corridors.
public sealed class PaperBossFacing : MonoBehaviour
{
    Camera view;
    void LateUpdate()
    {
        if (!view) view = Camera.main;
        if (!view) return;
        Vector3 direction = view.transform.position - transform.position; direction.y = 0;
        if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
    }
}
