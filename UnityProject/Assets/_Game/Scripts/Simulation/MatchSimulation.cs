using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WizardRoyale.Domain;

namespace WizardRoyale.Simulation
{
    public sealed class MatchSimulation
    {
        private readonly List<PlayerState> players = new List<PlayerState>();
        private readonly List<ProjectileState> projectiles = new List<ProjectileState>();
        private readonly List<AreaEffectState> areaEffects = new List<AreaEffectState>();
        private readonly List<PickupState> pickups = new List<PickupState>();
        private readonly List<SpellImpact> spellImpacts = new List<SpellImpact>();
        private long impactSequence;
        public IReadOnlyList<SpellImpact> SpellImpacts => spellImpacts;
        public long ImpactSequence => impactSequence;
        private readonly List<CircleObstacle> obstacles = new List<CircleObstacle>();
        private readonly Vector2[] spawns =
        {
            new Vector2(-3.50f, 2.40f), new Vector2(3.50f, 2.40f),
            new Vector2(3.50f, -2.40f), new Vector2(-3.50f, -2.40f)
        };
        private int nextProjectileId = 1;
        private int nextEffectId = 1;
        private int nextPickupId = 1;
        private float pickupSpawnDelay;
        private System.Random pickupRandom;

        public IReadOnlyList<PlayerState> Players => players;
        public IReadOnlyList<ProjectileState> Projectiles => projectiles;
        public IReadOnlyList<AreaEffectState> AreaEffects => areaEffects;
        public IReadOnlyList<PickupState> Pickups => pickups;
        public IReadOnlyList<CircleObstacle> Obstacles => obstacles;
        public MatchPhase Phase { get; private set; }
        public float CountdownRemaining { get; private set; }
        public float MatchTimeRemaining { get; private set; }
        public string ResultText { get; private set; } = string.Empty;

        public MatchSimulation()
        {
            GoldenArenaLayout layout = GoldenArenaLayout.Current;
            if (layout.swamp)
            {
                for (int i = 0; i < spawns.Length; i++) spawns[i] = layout.PixelToWorld(layout.spawnPixels[i]);
                foreach (GoldenArenaLayout.ArenaProp prop in layout.props)
                    foreach (Vector3 circle in prop.collisionPixels)
                        obstacles.Add(new CircleObstacle(layout.PixelToWorld(new Vector2(circle.x, circle.y)),
                            circle.z / layout.pixelsPerUnit));
                Reset();
                return;
            }
            obstacles.Add(new CircleObstacle(new Vector2(-2.85f, 1.95f), 0.48f));
            obstacles.Add(new CircleObstacle(new Vector2(2.15f, 1.95f), 0.46f));
            obstacles.Add(new CircleObstacle(new Vector2(-0.38f, 0.05f), 0.43f));
            obstacles.Add(new CircleObstacle(new Vector2(-2.55f, -1.42f), 0.62f));
            obstacles.Add(new CircleObstacle(new Vector2(1.64f, -1.35f), 0.46f));
            Reset();
        }

        public void Reset()
        {
            players.Clear();
            projectiles.Clear();
            areaEffects.Clear();
            pickups.Clear();
            spellImpacts.Clear();
            nextProjectileId = 1;
            nextEffectId = 1;
            nextPickupId = 1;
            pickupSpawnDelay = GameTuning.PickupInitialDelay;
            pickupRandom = new System.Random(1337);
            Phase = MatchPhase.Countdown;
            CountdownRemaining = 5f;
            MatchTimeRemaining = 180f;
            ResultText = string.Empty;
            AddPlayer(0, "Você", new Color(0.20f, 0.60f, 1f), false, WeaponType.Energy);
            AddPlayer(1, "Freya", new Color(1f, 0.28f, 0.38f), true, WeaponType.Energy);
            AddPlayer(2, "Loki", new Color(0.20f, 0.88f, 0.46f), true, WeaponType.Energy);
            AddPlayer(3, "Odin", new Color(1f, 0.70f, 0.16f), true, WeaponType.Energy);
        }

        private void AddPlayer(int id, string name, Color color, bool isBot, WeaponType weapon)
        {
            players.Add(new PlayerState
            {
                Id = id, Name = name, Color = color, IsBot = isBot,
                Character = id == 0 ? CharacterType.FloatingWizard
                    : id == 1 ? CharacterType.DarkFloatingWizard
                    : id == 2 ? CharacterType.LeafFloatingWizard
                    : id == 3 ? CharacterType.AutumnFloatingWizard : CharacterType.OriginalWizard,
                Position = spawns[id], Aim = -spawns[id].normalized,
                LastMove = -spawns[id].normalized, Weapon = weapon
            });
        }

        public void Tick(float dt, IReadOnlyDictionary<int, PlayerCommand> commands)
        {
            if (Phase == MatchPhase.GameOver) return;
            if (Phase == MatchPhase.Countdown)
            {
                CountdownRemaining = Mathf.Max(0f, CountdownRemaining - dt);
                if (CountdownRemaining <= 0f) Phase = MatchPhase.Playing;
                return;
            }

            MatchTimeRemaining = Mathf.Max(0f, MatchTimeRemaining - dt);
            TickAreaEffects(dt);
            foreach (PlayerState player in players)
            {
                PlayerCommand command;
                commands.TryGetValue(player.Id, out command);
                TickPlayer(player, command, dt);
            }
            ResolvePlayerPairs();
            foreach (PlayerState player in players) TickRecovery(player, dt);
            TickOrbits(dt);
            TickProjectiles(dt);
            TickPickups(dt);
            ResolveMatchEnd();
        }

