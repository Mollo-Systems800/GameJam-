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
        return "PASS: " + count + " character-space samples; normalized non-negative weights, valid bones, mirrored limbs, foot/hand/torso mapping.";
    }
}
'@)
[PlayerRigChecks]::Run()
