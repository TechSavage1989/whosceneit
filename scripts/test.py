"""Build the plugin and an unpublished upgrade fixture, then run its checks."""
from pathlib import Path
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
project = root / "src/WhoSceneIt/WhoSceneIt.csproj"
fixture = root / "test-artifacts/upgrade-fixture"
dotnet = os.environ.get("WSI_DOTNET", "dotnet")
current = tuple(map(int, ET.parse(project).findtext(".//Version").split(".")))
candidate = ".".join(map(str, (current[0], current[1], current[2] + 1)))

def run(*arguments):
    command = [dotnet, *map(str, arguments)]
    if arguments[0] != "run":
        subprocess.run(command, cwd=root, check=True)
        return
    lines = []
    with subprocess.Popen(command, cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT) as process:
        for line in process.stdout:
            print(line, end="", flush=True)
            lines.append(line)
        if process.wait() != 0:
            raise subprocess.CalledProcessError(process.returncode, command)
    if not re.search(r"^RESULT \d+ checks passed\.$", "".join(lines), re.MULTILINE):
        raise RuntimeError("The compiled test suite exited before reporting completion.")

run("build", project, "-c", "Release", "-p:UseSharedCompilation=false", "-o", fixture,
    "-p:Version=" + candidate, "-p:AssemblyVersion=" + candidate + ".0", "-p:FileVersion=" + candidate + ".0")
run("build", project, "-c", "Release", "-p:UseSharedCompilation=false")
run("run", "--project", root / "tests/WhoSceneIt.Tests/WhoSceneIt.Tests.csproj", "-c", "Release",
    "-p:UseSharedCompilation=false", "--", fixture / "WhoSceneIt.dll", *sys.argv[1:])
