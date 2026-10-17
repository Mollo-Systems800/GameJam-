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
    public TextAsset runMotion;
    public TextAsset jumpMotion;
    public TextAsset rollMotion;
    [Min(0.1f)] public float runReferenceSpeed = 4;
    [Range(0, 2)] public float idleStrength = 1;
    [Range(0, 1)] public float motionStrength = 0.65f;
    [Header("Postura y balanceo del torso")]
    [Range(0, 14)] public float torsoTwist = 9;
    [Range(0, 8)] public float runningLean = 3;
    [Range(0, 0.01f)] public float hipSway = 0.004f;
    public bool HasIdleMotion => idleData != null;
    public bool HasRunMotion => runData != null;
    public bool HasJumpMotion => jumpData != null;
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
    MotionData idleData;
    MotionData runData, jumpData;
    float runTime, jumpSampleTime;
    bool airborne;
    float sprintWeight;
    MotionData rollData;
    Bounds restBounds;

    [Serializable] sealed class MotionData
    {
        public float duration;
        public float sampleRate;
        public bool loop;
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
        restBounds = body;
        rig = new GameObject("Procedural Limb Rig").transform;
        rig.SetParent(player.visual, false);
        bones = new Transform[10];
        Vector3 centre = new Vector3(body.center.x, body.min.y, body.center.z);
        Bone(PlayerRigBone.Hips, rig, centre, 0, 0.49f);
        Bone(PlayerRigBone.Chest, bones[0], centre, 0, 0.72f);
        Bone(PlayerRigBone.LeftThigh, bones[0], centre, -0.10f, 0.48f);
        Bone(PlayerRigBone.LeftShin, bones[2], centre, -0.10f, 0.26f);
        Bone(PlayerRigBone.RightThigh, bones[0], centre, 0.10f, 0.48f);
        Bone(PlayerRigBone.RightShin, bones[4], centre, 0.10f, 0.26f);
        Bone(PlayerRigBone.LeftArm, bones[1], centre, -0.17f, 0.77f);
        Bone(PlayerRigBone.LeftForearm, bones[6], centre, -0.21f, 0.60f);
        Bone(PlayerRigBone.RightArm, bones[1], centre, 0.17f, 0.77f);
        Bone(PlayerRigBone.RightForearm, bones[8], centre, 0.21f, 0.60f);
        hipsRest = bones[0].localPosition;
        foreach (var filter in usable) Skin(filter, body);
        wasGrounded = player.IsGrounded;
        IsReady = ownedMeshes.Count > 0;
        idleData = LoadMotion(idleMotion);
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
        bone.position = player.visual.TransformPoint(origin + new Vector3(x, y, 0) * characterHeight);
        bone.rotation = player.visual.rotation;
        bones[(int)id] = bone;
    }

    void Skin(MeshFilter filter, Bounds bounds)
    {
        Mesh mesh = Instantiate(filter.sharedMesh);
        mesh.name = filter.sharedMesh.name + " - Animated Instance";
        var weights = new BoneWeight[mesh.vertexCount];
        Vector3[] vertices = mesh.vertices;
        Matrix4x4 toCharacter = player.visual.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        Matrix4x4 normalMatrix = toCharacter.inverse.transpose;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 point = toCharacter.MultiplyPoint3x4(vertices[i]);
            vertices[i] = point;
            var influence = PlayerRigWeights.SampleMesh(filter.name, (point.x - bounds.center.x) / bounds.size.y,
                (point.y - bounds.min.y) / bounds.size.y);
            weights[i] = new BoneWeight { boneIndex0 = influence.A, boneIndex1 = influence.B, boneIndex2 = influence.C, boneIndex3 = influence.D,
                weight0 = influence.WA, weight1 = influence.WB, weight2 = influence.WC, weight3 = influence.WD };
        }
        // Bake imported negative scales into a common space before skinning.
        // Keeping renderers under mirrored FBX transforms can invert shading and offsets.
        mesh.vertices = vertices;
        Vector3[] normals = mesh.normals;
        for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
        mesh.normals = normals;
        Vector4[] tangents = mesh.tangents;
        float sign = toCharacter.determinant < 0 ? -1 : 1;
        for (int i = 0; i < tangents.Length; i++)
        {
            Vector3 tangent = toCharacter.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
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
        var item = new GameObject("Animated " + filter.name);
        item.transform.SetParent(player.visual, false);
        var skin = item.AddComponent<SkinnedMeshRenderer>();
        var bindposes = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * item.transform.localToWorldMatrix;
        mesh.bindposes = bindposes;
        mesh.boneWeights = weights;
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

    void LateUpdate()
    {
        if (!IsReady || !player.CanMove) return;
        float dt = Time.deltaTime;
        bool grounded = player.IsGrounded;
        float target = grounded ? Mathf.Clamp01(player.PlanarSpeed / Mathf.Max(0.1f, player.baseSpeed)) : 0;
        float blend = 1 - Mathf.Exp(-blendSpeed * dt);
        stride = Mathf.Lerp(stride, target, blend);
        float sprintTarget = Mathf.InverseLerp(player.baseSpeed,
            player.baseSpeed * Mathf.Max(1.01f, player.sprintMultiplier), player.PlanarSpeed);
        sprintWeight = Mathf.Lerp(sprintWeight, grounded ? sprintTarget : 0, blend);
        idleWeight = grounded ? (1 - stride) * idleStrength : 0;
        airborne = !grounded;
        if (grounded) idleTime += dt;
        if (grounded) runTime += dt * player.PlanarSpeed / Mathf.Max(0.1f, runReferenceSpeed);
        if (jumpData != null && !grounded)
        {
            float launchSpeed = Mathf.Sqrt(2 * Mathf.Abs(Physics.gravity.y) * player.jumpHeight);
            float normalized = player.VerticalVelocity > 0
                ? Mathf.Lerp(jumpData.takeoff, jumpData.apex, Mathf.Clamp01(1 - player.VerticalVelocity / Mathf.Max(0.1f, launchSpeed)))
                : Mathf.Lerp(jumpData.apex, jumpData.landing, Mathf.Clamp01(-player.VerticalVelocity / Mathf.Max(0.1f, launchSpeed)));
            jumpSampleTime = normalized * jumpData.duration;
        }
        if (grounded && !wasGrounded) landing = 1;
        landing = Mathf.Max(0, landing - dt * 6);
        if (grounded) phase = Mathf.Repeat(phase + player.PlanarSpeed / Mathf.Max(0.2f, strideLength) * Mathf.PI * 2 * dt, Mathf.PI * 2);
        // Match the imported leg cycle so shoulders counter the same footfalls.
        float gaitPhase = runData != null ? runTime / runData.duration * Mathf.PI * 2 : phase;
        float cycle = Mathf.Sin(gaitPhase);
        bool rising = player.VerticalVelocity > 0;
        float thighL = grounded ? cycle * legSwing * stride : rising ? -25 : 10;
        float thighR = grounded ? -cycle * legSwing * stride : rising ? -38 : -8;
        float kneeL = grounded ? Mathf.Max(0, -cycle) * 38 * stride : rising ? 55 : 20;
        float kneeR = grounded ? Mathf.Max(0, cycle) * 38 * stride : rising ? 70 : 28;
        Pose(PlayerRigBone.LeftThigh, thighL, 0, blend);
        Pose(PlayerRigBone.Hips, 0, 0, blend);
        Pose(PlayerRigBone.RightThigh, thighR, 0, blend);
        Pose(PlayerRigBone.LeftShin, kneeL, 0, blend);
        Pose(PlayerRigBone.RightShin, kneeR, 0, blend);
        Pose(PlayerRigBone.LeftArm, grounded ? -cycle * armSwing * stride : -38, grounded ? -2 : -16, blend);
        Pose(PlayerRigBone.RightArm, grounded ? cycle * armSwing * stride : -38, grounded ? 2 : 16, blend);
        Pose(PlayerRigBone.LeftForearm, grounded ? -12 * stride : -32, 0, blend);
        Pose(PlayerRigBone.RightForearm, grounded ? -12 * stride : -32, 0, blend);
        Pose(PlayerRigBone.Chest, grounded ? 3 * stride : rising ? 8 : -4, grounded ? cycle * 2 * stride : 0, blend);
        float breathing = idleData != null ? Mathf.Sin(idleTime / idleData.duration * Mathf.PI * 4) * 0.002f * idleWeight : 0;
        float bob = grounded ? (1 - Mathf.Cos(gaitPhase * 2)) * 0.006f
            * Mathf.Lerp(1, 1.4f, sprintWeight) * stride - landing * 0.025f + breathing : 0;
        float sway = grounded ? cycle * hipSway * stride : 0;
        if (player.IsRolling) { bob = 0; sway = 0; }
        bones[0].localPosition = Vector3.Lerp(bones[0].localPosition,
            hipsRest + new Vector3(sway, bob, 0) * characterHeight, blend);
        ApplyRootRoll(blend);
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
        float frame = player.RollProgress * (rollData.rollDegrees.Length - 1);
        int first = Mathf.FloorToInt(frame);
        float degrees = Mathf.Lerp(rollData.rollDegrees[first],
            rollData.rollDegrees[Mathf.Min(first + 1, rollData.rollDegrees.Length - 1)], frame - first);
        Quaternion rotation = Quaternion.AngleAxis(degrees, Vector3.right);
        Vector3 pivot = restBounds.center;
        Vector3 position = pivot - rotation * pivot;
        // Support the rotating body above the floor using its rest bounds. The
        // controller and camera stay upright; locomotion still goes through Move.
        float radians = degrees * Mathf.Deg2Rad;
        float bottom = pivot.y - Mathf.Abs(Mathf.Cos(radians)) * restBounds.extents.y
            - Mathf.Abs(Mathf.Sin(radians)) * restBounds.extents.z;
        position.y += restBounds.min.y - bottom;
        rig.localRotation = rotation;
        rig.localPosition = position;
    }

    void Pose(PlayerRigBone bone, float pitch, float roll, float blend)
    {
        Transform joint = bones[(int)bone];
        Quaternion sampledIdle = SampleMotion(idleData, (int)bone, idleTime);
        Quaternion idle = Quaternion.SlerpUnclamped(Quaternion.identity, sampledIdle, idleStrength);
        Quaternion target = Quaternion.Euler(pitch, 0, roll)
            * Quaternion.SlerpUnclamped(Quaternion.identity, sampledIdle, idleWeight);
        if (!airborne && runData != null)
            target = Quaternion.Slerp(idle, SampleMotion(runData, (int)bone, runTime), stride);
        if (airborne && jumpData != null)
            target = SampleMotion(jumpData, (int)bone, jumpSampleTime);
        else if (!airborne && jumpData != null && landing > 0)
        {
            float tailTime = Mathf.Lerp(jumpData.landing, 1, 1 - landing) * jumpData.duration;
            target = Quaternion.Slerp(target, SampleMotion(jumpData, (int)bone, tailTime), landing);
        }
        if (player.IsRolling && rollData != null)
        {
            float progress = player.RollProgress;
            float weight = Mathf.SmoothStep(0, 1, Mathf.Min(progress / 0.1f, (1 - progress) / 0.15f));
            Quaternion rollPose = bone == PlayerRigBone.Hips ? Quaternion.identity
                : SampleMotion(rollData, (int)bone, progress * rollData.duration);
            target = Quaternion.Slerp(target, rollPose, weight);
        }
        if (!airborne && !player.IsRolling && (bone == PlayerRigBone.Hips || bone == PlayerRigBone.Chest))
        {
            // The source run leans too far forward on this mesh. Build an upright
            // torso pose with opposing hip/shoulder rotation instead of copying that lean.
            float gaitPhase = runData != null ? runTime / runData.duration * Mathf.PI * 2 : phase;
            float cycle = Mathf.Sin(gaitPhase);
            float effort = Mathf.Lerp(0.65f, 1, sprintWeight);
            Quaternion gait = bone == PlayerRigBone.Hips
                ? Quaternion.Euler(0, -cycle * torsoTwist * 0.45f * effort, cycle * 1.5f)
                : Quaternion.Euler(Mathf.Lerp(0.5f, runningLean, sprintWeight),
                    cycle * torsoTwist * effort, -cycle * 2.5f);
            Quaternion uprightIdle = Quaternion.Slerp(Quaternion.identity, idle, 0.25f);
            Quaternion torso = Quaternion.Slerp(uprightIdle, gait, stride);
            target = Quaternion.Slerp(torso, target, landing);
        }
        float limit = bone == PlayerRigBone.Hips ? 7 : bone == PlayerRigBone.Chest ? 14
            : bone == PlayerRigBone.LeftShin || bone == PlayerRigBone.RightShin ? 42 : 28;
        target = Quaternion.RotateTowards(Quaternion.identity, target, limit);
        target = Quaternion.Slerp(Quaternion.identity, target, motionStrength);
        if (bone == PlayerRigBone.LeftForearm || bone == PlayerRigBone.RightForearm)
        {
            // The source skeleton has a different arm basis. Discard its axial twist:
            // this low-poly, generated rig supports a hinge at the elbow, not a wrist roll.
            Vector3 hinge = Vector3.Cross(Vector3.forward, joint.localPosition).normalized;
            target = ElbowHinge(target, hinge);
        }
        joint.localRotation = Quaternion.Slerp(joint.localRotation, target, blend);
    }

    public static Quaternion ElbowHinge(Quaternion rotation, Vector3 axis)
    {
        axis.Normalize();
        float projected = Vector3.Dot(new Vector3(rotation.x, rotation.y, rotation.z), axis);
        float angle = Mathf.DeltaAngle(0, 2 * Mathf.Atan2(projected, rotation.w) * Mathf.Rad2Deg);
        return Quaternion.AngleAxis(Mathf.Clamp(angle, -30, 30), axis);
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
