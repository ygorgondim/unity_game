using System.Collections.Generic;
using UnityEngine;
using WizardRoyale.Domain;

namespace WizardRoyale.Presentation
{
    /// <summary>
    /// Procedural spell visuals. Gameplay stays authoritative in MatchSimulation; this class only
    /// observes the public state and turns state transitions into short-lived local effects.
    /// </summary>
    internal sealed class SpellVfxPresentation
    {
        private const int MaximumParticles = 256;
        private const int LightningDashCount = 18;
        private const float LightningHalfWidth = 0.20f;

        private readonly Transform root;
        private readonly Transform particleRoot;
        private readonly Sprite circleSprite;
        private readonly Sprite ringSprite;
        private readonly Sprite squareSprite;
        private readonly Material lineMaterial;
        private readonly SpellParticleLibrary epic;
        private readonly MaterialPropertyBlock areaProperties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock slashProperties;
        private readonly Sprite slimeOrbSprite;
        private readonly Material slimeOrbMaterial;
        private readonly Sprite fireOrbSprite;
        private readonly Material fireOrbMaterial;
        private readonly Sprite waterOrbSprite;
        private readonly Material waterOrbMaterial;
        private readonly List<VfxParticle> particles = new List<VfxParticle>();
        private readonly Dictionary<int, ProjectileTrack> projectileTracks = new Dictionary<int, ProjectileTrack>();
        private readonly Dictionary<int, float> effectEmissionClocks = new Dictionary<int, float>();
        private int replacementParticleIndex;

        private sealed class ProjectileTrack
        {
            public ProjectileKind Kind;
            public WeaponType SourceWeapon;
            public Vector2 Position;
            public Vector2 Velocity;
            public int BouncesLeft;
            public bool IsArmed;
            public bool HasFired;
            public float TrailClock;
            public float SpecialClock;
            public bool LaunchPlayed;
            public float EpicTrailClock;
            public float EpicChargeClock;
            public float WhiteWaveClock;
        }

        private sealed class VfxParticle
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float StartSize;
            public float EndSize;
            public Color StartColor;
            public Color EndColor;
            public float Drag;
            public float Gravity;
            public float AngularVelocity;
        }

        public SpellVfxPresentation(Transform presentationRoot, Sprite circle,
            Sprite square, Material sharedLineMaterial, SpellParticleLibrary particleLibrary,
            Sprite slimeSprite = null, Material slimeMaterial = null,
            Sprite fireSprite = null, Material fireMaterial = null,
            Sprite waterSprite = null, Material waterMaterial = null)
        {
            root = presentationRoot;
            circleSprite = circle;
            ringSprite = CreateVfxRingSprite();
            squareSprite = square;
            lineMaterial = sharedLineMaterial;
            epic = particleLibrary;
            slashProperties = new MaterialPropertyBlock();
            slimeOrbSprite = slimeSprite;
            slimeOrbMaterial = slimeMaterial;
            fireOrbSprite = fireSprite;
            waterOrbSprite = waterSprite;
            waterOrbMaterial = waterMaterial;
            fireOrbMaterial = fireMaterial;
            particleRoot = new GameObject("Spell VFX Particles").transform;
            particleRoot.SetParent(root, false);
        }

        public Transform CreateProjectileView(ProjectileState projectile)
        {
            GameObject viewObject = new GameObject(projectile.Kind + " " + projectile.Id);
            viewObject.transform.SetParent(root, false);
            if (projectile.Kind == ProjectileKind.ShadowSlash)
            {
                viewObject.AddComponent<ShadowSlashView>().Initialize(lineMaterial, circleSprite, ringSprite);
                return viewObject.transform;
            }
            if (projectile.Kind == ProjectileKind.Slash)
            {
                CreateSpriteNode(viewObject.transform, "White Light Wave", epic.GlowSprite, 8).sharedMaterial = epic.LightWaveMaterial;
            }
            else if (projectile.Kind == ProjectileKind.Lightning)
                BuildLightningView(viewObject.transform);
            else if (projectile.SourceWeapon == WeaponType.Wind)
                CreateSpriteNode(viewObject.transform, "White Light Wave", epic.GlowSprite, 8).sharedMaterial = epic.LightWaveMaterial;
            else
                BuildMovingProjectileView(viewObject.transform, projectile.Kind);
            return viewObject.transform;
        }

        public void UpdateProjectileView(Transform view, ProjectileState projectile, float deltaTime)
        {
            if (projectile.Kind == ProjectileKind.ShadowSlash)
            {
                view.GetComponent<ShadowSlashView>().Sync(projectile);
                return;
            }
            ProjectileTrack track;
            if (!projectileTracks.TryGetValue(projectile.Id, out track))
            {
                track = new ProjectileTrack
                {
                    Kind = projectile.Kind,
                    SourceWeapon = projectile.SourceWeapon,
                    Position = projectile.Position,
                    Velocity = projectile.Velocity,
                    BouncesLeft = projectile.BouncesLeft,
                    IsArmed = projectile.IsArmed,
                    HasFired = false
                };
                projectileTracks.Add(projectile.Id, track);
            }

            if (projectile.Kind == ProjectileKind.Slash)
                UpdateSlashView(view, projectile, track, deltaTime);
            else if (projectile.Kind == ProjectileKind.Lightning)
                UpdateLightningView(view, projectile, track, deltaTime);
            else if (projectile.SourceWeapon == WeaponType.Wind)
                UpdateWindWaveView(view, projectile, track, deltaTime);
            else
                UpdateMovingProjectileView(view, projectile, track, deltaTime);

            if (projectile.Kind != ProjectileKind.Lightning && projectile.Kind != ProjectileKind.Slash)
            {
                float strength = projectile.IsArmed ? 1.3f : 1f;
                epic.Trail(projectile.SourceWeapon, track.Position, projectile.Position, projectile.Velocity,
                    ref track.EpicTrailClock, deltaTime, strength);
            }

            track.Position = projectile.Position;
            track.Velocity = projectile.Velocity;
            track.BouncesLeft = projectile.BouncesLeft;
            track.IsArmed = projectile.IsArmed;
            track.HasFired = projectile.HasFired;
        }

