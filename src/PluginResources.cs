namespace Loupedeck.HaTTaPticPlugin
{
    using System;
    using System.IO;
    using System.Reflection;

    internal static class PluginResources
    {
        private static Assembly _assembly;

        public static void Init(Assembly assembly)
        {
            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        }

        public static Assembly Assembly => _assembly;

        public static string FindFile(string fileName)
        {
            if (_assembly == null)
            {
                throw new InvalidOperationException("PluginResources not initialized");
            }

            var resourceNames = _assembly.GetManifestResourceNames();
            foreach (var name in resourceNames)
            {
                if (name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }

            throw new FileNotFoundException($"Resource not found: {fileName}");
        }
    }
}
