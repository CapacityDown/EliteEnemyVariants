using System.Globalization;
using System.Text.Json;
using Mono.Cecil;
using REPOJP.EliteEnemyVariants;
using TMPro;
using UnityEngine;

int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
var codes = new Dictionary<string,EliteLanguage> {
 ["en-US"]=EliteLanguage.English,["ja-JP"]=EliteLanguage.Japanese,["ko-KR"]=EliteLanguage.Korean,
 ["zh-CN"]=EliteLanguage.ChineseSimplified,["zh-TW"]=EliteLanguage.ChineseTraditional,
 ["fr-FR"]=EliteLanguage.French,["de-DE"]=EliteLanguage.German,["es-ES"]=EliteLanguage.Spanish,
 ["es-419"]=EliteLanguage.SpanishLatinAmerica,["pt-BR"]=EliteLanguage.PortugueseBrazilian,["pt-PT"]=EliteLanguage.PortuguesePortugal,
 ["it-IT"]=EliteLanguage.Italian,["ru-RU"]=EliteLanguage.Russian,["pl-PL"]=EliteLanguage.Polish,["tr-TR"]=EliteLanguage.Turkish,
 ["uk-UA"]=EliteLanguage.Ukrainian,["ar-001"]=EliteLanguage.Arabic,["da-DK"]=EliteLanguage.Danish,
 ["fi-FI"]=EliteLanguage.Finnish,["sv-SE"]=EliteLanguage.Swedish,["th-TH"]=EliteLanguage.Thai
};
Check(codes.Count == 21 && codes.Values.Distinct().Count() == 21, "all native locales");
Check(EliteGameLocale.Language == EliteLanguage.English, "locale manager not ready");
LocalizationManager.instance = new();
var english = EliteText.Catalog(EliteLanguage.English);
Check(english.Count == 66, "36 menu strings and 30 descriptions");
foreach (var (code,language) in codes)
{
    Check(EliteLocale.FromLocaleCode(code) == language, "native code " + code);
    Check(EliteLocale.FromLocaleCode(code.ToUpperInvariant().Replace('-','_')) == language, "case/underscore " + code);
    SemiFunc.Locale = code; Check(EliteGameLocale.Language == language, "local game setting " + code);
    var catalog = EliteText.Catalog(language);
    Check(catalog.Keys.Order().SequenceEqual(english.Keys.Order()), "complete catalog " + code);
    foreach (var (key,value) in catalog)
    {
        Check(!string.IsNullOrWhiteSpace(value) && !value.Contains('\uFFFD'), "nonempty Unicode translation " + code + key);
        Check(EliteText.Get(key,language) == value, "no implicit fallback " + code + key);
        Check(!value.Contains("RoleShuffle"), "no copied product reference " + code + key);
        Check(!value.Contains('<') && !value.Contains('>'), "plain text " + code + key);
    }
    foreach (var enemy in Enum.GetValues<EliteArchetype>())
    {
        string key = "Enemy." + enemy;
        Check(catalog.ContainsKey(key), "all enemies " + code + enemy);
        if (language != EliteLanguage.English) Check(catalog[key] != english[key], "translated enemy " + code + enemy);
        else Check(catalog[key] == EliteGuideCatalog.Description(enemy), "English guide equivalence " + enemy);
    }
    TMP_Text text = new() { isRightToLeftText = true };
    EliteTextLayout.Apply(text,language);
    Check(text.useTextShaper && text.isRightToLeftText == (language == EliteLanguage.Arabic), "direction reset " + code);
}
SemiFunc.Throw = true; Check(EliteGameLocale.Language == EliteLanguage.English, "temporary native exception");
SemiFunc.Throw = false; SemiFunc.Locale = "ja-JP"; Check(EliteGameLocale.Language == EliteLanguage.Japanese, "retry after loading");
foreach (string? code in new string?[] {null,"","xx-XX"}) Check(EliteLocale.FromLocaleCode(code) == EliteLanguage.English, "unknown fallback");
Check(EliteLocale.FromLocaleCode("zh-Hant-HK") == EliteLanguage.ChineseTraditional, "traditional alias");
Check(EliteLocale.FromLocaleCode("es-MX") == EliteLanguage.SpanishLatinAmerica, "Latin America alias");
foreach (string phrase in new[] { "ความเสียหายเพิ่มขึ้น", "เริ่มต้น", "น้ำ", "العربية 30 m", "日本語の説明", "Two words\nNext line" })
{
    var tokens = EliteTextLayout.Tokens(phrase);
    Check(string.Concat(tokens) == phrase, "graphemes preserved " + phrase);
    Check(tokens.All(t=>CharUnicodeInfo.GetUnicodeCategory(t,0) != UnicodeCategory.NonSpacingMark), "no detached mark");
    Check(tokens.All(t=>t[^1] is < '\u0e40' or > '\u0e44'), "no detached Thai leading vowel");
}
TMP_FontAsset font = new() { atlasTexture = new Texture2D() };
Check(EliteFontMaterial.Ensure(font), "Unity 6 legacy Japanese material repaired");
Check(font.material?.mainTexture == font.atlasTexture && font.Definitions == 1, "atlas bound before measurement");
var material = font.material; Check(EliteFontMaterial.Ensure(font) && ReferenceEquals(material,font.material), "material repair idempotent");
Check(!EliteFontMaterial.Ensure(null) && !EliteFontMaterial.Ensure(new()), "missing atlas safe");
Shader.Available = null; Check(!EliteFontMaterial.Ensure(new() {atlasTexture=new()}), "missing shader safe");

