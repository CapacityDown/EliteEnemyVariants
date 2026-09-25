# Elite Enemy Variants compatibility API

`REPOJP.EliteEnemyVariants.EliteEnemyVariantsCompatibilityApi` is an optional,
host-side reflection contract. Integrations must continue normally when the type,
member, or plugin is unavailable.

## Contract version 1

- `public const int ApiVersion = 1`
- `public static bool IsActive { get; }`
- `public static bool IsEnhanced(UnityEngine.Component? component)`
- `public static int GetRewardTierBonus(UnityEngine.Component? component)`
- `public static bool NotifyExternalSpawn(EnemyParent? enemyParent)`

`NotifyExternalSpawn` is host-authoritative and idempotent. Repeated calls for the
same enemy do not repeat selection, Enhanced-cap accounting, Threat Budget
accounting, or dedicated `OnSpawn` initialization. The vanilla `SpawnRPC`
lifecycle remains the sole owner of dedicated `OnSpawn` initialization.
The per-spawn guard is cleared by the normal `DespawnRPC` lifecycle, so a later
legitimate respawn initializes the dedicated behavior once again.

Consumers must resolve this API lazily and treat missing or failed calls as an
optional integration failure. No client installation or custom client RPC is
required.

Elite Enemy Variants applies the same defensive rule when consuming optional
APIs. Its current RoleShuffle compatibility range is explicitly version 2
through version 2; future versions are not adopted until that range is reviewed
and intentionally updated.
