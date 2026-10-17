using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    public float baseSpeed = 4f;
    public float sprintMultiplier = 1.35f;
    public float minimumSpeed = 2.6f;
    public float slowdownEvery = 60f;
    public float slowdownAmount = 0.35f;
    [Header("Salto")]
    [Min(0.1f)] public float jumpHeight = 1.1f;
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;
    public bool IsGrounded => controller && controller.isGrounded;
    public Transform visual;
    public Camera view;
    public bool CanMove { get; set; } = true;
    public float SpeedMultiplier { get; set; } = 1;
    public float CurrentSpeed { get; private set; }
    public float PlanarSpeed { get; private set; }
    public float VerticalVelocity => verticalSpeed;
    public int Collected { get; private set; }
    public float NextSlowdown => Mathf.Max(0, slowdownEvery - fatigueTime);
    CharacterController controller;
    float verticalSpeed;
    float fatigueTime;
    float groundedGrace;
    float bufferedJump;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!view) view = Camera.main;
        if (visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            bool hasBody = false;
            Bounds bounds = default;
            foreach (var renderer in renderers)
            {
                if (PlayerLimbAnimator.IsControl(renderer.name)) { renderer.enabled = false; continue; }
                if (!renderer.enabled) continue;
                if (!hasBody) { bounds = renderer.bounds; hasBody = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (hasBody)
            {
                float scale = 1.8f / Mathf.Max(0.001f, bounds.size.y);
                Vector3 offset = bounds.center - transform.position;
                visual.localScale *= scale;
                visual.localPosition = new Vector3(-offset.x * scale,
                    -(bounds.min.y - transform.position.y) * scale, -offset.z * scale);
            }
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var animator in visual.GetComponentsInChildren<Animator>()) animator.applyRootMotion = false;
        }
        InitializeSpeed();
    }

    void Update()
    {
        if (!CanMove) { PlanarSpeed = 0; return; }
        fatigueTime += Time.deltaTime;
        if (slowdownEvery > 0 && fatigueTime >= slowdownEvery)
        {
            fatigueTime %= slowdownEvery;
            CurrentSpeed = Mathf.Max(minimumSpeed, CurrentSpeed - slowdownAmount);
        }
        Vector2 input = Vector2.zero;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
        }
        if (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > input.sqrMagnitude)
            input = Gamepad.current.leftStick.ReadValue();
        input = Vector2.ClampMagnitude(input, 1);
        Vector3 forward = view ? Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = forward * input.y + right * input.x;
        bool sprint = (keyboard != null && keyboard.leftShiftKey.isPressed)
                   || (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);
        bool jumpPressed = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        bufferedJump = jumpPressed ? jumpBuffer : bufferedJump - Time.deltaTime;
        groundedGrace = controller.isGrounded ? coyoteTime : groundedGrace - Time.deltaTime;
        if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
        if (bufferedJump > 0 && groundedGrace > 0 && verticalSpeed <= 0)
        {
            verticalSpeed = Mathf.Sqrt(2 * Mathf.Abs(Physics.gravity.y) * jumpHeight);
            bufferedJump = 0;
            groundedGrace = 0;
        }
        verticalSpeed += Physics.gravity.y * Time.deltaTime;
        Vector3 beforeMove = transform.position;
        controller.Move((direction * CurrentSpeed * SpeedMultiplier * (sprint ? sprintMultiplier : 1)
                       + Vector3.up * verticalSpeed) * Time.deltaTime);
        PlanarSpeed = Vector3.ProjectOnPlane(transform.position - beforeMove, Vector3.up).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
        if (visual && direction.sqrMagnitude > 0.01f)
            visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(direction), 14 * Time.deltaTime);
    }

    public void CollectPills()
    {
        CurrentSpeed = baseSpeed;
        fatigueTime = 0;
        Collected++;
    }

    public void InitializeSpeed()
    {
        CurrentSpeed = baseSpeed;
        fatigueTime = 0;
        Collected = 0;
        verticalSpeed = 0;
        groundedGrace = bufferedJump = 0;
        SpeedMultiplier = 1;
        PlanarSpeed = 0;
    }
}
