using System;
using System.Collections.Generic;

// Lengths refer to empty space between collider edges, not centre spacing.
public sealed class MazeParkourLayout
{
    public readonly struct Platform
    {
        public readonly float Start, Length;
        public float Centre => Start + Length * 0.5f;
        public Platform(float start, float length) { Start = start; Length = length; }
    }
    public readonly List<Platform> Platforms = new List<Platform>();
    public readonly float Gap;

    public MazeParkourLayout(float length, float desiredGap)
    {
        length = Math.Max(2, length);
        float lip = Math.Min(0.9f, length * 0.18f);
        float available = length - lip * 2;
        desiredGap = Math.Max(1.25f, Math.Min(desiredGap, available));
        int middle = Math.Max(0, (int)Math.Floor((available - desiredGap + 0.0001f) / (desiredGap + 0.9f)));
        float middleLength = middle == 0 ? 0 : (available - (middle + 1) * desiredGap) / middle;
        // A short section gets larger safe banks, never one oversized gap.
        if (middle == 0) lip = (length - desiredGap) * 0.5f;
        Gap = desiredGap;
        Platforms.Add(new Platform(0, lip));
        float cursor = lip + Gap;
        for (int i = 0; i < middle; i++)
        {
            Platforms.Add(new Platform(cursor, middleLength));
            cursor += middleLength + Gap;
        }
        Platforms.Add(new Platform(length - lip, lip));
    }
}
