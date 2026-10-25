using System;
using System.Collections.Generic;
using UnityEngine;
using WizardRoyale.Domain;
using Object = UnityEngine.Object;

namespace WizardRoyale.Presentation
{
    public enum SpellParticleShape { Ember, Flame, Smoke, Drop, Star, Ring, Glow, Wind }

    [Serializable]
    public sealed class SpellVisualStyle
    {
        public WeaponType Weapon;
        public Color Color;
        public Color Accent;
        [Range(0f, 120f)] public float TrailRate;
        [Range(0.01f, 0.5f)] public float ParticleSize;
        [Range(0.05f, 1.5f)] public float ParticleLife;
    }

    // Shared native particle buffers: no GameObject allocation for individual sparks.
    // Independent cosmetic RNG and bounded emission keep rendering out of simulation state.
    public sealed class SpellParticleLibrary : MonoBehaviour
    {
        [SerializeField, Range(0f, 2f)] private float particleDensity = 1f;
        [SerializeField] private SpellVisualStyle[] styles = CreateDefaultStyles();
        public static SpellVisualStyle[] CreateDefaultStyles() => new[]
        {
            Style(WeaponType.Energy, Color.white, Color.white, 48f, 0.07f, 0.24f),
            Style(WeaponType.Explosive, new Color(1f, 0.22f, 0.025f), new Color(1f, 0.76f, 0.13f), 70f, 0.18f, 0.42f),
            Style(WeaponType.Slime, new Color(0.12f, 0.85f, 0.30f), new Color(0.60f, 1f, 0.22f), 48f, 0.12f, 0.48f),
            Style(WeaponType.Water, new Color(0.025f, 0.65f, 1f), new Color(0.65f, 0.97f, 1f), 72f, 0.11f, 0.42f),
            Style(WeaponType.Lightning, new Color(1f, 0.64f, 0.035f), new Color(1f, 0.96f, 0.65f), 65f, 0.08f, 0.18f),
            Style(WeaponType.Orbit, new Color(0.48f, 0.045f, 1f), new Color(0.95f, 0.22f, 0.80f), 42f, 0.09f, 0.38f),
            Style(WeaponType.Wind, new Color(0.58f, 0.85f, 0.78f), new Color(0.90f, 1f, 0.96f), 64f, 0.23f, 0.38f),
            Style(WeaponType.Shadow, new Color(0.12f, 0.015f, 0.20f), new Color(0.55f, 0.12f, 0.95f), 48f, 0.12f, 0.40f)
        };
        private readonly List<Object> owned = new List<Object>();
        private readonly System.Random random = new System.Random(49831);
        private readonly ParticleSystem[,] systems = new ParticleSystem[8, 2];
        private readonly Sprite[] sprites = new Sprite[8];
        private readonly Material[] materials = new Material[8];
        private Material spriteGlow;
        private Material flameGlow;
        private Material areaLight;
        private Material fireCorona;
        private Material lightWave;
        private MaterialPropertyBlock fireProperties;
        private Material reflectedLight;
        private struct LocalLight
        {
            public Vector2 Position;
            public Color Color;
            public float Radius, Strength, Life, Duration;
        }
        private readonly List<LocalLight> lightPulses = new List<LocalLight>();
        private readonly List<LocalLight> frameLights = new List<LocalLight>();
        private readonly Dictionary<SpriteRenderer, SpriteRenderer> reflections = new Dictionary<SpriteRenderer, SpriteRenderer>();
        private int emittedThisFrame;
        private int waveParticleSerial;
        public int ActiveParticles { get; private set; }
        public Sprite FlameSprite => sprites[(int)SpellParticleShape.Flame];
        public Sprite StarSprite => sprites[(int)SpellParticleShape.Star];
        public Sprite GlowSprite => sprites[(int)SpellParticleShape.Glow];
        public Sprite DropSprite => sprites[(int)SpellParticleShape.Drop];
        public Sprite WindSprite => sprites[(int)SpellParticleShape.Wind];
        public Material GlowMaterial => spriteGlow;
        public Material FlameMaterial => flameGlow;
        public Material AreaMaterial => areaLight;
        public Material LightWaveMaterial => lightWave;