        private void UpdateSlashView(Transform view, ProjectileState projectile, ProjectileTrack track, float dt)
        {
            float age = projectile.MaxLife - projectile.Life;
            float fade = Mathf.Clamp01(projectile.Life / GameTuning.SlashFadeDuration);
            Vector2 direction = projectile.Velocity.normalized;
            Vector2 head = projectile.Position;
            UpdateLightWaveView(view, projectile, age, fade);

            if (!track.LaunchPlayed)
            {
                track.LaunchPlayed = true;
                SpawnRadialBurst(projectile.BeamStart, Color.white, 14,
                    0.025f, 0.38f, 0.12f, 0.24f);
            }
            if (age < GameTuning.SlashTravelDuration)
            {
                epic.WaveParticles(track.Position,head,direction,projectile.Radius,
                    ref track.EpicTrailClock,dt);
            }
        }

        private void UpdateWindWaveView(Transform view, ProjectileState projectile, ProjectileTrack track, float dt)
        {
            float age = projectile.MaxLife - projectile.Life;
            float fadeDuration = Mathf.Max(0.01f, Mathf.Min(0.12f, projectile.MaxLife * 0.15f));
            float fade = Mathf.Clamp01(projectile.Life / fadeDuration);
            UpdateLightWaveView(view, projectile, age, fade);
            epic.WaveParticles(track.Position, projectile.Position, projectile.Velocity, projectile.Radius,
                ref track.WhiteWaveClock, dt);
            // EpicTrailClock remains separate: UpdateProjectileView still emits the original
            // wind arcs and dust via Trail(WeaponType.Wind), in addition to these white particles.
            if (!track.LaunchPlayed)
            {
                track.LaunchPlayed = true;
                SpawnRadialBurst(projectile.Position, Color.white, 14, 0.025f, 0.38f, 0.12f, 0.24f);
            }
        }

        private void UpdateLightWaveView(Transform view, ProjectileState projectile, float age, float fade)
        {
            Vector2 direction = projectile.Velocity.normalized;
            view.position = new Vector3(projectile.Position.x, projectile.Position.y, -0.33f);
            view.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            view.localScale = new Vector3(projectile.Radius * 3.8f, projectile.Radius * 2.6f, 1f);
            SpriteRenderer wave = GetSprite(view, "White Light Wave");
            slashProperties.Clear();
            slashProperties.SetFloat("_Fade", Mathf.SmoothStep(0f, 1f, fade));
            slashProperties.SetFloat("_Age", age);
            slashProperties.SetFloat("_Phase", projectile.Id * 0.73f);
            wave.SetPropertyBlock(slashProperties);
        }

        public void ProjectileRemoved(int projectileId)
        {
            ProjectileTrack track;
            if (!projectileTracks.TryGetValue(projectileId, out track)) return;

            switch (track.Kind)
            {
                case ProjectileKind.Slash:
                    SpawnRadialBurst(track.Position, Color.white, 12,
                        0.025f, 0.30f, 0.10f, 0.22f);
                    break;
                case ProjectileKind.Energy:
                    SpawnRadialBurst(track.Position, epic.Profile(track.SourceWeapon).Color, 9, 0.04f, 0.28f, 0.16f, 0.28f);
                    break;
                case ProjectileKind.Slime:
                    SpawnRadialBurst(track.Position, SlimeColor(), 9, 0.06f, 0.24f, 0.14f, 0.26f);
                    break;
                case ProjectileKind.Explosive:
                    if (!track.IsArmed)
                        SpawnRadialBurst(track.Position, FireColor(), 7, 0.05f, 0.20f, 0.12f, 0.22f);
                    break;
            }

            projectileTracks.Remove(projectileId);
        }

        public Transform CreateEffectView(AreaEffectState effect)
        {
            GameObject viewObject = new GameObject(effect.Kind + " " + effect.Id);
            viewObject.transform.SetParent(root, false);

            int baseOrder = effect.Kind == AreaEffectKind.WaterPuddle ? 0 : 7;
            CreateSpriteNode(viewObject.transform, "Area Light", epic.GlowSprite, baseOrder).sharedMaterial = epic.AreaMaterial;
            CreateSpriteNode(viewObject.transform, "Flash", epic.GlowSprite, baseOrder + 1).sharedMaterial = epic.GlowMaterial;

            effectEmissionClocks[effect.Id] = 0f;
            if (effect.Kind == AreaEffectKind.Explosion)
            {
                epic.Impact(WeaponType.Explosive, effect.Position, Vector2.zero, 1.6f);
            }
            else
            {
                epic.Impact(WeaponType.Water, effect.Position, Vector2.zero, 0.85f);
            }
            return viewObject.transform;
        }

