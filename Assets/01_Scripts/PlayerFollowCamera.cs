using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Camera))]
public sealed class PlayerFollowCamera : MonoBehaviour
{
    public float targetHeight = 1.3f;
    public float followDistance = 2.6f;
    public float collisionRadius = 0.18f;
    public float mouseSensitivity = 0.10f;
    public float gamepadLookSpeed = 140;
    public float minimumPitch = -10;
    public float maximumPitch = 60;
    [Range(50, 100)] public float fieldOfView = 65;
    public Player Target { get; private set; }
    MazeBetaGame game;
    float yaw, pitch;
    float currentDistance;
    readonly Dictionary<Renderer, ShadowCastingMode> hiddenRenderers = new Dictionary<Renderer, ShadowCastingMode>();

    public void Initialize(MazeBetaGame owner, Player player)
    {
        RestoreBody();
        game = owner;
        Target = player;
        yaw = player.transform.eulerAngles.y;
        pitch = 18;
        currentDistance = followDistance;
        var camera = GetComponent<Camera>();
        camera.fieldOfView = fieldOfView;
        camera.nearClipPlane = 0.04f;
        camera.allowDynamicResolution = false;
        var additional = camera.GetUniversalAdditionalCameraData();
        additional.renderPostProcessing = true;
        additional.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        additional.antialiasingQuality = AntialiasingQuality.High;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        PlaceCamera(true);
    }

    void Update()
    {
        bool active = game && game.IsPlaying && Target && Application.isFocused;
        SetCursor(active);
        if (!active) return;
        Vector2 look = Mouse.current != null ? Mouse.current.delta.ReadValue() * mouseSensitivity : Vector2.zero;
        if (Gamepad.current != null) look += Gamepad.current.rightStick.ReadValue() * (gamepadLookSpeed * Time.deltaTime);
        yaw = Mathf.Repeat(yaw + look.x, 360);
        pitch = Mathf.Clamp(pitch - look.y, minimumPitch, maximumPitch);
        // Update before Player so walking uses the current horizontal viewing direction.
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);
    }

    void LateUpdate()
    {
        if (!Target) return;
        PlaceCamera(false);
    }

    void PlaceCamera(bool snap)
    {
        Vector3 pivot = Target.transform.position + Vector3.up * targetHeight;
        Vector3 backward = -(Quaternion.Euler(pitch, yaw, 0) * Vector3.forward);
        float available = Mathf.Max(0.2f, followDistance);
        foreach (var hit in Physics.SphereCastAll(pivot, collisionRadius, backward, available, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(Target.transform)) continue;
            available = Mathf.Min(available, Mathf.Max(0.05f, hit.distance - 0.08f));
        }
        // Retract immediately at a wall, then ease back out once the path is clear.
        currentDistance = snap || available < currentDistance ? available
            : Mathf.Lerp(currentDistance, available, 1 - Mathf.Exp(-8 * Time.deltaTime));
        transform.SetPositionAndRotation(pivot + backward * currentDistance, Quaternion.Euler(pitch, yaw, 0));
        SetBodyVisible(currentDistance > 0.55f);
    }

    void SetBodyVisible(bool visible)
    {
        if (Target.visual)
            foreach (var renderer in Target.visual.GetComponentsInChildren<Renderer>())
            {
                if (!hiddenRenderers.ContainsKey(renderer)) hiddenRenderers.Add(renderer, renderer.shadowCastingMode);
                renderer.shadowCastingMode = visible ? hiddenRenderers[renderer] : ShadowCastingMode.ShadowsOnly;
            }
    }

    void RestoreBody()
    {
        foreach (var entry in hiddenRenderers) if (entry.Key) entry.Key.shadowCastingMode = entry.Value;
        hiddenRenderers.Clear();
    }

    static void SetCursor(bool locked)
    {
        CursorLockMode mode = locked ? CursorLockMode.Locked : CursorLockMode.None;
        if (Cursor.lockState != mode) Cursor.lockState = mode;
        Cursor.visible = !locked;
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) SetCursor(false);
    }

    void OnDisable()
    {
        SetCursor(false);
        RestoreBody();
    }

    void OnGUI()
    {
        if (game && game.IsPlaying) GUI.DrawTexture(new Rect(Screen.width * 0.5f - 1, Screen.height * 0.5f - 1, 3, 3), Texture2D.whiteTexture);
    }
}
