var game = UnityEngine.Object.FindAnyObjectByType<WizardRoyale.Infrastructure.WizardRoyaleGame>();
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var simulation = (WizardRoyale.Simulation.MatchSimulation)game.GetType().GetField("simulation", flags).GetValue(game);
var presentation = (WizardRoyale.Presentation.MatchPresentation)game.GetType().GetField("presentation", flags).GetValue(game);
simulation.Players[0].Weapon = WizardRoyale.Domain.WeaponType.Orbit;
simulation.Players[0].AttackCooldown = 0f;
var commands = new Dictionary<int, WizardRoyale.Domain.PlayerCommand>();
var systems = UnityEngine.Object.FindAnyObjectByType<WizardRoyale.Presentation.SpellParticleLibrary>().GetComponentsInChildren<ParticleSystem>();
for (int i = 0; i < 26; i++)
{
    commands[0] = new WizardRoyale.Domain.PlayerCommand { Aim = Vector2.right, Attack = i == 0 };
    simulation.Tick(1f / 60f, commands);
    presentation.Sync(simulation, 1f / 60f);
    foreach (var system in systems) system.Simulate(1f / 60f, false, false, true);
}
Camera.main.orthographicSize = 4.3f;
Camera.main.transform.position = new Vector3(0f, 0.2f, -10f);
return new { particles = systems.Sum(s => s.particleCount), frame = Time.frameCount };
