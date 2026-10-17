using System;

// Deterministic timing shared by input, movement and animation. Call Step only
// while gameplay is active so pause/shop also freeze the pending action.
public sealed class PlayerRollState
{
    public bool Active { get; private set; }
    public float Progress => Math.Min(1, elapsed / duration);
    float duration = 1;
    float elapsed, cooldownRemaining, pending, unsupported;
    const float InputBuffer = 0.2f;
    const float GroundTolerance = 0.12f;

    public void Request() => pending = InputBuffer;

    public bool Step(float deltaTime, bool supported, float actionDuration, float cooldown)
    {
        float dt = Math.Max(0, deltaTime);
        cooldownRemaining = Math.Max(0, cooldownRemaining - dt);
        if (Active)
        {
            elapsed += dt;
            unsupported = supported ? 0 : unsupported + dt;
            if (elapsed >= duration || unsupported >= GroundTolerance)
            {
                Active = false;
                cooldownRemaining = Math.Max(0, cooldown);
            }
        }
        bool started = pending > 0 && !Active && supported && cooldownRemaining <= 0;
        if (started)
        {
            Active = true;
            duration = Math.Max(0.1f, actionDuration);
            elapsed = unsupported = pending = 0;
        }
        // A new press during an active roll cannot retrigger it automatically.
        pending = Active ? 0 : Math.Max(0, pending - dt);
        return started;
    }

    public void Reset()
    {
        Active = false;
        elapsed = cooldownRemaining = pending = unsupported = 0;
    }
}
