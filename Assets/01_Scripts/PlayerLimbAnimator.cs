using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Player))]
public sealed class PlayerLimbAnimator : MonoBehaviour
{
    [Min(0.2f)] public float strideLength = 1.8f;
    [Range(0, 45)] public float legSwing = 28;
    [Range(0, 45)] public float armSwing = 24;
    [Min(1)] public float blendSpeed = 12;
    [Header("Idle de respiracion")]
    public TextAsset idleMotion;
    public TextAsset walkMotion;
    public TextAsset runMotion;
    public TextAsset jumpMotion;
    public TextAsset rollMotion;
    [Min(0.1f)] public float runReferenceSpeed = 5.4f;
    [Range(0, 2)] public float idleStrength = 1;
    [Range(0, 1)] public float motionStrength = 0.65f;
    [Range(0, 1)] public float sprintMotionStrength = 1;
    [Range(0, 1)] public float jumpMotionStrength = 0.95f;
    [Range(0, 1)] public float walkMotionStrength = 0.85f;
    [Min(0.5f)] public float walkCycleDistance = 3.2f;
    [Header("Postura y balanceo del torso")]
    [Range(0, 14)] public float torsoTwist = 6;
    [Range(0, 8)] public float runningLean = 1;
    [Range(0, 8)] public float maxGroundedTorsoTilt = 4;
    [Tooltip("Corrige la pose encorvada de la malla original antes de animarla. Reinicia Play al cambiarlo.")]
    [Range(0, 1)] public float restPostureStrength = 1;
    [Range(0, 0.01f)] public float hipSway = 0.004f;
    [Header("Movimiento natural")]
    [Range(0, 2)] public float breathingStrength = 1;
    [Range(0, 2)] public float secondaryMotion = 1;
    [Range(0.05f, 0.4f)] public float actionBlendTime = 0.16f;
    public bool HasIdleMotion => idleData != null;
    public bool HasRunMotion => runData != null;
    public bool HasWalkMotion => walkData != null;
    public float WalkAnimationWeight => stride * (1 - sprintWeight) * (1 - airWeight) * (player && !player.IsRolling ? 1 : 0);
    public float RunAnimationWeight => sprintWeight * (1 - airWeight) * (player && !player.IsRolling ? 1 : 0);
    public bool HasJumpMotion => jumpData != null;
    public float JumpAnimationWeight => Mathf.Max(airWeight, landing * 0.65f) * (1 - rollWeight);
    public bool HasRollMotion => rollData != null;
    public bool IsReady { get; private set; }
    readonly List<Mesh> ownedMeshes = new List<Mesh>();
    readonly List<MeshRenderer> originalRenderers = new List<MeshRenderer>();
    readonly List<GameObject> generatedRenderers = new List<GameObject>();
    readonly List<bool> originalStates = new List<bool>();
    Transform[] bones;
    Transform rig;
    Player player;
    Vector3 hipsRest;
    float phase, stride, landing;
    float characterHeight;
    bool wasGrounded;
    float idleTime, idleWeight;
    MotionData outcomeData;
    float outcomeStarted;
    public bool HasOutcomeMotion => outcomeData != null;
    public void PlayOutcomeMotion(bool victory, bool finalDefeat)
    {
        outcomeData = victory || finalDefeat ? LoadMotion(Resources.Load<TextAsset>(victory ? "VictoryMotion" : "WritingMotion")) : null;
        outcomeStarted = Time.unscaledTime;
    }
    public void StopOutcomeMotion() { outcomeData = null; }
    MotionData idleData;
    MotionData runData, jumpData;
    float runTime, jumpSampleTime;
    bool airborne;
    float sprintWeight;
    MotionData rollData;
    MotionData walkData;
    float locomotionPhase, walkTime;
    Vector3 walkingDirection = Vector3.forward;
    Bounds restBounds;
    Vector3 chestRest, previousPosition, previousVelocity;
    float lifeTime, breathPhase, breath, exertion, airWeight, unsupportedTime;
    float accelerationLean, turnLean, previousYaw;
    PlayerRestPosture restPosture;
    readonly List<Vector3> supportPoints = new List<Vector3>();
    readonly List<BoneWeight> supportWeights = new List<BoneWeight>();
    Matrix4x4[] skinBindposes, supportMatrices;
    Vector3[] posedSupport;
    float rollWeight;
    public float RollAnimationWeight => rollWeight;

    [Serializable] sealed class MotionData
    {
        public float duration;
        public float sampleRate;
        public bool loop;
        public bool standingPose;
        public float takeoff, apex = 0.5f, landing = 1;
        public MotionTrack[] tracks;
        public float[] rollDegrees;
    }
    [Serializable] sealed class MotionTrack { public int bone; public Quaternion[] rotations; }

