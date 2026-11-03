using System;

namespace REPOJP.EliteEnemyVariants;

internal enum EliteLanguage
{
    English,
    Japanese,
    Korean,
    ChineseSimplified,
    ChineseTraditional,
    French,
    German,
    Spanish,
    PortugueseBrazilian,
    Italian,
    Russian,
    Polish,
    Turkish,
    Ukrainian,
    Arabic,
    Danish,
    Finnish,
    Swedish,
    Thai,
    PortuguesePortugal,
    SpanishLatinAmerica
}

internal static class EliteLocale
{
    internal static readonly string[] Names = Array.ConvertAll(
        (EliteLanguage[])Enum.GetValues(typeof(EliteLanguage)), NativeName);
    internal static EliteLanguage Parse(string value)
    {
        int index = Array.IndexOf(Names, value);
        if (index >= 0) return (EliteLanguage)index;
        // Preserve settings written before native language names were introduced.
        return Enum.TryParse(value, out EliteLanguage language) && Enum.IsDefined(typeof(EliteLanguage), language)
            ? language : EliteLanguage.English;
    }
    internal static EliteLanguage FromLocaleCode(string? code)
    {
        string normalized = (code ?? string.Empty).Replace('_', '-').ToLowerInvariant();
        if (normalized.StartsWith("zh"))
            return normalized.Contains("hant") || normalized is "zh-tw" or "zh-hk" or "zh-mo"
                ? EliteLanguage.ChineseTraditional : EliteLanguage.ChineseSimplified;
        if (normalized is "pt-pt") return EliteLanguage.PortuguesePortugal;
        if (normalized is "es-419" or "es-mx" or "es-ar" or "es-cl" or "es-co" or "es-pe")
            return EliteLanguage.SpanishLatinAmerica;
        string language = normalized.Split('-')[0];
        return language switch
        {
            "ja" => EliteLanguage.Japanese, "ko" => EliteLanguage.Korean,
            "fr" => EliteLanguage.French, "de" => EliteLanguage.German,
            "es" => EliteLanguage.Spanish, "pt" => EliteLanguage.PortugueseBrazilian,
            "it" => EliteLanguage.Italian, "ru" => EliteLanguage.Russian,
            "pl" => EliteLanguage.Polish, "tr" => EliteLanguage.Turkish,
            "uk" => EliteLanguage.Ukrainian, "ar" => EliteLanguage.Arabic,
            "da" => EliteLanguage.Danish, "fi" => EliteLanguage.Finnish,
            "sv" => EliteLanguage.Swedish, "th" => EliteLanguage.Thai,
            _ => EliteLanguage.English
        };
    }
    internal static EliteLanguage Next(EliteLanguage current) =>
        (EliteLanguage)(((int)current + 1) % Names.Length);
    internal static string NativeName(EliteLanguage language) => language switch
    {
        EliteLanguage.Japanese => "日本語",
        EliteLanguage.Korean => "한국어",
        EliteLanguage.ChineseSimplified => "简体中文",
        EliteLanguage.ChineseTraditional => "繁體中文",
        EliteLanguage.French => "Français",
        EliteLanguage.German => "Deutsch",
        EliteLanguage.Spanish => "Español",
        EliteLanguage.PortugueseBrazilian => "Português (Brasil)",
        EliteLanguage.Italian => "Italiano",
        EliteLanguage.Russian => "Русский",
        EliteLanguage.Polish => "Polski",
        EliteLanguage.Turkish => "Türkçe",
        EliteLanguage.Ukrainian => "Українська",
        EliteLanguage.Arabic => "العربية",
        EliteLanguage.Danish => "Dansk",
        EliteLanguage.Finnish => "Suomi",
        EliteLanguage.Swedish => "Svenska",
        EliteLanguage.Thai => "ไทย",
        EliteLanguage.PortuguesePortugal => "Português (Portugal)",
        EliteLanguage.SpanishLatinAmerica => "Español (Latinoamérica)",
        _ => "English"
    };
    internal static bool NeedsTranslation(EliteLanguage language) =>
        language is not EliteLanguage.English and not EliteLanguage.Japanese;
}
