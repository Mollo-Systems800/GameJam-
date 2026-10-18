using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ResidentMenuRepair
{
    const string Request = "Logs/repair-resident-menu.request";
    static ResidentMenuRepair() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        try { ApplyAndReview(); File.WriteAllText("Logs/resident-menu-repair-result.txt", "PASS: supplied winding and double-sided surfaces corrected; rigid head/hands seated pose; resident and chair rebuilt; Inicio ranking regenerated and loaded in the open editor."); }
        catch (Exception e) { File.WriteAllText("Logs/resident-menu-repair-result.txt", e.ToString()); Debug.LogException(e); }
    }
    public static void ApplyAndReview()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        MazeFinalistsResidentSetup.Apply();
        bool asyncShaders=ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;
            typeof(MazeFinalistsResidentSetup).GetMethod("RenderResidentsAndPlayer", BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        }
        finally { ShaderUtil.allowAsyncCompilation=asyncShaders; }
        MazePresentationPreview.Render();
        typeof(MazePresentationVerification).GetMethod("VerifyData", BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        typeof(MazePresentationVerification).GetMethod("VerifyAssets", BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        EditorSceneManager.OpenScene("Assets/00_Scenes/Inicio.unity");
        var menu=UnityEngine.Object.FindAnyObjectByType<StartupMenu>();
        menu.RefreshMenuState();menu.FitPanel();
        SceneView.RepaintAll();
    }
}
