"""Verify preview documentation and rebuild the exact extracted Source ZIP."""
from pathlib import Path
import hashlib
import subprocess
import sys
import tempfile
import zipfile
ROOT = Path(__file__).resolve().parents[1]
source = next((ROOT / 'dist').glob('*-Source.zip'))
preview = next((ROOT / 'dist').glob('*-Preview.zip'))
with zipfile.ZipFile(preview) as archive:
    for name in ['README.md', 'CHANGELOG.md', 'VALIDATION.md', 'docs/CHANGELOG.md', 'docs/VALIDATION.md']:
        assert archive.read(name)
    for line in archive.read('CONTENTS_SHA256.txt').decode().splitlines():
        digest, name = line.split('  ', 1)
        assert hashlib.sha256(archive.read(name)).hexdigest() == digest, name
with tempfile.TemporaryDirectory() as temp:
    target = Path(temp)
    with zipfile.ZipFile(source) as archive:
        assert archive.testzip() is None
        archive.extractall(target)
    subprocess.run([sys.executable, str(target / 'scripts/check_layout.py')], check=True)
    subprocess.run(['dotnet', 'build', str(target / 'src/WhoSceneIt/WhoSceneIt.csproj'), '-c', 'Release'], cwd=target, check=True)
print('Source ZIP extracted/rebuilt; preview contents hashes and doc paths passed.')