        public void UpdateEffectView(Transform view, AreaEffectState effect, float deltaTime)
        {
            view.position = new Vector3(effect.Position.x, effect.Position.y, -0.12f);
            float normalizedLife = effect.MaxLife > 0f ? Mathf.Clamp01(effect.Life / effect.MaxLife) : 0f;
            float progress = 1f - normalizedLife;
            float diameter = effect.Radius * 2f;

            SpriteRenderer area = GetSprite(view, "Area Light");
            SpriteRenderer flash = GetSprite(view, "Flash");
            // Keep the affected radius stable; the shimmer stays just inside its boundary.
            area.transform.localScale = Vector3.one * diameter;

            if (effect.Kind == AreaEffectKind.Explosion)
            {
                float fade = Mathf.SmoothStep(0f, 1f, normalizedLife);
                area.color = new Color(1f, 0.46f, 0.10f, 0.85f * fade);
                SetAreaLight(area, 0.045f, 0.22f);
                flash.transform.localScale = Vector3.one * Mathf.Lerp(diameter * 0.35f, diameter, progress);
                flash.color = new Color(1f, 0.78f, 0.40f, Mathf.Pow(normalizedLife, 2f) * 0.42f);
                return;
            }

            float expiryFade = Mathf.Clamp01(normalizedLife / 0.18f);
            area.color = new Color(0.13f, 0.77f, 1f, 0.9f * expiryFade);
            SetAreaLight(area, 0.075f, 0.25f);
            flash.transform.localScale = Vector3.one * (diameter * 0.75f);
            flash.color = new Color(0.35f, 0.86f, 1f, 0.08f * expiryFade);

            float clock;
            effectEmissionClocks.TryGetValue(effect.Id, out clock);
            clock += deltaTime;
            if (clock >= 0.22f)
            {
                epic.Ripple(effect.Position, effect.Radius, expiryFade);
                clock -= 0.22f;
                Vector2 offset = Random.insideUnitCircle * effect.Radius * 0.78f;
                SpawnParticle(effect.Position + offset, new Vector2(Random.Range(-0.05f, 0.05f), Random.Range(0.12f, 0.24f)),
                    Random.Range(0.035f, 0.065f), 0.012f, WaterColor(),
                    new Color(0.55f, 0.92f, 1f, 0f), 0.38f, 1.4f, -0.16f, 0f);
            }
            effectEmissionClocks[effect.Id] = clock;
        }

        private void SetAreaLight(SpriteRenderer renderer, float fill, float edge)
        {
            areaProperties.Clear();
            areaProperties.SetFloat("_Fill", fill);
            areaProperties.SetFloat("_Edge", edge);
            renderer.SetPropertyBlock(areaProperties);
        }

        public void EffectRemoved(int effectId)
        {
            effectEmissionClocks.Remove(effectId);
        }

        public void Tick(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            for (int i = 0; i < particles.Count; i++)
            {
                VfxParticle particle = particles[i];
                if (!particle.Transform.gameObject.activeSelf) continue;
                particle.Life -= dt;
                if (particle.Life <= 0f)
                {
                    particle.Transform.gameObject.SetActive(false);
                    continue;
                }

                float age = 1f - particle.Life / particle.MaxLife;
                particle.Velocity.y += particle.Gravity * dt;
                particle.Velocity *= Mathf.Exp(-particle.Drag * dt);
                Vector3 position = particle.Transform.position;
                position.x += particle.Velocity.x * dt;
                position.y += particle.Velocity.y * dt;
                particle.Transform.position = position;
                particle.Transform.Rotate(0f, 0f, particle.AngularVelocity * dt);
                float size = Mathf.Lerp(particle.StartSize, particle.EndSize, age);
                particle.Transform.localScale = Vector3.one * size;
                particle.Renderer.color = Color.Lerp(particle.StartColor, particle.EndColor, age);
            }
        }

        public void Reset()
        {
            projectileTracks.Clear();
            effectEmissionClocks.Clear();
            for (int i = 0; i < particles.Count; i++)
                particles[i].Transform.gameObject.SetActive(false);
        }

        private void BuildMovingProjectileView(Transform view, ProjectileKind kind)
        {
            CreateSpriteNode(view, "Aura", epic.GlowSprite, 5).sharedMaterial = epic.GlowMaterial;
            bool softOutline = kind == ProjectileKind.Explosive || kind == ProjectileKind.Water;
            SpriteRenderer outline = CreateSpriteNode(view, "Ring", softOutline ? epic.GlowSprite : ringSprite, 6);
            if (softOutline) outline.sharedMaterial = epic.GlowMaterial;
            CreateSpriteNode(view, "Core", kind == ProjectileKind.Slime || kind == ProjectileKind.Water
                ? epic.DropSprite : circleSprite, 7);
            CreateSpriteNode(view, "Shine", epic.StarSprite, 8).sharedMaterial = epic.GlowMaterial;
            CreateSpriteNode(view, "Ground Light", epic.GlowSprite, 1).sharedMaterial = epic.GlowMaterial;
            for (int i = 0; i < 5 && kind != ProjectileKind.Explosive; i++)
                CreateSpriteNode(view, "Element Detail " + i,
                    kind == ProjectileKind.Slime ? ringSprite : epic.StarSprite, 8).sharedMaterial = epic.GlowMaterial;

            if (kind == ProjectileKind.Explosive)
            {
                epic.CreateFireOverlay(view);
                SpriteRenderer warningArea = CreateSpriteNode(view, "Warning Fill", epic.GlowSprite, 4);
                warningArea.sharedMaterial = epic.AreaMaterial;
                warningArea.gameObject.SetActive(false);
                SpriteRenderer warningGlow = CreateSpriteNode(view, "Warning Ring", epic.GlowSprite, 4);
                warningGlow.sharedMaterial = epic.GlowMaterial;
                warningGlow.gameObject.SetActive(false);
                GameObject labelObject = new GameObject("Fuse Label");
                labelObject.transform.SetParent(view, false);
                labelObject.transform.localPosition = new Vector3(0f, 0.28f, 0f);
                TextMesh label = labelObject.AddComponent<TextMesh>();
                label.anchor = TextAnchor.LowerCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = 0.055f;
                label.fontSize = 32;
                label.fontStyle = FontStyle.Bold;
                label.color = Color.white;
                labelObject.GetComponent<MeshRenderer>().sortingOrder = 9;
                labelObject.SetActive(false);
            }
        }

