#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
editor="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/2022.3.62f1/Editor/Unity}"
if [[ ! -x "$editor" ]]; then
  echo 'Unity 2022.3.62f1 is not installed. Run ./setup-unity.sh first.' >&2
  exit 1
fi
if [[ -z "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]]; then
  echo 'WSLg display is unavailable. Enable WSLg, then reopen WSL.' >&2
  exit 1
fi
exec "$editor" -projectPath "$repo_dir" "$@"
