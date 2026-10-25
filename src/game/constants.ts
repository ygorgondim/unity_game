import { WeaponData, WeaponType, ArenaConfig, Obstacle, PlayerState } from '../types/game';

export const WEAPONS: Record<WeaponType, WeaponData> = {
  base_mage: {
    id: 'base_mage',
    name: 'Magia de Energia',
    description: 'Disparo mágico básico e lento. Fácil de esquivar.',
    icon: '🔮',
    color: '#8b5cf6',
    lightAttack: {
      name: 'Esfera Astral',
      damage: 15,
      cooldown: 2.0,
      range: 520,
      speed: 280,
      knockbackBase: 160,
      description: 'Dispara uma esfera mágica cadenciada (2s de recarga).'
    }
  },
  explosive: {
    id: 'explosive',
    name: 'Tiro Explosivo',
    description: 'Explode em grande área após 2 segundos ao atingir o alcance máximo.',
    icon: '💥',
    color: '#f97316',
    lightAttack: {
      name: 'Orbe de Fogo Explosivo',
      damage: 28,
      cooldown: 2.0,
      range: 480,
      speed: 260,
      knockbackBase: 260,
      description: 'Lança um projétil flamejante que para e explode após 2 segundos (2s de recarga).'
    }
  },
  slime: {
    id: 'slime',
    name: 'Tiro de Slime',
    description: 'Maior alcance e quica até 6 vezes em obstáculos e inimigos.',
    icon: '🧪',
    color: '#22c55e',
    lightAttack: {
      name: 'Gosa Quicante',
      damage: 18,
      cooldown: 2.0,
      range: 850,
      speed: 320,
      knockbackBase: 180,
      description: 'Projétil de slime rápido com alcance longo que quica 6 vezes em obstáculos e inimigos (2s de recarga).'
    }
  },
  water: {
    id: 'water',
    name: 'Tiro de Água',
    description: 'Expande 150% e cria poças de água por 5s que desaceleram inimigos.',
    icon: '🌊',
    color: '#06b6d4',
    lightAttack: {
      name: 'Bolha Gigante & Poça',
      damage: 16,
      cooldown: 2.0,
      range: 500,
      speed: 250,
      knockbackBase: 170,
      description: 'Expande em até 150% e molha o chão por 5s aplicando lentidão (2s de recarga).'
    }
  },
  lightning: {
    id: 'lightning',
    name: 'Tiro de Raio',
    description: 'Carrega por 0.9s e dispara em linha reta atingindo todos no caminho.',
    icon: '⚡',
    color: '#eab308',
    lightAttack: {
      name: 'Feixe Elétrico Instantâneo',
      damage: 30,
      cooldown: 2.0,
      range: 650,
      speed: 0,
      knockbackBase: 280,
      description: 'Trava por 0.9s e atinge instantaneamente todos na trajetória (2s de recarga).'
    }
  }
};

export const DEFAULT_ARENAS: ArenaConfig[] = [
  {
    id: 'arena_001',
    name: 'Arena 001',
    width: 1229,
    height: 819,
    rectWidth: 944,
    rectHeight: 720,
    radius: 0,
    shape: 'rectangle',
    theme: 'viking_colosseum'
  },
  {
    id: 'floating_colosseum',
    name: 'Coliseu Celestial',
    width: 1800,
    height: 1350,
    radius: 540,
    shape: 'circle',
    theme: 'floating_island'
  }
];

export function getCornerSpawns(arena: ArenaConfig): { x: number; y: number; facingAngle: number }[] {
  if (arena.id === 'arena_001') {
    return [
      { x: 264, y: 168, facingAngle: Math.PI / 4 },
      { x: 965, y: 168, facingAngle: (3 * Math.PI) / 4 },
      { x: 965, y: 648, facingAngle: -(3 * Math.PI) / 4 },
      { x: 264, y: 648, facingAngle: -Math.PI / 4 }
    ];
  }
  const cx = arena.width / 2;
  const cy = arena.height / 2;

  if (arena.shape === 'rectangle') {
    const rw = (arena.rectWidth || 1050);
    const rh = (arena.rectHeight || 780);
    const marginX = 140;
    const marginY = 140;

    return [
      { x: cx - rw / 2 + marginX, y: cy - rh / 2 + marginY, facingAngle: Math.PI / 4 },      // Top-Left -> facing down-right
      { x: cx + rw / 2 - marginX, y: cy - rh / 2 + marginY, facingAngle: (3 * Math.PI) / 4 }, // Top-Right -> facing down-left
      { x: cx + rw / 2 - marginX, y: cy + rh / 2 - marginY, facingAngle: -(3 * Math.PI) / 4 },// Bottom-Right -> facing up-left
      { x: cx - rw / 2 + marginX, y: cy + rh / 2 - marginY, facingAngle: -Math.PI / 4 }      // Bottom-Left -> facing up-right
    ];
  } else {
    // Circle Arena Corners (4 diagonal angles inside circle radius)
    const spawnDist = arena.radius * 0.68;
    return [
      { x: cx + Math.cos((5 * Math.PI) / 4) * spawnDist, y: cy + Math.sin((5 * Math.PI) / 4) * spawnDist, facingAngle: Math.PI / 4 },      // Top-Left
      { x: cx + Math.cos((7 * Math.PI) / 4) * spawnDist, y: cy + Math.sin((7 * Math.PI) / 4) * spawnDist, facingAngle: (3 * Math.PI) / 4 }, // Top-Right
      { x: cx + Math.cos(Math.PI / 4) * spawnDist,       y: cy + Math.sin(Math.PI / 4) * spawnDist,       facingAngle: -(3 * Math.PI) / 4 },// Bottom-Right
      { x: cx + Math.cos((3 * Math.PI) / 4) * spawnDist, y: cy + Math.sin((3 * Math.PI) / 4) * spawnDist, facingAngle: -Math.PI / 4 }      // Bottom-Left
    ];
  }
}

