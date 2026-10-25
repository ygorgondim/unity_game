import { PlayerState, GroundWeapon, ActiveProjectile, Particle, AttackVisual, WeaponType, Obstacle, WetPuddle } from '../types/game';
import { PHYSICS, WEAPONS, DEFAULT_ARENAS, getCornerSpawns } from './constants';
import { soundManager } from '../utils/audio';
import { ArenaMapData, mapPixel } from './arenaMap';

export interface CombatEvent {
  type: 'hit' | 'knockout' | 'pickup' | 'drop' | 'dash';
  attackerId?: string;
  victimId?: string;
  weaponType?: WeaponType;
  damage?: number;
  x?: number;
  y?: number;
}

export function updatePhysics(
  players: PlayerState[],
  groundWeapons: GroundWeapon[],
  projectiles: ActiveProjectile[],
  particles: Particle[],
  attackVisuals: AttackVisual[],
  wetPuddles: WetPuddle[],
  dt: number,
  arenaId: string,
  obstacles: Obstacle[] = [],
  arenaMap?: ArenaMapData
): CombatEvent[] {
  const events: CombatEvent[] = [];
  const arena = DEFAULT_ARENAS.find((a) => a.id === arenaId) || DEFAULT_ARENAS[0];
  const centerX = arena.width / 2;
  const centerY = arena.height / 2;

  // 1. Update Wet Puddles (Water Shot Ground Slow)
  for (let i = wetPuddles.length - 1; i >= 0; i--) {
    const puddle = wetPuddles[i];
    puddle.timer -= dt;

    if (puddle.timer <= 0) {
      wetPuddles.splice(i, 1);
      continue;
    }

    // Water ripple particles
    if (Math.random() < 0.2) {
      const ang = Math.random() * Math.PI * 2;
      const r = Math.random() * puddle.radius * 0.8;
      particles.push({
        x: puddle.x + Math.cos(ang) * r,
        y: puddle.y + Math.sin(ang) * r,
        vx: 0,
        vy: -10,
        size: Math.random() * 3 + 2,
        color: '#38bdf8',
        alpha: 0.6,
        life: 0.4,
        maxLife: 0.4,
        type: 'water'
      });
    }

    // Apply slow to players standing inside puddle
    for (const p of players) {
      if (p.isEliminated || p.isFalling) continue;
      const dist = Math.hypot(p.x - puddle.x, p.y - puddle.y);
      if (dist < puddle.radius + p.radius) {
        p.slowTimer = 0.25; // Sustained slow while in puddle
        p.slowFactor = 0.4; // 60% movement speed reduction
      }
    }
  }

  // 2. Update Active Projectiles
  for (let i = projectiles.length - 1; i >= 0; i--) {
    const proj = projectiles[i];

    // --- A. LIGHTNING BEAM (0.9s Cast Delay + Instant Zap) ---
    if (proj.type === 'lightning_beam') {
      if (proj.chargeTimer !== undefined && proj.chargeTimer > 0) {
        proj.chargeTimer -= dt;

        // Keep beam anchor attached to owner player
        const owner = players.find(p => p.id === proj.ownerId);
        if (owner) {
          proj.startX = owner.x;
          proj.startY = owner.y;
          const rawTx = owner.x + Math.cos(owner.facingAngle) * proj.radius; // radius holds range (650)
          const rawTy = owner.y + Math.sin(owner.facingAngle) * proj.radius;
          const hitPoint = getFirstObstacleIntersection(owner.x, owner.y, rawTx, rawTy, obstacles);
          proj.targetX = hitPoint.x;
          proj.targetY = hitPoint.y;
        }

        // Electric charge particles around player
        if (proj.startX !== undefined && proj.startY !== undefined) {
          const ang = Math.random() * Math.PI * 2;
          particles.push({
            x: proj.startX + Math.cos(ang) * 20,
            y: proj.startY + Math.sin(ang) * 20,
            vx: (Math.random() - 0.5) * 80,
            vy: (Math.random() - 0.5) * 80,
            size: Math.random() * 3 + 2,
            color: '#facc15',
            alpha: 1,
            life: 0.15,
            maxLife: 0.15,
            type: 'lightning'
          });
        }
        continue;
      }

      // Charge finished: Trigger Lightning Zap!
      if (!proj.isLightningActive) {
        proj.isLightningActive = true;
        proj.life = 0.25; // Visual ray duration
        soundManager.playMagic(); // Electric zap sound

        const owner = players.find(p => p.id === proj.ownerId);
        const sx = owner ? owner.x : (proj.startX || proj.x);
        const sy = owner ? owner.y : (proj.startY || proj.y);
        const facingAngle = owner ? owner.facingAngle : Math.atan2((proj.targetY || 0) - sy, (proj.targetX || 0) - sx);

        const rawTx = sx + Math.cos(facingAngle) * proj.radius;
        const rawTy = sy + Math.sin(facingAngle) * proj.radius;

        const hitPoint = getFirstObstacleIntersection(sx, sy, rawTx, rawTy, obstacles);
        const tx = hitPoint.x;
        const ty = hitPoint.y;
        proj.startX = sx;
        proj.startY = sy;
        proj.targetX = tx;
        proj.targetY = ty;

        // Beam hit detection against players
        for (const victim of players) {
          if (victim.isEliminated || victim.isFalling || victim.id === proj.ownerId) continue;

          // Distance from victim to line segment (sx,sy)->(tx,ty)
          const distToBeam = distanceToSegment(victim.x, victim.y, sx, sy, tx, ty);
          if (distToBeam < victim.radius + 20) {
            // Verify line of sight (ensure obstacle does not block line of sight between sx,sy and victim)
            const victimCheck = getFirstObstacleIntersection(sx, sy, victim.x, victim.y, obstacles);
            const distToVictim = Math.hypot(victim.x - sx, victim.y - sy);
            const distToObstacle = Math.hypot(victimCheck.x - sx, victimCheck.y - sy);

            if (!victimCheck.hit || distToObstacle >= distToVictim - victim.radius) {
              const attacker = players.find(p => p.id === proj.ownerId);
              const beamAngle = Math.atan2(ty - sy, tx - sx);
              applyDamageAndKnockback(attacker, victim, proj.damage, proj.knockbackBase, beamAngle, events, particles);
            }
          }
        }

        // If obstacle was struck, spawn electric sparks on wall surface
        if (hitPoint.hit) {
          for (let k = 0; k < 12; k++) {
            const ang = Math.random() * Math.PI * 2;
            const spd = Math.random() * 220 + 60;
            particles.push({
              x: tx,
              y: ty,
              vx: Math.cos(ang) * spd,
              vy: Math.sin(ang) * spd,
              size: Math.random() * 5 + 2,
              color: Math.random() < 0.6 ? '#facc15' : '#e0f2fe',
              alpha: 1,
              life: 0.3,
              maxLife: 0.3,
              type: 'lightning'
            });
          }
        }

        // Spawn dense lightning spark line along beam
        const steps = 15;
        for (let s = 0; s <= steps; s++) {
          const px = sx + (tx - sx) * (s / steps);
          const py = sy + (ty - sy) * (s / steps);
          for (let k = 0; k < 3; k++) {
            particles.push({
              x: px + (Math.random() - 0.5) * 25,
              y: py + (Math.random() - 0.5) * 25,
              vx: (Math.random() - 0.5) * 150,
              vy: (Math.random() - 0.5) * 150,
              size: Math.random() * 5 + 2,
              color: Math.random() < 0.5 ? '#fef08a' : '#38bdf8',
              alpha: 1,
              life: 0.3,
              maxLife: 0.3,
              type: 'lightning'
            });
          }
        }
      }

      proj.life -= dt;
      if (proj.life <= 0) {
        projectiles.splice(i, 1);
      }
      continue;
    }

    // --- B. EXPLOSIVE SHOT (Fuse Countdown & Area Explosion) ---
    if (proj.isExplodingPending) {
      if (proj.explodeTimer !== undefined) {
        proj.explodeTimer -= dt;

        // Pulsing warning sparks
        if (Math.random() < 0.8) {
          const ang = Math.random() * Math.PI * 2;
          const r = Math.random() * 40;
          particles.push({
            x: proj.x + Math.cos(ang) * r,
            y: proj.y + Math.sin(ang) * r,
            vx: 0,
            vy: -30,
            size: Math.random() * 4 + 2,
            color: Math.random() < 0.5 ? '#ef4444' : '#f97316',
            alpha: 1,
            life: 0.2,
            maxLife: 0.2,
            type: 'fire'
          });
        }

        if (proj.explodeTimer <= 0) {
          // BOOM! Trigger explosion
          soundManager.playAxeHeavy(); // Heavy boom
          const explosionRadius = 135;

          attackVisuals.push({
            id: Math.random().toString(),
            x: proj.x,
            y: proj.y,
            angle: 0,
            radius: explosionRadius,
            arcAngle: Math.PI * 2,
            color: '#f97316',
            type: 'explosion',
            life: 0.35,
            maxLife: 0.35
          });

          // Damage all players in explosion radius
          for (const victim of players) {
            if (victim.isEliminated || victim.isFalling) continue;
            const dist = Math.hypot(victim.x - proj.x, victim.y - proj.y);
            if (dist < explosionRadius + victim.radius) {
              const attacker = players.find(p => p.id === proj.ownerId);
              const expAngle = Math.atan2(victim.y - proj.y, victim.x - proj.x);
              applyDamageAndKnockback(attacker, victim, proj.damage, proj.knockbackBase, expAngle, events, particles);
            }
          }

          // Dense fire explosion particle burst
          for (let k = 0; k < 40; k++) {
            const ang = Math.random() * Math.PI * 2;
            const spd = Math.random() * 320 + 80;
            particles.push({
              x: proj.x,
              y: proj.y,
              vx: Math.cos(ang) * spd,
              vy: Math.sin(ang) * spd,
              size: Math.random() * 8 + 4,
              color: Math.random() < 0.6 ? '#ef4444' : '#fbbf24',
              alpha: 1,
              life: 0.5,
              maxLife: 0.5,
              type: 'fire'
            });
          }

          projectiles.splice(i, 1);
        }
      }
      continue;
    }

    // --- STANDARD PROJECTILE MOTION & LOGIC ---
    const previousProjectileX = proj.x;
    const previousProjectileY = proj.y;
    proj.x += proj.vx * dt;
    proj.y += proj.vy * dt;
    if (mapPixel(arenaMap, proj.x, proj.y) === 'collision') {
      proj.x = previousProjectileX;
      proj.y = previousProjectileY;
      projectiles.splice(i, 1);
      continue;
    }
    proj.life -= dt;

    // Water shot growth
    if (proj.type === 'water_bolt') {
      const lifeRatio = 1 - Math.max(0, proj.life / proj.maxLife);
      proj.scaleMultiplier = 1.0 + lifeRatio * 0.5; // Up to 150% size
      const currentRadius = proj.radius * (proj.scaleMultiplier || 1.0);

      // Droplet trail
      if (Math.random() < 0.7) {
        particles.push({
          x: proj.x,
          y: proj.y,
          vx: (Math.random() - 0.5) * 30,
          vy: (Math.random() - 0.5) * 30,
          size: Math.random() * 4 + 2,
          color: '#38bdf8',
          alpha: 0.8,
          life: 0.3,
          maxLife: 0.3,
          type: 'water'
        });
      }
    } else {
      // General trail particles
      if (Math.random() < 0.5) {
        particles.push({
          x: proj.x,
          y: proj.y,
          vx: (Math.random() - 0.5) * 30,
          vy: (Math.random() - 0.5) * 30,
          size: Math.random() * 4 + 2,
          color: proj.color,
          alpha: 0.8,
          life: 0.25,
          maxLife: 0.25,
          type: proj.type === 'slime_bolt' ? 'slime' : 'magic'
        });
      }
    }

    const currentRad = proj.radius * (proj.scaleMultiplier || 1.0);

    // Check hit against obstacles (walls / pillars)
    let hitObstacle = false;
    let obsCollisionNormal = { x: 0, y: 0 };

    for (const obs of obstacles) {
      if (obs.shape === 'circle') {
        const dx = proj.x - obs.x;
        const dy = proj.y - obs.y;
        const dist = Math.hypot(dx, dy);
        if (dist < currentRad + obs.radius) {
          hitObstacle = true;
          obsCollisionNormal = { x: dx / (dist || 1), y: dy / (dist || 1) };
          break;
        }
      } else {
        const hw = (obs.width || 100) / 2;
        const hh = (obs.height || 50) / 2;
        if (proj.x >= obs.x - hw - currentRad && proj.x <= obs.x + hw + currentRad &&
            proj.y >= obs.y - hh - currentRad && proj.y <= obs.y + hh + currentRad) {
          hitObstacle = true;
          // Determine collision normal for box
          const dx = proj.x - obs.x;
          const dy = proj.y - obs.y;
          if (Math.abs(dx / hw) > Math.abs(dy / hh)) {
            obsCollisionNormal = { x: Math.sign(dx), y: 0 };
          } else {
            obsCollisionNormal = { x: 0, y: Math.sign(dy) };
          }
          break;
        }
      }
    }

    // Check hit against arena outer bounds (for bouncing Slime or stopping Explosive)
    let hitBoundary = false;
    let boundaryNormal = { x: 0, y: 0 };

    if (arena.shape === 'circle') {
      const distCenter = Math.hypot(proj.x - centerX, proj.y - centerY);
      if (distCenter + currentRad > arena.radius) {
        hitBoundary = true;
        boundaryNormal = { x: (centerX - proj.x) / distCenter, y: (centerY - proj.y) / distCenter };
      }
    } else {
      const rw = arena.rectWidth || 1050;
      const rh = arena.rectHeight || 780;
      if (proj.x < centerX - rw / 2 + currentRad) { hitBoundary = true; boundaryNormal = { x: 1, y: 0 }; }
      else if (proj.x > centerX + rw / 2 - currentRad) { hitBoundary = true; boundaryNormal = { x: -1, y: 0 }; }
      else if (proj.y < centerY - rh / 2 + currentRad) { hitBoundary = true; boundaryNormal = { x: 0, y: 1 }; }
      else if (proj.y > centerY + rh / 2 - currentRad) { hitBoundary = true; boundaryNormal = { x: 0, y: -1 }; }
    }

    // --- HANDLE SLIME BOUNCE MECHANIC ---
    if (proj.type === 'slime_bolt' && (hitObstacle || hitBoundary) && proj.bouncesLeft !== undefined && proj.bouncesLeft > 0) {
      proj.bouncesLeft--;
      soundManager.playKick(); // Slime bounce squish sound

      const norm = hitObstacle ? obsCollisionNormal : boundaryNormal;
      // Reflect velocity vector V' = V - 2*(V.N)*N
      const dot = proj.vx * norm.x + proj.vy * norm.y;
      proj.vx = proj.vx - 2 * dot * norm.x;
      proj.vy = proj.vy - 2 * dot * norm.y;

      // Nudge slightly out of obstacle
      proj.x += norm.x * 6;
      proj.y += norm.y * 6;

      // Green slime splash sparks
      for (let k = 0; k < 6; k++) {
        const ang = Math.random() * Math.PI * 2;
        const spd = Math.random() * 100 + 30;
        particles.push({
          x: proj.x,
          y: proj.y,
          vx: Math.cos(ang) * spd,
          vy: Math.sin(ang) * spd,
          size: Math.random() * 4 + 2,
          color: '#22c55e',
          alpha: 1,
          life: 0.2,
          maxLife: 0.2,
          type: 'slime'
        });
      }
      continue;
    }

    // --- HANDLE IMPACT / EXPIRY FOR OTHER PROJECTILES ---
    if (hitObstacle || hitBoundary || proj.life <= 0) {
      if (proj.type === 'explosive_bolt') {
        // Explosive shot stops and arms 2-second fuse timer
        proj.isExplodingPending = true;
        proj.explodeTimer = 2.0;
        proj.vx = 0;
        proj.vy = 0;
        soundManager.playDash(); // Fuse arming sound
      } else if (proj.type === 'water_bolt') {
        // Water shot spawns wet puddle on impact
        wetPuddles.push({
          id: Math.random().toString(),
          x: proj.x,
          y: proj.y,
          radius: 75,
          timer: 5.0,
          maxTimer: 5.0
        });
        soundManager.playMagic();
        projectiles.splice(i, 1);
      } else {
        // Base / Slime out of bounces: destroy with sparks
        for (let k = 0; k < 8; k++) {
          const ang = Math.random() * Math.PI * 2;
          const spd = Math.random() * 120 + 40;
          particles.push({
            x: proj.x,
            y: proj.y,
            vx: Math.cos(ang) * spd,
            vy: Math.sin(ang) * spd,
            size: Math.random() * 4 + 2,
            color: proj.color,
            alpha: 1,
            life: 0.2,
            maxLife: 0.2,
            type: 'spark'
          });
        }
        projectiles.splice(i, 1);
      }
      continue;
    }

    // --- CHECK HIT AGAINST ENEMY PLAYERS ---
    for (const victim of players) {
      if (victim.isEliminated || victim.isFalling || victim.id === proj.ownerId) continue;

      const dist = Math.hypot(victim.x - proj.x, victim.y - proj.y);
      if (dist < victim.radius + currentRad) {
        if (proj.type === 'explosive_bolt') {
          // Arm fuse on player hit!
          proj.isExplodingPending = true;
          proj.explodeTimer = 2.0;
          proj.vx = 0;
          proj.vy = 0;
          break;
        } else if (proj.type === 'water_bolt') {
          const attacker = players.find((p) => p.id === proj.ownerId);
          applyDamageAndKnockback(attacker, victim, proj.damage, proj.knockbackBase, Math.atan2(proj.vy, proj.vx), events, particles);

          // Leave puddle at victim feet
          wetPuddles.push({
            id: Math.random().toString(),
            x: victim.x,
            y: victim.y,
            radius: 75,
            timer: 5.0,
            maxTimer: 5.0
          });
          projectiles.splice(i, 1);
          break;
        } else if (proj.type === 'slime_bolt') {
          const attacker = players.find((p) => p.id === proj.ownerId);
          applyDamageAndKnockback(attacker, victim, proj.damage, proj.knockbackBase, Math.atan2(proj.vy, proj.vx), events, particles);

          if (proj.bouncesLeft !== undefined && proj.bouncesLeft > 0) {
            proj.bouncesLeft--;
            soundManager.playKick(); // Slime bounce squish sound

            const dx = proj.x - victim.x;
            const dy = proj.y - victim.y;
            const distNorm = Math.hypot(dx, dy) || 1;
            const nx = dx / distNorm;
            const ny = dy / distNorm;

            // Reflect velocity vector away from victim
            const dot = proj.vx * nx + proj.vy * ny;
            if (dot < 0) {
              proj.vx = proj.vx - 2 * dot * nx;
              proj.vy = proj.vy - 2 * dot * ny;
            }

            // Nudge projectile clearly outside victim radius to prevent multi-hit overlap
            proj.x = victim.x + nx * (victim.radius + currentRad + 5);
            proj.y = victim.y + ny * (victim.radius + currentRad + 5);

            // Green slime splash particles
            for (let k = 0; k < 6; k++) {
              const ang = Math.random() * Math.PI * 2;
              const spd = Math.random() * 100 + 30;
              particles.push({
                x: proj.x,
                y: proj.y,
                vx: Math.cos(ang) * spd,
                vy: Math.sin(ang) * spd,
                size: Math.random() * 4 + 2,
                color: '#22c55e',
                alpha: 1,
                life: 0.2,
                maxLife: 0.2,
                type: 'slime'
              });
            }
          } else {
            // Out of bounces: destroy projectile
            for (let k = 0; k < 8; k++) {
              const ang = Math.random() * Math.PI * 2;
              const spd = Math.random() * 120 + 40;
              particles.push({
                x: proj.x,
                y: proj.y,
                vx: Math.cos(ang) * spd,
                vy: Math.sin(ang) * spd,
                size: Math.random() * 4 + 2,
                color: proj.color,
                alpha: 1,
                life: 0.2,
                maxLife: 0.2,
                type: 'spark'
              });
            }
            projectiles.splice(i, 1);
          }
          break;
        } else {
          // Base mage bolt hit
          const attacker = players.find((p) => p.id === proj.ownerId);
          applyDamageAndKnockback(attacker, victim, proj.damage, proj.knockbackBase, Math.atan2(proj.vy, proj.vx), events, particles);

          projectiles.splice(i, 1);
          break;
        }
      }
    }
  }

  // 3. Ground Weapon Drop Pickups & Despawn
  for (let i = groundWeapons.length - 1; i >= 0; i--) {
    const gw = groundWeapons[i];
    gw.timer -= dt;

    if (gw.timer <= 0) {
      groundWeapons.splice(i, 1);
      continue;
    }

    // Players walking over weapon pick up buff
    for (const p of players) {
      if (p.isEliminated || p.isFalling) continue;
      if (p.weapon === 'base_mage') {
        const dist = Math.hypot(p.x - gw.x, p.y - gw.y);
        if (dist < p.radius + 20) {
          p.weapon = gw.type;
          p.lightAttackCooldown = 0;
          groundWeapons.splice(i, 1);
          soundManager.playPickup();
          events.push({ type: 'pickup', attackerId: p.id, weaponType: gw.type, x: p.x, y: p.y });
          break;
        }
      }
    }
  }

  // 4. Update Players Motion & Physics
  for (const p of players) {
    if (p.isEliminated) continue;

    // Process Falling off Arena
    if (p.isFalling) {
      p.fallScale -= dt * 1.8;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.vx *= 0.95;
      p.vy *= 0.95;

      if (p.fallScale <= 0) {
        // Knockout!
        p.isFalling = false;
        p.stocks -= 1;
        p.stats.deaths += 1;
        events.push({ type: 'knockout', victimId: p.id, x: p.x, y: p.y });
        soundManager.playKnockout();

        // Spawn ringout particle burst
        for (let k = 0; k < 30; k++) {
          const ang = Math.random() * Math.PI * 2;
          const spd = Math.random() * 300 + 100;
          particles.push({
            x: p.x,
            y: p.y,
            vx: Math.cos(ang) * spd,
            vy: Math.sin(ang) * spd,
            size: Math.random() * 8 + 4,
            color: p.color,
            alpha: 1,
            life: 0.8,
            maxLife: 0.8,
            type: 'ringout'
          });
        }

        if (p.stocks <= 0) {
          p.isEliminated = true;
        } else {
          // Respawn in designated corner spawn
          const spawns = getCornerSpawns(arena);
          const cornerIdx = players.indexOf(p) % spawns.length;
          const spawn = spawns[cornerIdx >= 0 ? cornerIdx : 0];
          p.hp = p.maxHp;
          p.x = spawn.x;
          p.y = spawn.y;
          p.facingAngle = spawn.facingAngle;
          p.vx = 0;
          p.vy = 0;
          p.weapon = 'base_mage';
          p.fallScale = 1;
          p.floatTimer = 3.0;
        }
      }
      continue;
    }

    // Cooldown updates
    if (p.lightAttackCooldown > 0) p.lightAttackCooldown = Math.max(0, p.lightAttackCooldown - dt);
    if (p.dashCooldown > 0) p.dashCooldown = Math.max(0, p.dashCooldown - dt);
    if (p.hitStunTimer > 0) p.hitStunTimer = Math.max(0, p.hitStunTimer - dt);

    // Water slow timer
    if (p.slowTimer > 0) {
      p.slowTimer -= dt;
    }

    // Velocity update & Friction
    if (p.isDashing) {
      const previousX = p.x;
      const previousY = p.y;
      p.dashTimer -= dt;
      p.x += p.dashVx * dt;
      p.y += p.dashVy * dt;
      if (p.dashTimer <= 0) {
        p.isDashing = false;
      }
      if (mapPixel(arenaMap, p.x, p.y) === 'collision') {
        const canSlideX = mapPixel(arenaMap, p.x, previousY) !== 'collision';
        const canSlideY = mapPixel(arenaMap, previousX, p.y) !== 'collision';
        if (canSlideX) p.y = previousY;
        else if (canSlideY) p.x = previousX;
        else { p.x = previousX; p.y = previousY; }
        p.isDashing = false;
        p.dashVx = 0;
        p.dashVy = 0;
      }
    } else {
      const previousX = p.x;
      const previousY = p.y;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      const friction = (p.hitStunTimer > 0 || Math.hypot(p.vx, p.vy) > PHYSICS.PLAYER_SPEED * 1.2) ? 0.955 : PHYSICS.FRICTION;
      p.vx *= friction;
      p.vy *= friction;
      if (mapPixel(arenaMap, p.x, p.y) === 'collision') {
        const canSlideX = mapPixel(arenaMap, p.x, previousY) !== 'collision';
        const canSlideY = mapPixel(arenaMap, previousX, p.y) !== 'collision';
        if (canSlideX) {
          p.y = previousY;
          p.vy = 0;
        } else if (canSlideY) {
          p.x = previousX;
          p.vx = 0;
        } else {
          p.x = previousX;
          p.y = previousY;
          p.vx = 0;
          p.vy = 0;
        }
      }
    }

    // Player vs Player Push Collisions
    for (const p2 of players) {
      if (p2 === p || p2.isEliminated || p2.isFalling) continue;
      const dx = p2.x - p.x;
      const dy = p2.y - p.y;
      const dist = Math.hypot(dx, dy);
      const minDist = p.radius + p2.radius;
      if (dist < minDist && dist > 0) {
        const overlap = minDist - dist;
        p.x -= (dx / dist) * (overlap / 2);
        p.y -= (dy / dist) * (overlap / 2);
        p2.x += (dx / dist) * (overlap / 2);
        p2.y += (dy / dist) * (overlap / 2);
      }
    }

    // Player vs Obstacles Solid Collisions (with wall bounce)
    for (const obs of obstacles) {
      if (obs.shape === 'circle') {
        const dx = p.x - obs.x;
        const dy = p.y - obs.y;
        const dist = Math.hypot(dx, dy);
        const minDist = p.radius + obs.radius;
        if (dist < minDist && dist > 0) {
          const nx = dx / dist;
          const ny = dy / dist;
          const overlap = minDist - dist;
          p.x += nx * overlap;
          p.y += ny * overlap;

          // Velocity bounce reflection on obstacle impact
          const dot = p.vx * nx + p.vy * ny;
          if (dot < 0) {
            const restitution = 0.8;
            p.vx -= (1 + restitution) * dot * nx;
            p.vy -= (1 + restitution) * dot * ny;

            if (Math.hypot(p.vx, p.vy) > 80) {
              soundManager.playHit(5);
              for (let k = 0; k < 5; k++) {
                const angle = Math.atan2(ny, nx) + (Math.random() - 0.5);
                const spd = Math.random() * 120 + 40;
                particles.push({
                  x: p.x - nx * p.radius,
                  y: p.y - ny * p.radius,
                  vx: Math.cos(angle) * spd,
                  vy: Math.sin(angle) * spd,
                  size: Math.random() * 3 + 2,
                  color: '#fde047',
                  alpha: 1,
                  life: 0.25,
                  maxLife: 0.25,
                  type: 'spark'
                });
              }
            }
          }

          if (p.isDashing) {
            p.isDashing = false;
            p.dashVx = 0;
            p.dashVy = 0;
          }
        }
      } else {
        const hw = (obs.width || 100) / 2;
        const hh = (obs.height || 50) / 2;
        const closestX = Math.max(obs.x - hw, Math.min(obs.x + hw, p.x));
        const closestY = Math.max(obs.y - hh, Math.min(obs.y + hh, p.y));
        let dx = p.x - closestX;
        let dy = p.y - closestY;
        let dist = Math.hypot(dx, dy);
        if (dist < p.radius) {
          if (dist === 0) {
            dx = p.x - obs.x;
            dy = p.y - obs.y;
            dist = Math.hypot(dx, dy) || 1;
          }
          const nx = dx / dist;
          const ny = dy / dist;
          const overlap = p.radius - dist;
          p.x += nx * overlap;
          p.y += ny * overlap;

          // Velocity bounce reflection on obstacle wall impact
          const dot = p.vx * nx + p.vy * ny;
          if (dot < 0) {
            const restitution = 0.8;
            p.vx -= (1 + restitution) * dot * nx;
            p.vy -= (1 + restitution) * dot * ny;

            if (Math.hypot(p.vx, p.vy) > 80) {
              soundManager.playHit(5);
              for (let k = 0; k < 5; k++) {
                const angle = Math.atan2(ny, nx) + (Math.random() - 0.5);
                const spd = Math.random() * 120 + 40;
                particles.push({
                  x: p.x - nx * p.radius,
                  y: p.y - ny * p.radius,
                  vx: Math.cos(angle) * spd,
                  vy: Math.sin(angle) * spd,
                  size: Math.random() * 3 + 2,
                  color: '#fde047',
                  alpha: 1,
                  life: 0.25,
                  maxLife: 0.25,
                  type: 'spark'
                });
              }
            }
          }

          if (p.isDashing) {
            p.isDashing = false;
            p.dashVx = 0;
            p.dashVy = 0;
          }
        }
      }
    }

    // Check boundary check for arena fall
    let isOutside = false;
    if (arenaMap) {
      isOutside = mapPixel(arenaMap, p.x, p.y) === 'fall';
    } else if (arena.shape === 'circle') {
      const distFromCenter = Math.hypot(p.x - centerX, p.y - centerY);
      if (distFromCenter > arena.radius) {
        isOutside = true;
      }
    } else if (arena.shape === 'rectangle') {
      const rw = arena.rectWidth || 1050;
      const rh = arena.rectHeight || 780;
      if (
        p.x < centerX - rw / 2 ||
        p.x > centerX + rw / 2 ||
        p.y < centerY - rh / 2 ||
        p.y > centerY + rh / 2
      ) {
        isOutside = true;
      }
    }

    if (isOutside) {
      if (!p.isFalling) {
        // Player is floating outside the arena boundaries!
        if (p.floatTimer === undefined) p.floatTimer = 3.0;
        p.floatTimer = Math.max(0, p.floatTimer - dt);

        // Spawn float / hover magic particles while floating outside
        if (Math.random() < 0.35) {
          particles.push({
            x: p.x + (Math.random() - 0.5) * p.radius,
            y: p.y + p.radius + Math.random() * 6,
            vx: (Math.random() - 0.5) * 30,
            vy: Math.random() * 40 + 20,
            size: Math.random() * 3 + 2,
            color: '#38bdf8',
            alpha: 1,
            life: 0.3,
            maxLife: 0.3,
            type: 'spark'
          });
        }

        if (p.floatTimer <= 0) {
          // 3 seconds expired: fall into the void!
          p.isFalling = true;
          p.fallScale = 1.0;
        }
      }
    } else {
      // Safely back inside the arena! Reset 3.0s float grace
      p.floatTimer = 3.0;
    }
  }

  // 5. Update Attack Visuals & Particles
  for (let i = attackVisuals.length - 1; i >= 0; i--) {
    const av = attackVisuals[i];
    av.life -= dt;
    if (av.life <= 0) attackVisuals.splice(i, 1);
  }

  for (let i = particles.length - 1; i >= 0; i--) {
    const pt = particles[i];
    pt.x += pt.vx * dt;
    pt.y += pt.vy * dt;
    pt.life -= dt;
    pt.alpha = Math.max(0, pt.life / pt.maxLife);
    if (pt.life <= 0) particles.splice(i, 1);
  }

  return events;
}

