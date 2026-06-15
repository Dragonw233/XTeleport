param(
    [string]$MyPluginsRepoPath = "D:\git back\DalamudPlugins",
    [string]$LatihasRepoPath = "D:\git back\dalamud-plugins",
    [string]$ZipPath = "",
    [string]$BuildOutputDir = "",
    [string]$MyRepoJsonPath = "",
    [string]$LatihasRepoJsonPath = "",
    [string]$DownloadLink = "https://github.com/Dragonw233/XTeleport/releases/latest/download/latest.zip",
    [string]$RepoUrl = "https://github.com/Dragonw233/XTeleport",
    [switch]$SkipPush
)

$ErrorActionPreference = "Stop"

function Resolve-RequiredPath {
    param(
        [string]$Path,
        [string]$Message
    )

    if (-not (Test-Path $Path)) {
        throw $Message
    }

    return (Resolve-Path $Path).Path
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    $ZipPath = Join-Path $projectRoot "bin\x64\Release-Pro.zip"
}

if ([string]::IsNullOrWhiteSpace($BuildOutputDir)) {
    $BuildOutputDir = Join-Path $projectRoot "bin\x64\Release-Pro"
}

if ([string]::IsNullOrWhiteSpace($MyRepoJsonPath)) {
    $MyRepoJsonPath = Join-Path $MyPluginsRepoPath "pluginmaster.json"
}

if ([string]::IsNullOrWhiteSpace($LatihasRepoJsonPath)) {
    $LatihasRepoJsonPath = Join-Path $LatihasRepoPath "repo.json"
}

$zipFullPath = Resolve-RequiredPath $ZipPath "Build zip not found: $ZipPath"
$buildOutputFullPath = Resolve-RequiredPath $BuildOutputDir "Build output dir not found: $BuildOutputDir"
$myPluginsRepoFullPath = Resolve-RequiredPath $MyPluginsRepoPath "My plugins repo path not found: $MyPluginsRepoPath"
$myRepoJsonFullPath = Resolve-RequiredPath $MyRepoJsonPath "pluginmaster.json not found: $MyRepoJsonPath"
$latihasRepoFullPath = Resolve-RequiredPath $LatihasRepoPath "Latihas repo path not found: $LatihasRepoPath"
$latihasRepoJsonFullPath = Resolve-RequiredPath $LatihasRepoJsonPath "repo.json not found: $LatihasRepoJsonPath"

$manifestPath = Join-Path $projectRoot "Teleport.json"
$manifestFullPath = Resolve-RequiredPath $manifestPath "Teleport.json not found: $manifestPath"

$skipPushText = if ($SkipPush) { "1" } else { "0" }

$pythonScript = @'
import json
import pathlib
import subprocess
import sys
import zipfile
import shutil

my_repo = pathlib.Path(sys.argv[1])
my_repo_json_path = pathlib.Path(sys.argv[2])
latihas_repo = pathlib.Path(sys.argv[3])
latihas_repo_json_path = pathlib.Path(sys.argv[4])
manifest_path = pathlib.Path(sys.argv[5])
build_output_dir = pathlib.Path(sys.argv[6])
source_zip_path = pathlib.Path(sys.argv[7])
download_link = sys.argv[8]
repo_url = sys.argv[9]
skip_push = sys.argv[10] == "1"

def read_json(path: pathlib.Path):
    return json.loads(path.read_text(encoding="utf-8"))