        private void TickPlayer(PlayerState player, PlayerCommand command, float dt)
        {
            if (player.IsEliminated) return;
            if (player.RespawnTimer > 0f)
            {
                player.RespawnTimer = Mathf.Max(0f, player.RespawnTimer - dt);
                if (player.RespawnTimer <= 0f) BeginRespawn(player);
                return;
            }
            player.InvulnerabilityTimer = Mathf.Max(0f, player.InvulnerabilityTimer - dt);
            player.ShadowTimer = Mathf.Max(0f, player.ShadowTimer - dt);
            if (player.IsSpawning)
            {
                player.SpawnTimer = Mathf.Max(0f, player.SpawnTimer - dt);
                return;
            }
            bool recovering = !player.IsFalling && !IsInsideArena(player.Position);
            player.AttackCooldown = Mathf.Max(0f, player.AttackCooldown - dt);
            player.AttackLockTimer = Mathf.Max(0f, player.AttackLockTimer - dt);
            float rechargeRate = recovering ? GameTuning.RecoveryDashRechargeMultiplier : 1f;
            player.DashCooldown = Mathf.Max(0f, player.DashCooldown - dt * rechargeRate);
            player.HitStunTimer = Mathf.Max(0f, player.HitStunTimer - dt);
            player.SlowTimer = Mathf.Max(0f, player.SlowTimer - dt);
            if (player.SlowTimer <= 0f) player.SlowFactor = 1f;
            player.LastHitAge += dt;

            if (player.IsFalling)
            {
                if (player.IsDeathLaunched)
                {
                    // Lethal launches ignore obstacles and cannot be cancelled by movement or dash.
                    player.Position += player.ExternalVelocity * dt;
                    if (IsInsideArena(player.Position)) return;
                }
                player.FallScale = Mathf.Max(0f, player.FallScale - 1.8f * dt);
                if (player.FallScale <= 0f) CompleteFall(player);
                return;
            }

            if (command.Aim.sqrMagnitude > 0.04f) player.Aim = command.Aim.normalized;
            Vector2 move = Vector2.ClampMagnitude(command.Move, 1f);
            if (move.sqrMagnitude > 0.01f && player.AttackLockTimer <= 0f) player.LastMove = move.normalized;
            if (command.Attack && player.AttackCooldown <= 0f && player.HitStunTimer <= 0f
                && player.AttackLockTimer <= 0f) Fire(player);

            float recoveryMultiplier = recovering ? GameTuning.RecoveryMoveMultiplier : 1f;
            if (command.DashPressed && player.DashCooldown <= 0f && player.HitStunTimer <= 0f
                && player.AttackLockTimer <= 0f)
            {
                Vector2 dashDirection = move.sqrMagnitude > 0.01f ? move.normalized : player.Aim;
                player.DashVelocity = dashDirection * GameTuning.DashSpeed;
                player.DashTimer = GameTuning.DashDuration;
                bool shadow = player.Weapon == WeaponType.Shadow;
                if (shadow)
                {
                    player.DashVelocity *= GameTuning.ShadowDashRangeMultiplier;
                    player.ShadowTimer = GameTuning.ShadowDuration;
                }
                player.DashCooldown = GameTuning.DashCooldown
                    * (shadow ? GameTuning.ShadowDashCooldownMultiplier : 1f);
            }

            Vector2 voluntary = player.HitStunTimer > 0f || player.AttackLockTimer > 0f ? Vector2.zero :
                move * GameTuning.PlayerSpeed * player.SlowFactor * recoveryMultiplier;
            if (player.DashTimer > 0f)
            {
                player.DashTimer = Mathf.Max(0f, player.DashTimer - dt);
                voluntary = player.DashVelocity * (recovering ? GameTuning.RecoveryDashRangeMultiplier : 1f);
            }
            player.Position += (voluntary + player.ExternalVelocity) * dt;
            player.ExternalVelocity *= Mathf.Pow(0.91f, dt * 60f);
            ResolveObstacles(player);

        }

        private void TickRecovery(PlayerState player, float dt)
        {
            if (player.IsUnavailable) return;
            if (IsInsideArena(player.Position))
            {
                player.IsRecovering = false;
                player.FloatTimer = GameTuning.RecoveryDuration;
                return;
            }
            player.IsRecovering = true;
            player.FloatTimer = Mathf.Max(0f, player.FloatTimer - dt);
            if (player.FloatTimer > 0f) return;
            player.IsRecovering = false;
            player.IsFalling = true;
            player.FallScale = 1f;
        }

