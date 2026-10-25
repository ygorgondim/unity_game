export interface OnlineRoomPlayer {
  id: string;
  name: string;
  color: string;
  isHost: boolean;
  ready: boolean;
}

export interface OnlineRoomData {
  code: string;
  hostId: string;
  arenaId: string;
  stocks: number;
  timeLimit: number;
  players: OnlineRoomPlayer[];
  isStarted: boolean;
}

type EventCallback = (event: any) => void;

class MultiplayerManager {
  private socket: WebSocket | null = null;
  private listeners: Set<EventCallback> = new Set();
  private currentRoomCode: string | null = null;
  private playerId: string;
  public isConnected: boolean = false;

  constructor() {
    this.playerId = 'p_' + Math.random().toString(36).substring(2, 9);
  }

  public getPlayerId(): string {
    return this.playerId;
  }

  public getRoomFromUrl(): string | null {
    if (typeof window === 'undefined') return null;
    const params = new URLSearchParams(window.location.search);
    return params.get('room') || params.get('invite') || params.get('code');
  }

  public copyInviteLink(roomCode: string): boolean {
    if (typeof window === 'undefined') return false;
    const url = new URL(window.location.href);
    url.searchParams.set('room', roomCode);
    
    try {
      navigator.clipboard.writeText(url.toString());
      return true;
    } catch {
      // Fallback
      const input = document.createElement('input');
      input.value = url.toString();
      document.body.appendChild(input);
      input.select();
      document.execCommand('copy');
      document.body.removeChild(input);
      return true;
    }
  }

  public connect(roomCode: string, playerName: string, playerColor: string) {
    this.currentRoomCode = roomCode;

    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.sendJoin(roomCode, playerName, playerColor);
      return;
    }

    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const host = window.location.host;
    const wsUrl = `${protocol}//${host}`;

    this.socket = new WebSocket(wsUrl);

    this.socket.onopen = () => {
      this.isConnected = true;
      this.sendJoin(roomCode, playerName, playerColor);
      this.notifyListeners({ type: 'connected' });
    };

    this.socket.onmessage = (event) => {
      try {
        const message = JSON.parse(event.data);
        this.notifyListeners(message);
      } catch (e) {
        console.error('Failed to parse WS message:', e);
      }
    };

    this.socket.onclose = () => {
      this.isConnected = false;
      this.notifyListeners({ type: 'disconnected' });
    };

    this.socket.onerror = (err) => {
      console.error('WebSocket Error:', err);
      this.notifyListeners({ type: 'error', error: err });
    };
  }

  private sendJoin(roomCode: string, name: string, color: string) {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.socket.send(
        JSON.stringify({
          type: 'join_room',
          roomCode,
          player: {
            id: this.playerId,
            name,
            color
          }
        })
      );
    }
  }

  public updateRoomSettings(roomCode: string, arenaId: string, stocks: number, timeLimit: number) {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.socket.send(
        JSON.stringify({
          type: 'update_room_settings',
          roomCode,
          arenaId,
          stocks,
          timeLimit
        })
      );
    }
  }

  public sendStartGame(roomCode: string) {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.socket.send(
        JSON.stringify({
          type: 'start_game_request',
          roomCode
        })
      );
    }
  }

  public sendPlayerSync(roomCode: string, playerData: any) {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.socket.send(
        JSON.stringify({
          type: 'player_sync',
          roomCode,
          playerId: this.playerId,
          playerData
        })
      );
    }
  }

  public sendGameAction(roomCode: string, actionType: string, payload: any) {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.socket.send(
        JSON.stringify({
          type: 'game_action',
          roomCode,
          playerId: this.playerId,
          actionType,
          payload
        })
      );
    }
  }

  public sendRematchRequest(roomCode: string) {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      this.socket.send(
        JSON.stringify({
          type: 'rematch_request',
          roomCode
        })
      );
    }
  }

  public subscribe(callback: EventCallback) {
    this.listeners.add(callback);
    return () => {
      this.listeners.delete(callback);
    };
  }

  private notifyListeners(event: any) {
    this.listeners.forEach((cb) => cb(event));
  }

  public disconnect() {
    if (this.socket) {
      this.socket.close();
      this.socket = null;
    }
    this.isConnected = false;
  }
}

export const multiplayer = new MultiplayerManager();
