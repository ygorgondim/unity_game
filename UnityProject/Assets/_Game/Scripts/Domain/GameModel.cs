using UnityEngine;

namespace WizardRoyale.Domain
{
    public enum CharacterType
    {
        OriginalWizard,
        FloatingWizard,
        DarkFloatingWizard,
        LeafFloatingWizard,
        AutumnFloatingWizard
    }

    public enum WeaponType
    {
        Energy = 1, // Legacy identifier for the starting weapon; now the basic Slash.
        Explosive = 2,
        Slime = 3,
        Water = 4,
        Lightning = 5,
        Orbit = 6,
        Wind = 7,
        Shadow = 8
    }

    public enum ProjectileKind
    {
        Energy,
        Explosive,
        Slime,
        Water,
        Lightning,
        Slash,
        ShadowSlash
    }

    public enum AreaEffectKind
    {
        WaterPuddle,
        Explosion
    }

    public enum MatchPhase
    {
        Countdown,
        Playing,
        GameOver
    }

    public struct PlayerCommand
    {
        public Vector2 Move;
        public Vector2 Aim;
        public bool Attack;
        public bool DashPressed;
    }

    public sealed class PlayerState
    {
        public int Id;
        public string Name;
        public Color Color;
        public bool IsBot;
        public CharacterType Character = CharacterType.OriginalWizard;
        public Vector2 Position;
        public Vector2 ExternalVelocity;
        public Vector2 Aim = Vector2.right;
        public Vector2 LastMove = Vector2.right;
        public float Hp = 500f;
        public int Lives = 3;
        public float AttackCooldown;
        public float AttackLockTimer;
        public Vector2 AttackFacing = Vector2.right;
        public float DashCooldown;
        public float DashTimer;
        public Vector2 DashVelocity;
        public float HitStunTimer;
        public float FloatTimer = 3f;
        public bool IsRecovering;
        public bool IsFalling;
        public bool IsDeathLaunched;
        public float RespawnTimer;
        public float SpawnTimer;
        public float InvulnerabilityTimer;
        public float ShadowTimer;
        public bool IsShadowForm => ShadowTimer > 0f && !IsUnavailable;
        public bool IsSpawning => SpawnTimer > 0f;
        public bool IsUnavailable => IsEliminated || IsFalling || IsSpawning;
        public bool IsInvulnerable => IsUnavailable || InvulnerabilityTimer > 0f || ShadowTimer > 0f;
        public float FallScale = 1f;
        public bool IsEliminated;
        public int Kos;
        public int Deaths;
        public float DamageDealt;
        public float DamageTaken;
        public int LastAttackerId = -1;
        public float LastHitAge = 99f;
        public WeaponType Weapon = WeaponType.Energy;
        public readonly OrbitState Orbit = new OrbitState();
        public float SlowTimer;
        public float SlowFactor = 1f;
    }

    public sealed class OrbitState
    {
        public float Angle;
        public float AttackTime = -1f;
        public float AttackStartAngle;
        public Vector2 PreviousCenter;
        public bool Initialized;
        public readonly OrbitingOrbState[] Orbs = new OrbitingOrbState[GameTuning.OrbitCount];

        public OrbitState()
        {
            for (int i = 0; i < Orbs.Length; i++) Orbs[i] = new OrbitingOrbState();
        }

        public float AngleFor(int index) => Angle + index * Mathf.PI * 2f / GameTuning.OrbitCount;

        public Vector2 PositionFor(Vector2 center, int index)
        {
            float angle = AngleFor(index);
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;
        }

        public float Radius => Mathf.Lerp(GameTuning.OrbitRadius, GameTuning.OrbitActiveRadius, Expansion);
        public float Expansion
        {
            get
            {
                if (AttackTime < 0f) return 0f;
                if (AttackTime < GameTuning.OrbitExpandDuration)
                    return Mathf.SmoothStep(0f, 1f, AttackTime / GameTuning.OrbitExpandDuration);
                return 1f - Mathf.SmoothStep(0f, 1f,
                    (AttackTime - GameTuning.OrbitExpandDuration - GameTuning.OrbitSweepDuration)
                    / GameTuning.OrbitReturnDuration);
            }
        }

