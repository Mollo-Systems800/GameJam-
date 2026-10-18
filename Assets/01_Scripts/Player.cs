

using UnityEngine;

using UnityEngine.InputSystem;



[RequireComponent(typeof(CharacterController))]

public partial class Player : MonoBehaviour

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



    [Header("Voltereta")]

    [Min(0.1f)] public float rollDuration = 1.166667f;

    [Min(0)] public float rollSpeed = 6;

    [Min(0)] public float rollCooldown = 0.6f;



    public bool IsRolling => roll.Active;

    public float RollProgress => roll.Progress;

    public bool IsGrounded => controller && controller.isGrounded;



    public Transform visual;

    public Camera view;



    public bool CanMove { get; set; } = true;

    public MazeBetaGame Game { get; set; }



    // Sistema de doble salto recuperado

    public bool CanDoubleJump { get; set; }

    bool airJumpUsed;



    public float SpeedMultiplier { get; set; } = 1;

    public float CurrentSpeed { get; private set; }

    public float PlanarSpeed { get; private set; }

    public bool IsSprinting { get; private set; }

    public float VerticalVelocity => verticalSpeed;

    public int Collected { get; private set; }



    public float NextSlowdown =>

        Mathf.Max(0, slowdownEvery - fatigueTime);



    CharacterController controller;

    float verticalSpeed;

    float fatigueTime;

    float groundedGrace;

    float bufferedJump;



    readonly PlayerRollState roll = new PlayerRollState();

    Vector3 rollDirection;





    void Awake()

    {

        controller = GetComponent<CharacterController>();



        if (!view)

            view = Camera.main;



        if (visual)

        {

            var renderers = visual.GetComponentsInChildren<Renderer>();

            bool hasBody = false;

            Bounds bounds = default;



            foreach (var renderer in renderers)

            {

                if (PlayerLimbAnimator.IsControl(renderer.name))

                {

                    renderer.enabled = false;

                    continue;

                }



                if (!renderer.enabled) continue;



                if (!hasBody)

                {

                    bounds = renderer.bounds;

                    hasBody = true;

                }

                else

                {

                    bounds.Encapsulate(renderer.bounds);

                }

            }



            if (hasBody)

            {

                float scale = 1.8f /

                    Mathf.Max(0.001f, bounds.size.y);



                Vector3 offset = bounds.center - transform.position;



                visual.localScale *= scale;



                visual.localPosition = new Vector3(

                    -offset.x * scale,

                    -(bounds.min.y - transform.position.y) * scale,

                    -offset.z * scale

                );

            }



            foreach (var collider in

                visual.GetComponentsInChildren<Collider>())

                collider.enabled = false;



            foreach (var animator in

                visual.GetComponentsInChildren<Animator>())

                animator.applyRootMotion = false;

        }



        InitializeSpeed();

    }



    void Update()

    {

        if (!CanMove)

        {

            PlanarSpeed = 0;

            IsSprinting = false;

            return;

        }



        fatigueTime += Time.deltaTime;



        if (slowdownEvery > 0 && fatigueTime >= slowdownEvery)

        {

            fatigueTime %= slowdownEvery;



            CurrentSpeed = Mathf.Max(

                minimumSpeed,

                CurrentSpeed - slowdownAmount

            );

        }



        Vector2 input = Vector2.zero;

        var keyboard = Keyboard.current;



        if (keyboard != null)

        {

            input.x =

                (keyboard.dKey.isPressed ||

                 keyboard.rightArrowKey.isPressed ? 1 : 0)

                -

                (keyboard.aKey.isPressed ||

                 keyboard.leftArrowKey.isPressed ? 1 : 0);



            input.y =

                (keyboard.wKey.isPressed ||

                 keyboard.upArrowKey.isPressed ? 1 : 0)

                -

                (keyboard.sKey.isPressed ||

                 keyboard.downArrowKey.isPressed ? 1 : 0);

        }



        if (Gamepad.current != null &&

            Gamepad.current.leftStick.ReadValue().sqrMagnitude >

            input.sqrMagnitude)

        {

            input = Gamepad.current.leftStick.ReadValue();

        }



        input = Vector2.ClampMagnitude(input, 1);



        Vector3 forward = view

            ? Vector3.ProjectOnPlane(

                view.transform.forward,

                Vector3.up

              ).normalized

            : Vector3.forward;



        Vector3 right = Vector3.Cross(Vector3.up, forward);



        Vector3 direction =

            forward * input.y + right * input.x;



        bool sprint =

            (keyboard != null &&

             keyboard.leftShiftKey.isPressed)

            ||

            (Gamepad.current != null &&

             Gamepad.current.leftStickButton.isPressed);



        bool jumpPressed =

            (keyboard != null &&

             keyboard.spaceKey.wasPressedThisFrame)

            ||

            (Gamepad.current != null &&

             Gamepad.current.buttonSouth.wasPressedThisFrame);



        bool rollPressed =

            (keyboard != null &&

             keyboard.leftCtrlKey.wasPressedThisFrame)

            ||

            (keyboard != null &&

             (keyboard.rightCtrlKey.wasPressedThisFrame ||

              keyboard.cKey.wasPressedThisFrame))

            ||

            (Gamepad.current != null &&

             Gamepad.current.buttonEast.wasPressedThisFrame);



        if (IsRecoiling)
        {
            jumpPressed = rollPressed = false;
            sprint = false;
        }

        if (rollPressed)

            roll.Request();



        bool rollSupported =

            verticalSpeed <= 0 &&

            (controller.isGrounded || ProbeRollSupport());



        if (roll.Step(

            Time.deltaTime,

            rollSupported,

            rollDuration,

            rollCooldown))

        {

            rollDirection =

                direction.sqrMagnitude > 0.01f

                ? direction.normalized

                : forward;



            bufferedJump = 0;



            if (visual)

                visual.rotation =

                    Quaternion.LookRotation(rollDirection);

        }



        bufferedJump = jumpPressed

            ? jumpBuffer

            : bufferedJump - Time.deltaTime;



        if (IsRolling)

            bufferedJump = 0;



        groundedGrace = controller.isGrounded

            ? coyoteTime

            : groundedGrace - Time.deltaTime;



        if (controller.isGrounded && verticalSpeed < 0)

            verticalSpeed = -2;



        // Salto normal: conserva la restriccion de voltereta

        if (!IsRolling &&

            bufferedJump > 0 &&

            groundedGrace > 0 &&

            verticalSpeed <= 0 &&

            (!Game || !Game.RequiresRollNear(

                transform.position,

                controller.radius + 0.1f)))

        {

            verticalSpeed = Mathf.Sqrt(

                2 * Mathf.Abs(Physics.gravity.y) * jumpHeight

            );



            bufferedJump = 0;

            groundedGrace = 0;
            MazeAudioDirector.Play(MazeSound.Jump);

        }



        // Doble salto recuperado de la version antigua

        if (controller.isGrounded)

        {

            airJumpUsed = false;

        }

        else if (

            !IsRolling &&

            CanDoubleJump &&

            !airJumpUsed &&

            bufferedJump > 0 &&

            groundedGrace <= 0 &&

            (!Game || !Game.RequiresRollNear(

                transform.position,

                controller.radius + 0.1f)))

        {

            verticalSpeed = Mathf.Sqrt(

                2 * Mathf.Abs(Physics.gravity.y) * jumpHeight

            );



            bufferedJump = 0;

            airJumpUsed = true;
            MazeAudioDirector.Play(MazeSound.DoubleJump);

        }



        verticalSpeed += Physics.gravity.y * Time.deltaTime;



        Vector3 beforeMove = transform.position;



        Vector3 planarVelocity = IsRolling

            ? rollDirection * rollSpeed

            : direction * CurrentSpeed * SpeedMultiplier *

              (sprint ? sprintMultiplier : 1);



        // Se conserva la restriccion de movimiento actual

        Vector3 planarStep =

            DamageMovementStep(planarVelocity, Time.deltaTime);



        if (Game && !IsRolling)

        {

            planarStep = Game.RestrictWalking(

                transform.position,

                planarStep,

                controller.radius

            );

        }



        controller.Move(

            planarStep +

            Vector3.up * verticalSpeed * Time.deltaTime

        );



        PlanarSpeed =

            Vector3.ProjectOnPlane(

                transform.position - beforeMove,

                Vector3.up

            ).magnitude /

            Mathf.Max(0.0001f, Time.deltaTime);



        IsSprinting =

            sprint &&

            input.sqrMagnitude > 0.01f &&

            !IsRolling && !IsRecoiling &&

            PlanarSpeed > 0.1f;



        if (visual && !IsRolling)

        {

            if (view && view.GetComponent<PlayerFollowCamera>())

            {

                visual.rotation = Quaternion.Slerp(

                    visual.rotation,

                    Quaternion.Euler(

                        0,

                        view.transform.eulerAngles.y,

                        0

                    ),

                    14 * Time.deltaTime

                );

            }

            else if (direction.sqrMagnitude > 0.01f)

            {

                visual.rotation = Quaternion.Slerp(

                    visual.rotation,

                    Quaternion.LookRotation(direction),

                    14 * Time.deltaTime

                );

            }

        }

    }



    bool ProbeRollSupport()

    {

        float radius = controller.radius * 0.85f;



        Vector3 feet =

            transform.TransformPoint(controller.center)

            - Vector3.up * (controller.height * 0.5f);



        Vector3 origin =

            feet + Vector3.up * (radius + 0.05f);



        foreach (var hit in Physics.SphereCastAll(

            origin,

            radius,

            Vector3.down,

            0.14f,

            ~0,

            QueryTriggerInteraction.Ignore))

        {

            if (!hit.collider.transform.IsChildOf(transform)

                && hit.normal.y >= Mathf.Cos(

                    controller.slopeLimit * Mathf.Deg2Rad))

                return true;

        }



        return false;

    }



    public void CollectPills()

    {
        MazeAudioDirector.Play(MazeSound.Pills);

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

        IsSprinting = false;



        roll.Reset();

        ResetDamageReaction();



        // Reiniciar doble salto

        CanDoubleJump = false;

        airJumpUsed = false;

    }

}

