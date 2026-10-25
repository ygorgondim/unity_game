import React, { useState, useEffect } from 'react';
import { BotDifficulty, ArenaConfig } from '../types/game';
import { DEFAULT_ARENAS, WEAPONS, PLAYER_COLORS, PLAYER_NAMES } from '../game/constants';
import { Swords, Bot, Users, Play, Gamepad2, Sparkles, Shield, Zap, Globe, RefreshCw, Copy, Check, Link, Wifi, Crown, Volume2, VolumeX } from 'lucide-react';
import { ControlsOverlay } from './ControlsOverlay';
import { multiplayer, OnlineRoomData, OnlineRoomPlayer } from '../utils/multiplayer';

interface LobbyProps {
  isMuted: boolean;
  onToggleMute: () => void;
  onStartMatch: (
    playerName: string,
    playerColor: string,
    bots: { name: string; color: string; difficulty: BotDifficulty }[],
    stocks: number,
    timeLimit: number,
    arenaId: string,
    roomCode: string,
    isOnline?: boolean,
    onlinePlayers?: OnlineRoomPlayer[],
    isHost?: boolean
  ) => void;
}

export const Lobby: React.FC<LobbyProps> = ({ onStartMatch, isMuted, onToggleMute }) => {
  const [gameMode, setGameMode] = useState<'solo' | 'online'>('solo');
  const [playerName, setPlayerName] = useState('Guerreiro #1');
  const [playerColor, setPlayerColor] = useState(PLAYER_COLORS[0]);
  const [roomCode, setRoomCode] = useState(() => Math.floor(1000 + Math.random() * 9000).toString());
  const [selectedArena, setSelectedArena] = useState(DEFAULT_ARENAS[0].id);
  const [stocks, setStocks] = useState(1);
  const [timeLimit, setTimeLimit] = useState(180); // 3 minutes
  const [showControls, setShowControls] = useState(false);
  const [copiedLink, setCopiedLink] = useState(false);

  // Online Multiplayer state
  const [onlineRoom, setOnlineRoom] = useState<OnlineRoomData | null>(null);
  const [isHost, setIsHost] = useState(true);
  const [isConnectedWS, setIsConnectedWS] = useState(false);

  // Solo Bots state
  const [bots, setBots] = useState<{ id: string; name: string; color: string; difficulty: BotDifficulty }[]>([
    { id: 'b1', name: 'Bot Viking', color: PLAYER_COLORS[1], difficulty: 'medium' },
    { id: 'b2', name: 'Bot Mago', color: PLAYER_COLORS[2], difficulty: 'medium' },
    { id: 'b3', name: 'Bot Sombra', color: PLAYER_COLORS[3], difficulty: 'medium' }
  ]);

  // Detect invite link on load
  useEffect(() => {
    const inviteCode = multiplayer.getRoomFromUrl();
    if (inviteCode) {
      setRoomCode(inviteCode);
      setGameMode('online');
    }
  }, []);

  // Subscribe to WebSocket events when in Online Mode
  useEffect(() => {
    if (gameMode !== 'online') {
      multiplayer.disconnect();
      setIsConnectedWS(false);
      return;
    }

    multiplayer.connect(roomCode, playerName, playerColor);

    const unsubscribe = multiplayer.subscribe((event) => {
      if (event.type === 'connected') {
        setIsConnectedWS(true);
      } else if (event.type === 'disconnected') {
        setIsConnectedWS(false);
      } else if (event.type === 'joined_room') {
        setIsHost(event.isHost);
        setOnlineRoom(event.roomState);
        if (event.roomState) {
          setSelectedArena(event.roomState.arenaId || DEFAULT_ARENAS[0].id);
          setStocks(event.roomState.stocks || 1);
          setTimeLimit(event.roomState.timeLimit || 180);
        }
      } else if (event.type === 'room_state') {
        setOnlineRoom(event.roomState);
        if (event.roomState) {
          setSelectedArena(event.roomState.arenaId || DEFAULT_ARENAS[0].id);
          setStocks(event.roomState.stocks || 1);
          setTimeLimit(event.roomState.timeLimit || 180);
          const myId = multiplayer.getPlayerId();
          setIsHost(event.roomState.hostId === myId);
        }
      } else if (event.type === 'game_started') {
        // Match started by host!
        const room = event.roomState;
        onStartMatch(
          playerName,
          playerColor,
          [], // No bots in online unless room filled
          room.stocks || stocks,
          room.timeLimit || timeLimit,
          room.arenaId || selectedArena,
          roomCode,
          true,
          room.players || [],
          room.hostId === multiplayer.getPlayerId()
        );
      }
    });

    return () => {
      unsubscribe();
    };
  }, [gameMode, roomCode]);

  // Update room settings if host
  const handleArenaChange = (arenaId: string) => {
    setSelectedArena(arenaId);
    if (gameMode === 'online' && isHost) {
      multiplayer.updateRoomSettings(roomCode, arenaId, stocks, timeLimit);
    }
  };

  const handleStocksChange = (newStocks: number) => {
    setStocks(newStocks);
    if (gameMode === 'online' && isHost) {
      multiplayer.updateRoomSettings(roomCode, selectedArena, newStocks, timeLimit);
    }
  };

  const handleTimeLimitChange = (newTime: number) => {
    setTimeLimit(newTime);
    if (gameMode === 'online' && isHost) {
      multiplayer.updateRoomSettings(roomCode, selectedArena, stocks, newTime);
    }
  };

  const handleCopyInvite = () => {
    const success = multiplayer.copyInviteLink(roomCode);
    if (success) {
      setCopiedLink(true);
      setTimeout(() => setCopiedLink(false), 2500);
    }
  };

  const handleAddBot = () => {
    if (bots.length >= 3) return;
    const randomName = PLAYER_NAMES[(bots.length + 2) % PLAYER_NAMES.length];
    setBots([
      ...bots,
      {
        id: `bot_${Date.now()}`,
        name: `Bot ${randomName}`,
        color: PLAYER_COLORS[(bots.length + 1) % PLAYER_COLORS.length],
        difficulty: 'medium'
      }
    ]);
  };

  const handleRemoveBot = (id: string) => {
    setBots(bots.filter((b) => b.id !== id));
  };

  const handleUpdateBotDifficulty = (id: string, difficulty: BotDifficulty) => {
    setBots(bots.map((b) => (b.id === id ? { ...b, difficulty } : b)));
  };

  const generateNewRoomCode = () => {
    const newCode = Math.floor(1000 + Math.random() * 9000).toString();
    setRoomCode(newCode);
    if (gameMode === 'online') {
      multiplayer.connect(newCode, playerName, playerColor);
    }
  };

  const handleStartOnlineMatch = () => {
    if (isHost) {
      multiplayer.sendStartGame(roomCode);
    }
  };

  return (
    <div className="min-h-screen bg-slate-950 text-white flex flex-col items-center justify-center p-4 relative overflow-y-auto">
      {/* Toast Banner for Invite Copy */}
      {copiedLink && (
        <div className="fixed top-6 left-1/2 -translate-x-1/2 z-50 bg-emerald-500 text-slate-950 font-black px-6 py-3 rounded-2xl shadow-2xl flex items-center space-x-2 animate-bounce">
          <Check className="w-5 h-5 stroke-[3]" />
          <span>Link de Convite Copiado! Envie para seus amigos jogarem online.</span>
        </div>
      )}

      {/* Dynamic Background Glow */}
      <div className="absolute top-1/4 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-amber-500/10 rounded-full blur-[120px] pointer-events-none" />

      <div className="max-w-4xl w-full z-10 space-y-6 my-8">
        {/* Title & Banner */}
        <div className="text-center space-y-2">
          <div className="inline-flex items-center space-x-2 px-4 py-1.5 rounded-full bg-amber-500/10 border border-amber-500/30 text-amber-400 text-xs font-bold uppercase tracking-widest shadow-lg">
            <Swords className="w-4 h-4" />
            <span>Top-Down Knockout Arena PvP</span>
          </div>
          <h1 className="text-4xl md:text-5xl font-black tracking-tight text-white drop-shadow-md">
            ARENA KNOCKOUT
          </h1>
          <p className="text-slate-400 text-sm max-w-lg mx-auto">
            Derrube seus oponentes da plataforma! Jogue contra a IA ou convide seus amigos online.
          </p>
        </div>

        {/* Game Mode Selector Tabs */}
        <div className="flex bg-slate-900 p-1.5 rounded-2xl border border-slate-800 max-w-md mx-auto shadow-lg">
          <button
            onClick={() => setGameMode('solo')}
            className={`flex-1 py-3 px-4 rounded-xl font-black text-xs uppercase tracking-wider flex items-center justify-center space-x-2 transition ${
              gameMode === 'solo'
                ? 'bg-amber-500 text-slate-950 shadow-md scale-[1.02]'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Bot className="w-4 h-4" />
            <span>Modo Solo (Bots IA)</span>
          </button>
          <button
            onClick={() => setGameMode('online')}
            className={`flex-1 py-3 px-4 rounded-xl font-black text-xs uppercase tracking-wider flex items-center justify-center space-x-2 transition ${
              gameMode === 'online'
                ? 'bg-sky-500 text-slate-950 shadow-md scale-[1.02]'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Globe className="w-4 h-4" />
            <span>Online Multiplayer</span>
          </button>
        </div>

        {/* Main Lobby Card */}
        <div className="bg-slate-900/90 border border-slate-800 rounded-3xl p-6 md:p-8 shadow-2xl backdrop-blur grid grid-cols-1 md:grid-cols-2 gap-8">
          {/* Left Column: Player Setup & Room Settings */}
          <div className="space-y-6">
            <h2 className="text-lg font-black text-amber-400 flex items-center space-x-2 border-b border-slate-800 pb-3">
              <Users className="w-5 h-5" />
              <span>Configuração do Jogador & Sala</span>
            </h2>

            {/* Player Name */}
            <div>
              <label className="block text-xs font-bold text-slate-400 uppercase tracking-wider mb-2">
                Seu Nome de Guerreiro
              </label>
              <input
                type="text"
                value={playerName}
                onChange={(e) => {
                  setPlayerName(e.target.value);
                  if (gameMode === 'online') {
                    multiplayer.connect(roomCode, e.target.value, playerColor);
                  }
                }}
                maxLength={16}
                className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2.5 text-white font-bold text-sm focus:outline-none focus:border-amber-500 transition"
              />
            </div>

            {/* Color Selector */}
            <div>
              <label className="block text-xs font-bold text-slate-400 uppercase tracking-wider mb-2">
                Cor do Personagem
              </label>
              <div className="flex space-x-2">
                {PLAYER_COLORS.slice(0, 6).map((color) => (
                  <button
                    key={color}
                    onClick={() => {
                      setPlayerColor(color);
                      if (gameMode === 'online') {
                        multiplayer.connect(roomCode, playerName, color);
                      }
                    }}
                    className={`w-9 h-9 rounded-xl transition border-2 ${
                      playerColor === color ? 'border-amber-400 scale-110 shadow-lg' : 'border-transparent opacity-80'
                    }`}
                    style={{ backgroundColor: color }}
                  />
                ))}
              </div>
            </div>

            {/* Room Code & Invite Link */}
            <div className="space-y-2">
              <div className="flex items-center justify-between bg-slate-950 p-3 rounded-xl border border-slate-800">
                <div>
                  <span className="text-[10px] text-slate-400 font-bold uppercase block">Código da Sala</span>
                  <input
                    type="text"
                    value={roomCode}
                    onChange={(e) => setRoomCode(e.target.value)}
                    className="bg-transparent font-mono text-lg font-black text-amber-400 tracking-wider focus:outline-none w-28 uppercase"
                  />
                </div>
                <div className="flex items-center space-x-2">
                  <button
                    onClick={generateNewRoomCode}
                    className="p-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg transition"
                    title="Gerar novo código"
                  >
                    <RefreshCw className="w-4 h-4" />
                  </button>
                  <button
                    onClick={handleCopyInvite}
                    className="px-3 py-2 bg-sky-500/20 hover:bg-sky-500/30 text-sky-300 font-bold text-xs rounded-lg border border-sky-500/40 transition flex items-center space-x-1.5"
                    title="Copiar Link de Convite"
                  >
                    <Link className="w-4 h-4" />
                    <span>Copiar Convite</span>
                  </button>
                </div>
              </div>

              {gameMode === 'online' && (
                <div className="flex items-center space-x-2 text-[11px] font-bold px-1">
                  <Wifi className={`w-3.5 h-3.5 ${isConnectedWS ? 'text-emerald-400 animate-pulse' : 'text-amber-400'}`} />
                  <span className={isConnectedWS ? 'text-emerald-400' : 'text-amber-400'}>
                    {isConnectedWS ? `Conectado à sala online #${roomCode}` : 'Conectando ao servidor online...'}
                  </span>
                </div>
              )}
            </div>

            {/* Arena Selection */}
            <div>
              <label className="block text-xs font-bold text-slate-400 uppercase tracking-wider mb-2">
                Seleção de Arena
              </label>
              <div className="grid grid-cols-2 gap-3">
                {DEFAULT_ARENAS.map((arena) => (
                  <button
                    key={arena.id}
                    disabled={gameMode === 'online' && !isHost}
                    onClick={() => handleArenaChange(arena.id)}
                    className={`p-3 rounded-xl border text-left transition ${
                      selectedArena === arena.id
                        ? 'bg-amber-950/40 border-amber-500 text-amber-200'
                        : 'bg-slate-950 border-slate-800 text-slate-400 hover:border-slate-700'
                    } ${gameMode === 'online' && !isHost ? 'opacity-60 cursor-not-allowed' : ''}`}
                  >
                    <div className="font-bold text-xs">{arena.name}</div>
                    <div className="text-[10px] text-slate-500 uppercase font-mono mt-0.5">{arena.shape}</div>
                  </button>
                ))}
              </div>
            </div>

            {/* Lives / Stocks & Time */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-bold text-slate-400 uppercase tracking-wider mb-2">
                  Vidas (Stocks)
                </label>
                <select
                  value={stocks}
                  disabled={gameMode === 'online' && !isHost}
                  onChange={(e) => handleStocksChange(Number(e.target.value))}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-white text-xs font-bold focus:outline-none focus:border-amber-500 disabled:opacity-60"
                >
                  <option value={1}>1 Vida (Sobrevivência)</option>
                  <option value={2}>2 Vidas</option>
                  <option value={3}>3 Vidas</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-400 uppercase tracking-wider mb-2">
                  Tempo da Partida
                </label>
                <select
                  value={timeLimit}
                  disabled={gameMode === 'online' && !isHost}
                  onChange={(e) => handleTimeLimitChange(Number(e.target.value))}
                  className="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-white text-xs font-bold focus:outline-none focus:border-amber-500 disabled:opacity-60"
                >
                  <option value={120}>2 Minutos</option>
                  <option value={180}>3 Minutos</option>
                  <option value={300}>5 Minutos</option>
                </select>
              </div>
            </div>
          </div>

          {/* Right Column: Bots or Online Players List */}
          <div className="space-y-6 flex flex-col justify-between">
            {gameMode === 'solo' ? (
              /* SOLO MODE: BOTS SETUP */
              <div>
                <div className="flex items-center justify-between border-b border-slate-800 pb-3 mb-4">
                  <h2 className="text-lg font-black text-amber-400 flex items-center space-x-2">
                    <Bot className="w-5 h-5" />
                    <span>Oponentes Bots ({1 + bots.length}/4)</span>
                  </h2>

                  {bots.length < 3 && (
                    <button
                      onClick={handleAddBot}
                      className="text-xs bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 font-bold px-3 py-1.5 rounded-lg border border-amber-500/40 transition flex items-center space-x-1"
                    >
                      <span>+ Adicionar Bot</span>
                    </button>
                  )}
                </div>

                {/* Player 1 Card (You) */}
                <div className="bg-slate-950 p-3 rounded-xl border border-amber-500/50 flex items-center justify-between mb-3">
                  <div className="flex items-center space-x-3">
                    <div className="w-4 h-4 rounded-full" style={{ backgroundColor: playerColor }} />
                    <div>
                      <div className="font-bold text-sm text-amber-300">{playerName} (Você)</div>
                      <div className="text-[10px] text-slate-400">Jogador Humano</div>
                    </div>
                  </div>
                  <span className="text-xs bg-amber-950 text-amber-300 px-2 py-0.5 rounded font-bold border border-amber-700/50">
                    LÍDER
                  </span>
                </div>

                {/* Bots List */}
                <div className="space-y-2.5">
                  {bots.map((bot) => (
                    <div key={bot.id} className="bg-slate-950 p-3 rounded-xl border border-slate-800 flex items-center justify-between">
                      <div className="flex items-center space-x-3">
                        <div className="w-4 h-4 rounded-full" style={{ backgroundColor: bot.color }} />
                        <div>
                          <div className="font-bold text-sm text-slate-200">{bot.name}</div>
                          <div className="text-[10px] text-slate-400">Bot IA</div>
                        </div>
                      </div>

                      <div className="flex items-center space-x-2">
                        <select
                          value={bot.difficulty}
                          onChange={(e) => handleUpdateBotDifficulty(bot.id, e.target.value as BotDifficulty)}
                          className="bg-slate-900 border border-slate-700 text-xs text-slate-300 rounded px-2 py-1 font-semibold focus:outline-none"
                        >
                          <option value="easy">Fácil</option>
                          <option value="medium">Médio</option>
                          <option value="hard">Difícil</option>
                        </select>

                        <button
                          onClick={() => handleRemoveBot(bot.id)}
                          className="text-red-400 hover:text-red-300 text-xs font-bold px-2 py-1 bg-red-950/50 rounded border border-red-900/50"
                        >
                          X
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ) : (
              /* ONLINE MODE: REAL CONNECTED PLAYERS LIST */
              <div>
                <div className="flex items-center justify-between border-b border-slate-800 pb-3 mb-4">
                  <h2 className="text-lg font-black text-sky-400 flex items-center space-x-2">
                    <Globe className="w-5 h-5" />
                    <span>Jogadores Conectados ({onlineRoom?.players.length || 1}/4)</span>
                  </h2>

                  <button
                    onClick={handleCopyInvite}
                    className="text-xs bg-sky-500/20 hover:bg-sky-500/30 text-sky-300 font-bold px-3 py-1.5 rounded-lg border border-sky-500/40 transition flex items-center space-x-1"
                  >
                    <Link className="w-3.5 h-3.5" />
                    <span>Convidar</span>
                  </button>
                </div>

                {/* List of Connected Players */}
                <div className="space-y-2.5">
                  {onlineRoom?.players.map((p) => {
                    const isMe = p.id === multiplayer.getPlayerId();
                    return (
                      <div
                        key={p.id}
                        className={`p-3 rounded-xl border flex items-center justify-between ${
                          isMe ? 'bg-slate-950 border-sky-500/60' : 'bg-slate-950 border-slate-800'
                        }`}
                      >
                        <div className="flex items-center space-x-3">
                          <div className="w-4 h-4 rounded-full" style={{ backgroundColor: p.color }} />
                          <div>
                            <div className="font-bold text-sm text-slate-100 flex items-center space-x-1.5">
                              <span>{p.name}</span>
                              {isMe && <span className="text-sky-400 text-xs font-normal">(Você)</span>}
                            </div>
                            <div className="text-[10px] text-slate-400">Jogador Online</div>
                          </div>
                        </div>

                        {p.isHost && (
                          <span className="text-[10px] bg-amber-950 text-amber-300 px-2 py-0.5 rounded font-bold border border-amber-700/50 flex items-center space-x-1">
                            <Crown className="w-3 h-3 text-amber-400 fill-amber-400" />
                            <span>LÍDER DA SALA</span>
                          </span>
                        )}
                      </div>
                    );
                  })}

                  {/* Empty Slot Callouts */}
                  {Array.from({ length: Math.max(0, 4 - (onlineRoom?.players.length || 1)) }).map((_, i) => (
                    <div
                      key={i}
                      onClick={handleCopyInvite}
                      className="p-3 rounded-xl border border-dashed border-slate-800 bg-slate-950/40 flex items-center justify-between cursor-pointer hover:border-sky-500/40 transition"
                    >
                      <div className="flex items-center space-x-3 text-slate-500">
                        <div className="w-4 h-4 rounded-full border border-dashed border-slate-700" />
                        <span className="text-xs font-bold">Vaga Aberta • Clique para copiar convite</span>
                      </div>
                      <Link className="w-4 h-4 text-slate-500" />
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Launch Buttons */}
            <div className="space-y-3 pt-4 border-t border-slate-800">
              <button
                onClick={onToggleMute}
                className="w-full py-2.5 bg-slate-800/80 hover:bg-slate-700 text-slate-300 font-bold text-xs rounded-xl border border-slate-700 transition flex items-center justify-center space-x-2"
                title={isMuted ? 'Ativar som' : 'Desativar som'}
              >
                {isMuted ? <VolumeX className="w-4 h-4 text-red-400" /> : <Volume2 className="w-4 h-4 text-emerald-400" />}
                <span>{isMuted ? 'SOM DESATIVADO' : 'SOM ATIVADO'}</span>
              </button>
              {gameMode === 'solo' ? (
                <button
                  onClick={() =>
                    onStartMatch(
                      playerName,
                      playerColor,
                      bots.map((b) => ({ name: b.name, color: b.color, difficulty: b.difficulty })),
                      stocks,
                      timeLimit,
                      selectedArena,
                      roomCode,
                      false
                    )
                  }
                  className="w-full py-4 bg-gradient-to-r from-amber-500 via-yellow-500 to-amber-500 hover:from-amber-400 hover:to-yellow-400 text-slate-950 font-black text-lg rounded-2xl shadow-xl transition transform active:scale-95 flex items-center justify-center space-x-3"
                >
                  <Play className="w-6 h-6 fill-current" />
                  <span>INICIAR COMBATE SOLO</span>
                </button>
              ) : isHost ? (
                <button
                  onClick={handleStartOnlineMatch}
                  className="w-full py-4 bg-gradient-to-r from-sky-500 via-cyan-400 to-sky-500 hover:from-sky-400 hover:to-cyan-300 text-slate-950 font-black text-lg rounded-2xl shadow-xl transition transform active:scale-95 flex items-center justify-center space-x-3"
                >
                  <Play className="w-6 h-6 fill-current" />
                  <span>INICIAR COMBATE ONLINE ({onlineRoom?.players.length || 1} JOGADORES)</span>
                </button>
              ) : (
                <div className="w-full py-4 bg-slate-800/80 text-amber-300 font-black text-sm rounded-2xl border border-amber-500/30 text-center flex items-center justify-center space-x-2 animate-pulse">
                  <Wifi className="w-4 h-4" />
                  <span>Aguardando o Líder iniciar a partida online...</span>
                </div>
              )}

              <button
                onClick={() => setShowControls(true)}
                className="w-full py-2.5 bg-slate-800 hover:bg-slate-700 text-slate-300 font-bold text-xs rounded-xl border border-slate-700 transition flex items-center justify-center space-x-2"
              >
                <Gamepad2 className="w-4 h-4 text-amber-400" />
                <span>Ver Mapeamento de Controles e Joystick</span>
              </button>
            </div>
          </div>
        </div>

        {/* Weapons Catalog Preview */}
        <div className="bg-slate-900/90 border border-slate-800 rounded-3xl p-6 shadow-xl backdrop-blur">
          <h2 className="text-sm font-black uppercase tracking-wider text-amber-400 mb-4 flex items-center space-x-2">
            <Sparkles className="w-4 h-4" />
            <span>Armas da Arena & Habilidades Especiais</span>
          </h2>

          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-5 gap-3 text-xs">
            {Object.values(WEAPONS).map((w) => (
              <div key={w.id} className="bg-slate-950 p-3 rounded-2xl border border-slate-800 flex flex-col justify-between">
                <div>
                  <div className="flex items-center space-x-2 mb-1.5">
                    <span className="text-xl">{w.icon}</span>
                    <span className="font-bold text-slate-200">{w.name.split(' ')[0]}</span>
                  </div>
                  <p className="text-[11px] text-slate-400 leading-tight mb-2">{w.description}</p>
                </div>
                <div className="bg-slate-900 p-2 rounded-xl border border-slate-800/80 text-[10px]">
                  <span className="text-amber-300 font-bold block mb-0.5">Dano / Alcance:</span>
                  <span className="text-slate-300">{w.lightAttack.damage} Dano • {w.lightAttack.range}px</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Controls Modal */}
      {showControls && (
        <div className="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <ControlsOverlay onClose={() => setShowControls(false)} />
        </div>
      )}
    </div>
  );
};