        public void Reset()
        {
            Angle = 0f;
            AttackTime = -1f;
            AttackStartAngle = 0f;
            Initialized = false;
            foreach (OrbitingOrbState orb in Orbs) orb.ContactCooldowns.Clear();
        }
    }

    public sealed class OrbitingOrbState
    {
        public Vector2 Position;
        public readonly System.Collections.Generic.Dictionary<int, float> ContactCooldowns =
            new System.Collections.Generic.Dictionary<int, float>();
    }

    public sealed class ProjectileState
    {
        public WeaponType SourceWeapon = WeaponType.Energy;
        public int Id;
        public int OwnerId;
        public ProjectileKind Kind;
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 Acceleration;
        public float Radius;
        public float Damage;
        public float Knockback;
        public float Life;
        public float MaxLife;
        public int BouncesLeft;
        public int LastHitPlayerId = -1;
        public float ContactImmunity;
        public bool IsArmed;
        public float FuseTimer;
        public bool HasFired;
        public float ChargeTimer;
        public Vector2 BeamStart;
        public Vector2 BeamEnd;
        public readonly System.Collections.Generic.HashSet<int> HitPlayerIds =
            new System.Collections.Generic.HashSet<int>();
    }

    public sealed class AreaEffectState
    {
        public int Id;
        public int OwnerId;
        public AreaEffectKind Kind;
        public Vector2 Position;
        public float Radius;
        public float Life;
        public float MaxLife;
    }

    // Bounded, sequenced cosmetic notifications. Observing them never consumes simulation state.
    public struct SpellImpact
    {
        public long Sequence;
        public WeaponType Weapon;
        public Vector2 Position;
        public Vector2 Direction;
    }

    public sealed class PickupState
    {
        public int Id;
        public WeaponType Weapon;
        public Vector2 Position;
        public float Radius;
        public float Life;
        public float MaxLife;
    }

    public struct WeaponDefinition
    {
        public string Name;
        public ProjectileKind Projectile;
        public Color Color;
        public float Damage;
        public float Cooldown;
        public float Range;
        public float Speed;
        public float Knockback;
        public float Radius;

        public WeaponDefinition(string name, ProjectileKind projectile, Color color, float damage,
            float cooldown, float range, float speed, float knockback, float radius)
        {
            Name = name;
            Projectile = projectile;
            Color = color;
            Damage = damage;
            Cooldown = cooldown;
            Range = range;
            Speed = speed;
            Knockback = knockback;
            Radius = radius;
        }
    }

