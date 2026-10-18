using UnityEngine;
using UnityEngine.UI;

// A UI vertex gradient leaves the supplied illustration visible on the right.
public sealed class StartupBackdropShade : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        Color left = new Color(.015f, .025f, .035f, .72f);
        Color right = new Color(.015f, .025f, .035f, .06f);
        vh.AddVert(new Vector3(r.xMin, r.yMin), left, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin, r.yMax), left, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMax), right, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMin), right, Vector2.zero);
        vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
    }
}
