using System.Collections.Generic;
using UnityEngine;

public sealed partial class MazeBetaGame
{
    [Header("Senora sentada con silla (conjunto reemplazable)")]
    [Tooltip("Prefab del conjunto. Déjalo vacío para usar por separado Seated Woman Prefab y Chair Prefab.")]
    public GameObject seatedResidentPrefab;
    List<int> residentCells = new List<int>();
    readonly List<BoxCollider> residentZones = new List<BoxCollider>();
    public IReadOnlyList<int> ResidentCells => residentCells;
    public IReadOnlyList<BoxCollider> ResidentZones => residentZones;

    void SpawnSeatedWomen()
    {
        foreach (int cell in residentCells)
        {
            int side = 0;
            for (int d=0; d<4; d++) if (!Grid.IsOpen(cell,d)) { side=d; break; }
            var root = new GameObject("Senora sentada " + cell).transform;
            root.SetParent(generated, false);
            float inset = Mathf.Max(0, cellSize * 0.5f - WallThickness * 0.5f - 0.43f);
            root.position = CellPosition(cell) + Direction(side) * inset;
            if (EnvSeatedResident)
            {
                var resident = Instantiate(EnvSeatedResident, root);
                resident.name = "Abuela con silla";
                resident.transform.localPosition = Vector3.zero;
                resident.transform.localRotation = Quaternion.identity;
            }
            else
            {
                if (EnvChair)
                {
                    var chair = new GameObject("Silla").transform; chair.SetParent(root,false);
                    // The supplied chair faces -Z. At 0.82m overall height its
                    // seat is about 0.50m high, matching the seated model's pelvis.
                    FitVisual(EnvChair,chair,new Vector3(0.72f,0.82f,0.72f),true);
                    chair.localRotation = Quaternion.Euler(0,seatedChairYaw,0);
                }
                else
                {
                    var seat = Cube("Asiento provisional",root,wallMaterial);
                    seat.transform.localPosition = new Vector3(0,0.44f,0);
                    seat.transform.localScale = new Vector3(0.55f,0.12f,0.55f);
                    var back = Cube("Respaldo provisional",root,wallMaterial);
                    back.transform.localPosition = new Vector3(0,0.74f,-0.25f);
                    back.transform.localScale = new Vector3(0.55f,0.65f,0.08f);
                }
                if (EnvSeatedWoman)
                {
                    var woman = new GameObject("Senora - pose sentada").transform; woman.SetParent(root,false);
                    FitVisual(EnvSeatedWoman,woman,new Vector3(0.78f,1.34f,0.9f),true);
                    woman.localRotation = Quaternion.Euler(0,seatedWomanYaw,0);
                    woman.localPosition = seatedWomanOffset;
                }
            }
            var body = root.gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0,0.55f,-0.04f);
            body.size = new Vector3(0.55f,1.1f,0.48f);
            root.rotation = Quaternion.LookRotation(-Direction(side));
            var zone = new GameObject("Zona - solo pasar rodando").AddComponent<BoxCollider>();
            zone.transform.SetParent(root,false);
            zone.transform.rotation = Quaternion.identity;
            zone.transform.position = CellPosition(cell) + Vector3.up;
            zone.size = new Vector3(cellSize * 0.78f,4,cellSize * 0.78f);
            zone.isTrigger = true;
            residentZones.Add(zone);
            Label("PASA RODANDO [C / CTRL]", CellPosition(cell) + Vector3.up * 1.75f,
                root,new Color(1,0.8f,0.3f),0.045f);
        }
    }

    public bool RequiresRollNear(Vector3 position, float margin)
    {
        foreach (var zone in residentZones)
        {
            if (!zone) continue;
            Bounds bounds = zone.bounds;
            if (position.x > bounds.min.x-margin && position.x < bounds.max.x+margin &&
                position.z > bounds.min.z-margin && position.z < bounds.max.z+margin) return true;
        }
        return false;
    }

    public Vector3 RestrictWalking(Vector3 position, Vector3 displacement, float radius)
    {
        float fraction = 1;
        foreach (var zone in residentZones)
        {
            if (!zone) continue;
            Bounds bounds = zone.bounds;
            fraction = Mathf.Min(fraction, MazeSeatedResidents.EntryFraction(position.x,position.z,
                displacement.x,displacement.z,bounds.min.x-radius,bounds.max.x+radius,
                bounds.min.z-radius,bounds.max.z+radius));
        }
        return displacement * fraction;
    }

    void SpawnStoreTerminal()
    {
        for (int side=0; side<4; side++)
        {
            if (Grid.IsOpen(0,side)) continue;
            var root = new GameObject("Tienda - terminal de power ups").transform;
            root.SetParent(generated,false);
            root.position = CellPosition(0) + Direction(side) * Mathf.Max(0,cellSize * 0.5f-WallThickness * 0.5f-0.28f);
            if (EnvStore) FitVisual(EnvStore,root,new Vector3(0.65f,1.2f,0.4f),true);
            else
            {
                var shell=Cube("Terminal",root,wallMaterial);
                shell.transform.localPosition=Vector3.up*.6f; shell.transform.localScale=new Vector3(.6f,1.2f,.32f);
                var screen=Cube("Pantalla",root,pillMaterial);
                screen.transform.localPosition=new Vector3(0,.85f,.17f); screen.transform.localScale=new Vector3(.44f,.3f,.025f);
                var slot=Cube("Dispensador",root,coinMaterial);
                slot.transform.localPosition=new Vector3(0,.35f,.17f); slot.transform.localScale=new Vector3(.3f,.14f,.025f);
            }
            var body=root.gameObject.AddComponent<BoxCollider>();
            body.center=Vector3.up*.6f; body.size=new Vector3(.6f,1.2f,.32f);
            root.rotation = Quaternion.LookRotation(-Direction(side));
            Label("TIENDA [B]",root.position+Vector3.up*1.5f,root,new Color(.6f,1,1),.055f);
            break;
        }
    }
}
