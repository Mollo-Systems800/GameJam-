using System;
using System.Collections.Generic;

public static class MazeSeatedResidents
{
    public static List<int> Select(MazeGrid grid, int seed, int count, IEnumerable<int> pits)
    {
        var excluded = pits == null ? new HashSet<int>() : new HashSet<int>(pits);
        excluded.Add(0);
        foreach (int cell in grid.ParkourCells) excluded.Add(cell);
        foreach (var exit in grid.Exits) excluded.Add(exit.Cell);
        var candidates = new List<int>();
        var distance = grid.DistancesFrom(0);
        for (int cell = 1; cell < grid.Count; cell++)
        {
            if (excluded.Contains(cell) || distance[cell] < 2) continue;
            int closed = 0;
            for (int d = 0; d < 4; d++) if (!grid.IsOpen(cell,d)) closed++;
            if (closed > 0) candidates.Add(cell);
        }
        var random = new Random(seed ^ 312431);
        for (int i=candidates.Count-1; i>0; i--)
        {
            int j=random.Next(i+1); int tmp=candidates[i]; candidates[i]=candidates[j]; candidates[j]=tmp;
        }
        var selected=new List<int>();
        int wanted=Math.Max(3,count);
        // Prefer separated seats; fill any remaining places in compact maps.
        for (int pass=0; pass<3 && selected.Count<wanted; pass++)
            foreach(int cell in candidates)
            {
                if(selected.Count>=wanted) break;
                if(selected.Contains(cell)) continue;
                bool near=false;
                for(int d=0; d<4; d++) near |= selected.Contains(grid.Neighbor(cell,d));
                if(pass<2 && near) continue;
                if(pass==0 && !((grid.IsOpen(cell,0) && grid.IsOpen(cell,2)) || (grid.IsOpen(cell,1) && grid.IsOpen(cell,3)))) continue;
                selected.Add(cell);
            }
        return selected;
    }

    // Swept point against the proximity rectangle expanded by player radius.
    // Clip before entry, so a fast frame or a speed power-up cannot bypass it.
    public static float EntryFraction(float x,float z,float dx,float dz,float minX,float maxX,float minZ,float maxZ)
    {
        float enter=0,leave=1;
        if(!Slab(x,dx,minX,maxX,ref enter,ref leave) || !Slab(z,dz,minZ,maxZ,ref enter,ref leave)) return 1;
        return Math.Max(0,Math.Min(1,enter-.002f));
    }
    static bool Slab(float start,float delta,float min,float max,ref float enter,ref float leave)
    {
        if(Math.Abs(delta)<.000001f) return start>min && start<max;
        float a=(min-start)/delta,b=(max-start)/delta;
        if(a>b){float t=a;a=b;b=t;}
        enter=Math.Max(enter,a);leave=Math.Min(leave,b);
        return enter<=leave && leave>=0 && enter<=1;
    }
}
