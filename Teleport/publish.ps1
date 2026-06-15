param(
    [string]$GitHubToken = $env:GITHUB_TOKEN,
    [string]$RepoOwner = "Dragonw233",
    [string]$RepoName = "XTeleport",
    [string]$ReleaseTag = "",
    [string]$Version = "",
    [string]$MyPluginsRepoPath = "D:\git back\DalamudPlugins",
    [string]$LatihasRepoPath = "D:\git back\dalamud-plugins",
    [string]$ZipPath = "",
    [string]$BuildOutputDir = "",
    [string]$MyRepoJsonPath = "",
    [string]$LatihasRepoJsonPath = "",
    [switch]$SkipPush,
    [switch]$SkipReleaseUpload,
    [switch]$SkipRepoMirror
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

function Invoke-GitHubJson {
    param(
        [string]$Method,
        [string]$Uri,
        [object]$Body = $null
    )

    $headers = @{
        Authorization = "Bearer $GitHubToken"
        Accept = "application/vnd.github+json"
        "User-Agent" = "TeleportPublish"
        "X-GitHub-Api-Version" = "2022-11-28"
    }

    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers
    }

    $json = $Body | ConvertTo-Json -Depth 10
    return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers -Body $json -ContentType "application/json"
}

function Invoke-GitHubBinaryUpload {
    param(
        [string]$Uri,
        [string]$FilePath,
        [string]$ContentType
    )

    $headers = @{
        Authorization = "Bearer $GitHubToken"
        Accept = "application/vnd.github+json"
        "User-Agent" = "TeleportPublish"
        "X-GitHub-Api-Version" = "2022-11-28"
        "Content-Type" = $ContentType
    }

    Invoke-RestMethod -Method Post -Uri $Uri -Headers $headers -InFile $FilePath
}

if ([string]::IsNullOrWhiteSpace($GitHubToken) -and -not $SkipReleaseUpload) {
    throw "GitHub token is required. Pass -GitHubToken or set GITHUB_TOKEN."
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    $ZipPath = Join-Path $projectRoot "bin\x64\Release-Pro\latest.zip"
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

$buildOutputFullPath = Resolve-RequiredPath $BuildOutputDir "Build output dir not found: $BuildOutputDir"
$manifestPath = Join-Path $projectRoot "Teleport.json"
$manifestFullPath = Resolve-RequiredPath $manifestPath "Teleport.json not found: $manifestPath"

$manifest = Get-Content $manifestFullPath -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = [string]$manifest.AssemblyVersion
}

if ([string]::IsNullOrWhiteSpace($ReleaseTag)) {
    $ReleaseTag = $Version
}

$releaseZipPath = Join-Path $buildOutputFullPath "latest.zip"
$repoJsonOutputPath = Join-Path $buildOutputFullPath "repo.json"
$releaseDownloadLink = "https://github.com/$RepoOwner/$RepoName/releases/latest/download/latest.zip"
$releaseRepoJsonLink = "https://github.com/$RepoOwner/$RepoName/releases/latest/download/repo.json"

if (Test-Path $releaseZipPath) {
    Remove-Item $releaseZipPath -Force
}

Compress-Archive -Path (Join-Path $buildOutputFullPath "*") -DestinationPath $releaseZipPath -Force

$repoEntry = [ordered]@{
    Author = $manifest.Author
    Name = $manifest.Name
    InternalName = $manifest.InternalName
    AssemblyVersion = $Version
    Description = $manifest.Description
    ApplicableVersion = $manifest.ApplicableVersion
    RepoUrl = $manifest.RepoUrl
    DalamudApiLevel = $manifest.DalamudApiLevel
    LoadRequiredState = $manifest.LoadRequiredState
    LoadSync = $manifest.LoadSync
    CanUnloadAsync = $manifest.CanUnloadAsync
    LoadPriority = $manifest.LoadPriority
    IconUrl = $manifest.IconUrl
    Punchline = $manifest.Punchline
    AcceptsFeedback = $manifest.AcceptsFeedback
    DownloadLinkInstall = $releaseDownloadLink
    DownloadLinkTesting = $releaseDownloadLink
    DownloadLinkUpdate = $releaseDownloadLink
}

@($repoEntry) | ConvertTo-Json -Depth 8 | Set-Content -Path $repoJsonOutputPath -Encoding utf8