        private static SpellVisualStyle Style(WeaponType weapon, Color color, Color accent,
            float rate, float size, float life) => new SpellVisualStyle
            { Weapon = weapon, Color = color, Accent = accent, TrailRate = rate, ParticleSize = size, ParticleLife = life };

        public SpellVisualStyle Profile(WeaponType weapon)
        {
            foreach (SpellVisualStyle style in styles)
                if (style.Weapon == weapon) return style;
            return styles[0];
        }

        public void Initialize()
        {
            // Called from the game's Awake, on Unity's main thread. Native Unity
            // objects must not be constructed in MonoBehaviour field initializers.
            fireProperties = new MaterialPropertyBlock();
            SpellVfxSettings settings = Resources.Load<SpellVfxSettings>("Vfx/SpellVfxSettings");
            if (settings != null)
            {
                // Runtime tuning must not write back into the saved art-direction asset.
                settings = Own(Instantiate(settings));
                particleDensity = settings.ParticleDensity;
                if (settings.Styles != null && settings.Styles.Length > 0) styles = settings.Styles;
            }
            // Match the starting attack's particles and hit feedback to its reference palette.
            // Settings above are runtime copies, so this does not alter the saved asset.
            SpellVisualStyle basic = Profile(WeaponType.Energy);
            basic.Color = Color.white;
            basic.Accent = Color.white;
            Shader shader = Resources.Load<Shader>("Shaders/SpellParticle");
            if (shader == null) throw new InvalidOperationException("SpellParticle shader is missing.");
            spriteGlow = Own(new Material(shader));
            spriteGlow.name = "Spell HDR Sprite Glow";
            spriteGlow.SetFloat("_Intensity", 3.4f);
            flameGlow = Own(new Material(shader));
            flameGlow.name = "Living Flame Sprite";
            flameGlow.SetFloat("_Intensity", 2.1f);
            flameGlow.SetFloat("_FlameMotion", 1f);
            Shader areaShader = Resources.Load<Shader>("Shaders/SpellAreaLight");
            if (areaShader == null) throw new InvalidOperationException("SpellAreaLight shader is missing.");
            areaLight = Own(new Material(areaShader));
            Shader coronaShader = Resources.Load<Shader>("Shaders/FireCorona");
            if (coronaShader == null) throw new InvalidOperationException("FireCorona shader is missing.");
            fireCorona = Own(new Material(coronaShader));
            fireCorona.name = "Fire Ability - Animated Spherical Flame Ribbons";
            Shader waveShader = Resources.Load<Shader>("Shaders/WhiteLightWave");
            if (waveShader == null) throw new InvalidOperationException("WhiteLightWave shader is missing.");
            lightWave = Own(new Material(waveShader));
            lightWave.name = "Basic Attack - Animated White Light Wave";
            reflectedLight = Own(new Material(shader));
            reflectedLight.name = "Spell Colored Character Reflection";
            reflectedLight.SetFloat("_Intensity", 1.6f);
            reflectedLight.SetFloat("_TextureColor", 0f);
            for (int shape = 0; shape < 8; shape++)
            {
                sprites[shape] = CreateSprite((SpellParticleShape)shape);
                Material material = Own(new Material(shader));
                materials[shape] = material;
                material.name = "Spell " + (SpellParticleShape)shape;
                material.mainTexture = sprites[shape].texture;
                bool opaque = shape == (int)SpellParticleShape.Smoke || shape == (int)SpellParticleShape.Drop;
                material.SetFloat("_Intensity", opaque ? 1f : shape == (int)SpellParticleShape.Flame ? 1.7f : 2.6f);
                material.SetFloat("_DstBlend", opaque ? 10f : 1f); // OneMinusSrcAlpha / One
                if (shape == (int)SpellParticleShape.Flame) material.SetFloat("_FlameMotion", 1f);
                for (int layer = 0; layer < 2; layer++)
                    systems[shape, layer] = CreateSystem((SpellParticleShape)shape, material, layer);
            }
        }

