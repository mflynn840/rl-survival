#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
command -v unity >/dev/null || { echo 'Unity CLI is missing. Install the Unity CLI, then activate Unity Personal in WSL.' >&2; exit 1; }
mkdir -p "$repo_dir/Builds/Linux"
output="$repo_dir/Builds/Linux/RLSurvival"
unity build "$repo_dir" --target StandaloneLinux64 --output-path "$output"
[[ -x "$output" && -s "$output" ]] || { echo "Unity did not create the player at $output." >&2; exit 1; }
echo "Built Linux player: $output"
if [[ "${1:-}" == '--run' ]]; then exec "$output"; fi
