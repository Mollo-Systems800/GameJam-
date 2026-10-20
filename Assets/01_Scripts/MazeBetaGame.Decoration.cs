using UnityEngine;

public sealed partial class MazeBetaGame
{
    readonly System.Collections.Generic.List<TextMesh> exitSigns = new System.Collections.Generic.List<TextMesh>();
    void UpdateExitSigns()
    {
        if(IsCinematicPlaying || !view)return;
        foreach(var sign in exitSigns)
        {
            if(!sign)continue;
            Vector3 delta=view.transform.position-sign.transform.position;
            sign.GetComponent<Renderer>().enabled=delta.sqrMagnitude<64
                && Vector3.Dot(sign.transform.forward,delta)<0
                && !Physics.Linecast(view.transform.position,sign.transform.position,~0,QueryTriggerInteraction.Ignore);
        }
    }
    void BuildThemeDecorations()
    {
        var props = ActiveArea.decorationPrefabs;
        if (props == null || props.Length == 0) return;
        var random = new System.Random(ActiveSeed ^ 46571);
        for (int cell = 1; cell < Grid.Count; cell++)
        {
            if (Grid.IsExitCell(cell) || Grid.IsParkourCell(cell) || obstacleCells.Contains(cell) || residentCells.Contains(cell)) continue;
            if (random.NextDouble() > .38 + CurrentLevelInArea * .08) continue;
            int side = random.Next(4);
            for (int d = 0; d < 4; d++)
            {
                int wall = (side + d) % 4;
                if (Grid.IsOpen(cell, wall)) continue;
                var prefab = props[random.Next(props.Length)];
                if (!prefab) break;
                var root = new GameObject("Decoración " + ActiveArea.areaName + " " + cell).transform;
                root.SetParent(generated, false);
                // Shallow wall recesses leave the centre and every open passage free.
                root.position = CellPosition(cell) + Direction(wall) * (cellSize * .5f - WallThickness * .5f - .23f);
                FitVisual(prefab, root, new Vector3(.8f, 1.45f, .42f), false);
                root.rotation = Quaternion.LookRotation(-Direction(wall));
                break;
            }
        }
        if (CurrentAreaIndex != 2 || !ActiveArea.landscapePrefab) return;
        // Trees are scenery outside the maze; never obstacles in its route.
        for (int i = 0; i < Grid.Width; i += 2)
        {
            foreach (int z in new[] { -2, Grid.Height + 1 })
            {
                var tree = new GameObject("Arbol exterior " + i + " " + z).transform;
                tree.SetParent(generated, false);
                tree.position = new Vector3(i * cellSize, -.15f, z * cellSize);
                FitVisual(ActiveArea.landscapePrefab, tree, new Vector3(3, 4.2f, 3), true);
            }
        }
    }

    void BuildExitPortal(Vector3 position, Vector3 direction)
    {
        var root = new GameObject("Meta - salida del departamento").transform;
        root.SetParent(generated, false);
        root.position = position;
        root.rotation = Quaternion.LookRotation(direction);
        float span = cellSize * .88f;
        ExitPart(root, "Jamba izquierda", new Vector3(-span*.5f, 1.08f, 0), new Vector3(.16f,2.16f,.22f), wallMaterial);
        ExitPart(root, "Jamba derecha", new Vector3(span*.5f, 1.08f, 0), new Vector3(.16f,2.16f,.22f), wallMaterial);
        ExitPart(root, "Dintel verde", new Vector3(0,2.13f,0), new Vector3(span+.16f,.3f,.22f), exitMaterial);
        ExitPart(root, "Umbral", new Vector3(0,.015f,0), new Vector3(span,.03f,.32f), exitMaterial);
        // Physical sign respects depth/occlusion and sits below wall height.
        for (int side = -1; side <= 1; side += 2)
        {
            var label = new GameObject("SALIDA").AddComponent<TextMesh>();
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(0,2.13f,side*.122f);
            label.transform.localRotation = Quaternion.Euler(0, side < 0 ? 0 : 180, 0);
            label.text = "SALIDA"; label.fontSize = 48; label.characterSize = .035f;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            exitSigns.Add(label);
        }
        var light = new GameObject("Luz local de la meta").AddComponent<Light>();
        light.transform.SetParent(root, false); light.transform.localPosition = new Vector3(0,1.8f,.25f);
        light.type = LightType.Point; light.color = new Color(.55f,1,.7f);
        light.range = 3; light.intensity = 1.5f; light.shadows = LightShadows.Soft;
    }
    void ExitPart(Transform root, string name, Vector3 position, Vector3 size, Material material)
    {
        var part = Cube(name, root, material);
        part.transform.localPosition = position; part.transform.localScale = size;
    }
}
