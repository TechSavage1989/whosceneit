"""Package the built DLL, reproducible source, manifests, and review notes."""
from pathlib import Path
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile

from check_layout import check
check()

root = Path(__file__).resolve().parents[1]
version = ET.parse(root / "src/WhoSceneIt/WhoSceneIt.csproj").findtext(".//Version")
dll = root / "src/WhoSceneIt/bin/Release/netstandard2.0/WhoSceneIt.dll"
if not dll.is_file():
    raise SystemExit("Build the Release plugin first.")
dist = root / "dist"
dist.mkdir(exist_ok=True)
data = dll.read_bytes()
sha = hashlib.sha256(data).hexdigest()
repository = "TechSavage1989/whosceneit"
release_root = "https://github.com/" + repository + "/releases/"
manifest = {
    "SchemaVersion": 1, "AppId": "WhoSceneIt", "PluginId": "b6405a2e-52e6-4218-a014-00a46a65f1f8",
    "Repository": repository, "Version": version, "MinimumEmbyVersion": "4.8.0.0",
    "ReleaseUrl": release_root + "tag/v" + version,
    "Notes": ["Visible saved-rule list with status, user, eligibility, schedules, triggers, and owner actions.", "Existing cleanup rules, enabled states, schedules, and protection are preserved across updates and restarts.", "Correct startup status and preservation checks for missing or incompatible rule data."],
    "HasMoreNotes": True,
    "Asset": {"FileName": "WhoSceneIt.dll", "DownloadUrl": release_root + "download/v" + version + "/WhoSceneIt.dll", "Sha256": sha, "SizeBytes": len(data)}
}
(dist / "WhoSceneIt.dll").write_bytes(data)
(dist / "update-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
try:
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
    dirty = bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=root, text=True).strip())
except subprocess.CalledProcessError:
    commit, dirty = None, True
metadata = {"AppId": "WhoSceneIt", "Version": version, "SourceCommit": commit, "WorkingTreeDirty": dirty,
            "PluginSha256": sha, "TargetFramework": "netstandard2.0", "EmbySdk": "4.8.0.80", "Status": "prepared-preview"}
(dist / "build-metadata.json").write_text(json.dumps(metadata, indent=2) + "\n")
excluded = {".git", "bin", "obj", "dist", "node_modules", "test-artifacts", "__pycache__"}
source = sorted(p for p in root.rglob("*") if p.is_file() and not any(x in excluded for x in p.relative_to(root).parts))
def add(archive, name, content):
    info = zipfile.ZipInfo(name, date_time=(2026, 10, 25, 0, 0, 0))
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = 0o100644 << 16
    archive.writestr(info, content)
source_zip = dist / ("WhoSceneIt-v" + version + "-Source.zip")
with zipfile.ZipFile(source_zip, "w") as archive:
    for p in source: add(archive, p.relative_to(root).as_posix(), p.read_bytes())
complete = dist / ("WhoSceneIt-v" + version + "-Preview.zip")
with zipfile.ZipFile(complete, "w") as archive:
    add(archive, "WhoSceneIt.dll", data)
    for name, source_path in [("README.md", "README.md"), ("CHANGELOG.md", "docs/CHANGELOG.md"), ("VALIDATION.md", "docs/VALIDATION.md")]: add(archive, name, (root / source_path).read_bytes())
    for name in ["docs/CHANGELOG.md", "docs/VALIDATION.md"]: add(archive, name, (root / name).read_bytes())
    for name in ["update-manifest.json", "build-metadata.json"]: add(archive, name, (dist / name).read_bytes())
    for p in source: add(archive, "Source/" + p.relative_to(root).as_posix(), p.read_bytes())
    checks = "".join(hashlib.sha256(archive.read(name)).hexdigest() + "  " + name + "\n" for name in archive.namelist())
    add(archive, "CONTENTS_SHA256.txt", checks.encode())
names = ["WhoSceneIt.dll", source_zip.name, complete.name, "update-manifest.json", "build-metadata.json"]
(dist / "SHA256SUMS.txt").write_text("".join(hashlib.sha256((dist / name).read_bytes()).hexdigest() + "  " + name + "\n" for name in names))
print(json.dumps({"Version": version, "PluginSha256": sha, "Files": names + ["SHA256SUMS.txt"]}, indent=2))