export function applyDamageAndKnockback(
  attacker: PlayerState | undefined,
  victim: PlayerState,
  damage: number,
  baseKnockback: number,
  knockbackAngle: number,
  events: CombatEvent[],
  particles: Particle[]
) {
  victim.hp = Math.max(0, victim.hp - damage);

  if (attacker) {
    attacker.stats.damageDealt += damage;
  }
  victim.stats.damageTaken += damage;

  const missingHpRatio = (victim.maxHp - victim.hp) / victim.maxHp;

  // Progressive knockback scaling: a controlled increase as HP drops.
  // Tuned for the smaller arena so hits remain impactful without excessive
  // launches across the map.
  let knockbackMultiplier = Math.min(3.4, 0.7 + Math.pow(Math.max(0, missingHpRatio), 1.5) * 2.7);

  if (victim.hp === 0) {
    // Super Blast Knockout repel when HP reaches 0
    knockbackMultiplier = 6.5;
    victim.hitStunTimer = 0.85;

    // Intense ringout / KO blast particle burst
    for (let k = 0; k < 28; k++) {
      const ang = knockbackAngle + (Math.random() - 0.5) * 1.8;
      const spd = Math.random() * 450 + 150;
      particles.push({
        x: victim.x,
        y: victim.y,
        vx: Math.cos(ang) * spd,
        vy: Math.sin(ang) * spd,
        size: Math.random() * 6 + 3,
        color: '#ef4444',
        alpha: 1,
        life: 0.5,
        maxLife: 0.5,
        type: 'ringout'
      });
    }
  } else {
    victim.hitStunTimer = 0.45;
  }

  const totalKnockback = baseKnockback * knockbackMultiplier;

  victim.vx += Math.cos(knockbackAngle) * totalKnockback;
  victim.vy += Math.sin(knockbackAngle) * totalKnockback;

  soundManager.playHit(damage);

  events.push({
    type: 'hit',
    attackerId: attacker?.id,
    victimId: victim.id,
    damage,
    x: victim.x,
    y: victim.y
  });

  for (let k = 0; k < 12; k++) {
    const ang = knockbackAngle + (Math.random() - 0.5) * 1.5;
    const spd = Math.random() * 200 + 50;
    particles.push({
      x: victim.x,
      y: victim.y,
      vx: Math.cos(ang) * spd,
      vy: Math.sin(ang) * spd,
      size: Math.random() * 4 + 2,
      color: '#fef08a',
      alpha: 1,
      life: 0.3,
      maxLife: 0.3,
      type: 'spark'
    });
  }
}

