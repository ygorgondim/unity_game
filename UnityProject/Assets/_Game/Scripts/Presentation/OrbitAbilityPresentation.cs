using System.Collections.Generic;
using UnityEngine;
using WizardRoyale.Domain;

namespace WizardRoyale.Presentation
{
    // Reads the simulated orb position: the visible orb and its damaging hitbox stay together.
    public sealed class OrbitAbilityPresentation
    {
        private readonly Transform root;
        private readonly Sprite orbSprite;
        private readonly Material orbMaterial;
        private readonly Sprite glowSprite;
        private readonly Material lineMaterial;
        private readonly SpellParticleLibrary particles;
        private readonly Dictionary<int, OrbVisual[]> views = new Dictionary<int, OrbVisual[]>();
        private sealed class OrbVisual
        {
            public Transform Root;
            public SpriteRenderer Body, Glow, Core, Pearl, Ground;
            public LineRenderer Trail;
            public readonly SpriteRenderer[] Flames = new SpriteRenderer[7];
            public Vector2 Previous;
            public float EmissionClock;
            public bool WasVisible;
            public Vector2 SmoothedVelocity;
            public readonly Vector2[] History = new Vector2[32];
            public readonly float[] HistoryTimes = new float[32];
            public int HistoryCount;
            public float HistoryClock;
        }

        public OrbitAbilityPresentation(Transform parent, Sprite orb, Sprite glow, Material material,
            SpellParticleLibrary particleLibrary, Material bodyMaterial = null)
        {
            root = parent;
            orbSprite = orb;
            orbMaterial = bodyMaterial;
            glowSprite = glow;
            lineMaterial = material;
            particles = particleLibrary;
        }

        public void Reset()
        {
            foreach (OrbVisual[] group in views.Values)
                foreach (OrbVisual view in group)
                    if (view.Root != null) Object.Destroy(view.Root.gameObject);
            views.Clear();
        }

        public void Sync(PlayerState player, float deltaTime)
        {
            OrbVisual[] group;
            bool visible = player.Weapon == WeaponType.Orbit && player.Orbit.Initialized
                && !player.IsEliminated && !player.IsFalling;
            if (!views.TryGetValue(player.Id, out group))
            {
                if (!visible) return;
                group = new OrbVisual[GameTuning.OrbitCount];
                for (int index = 0; index < group.Length; index++)
                    group[index] = CreateView(player.Id, index);
                views.Add(player.Id, group);
            }
            foreach (OrbVisual view in group)
            {
                view.Root.gameObject.SetActive(visible);
                if (!visible)
                {
                    view.WasVisible = false; view.EmissionClock = 0f;
                    view.HistoryCount = 0; view.HistoryClock = 0f; view.SmoothedVelocity = Vector2.zero;
                }
            }
            if (!visible) return;

            for (int index = 0; index < group.Length; index++)
                SyncOrb(player, group[index], index, deltaTime);
        }

