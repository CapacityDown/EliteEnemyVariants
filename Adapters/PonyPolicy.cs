using System;

namespace REPOJP.EliteEnemyVariants;

// Pure encounter policy: decisions are independent of the enhancement-selection RNG.
internal sealed class PonyPolicy
{
    internal const float FlankDistance = 2.5f;
    internal const float InterceptLead = 2f;
    internal const float InterceptDuration = 3f;
    internal const float LandingPause = 1f;
    internal const float CaptureStayLimit = 10f;
    internal const float CaptureScanInterval = 0.25f;
    internal float NextPatrolAt;
    internal float GroundReadyAt;
    internal bool CoverUsed;
    internal bool InterceptUsed;
    private float _calmSince = -1f;
    private float _captureSince = -1f;

    internal PonyPolicy(float now, float intervalRoll) => SchedulePatrol(now, intervalRoll);

    private static float Unit(float value) => float.IsNaN(value) ? 1f : Math.Max(0f, Math.Min(1f, value));
    internal static float PatrolDuration(float roll) => 20f + 10f * Unit(roll);
    internal static float InterceptDistance(float speed, float fallTime) =>
        InterceptLead + Math.Max(0f, Math.Min(1.75f, speed * fallTime));
    internal void SchedulePatrol(float now, float roll) => NextPatrolAt = now + 10f + 5f * Unit(roll);

    internal bool TryPatrol(float now, float chanceRoll, float intervalRoll)
    {
        if (now < NextPatrolAt || now < GroundReadyAt) return false;
        SchedulePatrol(now, intervalRoll);
        return chanceRoll >= 0f && chanceRoll < 0.5f;
    }

    internal void Landed(float now)
    {
        GroundReadyAt = now + 20f;
        NextPatrolAt = Math.Max(NextPatrolAt, GroundReadyAt);
    }

    // Target changes, attacks, stuns and short interruptions are not new pursuits.
    internal void ObserveCalm(float now, bool calm)
    {
        if (!calm) { _calmSince = -1f; return; }
        if (_calmSince < 0f) _calmSince = now;
        if (now - _calmSince < 3f) return;
        CoverUsed = false;
        InterceptUsed = false;
    }

    internal static bool CanPreferArmed(bool visible, bool allowed, bool heldWeapon, bool protectedPlayer) =>
        visible && allowed && heldWeapon && !protectedPlayer;

    internal static bool CanClearCover(bool used, bool visible, bool cooldown, bool heldItem, bool ready) =>
        !used && visible && !cooldown && !heldItem && ready;

    internal static bool CanReuseFlank(bool locked, bool sameTarget, float targetMotionSquared, float travel) =>
        locked && sameTarget && targetMotionSquared >= 0f && targetMotionSquared < 0.25f &&
        travel >= 0f && travel <= FlankDistance;

    // One fixed window per continuous native capture, never renewed by ticks.
    internal bool ObserveCeilingCapture(float now, bool active)
    {
        if (!active) { _captureSince = -1f; return false; }
        if (_captureSince < 0f) _captureSince = now;
        return now >= _captureSince && now - _captureSince < CaptureStayLimit;
    }

    internal static bool CanCapture(bool attached, bool ready, bool visible, bool tongueFree,
        bool protectedPlayer, float pause, float recovery, float cooldown) =>
        attached && ready && visible && tongueFree && !protectedPlayer &&
        pause <= 0f && recovery <= 0f && cooldown <= 0f;
}
