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
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 point = player.visual.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
            var influence = PlayerRigWeights.Sample((point.x - bounds.center.x) / bounds.size.y,
                (point.y - bounds.min.y) / bounds.size.y);
            weights[i] = new BoneWeight { boneIndex0 = influence.A, boneIndex1 = influence.B, boneIndex2 = influence.C, boneIndex3 = influence.D,
                weight0 = influence.WA, weight1 = influence.WB, weight2 = influence.WC, weight3 = influence.WD };
        }
        var item = new GameObject("Animated " + filter.name);
        item.transform.SetParent(filter.transform, false);
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
        if (grounded) phase = Mathf.Repeat(phase + player.PlanarSpeed / Mathf.Max(0.2f, strideLength) * Mathf.PI * 2 * dt, Mathf.PI * 2);
        float cycle = Mathf.Sin(phase);
        bool rising = player.VerticalVelocity > 0;
        float thighL = grounded ? cycle * legSwing * stride : rising ? -25 : 10;
        float thighR = grounded ? -cycle * legSwing * stride : rising ? -38 : -8;
        float kneeL = grounded ? Mathf.Max(0, -cycle) * 38 * stride : rising ? 55 : 20;
        float kneeR = grounded ? Mathf.Max(0, cycle) * 38 * stride : rising ? 70 : 28;
        Pose(PlayerRigBone.LeftThigh, thighL, 0, blend);
        Pose(PlayerRigBone.RightThigh, thighR, 0, blend);
        Pose(PlayerRigBone.LeftShin, kneeL, 0, blend);
        Pose(PlayerRigBone.RightShin, kneeR, 0, blend);
        Pose(PlayerRigBone.LeftArm, grounded ? -cycle * armSwing * stride : -38, grounded ? -2 : -16, blend);
        Pose(PlayerRigBone.RightArm, grounded ? cycle * armSwing * stride : -38, grounded ? 2 : 16, blend);
        Pose(PlayerRigBone.LeftForearm, grounded ? -12 * stride : -32, 0, blend);
        Pose(PlayerRigBone.RightForearm, grounded ? -12 * stride : -32, 0, blend);
        Pose(PlayerRigBone.Chest, grounded ? 3 * stride : rising ? 8 : -4, grounded ? cycle * 2 * stride : 0, blend);
        if (grounded && !wasGrounded) landing = 1;
        landing = Mathf.Max(0, landing - dt * 6);
        float bob = grounded ? Mathf.Abs(cycle) * 0.012f * stride - landing * 0.025f : 0;
        bones[0].localPosition = Vector3.Lerp(bones[0].localPosition, hipsRest + Vector3.up * bob * characterHeight, blend);
        wasGrounded = grounded;
    }

    void Pose(PlayerRigBone bone, float pitch, float roll, float blend)
    {
        Transform joint = bones[(int)bone];
        joint.localRotation = Quaternion.Slerp(joint.localRotation, Quaternion.Euler(pitch, 0, roll), blend);
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
