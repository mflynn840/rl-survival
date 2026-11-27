namespace RLSurvival
{
    public sealed class WeaponDefinition
    {
        public readonly string Name, Description;
        public readonly int UnlockCost, Damage, Magazine, SplashRadius;
        public readonly float SecondsPerShot, AimDegrees;
        public WeaponDefinition(string name, string description, int unlockCost, int damage, int magazine, float secondsPerShot, float aimDegrees, int splashRadius = 0)
        { Name=name; Description=description; UnlockCost=unlockCost; Damage=damage; Magazine=magazine; SecondsPerShot=secondsPerShot; AimDegrees=aimDegrees; SplashRadius=splashRadius; }
    }

    public sealed class BlockDefinition
    {
        public readonly string Name, Description;
        public readonly int PlaceCost, UnlockCost, Durability, ShockDamage;
        public BlockDefinition(string name, string description, int placeCost, int unlockCost, int durability, int shockDamage = 0)
        { Name=name; Description=description; PlaceCost=placeCost; UnlockCost=unlockCost; Durability=durability; ShockDamage=shockDamage; }
    }

    public sealed class PerkDefinition
    {
        public readonly string Name, Description;
        public readonly int UnlockCost;
        public PerkDefinition(string name, string description, int unlockCost) { Name=name; Description=description; UnlockCost=unlockCost; }
    }

    public static class UpgradeCatalog
    {
        public static readonly WeaponDefinition[] Weapons =
        {
            new WeaponDefinition("Carbine", "Reliable starter rifle", 0, 26, 60, .16f, 7f),
            new WeaponDefinition("Shotgun", "Heavy close-range spread", 180, 58, 24, .62f, 15f),
            new WeaponDefinition("Railgun", "Slow, high-damage precision", 350, 160, 12, 1.05f, 5f),
            new WeaponDefinition("SMG", "Fast fire rate, low damage per shot", 260, 17, 100, .075f, 10f),
            new WeaponDefinition("Grenade Launcher", "Explosive splash damage", 520, 90, 10, 1.2f, 8f, 3),
            new WeaponDefinition("LMG", "Large magazine and steady damage", 430, 32, 140, .2f, 9f)
        };

        public static readonly BlockDefinition[] Blocks =
        {
            new BlockDefinition("Scrap", "Cheap, fast to build", 10, 0, 80),
            new BlockDefinition("Brick", "Balanced durability", 25, 65, 220),
            new BlockDefinition("Steel", "Strong walls for later waves", 40, 220, 450),
            new BlockDefinition("Titanium", "High durability, premium cost", 65, 420, 780),
            new BlockDefinition("Shock Grid", "Damages raiders while they mine it", 55, 360, 310, 28),
            new BlockDefinition("Reinforced Concrete", "Heavy, exceptionally durable", 85, 600, 1100),
            new BlockDefinition("Roof", "Sloped cover for a layered fortress", 30, 150, 180),
            new BlockDefinition("Spikes", "Damages raiders who cross the plate", 45, 260, 170, 20)
        };

        public static readonly PerkDefinition[] Perks =
        {
            new PerkDefinition("Quick Hands", "Move faster and reload more quickly", 120),
            new PerkDefinition("Scavenger", "Earn extra credits from each raider", 160),
            new PerkDefinition("Engineer", "Repair blocks without spending credits", 200),
            new PerkDefinition("Fortitude", "Increase the crew core's maximum health", 240),
            new PerkDefinition("Field Medic", "Restore core health after each cleared wave", 280),
            new PerkDefinition("Demolitionist", "Increase explosive and weapon damage", 320)
        };

        public static int Size(UpgradeKind kind)
        {
            switch (kind) { case UpgradeKind.Weapon: return Weapons.Length; case UpgradeKind.Block: return Blocks.Length; default: return Perks.Length; }
        }
        public static string Name(UpgradeKind kind, int index)
        {
            index=ClampIndex(kind,index);
            switch (kind) { case UpgradeKind.Weapon: return Weapons[index].Name; case UpgradeKind.Block: return Blocks[index].Name; default: return Perks[index].Name; }
        }
        public static int UnlockCost(UpgradeKind kind, int index)
        {
            index=ClampIndex(kind,index);
            switch (kind) { case UpgradeKind.Weapon: return Weapons[index].UnlockCost; case UpgradeKind.Block: return Blocks[index].UnlockCost; default: return Perks[index].UnlockCost; }
        }
        static int ClampIndex(UpgradeKind kind, int index) { return System.Math.Max(0,System.Math.Min(Size(kind)-1,index)); }
    }
}
