import React from 'react';
import { Gamepad2, Keyboard, Swords, Zap, Flame, RotateCcw } from 'lucide-react';

interface ControlsOverlayProps {
  onClose?: () => void;
}

export const ControlsOverlay: React.FC<ControlsOverlayProps> = ({ onClose }) => {
  return (
    <div className="bg-slate-900/95 border border-slate-800 rounded-2xl p-6 text-white max-w-2xl mx-auto shadow-2xl backdrop-blur">
      <div className="flex items-center justify-between pb-4 mb-4 border-b border-slate-800">
        <div className="flex items-center space-x-3">
          <Gamepad2 className="w-6 h-6 text-amber-400" />
          <h3 className="text-xl font-black tracking-wide text-white">Controles & Mapeamento</h3>
        </div>
        {onClose && (
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-white text-sm font-semibold px-3 py-1 bg-slate-800 rounded-lg transition"
          >
            Fechar
          </button>
        )}
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 text-sm">
        {/* Keyboard Controls */}
        <div className="space-y-3 bg-slate-950/60 p-4 rounded-xl border border-slate-800/80">
          <div className="flex items-center space-x-2 text-amber-400 font-bold mb-2">
            <Keyboard className="w-4 h-4" />
            <span>Teclado</span>
          </div>

          <div className="flex items-center justify-between py-1 border-b border-slate-800/50">
            <span className="text-slate-300">Movimentação</span>
            <span className="font-mono bg-slate-800 px-2 py-0.5 rounded text-amber-300">WASD / Setas</span>
          </div>

          <div className="flex items-center justify-between py-1 border-b border-slate-800/50">
            <span className="text-slate-300">Mira / Direção</span>
            <span className="font-mono bg-slate-800 px-2 py-0.5 rounded text-amber-300">Mover Cursor do Mouse</span>
          </div>

          <div className="flex items-center justify-between py-1 border-b border-slate-800/50">
            <span className="text-slate-300 flex items-center space-x-1">
              <Swords className="w-3.5 h-3.5 text-amber-400" />
              <span>Ataque Mágico (2s Cooldown)</span>
            </span>
            <span className="font-mono bg-slate-800 px-2.5 py-0.5 rounded text-amber-300 font-bold">Clique Esquerdo / J</span>
          </div>

          <div className="flex items-center justify-between py-1 border-b border-slate-800/50">
            <span className="text-slate-300 flex items-center space-x-1">
              <Flame className="w-3.5 h-3.5 text-blue-400" />
              <span>Dash Esquiva (2s Cooldown)</span>
            </span>
            <span className="font-mono bg-slate-800 px-2.5 py-0.5 rounded text-blue-300 font-bold">Espaço / Shift</span>
          </div>

          <div className="flex items-center justify-between py-1">
            <span className="text-slate-300 flex items-center space-x-1">
              <RotateCcw className="w-3.5 h-3.5 text-red-400" />
              <span>Descartar Buff / Voltar ao Normal</span>
            </span>
            <span className="font-mono bg-slate-800 px-2.5 py-0.5 rounded text-red-300 font-bold">Q</span>
          </div>
        </div>

        {/* Gamepad / Joystick Controls */}
        <div className="space-y-3 bg-slate-950/60 p-4 rounded-xl border border-slate-800/80">
          <div className="flex items-center space-x-2 text-emerald-400 font-bold mb-2">
            <Gamepad2 className="w-4 h-4" />
            <span>Joystick / Gamepad</span>
          </div>

          <div className="flex items-center justify-between py-1 border-b border-slate-800/50">
            <span className="text-slate-300">Movimentação</span>
            <span className="font-mono bg-slate-800 px-2 py-0.5 rounded text-emerald-300">Analógico Esquerdo / D-Pad</span>
          </div>

          <div className="flex items-center justify-between py-1 border-b border-slate-800/50">
            <span className="text-slate-300">Mira de Ataque</span>
            <span className="font-mono bg-slate-800 px-2 py-0.5 rounded text-emerald-300 font-bold">Analógico Direito</span>
          </div>

          <div className="flex items-center justify-between py-1 border-b border-slate-800/50">
            <span className="text-slate-300">Ataque Leve</span>
            <span className="font-mono bg-slate-800 px-2 py-0.5 rounded text-emerald-300 font-bold">Botão X / Quadrado / R1</span>
          </div>

          <div className="flex items-center justify-between py-1 border-b border-slate-800/50">
            <span className="text-slate-300">Habilidade</span>
            <span className="font-mono bg-slate-800 px-2 py-0.5 rounded text-emerald-300 font-bold">Botão Y / Triângulo / RT</span>
          </div>

          <div className="flex items-center justify-between py-1 border-b border-slate-800/50">
            <span className="text-slate-300">Dash</span>
            <span className="font-mono bg-slate-800 px-2 py-0.5 rounded text-emerald-300 font-bold">Botão B / Círculo / A</span>
          </div>

          <div className="flex items-center justify-between py-1">
            <span className="text-slate-300">Largar Arma</span>
            <span className="font-mono bg-slate-800 px-2 py-0.5 rounded text-emerald-300 font-bold">L1 / Select</span>
          </div>
        </div>
      </div>

      <div className="mt-4 pt-3 border-t border-slate-800 text-xs text-slate-400 text-center">
        💡 <strong className="text-amber-300">Dica de Combate:</strong> Quanto mais vida o inimigo perder, maior será a força de repelimento que ele sofrerá! Use as habilidades para lançá-los fora da arena!
      </div>
    </div>
  );
};
