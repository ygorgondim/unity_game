import React, { useRef, useEffect } from 'react';
import { PlayerState, GroundWeapon, ActiveProjectile, Particle, AttackVisual, ArenaConfig, Obstacle, WetPuddle } from '../types/game';
import { DEFAULT_ARENAS, WEAPONS, calculateTargetCamera } from '../game/constants';
import wizardSpritesheetUrl from '../sprites/wizard-spritesheet-01.png';
import arenaImageUrl from '../maps/arena_001.png';
import arenaBackgroundUrl from '../maps/arena_001-bg.png';

interface GameCanvasProps {
  players: PlayerState[];
  groundWeapons: GroundWeapon[];
  projectiles: ActiveProjectile[];
  particles: Particle[];
  attackVisuals: AttackVisual[];
  arenaId: string;
  localPlayerId: string;
  screenShake: number;
  obstacles?: Obstacle[];
  wetPuddles?: WetPuddle[];
  countdownTimer?: number;
  cameraRef?: React.RefObject<{ x: number; y: number; zoom: number }>;
  onMouseMove?: (e: React.MouseEvent<HTMLCanvasElement>) => void;
  onMouseDown?: (e: React.MouseEvent<HTMLCanvasElement>) => void;
  onMouseUp?: (e: React.MouseEvent<HTMLCanvasElement>) => void;
  onContextMenu?: (e: React.MouseEvent<HTMLCanvasElement>) => void;
}

