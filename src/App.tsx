import React, { useState, useEffect, useRef } from 'react';
import { PlayerState, GroundWeapon, ActiveProjectile, Particle, AttackVisual, BotDifficulty, WeaponType, Obstacle, WetPuddle } from './types/game';
import { DEFAULT_ARENAS, WEAPONS, PHYSICS, getCornerSpawns, PLAYER_COLORS, calculateTargetCamera } from './game/constants';
import { ArenaMapData } from './game/arenaMap';
import arenaMapUrl from './maps/arena_001-map.png';
import { updatePhysics, performLightAttack, performDash, dropWeapon, CombatEvent } from './game/physics';
import { computeBotInput } from './game/aiBot';
import { gamepadHandler } from './utils/gamepad';
import { soundManager } from './utils/audio';
import { multiplayer } from './utils/multiplayer';

import { Lobby } from './components/Lobby';
import { GameCanvas } from './components/GameCanvas';
import { HUD } from './components/HUD';
import { GameOverModal } from './components/GameOverModal';

export default function App() {
  const [gameState, setGameState] = useState<'lobby' | 'playing' | 'gameover'>('lobby');

  // Match Config
  const [arenaId, setArenaId] = useState(DEFAULT_ARENAS[0].id);
  const [matchTimeRemaining, setMatchTimeRemaining] = useState(180);
  const [localPlayerId, setLocalPlayerId] = useState('player_1');
  const [roomCode, setRoomCode] = useState('1000');
  const [winnerId, setWinnerId] = useState<string | null>(null);

  // Dynamic Entities
  const [players, setPlayers] = useState<PlayerState[]>([]);
  const [groundWeapons, setGroundWeapons] = useState<GroundWeapon[]>([]);
  const [projectiles, setProjectiles] = useState<ActiveProjectile[]>([]);
  const [particles, setParticles] = useState<Particle[]>([]);
  const [attackVisuals, setAttackVisuals] = useState<AttackVisual[]>([]);
  const [obstacles, setObstacles] = useState<Obstacle[]>([]);
  const [wetPuddles, setWetPuddles] = useState<WetPuddle[]>([]);

  // 5-Second Match Start Countdown
  const [countdownTimer, setCountdownTimer] = useState<number>(5.0);

  // HUD & Audio State
  const [announcement, setAnnouncement] = useState<string | null>(null);
  const [gamepadConnected, setGamepadConnected] = useState(false);
  const [isMuted, setIsMuted] = useState(false);
  const [screenShake, setScreenShake] = useState(0);

  // Refs for Game Loop
  const keysRef = useRef<Record<string, boolean>>({});
  const mouseCanvasPosRef = useRef<{ x: number; y: number }>({ x: 600, y: 400 });
  const mouseButtonsRef = useRef<{ left: boolean; right: boolean }>({ left: false, right: false });
  const gameLoopRef = useRef<number | null>(null);
  const lastTimeRef = useRef<number>(performance.now());
  const weaponSpawnTimerRef = useRef<number>(0);
  const countdownTimerRef = useRef<number>(5.0);
  const lastCountSecRef = useRef<number>(5);
  const obstaclesRef = useRef<Obstacle[]>([]);
  const arenaMapRef = useRef<ArenaMapData | undefined>(undefined);
  const wetPuddlesRef = useRef<WetPuddle[]>([]);
  const cameraRef = useRef<{ x: number; y: number; zoom: number }>({ x: 750, y: 500, zoom: 0.8 });

  // Track initial match settings for Rematch
  const matchSettingsRef = useRef<{
    playerName: string;
    playerColor: string;
    bots: { name: string; color: string; difficulty: BotDifficulty }[];
    stocks: number;
    timeLimit: number;
    arenaId: string;
    roomCode: string;
    isOnline?: boolean;
    onlinePlayers?: any[];
    isHost?: boolean;
  } | null>(null);

  const isOnlineRef = useRef<boolean>(false);
  const roomCodeRef = useRef<string>('1000');

  useEffect(() => {
    const image = new Image();
    image.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = image.naturalWidth;
      canvas.height = image.naturalHeight;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;
      ctx.drawImage(image, 0, 0);
      arenaMapRef.current = { width: canvas.width, height: canvas.height, data: ctx.getImageData(0, 0, canvas.width, canvas.height).data, worldScale: 0.8 };
    };
    image.src = arenaMapUrl;
  }, []);

  // Keyboard, Mouse & Gamepad listeners
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      keysRef.current[e.key.toLowerCase()] = true;
    };
    const handleKeyUp = (e: KeyboardEvent) => {
      keysRef.current[e.key.toLowerCase()] = false;
    };
    const handleGlobalMouseUp = (e: MouseEvent) => {
      if (e.button === 0) mouseButtonsRef.current.left = false;
      if (e.button === 2) mouseButtonsRef.current.right = false;
    };

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    window.addEventListener('mouseup', handleGlobalMouseUp);

    // Check connected gamepads
    const gpInterval = setInterval(() => {
      const gps = gamepadHandler.getConnectedGamepads();
      setGamepadConnected(gps.length > 0);
    }, 1000);

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
      window.removeEventListener('mouseup', handleGlobalMouseUp);
      clearInterval(gpInterval);
    };
  }, []);

  // Listen to incoming Multiplayer Sync Messages
  useEffect(() => {
    const unsubscribe = multiplayer.subscribe((message) => {
      if (message.type === 'player_sync' && gameState === 'playing') {
        const { playerId, playerData } = message;
        if (playerId === localPlayerId) return; // ignore self echo

        setPlayers((prev) => {
          const exists = prev.some((p) => p.id === playerId);
          if (!exists) return prev;

          return prev.map((p) => {
            if (p.id !== playerId) return p;
            return {
              ...p,
              x: playerData.x,
              y: playerData.y,
              vx: playerData.vx,
              vy: playerData.vy,
              facingAngle: playerData.facingAngle,
              hp: playerData.hp,
              stocks: playerData.stocks,
              weapon: playerData.weapon,
              floatTimer: playerData.floatTimer,
              isFalling: playerData.isFalling,
              isEliminated: playerData.isEliminated,
              isDashing: playerData.isDashing
            };
          });
        });
      } else if (message.type === 'game_action' && gameState === 'playing') {
        const { playerId, actionType, payload } = message;
        if (playerId === localPlayerId) return;

        setPlayers((prev) => {
          const next = prev.map((p) => ({ ...p }));
          const actor = next.find((p) => p.id === playerId);
          if (!actor) return prev;

          if (actionType === 'light_attack') {
            performLightAttack(actor, next, projectiles, attackVisuals, [], particles, obstaclesRef.current);
          } else if (actionType === 'dash') {
            performDash(actor);
          } else if (actionType === 'drop_weapon') {
            dropWeapon(actor, groundWeapons);
          }
          return next;
        });
      }
    });

    return () => {
      unsubscribe();
    };
  }, [gameState, localPlayerId, projectiles, attackVisuals, groundWeapons]);

  // Initialize Match
  const handleStartMatch = (
    playerName: string,
    playerColor: string,
    botsConfig: { name: string; color: string; difficulty: BotDifficulty }[],
    stocks: number,
    timeLimit: number,
    selectedArenaId: string,
    code: string,
    isOnline?: boolean,
    onlinePlayersList?: any[],
    isHost?: boolean
  ) => {
    isOnlineRef.current = !!isOnline;
    roomCodeRef.current = code;

    matchSettingsRef.current = {
      playerName,
      playerColor,
      bots: botsConfig,
      stocks,
      timeLimit,
      arenaId: selectedArenaId,
      roomCode: code,
      isOnline,
      onlinePlayers: onlinePlayersList,
      isHost
    };

    const arena = DEFAULT_ARENAS.find((a) => a.id === selectedArenaId) || DEFAULT_ARENAS[0];
    const spawns = getCornerSpawns(arena);
    const generatedObstacles: Obstacle[] = [];

    setObstacles(generatedObstacles);
    obstaclesRef.current = generatedObstacles;

    wetPuddlesRef.current = [];
    setWetPuddles([]);

    const createdPlayers: PlayerState[] = [];

    if (isOnline && onlinePlayersList && onlinePlayersList.length > 0) {
      // ONLINE MULTIPLAYER MATCH
      const myOnlineId = multiplayer.getPlayerId();
      setLocalPlayerId(myOnlineId);

      onlinePlayersList.forEach((op, idx) => {
        const spawn = spawns[idx % spawns.length];
        const isMe = op.id === myOnlineId;

        createdPlayers.push({
          id: op.id,
          name: op.name || `Guerreiro #${idx + 1}`,
          color: op.color || PLAYER_COLORS[idx % PLAYER_COLORS.length],
          avatarIndex: idx,
          isBot: false,
          x: spawn.x,
          y: spawn.y,
          vx: 0,
          vy: 0,
          radius: PHYSICS.PLAYER_RADIUS,
          facingAngle: spawn.facingAngle,
          hp: 500,
          maxHp: 500,
          stocks: stocks,
          isEliminated: false,
          isFalling: false,
          fallScale: 1,
          floatTimer: 3.0,
          weapon: 'base_mage',
          lightAttackCooldown: 0,
          dashCooldown: 0,
          slowTimer: 0,
          slowFactor: 1,
          hitStunTimer: 0,
          dashTimer: 0,
          isDashing: false,
          dashVx: 0,
          dashVy: 0,
          stats: { kills: 0, deaths: 0, damageDealt: 0, damageTaken: 0 }
        });
      });
    } else {
      // SOLO BOTS MATCH
      const humanPlayer: PlayerState = {
        id: 'player_human',
        name: playerName || 'Mago #1',
        color: playerColor,
        avatarIndex: 0,
        isBot: false,
        x: spawns[0].x,
        y: spawns[0].y,
        vx: 0,
        vy: 0,
        radius: PHYSICS.PLAYER_RADIUS,
        facingAngle: spawns[0].facingAngle,
        hp: 500,
        maxHp: 500,
        stocks: stocks,
        isEliminated: false,
        isFalling: false,
        fallScale: 1,
        floatTimer: 3.0,
        weapon: 'base_mage',
        lightAttackCooldown: 0,
        dashCooldown: 0,
        slowTimer: 0,
        slowFactor: 1,
        hitStunTimer: 0,
        dashTimer: 0,
        isDashing: false,
        dashVx: 0,
        dashVy: 0,
        stats: { kills: 0, deaths: 0, damageDealt: 0, damageTaken: 0 }
      };

      const botPlayers: PlayerState[] = botsConfig.map((b, idx) => {
        const spawn = spawns[(idx + 1) % spawns.length];
        return {
          id: `bot_${idx + 1}`,
          name: b.name,
          color: b.color,
          avatarIndex: idx + 1,
          isBot: true,
          botDifficulty: b.difficulty,
          x: spawn.x,
          y: spawn.y,
          vx: 0,
          vy: 0,
          radius: PHYSICS.PLAYER_RADIUS,
          facingAngle: spawn.facingAngle,
          hp: 500,
          maxHp: 500,
          stocks: stocks,
          isEliminated: false,
          isFalling: false,
          fallScale: 1,
          floatTimer: 3.0,
          weapon: 'base_mage',
          lightAttackCooldown: 0,
          dashCooldown: 0,
          slowTimer: 0,
          slowFactor: 1,
          hitStunTimer: 0,
          dashTimer: 0,
          isDashing: false,
          dashVx: 0,
          dashVy: 0,
          stats: { kills: 0, deaths: 0, damageDealt: 0, damageTaken: 0 }
        };
      });

      setLocalPlayerId(humanPlayer.id);
      createdPlayers.push(humanPlayer, ...botPlayers);
    }

    setPlayers(createdPlayers);
    setGroundWeapons([]);
    setProjectiles([]);
    setParticles([]);
    setAttackVisuals([]);
    setArenaId(selectedArenaId);
    setMatchTimeRemaining(timeLimit);
    setRoomCode(code);
    setWinnerId(null);
    setAnnouncement(null);

    countdownTimerRef.current = 5.0;
    setCountdownTimer(5.0);
    lastCountSecRef.current = 5;

    weaponSpawnTimerRef.current = 0;
    lastTimeRef.current = performance.now();
    setGameState('playing');
  };

  // Main Game Loop
  useEffect(() => {
    if (gameState !== 'playing') return;

    let matchTimerAcc = 0;

    const gameLoop = (currentTime: number) => {
      const dt = Math.min((currentTime - lastTimeRef.current) / 1000, 0.1);
      lastTimeRef.current = currentTime;

      // Handle 5-Second Start Countdown
      if (countdownTimerRef.current > -0.8) {
        countdownTimerRef.current -= dt;
        setCountdownTimer(countdownTimerRef.current);

        const currentSec = Math.ceil(countdownTimerRef.current);
        if (currentSec < lastCountSecRef.current) {
          if (currentSec > 0) {
            soundManager.playCountdownTick();
          } else if (currentSec === 0) {
            soundManager.playFightStart();
          }
          lastCountSecRef.current = currentSec;
        }

        if (countdownTimerRef.current > 0) {
          // Pause physics movement & match timers during countdown
          gameLoopRef.current = requestAnimationFrame(gameLoop);
          return;
        }
      }

      // Update Match Time
      matchTimerAcc += dt;
      if (matchTimerAcc >= 1.0) {
        matchTimerAcc -= 1.0;
        setMatchTimeRemaining((prev) => {
          if (prev <= 1) {
            // Time limit expired -> End match
            return 0;
          }
          return prev - 1;
        });
      }

      // Update Weapon Spawning (Starts at 10.0 seconds into the match!)
      weaponSpawnTimerRef.current += dt;
      if (weaponSpawnTimerRef.current >= PHYSICS.WEAPON_SPAWN_START) {
        // Trigger announcement when weapons first start spawning
        if (weaponSpawnTimerRef.current - dt < PHYSICS.WEAPON_SPAWN_START) {
          triggerAnnouncement('BUFFS DE MAGIA SURGINDO!');
        }

        // Spawn a new weapon drop every 8 seconds if < 3 weapons on ground
        if (Math.random() < dt / 8.0) {
          setGroundWeapons((prevGws) => {
            if (prevGws.length < 3) {
              const arena = DEFAULT_ARENAS.find((a) => a.id === arenaId) || DEFAULT_ARENAS[0];
              const types: WeaponType[] = ['explosive', 'slime', 'water', 'lightning'];
              const randomType = types[Math.floor(Math.random() * types.length)];

              let spawnX = arena.width / 2;
              let spawnY = arena.height / 2;

              if (arena.shape === 'rectangle') {
                const rw = (arena.rectWidth || 1050) * 0.75;
                const rh = (arena.rectHeight || 780) * 0.75;
                spawnX = arena.width / 2 + (Math.random() - 0.5) * rw;
                spawnY = arena.height / 2 + (Math.random() - 0.5) * rh;
              } else {
                const spawnRadius = (arena.radius || 540) * 0.70;
                const angle = Math.random() * Math.PI * 2;
                const dist = Math.random() * spawnRadius;
                spawnX = arena.width / 2 + Math.cos(angle) * dist;
                spawnY = arena.height / 2 + Math.sin(angle) * dist;
              }

              return [
                ...prevGws,
                {
                  id: Math.random().toString(),
                  type: randomType,
                  x: spawnX,
                  y: spawnY,
                  spawnTime: Date.now(),
                  duration: PHYSICS.WEAPON_LIFETIME,
                  timer: PHYSICS.WEAPON_LIFETIME
                }
              ];
            }
            return prevGws;
          });
        }
      }

      // Read Inputs & Compute Entities Movement
      setPlayers((prevPlayers) => {
        const arena = DEFAULT_ARENAS.find((a) => a.id === arenaId) || DEFAULT_ARENAS[0];
        const targetCam = calculateTargetCamera(prevPlayers, arena.width, arena.height, 1200, 800);

        // Smooth dynamic camera interpolation with cinematic delay (0.02 easing factor)
        cameraRef.current.x += (targetCam.camX - cameraRef.current.x) * 0.025;
        cameraRef.current.y += (targetCam.camY - cameraRef.current.y) * 0.025;
        cameraRef.current.zoom += (targetCam.zoom - cameraRef.current.zoom) * 0.02;

        const nextPlayers = prevPlayers.map((p) => ({ ...p }));

        for (const p of nextPlayers) {
          if (p.isEliminated || p.isFalling) continue;

          let moveX = 0;
          let moveY = 0;
          let attackTrigger = false;
          let skillTrigger = false;
          let dashTrigger = false;
          let dropWeaponTrigger = false;

          if (!p.isBot && p.id === localPlayerId) {
            // Read Keyboard Movement
            const k = keysRef.current;
            if (k['a'] || k['arrowleft']) moveX -= 1;
            if (k['d'] || k['arrowright']) moveX += 1;
            if (k['w'] || k['arrowup']) moveY -= 1;
            if (k['s'] || k['arrowdown']) moveY += 1;

            // Attack (Left Click or J), Dash (Space or Shift), Drop Weapon (Q)
            const mb = mouseButtonsRef.current;
            attackTrigger = mb.left || !!k['j'];
            dashTrigger = !!(k[' '] || k['space'] || k['l'] || k['shift']);
            dropWeaponTrigger = !!(k['q'] || k['u'] || k['g']);

            // Override with Joystick if connected
            let usedGamepadAim = false;
            const gpInput = gamepadHandler.getInput(0);
            if (gpInput) {
              if (Math.abs(gpInput.moveX) > 0.1) moveX = gpInput.moveX;
              if (Math.abs(gpInput.moveY) > 0.1) moveY = gpInput.moveY;
              if (gpInput.attack) attackTrigger = true;
              if (gpInput.dash) dashTrigger = true;
              if (gpInput.dropWeapon) dropWeaponTrigger = true;

              if (gpInput.hasAimInput) {
                const targetAngle = Math.atan2(gpInput.aimY, gpInput.aimX);
                let angleDiff = targetAngle - p.facingAngle;
                while (angleDiff < -Math.PI) angleDiff += Math.PI * 2;
                while (angleDiff > Math.PI) angleDiff -= Math.PI * 2;
                
                // Smooth interpolation (0.28 factor per tick provides crisp yet smooth rotation)
                p.facingAngle += angleDiff * 0.28;
                usedGamepadAim = true;
              }
            }

            if (!usedGamepadAim) {
              // Facing Direction Angle -> Mouse Aiming with Dynamic Camera
              const canvasW = 1200;
              const canvasH = 800;
              const { x: camX, y: camY, zoom } = cameraRef.current;

              const mouseWorldX = (mouseCanvasPosRef.current.x - canvasW / 2) / zoom + camX;
              const mouseWorldY = (mouseCanvasPosRef.current.y - canvasH / 2) / zoom + camY;

              const aimDx = mouseWorldX - p.x;
              const aimDy = mouseWorldY - p.y;
              if (aimDx !== 0 || aimDy !== 0) {
                p.facingAngle = Math.atan2(aimDy, aimDx);
              }
            }
          } else if (p.isBot) {
            // AI Bot Decisions
            const botInput = computeBotInput(p, nextPlayers, groundWeapons, arenaId, projectiles);
            moveX = botInput.moveX;
            moveY = botInput.moveY;
            attackTrigger = botInput.attack;
            dashTrigger = botInput.dash;
            dropWeaponTrigger = botInput.dropWeapon;

            if (botInput.hasAimInput && (botInput.aimX !== 0 || botInput.aimY !== 0)) {
              p.facingAngle = Math.atan2(botInput.aimY, botInput.aimX);
            } else if (moveX !== 0 || moveY !== 0) {
              p.facingAngle = Math.atan2(moveY, moveX);
            }
          }

          // Apply Movement Speed (with Slow Factor if affected by Water Puddle, and 50% boost if floating outside arena)
          const isFloatingOutside = p.floatTimer !== undefined && p.floatTimer < 3.0 && !p.isFalling;
          const floatSpeedMult = isFloatingOutside ? 1.5 : 1.0;
          const currentSpeed = PHYSICS.PLAYER_SPEED * (p.slowTimer > 0 ? p.slowFactor : 1.0) * floatSpeedMult;
          const moveLen = Math.hypot(moveX, moveY);
          if (moveLen > 0 && p.hitStunTimer <= 0) {
            p.vx = (moveX / moveLen) * currentSpeed;
            p.vy = (moveY / moveLen) * currentSpeed;
          }

          // Handle Actions Triggers
          if (dropWeaponTrigger) {
            dropWeapon(p, groundWeapons);
            if (!p.isBot && p.id === localPlayerId && isOnlineRef.current) {
              multiplayer.sendGameAction(roomCodeRef.current, 'drop_weapon', {});
            }
          }
          if (dashTrigger) {
            performDash(p);
            if (!p.isBot && p.id === localPlayerId && isOnlineRef.current) {
              multiplayer.sendGameAction(roomCodeRef.current, 'dash', {});
            }
          }
          if (attackTrigger) {
            performLightAttack(p, nextPlayers, projectiles, attackVisuals, [], particles, obstaclesRef.current);
            if (!p.isBot && p.id === localPlayerId && isOnlineRef.current) {
              multiplayer.sendGameAction(roomCodeRef.current, 'light_attack', {});
            }
          }
        }

        return nextPlayers;
      });

      // Update Physics Engine & Combat Events
      setPlayers((currentPlayers) => {
        const events = updatePhysics(
          currentPlayers,
          groundWeapons,
          projectiles,
          particles,
          attackVisuals,
          wetPuddlesRef.current,
          dt,
          arenaId,
          obstaclesRef.current
          , arenaMapRef.current
        );

        // Sync local player state over WebSocket if online
        if (isOnlineRef.current) {
          const localP = currentPlayers.find((p) => p.id === localPlayerId);
          if (localP) {
            multiplayer.sendPlayerSync(roomCodeRef.current, {
              x: localP.x,
              y: localP.y,
              vx: localP.vx,
              vy: localP.vy,
              facingAngle: localP.facingAngle,
              hp: localP.hp,
              stocks: localP.stocks,
              weapon: localP.weapon,
              floatTimer: localP.floatTimer,
              isFalling: localP.isFalling,
              isEliminated: localP.isEliminated,
              isDashing: localP.isDashing
            });
          }
        }

        setWetPuddles([...wetPuddlesRef.current]);

        // Handle Hits & Knockouts Events
        events.forEach((ev: CombatEvent) => {
          if (ev.type === 'hit') {
            setScreenShake((s) => Math.min(s + 0.4, 1.2));
          } else if (ev.type === 'knockout') {
            setScreenShake(1.5);
            triggerAnnouncement('KNOCKOUT!');
          }
        });

        // Decay Screen Shake
        setScreenShake((prev) => Math.max(0, prev - dt * 3));

        return currentPlayers;
      });

      // Check Match End Condition (1 player left OR time = 0)
      setPlayers((currentPlayers) => {
        const alivePlayers = currentPlayers.filter((p) => !p.isEliminated);
        if (alivePlayers.length <= 1 || matchTimeRemaining <= 0) {
          const winner = alivePlayers[0] || currentPlayers.slice().sort((a, b) => b.hp - a.hp)[0];
          setWinnerId(winner ? winner.id : null);
          setGameState('gameover');
        }
        return currentPlayers;
      });

      gameLoopRef.current = requestAnimationFrame(gameLoop);
    };

    gameLoopRef.current = requestAnimationFrame(gameLoop);

    return () => {
      if (gameLoopRef.current) cancelAnimationFrame(gameLoopRef.current);
    };
  }, [gameState, matchTimeRemaining, arenaId, localPlayerId]);

  const triggerAnnouncement = (text: string) => {
    setAnnouncement(text);
    setTimeout(() => {
      setAnnouncement(null);
    }, 2200);
  };

  const handleToggleMute = () => {
    const muted = soundManager.toggleMute();
    setIsMuted(muted);
  };

  const handleManualDropWeapon = () => {
    setPlayers((prev) => {
      const next = prev.map((p) => ({ ...p }));
      const localP = next.find((p) => p.id === localPlayerId);
      if (localP) {
        dropWeapon(localP, groundWeapons);
      }
      return next;
    });
  };

  const handlePlayAgain = () => {
    if (matchSettingsRef.current) {
      const s = matchSettingsRef.current;
      handleStartMatch(s.playerName, s.playerColor, s.bots, s.stocks, s.timeLimit, s.arenaId, s.roomCode);
    } else {
      setGameState('lobby');
    }
  };

  const handleCanvasMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = e.currentTarget;
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    mouseCanvasPosRef.current = {
      x: (e.clientX - rect.left) * scaleX,
      y: (e.clientY - rect.top) * scaleY
    };
  };

  const handleCanvasMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (e.button === 0) mouseButtonsRef.current.left = true;
    if (e.button === 2) mouseButtonsRef.current.right = true;
  };

  const handleCanvasMouseUp = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (e.button === 0) mouseButtonsRef.current.left = false;
    if (e.button === 2) mouseButtonsRef.current.right = false;
  };

  return (
    <div className="w-screen h-screen bg-slate-950 flex flex-col justify-center items-center overflow-hidden relative font-sans select-none">
      {gameState === 'lobby' && <Lobby onStartMatch={handleStartMatch} />}

      {gameState === 'playing' && (
        <div className="relative w-full h-full flex items-center justify-center p-2 md:p-6">
          <GameCanvas
            players={players}
            groundWeapons={groundWeapons}
            projectiles={projectiles}
            particles={particles}
            attackVisuals={attackVisuals}
            arenaId={arenaId}
            localPlayerId={localPlayerId}
            screenShake={screenShake}
            obstacles={obstacles}
            countdownTimer={countdownTimer}
            cameraRef={cameraRef}
            onMouseMove={handleCanvasMouseMove}
            onMouseDown={handleCanvasMouseDown}
            onMouseUp={handleCanvasMouseUp}
            onContextMenu={(e) => e.preventDefault()}
          />

          <HUD
            players={players}
            localPlayerId={localPlayerId}
            matchTimeRemaining={matchTimeRemaining}
            announcement={announcement}
            gamepadConnected={gamepadConnected}
            isMuted={isMuted}
            onToggleMute={handleToggleMute}
            onDropWeapon={handleManualDropWeapon}
          />
        </div>
      )}

      {gameState === 'gameover' && (
        <GameOverModal
          players={players}
          winnerId={winnerId}
          onPlayAgain={handlePlayAgain}
          onBackToLobby={() => setGameState('lobby')}
        />
      )}
    </div>
  );
}