export function performLightAttack(
  player: PlayerState,
  players: PlayerState[],
  projectiles: ActiveProjectile[],
  attackVisuals: AttackVisual[],
  events: CombatEvent[],
  particles: Particle[],
  obstacles: Obstacle[] = []
) {
  if (player.lightAttackCooldown > 0 || player.isFalling || player.isEliminated) return;

  const wData = WEAPONS[player.weapon];
  player.lightAttackCooldown = wData.lightAttack.cooldown;

  soundManager.playMagic();

  const cos = Math.cos(player.facingAngle);
  const sin = Math.sin(player.facingAngle);

  if (player.weapon === 'base_mage') {
    projectiles.push({
      id: Math.random().toString(),
      ownerId: player.id,
      type: 'base_bolt',
      x: player.x + cos * 25,
      y: player.y + sin * 25,
      vx: cos * wData.lightAttack.speed,
      vy: sin * wData.lightAttack.speed,
      radius: 10,
      damage: wData.lightAttack.damage,
      knockbackBase: wData.lightAttack.knockbackBase,
      life: wData.lightAttack.range / wData.lightAttack.speed,
      maxLife: wData.lightAttack.range / wData.lightAttack.speed,
      color: '#a855f7'
    });
  } else if (player.weapon === 'explosive') {
    projectiles.push({
      id: Math.random().toString(),
      ownerId: player.id,
      type: 'explosive_bolt',
      x: player.x + cos * 25,
      y: player.y + sin * 25,
      vx: cos * wData.lightAttack.speed,
      vy: sin * wData.lightAttack.speed,
      radius: 12,
      damage: wData.lightAttack.damage,
      knockbackBase: wData.lightAttack.knockbackBase,
      life: wData.lightAttack.range / wData.lightAttack.speed,
      maxLife: wData.lightAttack.range / wData.lightAttack.speed,
      color: '#f97316'
    });
  } else if (player.weapon === 'slime') {
    projectiles.push({
      id: Math.random().toString(),
      ownerId: player.id,
      type: 'slime_bolt',
      x: player.x + cos * 25,
      y: player.y + sin * 25,
      vx: cos * wData.lightAttack.speed,
      vy: sin * wData.lightAttack.speed,
      radius: 10,
      damage: wData.lightAttack.damage,
      knockbackBase: wData.lightAttack.knockbackBase,
      life: wData.lightAttack.range / wData.lightAttack.speed,
      maxLife: wData.lightAttack.range / wData.lightAttack.speed,
      color: '#22c55e',
      bouncesLeft: 6
    });
  } else if (player.weapon === 'water') {
    projectiles.push({
      id: Math.random().toString(),
      ownerId: player.id,
      type: 'water_bolt',
      x: player.x + cos * 25,
      y: player.y + sin * 25,
      vx: cos * wData.lightAttack.speed,
      vy: sin * wData.lightAttack.speed,
      radius: 12,
      damage: wData.lightAttack.damage,
      knockbackBase: wData.lightAttack.knockbackBase,
      life: wData.lightAttack.range / wData.lightAttack.speed,
      maxLife: wData.lightAttack.range / wData.lightAttack.speed,
      color: '#06b6d4',
      scaleMultiplier: 1.0
    });
  } else if (player.weapon === 'lightning') {
    const sx = player.x + cos * 25;
    const sy = player.y + sin * 25;
    const rawTx = player.x + cos * wData.lightAttack.range;
    const rawTy = player.y + sin * wData.lightAttack.range;
    const hitPoint = getFirstObstacleIntersection(sx, sy, rawTx, rawTy, obstacles);

    projectiles.push({
      id: Math.random().toString(),
      ownerId: player.id,
      type: 'lightning_beam',
      x: sx,
      y: sy,
      vx: 0,
      vy: 0,
      radius: wData.lightAttack.range, // Holds range length (650)
      damage: wData.lightAttack.damage,
      knockbackBase: wData.lightAttack.knockbackBase,
      life: 1.2,
      maxLife: 1.2,
      color: '#eab308',
      chargeTimer: 0.9,
      isLightningActive: false,
      startX: sx,
      startY: sy,
      targetX: hitPoint.x,
      targetY: hitPoint.y
    });
  }
}