        private void Fire(PlayerState player)
        {
            if (player.Weapon == WeaponType.Orbit && player.Orbit.AttackTime >= 0f) return;
            WeaponDefinition weapon = WeaponCatalog.Get(player.Weapon);
            player.AttackCooldown = weapon.Cooldown;
            Vector2 direction = player.Aim.sqrMagnitude > 0.01f ? player.Aim.normalized : Vector2.right;
            player.AttackFacing = direction;
            player.LastMove = direction;
            player.AttackLockTimer = GameTuning.AttackMovementPause;
            player.DashTimer = 0f;
            if (player.Weapon == WeaponType.Shadow)
            {
                Vector2 start = CharacterHitbox.Center(player.Position);
                projectiles.Add(new ProjectileState
                {
                    Id = nextProjectileId++, OwnerId = player.Id, Kind = ProjectileKind.ShadowSlash,
                    SourceWeapon = WeaponType.Shadow, Position = start, BeamStart = start,
                    BeamEnd = start + direction * weapon.Range, Velocity = direction,
                    Radius = weapon.Radius, Damage = weapon.Damage, Knockback = weapon.Knockback,
                    Life = GameTuning.ShadowSlashDuration, MaxLife = GameTuning.ShadowSlashDuration
                });
                foreach (PlayerState target in players)
                {
                    if (target.Id == player.Id || target.IsInvulnerable) continue;
                    if (!ShadowSlashGeometry.Intersects(target.Position, start, direction)) continue;
                    Vector2 targetCenter = CharacterHitbox.Center(target.Position);
                    Vector2 path = targetCenter - start;
                    Vector2 end = FindFirstObstacleIntersection(start, path.normalized, path.magnitude);
                    if ((end - targetCenter).sqrMagnitude > 0.0001f) continue;
                    ApplyDamage(player.Id, target, weapon.Damage, weapon.Knockback, direction, WeaponType.Shadow);
                }
                return;
            }
            if (player.Weapon == WeaponType.Orbit)
            {
                player.Orbit.AttackStartAngle = player.Orbit.Angle;
                player.Orbit.AttackTime = 0f;
                foreach (OrbitingOrbState orb in player.Orbit.Orbs) orb.ContactCooldowns.Clear();
                return;
            }
            if (weapon.Projectile == ProjectileKind.Slash)
            {
                Vector2 start = CharacterHitbox.Center(player.Position);
                Vector2 end = FindFirstObstacleIntersection(start, direction, weapon.Range, weapon.Radius);
                projectiles.Add(new ProjectileState
                {
                    Id = nextProjectileId++, OwnerId = player.Id, Kind = ProjectileKind.Slash, SourceWeapon = player.Weapon,
                    Position = start, BeamStart = start, BeamEnd = end, Velocity = direction,
                    Radius = weapon.Radius, Damage = weapon.Damage, Knockback = weapon.Knockback,
                    Life = GameTuning.SlashTravelDuration + GameTuning.SlashFadeDuration,
                    MaxLife = GameTuning.SlashTravelDuration + GameTuning.SlashFadeDuration,
                    HasFired = true
                });
                return;
            }
            Vector2 origin = CharacterHitbox.AttackOrigin(player.Position, direction, weapon.Radius);
            ProjectileState projectile = new ProjectileState
            {
                Id = nextProjectileId++, OwnerId = player.Id, Kind = weapon.Projectile, SourceWeapon = player.Weapon,
                Position = origin, Velocity = direction * weapon.Speed, Radius = weapon.Radius,
                Damage = weapon.Damage, Knockback = weapon.Knockback,
                Life = weapon.Speed > 0f ? weapon.Range / weapon.Speed : GameTuning.LightningCharge,
                MaxLife = weapon.Speed > 0f ? weapon.Range / weapon.Speed : GameTuning.LightningCharge,
                BouncesLeft = weapon.Projectile == ProjectileKind.Slime ? 6 : 0,
                ChargeTimer = weapon.Projectile == ProjectileKind.Lightning ? GameTuning.LightningCharge : 0f,
                BeamStart = origin, BeamEnd = origin + direction * weapon.Range
            };
            if (player.Weapon == WeaponType.Wind)
            {
                // Constant acceleration from v to 2v: average speed is 1.5v, keeping the same range.
                float duration = weapon.Range / (weapon.Speed * 1.5f);
                projectile.Life = projectile.MaxLife = duration;
                projectile.Acceleration = direction * (weapon.Speed / duration);
            }
            if (projectile.Kind == ProjectileKind.Lightning) UpdateLightningPath(projectile, player, weapon.Range);
            projectiles.Add(projectile);
        }

