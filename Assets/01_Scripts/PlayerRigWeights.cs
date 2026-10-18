using System;

// Append bones so existing baked motion track indices remain compatible.
public enum PlayerRigBone { Hips, Chest, LeftThigh, LeftShin, RightThigh, RightShin, LeftArm, LeftForearm, RightArm, RightForearm, Spine }

// Shared character-space weights keep coincident vertices on separate clothing meshes together.
public static class PlayerRigWeights
{
    public readonly struct Influence
    {
        public readonly int A, B, C, D;
        public readonly float WA, WB, WC, WD;
        public Influence(PlayerRigBone a, PlayerRigBone b, PlayerRigBone c, float wa, float wb,
            PlayerRigBone d = PlayerRigBone.Hips, float wd = 0)
        {
            A = (int)a; B = (int)b; C = (int)c; D = (int)d;
            WA = wa; WB = wb; WD = wd; WC = Math.Max(0, 1 - wa - wb - wd);
        }
    }

    static float Smooth(float low, float high, float value)
    {
        float t = Math.Max(0, Math.Min(1, (value - low) / (high - low)));
        return t * t * (3 - 2 * t);
    }

    public static Influence Sample(float x, float y)
    {
        bool left = x < 0;
        if (y < 0.53f)
        {
            float leg = 1 - Smooth(0.42f, 0.53f, y);
            float knee = 1 - Smooth(0.23f, 0.32f, y);
            float hand = Smooth(0.10f, 0.19f, Math.Abs(x)) * Smooth(0.38f, 0.50f, y);
            return new Influence(left ? PlayerRigBone.LeftThigh : PlayerRigBone.RightThigh,
                left ? PlayerRigBone.LeftShin : PlayerRigBone.RightShin, PlayerRigBone.Hips,
                leg * (1 - knee) * (1 - hand), leg * knee * (1 - hand),
                left ? PlayerRigBone.LeftForearm : PlayerRigBone.RightForearm, hand);
        }
        float arm = Smooth(0.10f, 0.19f, Math.Abs(x)) * (1 - Smooth(0.80f, 0.89f, y));
        float elbow = ElbowWeight(y);
        return new Influence(left ? PlayerRigBone.LeftArm : PlayerRigBone.RightArm,
            left ? PlayerRigBone.LeftForearm : PlayerRigBone.RightForearm, PlayerRigBone.Chest,
            arm * (1 - elbow), arm * elbow);
    }

    // Keep the blend close to the elbow pivot instead of bending the entire sleeve.
    static float ElbowWeight(float y) => 1 - Smooth(0.57f, 0.63f, y);

    static float LegWeight(float x, float y)
    {
        // The crotch is a shared surface, not two disconnected leg halves.
        float hip = Smooth(0.30f, 0.38f, y) * (1 - Smooth(0.015f, 0.065f, Math.Abs(x)));
        return (1 - Smooth(0.44f, 0.56f, y)) * (1 - hip);
    }

    // In this source pose the hands hang below the waist, in front of the coat.
    // Height alone cannot distinguish a cuff/finger from a hip or coat hem.
    public static float ArmWeight(float x, float y, float forward, float anatomicalArm = -1)
    {
        float lowArm = Math.Max(Smooth(0.035f, 0.09f, forward),
            Smooth(0.145f, 0.17f, Math.Abs(x)) * Smooth(0.38f, 0.43f, y)) * Smooth(0.30f, 0.35f, y);
        float upperArm = Smooth(0.52f, 0.58f, y);
        float lateral = Smooth(0.08f, 0.13f, Math.Abs(x)) * (1 - upperArm)
            + Smooth(0.10f, 0.165f, Math.Abs(x)) * upperArm;
        float arm = lateral * (lowArm + (1 - lowArm) * upperArm)
            * (1 - Smooth(0.72f, 0.80f, y));
        return anatomicalArm < 0 ? arm : anatomicalArm;
    }

    public static Influence SampleMesh(string meshName, float x, float y, float forward, float anatomicalArm = -1)
    {
        bool left = x < 0;
        bool skin = meshName == "Cube.001", coat = meshName == "Cube.007";
        if (!skin && !coat) return SampleMesh(meshName, x, y);
        float arm = ArmWeight(x, y, forward, anatomicalArm);
        if (skin && y < 0.56f)
        {
            // Every finger follows one rigid forearm. No part of the hand is
            // blended with a thigh simply because it lies below the pelvis.
            float leg = LegWeight(x, y), knee = 1 - Smooth(0.23f, 0.32f, y);
            if (y >= 0.32f)
                return new Influence(left ? PlayerRigBone.LeftThigh : PlayerRigBone.RightThigh,
                    left ? PlayerRigBone.LeftForearm : PlayerRigBone.RightForearm, PlayerRigBone.Hips,
                    leg * (1 - arm), arm, PlayerRigBone.Spine,
                    (1 - leg) * (1 - arm) * Smooth(0.48f, 0.60f, y));
            return new Influence(left ? PlayerRigBone.LeftThigh : PlayerRigBone.RightThigh,
                left ? PlayerRigBone.LeftShin : PlayerRigBone.RightShin, PlayerRigBone.Hips,
                leg * (1 - knee) * (1 - arm), leg * knee * (1 - arm),
                left ? PlayerRigBone.LeftForearm : PlayerRigBone.RightForearm, arm);
        }
        float elbow = ElbowWeight(y);
        bool upper = y >= 0.60f;
        float torso = upper ? Smooth(0.62f, 0.72f, y) : Smooth(0.48f, 0.60f, y);
        return new Influence(left ? PlayerRigBone.LeftArm : PlayerRigBone.RightArm,
            left ? PlayerRigBone.LeftForearm : PlayerRigBone.RightForearm,
            upper ? PlayerRigBone.Spine : PlayerRigBone.Hips,
            arm * (1 - elbow), arm * elbow,
            upper ? PlayerRigBone.Chest : PlayerRigBone.Spine, (1 - arm) * torso);
    }

    public static Influence SampleMesh(string meshName, float x, float y)
    {
        bool left = x < 0;
        // Clothing below the waist must not inherit knee weights.
        if (meshName == "Cube.007")
        {
            // Match the exposed arm at the sleeve seam; different masks pull it apart.
            float arm = Smooth(0.10f, 0.19f, Math.Abs(x)) * Smooth(0.48f, 0.56f, y)
                * (1 - Smooth(0.80f, 0.89f, y));
            float elbow = ElbowWeight(y);
            float chest = Smooth(0.50f, 0.74f, y);
            return new Influence(left ? PlayerRigBone.LeftArm : PlayerRigBone.RightArm,
                left ? PlayerRigBone.LeftForearm : PlayerRigBone.RightForearm, PlayerRigBone.Hips,
                arm * (1 - elbow), arm * elbow, PlayerRigBone.Chest, (1 - arm) * chest);
        }
        if (meshName == "Cube.005" || meshName == "Cube.010" || (meshName == "Cube.001" && y < 0.3f))
        {
            float leg = LegWeight(x, y);
            float knee = 1 - Smooth(0.23f, 0.32f, y);
            return new Influence(left ? PlayerRigBone.LeftThigh : PlayerRigBone.RightThigh,
                left ? PlayerRigBone.LeftShin : PlayerRigBone.RightShin, PlayerRigBone.Hips,
                leg * (1 - knee), leg * knee);
        }
        if (meshName == "Cube.001") return Sample(x, y);
        // Hair, eyes, eyebrows and tie remain rigid instead of stretching independently.
        return new Influence(PlayerRigBone.Chest, PlayerRigBone.Hips, PlayerRigBone.Chest, 1, 0);
    }
}
