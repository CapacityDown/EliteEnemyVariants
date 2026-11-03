using System;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteGameLocale
{
    // Local UI only: never derive the player's language from the host/config/network.
    internal static string Code
    {
        get
        {
            try { return LocalizationManager.instance != null ? SemiFunc.LocaleGetCode() : "en-US"; }
            catch (Exception) { return "en-US"; } // Scene/locale tables may still be loading; retry next refresh.
        }
    }
    internal static EliteLanguage Language => EliteLocale.FromLocaleCode(Code);
}
