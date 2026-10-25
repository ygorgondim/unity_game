using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WizardRoyale.Domain;
using WizardRoyale.Simulation;

namespace WizardRoyale.Tests
{
    public sealed class MatchSimulationTests
    {
        [Test]
        public void Knockback_UsesLegacyCurveAndZeroHpOverride()
        {
            Assert.That(GameTuning.KnockbackMultiplier(500f), Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(GameTuning.KnockbackMultiplier(250f), Is.GreaterThan(2f));
            Assert.That(GameTuning.KnockbackMultiplier(0f), Is.EqualTo(10f));
            Assert.That(GameTuning.KnockbackDistanceMultiplier, Is.EqualTo(2f));
        }

        [Test]
        public void Countdown_DoesNotConsumeMatchClock()
        {
            MatchSimulation simulation = new MatchSimulation();
            Dictionary<int, PlayerCommand> commands = new Dictionary<int, PlayerCommand>();
            simulation.Tick(1f, commands);
            Assert.That(simulation.Phase, Is.EqualTo(MatchPhase.Countdown));
            Assert.That(simulation.MatchTimeRemaining, Is.EqualTo(180f));
        }

        [Test]
        public void ArenaBoundary_ExcludesWaterAndRockFaces()
        {
            MatchSimulation simulation = new MatchSimulation();
            GoldenArenaLayout layout = GoldenArenaLayout.Current;
            Assert.That(simulation.IsInsideArena(Vector2.zero), Is.True);
            Assert.That(simulation.IsInsideArena(layout.PixelToWorld(new Vector2(1360f, 1590f))), Is.True);
            Assert.That(simulation.IsInsideArena(layout.PixelToWorld(new Vector2(1360f, 1740f))), Is.False, "Front rocks");
            Assert.That(simulation.IsInsideArena(layout.PixelToWorld(new Vector2(350f, 900f))), Is.False, "Left rocks");
            Assert.That(simulation.IsInsideArena(layout.PixelToWorld(new Vector2(2400f, 900f))), Is.False, "Right rocks");
            Assert.That(simulation.IsInsideArena(layout.PixelToWorld(new Vector2(100f, 100f))), Is.False, "Ocean");
            foreach (PlayerState player in simulation.Players)
                Assert.That(simulation.IsInsideArena(player.Position), Is.True, "Spawn must be on the floor");
        }

        [TestCase(1360f, 1740f)]
        [TestCase(100f, 100f)]
        public void LeavingFloor_StartsFallAndCostsOneLife(float pixelX, float pixelY)
        {
            MatchSimulation simulation = StartMatch();
            PlayerState player = simulation.Players[0];
            player.Position = GoldenArenaLayout.Current.PixelToWorld(new Vector2(pixelX, pixelY));
            simulation.Tick(1f / 60f, EmptyCommands());
            Assert.That(player.IsFalling, Is.True);
            Tick(simulation, 0.65f);
            Assert.That(player.Lives, Is.EqualTo(2));
            Assert.That(player.IsFalling, Is.False);
            Assert.That(simulation.IsInsideArena(player.Position), Is.True);
        }

        [Test]
        public void FloorMask_MatchesTheGameplayContour()
        {
            GoldenArenaLayout layout = GoldenArenaLayout.Current;
            Texture2D mask = Resources.Load<Texture2D>("Art/GoldenArena/floor-mask");
            Assert.That(mask, Is.Not.Null);
            Assert.That(mask.width, Is.EqualTo(layout.width));
            Assert.That(mask.height, Is.EqualTo(layout.height));
            for (int y = 8; y < layout.height; y += 31)
            for (int x = 8; x < layout.width; x += 31)
            {
                Color color = mask.GetPixel(x, layout.height - 1 - y);
                Assert.That(color.g > 0.5f, Is.EqualTo(layout.ContainsPixel(new Vector2(x + 0.5f, y + 0.5f))),
                    "Mask differs at " + x + "," + y);
            }
        }

        [Test]
        public void ObstacleLayout_UsesTheExactCollisionGeometry()
        {
            MatchSimulation simulation = new MatchSimulation();
            Assert.That(simulation.Obstacles, Has.Count.EqualTo(5));
            Assert.That(simulation.Obstacles[0].Center, Is.EqualTo(new Vector2(-2.85f, 1.95f)));
            Assert.That(simulation.Obstacles[0].Radius, Is.EqualTo(0.48f));
            Assert.That(simulation.Obstacles[1].Center, Is.EqualTo(new Vector2(2.15f, 1.95f)));
            Assert.That(simulation.Obstacles[1].Radius, Is.EqualTo(0.46f));
            Assert.That(simulation.Obstacles[2].Center, Is.EqualTo(new Vector2(-0.38f, 0.05f)));
            Assert.That(simulation.Obstacles[2].Radius, Is.EqualTo(0.43f));
            Assert.That(simulation.Obstacles[3].Center, Is.EqualTo(new Vector2(-2.55f, -1.42f)));
            Assert.That(simulation.Obstacles[3].Radius, Is.EqualTo(0.62f));
            Assert.That(simulation.Obstacles[4].Center, Is.EqualTo(new Vector2(1.64f, -1.35f)));
            Assert.That(simulation.Obstacles[4].Radius, Is.EqualTo(0.46f));
        }

        [Test]
        public void WeaponCatalog_DefinesTheFourSpecialSpells()
        {
            Assert.That(WeaponCatalog.Get(WeaponType.Explosive).Damage, Is.EqualTo(28f));
            Assert.That(WeaponCatalog.Get(WeaponType.Slime).Range, Is.EqualTo(8.5f));
            Assert.That(WeaponCatalog.Get(WeaponType.Water).Projectile, Is.EqualTo(ProjectileKind.Water));
            Assert.That(WeaponCatalog.Get(WeaponType.Lightning).Speed, Is.Zero);
        }

        [TestCase(WeaponType.Energy, 3.64f)]
        [TestCase(WeaponType.Explosive, 3.38f)]
        [TestCase(WeaponType.Slime, 4.16f)]
        [TestCase(WeaponType.Water, 3.25f)]
        public void MovingProjectiles_UseThirtyPercentFasterSpeeds(WeaponType weapon, float expectedSpeed)
        {
            Assert.That(WeaponCatalog.Get(weapon).Speed, Is.EqualTo(expectedSpeed).Within(0.001f));
        }

        [Test]
        public void MatchStart_GivesTheStandardSpellToEveryPlayer()
        {
            MatchSimulation simulation = new MatchSimulation();
            Assert.That(simulation.Players.All(player => player.Weapon == WeaponType.Energy), Is.True);
        }

        [Test]
        public void PlayerMovement_UsesConfiguredBaseSpeed()
        {
            MatchSimulation simulation = StartMatch();
            PlayerState player = simulation.Players[0];
            player.Position = new Vector2(0f, 2.8f);
            Dictionary<int, PlayerCommand> commands = EmptyCommands();
            commands[0] = new PlayerCommand { Move = Vector2.right };

            simulation.Tick(1f, commands);

            Assert.That(player.Position.x, Is.EqualTo(GameTuning.PlayerSpeed).Within(0.001f));
            Assert.That(player.Position.y, Is.EqualTo(2.8f).Within(0.001f));
            Assert.That(GameTuning.PlayerSpeed, Is.EqualTo(1f));
        }

        [Test]
        public void PickupSpawner_CreatesASpecialSpellAfterInitialDelay()
        {
            MatchSimulation simulation = StartMatch();
            simulation.Tick(GameTuning.PickupInitialDelay, EmptyCommands());

            PickupState pickup = simulation.Pickups.Single();
            Assert.That(pickup.Weapon, Is.Not.EqualTo(WeaponType.Energy));
            Assert.That(pickup.Life, Is.EqualTo(GameTuning.PickupLifetime));
            Assert.That(simulation.Obstacles.All(obstacle =>
                Vector2.Distance(pickup.Position, obstacle.Center) > obstacle.Radius + pickup.Radius), Is.True);
        }

        [Test]
        public void CrossingPickup_ChangesSpellAndResetsAttackCooldown()
        {
            MatchSimulation simulation = StartMatch();
            simulation.Tick(GameTuning.PickupInitialDelay, EmptyCommands());
            PickupState pickup = simulation.Pickups.Single();
            PlayerState player = simulation.Players[0];
            player.Position = pickup.Position;
            player.AttackCooldown = 1.5f;

            simulation.Tick(1f / 60f, EmptyCommands());

            Assert.That(player.Weapon, Is.EqualTo(pickup.Weapon));
            Assert.That(player.AttackCooldown, Is.Zero);
            Assert.That(simulation.Pickups, Is.Empty);
        }

        [Test]
        public void ExplosiveSpell_ArmsOnImpactAndCanDamageItsOwner()
        {
            MatchSimulation simulation = StartMatch();
            PlayerState caster = simulation.Players[0];
            caster.Position = new Vector2(-1.2f, 0.05f);
            caster.Weapon = WeaponType.Explosive;

            Fire(simulation, Vector2.right);
            Assert.That(simulation.Projectiles.Single().IsArmed, Is.True);

            Tick(simulation, 2.1f);
            Assert.That(caster.Hp, Is.LessThan(GameTuning.MaxHp));
            Assert.That(simulation.Projectiles, Is.Empty);
        }

        [Test]
        public void SlimeSpell_BouncesOffArenaBorder()
        {
            MatchSimulation simulation = StartMatch();
            PlayerState caster = simulation.Players[0];
            caster.Position = new Vector2(0f, 2.72f);
            caster.Weapon = WeaponType.Slime;

            Fire(simulation, Vector2.up, 0.05f);
            ProjectileState slime = simulation.Projectiles.Single();
            Assert.That(slime.BouncesLeft, Is.EqualTo(5));
            Assert.That(slime.Velocity.y, Is.LessThan(0f));
        }

        [Test]
        public void WaterSpell_CreatesPuddleThatSlowsPlayers()
        {
            MatchSimulation simulation = StartMatch();
            PlayerState caster = simulation.Players[0];
            caster.Position = new Vector2(-1.2f, 0.05f);
            caster.Weapon = WeaponType.Water;

            Fire(simulation, Vector2.right);
            Assert.That(simulation.AreaEffects.Single().Kind, Is.EqualTo(AreaEffectKind.WaterPuddle));

            simulation.Tick(1f / 60f, EmptyCommands());
            Assert.That(caster.SlowFactor, Is.EqualTo(GameTuning.WaterSlowFactor));
            Assert.That(caster.SlowTimer, Is.GreaterThan(0f));
        }

        [Test]
        public void LightningSpell_ChargesThenHitsEveryPlayerOnItsClearLine()
        {
            Assert.That(GameTuning.LightningCharge, Is.EqualTo(0.5f));
            MatchSimulation simulation = StartMatch();
            PlayerState caster = simulation.Players[0];
            PlayerState target = simulation.Players[1];
            caster.Position = new Vector2(-3f, 1f);
            caster.Weapon = WeaponType.Lightning;
            target.Position = new Vector2(-1f, 1f);

            Fire(simulation, Vector2.right);
            Assert.That(target.Hp, Is.EqualTo(GameTuning.MaxHp));

            Tick(simulation, GameTuning.LightningCharge + 0.02f);
            Assert.That(target.Hp, Is.EqualTo(GameTuning.MaxHp - 30f).Within(0.001f));
            Assert.That(simulation.Projectiles.Single().HasFired, Is.True);
        }

        [Test]
        public void LightningSpell_LocksItsOriginAndDirectionWhenCastStarts()
        {
            MatchSimulation simulation = StartMatch();
            PlayerState caster = simulation.Players[0];
            PlayerState target = simulation.Players[1];
            caster.Position = new Vector2(-3f, 1f);
            caster.Weapon = WeaponType.Lightning;
            target.Position = new Vector2(-1f, 1f);

            Fire(simulation, Vector2.right);
            ProjectileState lightning = simulation.Projectiles.Single();
            Vector2 lockedStart = lightning.BeamStart;
            Vector2 lockedEnd = lightning.BeamEnd;

            caster.Position = new Vector2(0f, -2f);
            caster.Aim = Vector2.up;
            Tick(simulation, GameTuning.LightningCharge + 0.02f);

            Assert.That(lightning.BeamStart, Is.EqualTo(lockedStart));
            Assert.That(lightning.BeamEnd, Is.EqualTo(lockedEnd));
            Assert.That(target.Hp, Is.EqualTo(GameTuning.MaxHp - 30f).Within(0.001f));
        }

        [Test]
        public void SpellImpact_PreservesCastElementAfterWeaponChangeAndSurvivesMultipleTicks()
        {
            MatchSimulation simulation = StartMatch();
            PlayerState caster = simulation.Players[0];
            PlayerState target = simulation.Players[1];
            caster.Position = new Vector2(-3f, 1f);
            caster.Weapon = WeaponType.Lightning;
            target.Position = new Vector2(-1f, 1f);
            Fire(simulation, Vector2.right);
            caster.Weapon = WeaponType.Water;
            Tick(simulation, GameTuning.LightningCharge + 0.02f);

            Assert.That(simulation.SpellImpacts.Count, Is.EqualTo(1));
            SpellImpact impact = simulation.SpellImpacts[0];
            Assert.That(impact.Weapon, Is.EqualTo(WeaponType.Lightning));
            Assert.That(impact.Sequence, Is.GreaterThan(0));
            Assert.That(impact.Direction.x, Is.GreaterThan(0.99f));
            Tick(simulation, 0.05f);
            Assert.That(simulation.SpellImpacts[0].Sequence, Is.EqualTo(impact.Sequence));
            simulation.Reset();
            Assert.That(simulation.SpellImpacts, Is.Empty);
            Assert.That(simulation.ImpactSequence, Is.EqualTo(impact.Sequence), "Sequences must not be reused after restart");
        }

        [Test]
        public void OrbitalContact_ReportsItsOwnElementWithoutAProjectile()
        {
            MatchSimulation simulation = StartMatch();
            simulation.Players[0].Position = new Vector2(-3f, 1f);
            simulation.Players[0].Weapon = WeaponType.Orbit;
            simulation.Players[1].Position = new Vector2(-2.3f, 1f);
            simulation.Tick(1f / 60f, EmptyCommands());
            Assert.That(simulation.Projectiles, Is.Empty);
            Assert.That(simulation.SpellImpacts.Any(impact => impact.Weapon == WeaponType.Orbit), Is.True);
            Assert.That(simulation.Players[1].Hp, Is.LessThan(GameTuning.MaxHp));
        }

        [Test]
        public void ExpiredProjectile_DoesNotReportACharacterImpact()
        {
            MatchSimulation simulation = StartMatch();
            simulation.Players[0].Position = new Vector2(-3f, 1f);
            simulation.Players[0].Weapon = WeaponType.Wind;
            Fire(simulation, Vector2.down);
            Assert.That(simulation.Projectiles.Single().SourceWeapon, Is.EqualTo(WeaponType.Wind));
            Tick(simulation, 1.5f);
            Assert.That(simulation.SpellImpacts, Is.Empty);
        }

        private static MatchSimulation StartMatch()
        {
            MatchSimulation simulation = new MatchSimulation();
            simulation.Tick(5f, EmptyCommands());
            return simulation;
        }

        private static void Fire(MatchSimulation simulation, Vector2 aim, float dt = 1f / 60f)
        {
            Dictionary<int, PlayerCommand> commands = EmptyCommands();
            commands[0] = new PlayerCommand { Aim = aim, Attack = true };
            simulation.Tick(dt, commands);
        }

        private static void Tick(MatchSimulation simulation, float seconds)
        {
            const float step = 1f / 60f;
            int count = Mathf.CeilToInt(seconds / step);
            for (int i = 0; i < count; i++) simulation.Tick(step, EmptyCommands());
        }

        private static Dictionary<int, PlayerCommand> EmptyCommands()
        {
            return new Dictionary<int, PlayerCommand>();
        }
    }
}
