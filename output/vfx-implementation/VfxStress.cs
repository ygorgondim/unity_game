var game = UnityEngine.Object.FindAnyObjectByType<WizardRoyale.Infrastructure.WizardRoyaleGame>();
game.enabled = false;
UnityEditor.EditorApplication.isPaused = false;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var simulation = (WizardRoyale.Simulation.MatchSimulation)game.GetType().GetField("simulation", flags).GetValue(game);
var presentation = (WizardRoyale.Presentation.MatchPresentation)game.GetType().GetField("presentation", flags).GetValue(game);
var library = UnityEngine.Object.FindAnyObjectByType<WizardRoyale.Presentation.SpellParticleLibrary>();
simulation.Reset();
simulation.Tick(5f, new Dictionary<int, WizardRoyale.Domain.PlayerCommand>());
presentation.Rebuild(simulation);
var positions = new[] { new Vector2(-2f, 0.9f), new Vector2(-0.8f, 0.9f), new Vector2(-2f, -0.9f), new Vector2(-0.8f, -0.9f) };
var commands = new Dictionary<int, WizardRoyale.Domain.PlayerCommand>();
var systems = library.GetComponentsInChildren<ParticleSystem>();
int maximum = 0;
int impacts = 0;
int objectCountBefore = library.transform.childCount;
foreach (var weapon in new[] { 1, 2, 3, 4, 5, 7, 6 })
{
    foreach (var player in simulation.Players) { player.Weapon = (WizardRoyale.Domain.WeaponType)weapon; player.AttackCooldown = 0f; }
    for (int frame = 0; frame < 180; frame++)
    {
        foreach (var player in simulation.Players)
        {
            player.Position = positions[player.Id];
            player.ExternalVelocity = Vector2.zero;
            player.Hp = 500f;
            player.HitStunTimer = 0f;
            player.IsFalling = false;
            player.IsEliminated = false;
            commands[player.Id] = new WizardRoyale.Domain.PlayerCommand { Aim = player.Id % 2 == 0 ? Vector2.right : Vector2.left, Attack = true };
        }
        simulation.Tick(1f / 60f, commands);
        presentation.Sync(simulation, 1f / 60f);
        foreach (var system in systems) system.Simulate(1f / 60f, false, false, true);
        maximum = Mathf.Max(maximum, systems.Sum(s => s.particleCount));
    }
}
impacts = (int)simulation.ImpactSequence;
int countBeforeReset = systems.Sum(s => s.particleCount);
presentation.Rebuild(simulation);
bool cleared = systems.All(s => s.particleCount == 0);
Camera.main.orthographicSize = 4f;
Camera.main.transform.position = new Vector3(-0.4f, 0.2f, -10f);
return new { simulatedSeconds = 21, maximum, impacts, countBeforeReset, cleared,
    particleObjectsBefore = objectCountBefore, particleObjectsAfter = library.transform.childCount,
    retainedImpacts = simulation.SpellImpacts.Count, hardParticleCapacity = systems.Sum(s => s.main.maxParticles) };
