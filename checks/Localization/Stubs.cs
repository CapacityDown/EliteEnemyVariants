namespace UnityEngine
{
    public enum HideFlags { HideAndDontSave }
    public class Texture2D { public int width = 2048, height = 1024; }
    public class Shader { public static Shader? Available = new(); public static Shader? Find(string name) => Available; }
    public class Material(Shader shader)
    {
        public string name = ""; public HideFlags hideFlags; public Shader? shader = shader;
        public Texture2D? mainTexture; public Dictionary<string,float> Values = new();
        public void SetFloat(string key, float value) => Values[key] = value;
    }
}
namespace TMPro
{
    public class TMP_Text { public bool useTextShaper, isRightToLeftText; }
    public class TMP_FontAsset
    {
        public string name = "Japanese font"; public UnityEngine.Texture2D? atlasTexture;
        public UnityEngine.Material? material; public int atlasPadding = 5;
        public float normalStyle = 0, boldStyle = .75f; public int Definitions;
        public void ReadFontAssetDefinition() { if (material?.mainTexture == null) throw new Exception("Missing material"); Definitions++; }
    }
    public static class TMP_Settings { public static TMP_FontAsset? defaultFontAsset; }
}
public class LocalizationManager { public static LocalizationManager? instance; }
public static class SemiFunc
{
    public static string Locale = "en-US"; public static bool Throw;
    public static string LocaleGetCode() => Throw ? throw new InvalidOperationException("Loading locale") : Locale;
}
namespace REPOJP.EliteEnemyVariants
{
    internal static class EnemyDisplayNameResolver { internal static string Get(EliteArchetype enemy) => enemy.ToString(); }
}