using var mod = ModuleDefinition.ReadModule(args[0]);
Check(mod.Assembly.Name.Version.ToString() == "5.0.1.0", "assembly release version");
string Attribute(string name) => (string)mod.Assembly.CustomAttributes.Single(a=>a.AttributeType.Name==name).ConstructorArguments[0].Value;
int build = int.Parse((string)mod.Assembly.CustomAttributes.Single(a=>a.AttributeType.Name=="AssemblyMetadataAttribute" &&
    (string)a.ConstructorArguments[0].Value=="BuildNumber").ConstructorArguments[1].Value,CultureInfo.InvariantCulture);
Check(build > 93, "historical attempts included");
Check(Attribute("AssemblyFileVersionAttribute") == "5.0.1."+build, "file build number");
Check(Attribute("AssemblyInformationalVersionAttribute") == "5.0.1+build."+build, "informational build number");
Check(int.Parse(File.ReadAllText(Path.Combine(args[1],"build/number.txt"))) == build, "counter matches binary");
foreach (var language in codes.Values)
    Check(mod.Resources.Any(r=>r.Name=="REPOJP.EliteEnemyVariants.Localization."+language+".json"), "embedded catalog "+language);
foreach (string name in new[]{"CheckpointRevenge","European.ttf","Korean.otf","ChineseSimplified.otf","ChineseTraditional.otf","Arabic.ttf","Thai.ttf","OFL.txt","OFL-NotoSans.txt"})
    Check(mod.Resources.OfType<EmbeddedResource>().Any(r=>r.Name=="REPOJP.EliteEnemyVariants.Assets.Fonts."+name && r.GetResourceData().Length>0), "embedded font/license "+name);
string ui = File.ReadAllText(Path.Combine(args[1],"UI/EliteMenu.cs"));
foreach (string fragment in new[]{"spacing: 1.5f","parent, 272f","parent, 240f","parent, 208f","new Vector2(108, 54)",
    "new Vector2(190, 24)","new Color(0.72f, 0.72f, 0.72f, 0.9f)","EliteMenuLayout.Height(entry.Kind)","EliteMenuLayout.FontSize(entry.Kind)",
    "topPadding: _rows.Count == 0 ? 20f : 3f, bottomPadding: 3f","entry.Control ? 12 : 0","selected ? \"> \" : \"  \"",
    "EliteGameLocale.Code","EliteGameLocale.Language","row.DefaultFont","entry.Off","EliteMenuState.CanEdit"})
    Check(ui.Contains(fragment),"Roles layout/localization contract: "+fragment);
