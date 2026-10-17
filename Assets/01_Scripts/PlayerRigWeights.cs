using System;

public enum PlayerRigBone { Hips, Chest, LeftThigh, LeftShin, RightThigh, RightShin, LeftArm, LeftForearm, RightArm, RightForearm }

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
        float elbow = 1 - Smooth(0.54f, 0.68f, y);
        return new Influence(left ? PlayerRigBone.LeftArm : PlayerRigBone.RightArm,
            left ? PlayerRigBone.LeftForearm : PlayerRigBone.RightForearm, PlayerRigBone.Chest,
            arm * (1 - elbow), arm * elbow);
    }
}
