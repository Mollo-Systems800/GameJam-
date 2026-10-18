using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class MazeBetaGame
{
    [Header("Terror: cielo oscuro y luces de oficina")]
    public bool horrorLighting=true;
    public Color nightSkyColor=new Color(.007f,.012f,.022f);
    public Color nightAmbientColor=new Color(.15f,.18f,.23f);
    [Range(0,.3f)] public float nightMoonIntensity=.065f;
    [Range(0,.08f)] public float nightFogDensity=.026f;
    [Range(4,40)] public int maximumOfficeLamps=18;
    public GameObject officeLampPrefab;
    public GameObject exitBeaconPrefab;
    public GameObject playerGuideLightPrefab;
    readonly List<int> nightLightCells=new List<int>();
    public IReadOnlyList<int> NightLightCells=>nightLightCells;
    GameObject guideLight;

    public void ApplyNightEnvironment()
    {
        if(!horrorLighting)return;
        RenderSettings.skybox=null;
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=nightAmbientColor;
        RenderSettings.ambientIntensity=1;
        RenderSettings.reflectionIntensity=.08f;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;
        RenderSettings.fogColor=nightSkyColor;RenderSettings.fogDensity=nightFogDensity;
        var camera=view?view:Camera.main;
        if(camera){camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=nightSkyColor;}
        foreach(var light in FindObjectsByType<Light>())
        {
            if(light.type!=LightType.Directional)continue;
            light.color=new Color(.43f,.56f,.78f);light.intensity=nightMoonIntensity;
            light.shadows=LightShadows.Soft;light.shadowStrength=.85f;
        }
    }
    void BuildNightLighting()
    {
        nightLightCells.Clear();
        if(guideLight){guideLight.SetActive(false);Destroy(guideLight);guideLight=null;}
        if(!horrorLighting)return;
        ApplyNightEnvironment();
        var chosen=new HashSet<int>{0,Grid.ExitCell};
        if(Grid.ParkourCells.Count>0)
        {chosen.Add(Grid.ParkourCells[0]);chosen.Add(Grid.ParkourCells[Grid.ParkourCells.Count-1]);}
        int budget=Mathf.Clamp(Grid.Count/12,4,Mathf.Max(4,maximumOfficeLamps));
        var path=Grid.FindPath(0,Grid.ExitCell);
        for(int i=3;i<path.Count && chosen.Count<budget;i+=4)
            if(!Grid.IsParkourPit(path[i]))chosen.Add(path[i]);
        var random=new System.Random(ActiveSeed^713407);
        for(int attempt=0;attempt<Grid.Count*2&&chosen.Count<budget;attempt++)
        {
            int cell=random.Next(Grid.Count);if(Grid.IsParkourPit(cell))continue;
            bool near=false;
            foreach(int other in chosen)
                if((CellPosition(cell)-CellPosition(other)).sqrMagnitude<cellSize*cellSize*8){near=true;break;}
            if(!near)chosen.Add(cell);
        }
        // Sorting makes lamp placement stable on retries and independent of HashSet ordering.
        nightLightCells.AddRange(chosen);nightLightCells.Sort();
        var group=new GameObject("Iluminacion nocturna - puntos de luz").transform;group.SetParent(generated,false);
        foreach(int cell in nightLightCells)
        {
            var prefab=cell==Grid.ExitCell&&exitBeaconPrefab?exitBeaconPrefab:officeLampPrefab;
            var lamp=prefab?Instantiate(prefab,group):FallbackNightLamp(group);
            lamp.name=cell==Grid.ExitCell?"Luz de emergencia - salida":"Lampara de oficina "+cell;
            // A slim floor stand rests in a corner, outside the traversable centre of the cell.
            float inset=cellSize*.5f-WallThickness*.5f-.18f;
            lamp.transform.localPosition=CellPosition(cell)+new Vector3(-inset,0,-inset);
            foreach(var collider in lamp.GetComponentsInChildren<Collider>())collider.enabled=false;
            var flicker=lamp.GetComponent<HorrorLamp>();if(flicker)flicker.Initialize(ActiveSeed+cell*113);
        }
        if(view)
        {
            guideLight=playerGuideLightPrefab?Instantiate(playerGuideLightPrefab,view.transform):new GameObject("Luz de orientacion");
            guideLight.name="Luz de orientacion del jugador";
            guideLight.transform.SetParent(view.transform,false);guideLight.transform.localPosition=new Vector3(.24f,-.14f,.08f);
            guideLight.transform.localRotation=Quaternion.identity;
            if(!guideLight.GetComponentInChildren<Light>())
            {
                var beam=guideLight.AddComponent<Light>();beam.type=LightType.Spot;beam.range=8;beam.spotAngle=68;
                beam.innerSpotAngle=28;beam.intensity=2.2f;beam.color=new Color(.75f,.83f,1);beam.shadows=LightShadows.Soft;
            }
        }
    }
    GameObject FallbackNightLamp(Transform parent)
    {
        var root=new GameObject("Lampara de oficina");root.transform.SetParent(parent,false);
        var housing=GameObject.CreatePrimitive(PrimitiveType.Cube);housing.transform.SetParent(root.transform,false);
        housing.transform.localPosition=Vector3.up*wallHeight;housing.transform.localScale=new Vector3(.5f,.06f,.18f);
        var spot=new GameObject("Luz").AddComponent<Light>();spot.transform.SetParent(root.transform,false);
        spot.transform.localPosition=Vector3.up*(wallHeight-.08f);spot.transform.localRotation=Quaternion.Euler(90,0,0);
        spot.type=LightType.Spot;spot.range=6;spot.spotAngle=110;spot.innerSpotAngle=55;spot.intensity=9;
        spot.color=new Color(1,.78f,.48f);spot.shadows=LightShadows.Soft;return root;
    }
}
