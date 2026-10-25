import React from 'react';
import { PlayerState } from '../types/game';
import { Trophy, Skull, Flame, RotateCcw, Home, Award } from 'lucide-react';
import { soundManager } from '../utils/audio';

interface GameOverModalProps {
  players: PlayerState[];
  winnerId: string | null;
  onPlayAgain: () => void;
  onBackToLobby: () => void;
}

export const GameOverModal: React.FC<GameOverModalProps> = ({
  players,
  winnerId,
  onPlayAgain,
  onBackToLobby
}) => {
  const winner = players.find((p) => p.id === winnerId) || players.slice().sort((a, b) => b.hp - a.hp)[0];

  React.useEffect(() => {
    soundManager.playVictory();
  }, []);

  const sortedPlayers = [...players].sort((a, b) => {
    if (a.id === winner?.id) return -1;
    if (b.id === winner?.id) return 1;
    return b.stats.damageDealt - a.stats.damageDealt;
  });

  return (
    <div className="fixed inset-0 bg-slate-950/85 backdrop-blur-md flex items-center justify-center p-4 z-50 animate-fade-in">
      <div className="bg-slate-900 border border-slate-800 rounded-3xl p-8 max-w-xl w-full text-white shadow-2xl relative overflow-hidden">
        {/* Glow backdrop */}
        <div className="absolute -top-24 -left-24 w-64 h-64 bg-amber-500/20 rounded-full blur-3xl pointer-events-none" />
        <div className="absolute -bottom-24 -right-24 w-64 h-64 bg-purple-500/20 rounded-full blur-3xl pointer-events-none" />

        {/* Winner Header */}
        <div className="text-center mb-6 relative">
          <div className="inline-flex p-4 bg-amber-500/20 rounded-full border border-amber-500/40 text-amber-400 mb-3 shadow-lg animate-bounce">
            <Trophy className="w-12 h-12" />
          </div>
          <div className="text-xs font-black uppercase tracking-widest text-amber-400">Vencedor da Arena</div>
          <h2 className="text-3xl font-black text-white mt-1" style={{ color: winner?.color || '#fbbf24' }}>
            {winner ? winner.name : 'Empate na Arena!'}
          </h2>
          <p className="text-xs text-slate-400 mt-1">O último guerreiro a permanecer na plataforma!</p>
        </div>

        {/* Scoreboard Table */}
        <div className="bg-slate-950/80 rounded-2xl border border-slate-800 p-4 mb-6 space-y-3">
          <div className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-2 flex items-center justify-between px-2">
            <span>Jogador</span>
            <div className="flex space-x-6">
              <span>Eliminações</span>
              <span>Dano Causado</span>
            </div>
          </div>

          {sortedPlayers.map((p, index) => (
            <div
              key={p.id}
              className={`flex items-center justify-between p-3 rounded-xl border transition ${
                p.id === winner?.id
                  ? 'bg-amber-950/40 border-amber-500/50 text-amber-200'
                  : 'bg-slate-900/60 border-slate-800 text-slate-300'
              }`}
            >
              <div className="flex items-center space-x-3">
                <span className="font-mono text-xs font-bold text-slate-500">#{index + 1}</span>
                <div className="w-3.5 h-3.5 rounded-full" style={{ backgroundColor: p.color }} />
                <span className="font-bold text-sm">{p.name}</span>
                {p.id === winner?.id && <Award className="w-4 h-4 text-amber-400" />}
              </div>

              <div className="flex items-center space-x-8 font-mono text-sm">
                <span className="text-red-400 font-bold flex items-center space-x-1">
                  <Skull className="w-3.5 h-3.5" />
                  <span>{p.stats.kills}</span>
                </span>
                <span className="text-amber-300 font-bold flex items-center space-x-1">
                  <Flame className="w-3.5 h-3.5" />
                  <span>{Math.round(p.stats.damageDealt)}</span>
                </span>
              </div>
            </div>
          ))}
        </div>

        {/* Action Buttons */}
        <div className="flex space-x-4">
          <button
            onClick={onPlayAgain}
            className="flex-1 py-3.5 bg-gradient-to-r from-amber-500 to-yellow-500 hover:from-amber-400 hover:to-yellow-400 text-slate-950 font-black text-sm rounded-xl shadow-lg transition flex items-center justify-center space-x-2"
          >
            <RotateCcw className="w-5 h-5" />
            <span>Jogar Novamente</span>
          </button>

          <button
            onClick={onBackToLobby}
            className="py-3.5 px-6 bg-slate-800 hover:bg-slate-700 text-white font-bold text-sm rounded-xl border border-slate-700 transition flex items-center justify-center space-x-2"
          >
            <Home className="w-5 h-5" />
            <span>Lobby</span>
          </button>
        </div>
      </div>
    </div>
  );
};
