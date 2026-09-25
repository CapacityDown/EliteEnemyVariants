using System;

namespace REPOJP.EliteEnemyVariants;

internal static class SelectionRollSource
{
    internal static ulong CreateStageSeed(long configuredSeed, int stageOrdinal)
    {
        ulong seed = configuredSeed > 0
            ? unchecked((ulong)configuredSeed)
            : unchecked((ulong)DateTime.UtcNow.Ticks ^ ((ulong)Environment.TickCount << 32));
        return Mix(seed ^ unchecked((ulong)(uint)stageOrdinal * 0x9E3779B97F4A7C15UL));
    }

    internal static float RollPercent(ulong stageSeed, int candidateKey)
    {
        ulong value = Mix(stageSeed ^ unchecked((ulong)(uint)candidateKey * 0xD6E8FEB86659FD93UL));
        float roll = (float)((value >> 11) * (100.0 / 9007199254740992.0));
        return roll >= 100f ? 99.99999f : roll;
    }

    internal static ulong RollPriority(ulong stageSeed, int candidateKey) =>
        Mix(stageSeed ^ unchecked((ulong)(uint)candidateKey * 0xA24BAED4963EE407UL) ^
            0x9FB21C651E98DF25UL);

    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
