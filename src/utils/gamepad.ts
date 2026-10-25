import { ControllerInput } from '../types/game';

export interface GamepadInfo {
  index: number;
  id: string;
  connected: boolean;
}

class GamepadHandler {
  private deadzone = 0.2;

  public getConnectedGamepads(): GamepadInfo[] {
    if (typeof navigator === 'undefined' || !navigator.getGamepads) return [];
    const gamepads = navigator.getGamepads();
    const result: GamepadInfo[] = [];

    for (let i = 0; i < gamepads.length; i++) {
      const gp = gamepads[i];
      if (gp && gp.connected) {
        result.push({
          index: gp.index,
          id: gp.id,
          connected: true,
        });
      }
    }
    return result;
  }

  public getInput(gamepadIndex: number = 0): ControllerInput | null {
    if (typeof navigator === 'undefined' || !navigator.getGamepads) return null;
    const gamepads = navigator.getGamepads();
    const gp = gamepads[gamepadIndex];
    if (!gp || !gp.connected) return null;

    // Movement Axis (Stick 1)
    let moveX = gp.axes[0] || 0;
    let moveY = gp.axes[1] || 0;

    // Apply deadzone
    if (Math.abs(moveX) < this.deadzone) moveX = 0;
    if (Math.abs(moveY) < this.deadzone) moveY = 0;

    // D-Pad override if active
    // Standard gamepad D-pad buttons: 12 (Up), 13 (Down), 14 (Left), 15 (Right)
    if (gp.buttons[12]?.pressed) moveY = -1;
    if (gp.buttons[13]?.pressed) moveY = 1;
    if (gp.buttons[14]?.pressed) moveX = -1;
    if (gp.buttons[15]?.pressed) moveX = 1;

    // Normalize diagonal movement
    const len = Math.sqrt(moveX * moveX + moveY * moveY);
    if (len > 1) {
      moveX /= len;
      moveY /= len;
    }

    // Aim Axis (Stick 2 - Right Analog Stick)
    let aimX = gp.axes[2] || 0;
    let aimY = gp.axes[3] || 0;
    let hasAimInput = false;

    const rawAimMag = Math.hypot(aimX, aimY);
    if (rawAimMag >= this.deadzone) {
      hasAimInput = true;
      // Smooth response curve for precise, non-twitchy aiming
      const normMag = Math.min(1, (rawAimMag - this.deadzone) / (1 - this.deadzone));
      const curvedMag = Math.pow(normMag, 1.2);
      aimX = (aimX / rawAimMag) * curvedMag;
      aimY = (aimY / rawAimMag) * curvedMag;
    } else {
      aimX = moveX;
      aimY = moveY;
    }

    // Buttons
    // Standard mapping:
    // Button 0 (A / Cross): Attack / Dash
    // Button 2 (X / Square) or 5 (R1) or 7 (RT): Attack
    // Button 1 (B / Circle) or 6 (LT): Dash
    // Button 4 (L1) / 8 (Select) / 9 (Start): Drop Weapon
    const attack = !!(gp.buttons[2]?.pressed || gp.buttons[0]?.pressed || gp.buttons[5]?.pressed || gp.buttons[7]?.pressed);
    const skill = !!(gp.buttons[3]?.pressed);
    const dash = !!(gp.buttons[1]?.pressed || gp.buttons[6]?.pressed);
    const dropWeapon = !!(gp.buttons[4]?.pressed || gp.buttons[8]?.pressed || gp.buttons[9]?.pressed);

    return {
      moveX,
      moveY,
      aimX,
      aimY,
      hasAimInput,
      attack,
      skill,
      dash,
      dropWeapon,
    };
  }
}

export const gamepadHandler = new GamepadHandler();
