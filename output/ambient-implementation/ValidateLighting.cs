var game = UnityEngine.Object.FindAnyObjectByType<WizardRoyale.Infrastructure.WizardRoyaleGame>();
game.enabled = false;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var simulation = (WizardRoyale.Simulation.MatchSimulation)game.GetType().GetField("simulation", flags).GetValue(game);
var presentation = (WizardRoyale.Presentation.MatchPresentation)game.GetType().GetField("presentation", flags).GetValue(game);
var ambience = UnityEngine.Object.FindAnyObjectByType<WizardRoyale.Presentation.ArenaAmbiencePresentation>();
var hero = simulation.Players[0];
var light = ambience.transform.Find("Diffuse Personal Light - " + hero.Name);
Vector2 original = hero.Position;
hero.Position += new Vector2(0.2f, -0.15f);
presentation.Sync(simulation, 1f / 60f);
bool follows = Vector2.Distance(light.position, hero.Position + Vector2.up * 0.025f) < 0.001f;
hero.IsFalling = true;
presentation.Sync(simulation, 1f / 60f);
bool hiddenOnFall = !light.gameObject.activeSelf;
hero.IsFalling = false;
hero.IsEliminated = true;
presentation.Sync(simulation, 1f / 60f);
bool hiddenOnElimination = !light.gameObject.activeSelf;
hero.IsEliminated = false;
hero.Position = original;
presentation.Sync(simulation, 1f / 60f);
bool restores = light.gameObject.activeSelf;
var shader = Shader.Find("WizardRoyale/AmbientGroundLight");
var nodes = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
return new { follows, hiddenOnFall, hiddenOnElimination, restores,
    activePersonalLights = nodes.Count(n => n.name.StartsWith("Diffuse Personal Light -")),
    aimRings = nodes.Count(n => n.name == "Aim Outline"),
    shaderSupported = shader.isSupported,
    shaderMessages = UnityEditor.ShaderUtil.GetShaderMessages(shader).Select(m => new { m.message, severity = m.severity.ToString() }).ToArray(),
    frame = Time.frameCount };