def write_json(path: pathlib.Path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

def bump_version(version: str) -> str:
    parts = version.split(".")
    if parts and all(part.isdigit() for part in parts):
        parts[-1] = str(int(parts[-1]) + 1)
        return ".".join(parts)
    try:
        return str(round(float(version) + 0.1, 10))
    except Exception:
        return "0.1"

def git_commit_and_push(repo: pathlib.Path, paths, message: str, skip_push_flag: bool):
    subprocess.run(["git", "status", "--short"], cwd=repo, check=True)
    subprocess.run(["git", "add", "--", *[str(path) for path in paths]], cwd=repo, check=True)
    status = subprocess.run(
        ["git", "status", "--porcelain"],
        cwd=repo,
        check=True,
        capture_output=True,
        text=True,
    )
    if not status.stdout.strip():
        print(f"No changes to commit in {repo}.")
        return False

    subprocess.run(["git", "commit", "-m", message], cwd=repo, check=True)
    if skip_push_flag:
        print(f"Push skipped for {repo}.")
    else:
        subprocess.run(["git", "push"], cwd=repo, check=True)
    return True

manifest = read_json(manifest_path)
if manifest.get("InternalName") != "Teleport":
    raise SystemExit(f"Teleport.json InternalName must be Teleport, got: {manifest.get('InternalName')}")

plugin_name = manifest["InternalName"]

my_entries = read_json(my_repo_json_path)
current = next((entry for entry in my_entries if entry.get("InternalName") == plugin_name), None)
assembly_version = bump_version(current.get("AssemblyVersion", "7.0.0.0") if current else "7.0.0.0")
my_entries = [entry for entry in my_entries if entry.get("InternalName") != plugin_name]

# Keep the distributed manifest version in sync with repo versions.
manifest["AssemblyVersion"] = assembly_version
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

build_manifest_path = build_output_dir / "Teleport.json"
if build_manifest_path.exists():
    build_manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

# Step 1: publish artifact to my plugin repo.
my_plugin_dir = my_repo / "plugins" / plugin_name
my_plugin_dir.mkdir(parents=True, exist_ok=True)
target_zip = my_plugin_dir / "latest.zip"

# Repack from build output so Teleport.json is at zip root instead of under Release-Pro/.
if target_zip.exists():
    target_zip.unlink()

with zipfile.ZipFile(target_zip, "w", compression=zipfile.ZIP_DEFLATED) as zf:
    for path in sorted(build_output_dir.rglob("*")):
        if path.is_dir():
            continue
        zf.write(path, path.relative_to(build_output_dir).as_posix())

my_entry = {
    "Author": manifest["Author"],
    "Name": manifest["Name"],
    "InternalName": plugin_name,
    "AssemblyVersion": assembly_version,
    "Description": manifest["Description"],
    "ApplicableVersion": manifest["ApplicableVersion"],
    "RepoUrl": repo_url,
    "DalamudApiLevel": manifest["DalamudApiLevel"],
    "LoadRequiredState": 0,
    "LoadSync": False,
    "CanUnloadAsync": False,
    "LoadPriority": 0,
    "IconUrl": manifest["IconUrl"],
    "Punchline": manifest["Punchline"],
    "AcceptsFeedback": True,
    "DownloadLinkInstall": download_link,
    "DownloadLinkTesting": download_link,
    "DownloadLinkUpdate": download_link,
}

my_entries.append(my_entry)
my_entries.sort(key=lambda item: item.get("InternalName", ""))
write_json(my_repo_json_path, my_entries)

git_commit_and_push(
    my_repo,
    [my_repo_json_path, target_zip],
    f"Publish {plugin_name} {assembly_version}",
    skip_push,
)

# Step 2: update Latihas repo.json only.
latihas_entries = read_json(latihas_repo_json_path)
latihas_entries = [entry for entry in latihas_entries if entry.get("InternalName") != plugin_name]

latihas_entry = {
    "Author": manifest["Author"],
    "Name": manifest["Name"],
    "InternalName": plugin_name,
    "AssemblyVersion": assembly_version,
    "Description": manifest["Description"],
    "ApplicableVersion": manifest["ApplicableVersion"],
    "RepoUrl": repo_url,
    "DalamudApiLevel": manifest["DalamudApiLevel"],
    "LoadRequiredState": 0,
    "LoadSync": False,
    "CanUnloadAsync": False,
    "LoadPriority": 0,
    "IconUrl": manifest["IconUrl"],
    "Punchline": manifest["Punchline"],
    "AcceptsFeedback": True,
    "DownloadLinkInstall": download_link,
    "DownloadLinkTesting": download_link,
    "DownloadLinkUpdate": download_link,
}

latihas_entries.append(latihas_entry)
latihas_entries.sort(key=lambda item: item.get("InternalName", ""))
write_json(latihas_repo_json_path, latihas_entries)

git_commit_and_push(
    latihas_repo,
    [latihas_repo_json_path],
    f"Update {plugin_name} {assembly_version}",
    skip_push,
)
'@

$tempPy = Join-Path $env:TEMP "teleport_publish.py"
Set-Content -Path $tempPy -Value $pythonScript -Encoding utf8

python $tempPy `
    $myPluginsRepoFullPath `
    $myRepoJsonFullPath `
    $latihasRepoFullPath `
    $latihasRepoJsonFullPath `
    $manifestFullPath `
    $buildOutputFullPath `
    $zipFullPath `
    $DownloadLink `
    $RepoUrl `
    $skipPushText
