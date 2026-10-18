using System.Collections.Generic;
using UnityEngine;

public sealed partial class MazeBetaGame
{
    const float CopingHeight = .14f;
    const float CopingLift = .035f;
    const float CopingOverhang = .024f;
    HashSet<Vector2Int> wallTopJunctions = new HashSet<Vector2Int>();

    void BuildWallCoping(Transform wall)
    {
        float width = WallThickness + CopingOverhang;
        float centreY = Mathf.Max(2,wallHeight) + CopingLift - CopingHeight * .5f;
        // Spans stop at the junction squares. Their top faces never overlap,
        // even at corners, T junctions and consecutive collinear walls.
        var span = Cube("Wall top cap",wall,wallMaterial,EnvCap);
        span.transform.localPosition = Vector3.up * centreY;
        span.transform.localScale = new Vector3(cellSize-width,CopingHeight,width);
        for (int side=-1;side<=1;side+=2)
        {
            Vector3 end = wall.TransformPoint(Vector3.right * (side * cellSize * .5f));
            var key = new Vector2Int(Mathf.RoundToInt(end.x/cellSize+.5f),Mathf.RoundToInt(end.z/cellSize+.5f));
            if (!wallTopJunctions.Add(key)) continue;
            var joint = Cube("Wall top junction " + key,generated,wallMaterial,EnvCap);
            // Snap to the same grid point whichever incident wall owns it.
            joint.transform.position = new Vector3((key.x-.5f)*cellSize,centreY,(key.y-.5f)*cellSize);
            joint.transform.localScale = new Vector3(width,CopingHeight,width);
        }
    }
}