export const GameCanvas: React.FC<GameCanvasProps> = ({
  players,
  groundWeapons,
  projectiles,
  particles,
  attackVisuals,
  arenaId,
  localPlayerId,
  screenShake,
  obstacles = [],
  wetPuddles = [],
  countdownTimer = 0,
  cameraRef,
  onMouseMove,
  onMouseDown,
  onMouseUp,
  onContextMenu
}) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const fpsLabelRef = useRef<HTMLDivElement | null>(null);
  const spriteImageRef = useRef<HTMLImageElement | null>(null);
  const spriteReadyRef = useRef(false);
  const arenaImageRef = useRef<HTMLImageElement | null>(null);
  const arenaBackgroundRef = useRef<HTMLImageElement | null>(null);
  const spriteFrameTimeRef = useRef(0);
  const spriteFrameRef = useRef(0);
  // Keep the animation loop stable. The game state changes every frame, so
  // putting these values in the effect dependencies would constantly cancel
  // and recreate requestAnimationFrame.
  const propsRef = useRef({
    players, groundWeapons, projectiles, particles, attackVisuals, arenaId,
    localPlayerId, screenShake, obstacles, wetPuddles, countdownTimer, cameraRef
  });
  propsRef.current = {
    players, groundWeapons, projectiles, particles, attackVisuals, arenaId,
    localPlayerId, screenShake, obstacles, wetPuddles, countdownTimer, cameraRef
  };

  useEffect(() => {
    const image = new Image();
    image.onload = () => {
      spriteImageRef.current = image;
      spriteReadyRef.current = true;
    };
    image.src = wizardSpritesheetUrl;
    const arenaImage = new Image();
    arenaImage.onload = () => { arenaImageRef.current = arenaImage; };
    arenaImage.src = arenaImageUrl;
    const arenaBackground = new Image();
    arenaBackground.onload = () => { arenaBackgroundRef.current = arenaBackground; };
    arenaBackground.src = arenaBackgroundUrl;

    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let animationFrameId: number;
    let fps = 0;
    let fpsFrames = 0;
    let fpsWindowStart = performance.now();

    const render = () => {
      const frameNow = performance.now();
      if (frameNow - spriteFrameTimeRef.current >= 100) {
        spriteFrameRef.current = (spriteFrameRef.current + 1) % 4;
        spriteFrameTimeRef.current = frameNow;
      }
      fpsFrames++;
      if (frameNow - fpsWindowStart >= 500) {
        fps = Math.round((fpsFrames * 1000) / (frameNow - fpsWindowStart));
        fpsFrames = 0;
        fpsWindowStart = frameNow;
        if (fpsLabelRef.current) {
          fpsLabelRef.current.textContent = `FPS: ${fps}`;
          fpsLabelRef.current.style.color = fps >= 55 ? '#4ade80' : fps >= 35 ? '#facc15' : '#f87171';
        }
      }
      const {
        players, groundWeapons, projectiles, particles, attackVisuals,
        arenaId, localPlayerId, screenShake, obstacles, wetPuddles,
        countdownTimer, cameraRef
      } = propsRef.current;
      const width = canvas.width;
      const height = canvas.height;

      const arena: ArenaConfig = DEFAULT_ARENAS.find((a) => a.id === arenaId) || DEFAULT_ARENAS[0];
      const centerX = arena.width / 2;
      const centerY = arena.height / 2;

      // Get camera position & zoom (from cameraRef or calculate dynamically)
      let camX = centerX;
      let camY = centerY;
      let zoom = 1.0;

      if (cameraRef && cameraRef.current) {
        camX = cameraRef.current.x;
        camY = cameraRef.current.y;
        zoom = cameraRef.current.zoom;
      } else {
        const targetCam = calculateTargetCamera(players, arena.width, arena.height, width, height);
        camX = targetCam.camX;
        camY = targetCam.camY;
        zoom = targetCam.zoom;
      }

      // Screen Shake offset
      const shakeX = (Math.random() - 0.5) * screenShake * 12;
      const shakeY = (Math.random() - 0.5) * screenShake * 12;

      ctx.save();
      ctx.clearRect(0, 0, width, height);
      if (arenaBackgroundRef.current) {
        ctx.drawImage(arenaBackgroundRef.current, 0, 0, width, height);
      }

      // Keep the map background static and full-screen.
      if (!arenaBackgroundRef.current) {
        ctx.fillStyle = '#020617';
        ctx.fillRect(0, 0, width, height);
      }

      // Apply World Translation and Scale based on dynamic camera
      ctx.translate(width / 2 + shakeX, height / 2 + shakeY);
      ctx.scale(zoom, zoom);
      ctx.translate(-camX, -camY);

      // --- 1. RENDER ARENA ISLAND ---
      ctx.save();
      ctx.shadowColor = 'rgba(0, 0, 0, 0.6)';
      ctx.shadowBlur = 0;
      ctx.shadowOffsetY = 25;

      if (arena.shape === 'circle') {
        ctx.beginPath();
        ctx.arc(centerX, centerY, arena.radius, 0, Math.PI * 2);
        const islandGrad = ctx.createRadialGradient(centerX, centerY, 50, centerX, centerY, arena.radius);
        islandGrad.addColorStop(0, '#334155');
        islandGrad.addColorStop(0.85, '#1e293b');
        islandGrad.addColorStop(1, '#0f172a');
        ctx.fillStyle = islandGrad;
        ctx.fill();

        ctx.lineWidth = 8;
        ctx.strokeStyle = '#38bdf8';
        ctx.shadowColor = '#38bdf8';
        ctx.shadowBlur = 0;
        ctx.stroke();
      } else {
        const rw = arena.rectWidth || 1050;
        const rh = arena.rectHeight || 780;
        ctx.beginPath();
        ctx.roundRect(centerX - rw / 2, centerY - rh / 2, rw, rh, arena.radius);
        const islandGrad = ctx.createLinearGradient(centerX - rw / 2, centerY - rh / 2, centerX + rw / 2, centerY + rh / 2);
        islandGrad.addColorStop(0, '#27272a');
        islandGrad.addColorStop(0.5, '#18181b');
        islandGrad.addColorStop(1, '#09090b');
        ctx.fillStyle = islandGrad;
        ctx.fill();

        ctx.lineWidth = 8;
        ctx.strokeStyle = '#ef4444';
        ctx.shadowColor = '#ef4444';
        ctx.shadowBlur = 0;
        ctx.stroke();
      }
      ctx.restore();

      // Arena Floor Grid Pattern
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.04)';
      ctx.lineWidth = 1;
      for (let x = 0; x < arena.width; x += 60) {
        ctx.beginPath();
        ctx.moveTo(x, 0);
        ctx.lineTo(x, arena.height);
        ctx.stroke();
      }
      for (let y = 0; y < arena.height; y += 60) {
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(arena.width, y);
        ctx.stroke();
      }

      // --- 1.2 RENDER WET PUDDLES (WATER SHOT SLOW ZONES) ---
      for (const puddle of wetPuddles) {
        ctx.save();
        ctx.translate(puddle.x, puddle.y);

        // Water puddle base
        const alpha = Math.min(1.0, puddle.timer / puddle.maxTimer) * 0.55;
        ctx.fillStyle = `rgba(14, 165, 233, ${alpha})`;
        ctx.beginPath();
        ctx.arc(0, 0, puddle.radius, 0, Math.PI * 2);
        ctx.fill();

        // Puddle outer wave ring
        const wave = Math.sin(Date.now() / 250) * 4;
        ctx.strokeStyle = `rgba(56, 189, 248, ${alpha + 0.2})`;
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(0, 0, puddle.radius + wave, 0, Math.PI * 2);
        ctx.stroke();

        ctx.font = '12px sans-serif';
        ctx.fillStyle = '#bae6fd';
        ctx.textAlign = 'center';
        ctx.fillText('💧 LENTO', 0, 4);

        ctx.restore();
      }

      // --- 1.5 RENDER OBSTACLES (WALLS / COVER) ---
      for (const obs of obstacles) {
        ctx.save();
        ctx.translate(obs.x, obs.y);

        ctx.shadowColor = 'rgba(0, 0, 0, 0.7)';
        ctx.shadowBlur = 0;
        ctx.shadowOffsetY = 8;

        if (obs.shape === 'rectangle') {
          const w = obs.width || 100;
          const h = obs.height || 50;

          ctx.beginPath();
          ctx.roundRect(-w / 2, -h / 2, w, h, 6);
          ctx.fillStyle = obs.color;
          ctx.fill();

          ctx.lineWidth = 3;
          ctx.strokeStyle = obs.type === 'wall_block' ? '#f59e0b' : '#94a3b8';
          if (obs.type === 'wall_block') {
            ctx.shadowColor = '#f59e0b';
            ctx.shadowBlur = 0;
          }
          ctx.stroke();

          ctx.beginPath();
          ctx.roundRect(-w / 2 + 4, -h / 2 + 4, w - 8, h - 8, 4);
          ctx.fillStyle = '#1e293b';
          ctx.fill();

          ctx.font = '14px sans-serif';
          ctx.fillStyle = obs.type === 'wall_block' ? '#fde047' : '#cbd5e1';
          ctx.textAlign = 'center';
          ctx.textBaseline = 'middle';
          ctx.fillText(obs.type === 'wall_block' ? 'ᚱ' : '🛡️', 0, 0);
        } else {
          ctx.beginPath();
          ctx.arc(0, 0, obs.radius, 0, Math.PI * 2);
          ctx.fillStyle = obs.color;
          ctx.fill();

          ctx.lineWidth = 4;
          ctx.strokeStyle = '#94a3b8';
          ctx.stroke();

          ctx.beginPath();
          ctx.arc(0, 0, obs.radius - 6, 0, Math.PI * 2);
          ctx.fillStyle = '#334155';
          ctx.fill();

          ctx.lineWidth = 2;
          ctx.strokeStyle = '#38bdf8';
          ctx.stroke();

          ctx.font = '18px sans-serif';
          ctx.fillStyle = '#e2e8f0';
          ctx.textAlign = 'center';
          ctx.textBaseline = 'middle';
          ctx.fillText('🏛️', 0, 0);
        }

        ctx.restore();
      }

      // The artwork replaces the old procedural arena and any generated walls.
      if (arenaImageRef.current) {
        ctx.drawImage(arenaImageRef.current, 0, 0, arena.width, arena.height);
      }

      // --- 2. RENDER GROUND WEAPON BUFF DROPS ---
      for (const gw of groundWeapons) {
        const wData = WEAPONS[gw.type];
        ctx.save();
        ctx.translate(gw.x, gw.y);

        const pulse = Math.sin(Date.now() / 200) * 4;
        ctx.beginPath();
        ctx.arc(0, 0, 24 + pulse, 0, Math.PI * 2);
        ctx.fillStyle = `${wData.color}33`;
        ctx.fill();
        ctx.lineWidth = 2;
        ctx.strokeStyle = wData.color;
        ctx.stroke();

        const remainingRatio = Math.max(0, gw.timer / gw.duration);
        ctx.beginPath();
        ctx.arc(0, 0, 28, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * remainingRatio);
        ctx.lineWidth = 3;
        ctx.strokeStyle = '#fde047';
        ctx.stroke();

        ctx.font = '22px sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(wData.icon, 0, 0);

        ctx.font = 'bold 11px sans-serif';
        ctx.fillStyle = '#f8fafc';
        ctx.shadowColor = 'rgba(0, 0, 0, 0.8)';
        ctx.shadowBlur = 0;
        ctx.fillText(wData.name, 0, 38);

        ctx.restore();
      }

      // --- 3. RENDER ATTACK VISUALS & EXPLOSIONS ---
      for (const av of attackVisuals) {
        ctx.save();
        ctx.translate(av.x, av.y);

        if (av.type === 'explosion') {
          const ratio = 1 - (av.life / av.maxLife);
          ctx.beginPath();
          ctx.arc(0, 0, av.radius * ratio, 0, Math.PI * 2);
          ctx.fillStyle = 'rgba(249, 115, 22, 0.4)';
          ctx.fill();
          ctx.lineWidth = 6;
          ctx.strokeStyle = '#ef4444';
          ctx.shadowColor = '#f97316';
          ctx.shadowBlur = 0;
          ctx.stroke();
        }
        ctx.restore();
      }

      // --- 4. RENDER PROJECTILES ---
      for (const proj of projectiles) {
        ctx.save();

        if (proj.type === 'lightning_beam') {
          const sx = proj.startX || proj.x;
          const sy = proj.startY || proj.y;
          const tx = proj.targetX || proj.x;
          const ty = proj.targetY || proj.y;

          if (proj.chargeTimer !== undefined && proj.chargeTimer > 0) {
            const maxCharge = 0.9;
            const progress = Math.min(1.0, Math.max(0.0, 1 - proj.chargeTimer / maxCharge));
            const angle = Math.atan2(ty - sy, tx - sx);
            const len = Math.hypot(tx - sx, ty - sy);
            const beamRadius = 22; // Hit zone half-width

            // --- A. TELEGRAPH DANGER AREA / HIT ZONE ---
            ctx.save();
            ctx.translate(sx, sy);
            ctx.rotate(angle);

            // 1. Translucent danger area corridor
            const pulseAlpha = 0.2 + Math.sin(Date.now() / 50) * 0.1;
            ctx.fillStyle = `rgba(250, 204, 21, ${pulseAlpha})`;
            ctx.strokeStyle = '#facc15';
            ctx.lineWidth = 2;
            ctx.shadowColor = '#eab308';
            ctx.shadowBlur = 0;

            ctx.beginPath();
            if (typeof ctx.roundRect === 'function') {
              ctx.roundRect(0, -beamRadius, Math.max(1, len), beamRadius * 2, [0, beamRadius, beamRadius, 0]);
            } else {
              ctx.rect(0, -beamRadius, Math.max(1, len), beamRadius * 2);
            }
            ctx.fill();
            ctx.stroke();

            // 2. Charging progress fill along the beam
            const fillWidth = len * progress;
            if (fillWidth > 0) {
              const grad = ctx.createLinearGradient(0, 0, len, 0);
              grad.addColorStop(0, 'rgba(234, 179, 8, 0.4)');
              grad.addColorStop(Math.min(1, progress), 'rgba(254, 240, 138, 0.85)');

              ctx.fillStyle = grad;
              ctx.beginPath();
              if (typeof ctx.roundRect === 'function') {
                ctx.roundRect(0, -beamRadius + 2, fillWidth, (beamRadius - 2) * 2, [0, beamRadius - 2, beamRadius - 2, 0]);
              } else {
                ctx.rect(0, -beamRadius + 2, fillWidth, (beamRadius - 2) * 2);
              }
              ctx.fill();
            }

            // 3. Center dotted beam trajectory line
            const flash = Math.sin(Date.now() / 30) > 0;
            ctx.strokeStyle = flash ? '#ffffff' : '#fef08a';
            ctx.lineWidth = 3;
            ctx.setLineDash([10, 6]);
            ctx.beginPath();
            ctx.moveTo(0, 0);
            ctx.lineTo(len, 0);
            ctx.stroke();
            ctx.setLineDash([]);

            // 4. Target impact reticle at end of ray (wall or max range)
            ctx.translate(len, 0);
            ctx.rotate(-angle); // Reset rotation for upright icon

            const ringPulse = Math.sin(Date.now() / 80) * 5;
            ctx.strokeStyle = '#facc15';
            ctx.lineWidth = 3;
            ctx.shadowColor = '#facc15';
            ctx.shadowBlur = 0;

            ctx.beginPath();
            ctx.arc(0, 0, beamRadius + ringPulse, 0, Math.PI * 2);
            ctx.stroke();

            ctx.font = 'bold 12px sans-serif';
            ctx.fillStyle = '#fef08a';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText('⚡', 0, 0);

            ctx.restore();

            // Caster charging circle around player
            ctx.fillStyle = 'rgba(250, 204, 21, 0.25)';
            ctx.strokeStyle = '#facc15';
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.arc(sx, sy, 36, 0, Math.PI * 2);
            ctx.fill();
            ctx.stroke();
          } else if (proj.isLightningActive) {
            // Active electric lightning beam zap
            ctx.strokeStyle = '#fef08a';
            ctx.shadowColor = '#eab308';
            ctx.shadowBlur = 0;
            ctx.lineWidth = 14;

            ctx.beginPath();
            ctx.moveTo(sx, sy);
            ctx.lineTo(tx, ty);
            ctx.stroke();

            ctx.strokeStyle = '#ffffff';
            ctx.lineWidth = 6;
            ctx.stroke();
          }
        } else if (proj.isExplodingPending) {
          // Explosive bolt stopped & ticking 2s countdown
          ctx.translate(proj.x, proj.y);
          const t = proj.explodeTimer || 0;
          const progress = 1 - t / 2.0;

          // Expanding red warning ring
          ctx.strokeStyle = '#ef4444';
          ctx.lineWidth = 3;
          ctx.shadowColor = '#dc2626';
          ctx.shadowBlur = 0;
          ctx.beginPath();
          ctx.arc(0, 0, 135 * progress, 0, Math.PI * 2);
          ctx.stroke();

          // Ticking bomb core
          const pulse = Math.sin(Date.now() / 60) * 3;
          ctx.fillStyle = '#f97316';
          ctx.beginPath();
          ctx.arc(0, 0, proj.radius + pulse, 0, Math.PI * 2);
          ctx.fill();

          ctx.font = 'black 14px sans-serif';
          ctx.fillStyle = '#ffffff';
          ctx.textAlign = 'center';
          ctx.textBaseline = 'middle';
          ctx.fillText(`${t.toFixed(1)}s`, 0, -22);
        } else {
          // Moving bolts (Base, Slime, Water, Explosive)
          ctx.translate(proj.x, proj.y);

          const r = proj.radius * (proj.scaleMultiplier || 1.0);

          ctx.fillStyle = proj.color;
          ctx.shadowColor = proj.color;
          ctx.shadowBlur = 0;

          ctx.beginPath();
          ctx.arc(0, 0, r, 0, Math.PI * 2);
          ctx.fill();

          // Render bounce counter on Slime shot
          if (proj.type === 'slime_bolt' && proj.bouncesLeft !== undefined) {
            ctx.font = 'bold 11px sans-serif';
            ctx.fillStyle = '#14532d';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(`${proj.bouncesLeft}`, 0, 0);
          }
        }

        ctx.restore();
      }

      // --- 5. RENDER PLAYERS (MAGES) ---
      for (const p of players) {
        if (p.isEliminated) continue;

        ctx.save();
        ctx.translate(p.x, p.y);

        if (p.isFalling) {
          ctx.scale(p.fallScale, p.fallScale);
        }

        // Slow status effect outline
        if (p.slowTimer > 0) {
          ctx.beginPath();
          ctx.arc(0, 0, p.radius + 7, 0, Math.PI * 2);
          ctx.strokeStyle = '#38bdf8';
          ctx.lineWidth = 4;
          ctx.stroke();
        }

        // Levitation Float Aura when outside the arena
        if (p.floatTimer !== undefined && p.floatTimer < 3.0 && !p.isFalling) {
          const hoverPulse = Math.sin(Date.now() / 80) * 4;
          ctx.beginPath();
          ctx.arc(0, 0, p.radius + 10 + hoverPulse, 0, Math.PI * 2);
          ctx.strokeStyle = '#38bdf8';
          ctx.lineWidth = 3;
          ctx.shadowColor = '#38bdf8';
          ctx.shadowBlur = 0;
          ctx.stroke();

          ctx.font = '16px sans-serif';
          ctx.fillText('🪽', 0, p.radius + 16);
        }

        // Player Ground Shadow
        ctx.beginPath();
        ctx.ellipse(0, p.radius + 2, p.radius * 0.9, p.radius * 0.3, 0, 0, Math.PI * 2);
        ctx.fillStyle = 'rgba(0, 0, 0, 0.4)';
        ctx.fill();

        // Wizard spritesheet: 4 columns x 3 rows, each frame 214x253.
        if (spriteReadyRef.current && spriteImageRef.current) {
          const moving = Math.hypot(p.vx, p.vy) > 2 || p.isDashing;
          const angle = moving ? Math.atan2(p.vy, p.vx) : (p.animationDirection ?? 0);
          const isSide = Math.abs(Math.cos(angle)) > Math.abs(Math.sin(angle));
          const row = isSide ? 2 : angle > 0 ? 0 : 1; // front, back, side
          const frame = moving ? spriteFrameRef.current : 0;
          // The source side-facing frame looks right; mirror it when moving left.
          const flipX = isSide && Math.cos(angle) < 0;
          const spriteHeight = p.radius * 2.4;
          const spriteWidth = spriteHeight * (214 / 253);

          ctx.save();
          if (flipX) ctx.scale(-1, 1);
          ctx.drawImage(
            spriteImageRef.current,
            frame * 214, row * 253, 214, 253,
            -spriteWidth / 2, -spriteHeight / 2, spriteWidth, spriteHeight
          );
          ctx.restore();
        } else {
          // Fallback while the image is loading.
          ctx.beginPath();
          ctx.arc(0, 0, p.radius, 0, Math.PI * 2);
          ctx.fillStyle = p.color;
          ctx.fill();
        }

        // Facing Direction Pointer
        ctx.beginPath();
        ctx.moveTo(0, 0);
        ctx.lineTo(Math.cos(p.facingAngle) * (p.radius + 14), Math.sin(p.facingAngle) * (p.radius + 14));
        ctx.lineWidth = 4;
        ctx.strokeStyle = p.color;
        ctx.stroke();

        // Aim Line for Local Player
        if (p.id === localPlayerId) {
          ctx.save();
          ctx.beginPath();
          ctx.setLineDash([6, 6]);
          ctx.moveTo(Math.cos(p.facingAngle) * (p.radius + 14), Math.sin(p.facingAngle) * (p.radius + 14));
          const aimLineLen = WEAPONS[p.weapon].lightAttack.range;
          ctx.lineTo(Math.cos(p.facingAngle) * aimLineLen, Math.sin(p.facingAngle) * aimLineLen);
          ctx.strokeStyle = `${p.color}99`;
          ctx.lineWidth = 2;
          ctx.stroke();

          // Reticle
          const reticleX = Math.cos(p.facingAngle) * aimLineLen;
          const reticleY = Math.sin(p.facingAngle) * aimLineLen;
          ctx.beginPath();
          ctx.arc(reticleX, reticleY, 6, 0, Math.PI * 2);
          ctx.strokeStyle = '#fde047';
          ctx.lineWidth = 2;
          ctx.stroke();
          ctx.restore();
        }

        // Weapon Display Icon
        const wData = WEAPONS[p.weapon];
        ctx.font = '16px sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(wData.icon, Math.cos(p.facingAngle) * 20, Math.sin(p.facingAngle) * 20);

        // Player Name Tag & HP Bar
        if (p.floatTimer !== undefined && p.floatTimer < 3.0 && !p.isFalling) {
          const t = Math.max(0, p.floatTimer);
          ctx.font = 'bold 11px sans-serif';
          ctx.fillStyle = '#38bdf8';
          ctx.shadowColor = '#0284c7';
          ctx.shadowBlur = 0;
          ctx.fillText(`🪽 Queda em ${t.toFixed(1)}s`, 0, -p.radius - 36);
        }

        ctx.font = 'bold 12px sans-serif';
        ctx.fillStyle = '#f8fafc';
        ctx.textAlign = 'center';
        ctx.fillText(p.name, 0, -p.radius - 22);

        // Health Bar
        const barW = 44;
        const barH = 6;
        const hpRatio = Math.max(0, p.hp / p.maxHp);
        ctx.fillStyle = '#334155';
        ctx.fillRect(-barW / 2, -p.radius - 14, barW, barH);

        const hpColor = hpRatio > 0.6 ? '#22c55e' : hpRatio > 0.3 ? '#eab308' : '#ef4444';
        ctx.fillStyle = hpColor;
        ctx.fillRect(-barW / 2, -p.radius - 14, barW * hpRatio, barH);

        // Damage % Text
        const damagePercent = Math.round(((p.maxHp - p.hp) / p.maxHp) * 100);
        ctx.font = 'bold 10px sans-serif';
        ctx.fillStyle = hpColor;
        ctx.fillText(`${damagePercent}% Dano`, 0, -p.radius - 2);

        ctx.restore();
      }

      // --- 6. RENDER PARTICLES ---
      for (const pt of particles) {
        ctx.save();
        ctx.globalAlpha = pt.alpha;
        ctx.fillStyle = pt.color;
        ctx.beginPath();
        ctx.arc(pt.x, pt.y, pt.size, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      }

      ctx.restore(); // Restore camera translation

      // --- 6.5. OFF-SCREEN PLAYER INDICATOR POINTERS ---
      for (const p of players) {
        if (p.isEliminated) continue;
        const screenX = (p.x - camX) * zoom + width / 2;
        const screenY = (p.y - camY) * zoom + height / 2;
        const margin = 28;

        if (screenX < margin || screenX > width - margin || screenY < margin || screenY > height - margin) {
          const clampedX = Math.max(margin, Math.min(width - margin, screenX));
          const clampedY = Math.max(margin, Math.min(height - margin, screenY));
          const angle = Math.atan2(screenY - height / 2, screenX - width / 2);

          ctx.save();
          ctx.translate(clampedX, clampedY);

          // Arrow pointer
          ctx.save();
          ctx.rotate(angle);
          ctx.beginPath();
          ctx.moveTo(14, 0);
          ctx.lineTo(-8, -9);
          ctx.lineTo(-8, 9);
          ctx.closePath();
          ctx.fillStyle = p.color;
          ctx.shadowColor = p.color;
          ctx.shadowBlur = 0;
          ctx.fill();
          ctx.strokeStyle = '#ffffff';
          ctx.lineWidth = 1.5;
          ctx.stroke();
          ctx.restore();

          // Player Name Badge
          ctx.font = 'bold 10px sans-serif';
          ctx.fillStyle = '#ffffff';
          ctx.textAlign = 'center';
          ctx.shadowColor = '#000000';
          ctx.shadowBlur = 0;
          ctx.fillText(p.name, 0, 16);

          ctx.restore();
        }
      }

      // --- 7. RENDER 5-SECOND COUNTDOWN OVERLAY ---
      if (countdownTimer !== undefined && countdownTimer > -0.8) {
        ctx.save();
        ctx.fillStyle = 'rgba(2, 6, 23, 0.4)';
        ctx.fillRect(0, 0, width, height);

        const countNum = Math.ceil(countdownTimer);
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';

        if (countNum > 0) {
          const progress = countdownTimer - Math.floor(countdownTimer);
          const scale = 1 + (1 - progress) * 0.25;

          ctx.font = `black ${Math.floor(110 * scale)}px system-ui, sans-serif`;
          ctx.fillStyle = '#f59e0b';
          ctx.shadowColor = '#d97706';
          ctx.shadowBlur = 0;
          ctx.fillText(`${countNum}`, width / 2, height / 2 - 20);

          ctx.font = 'bold 22px system-ui, sans-serif';
          ctx.fillStyle = '#f8fafc';
          ctx.shadowColor = 'rgba(0, 0, 0, 0.8)';
          ctx.shadowBlur = 0;
          ctx.fillText('PREPARE-SE PARA A BATALHA!', width / 2, height / 2 + 75);
        } else {
          ctx.font = 'black 100px system-ui, sans-serif';
          ctx.fillStyle = '#ef4444';
          ctx.shadowColor = '#dc2626';
          ctx.shadowBlur = 0;
          ctx.fillText('LUTA!', width / 2, height / 2 - 10);
        }

        ctx.restore();
      }

      animationFrameId = requestAnimationFrame(render);
    };

    render();

    return () => {
      cancelAnimationFrame(animationFrameId);
    };
  }, []);

  return (
    <div className="relative w-full h-full">
      <canvas
        ref={canvasRef}
        width={1200}
        height={800}
        onMouseMove={onMouseMove}
        onMouseDown={onMouseDown}
        onMouseUp={onMouseUp}
        onContextMenu={onContextMenu}
        className="w-full h-full object-contain bg-transparent rounded-xl shadow-2xl border border-slate-800 cursor-crosshair"
      />
      <div
        ref={fpsLabelRef}
        className="absolute left-5 top-5 z-20 rounded-md bg-slate-950/90 px-2.5 py-1 text-xs font-bold tracking-wide text-emerald-400 shadow-lg border border-slate-700 pointer-events-none"
      >
        FPS: --
      </div>
    </div>
  );
};
