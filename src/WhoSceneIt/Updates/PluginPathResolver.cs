using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;

namespace WhoSceneIt.Updates
{
    public static class PluginPathResolver
    {
        public static string Resolve(IApplicationPaths paths, IApplicationHost host, Assembly loaded)
        {
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));
            var identity = loaded.GetName();
            var comparer = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var pluginsRoot = string.IsNullOrWhiteSpace(paths?.PluginsPath) ? null : paths.PluginsPath;
            if (pluginsRoot != null && !Path.IsPathRooted(pluginsRoot))
                throw Unavailable("Emby's plugins directory is not an absolute path.");

            // Emby can load plugin bytes into memory, leaving Assembly.Location empty.
            // Its registered IPlugin metadata retains the original installed filename.
            var registered = (host?.Plugins ?? Array.Empty<MediaBrowser.Common.Plugins.IPlugin>())
                .Where(plugin => plugin != null && plugin.Id == new Guid(UpdateIntegrity.PluginId) && plugin.GetType().Assembly == loaded)
                .Select(plugin => plugin.AssemblyFilePath).Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => RegisteredPath(path, pluginsRoot)).Distinct(comparer).ToArray();
            if (registered.Length > 1) throw Unavailable("Emby registered more than one installation path for this plugin.");
            if (registered.Length == 1) return Validate(registered[0], identity);

            if (pluginsRoot != null && Directory.Exists(pluginsRoot))
            {
                var matches = Directory.EnumerateFiles(pluginsRoot, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase))
                    .Where(path => HasAssemblyName(path, identity.Name)).Select(Path.GetFullPath).Distinct(comparer).ToArray();
                if (matches.Length > 1) throw Unavailable("More than one Who Scene It DLL exists in Emby's plugins directory. Resolve the duplicate before updating.");
                if (matches.Length == 1) return Validate(matches[0], identity);
            }

            var location = loaded.IsDynamic ? null : loaded.Location;
            if (!string.IsNullOrWhiteSpace(location) && Path.IsPathRooted(location))
            {
                var full = Path.GetFullPath(location);
                // A configured plugins directory takes precedence over a shadow-copy/cache location.
                var prefix = pluginsRoot == null ? null : Path.GetFullPath(pluginsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (prefix == null || full.StartsWith(prefix, Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    return Validate(full, identity);
            }
            throw Unavailable("Emby did not provide a usable installed DLL path.");
        }

        private static string RegisteredPath(string value, string pluginsRoot)
        {
            if (Path.IsPathRooted(value)) return Path.GetFullPath(value);
            if (pluginsRoot != null && value == Path.GetFileName(value)) return Path.GetFullPath(Path.Combine(pluginsRoot, value));
            throw Unavailable("Emby's registered plugin path is not an absolute path or a plugin filename.");
        }
        private static bool HasAssemblyName(string path, string expected)
        {
            try { return AssemblyName.GetAssemblyName(path).Name == expected; }
            catch (BadImageFormatException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        private static string Validate(string path, AssemblyName expected)
        {
            if (!File.Exists(path)) throw Unavailable("The registered installed DLL does not exist.");
            var actual = AssemblyName.GetAssemblyName(path);
            if (actual.Name != expected.Name || actual.Name != "WhoSceneIt") throw Unavailable("The installation path identifies a different application.");
            if (actual.Version != expected.Version) throw Unavailable("The DLL on disk differs from the loaded plugin version. Restart Emby Server before updating.");
            return Path.GetFullPath(path);
        }
        private static InvalidOperationException Unavailable(string reason) => new InvalidOperationException(reason + " Stop Emby Server and replace the existing Who Scene It DLL manually if this cannot be resolved. No installed files were changed.");
    }
}
