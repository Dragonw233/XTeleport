from __future__ import annotations

import argparse
import contextlib
import json
import os
import re
import shutil
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Publish Teleport release artifacts.")
    parser.add_argument(
        "--github-token",
        default=os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN", ""),
    )
    parser.add_argument("--repo-owner", default="Dragonw233")
    parser.add_argument("--repo-name", default="XTeleport")
    parser.add_argument("--release-tag", default="")
    parser.add_argument("--version", default="")
    parser.add_argument("--my-plugins-repo-path", default=r"D:\git back\DalamudPlugins")
    parser.add_argument("--latihas-repo-path", default=r"D:\git back\dalamud-plugins")
    parser.add_argument("--zip-path", default="")
    parser.add_argument("--build-output-dir", default="")
    parser.add_argument("--my-repo-json-path", default="")
    parser.add_argument("--latihas-repo-json-path", default="")
    parser.add_argument("--skip-push", action="store_true")
    parser.add_argument("--skip-release-upload", action="store_true")
    parser.add_argument("--skip-repo-mirror", action="store_true")
    parser.add_argument("--bump-next-version", action="store_true")
    return parser.parse_args()


def resolve_required_path(path: Path | str, message: str) -> Path:
    resolved = Path(path)
    if not resolved.exists():
        raise FileNotFoundError(message)
    return resolved.resolve()


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, data: Any) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def get_csproj_version(csproj_path: Path) -> str:
    content = csproj_path.read_text(encoding="utf-8-sig")
    match = re.search(r"<Version>([^<]+)</Version>", content)
    if not match or not match.group(1).strip():
        raise RuntimeError(f"Unable to read <Version> from {csproj_path}")
    return match.group(1).strip()


def set_csproj_version(csproj_path: Path, version: str) -> None:
    content = csproj_path.read_text(encoding="utf-8-sig")
    updated, count = re.subn(r"<Version>[^<]+</Version>", f"<Version>{version}</Version>", content, count=1)
    if count != 1:
        raise RuntimeError(f"Failed to update <Version> in {csproj_path}")
    csproj_path.write_text(updated, encoding="utf-8")


def get_next_version(version: str) -> str:
    parts = version.split(".")
    if not parts:
        raise RuntimeError(f"Invalid version format: {version}")
    parts[-1] = str(int(parts[-1]) + 1)
    return ".".join(parts)


def create_zip_from_directory(source_dir: Path, destination_zip: Path) -> None:
    if destination_zip.exists():
        destination_zip.unlink()

    with zipfile.ZipFile(destination_zip, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        for file_path in source_dir.rglob("*"):
            if not file_path.is_file():
                continue
            if file_path.resolve() == destination_zip.resolve():
                continue
            zf.write(file_path, file_path.relative_to(source_dir))


def git_commit_and_push(repo: Path, paths: list[Path], message: str, skip_push: bool) -> bool:
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
    if skip_push:
        print(f"Push skipped for {repo}.")
    else:
        subprocess.run(["git", "push"], cwd=repo, check=True)
    return True


def github_headers(token: str, content_type: str | None = None) -> dict[str, str]:
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github+json",
        "User-Agent": "TeleportPublish",
        "X-GitHub-Api-Version": "2022-11-28",
    }
    if content_type:
        headers["Content-Type"] = content_type
    return headers


def github_json(method: str, url: str, token: str, body: Any | None = None) -> Any:
    data = None
    headers = github_headers(token, "application/json" if body is not None else None)
    if body is not None:
        data = json.dumps(body).encode("utf-8")

    request = urllib.request.Request(url, data=data, headers=headers, method=method.upper())
    try:
        with urllib.request.urlopen(request) as response:
            content = response.read().decode("utf-8")
            return json.loads(content) if content else None
    except urllib.error.HTTPError as exc:
        if exc.fp is not None:
            details = exc.fp.read().decode("utf-8", errors="replace")
        else:
            details = ""
        raise RuntimeError(f"GitHub API {method} {url} failed: {exc.code} {details}") from exc


def github_binary_upload(url: str, file_path: Path, token: str, content_type: str) -> None:
    data = file_path.read_bytes()
    request = urllib.request.Request(
        url,
        data=data,
        headers=github_headers(token, content_type),
        method="POST",
    )
    try:
        with urllib.request.urlopen(request):
            return
    except urllib.error.HTTPError as exc:
        details = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"GitHub upload failed for {file_path.name}: {exc.code} {details}") from exc


class TeeWriter:
    def __init__(self, *streams: Any) -> None:
        self.streams = streams

    def write(self, data: str) -> int:
        for stream in self.streams:
            stream.write(data)
            stream.flush()
        return len(data)

    def flush(self) -> None:
        for stream in self.streams:
            stream.flush()


