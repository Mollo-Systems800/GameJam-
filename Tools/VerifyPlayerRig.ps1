$ErrorActionPreference = 'Stop'
$weightsSource = Join-Path $PSScriptRoot '..\Assets\01_Scripts\PlayerRigWeights.cs'
Add-Type -TypeDefinition ((Get-Content -LiteralPath $weightsSource -Raw) + @'
public static class PlayerRigChecks
{
    public static string Run()
    {
        int count = 0;
        for (int xi = -100; xi <= 100; xi++)
        for (int yi = -20; yi <= 120; yi++)
        {
            float x = xi / 100f, y = yi / 100f;
            var w = PlayerRigWeights.Sample(x, y);
            if (Math.Abs(w.WA + w.WB + w.WC + w.WD - 1) > 0.0001f) throw new Exception("Weights do not sum to one");
            foreach (float value in new[] { w.WA, w.WB, w.WC, w.WD })
                if (float.IsNaN(value) || value < 0 || value > 1.0001f) throw new Exception("Invalid skin weight");
            foreach (int bone in new[] { w.A, w.B, w.C, w.D })
                if (bone < 0 || bone >= 10) throw new Exception("Invalid bone index");
            var mirror = PlayerRigWeights.Sample(-x, y);
            if (Math.Abs(w.WA - mirror.WA) + Math.Abs(w.WB - mirror.WB) + Math.Abs(w.WC - mirror.WC)
                + Math.Abs(w.WD - mirror.WD) > 0.0001f) throw new Exception("Asymmetric weighting");
            count++;
        }
        var leftFoot = PlayerRigWeights.Sample(-0.1f, 0.05f);
        var rightFoot = PlayerRigWeights.Sample(0.1f, 0.05f);
        if (leftFoot.B != (int)PlayerRigBone.LeftShin || leftFoot.WB < 0.99f
            || rightFoot.B != (int)PlayerRigBone.RightShin || rightFoot.WB < 0.99f) throw new Exception("Feet follow wrong leg");
        var leftHand = PlayerRigWeights.Sample(-0.25f, 0.50f);
        if (leftHand.D != (int)PlayerRigBone.LeftForearm || leftHand.WD < 0.99f) throw new Exception("Hand follows leg instead of arm");
        var torso = PlayerRigWeights.Sample(0, 0.7f);
        if (torso.C != (int)PlayerRigBone.Chest || torso.WC < 0.99f) throw new Exception("Torso follows an extremity");
        foreach (string name in new[] { "Cube.007", "Cube.005", "Cube.010", "Cube.001", "Cube.009", "Sphere" })
        for (int xi = -30; xi <= 30; xi++)
        for (int yi = 0; yi <= 100; yi++)
        {
            var w = PlayerRigWeights.SampleMesh(name, xi / 100f, yi / 100f);
            if (Math.Abs(w.WA + w.WB + w.WC + w.WD - 1) > 0.0001f) throw new Exception("Mesh weights are not normalized");
            foreach (float value in new[] { w.WA, w.WB, w.WC, w.WD })
                if (value < 0 || value > 1.0001f) throw new Exception("Invalid mesh weight");
            if (name == "Cube.007" && yi < 48 && (w.WA > 0.0001f || w.WB > 0.0001f || w.WC < 0.9999f))
                throw new Exception("Coat hem is influenced by limbs");
            if ((name == "Cube.005" || name == "Cube.010") && w.WD > 0)
                throw new Exception("Trousers/boots have arm weights");
            if ((name == "Cube.009" || name == "Sphere") && (w.WA != 1 || w.A != (int)PlayerRigBone.Chest))
                throw new Exception("Face/hair is not rigid");
            count++;
        }
        // Sleeve and exposed skin must deform together around the elbow.
        foreach (float x in new[] { -0.25f, -0.21f, -0.19f, 0.19f, 0.21f, 0.25f })
        for (int yi = 56; yi <= 72; yi++)
        {
            var skin = PlayerRigWeights.SampleMesh("Cube.001", x, yi / 100f);
            var sleeve = PlayerRigWeights.SampleMesh("Cube.007", x, yi / 100f);
            if (skin.A != sleeve.A || skin.B != sleeve.B
                || Math.Abs(skin.WA - sleeve.WA) > 0.0001f || Math.Abs(skin.WB - sleeve.WB) > 0.0001f)
                throw new Exception("Sleeve separates from the arm at the elbow");
            count++;
        }
        foreach (string name in new[] { "Cube.001", "Cube.007" })
        for (int xi = -30; xi <= 30; xi++)
        for (int yi = 0; yi <= 100; yi++)
        foreach (float forward in new[] { -0.1f, 0, 0.06f, 0.15f })
        {
            var w = PlayerRigWeights.SampleMesh(name, xi / 100f, yi / 100f, forward);
            if (Math.Abs(w.WA + w.WB + w.WC + w.WD - 1) > 0.0001f) throw new Exception("3D weights are not normalized");
            foreach (float value in new[] { w.WA, w.WB, w.WC, w.WD })
                if (float.IsNaN(value) || value < 0 || value > 1.0001f) throw new Exception("Invalid 3D skin weight");
            count++;
        }
        foreach (float x in new[] { -0.24f, -0.18f, 0.18f, 0.24f })
        foreach (float y in new[] { 0.36f, 0.40f, 0.45f, 0.50f })
        {
            var w = PlayerRigWeights.SampleMesh("Cube.001", x, y, 0.15f);
            if (w.B != (int)(x < 0 ? PlayerRigBone.LeftForearm : PlayerRigBone.RightForearm) || w.WB < 0.9999f)
                throw new Exception("Low hand/finger inherits a hip or leg weight");
        }
        foreach (float x in new[] { -0.01f, 0.01f })
        {
            var w = PlayerRigWeights.SampleMesh("Cube.005", x, 0.42f);
            if (w.WA > 0.0001f || w.WB > 0.0001f) throw new Exception("Crotch is pulled apart by opposing legs");
        }
        return "PASS: " + count + " samples; normalized 3D skin weights, rigid low hands, matching sleeve field, continuous waist and crotch attached to pelvis.";
    }
}
'@)
[PlayerRigChecks]::Run()