        private void SyncOrb(PlayerState player, OrbVisual visual, int index, float dt)
        {
            Transform view = visual.Root;
            OrbitState orb = player.Orbit;
            Vector2 position = orb.Orbs[index].Position;
            Vector2 previous = visual.WasVisible ? visual.Previous : position;
            Vector2 velocity = dt > 0.0001f ? (position - previous) / dt : Vector2.zero;
            visual.SmoothedVelocity = Vector2.Lerp(visual.SmoothedVelocity, velocity,
                1f - Mathf.Exp(-18f * Mathf.Max(0f, dt)));
            view.position = new Vector3(position.x, position.y, -0.24f);
            ArenaDepth.Set(view, position.y - CharacterHitbox.CenterHeight);
            SpriteRenderer body = visual.Body;
            int order = position.y > CharacterHitbox.Center(player.Position).y ? 2 : 5;
            body.sortingOrder = order;
            float diameter = GameTuning.OrbitOrbRadius * 2f;
            view.localScale = Vector3.one * (diameter / orbSprite.bounds.size.x);
            SpriteRenderer glow = visual.Glow;
            glow.sortingOrder = order - 1;
            Color color = WeaponCatalog.Get(WeaponType.Orbit).Color;
            glow.color = new Color(color.r, color.g, color.b, 0.22f + orb.Expansion * 0.12f);

            float scale = view.localScale.x;
            float pulse = 1f + Mathf.Sin(Time.time * 9f + index * 2f) * 0.08f;
            visual.Core.sortingOrder = order + 1;
            visual.Core.color = new Color(0.94f, 0.72f, 1f, 0.95f);
            visual.Core.transform.localScale = Vector3.one * (0.115f * pulse * (1f + orb.Expansion * 0.25f) / scale);
            visual.Core.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 42f + index * 37f);
            visual.Pearl.sortingOrder = order + 1;
            visual.Pearl.color = new Color(1f, 0.85f, 1f, 1f);
            visual.Pearl.transform.localScale = Vector3.one * (0.10f * pulse / scale);
            visual.Ground.transform.position = new Vector3(position.x, position.y - 0.16f, -0.1f);
            visual.Ground.transform.localScale = new Vector3(0.85f, 0.38f, 1f) / scale;
            visual.Ground.color = new Color(0.48f, 0.035f, 1f, 0.16f + orb.Expansion * 0.07f);
            Vector2 tangent = new Vector2(-Mathf.Sin(orb.AngleFor(index)), Mathf.Cos(orb.AngleFor(index)));
            Vector2 motion = visual.SmoothedVelocity.sqrMagnitude > 0.01f ? visual.SmoothedVelocity.normalized : tangent;
            float trailingAngle = Mathf.Atan2(-motion.y, -motion.x) * Mathf.Rad2Deg - 90f;
            for (int i = 0; i < visual.Flames.Length; i++)
            {
                SpriteRenderer flame = visual.Flames[i];
                float phase = Time.time * (11f + i * 0.9f) + index * 2.3f + i * 1.8f;
                float flutter = Mathf.PerlinNoise(index * 3f + i * 1.31f, Time.time * 2.1f);
                float angle = i * 2.39996f + (flutter - 0.5f) * 0.7f;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.105f;
                flame.transform.localPosition = new Vector3(offset.x / scale, offset.y / scale, 0f);
                flame.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.LerpAngle(angle * Mathf.Rad2Deg - 90f, trailingAngle, 0.50f + orb.Expansion * 0.22f)
                    + Mathf.Sin(phase) * 16f);
                float length = (0.30f + flutter * 0.18f) * (1f + orb.Expansion * 0.45f)
                    * (1f + Mathf.Clamp01(velocity.magnitude / 10f) * 0.15f);
                flame.transform.localScale = new Vector3(0.28f, length, 1f) / scale;
                flame.color = Color.Lerp(new Color(0.32f, 0.025f, 0.85f, 0.8f),
                    new Color(0.8f, 0.08f, 0.9f, 0.7f), i / 6f);
                flame.sortingOrder = order - 1;
            }
            particles.Trail(WeaponType.Orbit, previous, position, velocity,
                ref visual.EmissionClock, dt, 0.65f + orb.Expansion * 1.1f, order);
            visual.Previous = position;
            visual.WasVisible = true;

