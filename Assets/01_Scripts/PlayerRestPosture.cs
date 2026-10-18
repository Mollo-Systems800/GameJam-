using System.Collections.Generic;
using UnityEngine;

// Neutralise the supplied static model's crouched silhouette before binding it.
// Animation then starts from a standing mesh instead of adding motion to its crouch.
public sealed class PlayerRestPosture
{
    // The source FBX contains a second, displaced eyebrow pair above the hair.
    // Keep it in the source bounds used to calibrate the rig, but never draw it.
    public static bool IsDuplicateAccessory(string name) => name == "Cube.008";
    readonly Bounds body;
    readonly float strength;
    readonly Profile torso, arms, legs;
    public float PelvisZ { get; }
    public float CentreZ => Mathf.Lerp(body.center.z, PelvisZ, strength);

    public PlayerRestPosture(List<MeshFilter> filters, Transform visual, Bounds bounds, float amount)
    {
        body = bounds;
        strength = Mathf.Clamp01(amount);
        var central = new List<Vector3>();
        var armPoints = new List<Vector3>();
        var legPoints = new List<Vector3>();
        foreach (var filter in filters)
            foreach (var vertex in filter.sharedMesh.vertices)
            {
                Vector3 p = visual.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                float x = Mathf.Abs(p.x - body.center.x) / body.size.y;
                if (x < 0.12f) central.Add(p);
                if (filter.name == "Cube.001" && x > 0.16f) armPoints.Add(p);
                if (filter.name == "Cube.005" || filter.name == "Cube.010") legPoints.Add(p);
            }
        torso = new Profile(central, body, new[] { 0.45f, 0.55f, 0.65f, 0.75f, 0.85f });
        arms = new Profile(armPoints, body, new[] { 0.44f, 0.52f, 0.60f, 0.68f, 0.77f });
        legs = new Profile(legPoints, body, new[] { 0.12f, 0.24f, 0.35f, 0.44f });
        PelvisZ = torso.At(0.55f);
    }

    public Vector3 Point(Vector3 p, string meshName, float anatomicalArm = -1)
    { p.z -= Offset(p, meshName, anatomicalArm); return p; }

    public float ArmWeight(Vector3 p, float anatomicalArm) => PlayerRigWeights.ArmWeight(
        (p.x - body.center.x) / body.size.y, (p.y - body.min.y) / body.size.y, ArmForward(p), anatomicalArm);
    public float ArmForward(Vector3 p) => (p.z - torso.At((p.y - body.min.y) / body.size.y)) / body.size.y;

    // Differential of the same smooth deformation preserves normals and tangents.
    public Vector3 Normal(Vector3 point, Vector3 normal, string meshName, float anatomicalArm = -1)
    {
        Gradient(point, meshName, anatomicalArm, out float dx, out float dy);
        return new Vector3(normal.x + dx * normal.z, normal.y + dy * normal.z, normal.z).normalized;
    }
    public Vector3 Tangent(Vector3 point, Vector3 tangent, string meshName, float anatomicalArm = -1)
    {
        Gradient(point, meshName, anatomicalArm, out float dx, out float dy);
        return new Vector3(tangent.x, tangent.y, tangent.z - dx * tangent.x - dy * tangent.y).normalized;
    }
    void Gradient(Vector3 p, string name, float anatomicalArm, out float dx, out float dy)
    {
        float epsilon = body.size.y * 0.001f;
        dx = (Offset(p + Vector3.right * epsilon, name, anatomicalArm) - Offset(p - Vector3.right * epsilon, name, anatomicalArm)) / (2 * epsilon);
        dy = (Offset(p + Vector3.up * epsilon, name, anatomicalArm) - Offset(p - Vector3.up * epsilon, name, anatomicalArm)) / (2 * epsilon);
    }
    float Offset(Vector3 p, string name, float anatomicalArm)
    {
        float y = (p.y - body.min.y) / body.size.y;
        float x = Mathf.Abs(p.x - body.center.x) / body.size.y;
        bool leg = name == "Cube.005" || name == "Cube.010" || (name == "Cube.001" && y < 0.3f);
        // All face, hair and eye meshes receive exactly the same translation.
        // A height-dependent shear changes the jaw and separates accessories.
        if (name != "Cube.001" && name != "Cube.007" && !leg && name != "Cube.006")
            return (torso.At(0.85f) - PelvisZ) * strength;
        float offset = leg ? (legs.At(y) - PelvisZ) * (1 - Smooth(0.44f, 0.55f, y))
                           : torso.At(y) - PelvisZ;
        if (!leg)
        {
            // Translate the head as one piece instead of shearing its face/jaw.
            float head = Smooth(0.74f, 0.78f, y) * (1 - Smooth(0.12f, 0.16f, x));
            offset = Mathf.Lerp(offset, torso.At(0.85f) - PelvisZ, head);
            // One spatial field for exposed skin and sleeves keeps their shared
            // seams coincident, including the low wrists in this source pose.
            float arm = ArmWeight(p, anatomicalArm);
            // Translate the hand/cuff together, preserving finger lengths and
            // knuckles. Blend into the posture correction around the elbow.
            float sampleY = Mathf.Lerp(0.52f, y, Smooth(0.53f, 0.60f, y));
            float relaxedZ = PelvisZ + Mathf.Max(0, 0.77f - sampleY) * body.size.y * Mathf.Tan(4 * Mathf.Deg2Rad);
            offset = Mathf.Lerp(offset, arms.At(sampleY) - relaxedZ, arm);
        }
        return offset * strength;
    }
    static float Smooth(float low, float high, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(low, high, value));

    sealed class Profile
    {
        readonly float[] heights, centres;
        public Profile(List<Vector3> points, Bounds body, float[] slices)
        {
            heights = slices;
            centres = new float[slices.Length];
            for (int i = 0; i < slices.Length; i++)
            {
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                float nearest = float.PositiveInfinity, fallback = body.center.z;
                foreach (var point in points)
                {
                    float distance = Mathf.Abs((point.y - body.min.y) / body.size.y - slices[i]);
                    if (distance < nearest) { nearest = distance; fallback = point.z; }
                    if (distance > 0.04f) continue;
                    min = Mathf.Min(min, point.z); max = Mathf.Max(max, point.z);
                }
                centres[i] = float.IsInfinity(min) ? fallback : (min + max) * 0.5f;
            }
        }
        public float At(float height)
        {
            for (int i = 1; i < heights.Length; i++)
                if (height < heights[i])
                    return Mathf.Lerp(centres[i - 1], centres[i], Smooth(heights[i - 1], heights[i], height));
            return centres[centres.Length - 1];
        }
    }
}
