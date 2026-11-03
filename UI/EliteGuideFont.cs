using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteGuideFont
{
    private const string ResourceName =
        "REPOJP.EliteEnemyVariants.Assets.Fonts.CheckpointRevenge";
    private const float FallbackGlyphScale = 0.72f;

    private static bool _loadAttempted;
    private static TMP_FontAsset? _font;
    private static TMP_FontAsset? _scaledJapaneseFont;
    private static readonly Dictionary<int, TMP_FontAsset> MixedFonts = new();
    private static readonly Dictionary<(int, EliteLanguage), TMP_FontAsset> LocalizedFonts = new();
    private static readonly Dictionary<int, TMP_FontAsset> OriginalFonts = new();
    private static readonly Dictionary<string, TMP_FontAsset?> UnicodeFonts = new();
    internal static TMP_FontAsset? ForLanguage(TMP_FontAsset? primary, EliteLanguage language)
    {
        if (primary != null && OriginalFonts.TryGetValue(primary.GetInstanceID(), out var original)) primary = original;
        if (language == EliteLanguage.English) return primary;
        if (language == EliteLanguage.Japanese) return ForPrimary(primary);
        string resource = language switch
        {
            EliteLanguage.Korean => "Korean",
            EliteLanguage.Arabic => "Arabic",
            EliteLanguage.Thai => "Thai",
            EliteLanguage.ChineseTraditional => "ChineseTraditional",
            EliteLanguage.ChineseSimplified => "ChineseSimplified",
            _ => "European"
        };
        var fallback = LoadUnicode(resource);
        if (fallback == null) return primary;
        if (primary == null) return fallback;
        var key = (primary.GetInstanceID(), language);
        if (LocalizedFonts.TryGetValue(key, out var cached) && cached != null) return cached;
        var mixed = UnityEngine.Object.Instantiate(primary);
        mixed.name = $"{primary.name} + EliteEnemyVariants {language}";
        mixed.hideFlags = HideFlags.HideAndDontSave;
        mixed.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        // Arabic/Thai fonts contain their own script, not the Latin product names/punctuation.
        // Keep the game font primary, with the same Noto Sans family used by Roles as a final fallback.
        if (resource != "European" && LoadUnicode("European") is TMP_FontAsset european)
            mixed.fallbackFontAssetTable.Add(european);
        if (primary.fallbackFontAssetTable != null) mixed.fallbackFontAssetTable.AddRange(primary.fallbackFontAssetTable);
        OriginalFonts[mixed.GetInstanceID()] = primary;
        return LocalizedFonts[key] = mixed;
    }

    private static TMP_FontAsset? LoadUnicode(string name)
    {
        if (UnicodeFonts.TryGetValue(name, out var cached)) return cached;
        UnicodeFonts[name] = null;
        try
        {
            string extension = name is "European" or "Arabic" or "Thai" ? ".ttf" : ".otf";
            using var stream = typeof(EliteGuideFont).Assembly.GetManifestResourceStream(
                $"REPOJP.EliteEnemyVariants.Assets.Fonts.{name}{extension}");
            if (stream == null) throw new FileNotFoundException("Embedded font: " + name);
            byte[] bytes = ReadAllBytes(stream);
            using var sha = System.Security.Cryptography.SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            string directory = Path.Combine(BepInEx.Paths.CachePath, "EliteEnemyVariants", "Fonts");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + "-" + hash + extension);
            if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
            var source = new UnityEngine.Font(path) { hideFlags = HideFlags.HideAndDontSave };
            var font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (!EliteFontMaterial.Ensure(font)) throw new InvalidOperationException("TMP font creation failed: " + name);
            font.name = "EliteEnemyVariants " + name;
            font.hideFlags = HideFlags.HideAndDontSave;
            // Match fallback glyphs to the game's Latin text, including the
            // shared fonts used by REPOConfig's native language names.
            var faceInfo = font.faceInfo;
            faceInfo.scale *= FallbackGlyphScale;
            font.faceInfo = faceInfo;
            return UnicodeFonts[name] = font;
        }
        catch (Exception exception)
        {
            EliteEnemyVariantsPlugin.ModLogger.LogWarning($"Could not load {name} font: {exception.Message}");
            return null;
        }
    }

    internal static TMP_FontAsset? Font
    {
        get
        {
            if (!_loadAttempted)
            {
                _loadAttempted = true;
                _font = Load();
            }
            return _font;
        }
    }

    internal static TMP_FontAsset? ForPrimary(TMP_FontAsset? primary)
    {
        if (primary == null)
        {
            return Font;
        }

        TMP_FontAsset? japanese = ScaledJapaneseFont();
        if (japanese == null)
        {
            return primary;
        }

        int key = primary.GetInstanceID();
        if (MixedFonts.TryGetValue(key, out TMP_FontAsset mixed) &&
            mixed != null)
        {
            return mixed;
        }

        mixed = UnityEngine.Object.Instantiate(primary);
        mixed.name = $"{primary.name} + Checkpoint Revenge";
        mixed.hideFlags = HideFlags.HideAndDontSave;
        List<TMP_FontAsset> fallbacks = primary.fallbackFontAssetTable != null
            ? new List<TMP_FontAsset>(primary.fallbackFontAssetTable)
            : new List<TMP_FontAsset>();
        fallbacks.Remove(japanese);
        fallbacks.Insert(0, japanese);
        if (LoadUnicode("European") is TMP_FontAsset european) fallbacks.Add(european);
        mixed.fallbackFontAssetTable = fallbacks;
        MixedFonts[key] = mixed;
        OriginalFonts[mixed.GetInstanceID()] = primary;
        return mixed;
    }

    private static TMP_FontAsset? ScaledJapaneseFont()
    {
        if (_scaledJapaneseFont != null)
        {
            return _scaledJapaneseFont;
        }

        TMP_FontAsset? source = Font;
        if (source == null)
        {
            return null;
        }

        _scaledJapaneseFont = UnityEngine.Object.Instantiate(source);
        _scaledJapaneseFont.name = $"{source.name} Role Guide Scaled";
        _scaledJapaneseFont.hideFlags = HideFlags.HideAndDontSave;
        var faceInfo = _scaledJapaneseFont.faceInfo;
        faceInfo.scale *= FallbackGlyphScale;
        _scaledJapaneseFont.faceInfo = faceInfo;
        return _scaledJapaneseFont;
    }

    private static TMP_FontAsset? Load()
    {
        try
        {
            Assembly assembly = typeof(EliteGuideFont).Assembly;
            using Stream? stream =
                assembly.GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                EliteEnemyVariantsPlugin.ModLogger.LogWarning(
                    "The embedded Checkpoint Revenge font was not found.");
                return null;
            }

            byte[] bytes = ReadAllBytes(stream);
            AssetBundle? bundle = AssetBundle.LoadFromMemory(bytes);
            if (bundle == null)
            {
                EliteEnemyVariantsPlugin.ModLogger.LogWarning(
                    "The Checkpoint Revenge font bundle could not be loaded.");
                return null;
            }

            try
            {
                TMP_FontAsset[] fontAssets =
                    bundle.LoadAllAssets<TMP_FontAsset>();
                TMP_FontAsset? selected = Preferred(fontAssets);
                if (selected == null)
                {
                    Font[] sourceFonts = bundle.LoadAllAssets<Font>();
                    Font? source = Preferred(sourceFonts);
                    if (source != null)
                    {
                        selected = TMP_FontAsset.CreateFontAsset(source);
                    }
                }

                if (selected == null)
                {
                    EliteEnemyVariantsPlugin.ModLogger.LogWarning(
                        "The Checkpoint Revenge bundle contained no usable font asset.");
                    return null;
                }

                if (!EliteFontMaterial.Ensure(selected))
                {
                    EliteEnemyVariantsPlugin.ModLogger.LogWarning("Japanese font has no usable SDF material or atlas.");
                    return null;
                }
                selected.hideFlags = HideFlags.HideAndDontSave;
                EliteEnemyVariantsPlugin.ModLogger.LogInfo(
                    $"Loaded Japanese Role Guide font: {selected.name}.");
                return selected;
            }
            finally
            {
                bundle.Unload(unloadAllLoadedObjects: false);
            }
        }
        catch (Exception exception)
        {
            EliteEnemyVariantsPlugin.ModLogger.LogWarning(
                $"The Checkpoint Revenge font could not be loaded: " +
                exception.Message);
            return null;
        }
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static T? Preferred<T>(T[] assets)
        where T : UnityEngine.Object
    {
        foreach (T asset in assets)
        {
            if (asset != null &&
                (asset.name.IndexOf(
                     "checkpoint",
                     StringComparison.OrdinalIgnoreCase) >= 0 ||
                 asset.name.IndexOf(
                     "revenge",
                     StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return asset;
            }
        }
        return assets.Length > 0 ? assets[0] : null;
    }
}
