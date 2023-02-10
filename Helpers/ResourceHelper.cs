using System.IO;
using System.Reflection;

namespace Penguin.Persistence.Database.Helpers
{
    internal static class ResourceHelper
    {
        internal static string ReadEmbeddedScript(string Name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = $"Penguin.Persistence.Database.Scripts.{Name}";

            string result = string.Empty;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using StreamReader reader = new(stream);
                result = reader.ReadToEnd();
            }

            return result;
        }
    }
}