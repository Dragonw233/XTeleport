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
    [switch]$SkipRepoMirror,
    [switch]$BumpNextVersion
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pythonScript = Join-Path $scriptDir "publish.py"
$logPath = Join-Path $scriptDir "publish-run.log"

if (-not (Test-Path $pythonScript)) {
    throw "publish.py not found: $pythonScript"
}

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
Set-Content -Path $logPath -Value "[${timestamp}] publish.ps1 start" -Encoding utf8

$argsList = @(
    $pythonScript,
    "--repo-owner", $RepoOwner,
    "--repo-name", $RepoName,
    "--my-plugins-repo-path", $MyPluginsRepoPath,
    "--latihas-repo-path", $LatihasRepoPath
)

if (-not [string]::IsNullOrWhiteSpace($ReleaseTag)) {
    $argsList += @("--release-tag", $ReleaseTag)
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $argsList += @("--version", $Version)
}

if (-not [string]::IsNullOrWhiteSpace($ZipPath)) {
    $argsList += @("--zip-path", $ZipPath)
}

if (-not [string]::IsNullOrWhiteSpace($BuildOutputDir)) {
    $argsList += @("--build-output-dir", $BuildOutputDir)
}

if (-not [string]::IsNullOrWhiteSpace($MyRepoJsonPath)) {
    $argsList += @("--my-repo-json-path", $MyRepoJsonPath)
}

if (-not [string]::IsNullOrWhiteSpace($LatihasRepoJsonPath)) {
    $argsList += @("--latihas-repo-json-path", $LatihasRepoJsonPath)
}

if ($SkipPush) {
    $argsList += "--skip-push"
}

if ($SkipReleaseUpload) {
    $argsList += "--skip-release-upload"
}

if ($SkipRepoMirror) {
    $argsList += "--skip-repo-mirror"
}

if ($BumpNextVersion) {
    $argsList += "--bump-next-version"
}

$previousGitHubToken = $env:GITHUB_TOKEN
try {
    if (-not [string]::IsNullOrWhiteSpace($GitHubToken)) {
        $env:GITHUB_TOKEN = $GitHubToken
    }

    & python @argsList
    $exitCode = $LASTEXITCODE
}
finally {
    $env:GITHUB_TOKEN = $previousGitHubToken
}

Add-Content -Path $logPath -Value "[$(Get-Date -Format "yyyy-MM-dd HH:mm:ss")] publish.ps1 exit code: $exitCode" -Encoding utf8

if ($exitCode -ne 0) {
    exit $exitCode
}