    public static class WeaponCatalog
    {
        public static WeaponDefinition Get(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Shadow:
                    return new WeaponDefinition("Sombra", ProjectileKind.ShadowSlash,
                        new Color(0.32f, 0.10f, 0.55f), 40f, 1.4f, 1.4f, 0f, 1.6f, 0.25f);
                case WeaponType.Wind:
                    return new WeaponDefinition("Vento", ProjectileKind.Energy,
                        Color.white, 40f, 1.4f, 5.2f, 3.64f, 1.6f, 0.10f);
                case WeaponType.Orbit:
                    return new WeaponDefinition("Orbe Orbital", ProjectileKind.Energy,
                        new Color(0.66f, 0.36f, 1f), 40f, 3f,
                        GameTuning.OrbitActiveRadius, 0f, 1.6f, GameTuning.OrbitOrbRadius);
                case WeaponType.Explosive:
                    return new WeaponDefinition("Explosiva", ProjectileKind.Explosive,
                        new Color(0.98f, 0.45f, 0.09f), 64f, 1.4f, 4.8f, 3.38f, 2.6f, 0.12f);
                case WeaponType.Slime:
                    return new WeaponDefinition("Slime", ProjectileKind.Slime,
                        new Color(0.13f, 0.77f, 0.37f), 50f, 1.4f, 8.5f, 4.16f, 1.8f, 0.10f);
                case WeaponType.Water:
                    return new WeaponDefinition("Água", ProjectileKind.Water,
                        new Color(0.02f, 0.71f, 0.83f), 54f, 1.4f, 5f, 3.25f, 1.7f, 0.12f);
                case WeaponType.Lightning:
                    return new WeaponDefinition("Raio", ProjectileKind.Lightning,
                        new Color(0.92f, 0.70f, 0.03f), 60f, 1.4f, 6.5f, 0f, 2.8f, 0.08f);
                default:
                    return new WeaponDefinition("Slash", ProjectileKind.Slash,
                        Color.white, 40f, 1f, 1.5f, 3f, 1.6f, 0.18f);
            }
        }
    }

    public struct CircleObstacle
    {
        public Vector2 Center;
        public float Radius;

        public CircleObstacle(Vector2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }

    public static class GameTuning
    {
        // Ground interaction radius for pickups and puddles. Movement and combat use CharacterHitbox.
        public const float CharacterScale = 0.70f;
        public const float PlayerRadius = 0.22f * CharacterScale;
        public const float PlayerSpeed = 1.2f;
        public const float DashSpeed = 5f;
        public const float DashDuration = 0.18f;
        public const float DashCooldown = 2f;
        public const float MaxHp = 500f;
        public const float KnockbackDistanceMultiplier = 2f;
        public const float RecoveryDuration = 3f;
        public const float DeathLaunchSpeed = 20f;
        public const float RespawnDelay = 5f;
        public const float SpawnDuration = 1.5f;
        public const float AttackMovementPause = 0.15f;
        public const float ShadowDashRangeMultiplier = 1.30f;
        public const float ShadowDashCooldownMultiplier = 1f;
        public const float ShadowDuration = 0.9f;
        public const float ShadowSlashDuration = 0.30f;
        public const float SpawnProtectionDuration = 2f;
        public const float RecoveryMoveMultiplier = 1.30f;
        public const float RecoveryDashRangeMultiplier = 1.30f;
        public const float RecoveryDashRechargeMultiplier = 1.30f;
        // Bounding envelope for camera and spawning; the actual floor follows GoldenArenaLayout.
        public const float ArenaHalfWidth = 5.31f;
        public const float ArenaHalfHeight = 3.10f;
        public const float ExplosiveFuse = 1f;
        // Half the original circular area: radius scales by sqrt(0.5).
        public const float ExplosionRadius = 1.35f * 0.70710678f;
        public const float WaterPuddleRadius = 0.75f;
        public const float WaterPuddleDuration = 5f;
        public const float WaterSlowFactor = 0.4f;
        public const float WaterSlowLinger = 0.25f;
        public const float LightningCharge = 0.5f;
        public const float LightningVisualDuration = 0.25f;
        public const float SlashTravelDuration = 0.50f;
        public const float SlashFadeDuration = 0.16f;
        public const int OrbitCount = 3;
        public const float OrbitRadius = 0.70f;
        public const float OrbitActiveRadius = 2f;
        public const float OrbitOrbRadius = 0.12f;
        public const float OrbitIdlePeriod = 3f;
        public const float OrbitExpandDuration = 0.12f;
        public const float OrbitSweepDuration = 1f;
        public const float OrbitReturnDuration = 0.18f;
        public const float OrbitContactInterval = 0.75f;
        public const float PickupInitialDelay = 10f;
        public const float PickupMeanInterval = 4f;
        public const float PickupLifetime = 10f;
        public const float PickupRadius = 0.22f;
        public const int MaxGroundPickups = 3;

        public static float KnockbackMultiplier(float hpAfterHit)
        {
            if (hpAfterHit <= 0f)
                return 10f;

            float missing = Mathf.Clamp01((MaxHp - hpAfterHit) / MaxHp);
            return Mathf.Min(5f, 0.75f + Mathf.Pow(missing, 1.5f) * 4.25f);
        }
    }
}
