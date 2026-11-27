#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
command -v unity >/dev/null || { echo 'Unity CLI is missing. Install Unity CLI and activate a Unity license.' >&2; exit 1; }
output="$repo_dir/Builds/WebGL"
mkdir -p "$output"
export RLSURVIVAL_WEBGL_OUTPUT="$output"
unity build "$repo_dir" --target WebGL --output-path "$output" --execute-method RLSurvival.Editor.BuildPipelineCommands.BuildWebGL
[[ -s "$output/index.html" ]] || { echo "Unity did not create the WebGL player at $output." >&2; exit 1; }
echo "Built Unity WebGL player: $output"
echo 'Start LAN hosting with ./start-lan.sh; the browser and native player use the same Unity scene.'