if (-not $SkipRepoMirror) {
    $myPluginsRepoFullPath = Resolve-RequiredPath $MyPluginsRepoPath "My plugins repo path not found: $MyPluginsRepoPath"
    $myRepoJsonFullPath = Resolve-RequiredPath $MyRepoJsonPath "pluginmaster.json not found: $MyRepoJsonPath"
    $latihasRepoFullPath = Resolve-RequiredPath $LatihasRepoPath "Latihas repo path not found: $LatihasRepoPath"
    $latihasRepoJsonFullPath = Resolve-RequiredPath $LatihasRepoJsonPath "repo.json not found: $LatihasRepoJsonPath"

    $skipPushText = if ($SkipPush) { "1" } else { "0" }

    $pythonScript = @'
import json
import pathlib
import subprocess
import sys
import shutil

my_repo = pathlib.Path(sys.argv[1])
my_repo_json_path = pathlib.Path(sys.argv[2])
latihas_repo = pathlib.Path(sys.argv[3])
latihas_repo_json_path = pathlib.Path(sys.argv[4])
manifest_path = pathlib.Path(sys.argv[5])
build_output_dir = pathlib.Path(sys.argv[6])
release_zip_path = pathlib.Path(sys.argv[7])
download_link = sys.argv[8]
repo_url = sys.argv[9]
skip_push = sys.argv[10] == "1"
version = sys.argv[11]

def read_json(path: pathlib.Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def write_json(path: pathlib.Path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

def git_commit_and_push(repo: pathlib.Path, paths, message: str, skip_push_flag: bool):
    subprocess.run(["git", "add", "--", *[str(path) for path in paths]], cwd=repo, check=True)
    status = subprocess.run(
        ["git", "diff", "--cached", "--name-only"],
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
plugin_name = manifest["InternalName"]

manifest["AssemblyVersion"] = version
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

build_manifest_path = build_output_dir / "Teleport.json"
if build_manifest_path.exists():
    build_manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

my_entries = [entry for entry in read_json(my_repo_json_path) if entry.get("InternalName") != plugin_name]
my_entries.append({
    "Author": manifest["Author"],
    "Name": manifest["Name"],
    "InternalName": plugin_name,
    "AssemblyVersion": version,
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
})
my_entries.sort(key=lambda item: item.get("InternalName", ""))
write_json(my_repo_json_path, my_entries)

my_plugin_dir = my_repo / "plugins" / plugin_name
my_plugin_dir.mkdir(parents=True, exist_ok=True)
target_zip = my_plugin_dir / "latest.zip"
shutil.copy2(release_zip_path, target_zip)

git_commit_and_push(
    my_repo,
    [my_repo_json_path, target_zip],
    f"Publish {plugin_name} {version}",
    skip_push,
)

latihas_entries = [entry for entry in read_json(latihas_repo_json_path) if entry.get("InternalName") != plugin_name]
latihas_entries.append({
    "Author": manifest["Author"],
    "Name": manifest["Name"],
    "InternalName": plugin_name,
    "AssemblyVersion": version,
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
})
latihas_entries.sort(key=lambda item: item.get("InternalName", ""))
write_json(latihas_repo_json_path, latihas_entries)

    try:
        git_commit_and_push(
            latihas_repo,
            [latihas_repo_json_path],
            f"Update {plugin_name} {version}",
            skip_push,
        )
    except subprocess.CalledProcessError as exc:
        print(f"Warning: failed to update mirror repo {latihas_repo}: {exc}")
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
        $releaseZipPath `
        $releaseDownloadLink `
        $manifest.RepoUrl `
        $skipPushText `
        $Version
}

if (-not $SkipReleaseUpload) {
    $releaseApiBase = "https://api.github.com/repos/$RepoOwner/$RepoName/releases"
    $existingRelease = $null

    try {
        $existingRelease = Invoke-GitHubJson -Method Get -Uri "$releaseApiBase/tags/$ReleaseTag"
    }
    catch {
        if (-not $_.Exception.Response -or $_.Exception.Response.StatusCode.value__ -ne 404) {
            throw
        }
    }

    if ($null -eq $existingRelease) {
        $existingRelease = Invoke-GitHubJson -Method Post -Uri $releaseApiBase -Body @{
            tag_name = $ReleaseTag
            name = $ReleaseTag
            draft = $false
            prerelease = $false
            make_latest = "true"
        }
    }

    foreach ($asset in @($existingRelease.assets)) {
        if ($asset.name -in @("latest.zip", "repo.json")) {
            Invoke-GitHubJson -Method Delete -Uri "https://api.github.com/repos/$RepoOwner/$RepoName/releases/assets/$($asset.id)"
        }
    }

    $uploadBase = $existingRelease.upload_url.Replace("{?name,label}", "")
    $latestZipUploadUri = "${uploadBase}?name=latest.zip"
    $repoJsonUploadUri = "${uploadBase}?name=repo.json"
    Invoke-GitHubBinaryUpload -Uri $latestZipUploadUri -FilePath $releaseZipPath -ContentType "application/zip"
    Invoke-GitHubBinaryUpload -Uri $repoJsonUploadUri -FilePath $repoJsonOutputPath -ContentType "application/json"

    Write-Host "Uploaded local Release-Pro artifacts to GitHub Release $ReleaseTag"
    Write-Host "Repo JSON: $releaseRepoJsonLink"
}
