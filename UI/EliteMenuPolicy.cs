using System;
using System.Globalization;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteMenuPolicy
{
    internal const long AllEnemies = (1L << 30) - 1;
    internal static bool CanEdit(bool ready, bool multiplayer, bool inRoom, bool master) =>
        ready && (inRoom ? master : !multiplayer);
    internal static float ButtonTop(float rolesBottom, float eventsBottom) =>
        Math.Max(36f, Math.Max(rolesBottom, eventsBottom) + 12f);
    internal static float WheelStep(float wheel, float unitsPerNotch, float travel, float handle)
    {
        if (wheel == 0 || unitsPerNotch <= 0 || travel <= 0 || handle <= 0 ||
            !Finite(wheel) || !Finite(unitsPerNotch) || !Finite(travel) || !Finite(handle)) return 0;
        float step = wheel / unitsPerNotch * 3f * EliteMenuLayout.BodyLineStep * handle / travel;
        return float.IsNaN(step) || float.IsInfinity(step) ? 0 : step;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    internal static string Encode(int actor, long mask, bool active, int revision) =>
        string.Join(":", "1", actor.ToString(CultureInfo.InvariantCulture), mask.ToString(CultureInfo.InvariantCulture),
            active ? "1" : "0", revision.ToString(CultureInfo.InvariantCulture));
    internal static bool TryDecode(string? payload, int master, out long mask, out bool active)
    {
        mask = 0; active = false;
        if (string.IsNullOrEmpty(payload) || payload!.Length > 128 || master <= 0) return false;
        string[] fields = payload.Split(':');
        if (fields.Length != 5 || fields[0] != "1" ||
            !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int actor) || actor != master ||
            !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ||
            (parsed & ~AllEnemies) != 0 || (fields[3] != "0" && fields[3] != "1") ||
            !int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out _)) return false;
        mask = parsed; active = fields[3] == "1"; return true;
    }
}
