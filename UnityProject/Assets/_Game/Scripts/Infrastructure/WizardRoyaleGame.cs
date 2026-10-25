using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WizardRoyale.Domain;
using WizardRoyale.Presentation;
using WizardRoyale.Simulation;

namespace WizardRoyale.Infrastructure
{
    public sealed class WizardRoyaleGame : MonoBehaviour
    {
        private MatchSimulation simulation;
        private MatchPresentation presentation;
        private readonly Dictionary<int, PlayerCommand> commands = new Dictionary<int, PlayerCommand>();
        private readonly Dictionary<int, BotBrain> bots = new Dictionary<int, BotBrain>();
        private PlayerCommand humanCommand;
        private Camera gameCamera;
        private GUIStyle titleStyle;
        private GUIStyle centeredStyle;
        private GUIStyle smallStyle;

        private void Awake()
        {
            Application.targetFrameRate = 120;
            Time.fixedDeltaTime = 1f / 60f;
            gameCamera = Camera.main;
            if (gameCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                gameCamera = cameraObject.AddComponent<Camera>();
            }

            simulation = new MatchSimulation();
            presentation = new MatchPresentation(gameCamera, simulation);
            bots[1] = new BotBrain(101);
            bots[2] = new BotBrain(202);
            bots[3] = new BotBrain(303);
            presentation.Rebuild(simulation);
        }

        private void Update()
        {
            PollHumanInput();
            presentation.Sync(simulation, Time.unscaledDeltaTime);

            if (simulation.Phase == MatchPhase.GameOver && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                Restart();
        }

        private void FixedUpdate()
        {
            commands.Clear();
            commands[0] = humanCommand;
            humanCommand.DashPressed = false;

            foreach (PlayerState player in simulation.Players)
            {
                if (!player.IsBot || player.IsEliminated) continue;
                commands[player.Id] = bots[player.Id].Think(simulation, player);
            }

            simulation.Tick(Time.fixedDeltaTime, commands);
        }

        private void PollHumanInput()
        {
            Vector2 move = Vector2.zero;
            Vector2 aim = humanCommand.Aim.sqrMagnitude > 0f ? humanCommand.Aim : Vector2.right;
            bool attack = false;
            bool dash = false;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                attack |= keyboard.jKey.isPressed;
                dash |= keyboard.spaceKey.wasPressedThisFrame || keyboard.leftShiftKey.wasPressedThisFrame;
            }

            if (mouse != null)
            {
                Vector3 world = gameCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                Vector2 delta = (Vector2)world - CharacterHitbox.Center(simulation.Players[0].Position);
                if (delta.sqrMagnitude > 0.01f) aim = delta.normalized;
                attack |= mouse.leftButton.isPressed;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stickMove = gamepad.leftStick.ReadValue();
                if (stickMove.sqrMagnitude > move.sqrMagnitude) move = stickMove;
                Vector2 stickAim = gamepad.rightStick.ReadValue();
                if (stickAim.sqrMagnitude > 0.12f) aim = stickAim.normalized;
                attack |= gamepad.buttonSouth.isPressed || gamepad.rightTrigger.ReadValue() > 0.4f;
                dash |= gamepad.buttonEast.wasPressedThisFrame || gamepad.leftTrigger.wasPressedThisFrame;
            }

            humanCommand.Move = Vector2.ClampMagnitude(move, 1f);
            humanCommand.Aim = aim;
            humanCommand.Attack = attack;
            humanCommand.DashPressed |= dash;
        }

