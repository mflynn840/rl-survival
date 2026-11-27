using System;

namespace RLSurvival
{
    [Serializable] public sealed class RelayEvent { public int seq; public string type, from, name, payload; public RoomPlayer[] players; }
    [Serializable] public sealed class RoomPlayer { public string id, name; }
    [Serializable] public sealed class JoinRequest { public string token, room; }
    [Serializable] public sealed class Credentials { public string username, password; }
    [Serializable] public sealed class AuthResponse { public string token, error; public PlayerProfile profile; }
    [Serializable] public sealed class PlayerProfile
    {
        public string username;
        public string equippedAgentModel;
        public int credits, equippedGun, equippedBlock, equippedPerk;
        public bool[] guns, blocks, perks;
        public RaiderModel[] agentModels;
    }
    [Serializable] public sealed class GameCommand { public string action, id; public float x, y, z; public int value, value2; }
    [Serializable] public sealed class BlockSnapshot { public float x, y, z; public int type, hp, maxHp; }
    [Serializable] public sealed class RaiderSnapshot { public string id, weapon; public float x, z; public int hp, maxHp, door; }
    [Serializable] public sealed class PlayerSnapshot { public string id; public float x, z; public int hp, maxHp, gun; }
    [Serializable] public sealed class MatchSnapshot
    {
        public string phase, algorithm, difficulty;
        public bool hasDeployedModel;
        public RaiderModel deployedModel;
        public float timer;
        public int wave, remaining, health, maxHealth, kills;
        public BlockSnapshot[] blocks;
        public RaiderSnapshot[] raiders;
        public PlayerSnapshot[] players;
    }
}
