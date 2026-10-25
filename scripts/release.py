"""Manually invoked publication. Requires an explicitly approved source release."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess
import tempfile
import time
import urllib.request
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
repo = "TechSavage1989/whosceneit"
parser = argparse.ArgumentParser()
parser.add_argument("--version", required=True)
parser.add_argument("--preview", choices=["true", "false"], default="true")
args = parser.parse_args()
version = ET.parse(root / "src/WhoSceneIt/WhoSceneIt.csproj").findtext(".//Version")
if not re.fullmatch(r"\d+\.\d+\.\d+", args.version) or args.version != version:
    raise SystemExit("The approved version must match the source project.")
tag = "v" + version

def command(*arguments, capture=False, allowed_failure=False):
    result = subprocess.run(list(arguments), cwd=root, text=True, capture_output=capture)
    if result.returncode and not allowed_failure:
        raise SystemExit("Command failed: " + arguments[0])
    return result

sha = command("git", "rev-parse", "HEAD", capture=True).stdout.strip()
if command("git", "status", "--porcelain", capture=True).stdout.strip():
    raise SystemExit("Release source must be clean.")
metadata = json.loads((root / "dist/build-metadata.json").read_text())
if metadata["SourceCommit"] != sha or metadata["WorkingTreeDirty"]:
    raise SystemExit("Packaged source does not match the approved commit.")
checks = json.loads(command("gh", "api", f"repos/{repo}/commits/{sha}/check-runs", capture=True).stdout)
required = {"Build and test (ubuntu-latest)", "Build and test (windows-latest)"}
successful = {check["name"] for check in checks["check_runs"] if check["conclusion"] == "success"}
if not required <= successful:
    raise SystemExit("Windows and Linux CI must pass for the exact release commit first.")
existing = command("gh", "api", f"repos/{repo}/releases/tags/{tag}", capture=True, allowed_failure=True)
if existing.returncode == 0:
    raise SystemExit("This release already exists. Increment the version; do not overwrite.")
if "404" not in existing.stderr:
    raise SystemExit("Could not verify release absence safely.")
existing_tag = command("git", "ls-remote", "--tags", "origin", "refs/tags/" + tag, capture=True)
if existing_tag.stdout.strip():
    raise SystemExit("This tag already exists. Use a new version.")
command("git", "tag", tag, sha)
command("git", "push", "origin", "refs/tags/" + tag)

with tempfile.TemporaryDirectory(prefix="whosceneit-release-") as temporary:
    temp = Path(temporary)
    body = temp / "release-notes.md"
    body.write_text((root / "CHANGELOG.md").read_text() + "\nSource commit: " + sha + "\n\nSee VALIDATION.md in the preview package for completed and outstanding checks.\n")
    files = sorted(p for p in (root / "dist").iterdir() if p.is_file())
    create = ["gh", "release", "create", tag, "--repo", repo, "--verify-tag", "--target", sha,
              "--draft", "--title", "Who Scene It " + version, "--notes-file", str(body)]
    if args.preview == "true":
        create.append("--prerelease")
    command(*create)
    command("gh", "release", "upload", tag, *map(str, files), "--repo", repo)
    published = json.loads(command("gh", "api", f"repos/{repo}/releases/tags/{tag}", capture=True).stdout)
    tagged = json.loads(command("gh", "api", f"repos/{repo}/git/ref/tags/{tag}", capture=True).stdout)
    if tagged["object"]["sha"] != sha or published["tag_name"] != tag:
        raise SystemExit("The draft tag does not identify the approved source commit.")
    names = {asset["name"] for asset in published["assets"]}
    if names != {file.name for file in files}:
        raise SystemExit("Draft asset list is incomplete; the draft remains unpublished.")
    verify_dir = temp / "verify"
    verify_dir.mkdir()
    command("gh", "release", "download", tag, "--repo", repo, "--dir", str(verify_dir))
    for file in files:
        if hashlib.sha256(file.read_bytes()).digest() != hashlib.sha256((verify_dir / file.name).read_bytes()).digest():
            raise SystemExit("Uploaded asset digest mismatch; draft remains unpublished.")
    command("gh", "release", "edit", tag, "--repo", repo, "--draft=false")
    manifest = json.loads((root / "dist/update-manifest.json").read_text())
    with urllib.request.urlopen(manifest["Asset"]["DownloadUrl"], timeout=60) as response:
        public_dll = response.read(16 * 1024 * 1024 + 1)
    if hashlib.sha256(public_dll).hexdigest() != manifest["Asset"]["Sha256"]:
        raise SystemExit("Public asset digest mismatch; feed has not been advanced.")

    # Create the feed commit from the exact current main head, using a fast-forward-only push.
    # This preserves any concurrent changes and never force-pushes source or a published tag.
    command("git", "fetch", "origin", "main")
    command("git", "checkout", "--detach", "origin/main")
    (root / "update-manifest.json").write_bytes((root / "dist/update-manifest.json").read_bytes())
    command("git", "config", "user.name", "github-actions[bot]")
    command("git", "config", "user.email", "41898282+github-actions[bot]@users.noreply.github.com")
    command("git", "add", "update-manifest.json")
    command("git", "commit", "-m", "Point update feed to verified Who Scene It " + version)
    command("git", "push", "origin", "HEAD:main")
    feed_url = "https://raw.githubusercontent.com/" + repo + "/main/update-manifest.json"
    for attempt in range(12):
        try:
            request = urllib.request.Request(feed_url, headers={"Cache-Control": "no-cache"})
            with urllib.request.urlopen(request, timeout=30) as response:
                public_feed = json.load(response)
            if public_feed == manifest:
                break
        except (OSError, ValueError):
            pass
        if attempt == 11:
            raise SystemExit("Release assets are published, but the public feed could not yet be verified. Do not republish the same version.")
        time.sleep(5)
    print("Published " + tag + " at " + sha + "; all assets and public DLL digest verified.")
