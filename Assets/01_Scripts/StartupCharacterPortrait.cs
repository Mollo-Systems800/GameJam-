using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class StartupCharacterPortrait : MonoBehaviour
{
    public GameObject playerPrefab;
    public RawImage photo;
    GameObject stage;
    Camera portraitCamera;
    RenderTexture target;
    readonly List<Mesh> meshes = new List<Mesh>();
    void OnEnable() { if (playerPrefab && photo) Build(); }
    public void Build()
    {
        Cleanup();
        if (!playerPrefab || !photo) return;
        target = new RenderTexture(812, 696, 24) { name = "Postulante portrait", antiAliasing = 4, hideFlags = HideFlags.HideAndDontSave };
        target.Create(); photo.texture = target;
        stage = new GameObject("Startup portrait stage") { hideFlags = HideFlags.HideAndDontSave };
        stage.transform.position = new Vector3(500, 0, 500);
        var source = playerPrefab.GetComponent<Player>().visual;
        var model = Instantiate(source.gameObject, stage.transform);
        model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        foreach (var script in model.GetComponentsInChildren<MonoBehaviour>()) script.enabled = false;
        var filters = new List<MeshFilter>(); Bounds local = default; bool hasBounds = false;
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (!renderer || PlayerLimbAnimator.IsControl(filter.name)) { if (renderer) renderer.enabled = false; continue; }
            if (!filter.sharedMesh || !filter.sharedMesh.isReadable) continue;
            filters.Add(filter);
            foreach (var vertex in filter.sharedMesh.vertices)
            {
                Vector3 p = model.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                if (!hasBounds) { local = new Bounds(p, Vector3.zero); hasBounds = true; } else local.Encapsulate(p);
            }
        }
        if (hasBounds)
        {
            var posture = new PlayerRestPosture(filters, model.transform, local, 1);
            foreach (var filter in filters)
            {
                if (PlayerRestPosture.IsDuplicateAccessory(filter.name))
                { filter.GetComponent<MeshRenderer>().enabled = false; continue; }
                Mesh mesh = Instantiate(filter.sharedMesh); meshes.Add(mesh);
                Vector3[] v = mesh.vertices;
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 p = model.transform.InverseTransformPoint(filter.transform.TransformPoint(v[i]));
                    v[i] = filter.transform.InverseTransformPoint(model.transform.TransformPoint(posture.Point(p, filter.name)));
                }
                mesh.vertices = v; mesh.RecalculateBounds(); mesh.RecalculateNormals(); filter.sharedMesh = mesh;
            }
        }
        var renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = default; bool first = true;
        foreach (var renderer in renderers)
        { if (!renderer.enabled) continue; if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
        float scale = 1.8f / Mathf.Max(.01f, bounds.size.y);
        model.transform.localScale = Vector3.one * scale;
        model.transform.localPosition = -new Vector3(bounds.center.x - stage.transform.position.x, bounds.min.y, bounds.center.z - stage.transform.position.z) * scale;
        foreach (var t in stage.GetComponentsInChildren<Transform>()) t.gameObject.layer = 30;
        var cameraObject = new GameObject("Dossier photo camera"); cameraObject.transform.SetParent(stage.transform, false);
        portraitCamera = cameraObject.AddComponent<Camera>(); portraitCamera.cullingMask = 1 << 30;
        portraitCamera.clearFlags = CameraClearFlags.SolidColor; portraitCamera.backgroundColor = new Color(.64f, .65f, .58f);
        portraitCamera.orthographic = true; portraitCamera.orthographicSize = .5f;
        portraitCamera.nearClipPlane = .05f; portraitCamera.farClipPlane = 8;
        cameraObject.transform.localPosition = new Vector3(.16f, 1.28f, 3);
        cameraObject.transform.LookAt(stage.transform.position + new Vector3(0, 1.28f, 0));
        portraitCamera.targetTexture = target;
        var lightObject = new GameObject("Photo softbox"); lightObject.transform.SetParent(stage.transform, false);
        var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
        light.color = new Color(1, .91f, .75f); light.cullingMask = 1 << 30;
        lightObject.transform.rotation = Quaternion.Euler(28, 155, 0);
        var main = Camera.main; if (main) main.cullingMask &= ~(1 << 30);
    }
    void OnDisable() => Cleanup();
    void Cleanup()
    {
        if (photo) photo.texture = null;
        if (stage) { stage.SetActive(false); Remove(stage); } stage = null;
        if (target) { target.Release(); Remove(target); } target = null;
        foreach (var mesh in meshes) if (mesh) Remove(mesh); meshes.Clear();
    }
    static void Remove(Object obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
}
