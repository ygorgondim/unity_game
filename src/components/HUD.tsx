import React from 'react';
import { PlayerState, WeaponType } from '../types/game';
import { WEAPONS } from '../game/constants';
import { Shield, Zap, Swords, Gamepad2, Volume2, VolumeX, Flame } from 'lucide-react';

interface HUDProps {
  players: PlayerState[];
  localPlayerId: string;
  matchTimeRemaining: number;
  announcement: string | null;
  gamepadConnected: boolean;
  isMuted: boolean;
  onToggleMute: () => void;
  onDropWeapon: () => void;
}

export const HUD: React.FC<HUDProps> = ({
  players,
  localPlayerId,
  matchTimeRemaining,
  announcement,
  gamepadConnected,
  isMuted,
  onToggleMute,
  onDropWeapon
}) => {
  const localPlayer = players.find((p) => p.id === localPlayerId);
  const currentWeapon = localPlayer ? WEAPONS[localPlayer.weapon] : WEAPONS.base_mage;

  const formatTime = (secs: number) => {
    const m = Math.floor(secs / 60);
    const s = Math.floor(secs % 60);
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  };

  return (
    <div className="absolute inset-0 pointer-events-none flex flex-col justify-between p-4 z-10 select-none">
      {/* Top Bar: Time, Sound Toggle, Gamepad Status */}
      <div className="flex items-center justify-between w-full">
        {/* Gamepad & Audio */}
        <div className="flex items-center space-x-3 pointer-events-auto">
          <button
            onClick={onToggleMute}
            className="p-2 bg-slate-900/80 hover:bg-slate-800 text-white rounded-lg border border-slate-700 backdrop-blur transition flex items-center space-x-2"
            title="Alternar Som"
          >
            {isMuted ? <VolumeX className="w-5 h-5 text-red-400" /> : <Volume2 className="w-5 h-5 text-emerald-400" />}
          </button>

          <div
            className={`flex items-center space-x-2 px-3 py-1.5 rounded-lg border text-xs font-semibold backdrop-blur ${
              gamepadConnected
                ? 'bg-emerald-950/80 border-emerald-500/50 text-emerald-300'
                : 'bg-slate-900/80 border-slate-700 text-slate-400'
            }`}
          >
            <Gamepad2 className="w-4 h-4" />
            <span>{gamepadConnected ? 'Joystick Conectado' : 'Joystick Ativo (Conecte)'}</span>
          </div>
        </div>

        {/* Match Timer */}
        <div className="bg-slate-900/90 border border-slate-700 px-6 py-2 rounded-xl text-center shadow-lg backdrop-blur">
          <span className="text-xs uppercase font-bold text-slate-400 tracking-wider">Tempo Restante</span>
          <div className="text-2xl font-black text-amber-400 font-mono">{formatTime(matchTimeRemaining)}</div>
        </div>

        {/* Players Stock Status Pill */}
        <div className="flex space-x-2">
          {players.map((p) => {
            const damagePct = Math.round(((p.maxHp - p.hp) / p.maxHp) * 100);
            return (
              <div
                key={p.id}
                className={`flex items-center space-x-2 px-3 py-1.5 rounded-lg border backdrop-blur text-xs font-bold ${
                  p.isEliminated
                    ? 'bg-slate-950/80 border-red-900/50 text-slate-500 opacity-60'
                    : p.id === localPlayerId
                    ? 'bg-amber-950/80 border-amber-500/80 text-amber-200'
                    : 'bg-slate-900/80 border-slate-700 text-slate-200'
                }`}
              >
                <div className="w-3 h-3 rounded-full" style={{ backgroundColor: p.color }} />
                <span className="truncate max-w-[80px]">{p.name}</span>
                <span className="text-amber-400 font-mono">{damagePct}%</span>
                <div className="flex space-x-0.5">
                  {Array.from({ length: Math.max(0, p.stocks) }).map((_, i) => (
                    <span key={i} className="text-red-500 text-xs">
                      ❤️
                    </span>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Center Announcement Banner */}
      {announcement && (
        <div className="self-center bg-gradient-to-r from-red-600 via-amber-500 to-red-600 text-white font-black text-2xl tracking-widest uppercase px-8 py-3 rounded-2xl shadow-2xl border-2 border-yellow-300 animate-bounce">
          {announcement}
        </div>
      )}

      {/* Bottom Local Player Controls & Cooldown HUD */}
      {localPlayer && !localPlayer.isEliminated && (
        <div className="self-center w-full max-w-xl bg-slate-900/90 border border-slate-800 rounded-2xl p-4 backdrop-blur shadow-2xl flex items-center justify-between">
          {/* Current Weapon / Buff Display & Drop Button */}
          <div className="flex items-center space-x-3">
            <div
              className="w-14 h-14 rounded-xl flex items-center justify-center text-3xl shadow-inner border border-slate-700"
              style={{ backgroundColor: `${currentWeapon.color}22` }}
            >
              {currentWeapon.icon}
            </div>
            <div>
              <div className="text-xs font-bold text-slate-400 uppercase tracking-wider">Magia Atual</div>
              <div className="text-sm font-black text-white">{currentWeapon.name}</div>
              {localPlayer.weapon !== 'base_mage' && (
                <button
                  onClick={onDropWeapon}
                  className="pointer-events-auto mt-1 text-[11px] bg-red-950/80 hover:bg-red-900 text-red-300 border border-red-700 px-2.5 py-0.5 rounded font-semibold transition flex items-center space-x-1"
                >
                  <span>Largar Buff [Q]</span>
                </button>
              )}
            </div>
          </div>

          {/* Action Cooldown Meters */}
          <div className="flex items-center space-x-4 pointer-events-auto">
            {/* Light Attack (Click Esquerdo / J) */}
            <div className="flex flex-col items-center">
              <div className="relative w-12 h-12 rounded-xl bg-slate-800 border border-slate-700 flex items-center justify-center font-bold text-amber-400">
                <Swords className="w-5 h-5" />
                <span className="absolute bottom-0.5 right-1 text-[9px] text-amber-300 font-bold bg-slate-900/90 px-1 rounded">Esq</span>
                {localPlayer.lightAttackCooldown > 0 && (
                  <div
                    className="absolute inset-0 bg-slate-950/80 rounded-xl flex items-center justify-center text-xs font-mono text-white"
                    style={{
                      clipPath: `polygon(0 0, 100% 0, 100% ${
                        (localPlayer.lightAttackCooldown / currentWeapon.lightAttack.cooldown) * 100
                      }%, 0 ${(localPlayer.lightAttackCooldown / currentWeapon.lightAttack.cooldown) * 100}%)`
                    }}
                  >
                    {localPlayer.lightAttackCooldown.toFixed(1)}s
                  </div>
                )}
              </div>
              <span className="text-[10px] text-slate-400 font-semibold mt-1">Ataque (2s)</span>
            </div>

            {/* Dash (Espaço / L) */}
            <div className="flex flex-col items-center">
              <div className="relative w-12 h-12 rounded-xl bg-slate-800 border border-blue-500/50 flex items-center justify-center font-bold text-blue-400">
                <Flame className="w-5 h-5" />
                <span className="absolute bottom-0.5 right-1 text-[9px] text-blue-300 font-bold bg-slate-900/90 px-1 rounded">Espaço</span>
                {localPlayer.dashCooldown > 0 && (
                  <div
                    className="absolute inset-0 bg-slate-950/85 rounded-xl flex items-center justify-center text-xs font-mono text-white"
                    style={{
                      clipPath: `polygon(0 0, 100% 0, 100% ${(localPlayer.dashCooldown / 2.0) * 100}%, 0 ${
                        (localPlayer.dashCooldown / 2.0) * 100
                      }%)`
                    }}
                  >
                    {localPlayer.dashCooldown.toFixed(1)}s
                  </div>
                )}
              </div>
              <span className="text-[10px] text-blue-300 font-semibold mt-1">Dash (2s)</span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
