using System.Collections.Generic;
using UnityEngine;

public partial class Player
{
    [Header("Reaccion al dano")]
    public Color damageTint = new Color(1, .035f, .045f, 1);
    [Min(.1f)] public float damageFlashDuration = .55f;
    [Min(0)] public float knockbackSpeed = 6;
    [Min(.05f)] public float knockbackDuration = .28f;

    public bool IsRecoiling => recoilRemaining > 0;
    public bool DamageFlashActive => flashRemaining > 0;
    float recoilRemaining, flashRemaining;
    Vector3 recoilDirection;
    readonly List<DamageSurface> damageSurfaces = new List<DamageSurface>();
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int LegacyColor = Shader.PropertyToID("_Color");

    sealed class DamageSurface
    {
        public Renderer renderer;
        public int slot, colorProperty;
        public Color originalColor;
        public MaterialPropertyBlock original, tinted;
    }

    // Called only after accepted damage. The renderers are collected here because
    // PlayerLimbAnimator creates the visible skinned meshes at runtime in Start.
    public void ReactToDamage(Vector3? sourcePosition = null)
    {
        RestoreDamageColors();
        Transform facing = visual ? visual : transform;
        recoilDirection = Vector3.ProjectOnPlane(sourcePosition.HasValue
            ? transform.position - sourcePosition.Value : -facing.forward, Vector3.up);
        if (recoilDirection.sqrMagnitude < .001f)
            recoilDirection = Vector3.ProjectOnPlane(-facing.forward, Vector3.up);
        if (recoilDirection.sqrMagnitude < .001f) recoilDirection = Vector3.back;
        recoilDirection.Normalize();
        recoilRemaining = Mathf.Max(.05f, knockbackDuration);
        flashRemaining = Mathf.Max(.1f, damageFlashDuration);
        roll.Reset();
        bufferedJump = 0;
        IsSprinting = false;

        // A small collision-resolved initial kick is also visible on a lethal hit,
        // before the defeat cinematic takes ownership of the player's movement.
        if (controller && controller.enabled && CanMove && gameObject.activeInHierarchy)
            controller.Move(recoilDirection * Mathf.Min(.09f, Mathf.Max(0, knockbackSpeed) * .015f));

        foreach (var renderer in facing.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || PlayerLimbAnimator.IsControl(renderer.name)) continue;
            var materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                var material = materials[slot];
                if (!material) continue;
                int property = material.HasProperty(BaseColor) ? BaseColor : LegacyColor;
                if (!material.HasProperty(property)) continue;
                var original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original, slot);
                var tinted = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(tinted, slot);
                damageSurfaces.Add(new DamageSurface {
                    renderer = renderer, slot = slot, colorProperty = property,
                    originalColor = original.HasColor(property) ? original.GetColor(property) : material.GetColor(property),
                    original = original, tinted = tinted
                });
            }
        }
        ApplyDamageColor(1);
    }

    Vector3 DamageMovementStep(Vector3 desiredVelocity, float deltaTime)
    {
        if (recoilRemaining <= 0) return desiredVelocity * deltaTime;
        float duration = Mathf.Max(.05f, knockbackDuration);
        float dt = Mathf.Min(Mathf.Max(0, deltaTime), recoilRemaining);
        float control = 1 - Mathf.SmoothStep(0, 1, recoilRemaining / duration);
        // Integrate linear deceleration, independent of frame rate. CharacterController
        // receives this displacement together with gravity, so walls still block it.
        float distance = Mathf.Max(0, knockbackSpeed) / duration *
            (recoilRemaining * dt - .5f * dt * dt);
        recoilRemaining = Mathf.Max(0, recoilRemaining - deltaTime);
        return desiredVelocity * (control * dt + Mathf.Max(0, deltaTime - dt)) + recoilDirection * distance;
    }

    void LateUpdate()
    {
        if (flashRemaining <= 0 || (Game && (Game.IsPaused || Game.ShopOpen))) return;
        flashRemaining = Mathf.Max(0, flashRemaining - Time.deltaTime);
        if (flashRemaining <= 0) { RestoreDamageColors(); return; }
        // One clear flash with a smooth return; no rapid strobing.
        ApplyDamageColor(Mathf.Clamp01(flashRemaining / Mathf.Max(.1f, damageFlashDuration) * 1.6f));
    }

    void ApplyDamageColor(float weight)
    {
        foreach (var surface in damageSurfaces)
        {
            if (!surface.renderer) continue;
            Color color = Color.Lerp(surface.originalColor, damageTint, weight);
            color.a = surface.originalColor.a;
            surface.tinted.SetColor(surface.colorProperty, color);
            surface.renderer.SetPropertyBlock(surface.tinted, surface.slot);
        }
    }

    void RestoreDamageColors()
    {
        foreach (var surface in damageSurfaces)
            if (surface.renderer)
                surface.renderer.SetPropertyBlock(surface.original.isEmpty ? null : surface.original, surface.slot);
        damageSurfaces.Clear();
    }

    void ResetDamageReaction()
    {
        recoilRemaining = flashRemaining = 0;
        RestoreDamageColors();
    }

    void OnDisable() => ResetDamageReaction();
}
