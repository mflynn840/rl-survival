# RL Survival — Beta 2

3D co-op fortress defense with a LAN account/lobby relay. The browser playtest uses Three.js meshes and runs from the relay; the Unity project provides a native Linux player workflow. The arena and actors are created from 3D primitives and meshes.

## Project layout

```text
Assets/Scripts/
  Core/          Bootstrap, match composition root, phases, and tuning
  Gameplay/      Match pacing, construction, player combat, raiders, and RL brains
  Networking/    Relay client and wire/snapshot models
  Progression/   Account persistence and upgrade definitions
  UI/            Sign-in, lobby, agent setup, and in-match HUD
  World/         Arena generation and damageable fort blocks
Assets/Art/      3D reference direction and code-native model documentation
relay/            LAN HTTP relay, account store, and static WebGL server
test/             Two-client relay integration test
```

The Unity runtime has an assembly definition (`RLSurvival.Runtime.asmdef`). `SurvivalGame` is the composition root; match rules, presentation, networking, persistence, AI, and upgrade data live in focused components/classes so new weapons, policies, or rooms can be added without expanding one central script. The current level is a 3D roadside district: the motel is the raider spawn source, the road separates it from the crew's marked build plot, and the player/raider rigs are built from articulated 3D meshes. See [Assets/Art/README.md](Assets/Art/README.md) for the art direction and asset source boundaries.

## Agent selection

The match host selects one learning algorithm and one difficulty before starting a run. The selection is replicated to the room, shown in the HUD, and included in match snapshots. A deployed studio model carries its learned policy table through the host's start event and match snapshots.

- **Q-Learning** learns toward the best estimated future action.
- **SARSA** learns using the next action the current policy actually chooses.
- **Double Q-Learning** alternates two value tables to reduce overestimation.

Each raider chooses among three behaviors: pursue the nearest wall, target the weakest wall, or rush the crew core. Raiders share a per-match value table, earn rewards for making progress, damaging/breaching walls, and reaching the core, then carry that learning forward to later spawns and waves.

Each algorithm can be paired with any of these difficulty profiles:

| Difficulty | Enemy tuning |
| --- | --- |
| Rookie | More exploration, lower health/mining/speed, smaller groups |
| Standard | Balanced stats and learning |
| Veteran | More focused learning, higher health/mining/speed, larger groups |

## Beta 2 upgrades and loop

- Six weapons: Carbine, Shotgun, Railgun, SMG, Grenade Launcher, and LMG.
- Six block types: Scrap, Brick, Steel, Titanium, Shock Grid, and Reinforced Concrete.
- Six perks: Quick Hands, Scavenger, Engineer, Fortitude, Field Medic, and Demolitionist.
- Buy and equip upgrades with credits earned from kills and cleared waves. Repair and refill ammo during the intermission; the next wave starts automatically.
- New accounts start with $3,000 credits. During build and intermission, click the ground to place a block, then click any exposed block face to add adjacent blocks, including vertical stacks up to nine blocks high.
- The Agent Training Studio lets each player design a raider policy, choose its algorithm/difficulty and reward focus, train 600 sandbox episodes, inspect simulated breakthrough rate, save up to eight models, and deploy one in a match. Guest players can share models with the host, who chooses which one to deploy.
- Credits, unlocks, equipped items, and trained models persist in `relay/data/accounts.json`. Older profiles are migrated during server startup without losing existing unlocks.

## WSL setup and LAN play

The WSL Linux environment is the source of truth; no Windows `.bat` launchers or project-copy step are used.

1. Run `./start-lan.sh` from WSL. It starts the relay on port 8787, prints the Windows Wi-Fi LAN URL, and stores accounts in `relay/data/accounts.json`.
2. On first launch, approve the Windows admin prompt. WSL2 NAT needs a Windows TCP port proxy and a local-subnet-only firewall rule so other PCs can reach the relay. The launcher updates the proxy if WSL's IP changes.
3. Open the printed `http://<host-LAN-IP>:8787` URL on both PCs. Create separate username/password accounts, join room `FORT`, then host clicks **Start match** and **Start Wave 1**.
4. Keep the WSL terminal running. Press Ctrl+C to stop the server.

This PC's Wi-Fi LAN IP is currently detected from Windows at launch; do not use WSL's `172.*` address on the second PC. The relay uses plain HTTP and trusts client profile saves; use it on a trusted LAN, and do not forward port 8787 to the public internet.

## Unity Editor and Linux player

Unity CLI 1.1.0, Unity 2022.3.62f1, and Linux Build Support (IL2CPP) are installed inside WSL at `~/Unity/Hub/Editor/2022.3.62f1`.

- Run `./open-unity.sh` to open the project in the WSLg Unity Editor. Run `unity auth login`, then `unity license activate` if prompted; a Unity ID is required to activate the Editor license in WSL.
- Run `./build-unity.sh` to build `Builds/Linux/RL Survival.x86_64`.
- Run `./build-unity.sh --run` to build and launch the player.
- Run `./setup-unity.sh` to reinstall/repair the pinned editor and Linux module if needed.

The browser LAN client remains the easiest way to join from the second PC. Unity's WSL build is a native Linux player, not a Windows executable.

Controls: WASD moves, click the plot to place a block, click block faces to build upward or outward, click raiders to fire, 1–3 selects block tiers, and R repairs or buys ammo.

## Integration test

Run `npm test` for relay/protocol integration or `npm run test:browser` for the two-browser 3D game integration. The browser test creates separate Chromium clients, registers independent accounts, joins a lobby, launches the match, starts Wave 1, confirms the 3D canvas and live raiders, and saves [the screenshot](artifacts/wave1-two-browser-clients.png). The relay test uses two distinct local source IPs and covers authentication, lobby/network events, upgrades, model sharing, and profile persistence.

These tests use two clients in this environment. The WSL relay and Windows LAN forwarding need a final second-PC smoke test. The Unity Linux Editor and build module are installed; building is waiting on Unity license activation in WSL.

## Two-PC smoke test

1. Run `./start-lan.sh` in WSL on the host and approve the one-time Windows admin prompt for LAN forwarding.
2. On the second PC, open the LAN URL printed by the launcher (this PC currently uses Wi-Fi IP `192.168.4.40`; DHCP can change it).
3. Register separate usernames/passwords, join `FORT` on both clients, and start the match from the host.
4. Confirm both windows show Wave 1 and raiders; then build, shoot, earn credits, and try repairs, ammo, and upgrades.

`http://<host-LAN-IP>:8787/health` should return `{"ok":true,...}` if the other PC cannot load the game. Use the printed Windows LAN IP instead of `localhost` or WSL `172.*` on the second PC.
