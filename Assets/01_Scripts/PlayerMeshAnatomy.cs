using System.Collections.Generic;
using UnityEngine;

// Below the elbows, the source mesh has three separate surface branches:
// torso/coat hem and two forearms. Topology separates overlapping hands and
// clothing without a height threshold cutting through a cuff or a finger.
public static class PlayerMeshAnatomy
{
    public static float[] ArmRegions(Vector3[] points, int[] triangles, Bounds bounds)
    {
        int[] parent = new int[points.Length];
        var samePosition = new Dictionary<Vector3, int>();
        float cutoff = bounds.min.y + bounds.size.y * .60f;
        for (int i = 0; i < points.Length; i++)
        {
            parent[i] = i;
            if (points[i].y >= cutoff) continue;
            if (samePosition.TryGetValue(points[i], out int other)) parent[i] = Root(parent, other);
            else samePosition.Add(points[i], i);
        }
        for (int i = 0; i < triangles.Length; i++)
        {
            int a = triangles[i], b = triangles[i - i % 3 + (i + 1) % 3];
            if (points[a].y < cutoff && points[b].y < cutoff) parent[Root(parent, a)] = Root(parent, b);
        }
        var sum = new Dictionary<int, Vector3>();
        var counts = new Dictionary<int, int>();
        var bottom = new Dictionary<int, float>();
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i].y >= cutoff) continue;
            int id = Root(parent, i);
            if (!sum.ContainsKey(id)) { sum[id] = Vector3.zero; counts[id] = 0; bottom[id] = points[i].y; }
            sum[id] += points[i]; counts[id]++;
            bottom[id] = Mathf.Min(bottom[id], points[i].y);
        }
        var regions = new float[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            regions[i] = -1;
            if (points[i].y >= cutoff) continue;
            int id = Root(parent, i);
            float x = Mathf.Abs(sum[id].x / counts[id] - bounds.center.x) / bounds.size.y;
            float y = (bottom[id] - bounds.min.y) / bounds.size.y;
            regions[i] = x > .13f && y > .30f ? 1 : 0;
        }
        return BlendShoulders(points, triangles, regions, bounds);
    }

    static float[] BlendShoulders(Vector3[] points, int[] triangles, float[] regions, Bounds bounds)
    {
        var unique = new Dictionary<Vector3, int>();
        var positions = new List<Vector3>();
        var neighbors = new List<HashSet<int>>();
        var anchors = new List<float>();
        var ids = new int[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            if (!unique.TryGetValue(points[i], out int id))
            {
                id = positions.Count; unique.Add(points[i], id); positions.Add(points[i]);
                neighbors.Add(new HashSet<int>());
                anchors.Add((points[i].y - bounds.min.y) / bounds.size.y >= .75f ? 0 : regions[i]);
            }
            ids[i] = id;
        }
        for (int i = 0; i < triangles.Length; i++)
        {
            int a = ids[triangles[i]], b = ids[triangles[i - i % 3 + (i + 1) % 3]];
            if (a != b) { neighbors[a].Add(b); neighbors[b].Add(a); }
        }
        var values = new float[positions.Count]; var next = new float[positions.Count];
        for (int i = 0; i < values.Length; i++) values[i] = anchors[i] < 0 ? .5f : anchors[i];
        // Diffuse only across the shoulder surface. The hands/cuffs and torso
        // remain fixed boundary conditions, including every duplicated UV vertex.
        for (int pass = 0; pass < 128; pass++)
        {
            float change = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (anchors[i] >= 0) { next[i] = anchors[i]; continue; }
                float sum = 0, total = 0;
                foreach (int other in neighbors[i])
                {
                    float weight = 1 / Mathf.Max(bounds.size.y * .0001f, Vector3.Distance(positions[i], positions[other]));
                    sum += values[other] * weight; total += weight;
                }
                next[i] = total > 0 ? sum / total : 0;
                change = Mathf.Max(change, Mathf.Abs(next[i] - values[i]));
            }
            var old = values; values = next; next = old;
            if (change < .0001f) break;
        }
        for (int i = 0; i < regions.Length; i++) regions[i] = values[ids[i]];
        return regions;
    }
    static int Root(int[] parent, int i)
    {
        while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
        return i;
    }
}