export function performSkill(
  player: PlayerState,
  players: PlayerState[],
  projectiles: ActiveProjectile[],
  attackVisuals: AttackVisual[],
  events: CombatEvent[],
  particles: Particle[],
  obstacles: Obstacle[] = []
) {
  // Right-click skill removed as per prompt
}

export function performDash(player: PlayerState) {
  if (player.dashCooldown > 0 || player.isFalling || player.isEliminated) return;

  player.dashCooldown = PHYSICS.DASH_COOLDOWN;
  player.isDashing = true;
  player.dashTimer = PHYSICS.DASH_DURATION;

  const moveLen = Math.hypot(player.vx, player.vy);
  const angle = moveLen > 20 ? Math.atan2(player.vy, player.vx) : player.facingAngle;

  const isFloatingOutside = player.floatTimer !== undefined && player.floatTimer < 3.0 && !player.isFalling;
  const dashSpeedMult = isFloatingOutside ? 1.5 : 1.0;

  player.dashVx = Math.cos(angle) * PHYSICS.DASH_SPEED * dashSpeedMult;
  player.dashVy = Math.sin(angle) * PHYSICS.DASH_SPEED * dashSpeedMult;

  soundManager.playDash();
}

export function dropWeapon(player: PlayerState, groundWeapons: GroundWeapon[]) {
  if (player.weapon === 'base_mage' || player.isFalling || player.isEliminated) return;

  const droppedType = player.weapon;
  player.weapon = 'base_mage';

  const dropX = player.x + Math.cos(player.facingAngle) * 45;
  const dropY = player.y + Math.sin(player.facingAngle) * 45;

  groundWeapons.push({
    id: Math.random().toString(),
    type: droppedType,
    x: dropX,
    y: dropY,
    spawnTime: performance.now(),
    duration: PHYSICS.WEAPON_LIFETIME,
    timer: PHYSICS.WEAPON_LIFETIME
  });

  soundManager.playPickup();
}

