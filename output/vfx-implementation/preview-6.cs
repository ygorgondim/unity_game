
// Editor eval script, outside Assets: repeatable visual QA without shipping debug controls.
var game = UnityEngine.Object.FindFirstObjectByType<WizardRoyale.Infrastructure.WizardRoyaleGame>();
UnityEditor.EditorApplication.isPaused = false;
game.enabled = false;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var simulation = (WizardRoyale.Simulation.MatchSimulation)typeof(WizardRoyale.Infrastructure.WizardRoyaleGame).GetField("simulation", flags).GetValue(game);
var presentation = (WizardRoyale.Presentation.MatchPresentation)typeof(WizardRoyale.Infrastructure.WizardRoyaleGame).GetField("presentation", flags).GetValue(game);
int preset = 6;
simulation.Reset();
simulation.Tick(5f, new Dictionary<int, WizardRoyale.Domain.PlayerCommand>());
var hero = simulation.Players[0];
hero.Position = new Vector2(-2.8f, 0.8f);
hero.Weapon = (WizardRoyale.Domain.WeaponType)preset;
presentation.Rebuild(simulation);
var commands = new Dictionary<int, WizardRoyale.Domain.PlayerCommand>();
var library = UnityEngine.Object.FindFirstObjectByType<WizardRoyale.Presentation.SpellParticleLibrary>();
var particleSystems = library.GetComponentsInChildren<ParticleSystem>();
int afterEvent = -1;
int frames = preset == 1 ? 20 : preset == 5 ? 35 : preset == 6 ? 26 : 22;
if (preset == 2 || preset == 4) frames = 200;
for (int i = 0; i < frames; i++)
{
    commands[0] = new WizardRoyale.Domain.PlayerCommand { Aim = Vector2.right, Attack = i == 0 };
    simulation.Tick(1f / 60f, commands);
    presentation.Sync(simulation, 1f / 60f);
    foreach (var system in particleSystems) system.Simulate(1f / 60f, false, false, true);
    if ((preset == 2 || preset == 4) && simulation.AreaEffects.Count > 0 && afterEvent < 0) afterEvent = i;
    if (afterEvent >= 0 && i - afterEvent >= (preset == 2 ? 3 : 6)) break;
}
Camera.main.orthographicSize = preset == 6 ? 2.5f : 3f;
Camera.main.transform.position = new Vector3(preset == 6 ? -1.3f : -0.3f, 0.8f, -10f);
var playerViews = (Dictionary<int, Transform>)typeof(WizardRoyale.Presentation.MatchPresentation).GetField("playerViews", flags).GetValue(presentation);
foreach (var pair in playerViews) if (pair.Key != 0) pair.Value.gameObject.SetActive(false);
foreach (var node in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
    if (node.name.StartsWith("Obstacle O")) node.gameObject.SetActive(false);
return new { preset, particles = particleSystems.Sum(p => p.particleCount),
    projectiles = simulation.Projectiles.Count, effects = simulation.AreaEffects.Count,
    frame = Time.frameCount, shaderSupported = library.GlowMaterial.shader.isSupported };