def sync_repo_mirrors(
    manifest: dict[str, Any],
    version: str,
    my_plugins_repo_path: Path,
    my_repo_json_path: Path,
    latihas_repo_path: Path,
    latihas_repo_json_path: Path,
    release_zip_path: Path,
    download_link: str,
    repo_url: str,
    skip_push: bool,
) -> None:
    plugin_name = manifest["InternalName"]

    my_entries = [entry for entry in read_json(my_repo_json_path) if entry.get("InternalName") != plugin_name]
    my_entries.append(
        {
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
        }
    )
    my_entries.sort(key=lambda item: item.get("InternalName", ""))
    write_json(my_repo_json_path, my_entries)

    my_plugin_dir = my_plugins_repo_path / "plugins" / plugin_name
    my_plugin_dir.mkdir(parents=True, exist_ok=True)
    target_zip = my_plugin_dir / "latest.zip"
    shutil.copy2(release_zip_path, target_zip)

    git_commit_and_push(
        my_plugins_repo_path,
        [my_repo_json_path, target_zip],
        f"Publish {plugin_name} {version}",
        skip_push,
    )

    latihas_entries = [entry for entry in read_json(latihas_repo_json_path) if entry.get("InternalName") != plugin_name]
    latihas_entries.append(
        {
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
        }
    )
    latihas_entries.sort(key=lambda item: item.get("InternalName", ""))
    write_json(latihas_repo_json_path, latihas_entries)

    try:
        git_commit_and_push(
            latihas_repo_path,
            [latihas_repo_json_path],
            f"Update {plugin_name} {version}",
            skip_push,
        )
    except subprocess.CalledProcessError as exc:
        print(f"Warning: failed to update mirror repo {latihas_repo_path}: {exc}")


def upload_github_release(
    token: str,
    repo_owner: str,
    repo_name: str,
    release_tag: str,
    release_zip_path: Path,
    repo_json_output_path: Path,
) -> None:
    release_api_base = f"https://api.github.com/repos/{repo_owner}/{repo_name}/releases"
    existing_release = None

    try:
        existing_release = github_json("GET", f"{release_api_base}/tags/{release_tag}", token)
    except RuntimeError as exc:
        if " 404 " not in str(exc):
            raise

    if existing_release is None:
        existing_release = github_json(
            "POST",
            release_api_base,
            token,
            {
                "tag_name": release_tag,
                "name": release_tag,
                "draft": False,
                "prerelease": False,
                "make_latest": "true",
            },
        )

    for asset in existing_release.get("assets", []):
        if asset.get("name") in {"latest.zip", "repo.json"}:
            github_json(
                "DELETE",
                f"https://api.github.com/repos/{repo_owner}/{repo_name}/releases/assets/{asset['id']}",
                token,
            )

    upload_base = existing_release["upload_url"].replace("{?name,label}", "")
    github_binary_upload(f"{upload_base}?name=latest.zip", release_zip_path, token, "application/zip")
    github_binary_upload(f"{upload_base}?name=repo.json", repo_json_output_path, token, "application/json")


def fetch_release_tag(token: str, repo_owner: str, repo_name: str) -> str:
    release_api_base = f"https://api.github.com/repos/{repo_owner}/{repo_name}/releases/latest"
    release = github_json("GET", release_api_base, token)
    return str(release["tag_name"])


def fetch_remote_repo_version(repo_json_url: str) -> str:
    request = urllib.request.Request(
        repo_json_url,
        headers={
            "Accept": "application/json",
            "User-Agent": "TeleportPublish",
        },
        method="GET",
    )
    with urllib.request.urlopen(request) as response:
        payload = json.loads(response.read().decode("utf-8-sig"))
    if not payload:
        raise RuntimeError(f"Remote repo.json is empty: {repo_json_url}")
    return str(payload[0]["AssemblyVersion"])


