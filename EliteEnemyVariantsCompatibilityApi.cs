using System;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

/// <summary>
/// Optional host-side integration surface for other mods. Callers should resolve
/// this type lazily and must continue without it when EliteEnemyVariants is absent.
/// </summary>
public static class EliteEnemyVariantsCompatibilityApi
{
    /// <summary>
    /// Compatibility contract version. Version 1 exposes IsEnhanced,
    /// GetRewardTierBonus, and NotifyExternalSpawn with their original signatures.
    /// </summary>
    public const int ApiVersion = 1;

    /// <summary>
    /// Returns whether host-side enhancement selection is currently enabled.
    /// </summary>
    public static bool IsActive =>
        EliteEnemyVariantsPlugin.Instance != null &&
        EliteEnemyVariantsPlugin.Instance.Settings != null &&
        EliteEnemyVariantsPlugin.Instance.Settings.Enabled.Value;

    /// <summary>
    /// Returns true when the supplied enemy component belongs to an Enhanced
    /// enemy selected by the host. Invalid components fail closed.
    /// </summary>
    public static bool IsEnhanced(Component? component)
    {
        try
        {
            return IsActive && component != null && EliteRegistry.TryGet(component, out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns one additional reward tier for an Enhanced enemy and zero for
    /// every other component.
    /// </summary>
    public static int GetRewardTierBonus(Component? component) =>
        IsEnhanced(component) ? 1 : 0;

    /// <summary>
    /// Idempotently evaluates an externally spawned enemy on the host. The
    /// method never invokes dedicated OnSpawn behavior; SpawnRPC owns that
    /// lifecycle callback. Returns whether the enemy is Enhanced after evaluation.
    /// </summary>
    public static bool NotifyExternalSpawn(EnemyParent? enemyParent)
    {
        try
        {
            if (!IsActive || enemyParent == null ||
                !SemiFunc.IsMasterClientOrSingleplayer())
            {
                return false;
            }

            // Selection and its cap/threat accounting are idempotent. SpawnRPC
            // remains solely responsible for OnSpawnComplete, whose per-spawn
            // guard prevents duplicate dedicated initialization.
            EliteRegistry.SelectOnFirstSpawn(enemyParent);
            Enemy? enemy = enemyParent.GetComponentInChildren<Enemy>(true);
            return enemy != null && EliteRegistry.IsEnhanced(enemy);
        }
        catch (Exception exception)
        {
            EliteEnemyVariantsPlugin.ModLogger?.LogDebug(
                $"External enemy spawn notification was ignored: {exception.Message}");
            return false;
        }
    }
}
