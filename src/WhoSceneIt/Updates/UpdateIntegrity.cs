using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace WhoSceneIt.Updates
{
    public static class UpdateIntegrity
    {
        public const string Repository = "TechSavage1989/whosceneit";
        public const string PluginId = "b6405a2e-52e6-4218-a014-00a46a65f1f8";
        public const string FeedUrl = "https://raw.githubusercontent.com/TechSavage1989/whosceneit/main/update-manifest.json";
        public const long MaxBinarySize = 16 * 1024 * 1024;
        public const int MaxManifestSize = 128 * 1024;

        public static Version ParseVersion(string value)
        {
            if (value == null || !Regex.IsMatch(value, @"^\d{1,5}\.\d{1,5}\.\d{1,5}$") || !Version.TryParse(value, out var version))
                throw new InvalidDataException("The release version must have three numeric parts.");
            return new Version(version.Major, version.Minor, version.Build, 0);
        }
        public static void ValidateManifest(ReleaseManifest manifest, Version serverVersion)
        {
            if (manifest == null || manifest.SchemaVersion != 1 || manifest.AppId != "WhoSceneIt" ||
                !string.Equals(manifest.PluginId, PluginId, StringComparison.OrdinalIgnoreCase) || manifest.Repository != Repository)
                throw new InvalidDataException("The update feed does not identify Who Scene It.");
            var version = ParseVersion(manifest.Version);
            if (!Version.TryParse(manifest.MinimumEmbyVersion, out var minimum) || minimum > serverVersion)
                throw new InvalidDataException("This update requires a newer Emby Server version.");
            var root = "https://github.com/" + Repository + "/releases/";
            var tag = "v" + version.ToString(3);
            if (manifest.ReleaseUrl != root + "tag/" + tag || manifest.Asset == null || manifest.Asset.FileName != "WhoSceneIt.dll" ||
                manifest.Asset.DownloadUrl != root + "download/" + tag + "/WhoSceneIt.dll")
                throw new InvalidDataException("The release links are outside the trusted repository or have unexpected names.");
            if (manifest.Asset.SizeBytes < 1024 || manifest.Asset.SizeBytes > MaxBinarySize || manifest.Asset.Sha256 == null ||
                !Regex.IsMatch(manifest.Asset.Sha256, @"\A[a-fA-F0-9]{64}\z"))
                throw new InvalidDataException("The release has an invalid file size or SHA-256 digest.");
            if (manifest.Notes == null || manifest.Notes.Length > 30 || Array.Exists(manifest.Notes, x => x == null || x.Length > 1000))
                throw new InvalidDataException("The release notes are invalid.");
        }

        public static bool IsAllowedAssetHop(Uri uri, bool initial)
        {
            if (uri == null || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0) return false;
            if (initial) return uri.Host == "github.com" && uri.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.Ordinal);
            return uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com" || uri.Host == "github-releases.githubusercontent.com";
        }

        public static string Hash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        public static void ValidateBinary(string path, string version, string expectedSha256, long expectedSize)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length != expectedSize || info.Length > MaxBinarySize || !string.Equals(Hash(path), expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded file does not match its expected size and SHA-256 digest.");
            using (var stream = File.OpenRead(path)) using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt16() != 0x5a4d) throw new InvalidDataException("The download is not a PE plugin library.");
                stream.Position = 0x3c;
                var offset = reader.ReadInt32();
                if (offset < 64 || offset > stream.Length - 24) throw new InvalidDataException("Invalid PE header.");
                stream.Position = offset;
                if (reader.ReadUInt32() != 0x4550) throw new InvalidDataException("Invalid PE signature.");
                stream.Position = offset + 22;
                if ((reader.ReadUInt16() & 0x2000) == 0) throw new InvalidDataException("The download is an executable, not a DLL.");
            }
            var identity = AssemblyName.GetAssemblyName(path);
            if (identity.Name != "WhoSceneIt" || identity.Version != ParseVersion(version))
                throw new InvalidDataException("The downloaded assembly has the wrong application identity or version.");
        }
    }
}
