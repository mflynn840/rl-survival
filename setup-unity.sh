#!/usr/bin/env bash
set -euo pipefail
cli="${UNITY_CLI:-$HOME/.local/bin/unity}"
if [[ ! -x "$cli" ]]; then
  echo 'Installing the official Unity CLI…'
  curl -fsSL https://unity.com/install.sh | bash
fi
if ! "$cli" editors -i 2>/dev/null | grep -q '2022.3.62f1'; then
  echo 'Installing Unity 2022.3.62f1 and Linux IL2CPP support (about 4.2 GB)…'
  "$cli" install 2022.3.62f1 --module linux-il2cpp --accept-eula --yes
fi
echo 'Unity 2022.3.62f1 is ready in WSL.'
echo 'Run ./open-unity.sh for the Editor or ./build-unity.sh to build a Linux player.'
