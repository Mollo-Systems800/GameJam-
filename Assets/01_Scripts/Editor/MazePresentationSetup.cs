using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MazePresentationSetup
{
    const string Materials = "Assets/04_Materials/";
    const string Prefabs = "Assets/03_Prefabs/";
    public static void Apply()
    {
        ImportPhoto("Assets/02_Sprites/EmploymentApplication.jpg", 2048);
        ImportPhoto("Assets/02_Sprites/PillBoxReference.jpg", 4096);
        var application = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/02_Sprites/EmploymentApplication.jpg");
        var medicine = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/02_Sprites/PillBoxReference.jpg");
        var paperPrint = Material("EmploymentPrint", Color.white, application, true);
        var paperEdge = Material("EmploymentPaperEdge", new Color(.94f, .93f, .89f));
        var carton = Material("MedicineCarton", Color.white, medicine);
        Material wood = Material("SpadeWood", new Color(.39f, .19f, .075f));
        Material steel = Material("SpadeSteel", new Color(.10f, .38f, .43f));
        steel.SetFloat("_Metallic", .7f); steel.SetFloat("_Smoothness", .36f); EditorUtility.SetDirty(steel);
        var sheet = new GameObject("EmploymentApplicationBoss");
        float width = 1.8f * application.width / application.height;
        var backing = GameObject.CreatePrimitive(PrimitiveType.Cube); backing.name = "Paper thickness";
        backing.transform.SetParent(sheet.transform, false); backing.transform.localPosition = Vector3.up * .9f;
        backing.transform.localScale = new Vector3(width, 1.8f, .018f);
        backing.GetComponent<Renderer>().sharedMaterial = paperEdge; UnityEngine.Object.DestroyImmediate(backing.GetComponent<Collider>());
        var paperMesh = new SurfaceMesh();
        paperMesh.Face(new Vector3(-width/2,1.8f,.01f), new Vector3(width/2,1.8f,.01f), new Vector3(width/2,0,.01f), new Vector3(-width/2,0,.01f), Full);
        paperMesh.Face(new Vector3(width/2,1.8f,-.01f), new Vector3(-width/2,1.8f,-.01f), new Vector3(-width/2,0,-.01f), new Vector3(width/2,0,-.01f), Full);
        AddMesh(sheet, "Printed application", paperMesh.Save("EmploymentSheet"), paperPrint);
        sheet.AddComponent<PaperBossFacing>();
        var bossPrefab = PrefabUtility.SaveAsPrefabAsset(sheet, Prefabs + "EmploymentApplicationBoss.prefab");
        UnityEngine.Object.DestroyImmediate(sheet);

        // Map each photographed face onto an actual box. Projective UVs undo
        // the perspective of the reference without editing or regenerating it.
        var front = PhotoQuad(676,444,1493,260,1483,746,685,965);
        var side = PhotoQuad(553,373,676,444,685,965,563,878);
        var top = PhotoQuad(553,373,1343,203,1493,260,676,444);
        const float x = .375f, y = .46f, z = .065f;
        var boxMesh = new SurfaceMesh();
        boxMesh.Face(new Vector3(-x,y,z), new Vector3(x,y,z), new Vector3(x,0,z), new Vector3(-x,0,z), front);
        boxMesh.Face(new Vector3(x,y,-z), new Vector3(-x,y,-z), new Vector3(-x,0,-z), new Vector3(x,0,-z), front);
        boxMesh.Face(new Vector3(-x,y,-z), new Vector3(-x,y,z), new Vector3(-x,0,z), new Vector3(-x,0,-z), side);
        boxMesh.Face(new Vector3(x,y,z), new Vector3(x,y,-z), new Vector3(x,0,-z), new Vector3(x,0,z), side);
        boxMesh.Face(new Vector3(-x,y,-z), new Vector3(x,y,-z), new Vector3(x,y,z), new Vector3(-x,y,z), top);
        boxMesh.Face(new Vector3(-x,0,z), new Vector3(x,0,z), new Vector3(x,0,-z), new Vector3(-x,0,-z), top);
        var box = new GameObject("MedicineBox"); AddMesh(box, "Printed carton", boxMesh.Save("MedicineCarton"), carton);
        var pillPrefab = PrefabUtility.SaveAsPrefabAsset(box, Prefabs + "MedicineBox.prefab"); UnityEngine.Object.DestroyImmediate(box);
        var shovel = PrefabUtility.LoadPrefabContents(Prefabs + "Pala.prefab");
        try
        {
            foreach (var renderer in shovel.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = slots[i] && slots[i].name.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0 ? wood : steel;
                renderer.sharedMaterials = slots;
            }
            PrefabUtility.SaveAsPrefabAsset(shovel, Prefabs + "Pala.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(shovel); }
        EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        game.bossVisualPrefab = bossPrefab; game.pillVisualPrefab = pillPrefab;
        var hud = UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();
        MazeCanvasSetup.AddCampaignRecordForm(hud);
        EditorUtility.SetDirty(game); SaveScene();
        EditorSceneManager.OpenScene("Assets/00_Scenes/Inicio.unity");
        var menu = UnityEngine.Object.FindAnyObjectByType<StartupMenu>();
        StartupDossierSetup.AddRanking(menu); SaveScene(); AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/presentation-setup-result.txt", "PASS: printed boss, 3D medicine carton, colored shovel, record form and start-menu ranking saved.");
    }
    static void SaveScene()
    { var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
    static void ImportPhoto(string path, int maxSize)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
        importer.mipmapEnabled = true; importer.maxTextureSize = maxSize;
        importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
    }
    static Material Material(string name, Color color, Texture2D texture = null, bool unlit = false)
    {
        string path = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor", color); material.SetTexture("_BaseMap", texture);
        if (!unlit) { material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .14f); material.SetTexture("_EmissionMap", null); material.SetColor("_EmissionColor", Color.black); material.DisableKeyword("_EMISSION"); material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack; }
        EditorUtility.SetDirty(material); return material;
    }
    static void AddMesh(GameObject root, string name, Mesh mesh, Material material)
    {
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.transform.SetParent(root.transform, false);
        part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<Renderer>().sharedMaterial = material;
    }
    static readonly Vector2[] Full = { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
    static Vector2[] PhotoQuad(params float[] coordinates)
    {
        var result = new Vector2[4];
        for (int i = 0; i < 4; i++) result[i] = new Vector2(coordinates[i*2]/2048, coordinates[i*2+1]/1143);
        return result;
    }
    sealed class SurfaceMesh
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> triangles = new List<int>();
        public void Face(Vector3 tl, Vector3 tr, Vector3 br, Vector3 bl, Vector2[] photo)
        {
            const int grid = 12; int start = vertices.Count;
            for (int y = 0; y <= grid; y++) for (int x = 0; x <= grid; x++)
            {
                float s = x/(float)grid, t = y/(float)grid;
                vertices.Add(Vector3.Lerp(Vector3.Lerp(tl,tr,s), Vector3.Lerp(bl,br,s), t));
                Vector2 pixel = Project(photo, 1-s, t); uv.Add(new Vector2(pixel.x, 1-pixel.y));
            }
            for (int y = 0; y < grid; y++) for (int x = 0; x < grid; x++)
            {
                int a = start + y*(grid+1)+x, b = a+grid+1;
                triangles.AddRange(new[] {a,b,a+1,a+1,b,b+1});
            }
        }
        static Vector2 Project(Vector2[] p, float s, float t)
        {
            Vector2 d1=p[1]-p[2], d2=p[3]-p[2], d3=p[0]-p[1]+p[2]-p[3];
            float determinant=d1.x*d2.y-d2.x*d1.y;
            if (Mathf.Abs(determinant)<.000001f) return Vector2.Lerp(Vector2.Lerp(p[0],p[1],s),Vector2.Lerp(p[3],p[2],s),t);
            float g=(d3.x*d2.y-d2.x*d3.y)/determinant, h=(d1.x*d3.y-d3.x*d1.y)/determinant;
            return ((p[1]-p[0]+g*p[1])*s+(p[3]-p[0]+h*p[3])*t+p[0])/(g*s+h*t+1);
        }
        public Mesh Save(string name)
        {
            string path=Prefabs+name+".asset"; var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh) {mesh=new Mesh {name=name};AssetDatabase.CreateAsset(mesh,path);} else mesh.Clear();
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
        }
    }
}
