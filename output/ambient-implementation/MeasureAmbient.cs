var game = UnityEngine.Object.FindAnyObjectByType<WizardRoyale.Infrastructure.WizardRoyaleGame>();
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
game.GetType().GetMethod("Restart", flags).Invoke(game, null);
var simulation = (WizardRoyale.Simulation.MatchSimulation)game.GetType().GetField("simulation", flags).GetValue(game);
simulation.Tick(5f, new Dictionary<int, WizardRoyale.Domain.PlayerCommand>());
int[] weapons = { 6, 2, 5, 4 };
foreach (var player in simulation.Players) player.Weapon = (WizardRoyale.Domain.WeaponType)weapons[player.Id];
game.enabled = true;
UnityEditor.EditorApplication.isPaused = false;
var library = UnityEngine.Object.FindAnyObjectByType<WizardRoyale.Presentation.SpellParticleLibrary>();
var samples = new List<float>();
int previousFrame = Time.frameCount;
int firstFrame = previousFrame;
int peak = 0;
int warmup = 30;
UnityEditor.EditorApplication.CallbackFunction observe = null;
observe = () =>
{
    if (game == null || !UnityEditor.EditorApplication.isPlaying)
    {
        UnityEditor.EditorApplication.update -= observe;
        return;
    }
    if (Time.frameCount == previousFrame) return;
    previousFrame = Time.frameCount;
    if (warmup-- > 0) return;
    samples.Add(Time.unscaledDeltaTime * 1000f);
    peak = Mathf.Max(peak, library.ActiveParticles);
    if (samples.Count < 180) return;
    UnityEditor.EditorApplication.update -= observe;
    samples.Sort();
    float mean = samples.Average();
    string json = string.Format(System.Globalization.CultureInfo.InvariantCulture,
        "{{\"samples\":180,\"firstFrame\":{0},\"lastFrame\":{1},\"meanMs\":{2:0.00},\"p95Ms\":{3:0.00},\"fps\":{4:0.0},\"peakParticles\":{5}}}",
        firstFrame, previousFrame, mean, samples[171], 1000f / mean, peak);
    System.IO.File.WriteAllText("C:/Users/ygora/Documents/GitHub/Game_Unity/output/ambient-implementation/performance.json", json);
};
UnityEditor.EditorApplication.update += observe;
return new { collecting = true, frame = firstFrame };

