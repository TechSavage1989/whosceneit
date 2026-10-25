using System;
using System.IO;
using System.Reflection;

namespace WhoSceneIt.Updates
{
    public static class UpdateInstaller
    {
        public static InstallTransaction Install(string downloaded, string target, string backupDirectory, ReleaseManifest release, Action<string, string> replace = null)
        {
            UpdateIntegrity.ValidateBinary(downloaded, release.Version, release.Asset.Sha256, release.Asset.SizeBytes);
            var old = AssemblyName.GetAssemblyName(target);
            if (old.Name != "WhoSceneIt" || old.Version >= UpdateIntegrity.ParseVersion(release.Version))
                throw new InvalidDataException("Installation requires an older Who Scene It plugin.");
            Directory.CreateDirectory(backupDirectory);
            var backup = Path.Combine(backupDirectory, "WhoSceneIt.dll");
            var oldHash = UpdateIntegrity.Hash(target);
            var staged = Path.Combine(Path.GetDirectoryName(target), ".whosceneit-install-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                // A same-directory copy makes replacement independent of the download volume.
                CopyFlushed(downloaded, staged);
                UpdateIntegrity.ValidateBinary(staged, release.Version, release.Asset.Sha256, release.Asset.SizeBytes);
                AtomicWriteCopy(target, backup);
                if (UpdateIntegrity.Hash(backup) != oldHash) throw new IOException("The rollback backup could not be verified.");
                try
                {
                    (replace ?? Replace)(staged, target);
                    UpdateIntegrity.ValidateBinary(target, release.Version, release.Asset.Sha256, release.Asset.SizeBytes);
                }
                catch
                {
                    if (!File.Exists(target) || UpdateIntegrity.Hash(target) != oldHash) AtomicWriteCopy(backup, target);
                    throw;
                }
                return new InstallTransaction
                {
                    Version = release.Version, Sha256 = release.Asset.Sha256, SizeBytes = release.Asset.SizeBytes,
                    PreviousVersion = old.Version.ToString(3), PreviousSha256 = oldHash
                };
            }
            finally { if (File.Exists(staged)) File.Delete(staged); }
        }

        public static void AtomicWriteCopy(string source, string target)
        {
            var temporary = target + ".wsi-" + Guid.NewGuid().ToString("N") + ".tmp";
            try { CopyFlushed(source, temporary); Replace(temporary, target); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void CopyFlushed(string source, string target)
        {
            using (var input = File.OpenRead(source))
            using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { input.CopyTo(output); output.Flush(true); }
        }
        private static void Replace(string source, string target)
        {
            if (File.Exists(target)) File.Replace(source, target, null);
            else File.Move(source, target);
        }
    }
}