        private void Restart()
        {
            simulation.Reset();
            humanCommand = default;
            presentation.Rebuild(simulation);
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawHeader();
            DrawScoreboard();
            DrawControls();
            DrawWeaponStatus();

            if (simulation.Phase == MatchPhase.Countdown)
            {
                string countdown = Mathf.CeilToInt(simulation.CountdownRemaining).ToString();
                GUI.Label(new Rect(0, Screen.height * 0.36f, Screen.width, 100), countdown, titleStyle);
            }
            else if (simulation.Phase == MatchPhase.GameOver)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 220, Screen.height * 0.5f - 90, 440, 180), string.Empty);
                GUI.Label(new Rect(Screen.width * 0.5f - 210, Screen.height * 0.5f - 65, 420, 60), simulation.ResultText, titleStyle);
                GUI.Label(new Rect(Screen.width * 0.5f - 210, Screen.height * 0.5f + 20, 420, 30), "Pressione R para revanche", centeredStyle);
            }
        }

        private void DrawHeader()
        {
            GUI.Box(new Rect(Screen.width * 0.5f - 180, 12, 360, 62), string.Empty);
            GUI.Label(new Rect(Screen.width * 0.5f - 170, 17, 340, 28), "WIZARD ROYALE", centeredStyle);
            int seconds = Mathf.CeilToInt(simulation.MatchTimeRemaining);
            GUI.Label(new Rect(Screen.width * 0.5f - 170, 43, 340, 24), string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60), centeredStyle);
        }

        private void DrawScoreboard()
        {
            float y = 14f;
            foreach (PlayerState player in simulation.Players)
            {
                Rect panel = new Rect(14, y, 258, 70);
                GUI.Box(panel, string.Empty);
                Color previous = GUI.color;
                GUI.color = player.Color;
                GUI.DrawTexture(new Rect(22, y + 10, 10, 50), Texture2D.whiteTexture);
                GUI.color = previous;
                string status = player.IsEliminated ? "ELIMINADO" : string.Format("HP {0:0}   Vidas {1}", player.Hp, player.Lives);
                GUI.Label(new Rect(42, y + 7, 205, 22), player.Name + (player.IsBot ? "  [BOT]" : string.Empty), smallStyle);
                GUI.Label(new Rect(42, y + 29, 205, 22), status + "   KOs " + player.Kos, smallStyle);
                GUI.Label(new Rect(42, y + 49, 205, 18), WeaponCatalog.Get(player.Weapon).Name, smallStyle);
                y += 76f;
            }
        }

        private void DrawControls()
        {
            GUI.Box(new Rect(Screen.width - 350, Screen.height - 116, 336, 102), string.Empty);
            GUI.Label(new Rect(Screen.width - 338, Screen.height - 108, 312, 22), "WASD / Analógico esquerdo: mover", smallStyle);
            GUI.Label(new Rect(Screen.width - 338, Screen.height - 86, 312, 22), "Mouse / Analógico direito: mirar", smallStyle);
            GUI.Label(new Rect(Screen.width - 338, Screen.height - 64, 312, 22), "Clique/J: atacar   Espaço/Shift/B: dash", smallStyle);
            GUI.Label(new Rect(Screen.width - 338, Screen.height - 42, 312, 22), "Passe por um orbe para trocar de magia", smallStyle);
        }

        private void DrawWeaponStatus()
        {
            PlayerState player = simulation.Players[0];
            WeaponDefinition weapon = WeaponCatalog.Get(player.Weapon);
            Rect panel = new Rect(Screen.width * 0.5f - 190, Screen.height - 66, 380, 52);
            GUI.Box(panel, string.Empty);
            Color previous = GUI.color;
            GUI.color = weapon.Color;
            GUI.DrawTexture(new Rect(panel.x + 12, panel.y + 10, 12, 32), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(panel.x + 34, panel.y + 5, 230, 24), "Magia: " + weapon.Name, smallStyle);
            string cooldown = player.AttackCooldown <= 0f ? "PRONTA" : player.AttackCooldown.ToString("0.0") + "s";
            GUI.Label(new Rect(panel.x + 34, panel.y + 25, 230, 22), "Recarga: " + cooldown, smallStyle);
            GUI.Label(new Rect(panel.x + 245, panel.y + 15, 124, 22), "COLETE UM ORBE", smallStyle);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            centeredStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
        }
    }

    public static class WizardRoyaleBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartGame()
        {
            if (Object.FindAnyObjectByType<WizardRoyaleGame>() != null)
                return;
            new GameObject("Wizard Royale").AddComponent<WizardRoyaleGame>();
        }
    }
}
