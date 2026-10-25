var game = UnityEngine.Object.FindAnyObjectByType<WizardRoyale.Infrastructure.WizardRoyaleGame>();
game.enabled = false;
UnityEditor.EditorApplication.isPaused = false;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var simulation = (WizardRoyale.Simulation.MatchSimulation)game.GetType().GetField("simulation", flags).GetValue(game);
var presentation = (WizardRoyale.Presentation.MatchPresentation)game.GetType().GetField("presentation", flags).GetValue(game);
simulation.Reset();
simulation.Tick(5f, new Dictionary<int, WizardRoyale.Domain.PlayerCommand>());
Vector2[] positions = { new Vector2(-2.2f, 0.7f), new Vector2(2f, 0.4f), new Vector2(-1.8f, -1.1f), new Vector2(1.4f, -0.8f) };
foreach (var player in simulation.Players) { player.Position = positions[player.Id]; player.Weapon = WizardRoyale.Domain.WeaponType.Energy; }
presentation.Rebuild(simulation);
for (int i = 0; i < 90; i++) presentation.Sync(simulation, 1f / 60f);
foreach (var node in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
    if (node.name.StartsWith("Obstacle O")) node.gameObject.SetActive(false);
Camera.main.orthographicSize = 4.3f;
Camera.main.transform.position = new Vector3(0f, 0.2f, -10f);
return new { frame = Time.frameCount,
    lights = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Count(r => r.name == "Borderless Ground Illumination"),
    shader = Shader.Find("WizardRoyale/AmbientGroundLight").isSupported };