    void Start()
    {
        player = GetComponent<Player>();
        if (!player.visual) return;
        var filters = player.visual.GetComponentsInChildren<MeshFilter>();
        var usable = new List<MeshFilter>();
        Bounds body = default;
        bool hasBounds = false;
        foreach (var filter in filters)
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (!renderer || !renderer.enabled || !filter.sharedMesh || IsControl(filter.name)) continue;
            if (!filter.sharedMesh.isReadable)
            {
                Debug.LogWarning("Activa Read/Write en " + filter.sharedMesh.name + " para animar las extremidades.", this);
                continue;
            }
            usable.Add(filter);
            foreach (var vertex in filter.sharedMesh.vertices)
            {
                Vector3 point = player.visual.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                if (!hasBounds) { body = new Bounds(point, Vector3.zero); hasBounds = true; }
                else body.Encapsulate(point);
            }
        }
        if (!hasBounds || body.size.y < 0.0001f) return;
        characterHeight = body.size.y;
        restPosture = new PlayerRestPosture(usable, player.visual, body, restPostureStrength);
        restBounds = new Bounds();
        rig = new GameObject("Procedural Limb Rig").transform;
        rig.SetParent(player.visual, false);
        bones = new Transform[Enum.GetValues(typeof(PlayerRigBone)).Length];
        Vector3 centre = new Vector3(body.center.x, body.min.y, restPosture.CentreZ);
        Bone(PlayerRigBone.Hips, rig, centre, 0, 0.49f);
        Bone(PlayerRigBone.Spine, bones[0], centre, 0, 0.58f);
        Bone(PlayerRigBone.Chest, bones[(int)PlayerRigBone.Spine], centre, 0, 0.72f);
        Bone(PlayerRigBone.LeftThigh, bones[0], centre, -0.10f, 0.48f);
        Bone(PlayerRigBone.LeftShin, bones[2], centre, -0.10f, 0.26f);
        Bone(PlayerRigBone.RightThigh, bones[0], centre, 0.10f, 0.48f);
        Bone(PlayerRigBone.RightShin, bones[4], centre, 0.10f, 0.26f);
        Bone(PlayerRigBone.LeftArm, bones[1], centre, -0.115f, 0.73f);
        Bone(PlayerRigBone.LeftForearm, bones[6], centre, -0.18f, 0.60f);
        Bone(PlayerRigBone.RightArm, bones[1], centre, 0.115f, 0.73f);
        Bone(PlayerRigBone.RightForearm, bones[8], centre, 0.18f, 0.60f);
        hipsRest = bones[0].localPosition;
        chestRest = bones[1].localPosition;
        previousPosition = player.transform.position;
        previousYaw = player.visual.eulerAngles.y;
        foreach (var filter in usable) Skin(filter, body);
        supportMatrices = new Matrix4x4[bones.Length];
        posedSupport = new Vector3[supportPoints.Count];
        // Keep the corrected pelvis over the controller rather than retaining
        // the old bounds centre, which included the forward head and hands.
        player.visual.localPosition += player.transform.InverseTransformVector(
            player.visual.TransformVector(Vector3.forward * (body.center.z - restPosture.CentreZ)));
        wasGrounded = player.IsGrounded;
        IsReady = ownedMeshes.Count > 0;
        idleData = LoadMotion(idleMotion);
        walkData = LoadMotion(walkMotion);
        runData = LoadMotion(runMotion);
        jumpData = LoadMotion(jumpMotion);
        rollData = LoadMotion(rollMotion);
        if (rollData != null) player.rollDuration = rollData.duration;
    }

    static MotionData LoadMotion(TextAsset asset)
    {
        if (!asset) return null;
        var data = JsonUtility.FromJson<MotionData>(asset.text);
        return data != null && data.duration > 0 && data.sampleRate > 0 && data.tracks != null ? data : null;
    }

    public static bool IsControl(string objectName) => objectName.StartsWith("cs_", StringComparison.OrdinalIgnoreCase);

    void Bone(PlayerRigBone id, Transform parent, Vector3 origin, float x, float y)
    {
        var bone = new GameObject(id.ToString()).transform;
        bone.SetParent(parent, false);
        float forward = id == PlayerRigBone.LeftForearm || id == PlayerRigBone.RightForearm
            ? Mathf.Tan(4 * Mathf.Deg2Rad) * 0.17f * restPostureStrength : 0;
        bone.position = player.visual.TransformPoint(origin + new Vector3(x, y, forward) * characterHeight);
        bone.rotation = player.visual.rotation;
        bones[(int)id] = bone;
    }

    void Skin(MeshFilter filter, Bounds bounds)
    {
        if (PlayerRestPosture.IsDuplicateAccessory(filter.name))
        { filter.GetComponent<MeshRenderer>().enabled = false; return; }
        Mesh mesh = Instantiate(filter.sharedMesh);
        mesh.name = filter.sharedMesh.name + " - Animated Instance";
        var weights = new BoneWeight[mesh.vertexCount];
        Vector3[] vertices = mesh.vertices;
        var sourcePoints = new Vector3[vertices.Length];
        Matrix4x4 toCharacter = player.visual.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        Matrix4x4 normalMatrix = toCharacter.inverse.transpose;
        for (int i = 0; i < vertices.Length; i++) sourcePoints[i] = toCharacter.MultiplyPoint3x4(vertices[i]);
        var armRegions = filter.name == "Cube.001" || filter.name == "Cube.007"
            ? PlayerMeshAnatomy.ArmRegions(sourcePoints, mesh.triangles, bounds) : null;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 point = sourcePoints[i];
            float anatomicalArm = armRegions == null ? -1 : armRegions[i];
            vertices[i] = restPosture.Point(point, filter.name, anatomicalArm);
            var influence = PlayerRigWeights.SampleMesh(filter.name, (point.x - bounds.center.x) / bounds.size.y,
                (point.y - bounds.min.y) / bounds.size.y, restPosture.ArmForward(point), anatomicalArm);
            weights[i] = new BoneWeight { boneIndex0 = influence.A, boneIndex1 = influence.B, boneIndex2 = influence.C, boneIndex3 = influence.D,
                weight0 = influence.WA, weight1 = influence.WB, weight2 = influence.WC, weight3 = influence.WD };
        }
        // Bake imported negative scales into a common space before skinning.
        // Keeping renderers under mirrored FBX transforms can invert shading and offsets.
        mesh.vertices = vertices;
        Vector3[] normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++)
            normals[i] = restPosture.Normal(sourcePoints[i],
                normalMatrix.MultiplyVector(normals[i]).normalized, filter.name, armRegions == null ? -1 : armRegions[i]);
        mesh.normals = normals;
        Vector4[] tangents = mesh.tangents;
        float sign = toCharacter.determinant < 0 ? -1 : 1;
        for (int i = 0; i < tangents.Length; i++)
        {
            Vector3 tangent = toCharacter.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
            tangent = restPosture.Tangent(sourcePoints[i], tangent, filter.name, armRegions == null ? -1 : armRegions[i]);
            tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w * sign);
        }
        mesh.tangents = tangents;
        if (sign < 0)
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] triangles = mesh.GetTriangles(submesh);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int index = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = index;
                }
                mesh.SetTriangles(triangles, submesh, false);
            }
        mesh.RecalculateBounds();
        if (ownedMeshes.Count == 0) restBounds = mesh.bounds;
        else restBounds.Encapsulate(mesh.bounds);
        var item = new GameObject("Animated " + filter.name);
        item.transform.SetParent(player.visual, false);
        var skin = item.AddComponent<SkinnedMeshRenderer>();
        var bindposes = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * item.transform.localToWorldMatrix;
        mesh.bindposes = bindposes;
        mesh.boneWeights = weights;
        skinBindposes = bindposes;
        supportPoints.AddRange(vertices);
        supportWeights.AddRange(weights);
        skin.sharedMesh = mesh;
        skin.bones = bones;
        skin.rootBone = rig;
        skin.updateWhenOffscreen = true;
        skin.quality = SkinQuality.Bone4;
        var original = filter.GetComponent<MeshRenderer>();
        skin.sharedMaterials = original.sharedMaterials;
        skin.shadowCastingMode = original.shadowCastingMode;
        skin.receiveShadows = original.receiveShadows;
        skin.localBounds = mesh.bounds;
        originalStates.Add(original.enabled);
        originalRenderers.Add(original);
        original.enabled = false;
        ownedMeshes.Add(mesh);
        generatedRenderers.Add(item);
    }

    public bool CinematicIdle { get; set; }
    public PlayerStoryPose StoryPose { get; set; }
    public float StoryPoseWeight { get; set; }
    void LateUpdate()
    {
        if (!IsReady || (!player.CanMove && !CinematicIdle)) return;
        float dt = Time.deltaTime;
        lifeTime += dt;
        unsupportedTime = CinematicIdle || player.IsGrounded ? 0 : unsupportedTime + dt;
        bool grounded = CinematicIdle || player.IsGrounded || (player.VerticalVelocity <= 0 && unsupportedTime < 0.1f);
        float target = !CinematicIdle && grounded ? Mathf.Clamp01(player.PlanarSpeed / Mathf.Max(0.1f, player.baseSpeed)) : 0;
        float blend = 1 - Mathf.Exp(-blendSpeed * dt);
        float rollProgress = player.RollProgress;
        float rollTarget = !CinematicIdle && player.IsRolling
            ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 0.18f, rollProgress))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.78f, 1, rollProgress))) : 0;
        rollWeight = Mathf.Lerp(rollWeight, rollTarget, 1 - Mathf.Exp(-dt * 30));
        airWeight = Mathf.Lerp(airWeight, grounded ? 0 : 1, 1 - Mathf.Exp(-dt / Mathf.Max(0.05f, actionBlendTime)));
        stride = Mathf.Lerp(stride, target, blend);
        // Use the controller's sprint action, not an absolute speed threshold:
        // fatigue can make sprinting slower than the original walking speed.
        float sprintTarget = !CinematicIdle && player.IsSprinting ? 1 : 0;
        sprintWeight = Mathf.Lerp(sprintWeight, sprintTarget, blend);
        idleWeight = grounded ? (1 - stride) * idleStrength : 0;
        airborne = !grounded;
        // Breathing never stops at takeoff, at an action boundary or when speed reaches zero.
        idleTime += dt;
        exertion = Mathf.Lerp(exertion, Mathf.Lerp(stride * 0.4f, 1, sprintWeight), 1 - Mathf.Exp(-dt * 0.8f));
        breathPhase = Mathf.Repeat(breathPhase + dt * Mathf.Lerp(0.27f, 0.48f, exertion) * Mathf.PI * 2, Mathf.PI * 2);
        breath = Mathf.Sin(breathPhase) * breathingStrength * Mathf.Lerp(1, 1.3f, exertion);
        Vector3 velocity = (player.transform.position - previousPosition) / Mathf.Max(0.0001f, dt);
        float acceleration = Vector3.Dot((velocity - previousVelocity) / Mathf.Max(0.0001f, dt), player.visual.forward);
        float yawSpeed = Mathf.DeltaAngle(previousYaw, player.visual.eulerAngles.y) / Mathf.Max(0.0001f, dt);
        float response = 1 - Mathf.Exp(-dt * 6);
        accelerationLean = Mathf.Lerp(accelerationLean, Mathf.Clamp(acceleration * 0.08f, -2.5f, 2.5f), response);
        turnLean = Mathf.Lerp(turnLean, Mathf.Clamp(yawSpeed * 0.025f, -4, 4) * stride, response);
        previousPosition = player.transform.position;
        previousVelocity = velocity;
        previousYaw = player.visual.eulerAngles.y;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        if (planarVelocity.sqrMagnitude > 0.01f)
            walkingDirection = Vector3.Lerp(walkingDirection,
                player.visual.InverseTransformDirection(planarVelocity.normalized), blend);
        if (grounded && !player.IsRolling)
        {
            float walkFrequency = player.PlanarSpeed / Mathf.Max(0.5f, walkCycleDistance);
            float runFrequency = runData != null ? player.PlanarSpeed / Mathf.Max(0.1f, runReferenceSpeed) / runData.duration : walkFrequency * 1.4f;
            locomotionPhase = Mathf.Repeat(locomotionPhase + dt * Mathf.Lerp(walkFrequency, runFrequency, sprintWeight), 1);
            runTime = locomotionPhase * (runData != null ? runData.duration : 1);
            walkTime = locomotionPhase * (walkData != null ? walkData.duration : 1);
        }
        if (jumpData != null && !grounded)
        {
            float launchSpeed = Mathf.Sqrt(2 * Mathf.Abs(Physics.gravity.y) * player.jumpHeight);
            float normalized = player.VerticalVelocity > 0
                ? Mathf.Lerp(jumpData.takeoff, jumpData.apex, Mathf.Clamp01(1 - player.VerticalVelocity / Mathf.Max(0.1f, launchSpeed)))
                : Mathf.Lerp(jumpData.apex, jumpData.landing, Mathf.Clamp01(-player.VerticalVelocity / Mathf.Max(0.1f, launchSpeed)));
            jumpSampleTime = normalized * jumpData.duration;
        }
        if (grounded && !wasGrounded) landing = 1;
        landing = Mathf.Max(0, landing - dt * 3.5f);
        if (grounded) phase = Mathf.Repeat(phase + player.PlanarSpeed / Mathf.Max(0.2f, strideLength) * Mathf.PI * 2 * dt, Mathf.PI * 2);
        // Match the imported leg cycle so shoulders counter the same footfalls.
        float gaitPhase = runData != null ? runTime / runData.duration * Mathf.PI * 2 : phase;
        float cycle = Mathf.Sin(gaitPhase);
        bool rising = player.VerticalVelocity > 0;
        float gaitSwing = legSwing * Mathf.Lerp(0.65f, 1.4f, sprintWeight);
        float kneeSwing = Mathf.Lerp(24, 65, sprintWeight);
        float thighL = grounded ? cycle * gaitSwing * stride : rising ? -25 : 10;
        float thighR = grounded ? -cycle * gaitSwing * stride : rising ? -38 : -8;
        float kneeL = grounded ? Mathf.Max(0, -cycle) * kneeSwing * stride : rising ? 55 : 20;
        float kneeR = grounded ? Mathf.Max(0, cycle) * kneeSwing * stride : rising ? 70 : 28;
        Pose(PlayerRigBone.LeftThigh, thighL, 0, blend);
        Pose(PlayerRigBone.Hips, 0, 0, blend);
        Pose(PlayerRigBone.Spine, grounded ? 0 : rising ? 4 : -2, 0, blend);
        Pose(PlayerRigBone.RightThigh, thighR, 0, blend);
        Pose(PlayerRigBone.LeftShin, kneeL, 0, blend);
        Pose(PlayerRigBone.RightShin, kneeR, 0, blend);
        float armAmplitude = armSwing * Mathf.Lerp(0.6f, 1.6f, sprintWeight);
        Pose(PlayerRigBone.LeftArm, grounded ? -cycle * armAmplitude * stride : -38, grounded ? -2 : -16, blend);
        Pose(PlayerRigBone.RightArm, grounded ? cycle * armAmplitude * stride : -38, grounded ? 2 : 16, blend);
        Pose(PlayerRigBone.LeftForearm, grounded ? -12 * stride : -32, 0, blend);
        Pose(PlayerRigBone.RightForearm, grounded ? -12 * stride : -32, 0, blend);
        Pose(PlayerRigBone.Chest, grounded ? 3 * stride : rising ? 8 : -4, grounded ? cycle * 2 * stride : 0, blend);
        float breathing = breath * 0.0015f;
        float bob = grounded ? (1 - Mathf.Cos(gaitPhase * 2)) * 0.006f
            * Mathf.Lerp(1, 1.8f, sprintWeight) * stride - landing * 0.018f + breathing : breathing * 0.3f;
        float sway = grounded ? cycle * hipSway * stride
            + Mathf.Sin(lifeTime * 0.73f) * 0.0015f * (1 - stride) * secondaryMotion : 0;
        if (player.IsRolling) { bob = 0; sway = 0; }
        bones[0].localPosition = Vector3.Lerp(bones[0].localPosition,
            hipsRest + new Vector3(sway, bob, 0) * characterHeight, blend);
        bones[1].localPosition = Vector3.Lerp(bones[1].localPosition,
            chestRest + Vector3.up * breath * characterHeight * (player.IsRolling ? 0.0002f : 0.0012f), blend);
        ApplyRootRoll(blend);
        if(CinematicIdle)
        {
            // Suspend an interrupted roll and keep feet on the floor during story gestures.
            rig.localRotation=Quaternion.identity;
            rig.localPosition=Vector3.up*(StoryPose==PlayerStoryPose.Exhausted?-.12f*StoryPoseWeight:0);
        }
        wasGrounded = grounded;
    }

    void ApplyRootRoll(float blend)
    {
        if (!player.IsRolling || rollData == null || rollData.rollDegrees == null || rollData.rollDegrees.Length < 2)
        {
            rig.localRotation = Quaternion.Slerp(rig.localRotation, Quaternion.identity, blend);
            rig.localPosition = Vector3.Lerp(rig.localPosition, Vector3.zero, blend);
            return;
        }
        // Crouch first, complete the turn, then stand. Keep the source FBX's
        // unwrapped turn timing inside that action window.
        float rotationProgress = Mathf.InverseLerp(0.12f, 0.88f, player.RollProgress);
        float frame = rotationProgress * (rollData.rollDegrees.Length - 1);
        int first = Mathf.FloorToInt(frame);
        float degrees = Mathf.Lerp(rollData.rollDegrees[first],
            rollData.rollDegrees[Mathf.Min(first + 1, rollData.rollDegrees.Length - 1)], frame - first);
        Quaternion rotation = Quaternion.AngleAxis(degrees, Vector3.right);
        // Evaluate the actual posed skin in visual space, with the previous root
        // transform removed. Standing bounds cannot support a tucked body correctly.
        rig.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        for (int i = 0; i < bones.Length; i++)
            supportMatrices[i] = player.visual.worldToLocalMatrix * bones[i].localToWorldMatrix * skinBindposes[i];
        Bounds posed = default;
        for (int i = 0; i < supportPoints.Count; i++)
        {
            BoneWeight w = supportWeights[i];
            Vector3 p = supportPoints[i];
            Vector3 point = supportMatrices[w.boneIndex0].MultiplyPoint3x4(p) * w.weight0;
            if (w.weight1 > 0) point += supportMatrices[w.boneIndex1].MultiplyPoint3x4(p) * w.weight1;
            if (w.weight2 > 0) point += supportMatrices[w.boneIndex2].MultiplyPoint3x4(p) * w.weight2;
            if (w.weight3 > 0) point += supportMatrices[w.boneIndex3].MultiplyPoint3x4(p) * w.weight3;
            posedSupport[i] = point;
            if (i == 0) posed = new Bounds(point, Vector3.zero);
            else posed.Encapsulate(point);
        }
        Vector3 pivot = posed.center;
        Vector3 position = pivot - rotation * pivot;
        float bottom = float.PositiveInfinity;
        foreach (var point in posedSupport) bottom = Mathf.Min(bottom, (rotation * point + position).y);
        position.y += restBounds.min.y - bottom;
        rig.localRotation = rotation;
        rig.localPosition = position;
    }

    void Pose(PlayerRigBone bone, float pitch, float roll, float blend)
    {
        Transform joint = bones[(int)bone];
        Quaternion sampledIdle = SampleMotion(idleData, (int)bone, idleTime);
        Quaternion idle = Quaternion.SlerpUnclamped(Quaternion.identity, sampledIdle, idleStrength);
        Quaternion walk = walkData != null ? SampleWalking(bone) : Quaternion.Euler(pitch, 0, roll);
        Quaternion target = Quaternion.Slerp(idle, walk, stride);
        if (runData != null && sprintWeight > 0)
        {
            Quaternion run = SampleMotion(runData, (int)bone, runTime);
            if (bone == PlayerRigBone.LeftArm || bone == PlayerRigBone.RightArm)
            {
                Vector3 euler = run.eulerAngles;
                run = Quaternion.Euler(Mathf.DeltaAngle(0, euler.x),
                    Mathf.Clamp(Mathf.DeltaAngle(0, euler.y), -12, 12),
                    Mathf.Clamp(Mathf.DeltaAngle(0, euler.z), -15, 15));
            }
            target = Quaternion.Slerp(target, run, sprintWeight);
        }
        bool torsoBone = bone == PlayerRigBone.Hips || bone == PlayerRigBone.Spine || bone == PlayerRigBone.Chest;
        if (torsoBone)
        {
            // Keep the same standing alignment in idle, walk and run. Only a small
            // distributed lean is allowed; imported idle flexion must not become rest posture.
            float gaitPhase = runData != null ? runTime / runData.duration * Mathf.PI * 2 : phase;
            float cycle = Mathf.Sin(gaitPhase);
            float effort = Mathf.Lerp(0.65f, 1, sprintWeight);
            Quaternion gait;
            if (bone == PlayerRigBone.Hips)
                gait = Quaternion.Euler(0, -cycle * torsoTwist * 0.45f * effort, cycle * 1.5f);
            else if (bone == PlayerRigBone.Spine)
                gait = Quaternion.Euler(runningLean * 0.45f * sprintWeight + accelerationLean * 0.2f,
                    cycle * torsoTwist * 0.25f * effort, -turnLean * 0.5f);
            else
                gait = Quaternion.Euler(runningLean * 0.55f * sprintWeight + accelerationLean * 0.3f,
                    cycle * torsoTwist * 0.75f * effort, -cycle * 2.5f - turnLean * 0.5f);
            if (walkData != null) gait = Quaternion.Slerp(walk, gait, sprintWeight);
            if (runData != null && runData.standingPose)
                gait = Quaternion.Slerp(gait, SampleMotion(runData, (int)bone, runTime), sprintWeight * 0.6f);
            Quaternion uprightIdle = LimitTilt(idle, 0.35f);
            target = Quaternion.Slerp(uprightIdle, gait, stride);
        }
        else if (walkData == null && (bone == PlayerRigBone.LeftArm || bone == PlayerRigBone.RightArm))
        {
            // Preserve a readable arm swing even when retargeted clip rotations are small.
            target = Quaternion.Slerp(target, Quaternion.Euler(pitch, 0, roll), stride * 0.3f * (1 - sprintWeight));
        }
        bool jumpTrack = jumpData != null && (bone != PlayerRigBone.Spine || jumpData.standingPose);
        Quaternion airPose = jumpTrack
            ? SampleMotion(jumpData, (int)bone, jumpSampleTime) : Quaternion.Euler(pitch, 0, roll);
        target = Quaternion.Slerp(target, airPose, airWeight);
        if (!airborne && jumpTrack && landing > 0)
        {
            float tailTime = Mathf.Lerp(jumpData.landing, 1, 1 - landing) * jumpData.duration;
            target = Quaternion.Slerp(target, SampleMotion(jumpData, (int)bone, tailTime), landing * 0.65f);
        }
        if (rollWeight > 0.001f && rollData != null)
        {
            target = Quaternion.Slerp(target, TuckedRollPose(bone), rollWeight);
        }
        float limit = bone == PlayerRigBone.Hips ? 7 : bone == PlayerRigBone.Spine ? 7 : bone == PlayerRigBone.Chest ? 14
            : bone == PlayerRigBone.LeftShin || bone == PlayerRigBone.RightShin ? 42 : 28;
        float running = RunAnimationWeight;
        if (bone == PlayerRigBone.LeftThigh || bone == PlayerRigBone.RightThigh) limit = Mathf.Lerp(limit, 70, running);
        else if (bone == PlayerRigBone.LeftShin || bone == PlayerRigBone.RightShin) limit = Mathf.Lerp(limit, 115, running);
        else if (bone == PlayerRigBone.LeftArm || bone == PlayerRigBone.RightArm) limit = Mathf.Lerp(limit, 75, running);
        float jumping = JumpAnimationWeight;
        float jumpLimit = bone == PlayerRigBone.LeftThigh || bone == PlayerRigBone.RightThigh ? 85
            : bone == PlayerRigBone.LeftShin || bone == PlayerRigBone.RightShin ? 115
            : bone == PlayerRigBone.LeftArm || bone == PlayerRigBone.RightArm ? 85
            : bone == PlayerRigBone.Spine ? 10 : bone == PlayerRigBone.Chest ? 20 : limit;
        limit = Mathf.Lerp(limit, jumpLimit, jumping);
        float rollLimit = bone == PlayerRigBone.LeftThigh || bone == PlayerRigBone.RightThigh ? 105
            : bone == PlayerRigBone.LeftShin || bone == PlayerRigBone.RightShin ? 125
            : bone == PlayerRigBone.LeftArm || bone == PlayerRigBone.RightArm ? 80
            : bone == PlayerRigBone.Spine || bone == PlayerRigBone.Chest ? 32 : limit;
        limit = Mathf.Lerp(limit, rollLimit, rollWeight);
        target = Quaternion.RotateTowards(Quaternion.identity, target, limit);
        float strength = Mathf.Lerp(motionStrength, walkMotionStrength, WalkAnimationWeight);
        strength = Mathf.Lerp(strength, jumpMotionStrength, jumping);
        target = Quaternion.Slerp(Quaternion.identity, target,
            Mathf.Lerp(Mathf.Lerp(strength, sprintMotionStrength, running), 1, rollWeight));
        // Small secondary motion is additive, so no imported action can freeze the
        // breathing, shoulder adjustment or relaxed elbow bend at its last frame.
        float living = secondaryMotion * (player.IsRolling ? 0.12f : 1);
        float rest = (1 - stride) * (1 - airWeight);
        Vector3 secondary = Vector3.zero;
        if (bone == PlayerRigBone.Hips)
            secondary = new Vector3(breath * 0.25f, Mathf.Sin(lifeTime * 0.67f) * 0.35f * rest, Mathf.Sin(lifeTime * 0.73f) * 0.65f * rest);
        else if (bone == PlayerRigBone.Spine)
            secondary = new Vector3(-breath * 1.2f - landing * 2, Mathf.Sin(lifeTime * 0.81f) * 0.4f, Mathf.Sin(lifeTime * 0.73f + 0.4f) * 0.4f);
        else if (bone == PlayerRigBone.Chest)
            secondary = new Vector3(breath * 1.4f - landing * 2, Mathf.Sin(lifeTime * 0.67f + 0.7f) * 0.6f, Mathf.Sin(lifeTime * 0.91f) * 0.4f);
        else if (bone == PlayerRigBone.LeftArm || bone == PlayerRigBone.RightArm)
        {
            float offset = bone == PlayerRigBone.LeftArm ? 0 : 1.1f;
            float side = bone == PlayerRigBone.LeftArm ? -1 : 1;
            secondary = new Vector3(breath * 0.9f + Mathf.Sin(lifeTime * 1.3f + offset) * 0.9f,
                Mathf.Sin(lifeTime * 0.83f + offset) * 0.7f, side * (1.5f * rest + breath * 0.6f));
        }
        else if (bone == PlayerRigBone.LeftForearm || bone == PlayerRigBone.RightForearm)
            secondary.x = -5 * rest + breath * 0.9f + Mathf.Sin(lifeTime * 1.1f + (bone == PlayerRigBone.LeftForearm ? 0 : 0.9f));
        target *= Quaternion.Euler(secondary * living);
        target = Quaternion.RotateTowards(Quaternion.identity, target, limit + 3);
        if (torsoBone)
        {
            // Budget tilt across the entire parent chain, rather than allowing
            // every joint to bend by the full amount and accumulating a hunch.
            float share = bone == PlayerRigBone.Hips ? 0.15f : bone == PlayerRigBone.Spine ? 0.35f : 0.5f;
            Quaternion upright = LimitTilt(target, Mathf.Clamp(maxGroundedTorsoTilt, 0, 8) * share);
            target = Quaternion.Slerp(upright, target, Mathf.Max(airWeight, rollWeight));
        }
        if (bone == PlayerRigBone.LeftForearm || bone == PlayerRigBone.RightForearm)
        {
            // The source skeleton has a different arm basis. Discard its axial twist:
            // this low-poly, generated rig supports a hinge at the elbow, not a wrist roll.
            Vector3 hinge = Vector3.Cross(Vector3.forward, joint.localPosition).normalized;
            target = ElbowHinge(target, hinge);
        }
        if(CinematicIdle && outcomeData != null)
            target = SampleMotion(outcomeData, (int)bone, Mathf.Min(Time.unscaledTime-outcomeStarted, outcomeData.duration));
        else if(CinematicIdle && StoryPose!=PlayerStoryPose.Neutral)
            target=Quaternion.Slerp(target,StoryJointPose(bone),Mathf.Clamp01(StoryPoseWeight));
        float follow = Mathf.Lerp(bone == PlayerRigBone.Spine ? 0.7f : bone == PlayerRigBone.Chest ? 0.85f : 1, 2.5f, rollWeight);
        joint.localRotation = Quaternion.Slerp(joint.localRotation, target, 1 - Mathf.Pow(1 - blend, follow));
    }

    Quaternion StoryJointPose(PlayerRigBone bone)
    {
        bool arm=bone==PlayerRigBone.LeftArm||bone==PlayerRigBone.RightArm;
        bool elbow=bone==PlayerRigBone.LeftForearm||bone==PlayerRigBone.RightForearm;
        float side=bone==PlayerRigBone.LeftArm?-1:1;
        if(elbow)return Quaternion.AngleAxis(StoryPose==PlayerStoryPose.Fear?-25:-12,
            Vector3.Cross(Vector3.forward,bones[(int)bone].localPosition).normalized);
        if(StoryPose==PlayerStoryPose.Fear)
        {
            if(arm)return Quaternion.Euler(-48+Mathf.Sin(lifeTime*6)*1.5f,0,side*12);
            if(bone==PlayerRigBone.Chest)return Quaternion.Euler(-5+breath,0,0);
            if(bone==PlayerRigBone.Spine)return Quaternion.Euler(-3,0,0);
        }
        if(StoryPose==PlayerStoryPose.Exhausted)
        {
            if(arm)return Quaternion.Euler(-18+breath*1.5f,0,side*3);
            if(bone==PlayerRigBone.Spine)return Quaternion.Euler(7+breath,0,0);
            if(bone==PlayerRigBone.Chest)return Quaternion.Euler(10+breath*1.5f,0,2);
            if(bone==PlayerRigBone.LeftThigh||bone==PlayerRigBone.RightThigh)return Quaternion.Euler(-22,0,0);
            if(bone==PlayerRigBone.LeftShin||bone==PlayerRigBone.RightShin)return Quaternion.Euler(30,0,0);
        }
        if(StoryPose==PlayerStoryPose.Relief)
        {
            if(arm)return Quaternion.Euler(-12+breath,0,side*17);
            if(bone==PlayerRigBone.Chest)return Quaternion.Euler(-2+breath*1.5f,Mathf.Sin(lifeTime)*2,0);
        }
        return Quaternion.identity;
    }

    Quaternion TuckedRollPose(PlayerRigBone bone)
    {
        // The imported knees were relative to the original crouched mesh. Add
        // a stable sagittal tuck for the corrected standing rest pose; discard
        // axial limb twists that this generated rig cannot reproduce safely.
        Quaternion sample = SampleMotion(rollData, (int)bone, player.RollProgress * rollData.duration);
        float pitch = Mathf.DeltaAngle(0, sample.eulerAngles.x);
        if (bone == PlayerRigBone.LeftThigh || bone == PlayerRigBone.RightThigh)
            return Quaternion.Euler(-85 + Mathf.Clamp(pitch + 80, -15, 15) * 0.3f, 0, 0);
        if (bone == PlayerRigBone.LeftShin || bone == PlayerRigBone.RightShin)
            return Quaternion.Euler(110 + Mathf.Clamp(pitch - 30, -15, 15) * 0.3f, 0, 0);
        if (bone == PlayerRigBone.LeftArm || bone == PlayerRigBone.RightArm)
            return Quaternion.Euler(-65 + Mathf.Clamp(pitch + 60, -20, 20) * 0.2f, 0, bone == PlayerRigBone.LeftArm ? -8 : 8);
        if (bone == PlayerRigBone.LeftForearm || bone == PlayerRigBone.RightForearm)
            return Quaternion.AngleAxis(-25, Vector3.Cross(Vector3.forward, bones[(int)bone].localPosition).normalized);
        if (bone == PlayerRigBone.Spine) return Quaternion.Euler(24, 0, 0);
        if (bone == PlayerRigBone.Chest) return Quaternion.Euler(24 + Mathf.Clamp(pitch - 15, -10, 10) * 0.2f, 0, 0);
        return Quaternion.identity;
    }

    Quaternion SampleWalking(PlayerRigBone bone)
    {
        Quaternion sample = SampleMotion(walkData, (int)bone, walkTime);
        Vector3 angles = sample.eulerAngles;
        float pitch = Mathf.DeltaAngle(0, angles.x);
        float yaw = Mathf.DeltaAngle(0, angles.y);
        float roll = Mathf.DeltaAngle(0, angles.z);
        if (bone == PlayerRigBone.LeftThigh || bone == PlayerRigBone.RightThigh)
        {
            float sideStep = Mathf.Cos(locomotionPhase * Mathf.PI * 2 + (bone == PlayerRigBone.RightThigh ? Mathf.PI : 0));
            pitch *= walkingDirection.z;
            roll += -sideStep * walkingDirection.x * 16;
        }
        else if (bone == PlayerRigBone.LeftArm || bone == PlayerRigBone.RightArm)
            pitch *= walkingDirection.z;
        // Knees keep their natural hinge direction when walking backwards or sideways.
        return Quaternion.Euler(pitch, yaw, roll);
    }

    public static Quaternion ElbowHinge(Quaternion rotation, Vector3 axis)
    {
        axis.Normalize();
        float projected = Vector3.Dot(new Vector3(rotation.x, rotation.y, rotation.z), axis);
        float angle = Mathf.DeltaAngle(0, 2 * Mathf.Atan2(projected, rotation.w) * Mathf.Rad2Deg);
        return Quaternion.AngleAxis(Mathf.Clamp(angle, -30, 30), axis);
    }

    public static Quaternion LimitTilt(Quaternion rotation, float degrees)
    {
        Vector3 up = rotation * Vector3.up;
        float tilt = Vector3.Angle(Vector3.up, up);
        if (tilt <= Mathf.Max(0, degrees) || tilt < 0.0001f) return rotation;
        Quaternion upright = Quaternion.FromToRotation(up, Vector3.up) * rotation;
        return Quaternion.Slerp(upright, rotation, Mathf.Clamp01(degrees / tilt));
    }

    static Quaternion SampleMotion(MotionData data, int bone, float time)
    {
        if (data == null) return Quaternion.identity;
        foreach (var track in data.tracks)
        {
            if (track.bone != bone || track.rotations == null || track.rotations.Length == 0) continue;
            float frame = (data.loop ? Mathf.Repeat(time, data.duration) : Mathf.Clamp(time, 0, data.duration)) * data.sampleRate;
            int first = Mathf.Clamp(Mathf.FloorToInt(frame), 0, track.rotations.Length - 1);
            int next = data.loop ? (first + 1) % track.rotations.Length : Mathf.Min(first + 1, track.rotations.Length - 1);
            return Quaternion.Slerp(track.rotations[first], track.rotations[next], frame - Mathf.Floor(frame));
        }
        return Quaternion.identity;
    }

    void OnDestroy()
    {
        for (int i = 0; i < originalRenderers.Count; i++)
            if (originalRenderers[i]) originalRenderers[i].enabled = originalStates[i];
        foreach (var item in generatedRenderers) if (item) Destroy(item);
        foreach (var mesh in ownedMeshes) if (mesh) Destroy(mesh);
        if (rig) Destroy(rig.gameObject);
    }
}