export function generateArenaObstacles(arena: ArenaConfig): Obstacle[] {
  const obstacles: Obstacle[] = [];
  const cx = arena.width / 2;
  const cy = arena.height / 2;

  const corners = getCornerSpawns(arena);

  // Helper to ensure obstacle doesn't spawn right on top of corners or center
  const isValidPos = (x: number, y: number, rad: number) => {
    // Avoid center (within 130px)
    if (Math.hypot(x - cx, y - cy) < 130) return false;
    // Avoid corners (within 180px)
    for (const c of corners) {
      if (Math.hypot(x - c.x, y - c.y) < 180) return false;
    }
    // Avoid overlap with existing obstacles
    for (const obs of obstacles) {
      if (Math.hypot(x - obs.x, y - obs.y) < rad + obs.radius + 50) return false;
    }
    return true;
  };

  const wallColors = ['#1e293b', '#334155', '#0f172a', '#18181b'];

  // 1. GENERATE RANDOM EDGE WALLS AT EXTREMITIES (specifically for rectangular/rune arena)
  if (arena.shape === 'rectangle') {
    const rw = arena.rectWidth || 1050;
    const rh = arena.rectHeight || 780;

    // Pick 2 to 3 random edges/extremities out of ['top', 'bottom', 'left', 'right']
    const allEdges = ['top', 'bottom', 'left', 'right'];
    // Shuffle edges
    for (let i = allEdges.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [allEdges[i], allEdges[j]] = [allEdges[j], allEdges[i]];
    }
    const numEdgeWalls = Math.floor(Math.random() * 2) + 2; // 2 or 3 edge walls
    const chosenEdges = allEdges.slice(0, numEdgeWalls);

    for (const edge of chosenEdges) {
      const color = wallColors[Math.floor(Math.random() * wallColors.length)];

      if (edge === 'top' || edge === 'bottom') {
        const width = Math.floor(Math.random() * 100) + 180; // 180px - 280px wide
        const height = 36; // 36px thick wall
        const rad = Math.hypot(width / 2, height / 2);

        // Position x: along middle section of top/bottom edge (offset slightly from center)
        const offsetX = (Math.random() - 0.5) * (rw * 0.35);
        const x = cx + offsetX;
        const y = edge === 'top' ? (cy - rh / 2 + height / 2) : (cy + rh / 2 - height / 2);

        if (isValidPos(x, y, rad)) {
          obstacles.push({
            id: `edge_wall_${edge}_${Date.now()}_${Math.random()}`,
            x,
            y,
            shape: 'rectangle',
            radius: rad,
            width,
            height,
            type: 'wall_block',
            color
          });
        }
      } else {
        // left or right edge
        const width = 36; // 36px thick wall
        const height = Math.floor(Math.random() * 100) + 180; // 180px - 280px tall
        const rad = Math.hypot(width / 2, height / 2);

        // Position y: along middle section of left/right edge
        const offsetY = (Math.random() - 0.5) * (rh * 0.35);
        const x = edge === 'left' ? (cx - rw / 2 + width / 2) : (cx + rw / 2 - width / 2);
        const y = cy + offsetY;

        if (isValidPos(x, y, rad)) {
          obstacles.push({
            id: `edge_wall_${edge}_${Date.now()}_${Math.random()}`,
            x,
            y,
            shape: 'rectangle',
            radius: rad,
            width,
            height,
            type: 'wall_block',
            color
          });
        }
      }
    }
  }

  // 2. GENERATE INTERIOR OBSTACLES (Pillars / Cover)
  const colors = ['#475569', '#334155', '#1e293b', '#64748b'];
  const types: ('stone_pillar' | 'rune_statue' | 'wall_block')[] = ['stone_pillar', 'wall_block', 'rune_statue'];
  const targetTotal = 7;

  let attempts = 0;
  while (obstacles.length < targetTotal && attempts < 150) {
    attempts++;
    let x = cx;
    let y = cy;
    const isRect = Math.random() < 0.4;
    const type = types[Math.floor(Math.random() * types.length)];
    const color = colors[Math.floor(Math.random() * colors.length)];

    if (arena.shape === 'circle') {
      const dist = Math.random() * (arena.radius * 0.58) + 120;
      const angle = Math.random() * Math.PI * 2;
      x = cx + Math.cos(angle) * dist;
      y = cy + Math.sin(angle) * dist;
    } else {
      const rw = (arena.rectWidth || 1050) * 0.65;
      const rh = (arena.rectHeight || 780) * 0.65;
      x = cx + (Math.random() - 0.5) * rw;
      y = cy + (Math.random() - 0.5) * rh;
    }

    if (isRect) {
      const width = Math.floor(Math.random() * 40) + 100;  // 100 - 140px
      const height = Math.floor(Math.random() * 20) + 40;  // 40 - 60px
      const rad = Math.hypot(width / 2, height / 2);
      if (isValidPos(x, y, rad)) {
        obstacles.push({
          id: `obs_${obstacles.length}_${Date.now()}`,
          x,
          y,
          shape: 'rectangle',
          radius: rad,
          width,
          height,
          type,
          color
        });
      }
    } else {
      const radius = Math.floor(Math.random() * 15) + 38; // 38 - 53px
      if (isValidPos(x, y, radius)) {
        obstacles.push({
          id: `obs_${obstacles.length}_${Date.now()}`,
          x,
          y,
          shape: 'circle',
          radius,
          type,
          color
        });
      }
    }
  }

  return obstacles;
}

