using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteText
{
    private static readonly Dictionary<EliteLanguage, IReadOnlyDictionary<string, string>> Catalogs = new();
    internal static IReadOnlyDictionary<string, string> Catalog(EliteLanguage language)
    {
        if (Catalogs.TryGetValue(language, out var catalog)) return catalog;
        using Stream? stream = typeof(EliteText).Assembly.GetManifestResourceStream(
            $"REPOJP.EliteEnemyVariants.Localization.{language}.json");
        if (stream == null) return Catalogs[language] = new Dictionary<string, string>();
        var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>),
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        return Catalogs[language] = (Dictionary<string, string>)serializer.ReadObject(stream)!;
    }
    internal static string Get(string key, EliteLanguage language)
    {
        if (Catalog(language).TryGetValue(key, out string? value) && value != null) return value;
        return Catalog(EliteLanguage.English).TryGetValue(key, out value) && value != null ? value : key;
    }
}