def main() -> int:
    args = parse_args()

    if not args.github_token and not args.skip_release_upload:
        raise RuntimeError("GitHub token is required. Pass --github-token or set GITHUB_TOKEN.")

    project_root = Path(__file__).resolve().parent
    build_output_dir = Path(args.build_output_dir) if args.build_output_dir else project_root / "bin" / "x64" / "Release-Pro"
    zip_path = Path(args.zip_path) if args.zip_path else project_root / "bin" / "x64" / "Release-Pro" / "latest.zip"
    my_plugins_repo_path = Path(args.my_plugins_repo_path)
    latihas_repo_path = Path(args.latihas_repo_path)
    my_repo_json_path = Path(args.my_repo_json_path) if args.my_repo_json_path else my_plugins_repo_path / "pluginmaster.json"
    latihas_repo_json_path = Path(args.latihas_repo_json_path) if args.latihas_repo_json_path else latihas_repo_path / "repo.json"

    build_output_full_path = resolve_required_path(build_output_dir, f"Build output dir not found: {build_output_dir}")
    manifest_full_path = resolve_required_path(project_root / "Teleport.json", f"Teleport.json not found: {project_root / 'Teleport.json'}")
    csproj_full_path = resolve_required_path(project_root / "Teleport.csproj", f"Teleport.csproj not found: {project_root / 'Teleport.csproj'}")

    manifest = read_json(manifest_full_path)
    project_version = get_csproj_version(csproj_full_path)
    version = args.version or project_version
    release_tag = args.release_tag or version

    manifest["AssemblyVersion"] = version
    write_json(manifest_full_path, manifest)

    release_zip_path = zip_path if zip_path.is_absolute() else (project_root / zip_path)
    repo_json_output_path = build_output_full_path / "repo.json"
    release_download_link = f"https://github.com/{args.repo_owner}/{args.repo_name}/releases/latest/download/latest.zip"
    release_repo_json_link = f"https://github.com/{args.repo_owner}/{args.repo_name}/releases/latest/download/repo.json"

    create_zip_from_directory(build_output_full_path, release_zip_path)

    repo_entry = {
        "Author": manifest["Author"],
        "Name": manifest["Name"],
        "InternalName": manifest["InternalName"],
        "AssemblyVersion": version,
        "Description": manifest["Description"],
        "ApplicableVersion": manifest["ApplicableVersion"],
        "RepoUrl": manifest["RepoUrl"],
        "DalamudApiLevel": manifest["DalamudApiLevel"],
        "LoadRequiredState": manifest["LoadRequiredState"],
        "LoadSync": manifest["LoadSync"],
        "CanUnloadAsync": manifest["CanUnloadAsync"],
        "LoadPriority": manifest["LoadPriority"],
        "IconUrl": manifest["IconUrl"],
        "Punchline": manifest["Punchline"],
        "AcceptsFeedback": manifest["AcceptsFeedback"],
        "DownloadLinkInstall": release_download_link,
        "DownloadLinkTesting": release_download_link,
        "DownloadLinkUpdate": release_download_link,
    }
    write_json(repo_json_output_path, [repo_entry])

    if not args.skip_repo_mirror:
        sync_repo_mirrors(
            manifest=manifest,
            version=version,
            my_plugins_repo_path=resolve_required_path(my_plugins_repo_path, f"My plugins repo path not found: {my_plugins_repo_path}"),
            my_repo_json_path=resolve_required_path(my_repo_json_path, f"pluginmaster.json not found: {my_repo_json_path}"),
            latihas_repo_path=resolve_required_path(latihas_repo_path, f"Latihas repo path not found: {latihas_repo_path}"),
            latihas_repo_json_path=resolve_required_path(latihas_repo_json_path, f"repo.json not found: {latihas_repo_json_path}"),
            release_zip_path=release_zip_path,
            download_link=release_download_link,
            repo_url=manifest["RepoUrl"],
            skip_push=args.skip_push,
        )

    if not args.skip_release_upload:
        upload_github_release(
            token=args.github_token,
            repo_owner=args.repo_owner,
            repo_name=args.repo_name,
            release_tag=release_tag,
            release_zip_path=release_zip_path,
            repo_json_output_path=repo_json_output_path,
        )
        print(f"Uploaded local Release-Pro artifacts to GitHub Release {release_tag}")
        print(f"Repo JSON: {release_repo_json_link}")
        remote_release_tag = fetch_release_tag(args.github_token, args.repo_owner, args.repo_name)
        remote_repo_version = fetch_remote_repo_version(release_repo_json_link)
        print(f"Local version: {version}")
        print(f"Remote release tag: {remote_release_tag}")
        print(f"Remote repo.json version: {remote_repo_version}")

    if args.bump_next_version:
        next_version = get_next_version(version)
        set_csproj_version(csproj_full_path, next_version)
        manifest["AssemblyVersion"] = next_version
        write_json(manifest_full_path, manifest)
        print(f"Bumped next version to {next_version} in Teleport.csproj and Teleport.json")
    return 0


if __name__ == "__main__":
    project_root = Path(__file__).resolve().parent
    log_path = project_root / "publish-run.log"
    exit_code = 1
    with log_path.open("w", encoding="utf-8") as log_file:
        log_file.write(f"[{__import__('datetime').datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] publish.py start\n")
        log_file.flush()
        tee = contextlib.ExitStack()
        tee.enter_context(contextlib.redirect_stdout(TeeWriter(sys.stdout, log_file)))
        tee.enter_context(contextlib.redirect_stderr(TeeWriter(sys.stderr, log_file)))
        try:
            exit_code = main()
            raise SystemExit(exit_code)
        except Exception as exc:
            print(f"ERROR: {exc}", file=sys.stderr)
            raise
        finally:
            print(f"[{__import__('datetime').datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] publish.py exit code: {exit_code}")
            tee.close()