        private ParticleSystem CreateSystem(SpellParticleShape shape, Material material, int layer)
        {
            GameObject node = new GameObject(shape + (layer == 0 ? " Behind" : " Front"));
            node.transform.SetParent(transform, false);
            ParticleSystem system = node.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = shape == SpellParticleShape.Ember || shape == SpellParticleShape.Flame ? 320 : 160;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.gravityModifier = shape == SpellParticleShape.Drop ? 0.065f : 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission;
            emission.enabled = false;
            var shapeModule = system.shape;
            shapeModule.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, shape == SpellParticleShape.Ring || shape == SpellParticleShape.Smoke
                ? AnimationCurve.Linear(0f, 0.35f, 1f, 1f)
                : new AnimationCurve(new Keyframe(0f, 0.85f), new Keyframe(0.12f, 1f), new Keyframe(1f, 0f)));
            var color = system.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0f, 1f) });
            if (shape == SpellParticleShape.Flame)
            {
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(new Color(0.9f, 0.65f, 0.85f), 0.4f),
                        new GradientColorKey(new Color(0.28f, 0.12f, 0.30f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.09f),
                        new GradientAlphaKey(0.45f, 0.5f), new GradientAlphaKey(0f, 1f) });
                var noise = system.noise;
                noise.enabled = true;
                noise.strength = 0.10f;
                noise.frequency = 2f;
                noise.scrollSpeed = 0.35f;
                noise.damping = true;
                noise.quality = ParticleSystemNoiseQuality.Low;
                var force = system.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.y = 0.3f;
                var drag = system.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.limit = 3f;
                drag.dampen = 0.2f;
            }
            color.color = fade;
            var rotation = system.rotationOverLifetime;
            rotation.enabled = shape != SpellParticleShape.Wind;
            rotation.z = shape == SpellParticleShape.Flame
                ? new ParticleSystem.MinMaxCurve(-0.25f, 0.25f) : new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            ParticleSystemRenderer renderer = node.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = layer == 0 ? 2 : 8;
            if (shape == SpellParticleShape.Glow) renderer.sortingOrder = 1;
            if (shape == SpellParticleShape.Smoke) renderer.sortingOrder = 2;
            system.useAutoRandomSeed = false;
            system.randomSeed = (uint)(931 + (int)shape * 13 + layer);
            system.Play();
            return system;
        }

        public void BeginFrame(float dt)
        {
            emittedThisFrame = 0;
            frameLights.Clear();
            for (int i = lightPulses.Count - 1; i >= 0; i--)
            {
                LocalLight light = lightPulses[i];
                light.Life -= Mathf.Max(0f, dt);
                if (light.Life <= 0f) lightPulses.RemoveAt(i);
                else { lightPulses[i] = light; frameLights.Add(light); }
            }
            ActiveParticles = 0;
            foreach (ParticleSystem system in systems)
                if (system != null) ActiveParticles += system.particleCount;
        }

        public void ResetEffects()
        {
            foreach (ParticleSystem system in systems)
                if (system != null) system.Clear();
            ActiveParticles = emittedThisFrame = 0;
            waveParticleSerial = 0;
            lightPulses.Clear();
            frameLights.Clear();
            reflections.Clear(); // Character views are rebuilt by MatchPresentation.
        }

        public void Light(Vector2 position, Color color, float radius, float strength)
        {
            if (frameLights.Count < 80)
                frameLights.Add(new LocalLight { Position = position, Color = color, Radius = radius,
                    Strength = strength, Life = 1f, Duration = 1f });
        }

        // The arena uses layered unlit art: project soft ground glows and tint only the
        // opaque character pixels, instead of introducing lights its shaders cannot receive.
        public void Illuminate(SpriteRenderer target, bool visible = true)
        {
            if (!visible)
            {
                if (reflections.TryGetValue(target, out SpriteRenderer previous)) previous.enabled = false;
                return;
            }
            if (!reflections.TryGetValue(target, out SpriteRenderer reflection))
            {
                GameObject node = new GameObject("Spell Light Reflection");
                node.transform.SetParent(target.transform, false);
                reflection = node.AddComponent<SpriteRenderer>();
                reflection.sharedMaterial = reflectedLight;
                reflections.Add(target, reflection);
            }
            Color illumination = Color.black;
            Vector2 position = target.transform.position;
            foreach (LocalLight light in frameLights)
            {
                float falloff = Mathf.Clamp01(1f - Vector2.Distance(position, light.Position) / light.Radius);
                illumination += light.Color * (falloff * falloff * light.Strength * light.Life / light.Duration);
            }
            float maximum = Mathf.Max(illumination.r, Mathf.Max(illumination.g, illumination.b));
            reflection.enabled = maximum > 0.005f && target.enabled;
            reflection.sprite = target.sprite;
            reflection.flipX = target.flipX;
            reflection.flipY = target.flipY;
            reflection.sortingLayerID = target.sortingLayerID;
            reflection.sortingOrder = target.sortingOrder + 1;
            float strength = Mathf.Min(maximum, 0.30f);
            if (maximum > 0f) illumination /= maximum;
            illumination.a = strength;
            reflection.color = illumination;
        }

        private float Range(float minimum, float maximum) => Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        private Vector2 Disk()
        {
            float angle = Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Sqrt(Range(0f, 1f));
        }

        private void Emit(SpellParticleShape shape, Vector2 position, Vector2 velocity, Color color,
            float size, float life, int order = 8, float angle = float.NaN, bool important = false)
        {
            // Leave capacity for impacts when trails are busy. Native buffers provide a second hard cap.
            if (emittedThisFrame >= (important ? 420 : 260) || (!important && ActiveParticles > 1900)) return;
            ParticleSystem system = systems[(int)shape, order < 3 ? 0 : 1];
            if (system.particleCount >= system.main.maxParticles) return;
            if (!important && system.particleCount >= system.main.maxParticles - 40) return;
            var particle = new ParticleSystem.EmitParams
            {
                position = new Vector3(position.x, position.y, -0.34f),
                velocity = new Vector3(velocity.x, velocity.y, 0f),
                startColor = color,
                startSize = size,
                startLifetime = life,
                rotation = float.IsNaN(angle) ? Range(-180f, 180f) : -angle,
                randomSeed = (uint)random.Next(1, int.MaxValue)
            };
            system.Emit(particle, 1);
            emittedThisFrame++;
        }

        private static SpellParticleShape ShapeFor(WeaponType weapon)
        {
            switch (weapon)
            {
                case WeaponType.Explosive: case WeaponType.Orbit: return SpellParticleShape.Flame;
                case WeaponType.Slime: case WeaponType.Water: return SpellParticleShape.Drop;
                case WeaponType.Wind: return SpellParticleShape.Wind;
                default: return SpellParticleShape.Ember;
            }
        }

        public void Trail(WeaponType weapon, Vector2 from, Vector2 to, Vector2 velocity,
            ref float clock, float dt, float strength = 1f, int order = 8)
        {
            SpellVisualStyle profile = Profile(weapon);
            Light(to, profile.Color, weapon == WeaponType.Orbit ? 0.9f : 0.7f,
                weapon == WeaponType.Orbit || weapon == WeaponType.Explosive ? 0.10f * strength : 0.05f);
            float rate = profile.TrailRate * particleDensity * strength;
            if (rate <= 0f || dt <= 0f) return;
            bool fire = weapon == WeaponType.Explosive || weapon == WeaponType.Orbit;
            float distance = Vector2.Distance(from, to);
            if (distance > 2.5f) { from = to; distance = 0f; clock = 0f; }
            float spacing = Mathf.Clamp(profile.ParticleSize * 0.8f, 0.07f, 0.14f)
                / Mathf.Max(0.1f, particleDensity * Mathf.Sqrt(Mathf.Max(0.1f, strength)));
            float carried = clock;
            float emittedDistance = fire ? Mathf.Max(distance / spacing, Mathf.Min(dt, 0.1f) * rate * 0.20f)
                : Mathf.Min(dt, 0.1f) * rate;
            clock += emittedDistance;
            int count = Mathf.Min(fire ? 32 : 12, Mathf.FloorToInt(clock));
            clock -= Mathf.Floor(clock);
            Vector2 direction = velocity.sqrMagnitude > 0.001f ? velocity.normalized : Vector2.up;
            for (int i = 0; i < count; i++)
            {
                float along = fire ? Mathf.Clamp01((i + 1f - carried) / Mathf.Max(0.0001f, emittedDistance))
                    : (i + 0.5f) / count;
                Vector2 position = Vector2.Lerp(from, to, along) + Disk() * (fire ? 0.018f : 0.035f);
                Color tint = fire ? Color.Lerp(profile.Color, Color.Lerp(profile.Accent, Color.white, 0.35f), Range(0.2f, 0.8f))
                    : Color.Lerp(profile.Color, profile.Accent, Range(0f, 0.7f));
                Vector2 drift = -direction * (fire ? Mathf.Clamp(velocity.magnitude * 0.06f, 0.08f, 0.55f) : Range(0.06f, 0.28f))
                    + Disk() * (fire ? 0.055f : 0.13f);
                if (fire) drift += Vector2.up * 0.12f;
                // Wind arcs face the projectile's travel, not the backwards drift of its wake.
                Vector2 facing = weapon == WeaponType.Wind ? direction : drift;
                float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f;
                Emit(ShapeFor(weapon), position, drift, tint,
                    profile.ParticleSize * Range(0.7f, 1.5f), profile.ParticleLife * Range(0.7f, 1.2f), order, angle);
                if (weapon == WeaponType.Orbit || weapon == WeaponType.Explosive)
                    Emit(SpellParticleShape.Ember, position, drift * 2f, profile.Accent,
                        Range(0.015f, 0.035f), Range(0.25f, 0.55f), order);
                if (weapon == WeaponType.Wind && i % 3 == 0)
                    Emit(SpellParticleShape.Smoke, position, drift, new Color(0.50f, 0.36f, 0.24f, 0.35f),
                        Range(0.07f, 0.15f), 0.32f, order);
            }
        }

        public void WaveParticles(Vector2 from, Vector2 to, Vector2 direction, float radius,
            ref float clock, float dt)
        {
            if (dt <= 0f || particleDensity <= 0f) return;
            if ((to-from).sqrMagnitude > 6.25f) from = to;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            Vector2 normal = new Vector2(-direction.y,direction.x);
            clock += Mathf.Min(dt,0.1f)*300f*particleDensity;
            int count = Mathf.Min(40,(int)clock);
            clock -= Mathf.Floor(clock);
            Light(to,Color.white,0.85f,0.16f);
            for (int i = 0; i < count; i++)
            {
                int serial = waveParticleSerial++;
                float side = Range(-1f,1f);
                Vector2 position = Vector2.Lerp(from,to,(i+0.5f)/count)
                    + normal*(side*radius) + direction*(radius*0.65f-side*side*radius*0.55f);
                if (serial%4 == 0) position -= direction*Range(0.08f,0.32f);
                Vector2 drift = -direction*Range(0.18f,0.80f)+normal*Range(-0.20f,0.20f);
                Color white = new Color(1f,1f,1f,Range(0.75f,1f));
                Emit(serial%7 == 0 ? SpellParticleShape.Star : SpellParticleShape.Ember,
                    position,drift,white,Range(0.018f,0.052f),Range(0.30f,0.62f),8,
                    Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-90f);
            }
        }

        public void Impact(WeaponType weapon, Vector2 position, Vector2 direction, float power = 1f)
        {
            SpellVisualStyle profile = Profile(weapon);
            LocalLight light = new LocalLight { Position = position, Color = profile.Color,
                Radius = 1.5f * power, Strength = 0.5f * power, Life = 0.22f, Duration = 0.22f };
            if (lightPulses.Count >= 24) lightPulses.RemoveAt(0);
            lightPulses.Add(light);
            frameLights.Add(light);
            int count = Mathf.RoundToInt((weapon == WeaponType.Explosive ? 64
                : weapon == WeaponType.Water || weapon == WeaponType.Slime ? 42 : 26) * particleDensity * power);
            for (int i = 0; i < count; i++)
            {
                Vector2 spread = Disk();
                Vector2 velocity = (spread * Range(0.7f, 2.3f) + direction * 0.45f) * power;
                Color tint = Color.Lerp(profile.Color, profile.Accent, Range(0f, 1f));
                bool liquid = weapon == WeaponType.Water || weapon == WeaponType.Slime;
                SpellParticleShape shape = i % (liquid ? 4 : 3) == 0 ? SpellParticleShape.Ember : ShapeFor(weapon);
                if (weapon == WeaponType.Explosive && i % 3 != 0) shape = SpellParticleShape.Ember;
                float size = profile.ParticleSize * (shape == SpellParticleShape.Flame ? 3.3f : liquid ? 1.3f : 0.8f) * Range(0.5f, 1.5f);
                Vector2 facing = weapon == WeaponType.Wind && direction.sqrMagnitude > 0.001f ? direction : velocity;
                Emit(shape, position + spread * 0.05f, velocity, tint, size * power,
                    Range(0.16f, 0.5f) * (shape == SpellParticleShape.Flame ? 1.5f : 1f), 8,
                    Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f, true);
            }
            if (weapon != WeaponType.Explosive)
                Emit(SpellParticleShape.Star, position, Vector2.zero, profile.Accent, 0.48f * power, 0.13f, 8, 0f, true);
            Emit(SpellParticleShape.Ring, position, Vector2.zero, profile.Color, 0.9f * power, 0.28f, 8, 0f, true);
            Emit(SpellParticleShape.Glow, position - Vector2.up * 0.1f, Vector2.zero,
                new Color(profile.Color.r, profile.Color.g, profile.Color.b, 0.28f), 1.4f * power, 0.22f, 1, 0f, true);
            if (weapon == WeaponType.Explosive)
                for (int i = 0; i < 7; i++)
                    Emit(SpellParticleShape.Smoke, position + Disk() * 0.25f, Vector2.up * Range(0.15f, 0.45f),
                        new Color(0.16f, 0.10f, 0.18f, 0.25f), Range(0.4f, 0.8f), Range(0.4f, 0.8f), 2, 0f, true);
        }

        public void Charge(Vector2 position, float progress, ref float clock, float dt)
        {
            Light(position, Profile(WeaponType.Lightning).Color, 0.75f, progress * 0.12f);
            clock += Mathf.Min(dt, 0.1f) * (25f + progress * 55f) * particleDensity;
            int count = Mathf.Min(10, (int)clock);
            clock -= Mathf.Floor(clock);
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Disk().normalized * Range(0.14f, 0.32f);
                Emit(SpellParticleShape.Ember, position + offset, -offset * 4f,
                    Profile(WeaponType.Lightning).Accent, Range(0.03f, 0.07f), 0.16f);
            }
        }

        public void Lightning(Vector2 from, Vector2 to)
        {
            for (int i = 0; i < 5; i++)
            {
                Vector2 position = Vector2.Lerp(from, to, i / 4f);
                if (lightPulses.Count >= 24) lightPulses.RemoveAt(0);
                lightPulses.Add(new LocalLight { Position = position, Color = Profile(WeaponType.Lightning).Accent,
                    Radius = 1f, Strength = 0.25f, Life = 0.16f, Duration = 0.16f });
            }
            for (int i = 0; i < 28; i++)
                Emit(SpellParticleShape.Ember, Vector2.Lerp(from, to, i / 27f) + Disk() * 0.07f,
                    Disk() * 1.6f, Profile(WeaponType.Lightning).Accent, Range(0.04f, 0.10f), Range(0.1f, 0.3f), important: true);
            Impact(WeaponType.Lightning, to, (to - from).normalized, 0.8f);
        }

        public void Ripple(Vector2 position, float radius, float fade)
        {
            Emit(SpellParticleShape.Ring, position, Vector2.zero,
                new Color(0.25f, 0.85f, 1f, 0.12f * fade), radius * 1.75f, 0.50f, 2, 0f);
            Emit(SpellParticleShape.Star, position + Disk() * radius * 0.8f, Vector2.up * 0.03f,
                new Color(0.65f, 0.95f, 1f, fade * 0.65f), 0.08f, 0.3f, 2, 0f);
        }

        public SpriteRenderer CreateFireOverlay(Transform parent, int sortingOrder = 8)
        {
            GameObject node = new GameObject("Animated Fire Overlay");
            node.transform.SetParent(parent, false);
            SpriteRenderer renderer = node.AddComponent<SpriteRenderer>();
            // Full-rect UVs, independent of the cropped ability illustration.
            renderer.sprite = GlowSprite;
            renderer.sharedMaterial = fireCorona;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        public void UpdateFireOverlay(SpriteRenderer overlay, float imageDiameter, float phase, float heat, float opacity = 1f)
        {
            overlay.transform.localScale = Vector3.one * (imageDiameter * 1.7f);
            overlay.color = new Color(1f, 1f, 1f, opacity);
            fireProperties.Clear();
            fireProperties.SetFloat("_Phase", phase);
            fireProperties.SetFloat("_Heat", Mathf.Clamp01(heat));
            overlay.SetPropertyBlock(fireProperties);
        }

        private Sprite CreateSprite(SpellParticleShape shape)
        {
            const int size = 64;
            Texture2D texture = Own(new Texture2D(size, size, TextureFormat.RGBA32, false));
            texture.name = "Spell " + shape + " Texture";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                Vector2 p = new Vector2(u * 2f - 1f, v * 2f - 1f);
                float r = p.magnitude;
                float alpha;
                float value = 1f;
                switch (shape)
                {
                    case SpellParticleShape.Flame:
                        float bend = Mathf.Sin(v * 9f) * 0.18f * v;
                        float width = Mathf.Max(0.025f, Mathf.Sin(Mathf.Pow(v, 0.65f) * Mathf.PI) * 0.56f * (1f - v * 0.65f));
                        width *= 0.65f + Mathf.PerlinNoise(u * 8f, v * 9f) * 0.65f;
                        alpha = Mathf.Clamp01(1f - Mathf.Abs(p.x - bend) / width);
                        alpha = alpha * alpha * Mathf.Clamp01(v * 12f) * Mathf.Clamp01((1f - v) * 9f);
                        value = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(alpha * 2f));
                        break;
                    case SpellParticleShape.Ember:
                        alpha = Mathf.Clamp01(1f - new Vector2(p.x * 3.2f, p.y).magnitude);
                        alpha *= alpha;
                        break;
                    case SpellParticleShape.Star:
                        alpha = Mathf.Clamp01(1f - Mathf.Abs(p.x * p.y) * 55f) * Mathf.Pow(Mathf.Clamp01(1f - r), 1.8f);
                        break;
                    case SpellParticleShape.Ring:
                        alpha = Mathf.Exp(-Mathf.Pow((r - 0.78f) * 30f, 2f));
                        break;
                    case SpellParticleShape.Wind:
                        float arc = new Vector2(p.x, p.y + 0.6f).magnitude;
                        alpha = Mathf.Exp(-Mathf.Pow((arc - 0.9f) * 19f, 2f)) * Mathf.Clamp01(1f - Mathf.Abs(p.x));
                        break;
                    case SpellParticleShape.Drop:
                        alpha = 1f - Mathf.SmoothStep(0.70f, 0.98f, new Vector2(p.x * 1.1f, p.y).magnitude);
                        value = Mathf.Clamp01(0.28f + (1f - r) * 0.22f + Mathf.Exp(-((p - new Vector2(-0.28f, 0.32f)).sqrMagnitude) * 40f));
                        break;
                    case SpellParticleShape.Smoke:
                        alpha = Mathf.Pow(Mathf.Clamp01(1f - r), 1.5f) * (0.45f + Mathf.PerlinNoise(u * 7f, v * 7f) * 0.55f);
                        break;
                    default:
                        alpha = Mathf.Pow(Mathf.Clamp01(1f - r), 3f);
                        break;
                }
                pixels[y * size + x] = new Color(value, value, value, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Own(Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect));
        }

        private T Own<T>(T asset) where T : Object { owned.Add(asset); return asset; }
        private void OnDestroy()
        {
            foreach (Object asset in owned) if (asset != null) Destroy(asset);
            owned.Clear();
        }
    }
}
