using System.Reflection;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteBuild
{
    internal static readonly int Number = ReadNumber();
    internal static string Label => $"EliteEnemyVariants v{EliteEnemyVariantsPlugin.PluginVersion}(build-{Number})";
    private static int ReadNumber()
    {
        foreach (var attribute in typeof(EliteBuild).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            if (attribute.Key == "BuildNumber" && int.TryParse(attribute.Value, out int number)) return number;
        return 0;
    }
}
