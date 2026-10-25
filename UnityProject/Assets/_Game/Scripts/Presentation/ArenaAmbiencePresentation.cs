using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WizardRoyale.Domain;

namespace WizardRoyale.Presentation
{
    // Cosmetic only. A fixed particle pool and its own random source keep ambience independent of gameplay.
    public sealed class ArenaAmbiencePresentation : MonoBehaviour
    {
        private const int MoteCount = 64;
        private readonly System.Random random = new System.Random(1738);
        private readonly List<Object> ownedAssets = new List<Object>();
        private readonly List<Mote> motes = new List<Mote>();
        private readonly List<SpriteRenderer> sunbeams = new List<SpriteRenderer>();
        private readonly Dictionary<int, PersonalLight> personalLights = new Dictionary<int, PersonalLight>();
        private static readonly float[] ShaftPositions = { -4.2f, -3.45f, -0.65f, 2.1f, 2.8f };
        private static readonly float[] ShaftWidths = { 3.6f, 0.7f, 2.7f, 3.9f, 0.6f };
        private static readonly float[] ShaftStrengths = { 0.44f, 0.24f, 0.38f, 0.42f, 0.20f };
        [SerializeField] private ArenaLightingSettings lighting;
        private Material groundLightMaterial;
        private Sprite lightQuad;
        private Sprite softGlow;
        private Camera lightingCamera;
        private SpriteRenderer solarHaze;
        private SpriteRenderer solarFlare;
        private GoldenArenaLayout layout;
        private Material glowMaterial;
        private ArenaWaterAtmosphere waterAtmosphere;
        private Vector2 floorMinimum;
        private Vector2 floorMaximum;
        private float clock;

        private sealed class PersonalLight
        {
            public Transform Root;
            public SpriteRenderer Ground;
            public SpriteRenderer Air;
        }

        private sealed class Mote
        {
            public Transform Transform;
            public SpriteRenderer Core;
            public SpriteRenderer Halo;
            public Vector2 Anchor;
            public Vector2 Drift;
            public float Age;
            public float Duration;
            public float Rest;
            public float Phase;
            public float Size;
            public Color Color;
        }

        public void Initialize(Camera targetCamera)
        {
            lightingCamera = targetCamera;
            ArenaLightingSettings saved = Resources.Load<ArenaLightingSettings>("Lighting/ArenaLightingSettings");
            lighting = Own(saved != null ? Instantiate(saved) : ScriptableObject.CreateInstance<ArenaLightingSettings>());
            layout = GoldenArenaLayout.Current;
            floorMinimum = floorMaximum = layout.PixelToWorld(layout.floor[0]);
            foreach (Vector2 vertex in layout.floor)
            {
                Vector2 world = layout.PixelToWorld(vertex);
                floorMinimum = Vector2.Min(floorMinimum, world);
                floorMaximum = Vector2.Max(floorMaximum, world);
            }

            TintEnvironment();
            ConfigureSunsetCamera(targetCamera);
            waterAtmosphere = new ArenaWaterAtmosphere(transform, targetCamera, layout);
            Shader shader = Resources.Load<Shader>("Shaders/AmbientGlow");
            if (shader == null)
            {
                Debug.LogWarning("AmbientGlow shader missing: sunset grading remains active, atmospheric particles skipped.");
                return;
            }
            glowMaterial = Own(new Material(shader));
            glowMaterial.name = "Twilight - Solar Glow";
            Sprite glow = CreateSprite("Ambient Soft Glow", 0);
            softGlow = glow;
            Sprite star = CreateSprite("Ambient Anime Sparkle", 1);
            Sprite beam = CreateSprite("Ambient Soft Sunbeam", 2);

            InitializeGroundLights();
            CreateSunlight(glow, star, beam);
            for (int i = 0; i < MoteCount; i++)
            {
                GameObject moteObject = new GameObject("Floating Light " + (i + 1).ToString("00"));
                moteObject.transform.SetParent(transform, false);
                int sortingOrder = i % 5 == 0 ? 6 : 0;
                Mote mote = new Mote
                {
                    Transform = moteObject.transform,
                    Core = CreateRenderer(moteObject.transform, "Light", i % 6 == 0 ? star : glow, sortingOrder),
                    Halo = CreateRenderer(moteObject.transform, "Halo", glow, sortingOrder - 1)
                };
                RespawnMote(mote);
                mote.Age = Range(0f, mote.Duration + mote.Rest);
                motes.Add(mote);
            }
            Tick(0f);
        }