        private void BuildLightningView(Transform view)
        {
            CreateSpriteNode(view, "Danger Corridor", squareSprite, 4);
            CreateSpriteNode(view, "Charge Progress", squareSprite, 5);
            CreateLineNode(view, "Top Edge", 6);
            CreateLineNode(view, "Bottom Edge", 6);

            GameObject dashes = new GameObject("Trajectory Dashes");
            dashes.transform.SetParent(view, false);
            for (int i = 0; i < LightningDashCount; i++)
                CreateSpriteNode(dashes.transform, "Dash " + i.ToString("00"), squareSprite, 6);

            CreateSpriteNode(view, "Caster Ring", ringSprite, 7);
            CreateSpriteNode(view, "Target Ring", ringSprite, 7);
            CreateSpriteNode(view, "Target Core", circleSprite, 8);
            CreateLineNode(view, "Outer Beam", 8);
            CreateLineNode(view, "Core Beam", 9);
            for (int i = 0; i < 4; i++)
                CreateLineNode(view, "Lightning Branch " + i, 8).positionCount = 6;
        }

        private void UpdateMovingProjectileView(Transform view, ProjectileState projectile,
            ProjectileTrack track, float deltaTime)
        {
            view.position = new Vector3(projectile.Position.x, projectile.Position.y, -0.3f);
            SpriteRenderer aura = GetSprite(view, "Aura");
            SpriteRenderer ring = GetSprite(view, "Ring");
            SpriteRenderer core = GetSprite(view, "Core");
            SpriteRenderer shine = GetSprite(view, "Shine");
            Color color = ColorFor(projectile.Kind);
            if (projectile.SourceWeapon == WeaponType.Wind) color = epic.Profile(WeaponType.Wind).Color;
            float diameter = projectile.Radius * 2f;
            float pulse = 1f + Mathf.Sin(Time.time * 10f + projectile.Id * 1.31f) * 0.10f;

            aura.transform.localScale = Vector3.one * (diameter * 3.2f * pulse);
            aura.color = WithAlpha(color, 0.32f);
            ring.transform.localScale = Vector3.one * (diameter * 1.25f);
            ring.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 130f + projectile.Id * 17f);
            ring.color = WithAlpha(Color.Lerp(color, Color.white, 0.34f), 0.88f);
            if (projectile.Kind == ProjectileKind.Explosive || projectile.Kind == ProjectileKind.Water)
                ring.color = WithAlpha(color, 0.18f);
            core.transform.localScale = Vector3.one * diameter;
            core.color = color;
            shine.transform.localPosition = new Vector3(-projectile.Radius * 0.27f, projectile.Radius * 0.31f, 0f);
            shine.color = new Color(1f, 1f, 1f, 0.88f);
            shine.transform.localScale = Vector3.one * (diameter * 0.55f);

            SpriteRenderer ground = GetSprite(view, "Ground Light");
            ground.transform.localPosition = new Vector3(0f, -0.14f, 0.1f);
            ground.transform.localScale = new Vector3(diameter * 4.5f, diameter * 2f, 1f);
            ground.color = WithAlpha(color, projectile.Kind == ProjectileKind.Explosive ? 0.26f : 0.12f);
            float heading = Mathf.Atan2(projectile.Velocity.y, projectile.Velocity.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 5 && projectile.Kind != ProjectileKind.Explosive; i++)
            {
                SpriteRenderer detail = GetSprite(view, "Element Detail " + i);
                float phase = Time.time * (10f + i) + projectile.Id + i * 2.1f;
                float angle = i * Mathf.PI * 2f / 5f + Time.time * 2f;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * projectile.Radius * 0.7f;
                detail.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
                detail.transform.localScale = Vector3.one * diameter * (0.14f + Mathf.Sin(phase) * 0.035f);
                detail.color = WithAlpha(Color.Lerp(color, Color.white, 0.7f), 0.65f);
            }
            if (projectile.SourceWeapon == WeaponType.Wind)
            {
                core.sprite = epic.WindSprite;
                core.sharedMaterial = epic.GlowMaterial;
                core.transform.localScale = new Vector3(diameter * 2.2f, diameter * 2.4f, 1f);
                core.transform.localRotation = Quaternion.Euler(0f, 0f, heading - 90f);
                core.color = new Color(0.6f, 0.95f, 0.9f, 0.75f);
                ring.sprite = epic.WindSprite;
                ring.sharedMaterial = epic.GlowMaterial;
                ring.transform.localScale = new Vector3(diameter * 2.8f, diameter * 2.7f, 1f);
                ring.transform.localRotation = Quaternion.Euler(0f, 0f, heading - 90f);
                ring.color = new Color(0.8f, 1f, 0.95f, 0.55f);
                shine.color = Color.clear;
            }