Check(!ui.Contains("StartsWith(\"[OFF]"),"translated OFF does not control state detection");
Check(ui.Contains("EliteMenuLayout.Bold(entry.Kind)") && ui.Contains("text.enableAutoSizing = entry.Control;"),"Roles bold and fixed guide-heading format");
Check(ui.Contains("version.labelTMP.enableAutoSizing = false;") && ui.Contains("\"  \" + T(\"Back\")"),"Roles version label and back-button indentation");
Check(ui.Contains("literal ? key : \"> \" + T(key)") && ui.Contains("Text(\"Status\");"),"Roles tool action prefix and plain status heading");
Check(ui.Contains("EliteMenuRowKind.Message") && ui.Contains("TextAlignmentOptions.TopRight"),"Roles wrapped feedback format with RTL support");
Check(english["ToolsTitle"] == "Tools" && english["Tools"] == "TOOLS", "separate heading and navigation capitalization");
Check(!ui.Contains("View.Issues") && ui.Contains("ConfirmOpenIssues(reportPage)"),"GitHub uses native dialog without replacing report view");
Check(ui.Contains("if (IssuesInputBlocked) return false;") && ui.Contains("_view != view || IssuesInputBlocked"),"underlying Escape and stale clicks blocked while modal");
Check(ui.Contains("Time.unscaledTime < _nextRefresh || IssuesInputBlocked") && ui.Contains("_view == view && !IssuesInputBlocked"),"background and post-click refresh paused while modal");
Check(ui.Contains("_page = null; CloseIssuesDialog();"),"closing Elites invalidates pending confirmation");
Check(english.ContainsKey("ExternalSite") && !english.ContainsKey("Issues"),"native external-site heading replaces inline Issues view");
var history = JsonDocument.Parse(File.ReadAllText(Path.Combine(args[1],"build/history.json"))).RootElement.GetProperty("entries").EnumerateArray().ToArray();
Check(history.Length==93 && history.Select(e=>e.GetProperty("callId").GetString()).Distinct().Count()==93,"history de-duplicated");
Check(history.Count(e=>e.GetProperty("outcome").GetString()=="failed")==19,"historical failures retained");
Check(history.Select(e=>e.GetProperty("number").GetInt32()).SequenceEqual(Enumerable.Range(1,93)),"contiguous historical numbers");
if (args.Length > 3)
{
    // Optional UI-only change audit against the exact previously deployed binary.
    var uiTypes = Directory.GetFiles(Path.Combine(args[1],"UI"),"*.cs")
        .SelectMany(f=>System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f),@"\b(?:class|enum|struct)\s+(\w+)")
        .Select(m=>m.Groups[1].Value)).ToHashSet();
    using var previous = ModuleDefinition.ReadModule(args[3]);
    var currentTypes = mod.GetTypes().ToDictionary(t=>t.FullName);
    int preserved = 0;
    foreach (var type in previous.GetTypes())
    {
        var rootType = type; while (rootType.DeclaringType != null) rootType = rootType.DeclaringType;
        if (uiTypes.Contains(rootType.Name) || rootType.Name.StartsWith('<')) continue;
        foreach (var method in type.Methods.Where(m=>m.HasBody))
        {
            var current = currentTypes[type.FullName].Methods.Single(m=>m.FullName==method.FullName);
            string Code(MethodDefinition m) => string.Join("\n",m.Body.Instructions.Select(i=>i.OpCode.Code+":"+i.Operand));
            Check(Code(method)==Code(current),"unchanged non-UI method "+method.FullName); preserved++;
        }
    }
    Console.WriteLine($"PASS: {preserved} non-UI method bodies unchanged from the previous deployed DLL.");
}
Console.WriteLine($"PASS: {checks} localization/layout/build checks; 21 locales, 30 enemies, build-{build}. UI engine behavior still requires in-game verification.");