        private void ConfigureSunsetCamera(Camera targetCamera)
        {
            UniversalAdditionalCameraData cameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            targetCamera.allowHDR = true;
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = cameraData.volumeLayerMask.value | (1 << gameObject.layer);

            VolumeProfile profile = Own(ScriptableObject.CreateInstance<VolumeProfile>());
            profile.name = "Golden Arena - Blue Twilight and Sun Shafts";

            WhiteBalance balance = Own(profile.Add<WhiteBalance>(true));
            balance.temperature.Override(-9f);
            balance.tint.Override(5f);

            ColorAdjustments colors = Own(profile.Add<ColorAdjustments>(true));
            colors.postExposure.Override(lighting.Exposure);
            colors.contrast.Override(9f);
            colors.saturation.Override(-3f);
            colors.colorFilter.Override(new Color(0.90f, 0.94f, 1f));

            Bloom bloom = Own(profile.Add<Bloom>(true));
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(lighting.BloomIntensity);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.96f, 0.90f));

            Vignette vignette = Own(profile.Add<Vignette>(true));
            vignette.color.Override(new Color(0.025f, 0.035f, 0.09f));
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(0.27f);
            vignette.smoothness.Override(0.65f);
            vignette.rounded.Override(false);

            Volume volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 30f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        private void TintEnvironment()
        {
            Transform geometry = transform.parent.Find("Golden Arena Layers");
            if (geometry == null) return;
            foreach (SpriteRenderer renderer in geometry.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer.name == "Arena - Decorative Cliffs") renderer.color = lighting.GroundTint;
                else if (renderer.name == "Ocean - Fall Zone" || renderer.name.StartsWith("Ocean Tile"))
                    renderer.color = lighting.OceanTint;
            }
        }

        private void InitializeGroundLights()
        {
            Shader shader = Resources.Load<Shader>("Shaders/AmbientGroundLight");
            if (shader == null) throw new System.InvalidOperationException("AmbientGroundLight shader is missing.");
            groundLightMaterial = Own(new Material(shader));
            groundLightMaterial.name = "Twilight - Diffuse Ground Illumination";
            Vector2 size = new Vector2(layout.width, layout.height) / layout.pixelsPerUnit;
            Vector2 minimum = layout.ArtCenter - size * 0.5f;
            groundLightMaterial.SetTexture("_ArenaTex", Resources.Load<Texture2D>(layout.resourceRoot + "/arena"));
            groundLightMaterial.SetTexture("_FloorTex", layout.FloorMaskTexture);
            groundLightMaterial.SetVector("_ArenaRect", new Vector4(minimum.x, minimum.y, 1f / size.x, 1f / size.y));
            groundLightMaterial.SetFloat("_Intensity", 1.2f);
            Texture2D white = Texture2D.whiteTexture;
            lightQuad = Own(Sprite.Create(white, new Rect(0f, 0f, white.width, white.height),
                Vector2.one * 0.5f, white.width, 0, SpriteMeshType.FullRect));
        }

        private SpriteRenderer GroundLight(Transform parent, string name, int order, Material material = null)
        {
            SpriteRenderer renderer = CreateRenderer(parent, name, lightQuad, order);
            renderer.sharedMaterial = material != null ? material : groundLightMaterial;
            return renderer;
        }

        private void CreateSunlight(Sprite glow, Sprite star, Sprite beam)
        {
            SpriteRenderer sunlight = CreateRenderer(transform, "Warm Sun Haze", glow, -7);
            solarHaze = sunlight;
            sunlight.transform.localPosition = new Vector3(-5.1f, 4.4f, 0f);
            sunlight.transform.localScale = Vector3.one * 8f;
            sunlight.color = new Color(1f, 0.57f, 0.22f, 0.20f * lighting.SunbeamIntensity);

            SpriteRenderer flare = CreateRenderer(transform, "Soft Solar Source", star, 0);
            solarFlare = flare;
            flare.transform.localPosition = new Vector3(-5.4f, 4.3f, 0f);
            flare.transform.localScale = new Vector3(5.4f, 1.8f, 1f);
            flare.color = new Color(1f, 0.82f, 0.52f, 0.42f * lighting.SunbeamIntensity);
            SpriteRenderer veil = CreateRenderer(transform, "Solar Atmospheric Veil", beam, -1);
            veil.transform.localPosition = new Vector3(-3.8f, 2.8f, 0f);
            veil.transform.localScale = new Vector3(2.8f, 9f, 1f);
            veil.transform.localRotation = Quaternion.Euler(0f, 0f, 32f);
            veil.color = new Color(1f, 0.70f, 0.35f, 0.06f * lighting.SunbeamIntensity);

            Shader airShader = Resources.Load<Shader>("Shaders/AmbientSolarShaft");
            if (airShader == null) throw new System.InvalidOperationException("AmbientSolarShaft shader is missing.");
            Material airMaterial = Own(new Material(airShader));
            airMaterial.name = "Twilight - Unmasked Atmospheric Solar Shafts";

            for (int i = 0; i < 5; i++)
            {
                // Solar light has exactly one pass, independent of all floor/island masks.
                // Above water clouds (order 10), below identifiers (order 20+).
                SpriteRenderer ray = CreateRenderer(transform, "Atmospheric Solar Shaft " + (i + 1), lightQuad, 11);
                ray.sharedMaterial = airMaterial;
                ray.transform.localPosition = new Vector3(ShaftPositions[i], 0.5f + i * 0.08f, 0f);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, 32f);
                ray.transform.localScale = new Vector3(ShaftWidths[i], 13f, 1f);
                sunbeams.Add(ray);
            }
        }

        public void ResetCharacterLights()
        {
            foreach (PersonalLight light in personalLights.Values)
                if (light.Root != null) Destroy(light.Root.gameObject);
            personalLights.Clear();
        }

        public void SyncCharacterLight(PlayerState player, SpellParticleLibrary spells)
        {
            if (groundLightMaterial == null) return;
            if (!personalLights.TryGetValue(player.Id, out PersonalLight light))
            {
                Transform anchor = new GameObject("Diffuse Personal Light - " + player.Name).transform;
                anchor.SetParent(transform, false);
                light = new PersonalLight
                {
                    Root = anchor,
                    Ground = GroundLight(anchor, "Borderless Ground Illumination", 0),
                    Air = CreateRenderer(anchor, "Soft Character Light Scatter", softGlow, 2)
                };
                personalLights.Add(player.Id, light);
            }
            bool visible = !player.IsEliminated && !player.IsFalling && layout.ContainsWorld(player.Position);
            light.Root.gameObject.SetActive(visible);
            if (!visible) return;
            light.Root.position = new Vector3(player.Position.x, player.Position.y + 0.025f, -0.12f);
            float breath = 1f + Mathf.Sin(clock * 1.3f + player.Id * 1.7f) * 0.025f;
            light.Ground.transform.localScale = new Vector3(lighting.PersonalLightSize.x,
                lighting.PersonalLightSize.y, 1f) * breath;
            float cooldown = WeaponCatalog.Get(player.Weapon).Cooldown;
            float cast = Mathf.Clamp01(1f - (cooldown - player.AttackCooldown) / 0.24f);
            if (player.Weapon == WeaponType.Orbit) cast = Mathf.Max(cast, player.Orbit.Expansion);
            Color tint = Color.Lerp(lighting.PersonalLightColor, player.Color, 0.12f);
            tint = Color.Lerp(tint, WeaponCatalog.Get(player.Weapon).Color, cast * 0.30f);
            light.Ground.color = new Color(tint.r, tint.g, tint.b, lighting.PersonalLightIntensity * 0.62f);
            light.Air.transform.localPosition = Vector3.up * 0.28f;
            light.Air.transform.localScale = new Vector3(1.25f, 1.65f, 1f);
            light.Air.color = new Color(tint.r, tint.g, tint.b, lighting.PersonalLightIntensity * 0.055f);
            spells.Light(CharacterHitbox.Center(player.Position), tint, 0.9f, lighting.PersonalLightIntensity * 0.10f);
            float sun = 0f;
            for (int i = 0; i < sunbeams.Count; i++)
            {
                Vector3 local = sunbeams[i].transform.InverseTransformPoint(player.Position);
                float beamWidth = Mathf.Lerp(0.85f, 0.32f, local.y + 0.5f);
                float across = local.x * 2f / Mathf.Max(0.1f, beamWidth);
                sun += Mathf.Exp(-across * across * 3f) * ShaftStrengths[i];
            }
            spells.Light(CharacterHitbox.Center(player.Position), lighting.SunColor, 0.9f,
                Mathf.Clamp01(sun) * lighting.SunbeamIntensity * 0.20f);
        }

        public void Tick(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            clock += dt;
            ExtendSunlightToScreenEdges();
            if (waterAtmosphere != null) waterAtmosphere.Tick(clock);
            for (int i = 0; i < sunbeams.Count; i++)
            {
                float breath = 0.85f + Mathf.Sin(clock * 0.22f + i * 1.7f) * 0.15f;
                Color sun = lighting.SunColor;
                sun.a = lighting.SunbeamIntensity * ShaftStrengths[i] * breath * 0.55f;
                sunbeams[i].color = sun;
                sunbeams[i].sharedMaterial.SetFloat("_AmbientTime", clock);
            }

            foreach (Mote mote in motes)
            {
                mote.Age += dt;
                if (mote.Age >= mote.Duration + mote.Rest) RespawnMote(mote);
                float progress = Mathf.Clamp01(mote.Age / mote.Duration);
                float fade = Mathf.Sin(progress * Mathf.PI);
                fade = mote.Age < mote.Duration ? fade * fade : 0f;
                Vector2 sway = new Vector2(Mathf.Sin(clock * 0.65f + mote.Phase) * 0.12f,
                    Mathf.Sin(clock * 0.42f + mote.Phase) * 0.07f);
                mote.Transform.localPosition = mote.Anchor + mote.Drift * mote.Age + sway;
                float size = mote.Size * (0.90f + Mathf.Sin(clock + mote.Phase) * 0.10f);
                mote.Core.transform.localScale = Vector3.one * size;
                mote.Halo.transform.localScale = Vector3.one * (size * 4.8f);
                Color coreColor = mote.Color * 1.8f;
                coreColor.a = fade * 0.80f;
                mote.Core.color = coreColor;
                Color haloColor = mote.Color;
                haloColor.a = fade * 0.16f;
                mote.Halo.color = haloColor;
            }
        }

        private void ExtendSunlightToScreenEdges()
        {
            if (lightingCamera == null) return;
            float halfHeight = lightingCamera.orthographicSize;
            float halfWidth = halfHeight * lightingCamera.aspect;
            Vector2 cameraCenter = lightingCamera.transform.position;
            foreach (SpriteRenderer ray in sunbeams)
            {
                Vector2 axis = ray.transform.up;
                float reach = Mathf.Abs(Vector2.Dot(cameraCenter - (Vector2)ray.transform.position, axis))
                    + Mathf.Abs(axis.x) * halfWidth + Mathf.Abs(axis.y) * halfHeight;
                Vector3 scale = ray.transform.localScale;
                // Keep the faded endpoints outside the viewport even at maximum zoom.
                // The shafts retain their world-space centers and direction while extending.
                scale.y = Mathf.Max(13f, (reach + 4f) * 2.6f);
                ray.transform.localScale = scale;
            }
            float distance = Mathf.Abs(lightingCamera.transform.position.z);
            float zoom = Mathf.Clamp(halfHeight / 4.3f, 0.7f, 2f);
            if (solarFlare != null)
            {
                Vector3 source = lightingCamera.ViewportToWorldPoint(new Vector3(0.06f, 1.02f, distance));
                source.z = 0f;
                solarFlare.transform.position = source;
                solarFlare.transform.localScale = new Vector3(5.4f, 1.8f, 1f) * zoom;
            }
            if (solarHaze != null)
            {
                Vector3 haze = lightingCamera.ViewportToWorldPoint(new Vector3(0.08f, 0.99f, distance));
                haze.z = 0f;
                solarHaze.transform.position = haze;
                solarHaze.transform.localScale = Vector3.one * (8f * zoom);
            }
        }

        private void RespawnMote(Mote mote)
        {
            mote.Anchor = SampleFloor() + Vector2.up * Range(0.05f, 0.35f);
            mote.Drift = new Vector2(Range(0.008f, 0.030f), Range(0.010f, 0.035f));
            mote.Age = 0f;
            mote.Duration = Range(4.5f, 9f);
            mote.Rest = Range(1f, 3.5f);
            mote.Phase = Range(0f, Mathf.PI * 2f);
            // Keep most lights small, with occasional particles up to three times the original size.
            mote.Size = Range(0.035f, 0.070f) * Mathf.Lerp(1f, 3f, Mathf.Pow(Range(0f, 1f), 2f));
            float palette = Range(0f, 1f);
            mote.Size *= 0.70f;
            mote.Color = palette < 0.85f
                ? Color.Lerp(new Color(1f, 0.60f, 0.22f), new Color(1f, 0.91f, 0.65f), Range(0f, 1f))
                : Color.Lerp(new Color(0.40f, 0.58f, 1f), new Color(0.78f, 0.85f, 1f), Range(0f, 1f));
        }

        private Vector2 SampleFloor()
        {
            for (int attempt = 0; attempt < 48; attempt++)
            {
                Vector2 point = new Vector2(Range(floorMinimum.x, floorMaximum.x), Range(floorMinimum.y, floorMaximum.y));
                if (layout.ContainsWorld(point)) return point;
            }
            return layout.PixelToWorld(layout.floor[0]);
        }

        private SpriteRenderer CreateRenderer(Transform parent, string label, Sprite sprite, int sortingOrder)
        {
            GameObject node = new GameObject(label);
            node.transform.SetParent(parent, false);
            SpriteRenderer renderer = node.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = glowMaterial;
            renderer.sortingOrder = sortingOrder;
            renderer.color = Color.clear;
            return renderer;
        }

        private Sprite CreateSprite(string label, int shape)
        {
            const int size = 128;
            Texture2D texture = Own(new Texture2D(size, size, TextureFormat.RGBA32, false));
            texture.name = label;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float radius2 = nx * nx + ny * ny;
                float alpha = Mathf.Exp(-radius2 * 5.5f) * (1f - Mathf.SmoothStep(0.5f, 1f, radius2));
                if (shape == 1)
                {
                    float cross = Mathf.Exp(-Mathf.Abs(nx) * 24f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(ny)), 2f)
                        + Mathf.Exp(-Mathf.Abs(ny) * 24f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(nx)), 2f);
                    alpha = Mathf.Clamp01(alpha * 0.45f + cross);
                }
                else if (shape == 2)
                {
                    float width = Mathf.Lerp(0.50f, 0.95f, (1f - ny) * 0.5f);
                    alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(nx) / width), 2f)
                        * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(ny)), 0.75f);
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Own(Sprite.Create(texture, new Rect(0f, 0f, size, size),
                Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect));
            sprite.name = label;
            return sprite;
        }

        private float Range(float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        private T Own<T>(T asset) where T : Object
        {
            ownedAssets.Add(asset);
            return asset;
        }

        private void OnDestroy()
        {
            if (waterAtmosphere != null) waterAtmosphere.Dispose();
            foreach (Object asset in ownedAssets)
                if (asset != null) Destroy(asset);
        }
    }
}