            if (projectile.Kind == ProjectileKind.Explosive)
                UpdateExplosiveView(view, projectile, track, deltaTime, aura, ring, core, shine);
            else if (projectile.Kind == ProjectileKind.Slime)
            {
                UpdateSlimeView(view, projectile, track);
                ring.color = new Color(0.5f, 1f, 0.42f, 0.3f);
            }
            else if (projectile.Kind == ProjectileKind.Water)
                UpdateWaterView(projectile, core, ring);

            if (projectile.SourceWeapon != WeaponType.Wind && projectile.Kind != ProjectileKind.Explosive)
                EmitProjectileTrail(projectile, track, deltaTime);
        }

        private void UpdateExplosiveView(Transform view, ProjectileState projectile, ProjectileTrack track,
            float deltaTime, SpriteRenderer aura, SpriteRenderer ring, SpriteRenderer core, SpriteRenderer shine)
        {
            SpriteRenderer warningFill = GetSprite(view, "Warning Fill");
            SpriteRenderer warningRing = GetSprite(view, "Warning Ring");
            Transform labelTransform = view.Find("Fuse Label");
            bool armed = projectile.IsArmed;
            float progress = armed ? 1f - Mathf.Clamp01(projectile.FuseTimer / GameTuning.ExplosiveFuse) : 0f;
            float dangerPulse = armed ? 0.96f + Mathf.Sin(Time.time * 12f) * 0.025f : 1f;
            float imageDiameter = projectile.Radius * 2f * dangerPulse;
            if (fireOrbSprite != null)
            {
                core.sprite = fireOrbSprite;
                if (fireOrbMaterial != null) core.sharedMaterial = fireOrbMaterial;
                core.transform.localScale = Vector3.one * (imageDiameter / Mathf.Max(0.001f, fireOrbSprite.bounds.size.x));
                core.color = Color.white;
            }
            // The reference-style fire ribbons replace the star flare and attached flame cards.
            aura.enabled = false;
            ring.enabled = false;
            shine.enabled = false;
            epic.UpdateFireOverlay(GetSprite(view, "Animated Fire Overlay"), imageDiameter,
                projectile.Id * 0.731f, progress);
            warningFill.gameObject.SetActive(armed);
            warningRing.gameObject.SetActive(armed);
            labelTransform.gameObject.SetActive(armed);
            if (!armed) return;

            warningFill.transform.localScale = Vector3.one * (GameTuning.ExplosionRadius * 2f);
            warningFill.color = new Color(1f, 0.40f, 0.08f, 0.65f + progress * 0.2f);
            SetAreaLight(warningFill, 0.045f, 0.20f + progress * 0.10f);
            warningRing.transform.localScale = Vector3.one * (GameTuning.ExplosionRadius * 1.3f);
            warningRing.color = new Color(1f, 0.33f, 0.04f, 0.08f + progress * 0.08f);
            if (fireOrbSprite == null)
            {
                core.transform.localScale = Vector3.one * imageDiameter;
                core.color = FireColor();
            }
            aura.transform.localScale = Vector3.one * (projectile.Radius * 3.2f * dangerPulse);
            aura.color = new Color(1f, 0.12f, 0.02f, 0.28f);
            ring.transform.localScale = Vector3.one * (projectile.Radius * 2.6f * dangerPulse);
            ring.color = new Color(1f, 0.72f, 0.10f, 0.18f);
            shine.transform.localScale = Vector3.one * (projectile.Radius * 0.55f);
            labelTransform.localPosition = new Vector3(0f, projectile.Radius + 0.13f, 0f);
            labelTransform.GetComponent<TextMesh>().text = projectile.FuseTimer.ToString("0.0") + "s";

            if (!track.IsArmed)
                SpawnRadialBurst(projectile.Position, FireColor(), 10, 0.04f, 0.18f, 0.12f, 0.24f);

        }

        private void UpdateSlimeView(Transform view, ProjectileState projectile, ProjectileTrack track)
        {
            float angle = Mathf.Atan2(projectile.Velocity.y, projectile.Velocity.x) * Mathf.Rad2Deg;
            float wobble = Mathf.Sin(Time.time * 13f + projectile.Id) * 0.13f;
            Transform core = view.Find("Core");
            core.localRotation = Quaternion.Euler(0f, 0f, angle);
            core.localScale = new Vector3(projectile.Radius * 2.25f * (1f + wobble),
                projectile.Radius * 1.78f * (1f - wobble), 1f);
            if (slimeOrbSprite != null)
            {
                SpriteRenderer renderer = core.GetComponent<SpriteRenderer>();
                renderer.sprite = slimeOrbSprite;
                if (slimeOrbMaterial != null) renderer.sharedMaterial = slimeOrbMaterial;
                renderer.color = Color.white;
                // Normalize the artwork's world size while retaining the existing jelly wobble.
                Vector3 size = core.localScale;
                core.localScale = new Vector3(size.x / slimeOrbSprite.bounds.size.x,
                    size.y / slimeOrbSprite.bounds.size.y, 1f);
            }

            bool bounced = projectile.BouncesLeft < track.BouncesLeft;
            if (!bounced && track.Velocity.sqrMagnitude > 0.01f && projectile.Velocity.sqrMagnitude > 0.01f)
                bounced = Vector2.Dot(track.Velocity.normalized, projectile.Velocity.normalized) < 0.55f;
            if (bounced)
            {
                epic.Impact(WeaponType.Slime, projectile.Position, projectile.Velocity.normalized, 0.65f);
                SpawnRadialBurst(projectile.Position, SlimeColor(), 7, 0.04f, 0.20f, 0.12f, 0.23f);
            }
        }

        private void UpdateWaterView(ProjectileState projectile, SpriteRenderer core, SpriteRenderer ring)
        {
            float wave = Mathf.Sin(Time.time * 12f + projectile.Id * 0.7f) * 0.08f;
            core.transform.localScale = new Vector3(projectile.Radius * 2.10f * (1f + wave),
                projectile.Radius * 1.90f * (1f - wave), 1f);
            core.color = WaterColor();
            if (waterOrbSprite != null)
            {
                core.sprite = waterOrbSprite;
                if (waterOrbMaterial != null) core.sharedMaterial = waterOrbMaterial;
                core.color = Color.white;
                Vector3 size = core.transform.localScale;
                core.transform.localScale = new Vector3(size.x / waterOrbSprite.bounds.size.x,
                    size.y / waterOrbSprite.bounds.size.y, 1f);
            }
            ring.transform.localScale = Vector3.one * (projectile.Radius * 2.55f);
            ring.color = new Color(0.40f, 0.90f, 1f, 0.18f);
        }

        private void EmitProjectileTrail(ProjectileState projectile, ProjectileTrack track, float deltaTime)
        {
            float interval;
            switch (projectile.Kind)
            {
                case ProjectileKind.Water: interval = 0.035f; break;
                case ProjectileKind.Slime: interval = 0.050f; break;
                default: interval = 0.045f; break;
            }

            track.TrailClock += deltaTime;
            if (track.TrailClock < interval) return;
            track.TrailClock -= interval;

            Vector2 backwards = projectile.Velocity.sqrMagnitude > 0.01f
                ? -projectile.Velocity.normalized * Random.Range(0.05f, 0.12f)
                : Vector2.zero;
            Vector2 velocity = backwards + Random.insideUnitCircle * 0.06f;
            Color color = ColorFor(projectile.Kind);
            Color end = WithAlpha(color, 0f);
            float startSize = Random.Range(0.035f, 0.065f);
            SpawnParticle(projectile.Position, velocity, startSize,
                projectile.Kind == ProjectileKind.Water ? startSize * 0.35f : 0.012f,
                WithAlpha(color, 0.82f), end, 0.26f, 2.4f,
                projectile.Kind == ProjectileKind.Water ? -0.12f : 0f, Random.Range(-180f, 180f));
        }

        private void UpdateLightningView(Transform view, ProjectileState projectile,
            ProjectileTrack track, float deltaTime)
        {
            Vector2 start = projectile.BeamStart;
            Vector2 end = projectile.BeamEnd;
            Vector2 delta = end - start;
            float length = Mathf.Max(0.001f, delta.magnitude);
            Vector2 direction = delta / length;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            bool charging = !projectile.HasFired;
            float progress = charging
                ? 1f - Mathf.Clamp01(projectile.ChargeTimer / GameTuning.LightningCharge)
                : 1f;

            SpriteRenderer corridor = GetSprite(view, "Danger Corridor");
            SpriteRenderer chargeFill = GetSprite(view, "Charge Progress");
            LineRenderer topEdge = GetLine(view, "Top Edge");
            LineRenderer bottomEdge = GetLine(view, "Bottom Edge");
            SpriteRenderer casterRing = GetSprite(view, "Caster Ring");
            SpriteRenderer targetRing = GetSprite(view, "Target Ring");
            SpriteRenderer targetCore = GetSprite(view, "Target Core");
            LineRenderer outerBeam = GetLine(view, "Outer Beam");
            LineRenderer coreBeam = GetLine(view, "Core Beam");
            Transform dashRoot = view.Find("Trajectory Dashes");

            corridor.gameObject.SetActive(charging);
            chargeFill.gameObject.SetActive(charging);
            topEdge.gameObject.SetActive(charging);
            bottomEdge.gameObject.SetActive(charging);
            dashRoot.gameObject.SetActive(charging);
            casterRing.gameObject.SetActive(charging);
            outerBeam.gameObject.SetActive(!charging);
            coreBeam.gameObject.SetActive(!charging);

            float targetPulse = 1f + Mathf.Sin(Time.time * 12.5f) * 0.13f;
            targetRing.transform.position = new Vector3(end.x, end.y, -0.31f);
            targetRing.transform.localScale = Vector3.one * (0.42f * targetPulse);
            targetRing.color = new Color(0.98f, 0.80f, 0.09f, charging ? 0.95f : 0.72f);
            targetCore.transform.position = new Vector3(end.x, end.y, -0.31f);
            targetCore.transform.localScale = Vector3.one * (charging ? 0.07f : 0.14f * targetPulse);
            targetCore.color = charging
                ? new Color(1f, 0.96f, 0.55f, 0.92f)
                : new Color(1f, 1f, 1f, 0.96f);

            if (charging)
            {
                epic.Charge(start, progress, ref track.EpicChargeClock, deltaTime);
                float pulseAlpha = 0.15f + Mathf.Sin(Time.time * 22f) * 0.055f;
                SetSpriteSegment(corridor.transform, start, end, LightningHalfWidth * 2f, -0.30f);
                corridor.color = new Color(0.98f, 0.80f, 0.08f, pulseAlpha);
                Vector2 fillEnd = Vector2.Lerp(start, end, progress);
                SetSpriteSegment(chargeFill.transform, start, fillEnd, LightningHalfWidth * 1.72f, -0.31f);
                chargeFill.color = new Color(1f, 0.91f, 0.36f, Mathf.Lerp(0.28f, 0.78f, progress));
                SetLine(topEdge, start + normal * LightningHalfWidth, end + normal * LightningHalfWidth,
                    new Color(0.98f, 0.80f, 0.08f, 0.92f), 0.022f);
                SetLine(bottomEdge, start - normal * LightningHalfWidth, end - normal * LightningHalfWidth,
                    new Color(0.98f, 0.80f, 0.08f, 0.92f), 0.022f);

                bool whiteFlash = Mathf.Sin(Time.time * 33f) > 0f;
                for (int i = 0; i < LightningDashCount; i++)
                {
                    Transform dash = dashRoot.GetChild(i);
                    float along = (i + 0.5f) / LightningDashCount;
                    Vector2 center = Vector2.Lerp(start, end, along);
                    dash.position = new Vector3(center.x, center.y, -0.32f);
                    dash.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                    dash.localScale = new Vector3(Mathf.Min(0.16f, length / LightningDashCount * 0.58f), 0.026f, 1f);
                    dash.GetComponent<SpriteRenderer>().color = whiteFlash
                        ? new Color(1f, 1f, 1f, 0.95f)
                        : new Color(1f, 0.94f, 0.50f, 0.92f);
                }

                casterRing.transform.position = new Vector3(start.x, start.y, -0.31f);
                casterRing.transform.localScale = Vector3.one * (0.66f + Mathf.Sin(Time.time * 14f) * 0.05f);
                casterRing.color = new Color(0.98f, 0.80f, 0.08f, 0.90f);

                track.SpecialClock += deltaTime;
                if (track.SpecialClock >= 0.025f)
                {
                    track.SpecialClock -= 0.025f;
                    Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(0.18f, 0.31f);
                    SpawnParticle(start + offset, -offset.normalized * Random.Range(0.25f, 0.48f),
                        Random.Range(0.025f, 0.055f), 0.008f, LightningColor(),
                        new Color(0.32f, 0.82f, 1f, 0f), 0.16f, 1.2f, 0f, Random.Range(-240f, 240f));
                }
                for (int i = 0; i < 4; i++) GetLine(view, "Lightning Branch " + i).gameObject.SetActive(false);
                return;
            }

            SetLine(outerBeam, start, end, new Color(1f, 0.68f, 0.10f, 0.98f), 0.12f);
            SetLine(coreBeam, start, end, Color.white, 0.032f);
            float fade = Mathf.Clamp01(projectile.Life / GameTuning.LightningVisualDuration);
            outerBeam.startColor = outerBeam.endColor = new Color(1f, 0.68f, 0.10f, fade);
            coreBeam.startColor = coreBeam.endColor = new Color(1f, 0.98f, 0.85f, fade);
            outerBeam.positionCount = coreBeam.positionCount = 23;
            for (int i = 0; i < coreBeam.positionCount; i++)
            {
                float t = i / (float)(coreBeam.positionCount - 1);
                float jitter = (Mathf.PerlinNoise(i * 2.13f + projectile.Id, Mathf.Floor(Time.time * 45f)) - 0.5f)
                    * 0.16f * Mathf.Sin(t * Mathf.PI);
                Vector2 point = Vector2.Lerp(start, end, t) + normal * jitter;
                Vector3 p = new Vector3(point.x, point.y, -0.33f);
                outerBeam.SetPosition(i, p);
                coreBeam.SetPosition(i, p);
            }
            for (int branchIndex = 0; branchIndex < 4; branchIndex++)
            {
                LineRenderer branch = GetLine(view, "Lightning Branch " + branchIndex);
                branch.gameObject.SetActive(true);
                branch.startWidth = 0.024f;
                branch.endWidth = 0f;
                branch.startColor = new Color(1f, 0.78f, 0.25f, fade * 0.9f);
                branch.endColor = new Color(0.8f, 0.65f, 1f, 0f);
                Vector2 origin = Vector2.Lerp(start, end, 0.18f + branchIndex * 0.21f);
                for (int i = 0; i < branch.positionCount; i++)
                {
                    float t = i / (float)(branch.positionCount - 1);
                    Vector2 p = origin + direction * t * 0.24f + normal * ((branchIndex % 2 == 0 ? 1f : -1f)
                        * t * 0.34f + Mathf.Sin(i * 7f + projectile.Id) * t * 0.08f);
                    branch.SetPosition(i, new Vector3(p.x, p.y, -0.33f));
                }
            }
            if (!track.HasFired)
            {
                epic.Lightning(start, end);
                for (int i = 0; i <= 18; i++)
                {
                    Vector2 point = Vector2.Lerp(start, end, i / 18f) + Random.insideUnitCircle * 0.12f;
                    Color spark = Random.value < 0.58f ? new Color(1f, 0.95f, 0.46f) : new Color(0.25f, 0.80f, 1f);
                    SpawnParticle(point, Random.insideUnitCircle * Random.Range(0.18f, 0.55f),
                        Random.Range(0.025f, 0.065f), 0.008f, spark, WithAlpha(spark, 0f),
                        0.28f, 1.8f, 0f, Random.Range(-360f, 360f));
                }
                SpawnRadialBurst(end, LightningColor(), 14, 0.035f, 0.22f, 0.18f, 0.32f);
            }
        }

        private void SpawnRadialBurst(Vector2 position, Color color, int count,
            float minimumSize, float maximumSpeed, float minimumLife, float maximumLife)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = Random.Range(maximumSpeed * 0.35f, maximumSpeed);
                float size = Random.Range(minimumSize, minimumSize * 1.75f);
                Color start = WithAlpha(color, Random.Range(0.78f, 1f));
                SpawnParticle(position, direction * speed, size, size * 0.18f,
                    start, WithAlpha(color, 0f), Random.Range(minimumLife, maximumLife),
                    1.35f, -0.18f, Random.Range(-280f, 280f));
            }
        }

        private void SpawnParticle(Vector2 position, Vector2 velocity, float startSize, float endSize,
            Color startColor, Color endColor, float life, float drag, float gravity, float angularVelocity)
        {
            VfxParticle particle = AcquireParticle();
            particle.Transform.gameObject.SetActive(true);
            particle.Transform.position = new Vector3(position.x, position.y, -0.34f);
            particle.Transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            particle.Transform.localScale = Vector3.one * startSize;
            particle.Renderer.color = startColor;
            particle.Velocity = velocity;
            particle.Life = particle.MaxLife = Mathf.Max(0.01f, life);
            particle.StartSize = startSize;
            particle.EndSize = endSize;
            particle.StartColor = startColor;
            particle.EndColor = endColor;
            particle.Drag = drag;
            particle.Gravity = gravity;
            particle.AngularVelocity = angularVelocity;
        }

        private VfxParticle AcquireParticle()
        {
            for (int i = 0; i < particles.Count; i++)
                if (!particles[i].Transform.gameObject.activeSelf)
                    return particles[i];

            if (particles.Count < MaximumParticles)
            {
                GameObject particleObject = new GameObject("Pooled Spell Particle " + particles.Count);
                particleObject.transform.SetParent(particleRoot, false);
                SpriteRenderer renderer = particleObject.AddComponent<SpriteRenderer>();
                renderer.sprite = circleSprite;
                renderer.sortingOrder = 10;
                VfxParticle particle = new VfxParticle
                {
                    Transform = particleObject.transform,
                    Renderer = renderer
                };
                particles.Add(particle);
                return particle;
            }

            VfxParticle replacement = particles[replacementParticleIndex];
            replacementParticleIndex = (replacementParticleIndex + 1) % particles.Count;
            return replacement;
        }

        private SpriteRenderer CreateSpriteNode(Transform parent, string name, Sprite sprite, int sortingOrder)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            SpriteRenderer renderer = node.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private LineRenderer CreateLineNode(Transform parent, string name, int sortingOrder)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            LineRenderer line = node.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.sortingOrder = sortingOrder;
            line.sharedMaterial = name.Contains("Branch") || name.Contains("Core")
                ? epic.GlowMaterial : lineMaterial;
            return line;
        }

        private static void SetSpriteSegment(Transform transform, Vector2 start, Vector2 end, float width, float z)
        {
            Vector2 delta = end - start;
            float length = Mathf.Max(0.001f, delta.magnitude);
            Vector2 center = (start + end) * 0.5f;
            transform.position = new Vector3(center.x, center.y, z);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            transform.localScale = new Vector3(length, width, 1f);
        }

        private static void SetLine(LineRenderer line, Vector2 start, Vector2 end, Color color, float width)
        {
            line.SetPosition(0, new Vector3(start.x, start.y, -0.32f));
            line.SetPosition(1, new Vector3(end.x, end.y, -0.32f));
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width;
        }

        private static SpriteRenderer GetSprite(Transform rootTransform, string path)
        {
            return rootTransform.Find(path).GetComponent<SpriteRenderer>();
        }

        private static LineRenderer GetLine(Transform rootTransform, string path)
        {
            return rootTransform.Find(path).GetComponent<LineRenderer>();
        }

        private static Color ColorFor(ProjectileKind kind)
        {
            switch (kind)
            {
                case ProjectileKind.Explosive: return FireColor();
                case ProjectileKind.Slime: return SlimeColor();
                case ProjectileKind.Water: return WaterColor();
                case ProjectileKind.Lightning: return LightningColor();
                default: return EnergyColor();
            }
        }

        private static Color EnergyColor()
        {
            return WeaponCatalog.Get(WeaponType.Wind).Color;
        }

        private static Color FireColor()
        {
            return new Color(0.98f, 0.31f, 0.06f);
        }

        private static Color SlimeColor()
        {
            return new Color(0.13f, 0.77f, 0.37f);
        }

        private static Color WaterColor()
        {
            return new Color(0.02f, 0.71f, 0.90f);
        }

        private static Color LightningColor()
        {
            return new Color(0.98f, 0.80f, 0.08f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Sprite CreateVfxRingSprite()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Runtime Spell VFX Ring";
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float outerRadius = size * 0.48f;
            float innerRadius = size * 0.405f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float outerAlpha = Mathf.Clamp01(outerRadius - distance);
                float innerAlpha = Mathf.Clamp01(distance - innerRadius);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Min(outerAlpha, innerAlpha));
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }
    }
}
