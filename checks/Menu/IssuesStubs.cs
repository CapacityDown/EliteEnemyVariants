using System.Reflection;

namespace UnityEngine
{
    public class UiObject
    {
        public bool Destroyed;
        public static bool operator ==(UiObject? a, UiObject? b)
        {
            bool an = ReferenceEquals(a, null) || a.Destroyed;
            bool bn = ReferenceEquals(b, null) || b.Destroyed;
            return an || bn ? an == bn : ReferenceEquals(a, b);
        }
        public static bool operator !=(UiObject? a, UiObject? b) => !(a == b);
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public readonly record struct Color(float r, float g, float b);
    public static class Application
    {
        public static readonly List<string> Opened = new();
        public static bool Fail;
        public static void OpenURL(string url) { if (Fail) throw new IOException("browser unavailable"); Opened.Add(url); }
    }
}
namespace TMPro
{
    public enum TextWrappingModes { NoWrap, Normal }
    public class TMP_FontAsset;
    public class TextMeshProUGUI
    {
        public TMP_FontAsset font = new();
        public TextWrappingModes textWrappingMode;
        public bool enableAutoSizing, Shaped;
        public float fontSizeMin, fontSizeMax;
    }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    }
}
public class MenuButton { public string buttonTextString = ""; }
public class MenuPageTwoOptions
{
    public readonly MenuButton option1Button = new(), option2Button = new();
    public readonly TMPro.TextMeshProUGUI bodyTextMesh = new();
}
public class MenuPage : UnityEngine.UiObject
{
    public enum PageState { Open, Closing }
    public PageState State;
    public readonly MenuPageTwoOptions Dialog = new();
    public readonly TMPro.TextMeshProUGUI Header = new();
    public T? GetComponent<T>() where T : class => Dialog as T;
    public T[] GetComponentsInChildren<T>(bool includeInactive) => new object[] { Header, Dialog.bodyTextMesh }.OfType<T>().ToArray();
    public void PageStateSet(PageState value) => State = value;
}
public class MenuManager
{
    public static readonly MenuManager instance = new();
    private MenuPage? currentMenuPage;
    public MenuPage Current => currentMenuPage!;
    public void Create() => currentMenuPage = new();
}
namespace MenuLib.MonoBehaviors { public class REPOPopupPage : UnityEngine.UiObject; }
namespace MenuLib
{
    public static class MenuAPI
    {
        public static int Opens;
        public static string Header = "", Body = "";
        public static UnityEngine.Color Color;
        public static Action Confirm = null!, Cancel = null!;
        public static void OpenPopup(string title, UnityEngine.Color color, string content, Action yes, Action no)
        { Opens++; Header = title; Color = color; Body = content; Confirm = yes; Cancel = no; MenuManager.instance.Create(); }
    }
}
namespace REPOJP.EliteEnemyVariants
{
    internal static class EliteGuideFont
    {
        internal static readonly TMPro.TMP_FontAsset Font = new();
        internal static TMPro.TMP_FontAsset ForLanguage(TMPro.TMP_FontAsset original, EliteLanguage language) => Font;
    }
    internal static class EliteTextLayout
    {
        internal static void Apply(TMPro.TextMeshProUGUI text, EliteLanguage language) => text.Shaped = true;
    }
}
