using System.Reflection;

namespace Daymark.Windows;

internal static class BuildInfo
{
    public static string Version => typeof(BuildInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "development";
}
