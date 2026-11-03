namespace UnityEngine
{
    public struct Vector2 { public float y; }
    public struct Rect { public float height; }
    public class RectTransform { public Vector2 sizeDelta; public Rect rect; }
    public static class Mathf
    {
        public static float Sign(float value) => value >= 0 ? 1 : -1;
        public static float Abs(float value) => Math.Abs(value);
    }
}
namespace UnityEngine.InputSystem
{
    public static class InputSystem { internal static float scrollWheelDeltaPerTick { get; set; } = 1f; }
}
public class MenuScrollBox
{
    public float scrollWheelStep = 15, scrollHeight = 1000, scrollerStartPosition = 1000, scrollerEndPosition;
    public UnityEngine.RectTransform scrollHandle = new() { sizeDelta = new() { y = 40 } };
    public UnityEngine.RectTransform scrollBarBackground = new() { rect = new() { height = 1000 } };
}
public static class SemiFunc
{
    public static float Scroll;
    public static float InputScrollY() => Scroll;
}
namespace REPOJP.EliteEnemyVariants
{
    internal static class EliteMenu
    {
        internal static readonly MenuScrollBox Box = new();
        internal static bool Open = true;
        internal static bool OwnsScrollBox(MenuScrollBox box) => Open && ReferenceEquals(Box, box);
    }
}
namespace REPOJP.StageRoles
{
    internal static class RoleMenu
    {
        internal static readonly MenuScrollBox Box = new();
        internal static bool TryGetScrollSettings(MenuScrollBox box, out float lineHeight)
        { lineHeight = 32.5f; return ReferenceEquals(Box, box); }
    }
}
