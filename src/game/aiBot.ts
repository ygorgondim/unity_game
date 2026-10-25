import { PlayerState, GroundWeapon, ControllerInput, ArenaConfig, ActiveProjectile } from '../types/game';
import { DEFAULT_ARENAS, WEAPONS } from './constants';

export function computeBotInput(
  bot: PlayerState,
  allPlayers: PlayerState[],
  groundWeapons: GroundWeapon[],
  arenaId: string,
  projectiles: ActiveProjectile[] = []
): ControllerInput {
  const arena: ArenaConfig = DEFAULT_ARENAS.find((a) => a.id === arenaId) || DEFAULT_ARENAS[0];
  const centerX = arena.width / 2;
  const centerY = arena.height / 2;

  let moveX = 0;
  let moveY = 0;
  let aimX = 0;
  let aimY = 0;
  let hasAimInput = false;
  let attack = false;
  let skill = false;
  let dash = false;
  let dropWeapon = false;

  const difficulty = bot.botDifficulty || 'medium';

  // 1. ARENA BOUNDARY SAFETY: If bot gets close to arena ring edge, pull back to center!
  let isNearEdge = false;
  let centerForceX = 0;
  let centerForceY = 0;

  if (arena.shape === 'rectangle') {
    const halfW = (arena.rectWidth || 1050) / 2;
    const halfH = (arena.rectHeight || 780) / 2;
    const safeW = halfW * 0.78;
    const safeH = halfH * 0.78;

    const dx = bot.x - centerX;
    const dy = bot.y - centerY;

    if (Math.abs(dx) > safeW || Math.abs(dy) > safeH) {
      isNearEdge = true;
      const angleToCenter = Math.atan2(centerY - bot.y, centerX - bot.x);
      const excessX = Math.max(0, Math.abs(dx) - safeW) / (halfW - safeW);
      const excessY = Math.max(0, Math.abs(dy) - safeH) / (halfH - safeH);
      const urgency = Math.max(excessX, excessY);

      centerForceX = Math.cos(angleToCenter) * (1 + urgency * 2);
      centerForceY = Math.sin(angleToCenter) * (1 + urgency * 2);

      if ((Math.abs(dx) > halfW * 0.90 || Math.abs(dy) > halfH * 0.90) && bot.dashCooldown <= 0) {
        dash = true; // Emergency recovery dash back onto stage
      }
    }
  } else {
    // Circle arena (Coliseu Celestial)
    const distToCenter = Math.hypot(bot.x - centerX, bot.y - centerY);
    const safeRadius = arena.radius * 0.78;

    if (distToCenter > safeRadius) {
      isNearEdge = true;
      const angleToCenter = Math.atan2(centerY - bot.y, centerX - bot.x);
      const urgency = (distToCenter - safeRadius) / (arena.radius - safeRadius);
      centerForceX = Math.cos(angleToCenter) * (1 + urgency * 2);
      centerForceY = Math.sin(angleToCenter) * (1 + urgency * 2);

      if (distToCenter > arena.radius * 0.88 && bot.dashCooldown <= 0) {
        dash = true; // Emergency recovery dash back onto stage
      }
    }
  }

  // Urgent Float Recovery: If bot is floating outside, override movement and dash back!
  if (bot.floatTimer !== undefined && bot.floatTimer < 2.95) {
    isNearEdge = true;
    const angleToCenter = Math.atan2(centerY - bot.y, centerX - bot.x);
    centerForceX = Math.cos(angleToCenter) * 3.5;
    centerForceY = Math.sin(angleToCenter) * 3.5;
    if (bot.dashCooldown <= 0) {
      dash = true; // Immediate recovery dash
    }
  }

  // 2. ANTI-CROWDING / SEPARATION FORCE: Maintain personal space from all other alive players
  let sepX = 0;
  let sepY = 0;
  const desiredSeparation = difficulty === 'hard' ? 110 : difficulty === 'medium' ? 85 : 60;

  for (const p of allPlayers) {
    if (p.id === bot.id || p.isEliminated || p.isFalling) continue;
    const dx = bot.x - p.x;
    const dy = bot.y - p.y;
    const dist = Math.hypot(dx, dy);
    if (dist > 0 && dist < desiredSeparation) {
      const force = Math.pow((desiredSeparation - dist) / desiredSeparation, 1.2);
      sepX += (dx / dist) * force * 1.8;
      sepY += (dy / dist) * force * 1.8;
    }
  }

  // 3. PROJECTILE DODGING: Detect incoming enemy projectiles and evade
  let dodgeX = 0;
  let dodgeY = 0;
  const dodgePerceptionRadius = difficulty === 'hard' ? 350 : difficulty === 'medium' ? 240 : 130;
  const dodgeReactionChance = difficulty === 'hard' ? 0.92 : difficulty === 'medium' ? 0.65 : 0.30;

  for (const proj of projectiles) {
    if (proj.ownerId === bot.id) continue;

    const pdx = bot.x - proj.x;
    const pdy = bot.y - proj.y;
    const pDist = Math.hypot(pdx, pdy);

    if (pDist < dodgePerceptionRadius) {
      const speed = Math.hypot(proj.vx, proj.vy);
      if (speed > 10) {
        const pvx = proj.vx / speed;
        const pvy = proj.vy / speed;

        // Heading dot product
        const headingDot = pdx * pvx + pdy * pvy;
        if (headingDot > 0) {
          // Distance from bot to ray
          const projXOnRay = proj.x + pvx * headingDot;
          const projYOnRay = proj.y + pvy * headingDot;
          const distToRay = Math.hypot(bot.x - projXOnRay, bot.y - projYOnRay);

          const safeMargin = bot.radius + (proj.radius || 10) + 25;
          if (distToRay < safeMargin) {
            // Perpendicular dodge direction
            let perpX = -pvy;
            let perpY = pvx;

            const side = (bot.x - proj.x) * pvy - (bot.y - proj.y) * pvx;
            if (side < 0) {
              perpX = -perpX;
              perpY = -perpY;
            }

            if (Math.random() < dodgeReactionChance) {
              const urgency = 1 - pDist / dodgePerceptionRadius;
              dodgeX += perpX * (1.2 + urgency * 1.5);
              dodgeY += perpY * (1.2 + urgency * 1.5);

              // Urgent dash dodge if projectile is dangerously close
              if (pDist < 160 && bot.dashCooldown <= 0 && difficulty !== 'easy' && Math.random() < 0.6) {
                dash = true;
              }
            }
          }
        }
      }
    }
  }

  // 4. MAIN COMBAT / NAVIGATION DECISIONS
  let combatX = 0;
  let combatY = 0;

  // Search for weapon buff if holding base mage
  let targetWeapon: GroundWeapon | null = null;
  if (bot.weapon === 'base_mage' && groundWeapons.length > 0) {
    let minDist = Infinity;
    for (const gw of groundWeapons) {
      const d = Math.hypot(gw.x - bot.x, gw.y - bot.y);
      if (d < minDist) {
        minDist = d;
        targetWeapon = gw;
      }
    }
  }

  if (targetWeapon) {
    const angle = Math.atan2(targetWeapon.y - bot.y, targetWeapon.x - bot.x);
    combatX = Math.cos(angle) * 1.2;
    combatY = Math.sin(angle) * 1.2;
  } else {
    // Target closest alive enemy
    let targetEnemy: PlayerState | null = null;
    let minDist = Infinity;
    for (const p of allPlayers) {
      if (p.id === bot.id || p.isEliminated || p.isFalling) continue;
      const d = Math.hypot(p.x - bot.x, p.y - bot.y);
      if (d < minDist) {
        minDist = d;
        targetEnemy = p;
      }
    }

    if (targetEnemy) {
      const dx = targetEnemy.x - bot.x;
      const dy = targetEnemy.y - bot.y;
      const dist = Math.hypot(dx, dy) || 1;
      const angleToEnemy = Math.atan2(dy, dx);

      // Aim towards enemy
      aimX = dx;
      aimY = dy;
      hasAimInput = true;

      const weaponData = WEAPONS[bot.weapon];
      const maxRange = weaponData.lightAttack.range;

      // Safe Attack Spacing
      // Min safe distance = 35% of maxRange or 160px
      // Ideal distance = 50% to 72% of maxRange
      const minSafeDist = Math.max(160, maxRange * 0.35);
      const maxIdealDist = maxRange * 0.72;

      if (dist < minSafeDist) {
        // TOO CLOSE! Back away from enemy while maintaining aim
        const backAngle = angleToEnemy + Math.PI;
        combatX = Math.cos(backAngle) * 1.3;
        combatY = Math.sin(backAngle) * 1.3;
      } else if (dist > maxIdealDist) {
        // TOO FAR! Close in to optimal range
        combatX = Math.cos(angleToEnemy);
        combatY = Math.sin(angleToEnemy);
      } else {
        // OPTIMAL DISTANCE: Orbit/strafe around enemy
        const strafeDirection = (bot.id.charCodeAt(0) % 2 === 0 ? 1 : -1);
        const strafeAngle = angleToEnemy + (Math.PI / 2) * strafeDirection;
        combatX = Math.cos(strafeAngle) * 0.85;
        combatY = Math.sin(strafeAngle) * 0.85;
      }

      // Attack Decision
      if (dist <= maxRange && bot.lightAttackCooldown <= 0) {
        // Attack probability based on difficulty
        const attackChance = difficulty === 'hard' ? 0.85 : difficulty === 'medium' ? 0.60 : 0.35;
        if (Math.random() < attackChance) {
          attack = true;
        }
      }

      // Tactical dash away or around on hard/medium difficulty
      const combatDashChance = difficulty === 'hard' ? 0.03 : difficulty === 'medium' ? 0.015 : 0.003;
      if (bot.dashCooldown <= 0 && Math.random() < combatDashChance) {
        dash = true;
      }
    } else {
      // Patrol around arena center
      combatX = (Math.random() - 0.5) * 0.5;
      combatY = (Math.random() - 0.5) * 0.5;
    }
  }

  // Combine Vector Forces with Weights
  if (isNearEdge) {
    // Boundary safety overrides combat
    moveX = centerForceX + dodgeX * 0.5 + sepX * 0.5;
    moveY = centerForceY + dodgeY * 0.5 + sepY * 0.5;
  } else {
    // Normal weighted blend: Dodge + Separation + Combat
    moveX = combatX + sepX + dodgeX * 1.5;
    moveY = combatY + sepY + dodgeY * 1.5;
  }

  // Normalize or cap movement vector length
  const totalLen = Math.hypot(moveX, moveY);
  if (totalLen > 1) {
    moveX /= totalLen;
    moveY /= totalLen;
  }

  return {
    moveX,
    moveY,
    aimX,
    aimY,
    hasAimInput,
    attack,
    skill,
    dash,
    dropWeapon
  };
}