export const PLAYER_COLORS = [
  '#ef4444', // Red
  '#3b82f6', // Blue
  '#10b981', // Green
  '#f59e0b', // Amber/Yellow
  '#8b5cf6', // Purple
  '#ec4899', // Pink
  '#06b6d4', // Cyan
  '#f97316'  // Orange
];

export const PLAYER_NAMES = [
  'Valquíria',
  'Guerreiro',
  'Mago Astral',
  'Sombra Ninja',
  'Thor',
  'Loki',
  'Freya',
  'Odin'
];

export const PHYSICS = {
  PLAYER_SPEED: 50,  // px per sec (reduzido para 50px/s conforme solicitado)
  FRICTION: 0.91,    // Ground friction deceleration
  DASH_SPEED: 500,   // px per sec during dash
  DASH_DURATION: 0.18, // seconds
  DASH_COOLDOWN: 2.0, // seconds
  PLAYER_RADIUS: 22,  // collision size
  WEAPON_LIFETIME: 10.0, // seconds before uncollected ground weapon despawns
  WEAPON_SPAWN_START: 10.0, // seconds after match start before weapons spawn
  
  // Knockback formula multiplier:
  // Knockback Force = Base * (1 + ((MaxHP - CurrentHP) / MaxHP) * KNOCKBACK_HP_SCALING)
  KNOCKBACK_HP_SCALING: 3.2, 
};

export function calculateTargetCamera(
  players: PlayerState[],
  arenaWidth: number,
  arenaHeight: number,
  canvasW = 1200,
  canvasH = 800
) {
  const alive = players.filter((p) => !p.isEliminated);

  let minX = arenaWidth / 2 - 250;
  let maxX = arenaWidth / 2 + 250;
  let minY = arenaHeight / 2 - 180;
  let maxY = arenaHeight / 2 + 180;

  if (alive.length > 0) {
    minX = Math.min(...alive.map((p) => p.x));
    maxX = Math.max(...alive.map((p) => p.x));
    minY = Math.min(...alive.map((p) => p.y));
    maxY = Math.max(...alive.map((p) => p.y));
  }

  // Camera center target
  let camX = (minX + maxX) / 2;
  let camY = (minY + maxY) / 2;

  // Padding around players in world units for stable zoom
  const paddingX = 260;
  const paddingY = 210;

  const spanX = (maxX - minX) + paddingX * 2;
  const spanY = (maxY - minY) + paddingY * 2;

  const scaleX = canvasW / Math.max(350, spanX);
  const scaleY = canvasH / Math.max(280, spanY);

  let zoom = Math.min(scaleX, scaleY);

  // Maximum zoom when players are close
  const maxZoom = 1.35;

  // Minimum zoom out to ensure all characters fit
  const arenaFitZoom = Math.min(canvasW / arenaWidth, canvasH / arenaHeight);
  const minZoom = Math.min(arenaFitZoom, 0.55);

  zoom = Math.max(minZoom, Math.min(maxZoom, zoom));

  // Clamp camera position so viewport stays reasonably inside arena bounds
  const halfViewW = (canvasW / 2) / zoom;
  const halfViewH = (canvasH / 2) / zoom;
  const margin = 120;

  camX = Math.max(halfViewW - margin, Math.min(arenaWidth - halfViewW + margin, camX));
  camY = Math.max(halfViewH - margin, Math.min(arenaHeight - halfViewH + margin, camY));

  return { camX, camY, zoom };
}

