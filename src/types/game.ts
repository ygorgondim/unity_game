export type WeaponType = 'base_mage' | 'explosive' | 'slime' | 'water' | 'lightning';

export interface WeaponData {
  id: WeaponType;
  name: string;
  description: string;
  icon: string; // Emoji / Icon key
  color: string;
  lightAttack: {
    name: string;
    damage: number;
    cooldown: number; // in seconds
    range: number;
    speed: number;
    knockbackBase: number;
    description: string;
  };
}

export interface PlayerStats {
  kills: number;
  deaths: number;
  damageDealt: number;
  damageTaken: number;
}

export type BotDifficulty = 'easy' | 'medium' | 'hard';

export interface PlayerState {
  id: string;
  name: string;
  color: string;
  avatarIndex: number;
  isBot: boolean;
  botDifficulty?: BotDifficulty;
  
  // Position & Motion
  x: number;
  y: number;
  vx: number;
  vy: number;
  radius: number;
  facingAngle: number; // in radians
  
  // Health & Knockback
  hp: number; // Max 500
  maxHp: number;
  stocks: number; // Lives remaining
  isEliminated: boolean;
  isFalling: boolean; // Animation when knocked off arena boundary
  fallScale: number;
  floatTimer: number; // 3-second grace period floating outside the arena before falling
  
  // Combat State
  weapon: WeaponType;
  lightAttackCooldown: number;
  dashCooldown: number;
  
  // Active Buffs / Status
  slowTimer: number; // Water puddle or slow effect
  slowFactor: number;
  
  // Hit stun / Knockback state
  hitStunTimer: number;
  dashTimer: number;
  isDashing: boolean;
  dashVx: number;
  dashVy: number;
  
  // Scoreboard stats
  stats: PlayerStats;
}

export interface Obstacle {
  id: string;
  x: number;
  y: number;
  shape: 'circle' | 'rectangle';
  radius: number;
  width?: number;
  height?: number;
  type: 'stone_pillar' | 'rune_statue' | 'wall_block';
  color: string;
}

export interface WetPuddle {
  id: string;
  x: number;
  y: number;
  radius: number;
  timer: number;
  maxTimer: number;
}

export interface GroundWeapon {
  id: string;
  type: WeaponType;
  x: number;
  y: number;
  spawnTime: number;
  duration: number; // Lifetime in seconds before vanishing
  timer: number;
}

export type ProjectileType = 'base_bolt' | 'explosive_bolt' | 'slime_bolt' | 'water_bolt' | 'lightning_beam';

export interface ActiveProjectile {
  id: string;
  ownerId: string;
  type: ProjectileType;
  x: number;
  y: number;
  vx: number;
  vy: number;
  radius: number;
  damage: number;
  knockbackBase: number;
  life: number;
  maxLife: number;
  color: string;
  
  // Buff specific logic
  bouncesLeft?: number;
  scaleMultiplier?: number;
  isExplodingPending?: boolean;
  explodeTimer?: number;
  chargeTimer?: number;
  isLightningActive?: boolean;
  startX?: number;
  startY?: number;
  targetX?: number;
  targetY?: number;
}

export interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  size: number;
  color: string;
  alpha: number;
  life: number;
  maxLife: number;
  type: 'spark' | 'smoke' | 'wind' | 'magic' | 'slash' | 'ringout' | 'blood' | 'ring' | 'water' | 'slime' | 'fire' | 'lightning';
}

export interface AttackVisual {
  id: string;
  x: number;
  y: number;
  angle: number;
  radius: number;
  arcAngle: number;
  color: string;
  type: 'slash' | 'punch' | 'kick' | 'whirlwind' | 'cleave' | 'explosion' | 'lightning';
  life: number;
  maxLife: number;
}

export interface ArenaConfig {
  id: string;
  name: string;
  width: number;
  height: number;
  radius: number; // Circle / Rounded arena island radius
  shape: 'circle' | 'rectangle';
  rectWidth?: number;
  rectHeight?: number;
  theme: 'floating_island' | 'cyber_ring' | 'viking_colosseum';
}

export interface GameSettings {
  maxPlayers: number;
  stocks: number;
  timeLimitSeconds: number;
  weaponSpawnInterval: number; // in seconds
  arena: ArenaConfig;
}

export interface RoomState {
  roomId: string;
  hostId: string;
  isStarted: boolean;
  isGameOver: boolean;
  winnerId: string | null;
  settings: GameSettings;
  players: PlayerState[];
  groundWeapons: GroundWeapon[];
  projectiles: ActiveProjectile[];
  matchTimeRemaining: number;
  gameStartTime: number;
}

export interface ControllerInput {
  moveX: number; // -1 to 1
  moveY: number; // -1 to 1
  aimX: number;  // -1 to 1
  aimY: number;  // -1 to 1
  hasAimInput: boolean;
  attack: boolean; // Light Attack
  skill: boolean;  // Skill
  dash: boolean;   // Dash
  dropWeapon: boolean; // Throw / Drop weapon
}
