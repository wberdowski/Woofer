using System.Reflection;

namespace Woofer.Core.Helpers
{
    internal static class AssemblyHelper
    {
        public static string? GetVersion()
        {
            return Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        }
    }
}