export function getFirstObstacleIntersection(
  x1: number,
  y1: number,
  x2: number,
  y2: number,
  obstacles: Obstacle[] = []
): { x: number; y: number; hit: boolean } {
  let minT = 1.0;
  let hit = false;
  const dx = x2 - x1;
  const dy = y2 - y1;
  const len = Math.hypot(dx, dy);

  if (len < 0.0001 || !obstacles || obstacles.length === 0) {
    return { x: x2, y: y2, hit: false };
  }

  for (const obs of obstacles) {
    if (obs.shape === 'circle') {
      const cx = obs.x;
      const cy = obs.y;
      const r = obs.radius;

      const fx = x1 - cx;
      const fy = y1 - cy;

      const a = dx * dx + dy * dy;
      const b = 2 * (fx * dx + fy * dy);
      const c = fx * fx + fy * fy - r * r;

      if (c <= 0) {
        minT = 0;
        hit = true;
        break;
      }

      const disc = b * b - 4 * a * c;
      if (disc >= 0) {
        const t = (-b - Math.sqrt(disc)) / (2 * a);
        if (t >= 0 && t < minT) {
          minT = t;
          hit = true;
        }
      }
    } else {
      const hw = (obs.width || 100) / 2;
      const hh = (obs.height || 50) / 2;
      const minX = obs.x - hw;
      const maxX = obs.x + hw;
      const minY = obs.y - hh;
      const maxY = obs.y + hh;

      let tMinX = -Infinity;
      let tMaxX = Infinity;
      if (Math.abs(dx) > 1e-9) {
        tMinX = (minX - x1) / dx;
        tMaxX = (maxX - x1) / dx;
        if (tMinX > tMaxX) {
          const tmp = tMinX;
          tMinX = tMaxX;
          tMaxX = tmp;
        }
      } else {
        if (x1 < minX || x1 > maxX) continue;
      }

      let tMinY = -Infinity;
      let tMaxY = Infinity;
      if (Math.abs(dy) > 1e-9) {
        tMinY = (minY - y1) / dy;
        tMaxY = (maxY - y1) / dy;
        if (tMinY > tMaxY) {
          const tmp = tMinY;
          tMinY = tMaxY;
          tMaxY = tmp;
        }
      } else {
        if (y1 < minY || y1 > maxY) continue;
      }

      const tEnter = Math.max(tMinX, tMinY, 0);
      const tExit = Math.min(tMaxX, tMaxY, 1);

      if (tEnter <= tExit && tEnter < minT) {
        minT = tEnter;
        hit = true;
      }
    }
  }

  return {
    x: x1 + minT * dx,
    y: y1 + minT * dy,
    hit
  };
}

function distanceToSegment(px: number, py: number, x1: number, y1: number, x2: number, y2: number): number {
  const dx = x2 - x1;
  const dy = y2 - y1;
  const lenSq = dx * dx + dy * dy;
  if (lenSq === 0) return Math.hypot(px - x1, py - y1);
  let t = ((px - x1) * dx + (py - y1) * dy) / lenSq;
  t = Math.max(0, Math.min(1, t));
  const projX = x1 + t * dx;
  const projY = y1 + t * dy;
  return Math.hypot(px - projX, py - projY);
}