            LineRenderer trail = visual.Trail;
            trail.sortingOrder = order - 1;
            trail.startWidth = 0.005f;
            trail.endWidth = 0.025f + orb.Expansion * 0.035f;
            trail.startColor = new Color(color.r, color.g, color.b, 0f);
            trail.endColor = new Color(color.r, color.g, color.b, 0.7f);
            RecordHistory(visual, position, dt, Vector2.Distance(previous, position) > 2.5f);
            trail.enabled = visual.HistoryCount > 1;
            trail.positionCount = Mathf.Max(2, visual.HistoryCount);
            for (int i = 0; i < visual.HistoryCount; i++)
            {
                Vector2 point = i == visual.HistoryCount - 1 ? position : visual.History[i];
                trail.SetPosition(i, new Vector3(point.x, point.y, -0.23f));
            }
        }

        private static void RecordHistory(OrbVisual visual, Vector2 position, float dt, bool reset)
        {
            visual.HistoryClock += Mathf.Max(0f, dt);
            if (reset) visual.HistoryCount = 0;
            while (visual.HistoryCount > 1 && visual.HistoryTimes[1] < visual.HistoryClock - 0.20f)
                RemoveOldest(visual);
            int last = visual.HistoryCount - 1;
            if (last >= 0 && (visual.HistoryClock - visual.HistoryTimes[last] < 0.0125f
                || (visual.History[last] - position).sqrMagnitude < 0.000001f)) return;
            if (visual.HistoryCount == visual.History.Length) RemoveOldest(visual);
            int next = visual.HistoryCount++;
            visual.History[next] = position;
            visual.HistoryTimes[next] = visual.HistoryClock;
        }

        private static void RemoveOldest(OrbVisual visual)
        {
            for (int i = 1; i < visual.HistoryCount; i++)
            {
                visual.History[i - 1] = visual.History[i];
                visual.HistoryTimes[i - 1] = visual.HistoryTimes[i];
            }
            visual.HistoryCount--;
        }

        private OrbVisual CreateView(int playerId, int index)
        {
            GameObject view = new GameObject("Orbital Ability - Player " + playerId + " - Orb " + index);
            view.transform.SetParent(root, false);
            OrbVisual visual = new OrbVisual { Root = view.transform, Body = view.AddComponent<SpriteRenderer>() };
            visual.Body.sprite = orbSprite;
            if (orbMaterial != null) visual.Body.sharedMaterial = orbMaterial;
            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(view.transform, false);
            glow.transform.localScale = Vector3.one * (orbSprite.bounds.size.x * 2.6f);
            visual.Glow = glow.AddComponent<SpriteRenderer>();
            visual.Glow.sprite = glowSprite;
            visual.Glow.sharedMaterial = particles.GlowMaterial;
            visual.Core = CreateEnergyNode(view.transform, "White Lilac Stellar Core", particles.StarSprite);
            visual.Pearl = CreateEnergyNode(view.transform, "Incandescent Stellar Pearl", particles.GlowSprite);
            visual.Ground = CreateEnergyNode(view.transform, "Violet Ground Light", particles.GlowSprite);
            visual.Ground.sortingOrder = 1;
            for (int i = 0; i < visual.Flames.Length; i++)
            {
                visual.Flames[i] = CreateEnergyNode(view.transform, "Violet Flame " + i, particles.FlameSprite);
                visual.Flames[i].sharedMaterial = particles.FlameMaterial;
            }
            GameObject trailObject = new GameObject("Orbital Trail");
            trailObject.transform.SetParent(view.transform, false);
            LineRenderer trail = trailObject.AddComponent<LineRenderer>();
            trail.sharedMaterial = lineMaterial;
            trail.useWorldSpace = true;
            trail.positionCount = 16;
            trail.numCapVertices = 3;
            visual.Trail = trail;
            return visual;
        }

        private SpriteRenderer CreateEnergyNode(Transform parent, string name, Sprite sprite)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            SpriteRenderer renderer = node.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = particles.GlowMaterial;
            return renderer;
        }

        public static Sprite CreateOrbSprite(WeaponType type = WeaponType.Orbit)
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Runtime Ability Orb - " + type;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            Color violet = type == WeaponType.Wind ? new Color(0.70f, 0.94f, 0.86f)
                : WeaponCatalog.Get(type).Color;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = (new Vector2(x + 0.5f, y + 0.5f) / size - Vector2.one * 0.5f) * 2f;
                float radius = p.magnitude;
                float spiral = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Mathf.Atan2(p.y, p.x) * 3f - radius * 15f), 3f);
                Color light = type == WeaponType.Wind ? Color.white : new Color(0.94f, 0.82f, 1f);
                Color color = Color.Lerp(violet * 0.65f, light,
                    Mathf.Clamp01((1f - radius) * 0.7f + spiral * 0.5f));
                float highlight = Mathf.Exp(-(p - new Vector2(-0.3f, 0.35f)).sqrMagnitude * 28f);
                color = Color.Lerp(color, Color.white, highlight * 0.9f);
                if (type == WeaponType.Orbit)
                {
                    float fissure = Mathf.Pow(Mathf.Clamp01(spiral), 5f) * Mathf.SmoothStep(0.15f, 0.4f, radius);
                    color = Color.Lerp(new Color(0.025f, 0.008f, 0.055f), new Color(0.36f, 0.025f, 0.65f),
                        Mathf.Clamp01(fissure * 0.85f + Mathf.Pow(radius, 5f) * 0.7f));
                    color = Color.Lerp(color, new Color(0.96f, 0.80f, 1f), Mathf.Exp(-radius * radius * 85f));
                }
                color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.92f, 1f, radius));
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f,
                size / (GameTuning.PickupRadius * 2f), 0, SpriteMeshType.FullRect);
        }
    }
}
