namespace REPOJP.EliteEnemyVariants;

// Roles' shared row format, without a runtime dependency on RoleShuffle.
internal enum EliteMenuRowKind { Body, EnemyHeading, Setting, Action, Message, Space }

internal static class EliteMenuLayout
{
    internal const float BodyHeight = 25f;
    internal const float RowPadding = 3f;
    internal const float RowSpacing = 1.5f;
    internal const float BodyLineStep = BodyHeight + RowPadding * 2 + RowSpacing;
    internal static float Height(EliteMenuRowKind kind) => kind switch
    {
        EliteMenuRowKind.EnemyHeading => 64f,
        EliteMenuRowKind.Setting => 56f,
        EliteMenuRowKind.Action => 40f,
        EliteMenuRowKind.Message => 80f,
        EliteMenuRowKind.Space => 10f,
        _ => BodyHeight
    };
    internal static float FontSize(EliteMenuRowKind kind) =>
        kind is EliteMenuRowKind.EnemyHeading or EliteMenuRowKind.Setting ? 20f : 18f;
    internal static bool Bold(EliteMenuRowKind kind) =>
        kind is EliteMenuRowKind.EnemyHeading or EliteMenuRowKind.Setting or EliteMenuRowKind.Action;
    internal static bool Control(EliteMenuRowKind kind) =>
        kind is EliteMenuRowKind.Setting or EliteMenuRowKind.Action;
    internal static bool Wrap(EliteMenuRowKind kind) => kind == EliteMenuRowKind.Message;
}
