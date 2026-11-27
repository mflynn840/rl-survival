#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
port="${PORT:-8787}"
node_bin="${NODE_BIN:-$(command -v node || true)}"
if [[ -z "$node_bin" ]]; then
  echo "Node.js 18+ is required. On Ubuntu run: sudo apt update && sudo apt install nodejs npm" >&2
  exit 1
fi
if [[ ! -f "$repo_dir/Builds/WebGL/index.html" ]]; then
  echo 'Unity WebGL build missing. Run ./build-webgl.sh before starting LAN play.' >&2
  exit 1
fi

lan_ip=""
if command -v powershell.exe >/dev/null 2>&1; then
  lan_ip="$(powershell.exe -NoProfile -Command '(Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.InterfaceAlias -match "Wi-Fi|WLAN|Ethernet" -and $_.InterfaceAlias -notmatch "WSL|vEthernet|Loopback" -and $_.IPAddress -notlike "169.254*"} | Sort-Object @{Expression={if ($_.InterfaceAlias -match "Wi-Fi|WLAN") {0} else {1}}} | Select-Object -First 1 -ExpandProperty IPAddress)' | tr -d '\r')"
fi
if [[ -z "$lan_ip" ]]; then
  lan_ip="$(ip -4 -o addr show scope global 2>/dev/null | awk '$2 != "eth0" && $2 !~ /docker|br-|veth/ {split($4,a,"/"); if(a[1] !~ /^169\.254\./){print a[1]; exit}}')"
fi

export PORT="$port"
export WEB_BUILD="${WEB_BUILD:-$repo_dir/Builds/WebGL}"
export ACCOUNTS_FILE="${ACCOUNTS_FILE:-$repo_dir/relay/data/accounts.json}"
mkdir -p "$(dirname -- "$ACCOUNTS_FILE")"
echo "RL Survival LAN server (WSL)"
echo "Unity WebGL game: http://localhost:$port"
if [[ -n "$lan_ip" ]]; then
  echo "Other PCs:   http://$lan_ip:$port"
else
  echo 'LAN IP not detected; run ipconfig.exe on Windows to find it.'
fi
echo "Room code: FORT | TCP port: $port"
echo 'Keep this terminal open. Press Ctrl+C to stop.'

# WSL2 NAT needs a Windows port proxy for other LAN machines to reach WSL.
if grep -qi microsoft /proc/version && command -v powershell.exe >/dev/null 2>&1; then
  wsl_ip="$(hostname -I | awk '{print $1}')"
  win_helper="$(wslpath -w "$repo_dir/tools/Setup-WslLan.ps1")"
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$win_helper" -WslAddress "$wsl_ip" -Port "$port" || \
    echo 'Windows LAN forwarding was not configured. Local WSL play works; see README.md for the one-time admin setup.' >&2
fi

cd "$repo_dir"
exec "$node_bin" relay/server.js
