using UnityEngine;
using UnityEngine.UI;

// Native Canvas linework; no baked menu illustration or raster texture.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class StartupPaperArt : MaskableGraphic
{
    public enum Drawing { Desk, Sheet, Maze, Stamp, Tape }
    public Drawing drawing;
    public int variation = 7;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (drawing == Drawing.Desk)
        {
            Quad(vh, r.xMin, r.yMin, r.width, r.height, color);
            var faint = new Color(.82f, .86f, .72f, .035f);
            for (float x = r.xMin; x < r.xMax; x += 72) Quad(vh, x, r.yMin, 1, r.height, faint);
            for (float y = r.yMin; y < r.yMax; y += 72) Quad(vh, r.xMin, y, r.width, 1, faint);
            Line(vh, new Vector2(r.xMin, r.yMin + 38), new Vector2(r.xMax, r.yMin + 38), 2, new Color(0, 0, 0, .12f));
        }
        else if (drawing == Drawing.Sheet)
        {
            Quad(vh, r.xMin, r.yMin, r.width, r.height, color);
            var rng = new System.Random(variation);
            // Sparse fibres and slightly irregular edges, all fixed between frames.
            for (int i = 0; i < 220; i++)
                Quad(vh, r.xMin + (float)rng.NextDouble() * r.width, r.yMin + (float)rng.NextDouble() * r.height,
                    rng.Next(1, 4), 1, new Color(.25f, .21f, .14f, .055f));
            for (float y = r.yMin + 12; y < r.yMax; y += 21)
                Quad(vh, r.xMin - 1, y, 2 + rng.Next(3), 5, new Color(.44f, .38f, .25f, .13f));
            Quad(vh, r.xMax - 14, r.yMin, 14, 1, new Color(.4f, .32f, .2f, .25f));
        }
        else if (drawing == Drawing.Maze)
        {
            var grid = new MazeGrid(15, 5, variation, 1, .18f);
            float cell = Mathf.Min((r.width - 28) / grid.Width, (r.height - 24) / grid.Height);
            Vector2 start = new Vector2(r.xMin + 14, r.yMin + 12);
            Color ink = color;
            for (int c = 0; c < grid.Count; c++)
            {
                Vector2 p = start + new Vector2(c % grid.Width, c / grid.Width) * cell;
                if (!grid.IsOpen(c, 0)) Line(vh, p + Vector2.up * cell, p + Vector2.one * cell, 2.3f, ink);
                if (!grid.IsOpen(c, 1)) Line(vh, p + Vector2.right * cell, p + Vector2.one * cell, 2.3f, ink);
                if (c / grid.Width == 0 && !grid.IsOpen(c, 2)) Line(vh, p, p + Vector2.right * cell, 2.3f, ink);
                if (c % grid.Width == 0 && !grid.IsOpen(c, 3)) Line(vh, p, p + Vector2.up * cell, 2.3f, ink);
            }
            var route = grid.FindPath(0, grid.ExitCell);
            Color pencil = new Color(.64f, .20f, .14f, .9f);
            for (int i = 1; i < Mathf.Min(route.Count, 19); i++)
            {
                Vector2 a = start + (new Vector2(route[i - 1] % grid.Width, route[i - 1] / grid.Width) + Vector2.one * .5f) * cell;
                Vector2 b = start + (new Vector2(route[i] % grid.Width, route[i] / grid.Width) + Vector2.one * .5f) * cell;
                Line(vh, a, b, 2.5f, pencil);
            }
        }
        else if (drawing == Drawing.Stamp)
        {
            var inset = new Vector2(4, 4);
            Vector2 a = new Vector2(r.xMin, r.yMin) + inset, b = new Vector2(r.xMax, r.yMax) - inset;
            Line(vh, a, new Vector2(a.x + 34, a.y), 3, color);
            Line(vh, new Vector2(a.x + 39, a.y), new Vector2(b.x, a.y + 1), 3, color);
            Line(vh, new Vector2(b.x, a.y), b, 3, color);
            Line(vh, b, new Vector2(a.x, b.y - 1), 3, color);
            Line(vh, new Vector2(a.x, b.y), a, 3, color);
        }
        else
        {
            Quad(vh, r.xMin, r.yMin, r.width, r.height, color);
            for (float x = r.xMin + 7; x < r.xMax; x += 11) Line(vh, new Vector2(x, r.yMin), new Vector2(x + 9, r.yMax), 1, new Color(1, 1, 1, .13f));
        }
    }
    static void Quad(VertexHelper vh, float x, float y, float w, float h, Color c)
    {
        int n = vh.currentVertCount;
        vh.AddVert(new Vector3(x, y), c, Vector2.zero); vh.AddVert(new Vector3(x, y + h), c, Vector2.zero);
        vh.AddVert(new Vector3(x + w, y + h), c, Vector2.zero); vh.AddVert(new Vector3(x + w, y), c, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
    }
    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
    {
        Vector2 d = (b - a).normalized, p = new Vector2(-d.y, d.x) * width * .5f;
        int n = vh.currentVertCount;
        vh.AddVert(a - p, c, Vector2.zero); vh.AddVert(a + p, c, Vector2.zero);
        vh.AddVert(b + p, c, Vector2.zero); vh.AddVert(b - p, c, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
    }
}