        private void TickOrbits(float dt)
        {
            foreach (PlayerState owner in players)
            {
                OrbitState orb = owner.Orbit;
                if (owner.Weapon != WeaponType.Orbit || owner.IsUnavailable)
                {
                    orb.Reset();
                    continue;
                }
                Vector2 center = CharacterHitbox.Center(owner.Position);
                if (!orb.Initialized)
                {
                    orb.PreviousCenter = center;
                    for (int index = 0; index < orb.Orbs.Length; index++)
                        orb.Orbs[index].Position = orb.PositionFor(center, index);
                    orb.Initialized = true;
                }
                // Sweep small arc segments so fast rotations cannot skip a character between frames.
                int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.01f));
                float step = dt / steps;
                WeaponDefinition weapon = WeaponCatalog.Get(WeaponType.Orbit);
                for (int i = 0; i < steps; i++)
                {
                    bool attacking = orb.AttackTime >= 0f;
                    if (attacking)
                    {
                        orb.AttackTime += step;
                        orb.Angle = orb.AttackStartAngle + Mathf.PI * 2f * Mathf.Clamp01(
                            (orb.AttackTime - GameTuning.OrbitExpandDuration) / GameTuning.OrbitSweepDuration);
                        float duration = GameTuning.OrbitExpandDuration + GameTuning.OrbitSweepDuration
                            + GameTuning.OrbitReturnDuration;
                        if (orb.AttackTime >= duration)
                        {
                            orb.Angle += (orb.AttackTime - duration) * Mathf.PI * 2f / GameTuning.OrbitIdlePeriod;
                            orb.AttackTime = -1f;
                        }
                    }
                    else orb.Angle += step * Mathf.PI * 2f / GameTuning.OrbitIdlePeriod;
                    orb.Angle = Mathf.Repeat(orb.Angle, Mathf.PI * 2f);
                    Vector2 stepCenter = Vector2.Lerp(orb.PreviousCenter, center, (i + 1f) / steps);
                    for (int index = 0; index < orb.Orbs.Length; index++)
                    {
                        OrbitingOrbState satellite = orb.Orbs[index];
                        Vector2 previous = satellite.Position;
                        satellite.Position = orb.PositionFor(stepCenter, index);
                        foreach (PlayerState target in players)
                        {
                            if (target.Id == owner.Id) continue;
                            float cooldown;
                            satellite.ContactCooldowns.TryGetValue(target.Id, out cooldown);
                            cooldown = Mathf.Max(0f, cooldown - step);
                            satellite.ContactCooldowns[target.Id] = cooldown;
                            if (target.IsInvulnerable || cooldown > 0f) continue;
                            if (!CharacterHitbox.IntersectsSegment(target.Position, previous, satellite.Position, weapon.Radius)) continue;
                            Vector2 direction = CharacterHitbox.Center(target.Position) - stepCenter;
                            if (direction.sqrMagnitude < 0.000001f) direction = satellite.Position - stepCenter;
                            ApplyDamage(owner.Id, target, weapon.Damage, weapon.Knockback, direction, WeaponType.Orbit);
                            satellite.ContactCooldowns[target.Id] = attacking
                                ? GameTuning.OrbitExpandDuration + GameTuning.OrbitSweepDuration
                                    + GameTuning.OrbitReturnDuration + 0.01f
                                : GameTuning.OrbitContactInterval;
                        }
                    }
                }
                orb.PreviousCenter = center;
            }
        }

        private void TickProjectiles(float dt)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                ProjectileState projectile = projectiles[i];
                projectile.ContactImmunity = Mathf.Max(0f, projectile.ContactImmunity - dt);
                bool remove;
                switch (projectile.Kind)
                {
                    case ProjectileKind.Slash: remove = TickSlash(projectile, dt); break;
                    case ProjectileKind.ShadowSlash:
                        projectile.Life -= dt;
                        remove = projectile.Life <= 0f;
                        break;
                    case ProjectileKind.Explosive: remove = TickExplosive(projectile, dt); break;
                    case ProjectileKind.Slime: remove = TickSlime(projectile, dt); break;
                    case ProjectileKind.Water: remove = TickWater(projectile, dt); break;
                    case ProjectileKind.Lightning: remove = TickLightning(projectile, dt); break;
                    default: remove = TickEnergy(projectile, dt); break;
                }
                if (remove) projectiles.RemoveAt(i);
            }
        }

        private bool TickSlash(ProjectileState projectile, float dt)
        {
            float previousAge = projectile.MaxLife - projectile.Life;
            Vector2 previous = projectile.Position;
            projectile.Life -= dt;
            float progress = Mathf.Clamp01((projectile.MaxLife - projectile.Life) / GameTuning.SlashTravelDuration);
            projectile.Position = Vector2.Lerp(projectile.BeamStart, projectile.BeamEnd, progress);
            // Damage advances with the visible wave, once per target; the fading trail is cosmetic.
            if (previousAge < GameTuning.SlashTravelDuration)
            {
                foreach (PlayerState target in players)
                {
                    if (target.Id == projectile.OwnerId || target.IsInvulnerable
                        || projectile.HitPlayerIds.Contains(target.Id)) continue;
                    if (!CharacterHitbox.IntersectsSegment(target.Position, previous, projectile.Position,
                        projectile.Radius)) continue;
                    projectile.HitPlayerIds.Add(target.Id);
                    ApplyDamage(projectile.OwnerId, target, projectile.Damage, projectile.Knockback,
                        projectile.Velocity, projectile.SourceWeapon);
                }
            }
            return projectile.Life <= 0f;
        }

        private bool TickEnergy(ProjectileState projectile, float dt)
        {
            float step = Mathf.Min(dt, Mathf.Max(0f, projectile.Life));
            Vector2 previous = projectile.Position;
            Vector2 displacement = projectile.Velocity * step + projectile.Acceleration * (0.5f * step * step);
            projectile.Velocity += projectile.Acceleration * step;
            float distance = displacement.magnitude;
            Vector2 direction = distance > 0.000001f ? displacement / distance : projectile.Velocity.normalized;
            projectile.Position = FindFirstObstacleIntersection(previous, direction, distance, projectile.Radius);
            projectile.Life -= dt;
            if (!IsInsideArena(projectile.Position)) return true;
            // Sweep the travelled segment so accelerating projectiles do not skip targets.
            PlayerState target = null;
            float nearest = float.PositiveInfinity;
            foreach (PlayerState candidate in players)
            {
                if (candidate.Id == projectile.OwnerId || candidate.IsInvulnerable) continue;
                if (!CharacterHitbox.IntersectsSegment(candidate.Position, previous, projectile.Position,
                    projectile.Radius)) continue;
                float along = Vector2.Dot(CharacterHitbox.Center(candidate.Position) - previous, direction);
                if (along >= nearest) continue;
                nearest = along;
                target = candidate;
            }
            if (target != null)
            {
                ApplyDamage(projectile.OwnerId, target, projectile.Damage, projectile.Knockback, direction, projectile.SourceWeapon);
                return true;
            }
            bool hitObstacle = (projectile.Position - previous).sqrMagnitude + 0.000001f < distance * distance;
            return projectile.Life <= 0f || hitObstacle;
        }

        private bool TickExplosive(ProjectileState projectile, float dt)
        {
            if (projectile.IsArmed)
            {
                projectile.FuseTimer -= dt;
                if (projectile.FuseTimer > 0f) return false;
                Explode(projectile);
                return true;
            }
            Vector2 previous = projectile.Position;
            projectile.Position += projectile.Velocity * dt;
            projectile.Life -= dt;
            CircleObstacle ignored;
            bool impact = projectile.Life <= 0f || !IsInsideArena(projectile.Position) ||
                          TryGetObstacle(projectile, out ignored) || FindProjectileTarget(projectile) != null;
            if (!impact) return false;
            if (!IsInsideArena(projectile.Position)) projectile.Position = previous;
            projectile.IsArmed = true;
            projectile.Velocity = Vector2.zero;
            projectile.FuseTimer = GameTuning.ExplosiveFuse;
            return false;
        }

        private void Explode(ProjectileState projectile)
        {
            areaEffects.Add(new AreaEffectState
            {
                Id = nextEffectId++, OwnerId = projectile.OwnerId, Kind = AreaEffectKind.Explosion,
                Position = projectile.Position, Radius = GameTuning.ExplosionRadius, Life = 0.22f, MaxLife = 0.22f
            });
            foreach (PlayerState target in players)
            {
                if (target.IsInvulnerable) continue;
                if (!CharacterHitbox.IntersectsCircle(target.Position, projectile.Position,
                    GameTuning.ExplosionRadius)) continue;
                Vector2 offset = CharacterHitbox.Center(target.Position) - projectile.Position;
                Vector2 direction = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector2.up;
                ApplyDamage(projectile.OwnerId, target, projectile.Damage, projectile.Knockback, direction, projectile.SourceWeapon);
            }
        }

        private bool TickSlime(ProjectileState projectile, float dt)
        {
            projectile.Position += projectile.Velocity * dt;
            projectile.Life -= dt;
            if (projectile.Life <= 0f) return true;
            if (!IsInsideArena(projectile.Position))
            {
                Vector2 boundaryNormal;
                projectile.Position = GoldenArenaLayout.Current.ClosestFloorPoint(projectile.Position, out boundaryNormal)
                    + boundaryNormal * 0.02f;
                return !Bounce(projectile, boundaryNormal);
            }

            CircleObstacle obstacle;
            if (TryGetObstacle(projectile, out obstacle))
            {
                Vector2 normal = (projectile.Position - obstacle.Center).normalized;
                projectile.Position = obstacle.Center + normal * (obstacle.Radius + projectile.Radius + 0.02f);
                return !Bounce(projectile, normal);
            }

            PlayerState target = FindProjectileTarget(projectile);
            if (target == null || target.Id == projectile.LastHitPlayerId && projectile.ContactImmunity > 0f) return false;
            Vector2 hitPoint = CharacterHitbox.ClosestAxisPoint(target.Position, projectile.Position);
            Vector2 hitNormal = (projectile.Position - hitPoint).normalized;
            if (hitNormal.sqrMagnitude < 0.01f) hitNormal = -projectile.Velocity.normalized;
            ApplyDamage(projectile.OwnerId, target, projectile.Damage, projectile.Knockback, projectile.Velocity.normalized, projectile.SourceWeapon);
            projectile.LastHitPlayerId = target.Id;
            projectile.ContactImmunity = 0.12f;
            projectile.Position = hitPoint + hitNormal * (CharacterHitbox.Radius + projectile.Radius + 0.03f);
            return !Bounce(projectile, hitNormal);
        }

        private static bool Bounce(ProjectileState projectile, Vector2 normal)
        {
            if (projectile.BouncesLeft <= 0) return false;
            projectile.Velocity = Vector2.Reflect(projectile.Velocity, normal);
            projectile.BouncesLeft--;
            return true;
        }

        private bool TickWater(ProjectileState projectile, float dt)
        {
            Vector2 previous = projectile.Position;
            projectile.Position += projectile.Velocity * dt;
            projectile.Life -= dt;
            projectile.Radius = Mathf.Lerp(0.12f, 0.18f, 1f - Mathf.Clamp01(projectile.Life / projectile.MaxLife));
            CircleObstacle ignored;
            bool impact = projectile.Life <= 0f || !IsInsideArena(projectile.Position) || TryGetObstacle(projectile, out ignored);
            PlayerState target = impact ? null : FindProjectileTarget(projectile);
            if (target != null)
            {
                ApplyDamage(projectile.OwnerId, target, projectile.Damage, projectile.Knockback, projectile.Velocity.normalized, projectile.SourceWeapon);
                impact = true;
            }
            if (!impact) return false;
            if (!IsInsideArena(projectile.Position)) projectile.Position = previous;
            areaEffects.Add(new AreaEffectState
            {
                Id = nextEffectId++, OwnerId = projectile.OwnerId, Kind = AreaEffectKind.WaterPuddle,
                Position = projectile.Position, Radius = GameTuning.WaterPuddleRadius,
                Life = GameTuning.WaterPuddleDuration, MaxLife = GameTuning.WaterPuddleDuration
            });
            return true;
        }

        private bool TickLightning(ProjectileState projectile, float dt)
        {
            PlayerState owner = players.FirstOrDefault(p => p.Id == projectile.OwnerId);
            if (owner == null || owner.IsUnavailable) return true;
            if (!projectile.HasFired)
            {
                projectile.ChargeTimer -= dt;
                if (projectile.ChargeTimer > 0f) return false;
                projectile.HasFired = true;
                projectile.Life = GameTuning.LightningVisualDuration;
                foreach (PlayerState target in players)
                {
                    if (target.Id == owner.Id || target.IsInvulnerable) continue;
                    if (!CharacterHitbox.IntersectsSegment(target.Position, projectile.BeamStart,
                        projectile.BeamEnd, projectile.Radius)) continue;
                    ApplyDamage(owner.Id, target, projectile.Damage, projectile.Knockback,
                        (projectile.BeamEnd - projectile.BeamStart).normalized, projectile.SourceWeapon);
                }
                return false;
            }
            projectile.Life -= dt;
            return projectile.Life <= 0f;
        }

        private void UpdateLightningPath(ProjectileState projectile, PlayerState owner, float range)
        {
            Vector2 direction = owner.Aim.sqrMagnitude > 0.01f ? owner.Aim.normalized : Vector2.right;
            projectile.BeamStart = CharacterHitbox.AttackOrigin(owner.Position, direction, projectile.Radius);
            projectile.BeamEnd = FindFirstObstacleIntersection(projectile.BeamStart, direction, range);
            projectile.Position = projectile.BeamStart;
            projectile.Velocity = direction;
        }

        private Vector2 FindFirstObstacleIntersection(Vector2 origin, Vector2 direction, float range,
            float padding = 0f)
        {
            float nearest = range;
            foreach (CircleObstacle obstacle in obstacles)
            {
                Vector2 toOrigin = origin - obstacle.Center;
                float b = 2f * Vector2.Dot(toOrigin, direction);
                float radius = obstacle.Radius + padding;
                float c = Vector2.Dot(toOrigin, toOrigin) - radius * radius;
                if (c <= 0f) return origin;
                float discriminant = b * b - 4f * c;
                if (discriminant < 0f) continue;
                float t = (-b - Mathf.Sqrt(discriminant)) * 0.5f;
                if (t >= 0f && t < nearest) nearest = t;
            }
            return origin + direction * nearest;
        }

        private void TickPickups(float dt)
        {
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                PickupState pickup = pickups[i];
                pickup.Life -= dt;
                if (pickup.Life <= 0f) pickups.RemoveAt(i);
            }

            pickupSpawnDelay = Mathf.Max(0f, pickupSpawnDelay - dt);
            if (pickupSpawnDelay <= 0f && pickups.Count < GameTuning.MaxGroundPickups)
            {
                float chance = Mathf.Clamp01(dt / GameTuning.PickupMeanInterval);
                if (pickupRandom.NextDouble() < chance) TrySpawnPickup();
            }

            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                PickupState pickup = pickups[i];
                foreach (PlayerState player in players)
                {
                    if (player.IsUnavailable) continue;
                    float collectRadius = GameTuning.PlayerRadius + pickup.Radius;
                    if ((player.Position - pickup.Position).sqrMagnitude > collectRadius * collectRadius) continue;
                    player.Weapon = pickup.Weapon;
                    player.Orbit.Reset();
                    player.AttackCooldown = 0f;
                    pickups.RemoveAt(i);
                    break;
                }
            }
        }

        private bool TrySpawnPickup()
        {
            Vector2 position;
            if (!TryFindPickupPosition(out position)) return false;
            WeaponType weapon = (WeaponType)pickupRandom.Next((int)WeaponType.Explosive, (int)WeaponType.Shadow + 1);
            pickups.Add(new PickupState
            {
                Id = nextPickupId++, Weapon = weapon, Position = position,
                Radius = GameTuning.PickupRadius,
                Life = GameTuning.PickupLifetime, MaxLife = GameTuning.PickupLifetime
            });
            return true;
        }

        private bool TryFindPickupPosition(out Vector2 position)
        {
            const float margin = 0.55f;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                float x = Mathf.Lerp(-GameTuning.ArenaHalfWidth + margin, GameTuning.ArenaHalfWidth - margin,
                    (float)pickupRandom.NextDouble());
                float y = Mathf.Lerp(-GameTuning.ArenaHalfHeight + margin, GameTuning.ArenaHalfHeight - margin,
                    (float)pickupRandom.NextDouble());
                position = new Vector2(x, y);
                if (IsPickupPositionValid(position)) return true;
            }
            position = Vector2.zero;
            return false;
        }

        private bool IsPickupPositionValid(Vector2 position)
        {
            if (!IsInsideArena(position) ||
                !IsInsideArena(position + Vector2.right * GameTuning.PickupRadius) ||
                !IsInsideArena(position - Vector2.right * GameTuning.PickupRadius) ||
                !IsInsideArena(position + Vector2.up * GameTuning.PickupRadius) ||
                !IsInsideArena(position - Vector2.up * GameTuning.PickupRadius)) return false;
            foreach (CircleObstacle obstacle in obstacles)
            {
                float clearance = obstacle.Radius + GameTuning.PickupRadius + 0.20f;
                if ((position - obstacle.Center).sqrMagnitude < clearance * clearance) return false;
            }
            foreach (PlayerState player in players)
            {
                float clearance = GameTuning.PlayerRadius + GameTuning.PickupRadius + 0.45f;
                if ((position - player.Position).sqrMagnitude < clearance * clearance) return false;
            }
            foreach (PickupState pickup in pickups)
            {
                float clearance = GameTuning.PickupRadius * 2f + 0.35f;
                if ((position - pickup.Position).sqrMagnitude < clearance * clearance) return false;
            }
            return true;
        }

        private void TickAreaEffects(float dt)
        {
            for (int i = areaEffects.Count - 1; i >= 0; i--)
            {
                AreaEffectState effect = areaEffects[i];
                effect.Life -= dt;
                if (effect.Life <= 0f)
                {
                    areaEffects.RemoveAt(i);
                    continue;
                }
                if (effect.Kind != AreaEffectKind.WaterPuddle) continue;
                foreach (PlayerState player in players)
                {
                    if (player.IsInvulnerable) continue;
                    float radius = effect.Radius + GameTuning.PlayerRadius;
                    if ((player.Position - effect.Position).sqrMagnitude > radius * radius) continue;
                    player.SlowFactor = GameTuning.WaterSlowFactor;
                    player.SlowTimer = GameTuning.WaterSlowLinger;
                }
            }
        }

        private PlayerState FindProjectileTarget(ProjectileState projectile)
        {
            foreach (PlayerState target in players)
            {
                if (target.Id == projectile.OwnerId || target.IsInvulnerable) continue;
                if (CharacterHitbox.IntersectsCircle(target.Position, projectile.Position, projectile.Radius))
                    return target;
            }
            return null;
        }

        private bool TryGetObstacle(ProjectileState projectile, out CircleObstacle hit)
        {
            foreach (CircleObstacle obstacle in obstacles)
            {
                float radius = projectile.Radius + obstacle.Radius;
                if ((projectile.Position - obstacle.Center).sqrMagnitude > radius * radius) continue;
                hit = obstacle;
                return true;
            }
            hit = default;
            return false;
        }

        private void ApplyDamage(int attackerId, PlayerState target, float damage, float knockback, Vector2 direction,
            WeaponType sourceWeapon)
        {
            PlayerState attacker = players.FirstOrDefault(p => p.Id == attackerId);
            if (target.IsInvulnerable || target.Hp <= 0f) return;
            float actualDamage = Mathf.Min(target.Hp, damage);
            target.Hp = Mathf.Max(0f, target.Hp - damage);
            target.DamageTaken += actualDamage;
            target.LastAttackerId = attackerId;
            target.LastHitAge = 0f;
            if (attacker != null) attacker.DamageDealt += actualDamage;
            target.ExternalVelocity += direction.normalized * knockback * GameTuning.KnockbackMultiplier(target.Hp) *
                                       GameTuning.KnockbackDistanceMultiplier;
            target.HitStunTimer = target.Hp <= 0f ? 0.85f : 0.45f;
            if (target.Hp <= 0f)
            {
                Vector2 launchDirection = direction;
                if (launchDirection.sqrMagnitude < 0.000001f && attacker != null)
                    launchDirection = target.Position - attacker.Position;
                if (launchDirection.sqrMagnitude < 0.000001f) launchDirection = Vector2.up;
                target.ExternalVelocity = launchDirection.normalized * GameTuning.DeathLaunchSpeed;
                target.IsDeathLaunched = true;
                target.IsFalling = true;
                target.IsRecovering = false;
                target.FloatTimer = 0f;
                target.DashTimer = 0f;
                target.FallScale = 1f;
            }
            if (actualDamage > 0f)
            {
                if (spellImpacts.Count >= 128) spellImpacts.RemoveAt(0);
                spellImpacts.Add(new SpellImpact
                {
                    Sequence = ++impactSequence, Weapon = sourceWeapon,
                    Position = CharacterHitbox.Center(target.Position), Direction = direction.normalized
                });
            }
        }

        private void ResolveObstacles(PlayerState player)
        {
            foreach (CircleObstacle obstacle in obstacles)
            {
                Vector2 separation;
                if (!CharacterHitbox.TryGetCircleSeparation(player.Position, obstacle.Center,
                    obstacle.Radius, out separation)) continue;
                Vector2 normal = separation.normalized;
                player.Position += separation;
                float intoObstacle = Vector2.Dot(player.ExternalVelocity, -normal);
                if (intoObstacle > 0f) player.ExternalVelocity += normal * intoObstacle * 1.8f;
            }
        }

        private void ResolvePlayerPairs()
        {
            for (int i = 0; i < players.Count; i++)
            {
                PlayerState a = players[i];
                if (a.IsUnavailable) continue;
                for (int j = i + 1; j < players.Count; j++)
                {
                    PlayerState b = players[j];
                    if (b.IsUnavailable) continue;
                    Vector2 separation;
                    if (!CharacterHitbox.TryGetCharacterSeparation(a.Position, b.Position,
                        out separation)) continue;
                    a.Position += separation * 0.5f;
                    b.Position -= separation * 0.5f;
                }
            }
        }

        private void CompleteFall(PlayerState player)
        {
            player.Lives--;
            player.Deaths++;
            if (player.LastAttackerId >= 0 && player.LastAttackerId != player.Id && player.LastHitAge <= 8f)
            {
                PlayerState attacker = players.FirstOrDefault(p => p.Id == player.LastAttackerId);
                if (attacker != null) attacker.Kos++;
            }
            if (player.Lives <= 0)
            {
                player.IsEliminated = true;
                player.FallScale = 0f;
                return;
            }
            player.RespawnTimer = GameTuning.RespawnDelay;
            player.IsDeathLaunched = false;
            player.ExternalVelocity = Vector2.zero;
            player.IsRecovering = false;
            player.Orbit.Reset();
            player.SpawnTimer = player.InvulnerabilityTimer = 0f;
            player.ShadowTimer = 0f;
        }

        private void BeginRespawn(PlayerState player)
        {
            player.Position = spawns[player.Id];
            player.SpawnTimer = GameTuning.SpawnDuration;
            player.InvulnerabilityTimer = GameTuning.SpawnDuration + GameTuning.SpawnProtectionDuration;
            player.ShadowTimer = 0f;
            player.ExternalVelocity = Vector2.zero;
            player.Hp = GameTuning.MaxHp;
            player.Weapon = WeaponType.Energy;
            player.Orbit.Reset();
            player.AttackCooldown = player.DashCooldown = player.DashTimer = player.HitStunTimer = 0f;
            player.AttackLockTimer = 0f;
            player.SlowTimer = 0f;
            player.SlowFactor = 1f;
            player.FloatTimer = GameTuning.RecoveryDuration;
            player.IsRecovering = false;
            player.FallScale = 1f;
            player.IsFalling = false;
            player.IsDeathLaunched = false;
            player.LastAttackerId = -1;
            player.LastHitAge = 99f;
        }

        private void ResolveMatchEnd()
        {
            List<PlayerState> survivors = players.Where(p => !p.IsEliminated).ToList();
            if (survivors.Count <= 1)
            {
                EndMatch(survivors.Count == 1 ? survivors[0].Name + " venceu!" : "Empate!");
                return;
            }
            if (MatchTimeRemaining > 0f) return;
            int bestLives = survivors.Max(p => p.Lives);
            List<PlayerState> byLives = survivors.Where(p => p.Lives == bestLives).ToList();
            float bestHp = byLives.Max(p => p.Hp);
            List<PlayerState> winners = byLives.Where(p => Mathf.Abs(p.Hp - bestHp) < 0.01f).ToList();
            EndMatch(winners.Count == 1 ? winners[0].Name + " venceu!" : "Empate!");
        }

        private void EndMatch(string result)
        {
            Phase = MatchPhase.GameOver;
            ResultText = result;
        }

        public bool IsInsideArena(Vector2 position)
        {
            return GoldenArenaLayout.Current.ContainsWorld(position);
        }

        public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            if (segment.sqrMagnitude <= 0.0001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, start + segment * t);
        }
    }

    public sealed class BotBrain
    {
        private readonly System.Random random;
        public BotBrain(int seed) { random = new System.Random(seed); }

        public PlayerCommand Think(MatchSimulation simulation, PlayerState bot)
        {
            if (bot.IsUnavailable) return default;
            PlayerState target = simulation.Players.Where(p => p.Id != bot.Id && !p.IsInvulnerable)
                .OrderBy(p => (p.Position - bot.Position).sqrMagnitude).FirstOrDefault();
            if (target == null) return default;
            Vector2 toTarget = target.Position - bot.Position;
            float distance = toTarget.magnitude;
            PickupState pickupTarget = bot.Weapon == WeaponType.Energy
                ? simulation.Pickups.OrderBy(p => (p.Position - bot.Position).sqrMagnitude).FirstOrDefault()
                : null;
            Vector2 inwardNormal;
            Vector2 closestEdge = GoldenArenaLayout.Current.ClosestFloorPoint(bot.Position, out inwardNormal);
            bool danger = !simulation.IsInsideArena(bot.Position) ||
                          (bot.Position - closestEdge).sqrMagnitude < 0.64f ||
                          Mathf.Abs(bot.Position.x) > GameTuning.ArenaHalfWidth * 0.78f ||
                          Mathf.Abs(bot.Position.y) > GameTuning.ArenaHalfHeight * 0.78f;
            Vector2 move;
            if (danger) move = -bot.Position.normalized;
            else if (pickupTarget != null && (pickupTarget.Position - bot.Position).sqrMagnitude < 20.25f)
                move = (pickupTarget.Position - bot.Position).normalized;
            else if (distance > 2.8f) move = toTarget.normalized;
            else if (distance < 1.35f) move = -toTarget.normalized;
            else move = new Vector2(-toTarget.y, toTarget.x).normalized * (bot.Id % 2 == 0 ? 1f : -1f);
            WeaponDefinition weapon = WeaponCatalog.Get(bot.Weapon);
            return new PlayerCommand
            {
                Move = move, Aim = toTarget.normalized,
                Attack = distance <= weapon.Range && bot.AttackCooldown <= 0f,
                DashPressed = danger && bot.DashCooldown <= 0f ||
                              bot.DashCooldown <= 0f && random.NextDouble() < 0.0025
            };
        }
    }
}
