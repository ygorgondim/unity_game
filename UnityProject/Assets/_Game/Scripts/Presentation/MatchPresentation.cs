using System.Collections.Generic;
using UnityEngine;
using WizardRoyale.Domain;
using WizardRoyale.Simulation;

namespace WizardRoyale.Presentation
{
    public sealed class MatchPresentation
    {
        private const float CameraPaddingX = 2.60f;
        private const float CameraPaddingY = 2.10f;
        private const float CameraMinimumSize = 2.95f;
        private const float CameraMaximumSize = 7.25f;
        private const float CameraBoundaryMargin = 1.20f;
        private const float CameraFollowSharpness = 1.52f;
        private const float CameraZoomSharpness = 1.21f;
        private const float HitFlashDuration = 0.20f;
        private const float PickupVisualScale = 0.70f;
        private const float PickupFloatAmplitude = 0.04f;

        private readonly Transform root;
        private readonly Camera camera;
        private readonly Dictionary<int, Transform> playerViews = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> projectileViews = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> effectViews = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> pickupViews = new Dictionary<int, Transform>();
        private readonly Dictionary<int, PlayerVisualState> playerVisualStates = new Dictionary<int, PlayerVisualState>();
        private readonly Dictionary<WeaponType, Sprite> pickupOrbSprites;
        private readonly Sprite circleSprite;
        private readonly Sprite ringSprite;
        private readonly Sprite arrowHeadSprite;
        private readonly Sprite squareSprite;
        private readonly Sprite pickupGlowSprite;
        private readonly WizardAnimationSet wizardAnimation;
        private readonly WizardAnimationSet floatingWizardAnimation;
        private readonly WizardAnimationSet darkFloatingWizardAnimation;
        private readonly WizardAnimationSet leafFloatingWizardAnimation;
        private readonly WizardAnimationSet autumnFloatingWizardAnimation;
        private readonly Material lineMaterial;
        private readonly Material hitSilhouetteMaterial;
        private readonly Dictionary<WeaponType, Material> pickupOrbMaterials = new Dictionary<WeaponType, Material>();
        private readonly SpellVfxPresentation spellVfx;
        private readonly ArenaAmbiencePresentation ambience;
        private readonly CharacterIdentifiers identifiers;
        private readonly FloatingCharacterPresentation floatingCharacters;
        private readonly RecoveryWindPresentation recoveryWind;
        private readonly RespawnPresentation respawns;
        private readonly OrbitAbilityPresentation orbitAbilities;
        private readonly SpellParticleLibrary epicParticles;
        private long lastImpactSequence;

        public MatchPresentation(Camera targetCamera, MatchSimulation simulation)
        {
            camera = targetCamera;
            root = new GameObject("Runtime Presentation").transform;
            circleSprite = CreateCircleSprite();
            pickupGlowSprite = CreatePickupGlowSprite();
            ringSprite = CreateRingSprite();
            arrowHeadSprite = CreateArrowHeadSprite();
            squareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);
            wizardAnimation = LoadWizardAnimation();
            floatingWizardAnimation = LoadFloatingWizardAnimation();
            darkFloatingWizardAnimation = LoadFloatingWizardAnimation("Art/DarkFloatingWizard/Idle/idle_");
            leafFloatingWizardAnimation = LoadFloatingWizardAnimation("Art/LeafFloatingWizard/Idle/idle_");
            autumnFloatingWizardAnimation = LoadFloatingWizardAnimation("Art/AutumnFloatingWizard/Idle/idle_");
            pickupOrbSprites = LoadPickupOrbSprites();
            Shader pickupShader = Resources.Load<Shader>("Shaders/PickupOrb");
            if (pickupShader != null)
            {
                foreach (KeyValuePair<WeaponType, Sprite> pair in pickupOrbSprites)
                {
                    Material material = new Material(pickupShader);
                    material.name = "Pickup Orb - " + pair.Key;
                    material.mainTexture = pair.Value.texture;
                    material.SetTexture("_OrbTex", pair.Value.texture);
                    pickupOrbMaterials.Add(pair.Key, material);
                }
            }
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            GameObject particleLibrary = new GameObject("Spell Particle Library");
            particleLibrary.transform.SetParent(root, false);
            epicParticles = particleLibrary.AddComponent<SpellParticleLibrary>();
            epicParticles.Initialize();
            Sprite orbitalSprite;
            if (!pickupOrbSprites.TryGetValue(WeaponType.Orbit, out orbitalSprite))
                orbitalSprite = OrbitAbilityPresentation.CreateOrbSprite();
            Material orbitalMaterial;
            pickupOrbMaterials.TryGetValue(WeaponType.Orbit, out orbitalMaterial);
            orbitAbilities = new OrbitAbilityPresentation(root, orbitalSprite,
                pickupGlowSprite, lineMaterial, epicParticles, orbitalMaterial);
            Shader hitSilhouetteShader = Resources.Load<Shader>("Shaders/WhiteSilhouette");
            if (hitSilhouetteShader != null)
                hitSilhouetteMaterial = new Material(hitSilhouetteShader);
            Sprite slimeSprite;
            Material slimeMaterial;
            pickupOrbSprites.TryGetValue(WeaponType.Slime, out slimeSprite);
            pickupOrbMaterials.TryGetValue(WeaponType.Slime, out slimeMaterial);
            Sprite fireSprite;
            Material fireMaterial;
            pickupOrbSprites.TryGetValue(WeaponType.Explosive, out fireSprite);
            pickupOrbMaterials.TryGetValue(WeaponType.Explosive, out fireMaterial);
            Sprite waterSprite;
            Material waterMaterial;
            pickupOrbSprites.TryGetValue(WeaponType.Water, out waterSprite);
            pickupOrbMaterials.TryGetValue(WeaponType.Water, out waterMaterial);
            spellVfx = new SpellVfxPresentation(root, circleSprite, squareSprite, lineMaterial, epicParticles,
                slimeSprite, slimeMaterial, fireSprite, fireMaterial, waterSprite, waterMaterial);
            identifiers = new CharacterIdentifiers(camera, arrowHeadSprite, pickupGlowSprite);
            floatingCharacters = new FloatingCharacterPresentation(pickupGlowSprite);
            recoveryWind = new RecoveryWindPresentation(root, pickupGlowSprite, circleSprite, lineMaterial);
            respawns = new RespawnPresentation(root, ringSprite, pickupGlowSprite, circleSprite);
            BuildArena(simulation);
            GameObject ambienceObject = new GameObject("Golden Arena - Sunset Ambience");
            ambienceObject.transform.SetParent(root, false);
            ambience = ambienceObject.AddComponent<ArenaAmbiencePresentation>();
            ambience.Initialize(camera);
        }

        public void Rebuild(MatchSimulation simulation)
        {
            spellVfx.Reset();
            epicParticles.ResetEffects();
            lastImpactSequence = simulation.ImpactSequence;
            identifiers.Reset();
            floatingCharacters.Reset();
            recoveryWind.Reset();
            respawns.Reset();
            orbitAbilities.Reset();
            ambience.ResetCharacterLights();
            foreach (Transform view in playerViews.Values)
                if (view != null) Object.Destroy(view.gameObject);
            foreach (Transform view in projectileViews.Values)
                if (view != null) Object.Destroy(view.gameObject);
            foreach (Transform view in effectViews.Values)
                if (view != null) Object.Destroy(view.gameObject);
            foreach (Transform view in pickupViews.Values)
                if (view != null) Object.Destroy(view.gameObject);
            playerViews.Clear();
            playerVisualStates.Clear();
            projectileViews.Clear();
            effectViews.Clear();
            pickupViews.Clear();

            foreach (PlayerState player in simulation.Players)
                playerViews[player.Id] = CreatePlayerView(player);
        }

        public void Sync(MatchSimulation simulation, float deltaTime)
        {
            epicParticles.BeginFrame(deltaTime);
            foreach (SpellImpact impact in simulation.SpellImpacts)
            {
                if (impact.Sequence <= lastImpactSequence) continue;
                epicParticles.Impact(impact.Weapon, impact.Position, impact.Direction,
                    impact.Weapon == WeaponType.Explosive ? 0.45f : 0.85f);
                lastImpactSequence = impact.Sequence;
            }
            spellVfx.Tick(deltaTime);
            recoveryWind.Tick(deltaTime);
            foreach (PlayerState player in simulation.Players)
            {
                Transform view = playerViews[player.Id];
                view.gameObject.SetActive(!player.IsEliminated && player.RespawnTimer <= 0f);
                view.position = new Vector3(player.Position.x, player.Position.y, -0.2f);
                float scale = player.IsFalling ? player.FallScale : 1f;
                if (player.IsSpawning)
                    scale = Mathf.Lerp(0.08f, 1f, Mathf.SmoothStep(0f, 1f,
                        1f - player.SpawnTimer / GameTuning.SpawnDuration));
                view.localScale = Vector3.one * scale;
                orbitAbilities.Sync(player, deltaTime);
                ambience.SyncCharacterLight(player, epicParticles);
                Transform wizard = view.Find("Wizard");
                if (wizard != null)
                {
                    SpriteRenderer wizardRenderer = wizard.GetComponent<SpriteRenderer>();
                    bool sinking = player.IsFalling
                        && (!player.IsDeathLaunched || !simulation.IsInsideArena(player.Position));
                    wizardRenderer.sortingOrder = sinking ? -15 : 3;
                    ArenaDepth.Set(wizard, player.Position.y, sinking);
                    bool isMoving = UpdateWizardAnimation(player, wizardRenderer, deltaTime);
                    floatingCharacters.Sync(player, isMoving);
                    respawns.Sync(player, wizardRenderer);
                    recoveryWind.Sync(player, wizardRenderer, deltaTime);
                    UpdateHitFeedback(player, view, wizardRenderer, deltaTime);
                    identifiers.Sync(player, wizardRenderer);
                }
                Transform indicator = view.Find("Aim Indicator");
                if (indicator != null) indicator.gameObject.SetActive(!player.IsUnavailable);
                Transform aimPivot = view.Find("Aim Indicator/Aim Pivot");
                if (aimPivot != null)
                {
                    aimPivot.right = player.Aim;
                    Transform head = aimPivot.Find("Arrow Head");
                    if (head != null)
                    {
                        Color spellColor = WeaponCatalog.Get(player.Weapon).Color;
                        spellColor.a = player.IsBot ? 0.82f : 1f;
                        head.GetComponent<SpriteRenderer>().color = spellColor;
                    }
                }
            }

            HashSet<int> active = new HashSet<int>();
            foreach (PickupState pickup in simulation.Pickups)
            {
                active.Add(pickup.Id);
                Transform view;
                if (!pickupViews.TryGetValue(pickup.Id, out view))
                {
                    view = CreatePickupView(pickup);
                    pickupViews[pickup.Id] = view;
                }
                float phase = Time.time * 3.5f + pickup.Id * 1.71f;
                view.position = new Vector3(pickup.Position.x,
                    pickup.Position.y + Mathf.Sin(phase) * PickupFloatAmplitude, -0.18f);
                view.localScale = Vector3.one * (PickupVisualScale * (0.96f + Mathf.Sin(phase * 1.35f) * 0.04f));
                ArenaDepth.Set(view, pickup.Position.y);
                float fade = Mathf.Clamp01(pickup.Life / 1.5f);
                view.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, fade);
                SpriteRenderer aura = view.Find("Aura").GetComponent<SpriteRenderer>();
                Color auraColor = PickupGlowColor(pickup.Weapon);
                auraColor.a = (0.70f + Mathf.Sin(phase) * 0.08f) * fade;
                aura.color = auraColor;

                // Cancel the orb's bobbing and pulsing so the shadow stays anchored to the ground.
                float heightBlend = (Mathf.Sin(phase) + 1f) * 0.5f;
                Transform shadow = view.Find("Ground Shadow");
                shadow.position = new Vector3(pickup.Position.x, pickup.Position.y - 0.18f, -0.17f);
                shadow.localScale = new Vector3(
                    Mathf.Lerp(0.28f, 0.33f, heightBlend) / view.localScale.x,
                    Mathf.Lerp(0.09f, 0.115f, heightBlend) / view.localScale.y, 1f);
                shadow.GetComponent<SpriteRenderer>().color = new Color(0.08f, 0.05f, 0.10f,
                    Mathf.Lerp(0.32f, 0.24f, heightBlend) * fade);
            }

            List<int> stale = new List<int>();
            foreach (KeyValuePair<int, Transform> pair in pickupViews)
            {
                if (active.Contains(pair.Key)) continue;
                Object.Destroy(pair.Value.gameObject);
                stale.Add(pair.Key);
            }
            foreach (int id in stale) pickupViews.Remove(id);

            active.Clear();
            foreach (ProjectileState projectile in simulation.Projectiles)
            {
                active.Add(projectile.Id);
                Transform view;
                if (!projectileViews.TryGetValue(projectile.Id, out view))
                {
                    view = CreateProjectileView(projectile);
                    projectileViews[projectile.Id] = view;
                }
                UpdateProjectileView(view, projectile, deltaTime);
            }

            stale.Clear();
            foreach (KeyValuePair<int, Transform> pair in projectileViews)
            {
                if (active.Contains(pair.Key)) continue;
                spellVfx.ProjectileRemoved(pair.Key);
                Object.Destroy(pair.Value.gameObject);
                stale.Add(pair.Key);
            }
            foreach (int id in stale) projectileViews.Remove(id);

            active.Clear();
            foreach (AreaEffectState effect in simulation.AreaEffects)
            {
                active.Add(effect.Id);
                Transform view;
                if (!effectViews.TryGetValue(effect.Id, out view))
                {
                    view = CreateEffectView(effect);
                    effectViews[effect.Id] = view;
                }
                spellVfx.UpdateEffectView(view, effect, deltaTime);
            }

            stale.Clear();
            foreach (KeyValuePair<int, Transform> pair in effectViews)
            {
                if (active.Contains(pair.Key)) continue;
                spellVfx.EffectRemoved(pair.Key);
                Object.Destroy(pair.Value.gameObject);
                stale.Add(pair.Key);
            }
            foreach (int id in stale) effectViews.Remove(id);

            foreach (PlayerState player in simulation.Players)
            {
                if (player.IsEliminated) continue;
                Transform wizard = playerViews[player.Id].Find("Wizard");
                if (wizard != null) epicParticles.Illuminate(wizard.GetComponent<SpriteRenderer>(),
                    !player.IsFalling && !player.IsShadowForm);
            }

            UpdateCamera(simulation, deltaTime);
            ambience.Tick(deltaTime);
        }

        private void BuildArena(MatchSimulation simulation)
        {
            if (GoldenArenaLayout.Current.swamp)
            {
                Transform swamp = new GameObject("Swamp Arena Layers").transform;
                swamp.SetParent(root, false);
                SwampArenaPresentation.Build(swamp, GoldenArenaLayout.Current);
                camera.orthographic = true;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.backgroundColor = new Color(0.10f, 0.20f, 0.18f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                FrameWholeArena();
                return;
            }
            GameObject geometry = new GameObject("Golden Arena Layers");
            geometry.transform.SetParent(root, false);
            CreateArenaLayer(geometry.transform, "Ocean - Fall Zone", "ocean", -30, true);
            CreateArenaLayer(geometry.transform, "Arena - Decorative Cliffs", "arena", -10, false);

            // A trigger exposes the exact walkable contour for inspection and future physics users.
            // MatchSimulation uses this same layout, since characters move without Rigidbody2D.
            GameObject floor = new GameObject("Walkable Top - Rocks Excluded");
            floor.transform.SetParent(geometry.transform, false);
            PolygonCollider2D floorTrigger = floor.AddComponent<PolygonCollider2D>();
            floorTrigger.isTrigger = true;
            GoldenArenaLayout layout = GoldenArenaLayout.Current;
            Vector2[] points = new Vector2[layout.floor.Length];
            for (int i = 0; i < points.Length; i++) points[i] = layout.PixelToWorld(layout.floor[i]);
            floorTrigger.points = points;

            for (int i = 0; i < simulation.Obstacles.Count; i++)
                CreateObstacleGeometry(geometry.transform, simulation.Obstacles[i], i + 1);

            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.01f, 0.42f, 0.82f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            FrameWholeArena();
        }

        private void CreateArenaLayer(Transform parent, string name, string assetName, int sortingOrder, bool ocean)
        {
            GoldenArenaLayout layout = GoldenArenaLayout.Current;
            Texture2D texture = Resources.Load<Texture2D>("Art/GoldenArena/" + assetName);
            if (texture == null) throw new System.InvalidOperationException("Missing arena layer: " + assetName);
            GameObject layer = new GameObject(name);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = layout.ArtCenter;
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                Vector2.one * 0.5f, texture.width / (layout.width / layout.pixelsPerUnit),
                0, SpriteMeshType.FullRect);
            renderer.sortingOrder = sortingOrder;
            if (ocean)
            {
                // Alternating mirrored copies meet at identical edges, avoiding visible repeat seams.
                Vector2 size = renderer.sprite.bounds.size;
                for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                {
                    if (x == 0 && y == 0) continue;
                    GameObject tile = new GameObject("Ocean Tile " + x + "," + y);
                    tile.transform.SetParent(layer.transform, false);
                    tile.transform.localPosition = new Vector3(x * size.x, y * size.y, 0f);
                    SpriteRenderer tileRenderer = tile.AddComponent<SpriteRenderer>();
                    tileRenderer.sprite = renderer.sprite;
                    tileRenderer.sortingOrder = sortingOrder;
                    tileRenderer.flipX = Mathf.Abs(x) % 2 == 1;
                    tileRenderer.flipY = Mathf.Abs(y) % 2 == 1;
                }
            }
        }

        private void CreateObstacleGeometry(Transform parent, CircleObstacle obstacle, int index)
        {
            GameObject view = new GameObject("Obstacle O" + index);
            view.transform.SetParent(parent, false);
            view.transform.localPosition = new Vector3(obstacle.Center.x, obstacle.Center.y, 0f);

            GameObject fill = new GameObject("Collision Fill");
            fill.transform.SetParent(view.transform, false);
            fill.transform.localScale = Vector3.one * (obstacle.Radius * 2f);
            SpriteRenderer fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = circleSprite;
            fillRenderer.color = new Color(1f, 0.25f, 0.07f, 0.28f);
            fillRenderer.sortingOrder = -3;

            const int segments = 64;
            Vector2[] circle = new Vector2[segments + 1];
            for (int segment = 0; segment <= segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                circle[segment] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * obstacle.Radius;
            }
            CreateLine(view.transform, "Exact Collision Radius", circle,
                new Color(1f, 0.34f, 0.08f, 1f), 0.045f, -2, false);
            CreateLine(view.transform, "Center Horizontal",
                new[] { new Vector2(-0.09f, 0f), new Vector2(0.09f, 0f) },
                Color.white, 0.025f, -1, false);
            CreateLine(view.transform, "Center Vertical",
                new[] { new Vector2(0f, -0.09f), new Vector2(0f, 0.09f) },
                Color.white, 0.025f, -1, false);

            string label = string.Format("O{0}  ({1:+0.00;-0.00;0.00}, {2:+0.00;-0.00;0.00})  r={3:0.00}",
                index, obstacle.Center.x, obstacle.Center.y, obstacle.Radius);
            CreateWorldLabel(view.transform, "Collision Label", label,
                new Vector2(0f, obstacle.Radius + 0.10f), TextAnchor.LowerCenter,
                new Color(1f, 0.70f, 0.42f), 2, 0.060f);
        }

        private LineRenderer CreateLine(Transform parent, string name, IReadOnlyList<Vector2> points,
            Color color, float width, int sortingOrder, bool useWorldSpace)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = useWorldSpace;
            line.positionCount = points.Count;
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sortingOrder = sortingOrder;
            line.material = lineMaterial;
            for (int i = 0; i < points.Count; i++)
                line.SetPosition(i, new Vector3(points[i].x, points[i].y, 0f));
            return line;
        }

        private static void CreateWorldLabel(Transform parent, string name, string text, Vector2 position,
            TextAnchor anchor, Color color, int sortingOrder, float characterSize)
        {
            GameObject labelObject = new GameObject(name);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(position.x, position.y, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = anchor;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 32;
            label.fontStyle = FontStyle.Bold;
            label.color = color;
            labelObject.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
        }

        private Transform CreatePlayerView(PlayerState player)
        {
            GameObject view = new GameObject(player.Name);
            view.transform.SetParent(root);

            GameObject body = new GameObject("Wizard");
            body.transform.SetParent(view.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            WizardAnimationSet animation = AnimationFor(player);
            Sprite initialSprite = animation != null ? animation.FrontFrame : null;
            bodyRenderer.sprite = initialSprite != null ? initialSprite : circleSprite;
            if (initialSprite != null)
                body.transform.localPosition = animation.HasFootPivot
                    ? Vector3.zero : Vector3.up * -initialSprite.bounds.min.y;
            bodyRenderer.sortingOrder = 3;
            if (initialSprite == null)
            {
                bodyRenderer.color = player.Color;
                body.transform.localScale = Vector3.one * (0.55f * GameTuning.CharacterScale);
            }

            playerVisualStates[player.Id] = new PlayerVisualState(player.Position,
                ResolveFacing(player.LastMove), player.DamageTaken);

            GameObject flash = new GameObject("Hit Flash");
            flash.transform.SetParent(body.transform, false);
            SpriteRenderer flashRenderer = flash.AddComponent<SpriteRenderer>();
            flashRenderer.sprite = bodyRenderer.sprite;
            flashRenderer.sortingOrder = 4;
            if (hitSilhouetteMaterial != null)
                flashRenderer.sharedMaterial = hitSilhouetteMaterial;
            flashRenderer.color = Color.clear;
            flashRenderer.enabled = false;

            GameObject impactRing = new GameObject("Hit Ring");
            impactRing.transform.SetParent(view.transform, false);
            SpriteRenderer impactRenderer = impactRing.AddComponent<SpriteRenderer>();
            impactRenderer.sprite = ringSprite;
            impactRenderer.sortingOrder = 2;
            impactRenderer.color = Color.clear;
            impactRenderer.enabled = false;

            GameObject aimIndicator = new GameObject("Aim Indicator");
            aimIndicator.transform.SetParent(view.transform, false);

            GameObject aimPivot = new GameObject("Aim Pivot");
            aimPivot.transform.SetParent(aimIndicator.transform, false);
            aimPivot.transform.localPosition = Vector3.up * CharacterHitbox.CenterHeight;
            aimPivot.transform.right = player.Aim;

            GameObject arrowHead = new GameObject("Arrow Head");
            arrowHead.transform.SetParent(aimPivot.transform, false);
            arrowHead.transform.localPosition = new Vector3(0.39f * GameTuning.CharacterScale, 0f, 0f);
            arrowHead.transform.localScale = new Vector3(0.16f * GameTuning.CharacterScale,
                0.14f * GameTuning.CharacterScale, 1f);
            SpriteRenderer headRenderer = arrowHead.AddComponent<SpriteRenderer>();
            headRenderer.sprite = arrowHeadSprite;
            Color spellColor = WeaponCatalog.Get(player.Weapon).Color;
            spellColor.a = player.IsBot ? 0.82f : 1f;
            headRenderer.color = spellColor;
            headRenderer.sortingOrder = 5;

            identifiers.Register(player, view.transform, bodyRenderer);
            if (animation != null && animation.IsFloating)
                floatingCharacters.Register(player, view.transform, bodyRenderer);
            return view.transform;
        }

        private Transform CreateProjectileView(ProjectileState projectile)
        {
            return spellVfx.CreateProjectileView(projectile);
        }

        private Transform CreatePickupView(PickupState pickup)
        {
            GameObject view = new GameObject("Pickup " + pickup.Weapon + " " + pickup.Id);
            view.transform.SetParent(root);
            SpriteRenderer orbRenderer = view.AddComponent<SpriteRenderer>();
            Sprite orbSprite;
            orbRenderer.sprite = pickupOrbSprites.TryGetValue(pickup.Weapon, out orbSprite)
                ? orbSprite : circleSprite;
            orbRenderer.sortingOrder = 3;
            Material orbMaterial;
            if (pickupOrbMaterials.TryGetValue(pickup.Weapon, out orbMaterial))
                orbRenderer.sharedMaterial = orbMaterial;

            GameObject shadow = new GameObject("Ground Shadow");
            shadow.transform.SetParent(view.transform, false);
            SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = pickupGlowSprite;
            shadowRenderer.sortingOrder = 1;
            shadowRenderer.color = Color.clear;

            GameObject aura = new GameObject("Aura");
            aura.transform.SetParent(view.transform, false);
            aura.transform.localScale = Vector3.one * (GameTuning.PickupRadius * 4f);
            SpriteRenderer auraRenderer = aura.AddComponent<SpriteRenderer>();
            auraRenderer.sprite = pickupGlowSprite;
            auraRenderer.sortingOrder = 2;
            auraRenderer.color = Color.clear;
            return view.transform;
        }

        private static Color PickupGlowColor(WeaponType weapon)
        {
            switch (weapon)
            {
                case WeaponType.Explosive: return new Color(1f, 0.68f, 0.56f);
                case WeaponType.Lightning: return new Color(1f, 0.89f, 0.57f);
                case WeaponType.Water: return new Color(0.55f, 0.82f, 1f);
                case WeaponType.Slime: return new Color(0.69f, 0.88f, 0.65f);
                case WeaponType.Orbit: return new Color(0.72f, 0.48f, 1f);
                case WeaponType.Wind: return new Color(0.78f, 0.94f, 1f);
                case WeaponType.Shadow: return new Color(0.40f, 0.18f, 0.72f);
                default: return Color.white;
            }
        }

        private static Dictionary<WeaponType, Sprite> LoadPickupOrbSprites()
        {
            Dictionary<WeaponType, Sprite> sprites = new Dictionary<WeaponType, Sprite>();
            WeaponType[] weapons = { WeaponType.Wind, WeaponType.Water, WeaponType.Explosive,
                WeaponType.Lightning, WeaponType.Slime, WeaponType.Orbit, WeaponType.Shadow };
            string[] names = { "wind", "water", "explosive", "lightning", "slime", "orbit", "shadow" };
            // Visible-art bounds measured in the supplied 1254px originals (left/top/right/bottom).
            // Normalize both the pivot and diameter despite differing source padding/import sizes.
            Vector4[] bounds =
            {
                new Vector4(86, 92, 1150, 1148), new Vector4(110, 120, 1144, 1138),
                new Vector4(168, 166, 1078, 1050), new Vector4(174, 168, 1072, 1048),
                new Vector4(174, 168, 1072, 1048), new Vector4(178, 172, 1074, 1046),
                new Vector4(78, 115, 1158, 1158)
            };
            for (int i = 0; i < weapons.Length; i++)
            {
                Texture2D texture = Resources.Load<Texture2D>("Art/PickupOrbs/" + names[i]);
                if (texture == null) continue;
                Vector4 box = bounds[i];
                float side = Mathf.Max(box.z - box.x, box.w - box.y) + 8f;
                Vector2 center = new Vector2((box.x + box.z) * 0.5f, (box.y + box.w) * 0.5f);
                Rect rect = new Rect((center.x - side * 0.5f) / 1254f * texture.width,
                    (1254f - center.y - side * 0.5f) / 1254f * texture.height,
                    side / 1254f * texture.width, side / 1254f * texture.height);
                float pixelsPerUnit = rect.width / (GameTuning.PickupRadius * 2f);
                Sprite sprite = Sprite.Create(texture, rect, Vector2.one * 0.5f,
                    pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = "Pickup Orb " + weapons[i];
                sprites.Add(weapons[i], sprite);
            }
            return sprites;
        }

        private void UpdateProjectileView(Transform view, ProjectileState projectile, float deltaTime)
        {
            ArenaDepth.Set(view, projectile.Position.y - CharacterHitbox.CenterHeight);
            spellVfx.UpdateProjectileView(view, projectile, deltaTime);
        }

        private Transform CreateEffectView(AreaEffectState effect)
        {
            return spellVfx.CreateEffectView(effect);
        }

        private void UpdateCamera(MatchSimulation simulation, float dt)
        {
            bool hasTrackedPlayer = false;
            Vector2 minimum = Vector2.zero;
            Vector2 maximum = Vector2.zero;

            foreach (PlayerState player in simulation.Players)
            {
                if (player.IsEliminated || player.RespawnTimer > 0f) continue;

                if (!hasTrackedPlayer)
                {
                    minimum = maximum = player.Position;
                    hasTrackedPlayer = true;
                }
                else
                {
                    minimum = Vector2.Min(minimum, player.Position);
                    maximum = Vector2.Max(maximum, player.Position);
                }
            }

            if (!hasTrackedPlayer)
            {
                SmoothCamera(Vector2.zero, GetWholeArenaSize(), dt);
                return;
            }

            Vector2 targetPosition = (minimum + maximum) * 0.5f;
            float spanX = maximum.x - minimum.x + CameraPaddingX * 2f;
            float spanY = maximum.y - minimum.y + CameraPaddingY * 2f;
            float aspect = Mathf.Max(0.5f, camera.aspect);
            float targetSize = Mathf.Max(spanY * 0.5f, spanX * 0.5f / aspect);
            targetSize = Mathf.Clamp(targetSize, CameraMinimumSize, CameraMaximumSize);

            float halfViewWidth = targetSize * aspect;
            targetPosition.x = ClampCameraAxis(targetPosition.x, GameTuning.ArenaHalfWidth,
                halfViewWidth, CameraBoundaryMargin);
            targetPosition.y = ClampCameraAxis(targetPosition.y, GameTuning.ArenaHalfHeight,
                targetSize, CameraBoundaryMargin);

            SmoothCamera(targetPosition, targetSize, dt);
        }

        private void FrameWholeArena()
        {
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographicSize = GetWholeArenaSize();
        }

        private float GetWholeArenaSize()
        {
            const float framingMargin = 0.38f;
            float verticalSize = GameTuning.ArenaHalfHeight + framingMargin;
            float horizontalSize = (GameTuning.ArenaHalfWidth + framingMargin) / Mathf.Max(0.5f, camera.aspect);
            return Mathf.Max(verticalSize, horizontalSize);
        }

        private void SmoothCamera(Vector2 targetPosition, float targetSize, float dt)
        {
            float safeDeltaTime = Mathf.Max(0f, dt);
            float positionBlend = 1f - Mathf.Exp(-CameraFollowSharpness * safeDeltaTime);
            float zoomBlend = 1f - Mathf.Exp(-CameraZoomSharpness * safeDeltaTime);
            Vector3 currentPosition = camera.transform.position;
            Vector2 smoothedPosition = Vector2.Lerp(currentPosition, targetPosition, positionBlend);
            camera.transform.position = new Vector3(smoothedPosition.x, smoothedPosition.y, -10f);
            camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, targetSize, zoomBlend);
        }

        private static float ClampCameraAxis(float target, float arenaHalfExtent, float viewHalfExtent,
            float boundaryMargin)
        {
            float travel = arenaHalfExtent - viewHalfExtent + boundaryMargin;
            return travel > 0f ? Mathf.Clamp(target, -travel, travel) : 0f;
        }

        private bool UpdateWizardAnimation(PlayerState player, SpriteRenderer renderer, float deltaTime)
        {
            WizardAnimationSet animation = AnimationFor(player);
            if (animation == null) return false;

            PlayerVisualState state;
            if (!playerVisualStates.TryGetValue(player.Id, out state))
                state = new PlayerVisualState(player.Position, ResolveFacing(player.LastMove), player.DamageTaken);

            Vector2 movement = player.Position - state.PreviousPosition;
            bool movedThisFrame = movement.sqrMagnitude > 0.000001f && !player.IsUnavailable;
            bool attacking = player.AttackLockTimer > 0f && !player.IsUnavailable;
            if (attacking)
            {
                state.Facing = ResolveFacing(player.AttackFacing);
                state.MovementGraceTime = 0f;
            }
            else if (movedThisFrame)
            {
                state.Facing = ResolveFacing(movement);
                state.MovementGraceTime = 0.08f;
            }

            bool isMoving = state.MovementGraceTime > 0f && !player.IsUnavailable && !attacking;
            if (isMoving)
            {
                state.AnimationTime += Mathf.Max(0f, deltaTime);
                state.MovementGraceTime -= Mathf.Max(0f, deltaTime);
            }
            else
            {
                state.AnimationTime = 0f;
            }

            renderer.sprite = animation.GetFrame(state.Facing, isMoving, state.AnimationTime);
            renderer.flipX = isMoving &&
                (state.Facing == CharacterFacing.Right && animation.MirrorRightWalk ||
                 state.Facing == CharacterFacing.Left && animation.MirrorLeftWalk);
            state.PreviousPosition = player.Position;
            playerVisualStates[player.Id] = state;
            return isMoving;
        }

        private void UpdateHitFeedback(PlayerState player, Transform view, SpriteRenderer wizardRenderer,
            float deltaTime)
        {
            PlayerVisualState state = playerVisualStates[player.Id];
            if (player.DamageTaken > state.PreviousDamageTaken)
                state.HitFlashTime = HitFlashDuration;
            else
                state.HitFlashTime = Mathf.Max(0f, state.HitFlashTime - Mathf.Max(0f, deltaTime));
            state.PreviousDamageTaken = player.DamageTaken;

            float progress = 1f - state.HitFlashTime / HitFlashDuration;
            Transform flash = view.Find("Wizard/Hit Flash");
            SpriteRenderer flashRenderer = flash.GetComponent<SpriteRenderer>();
            flashRenderer.sprite = wizardRenderer.sprite;
            flashRenderer.flipX = wizardRenderer.flipX;
            flashRenderer.sortingOrder = wizardRenderer.sortingOrder + 1;
            float flashAlpha = state.HitFlashTime > 0f
                ? progress < 0.40f ? 1f : 1f - (progress - 0.40f) / 0.60f
                : 0f;
            flashRenderer.enabled = hitSilhouetteMaterial != null && flashAlpha > 0f;
            flashRenderer.color = hitSilhouetteMaterial != null
                ? new Color(1f, 1f, 1f, flashAlpha)
                : Color.clear;

            Transform ring = view.Find("Hit Ring");
            SpriteRenderer ringRenderer = ring.GetComponent<SpriteRenderer>();
            ringRenderer.enabled = state.HitFlashTime > 0f;
            ring.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.05f, progress);
            ringRenderer.color = new Color(1f, 1f, 1f,
                state.HitFlashTime > 0f ? 0.70f * (1f - progress) : 0f);
        }

        private static CharacterFacing ResolveFacing(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
                return direction.x >= 0f ? CharacterFacing.Right : CharacterFacing.Left;
            return direction.y >= 0f ? CharacterFacing.Back : CharacterFacing.Front;
        }

        private WizardAnimationSet AnimationFor(PlayerState player)
        {
            if (player.Character == CharacterType.AutumnFloatingWizard && autumnFloatingWizardAnimation != null)
                return autumnFloatingWizardAnimation;
            if (player.Character == CharacterType.LeafFloatingWizard && leafFloatingWizardAnimation != null)
                return leafFloatingWizardAnimation;
            if (player.Character == CharacterType.DarkFloatingWizard && darkFloatingWizardAnimation != null)
                return darkFloatingWizardAnimation;
            return player.Character == CharacterType.FloatingWizard && floatingWizardAnimation != null
                ? floatingWizardAnimation : wizardAnimation;
        }

        private static WizardAnimationSet LoadFloatingWizardAnimation(string art = "Art/FloatingWizard/Idle/idle_")
        {
            Vector2 pivot = new Vector2(0.5f, 18f / 512f);
            Sprite front = LoadSprite(art + "front", 600f, pivot);
            Sprite back = LoadSprite(art + "back", 600f, pivot);
            Sprite left = LoadSprite(art + "left", 600f, pivot);
            Sprite right = LoadSprite(art + "right", 600f, pivot);
            if (front == null || back == null || left == null || right == null) return null;
            return new WizardAnimationSet(new[] { front }, new[] { left }, new[] { right }, new[] { back },
                front, back, left, right, true, true);
        }

        private static WizardAnimationSet LoadWizardAnimation()
        {
            const string art = "Art/Character01";
            Vector2 pivot = Vector2.one * 0.5f;
            Sprite[] front = LoadAnimationFrames(art + "/Front", 300f, pivot);
            Sprite[] side = LoadAnimationFrames(art + "/Side", 300f, pivot);
            Sprite[] right = side;
            Sprite[] back = LoadAnimationFrames(art + "/Back", 300f, pivot);
            Sprite idleFront = LoadSprite(art + "/Idle/idle_front", 600f, pivot);
            Sprite idleBack = LoadSprite(art + "/Idle/idle_back", 600f, pivot);
            Sprite idleLeft = LoadSprite(art + "/Idle/idle_left", 600f, pivot);
            Sprite idleRight = LoadSprite(art + "/Idle/idle_right", 600f, pivot);
            return front.Length > 0 && side.Length > 0 && right.Length > 0 && back.Length > 0 &&
                   idleFront != null && idleBack != null && idleLeft != null && idleRight != null
                ? new WizardAnimationSet(front, side, right, back, idleFront, idleBack, idleLeft, idleRight, false)
                : null;
        }

        private static Sprite LoadSprite(string resourcesPath, float pixelsPerUnit, Vector2 pivot)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
            if (texture == null) return null;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                pivot, pixelsPerUnit / GameTuning.CharacterScale, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return sprite;
        }

        private static Sprite[] LoadAnimationFrames(string resourcesPath, float pixelsPerUnit, Vector2 pivot)
        {
            Texture2D[] textures = Resources.LoadAll<Texture2D>(resourcesPath);
            System.Array.Sort(textures, (left, right) => string.CompareOrdinal(left.name, right.name));
            Sprite[] frames = new Sprite[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D texture = textures[i];
                frames[i] = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    pivot, pixelsPerUnit / GameTuning.CharacterScale, 0, SpriteMeshType.FullRect);
                frames[i].name = texture.name;
            }
            return frames;
        }

        private enum CharacterFacing
        {
            Front,
            Back,
            Left,
            Right
        }

        private sealed class PlayerVisualState
        {
            public Vector2 PreviousPosition;
            public CharacterFacing Facing;
            public float AnimationTime;
            public float MovementGraceTime;
            public float PreviousDamageTaken;
            public float HitFlashTime;

            public PlayerVisualState(Vector2 position, CharacterFacing facing, float damageTaken)
            {
                PreviousPosition = position;
                Facing = facing;
                PreviousDamageTaken = damageTaken;
            }
        }

        private sealed class WizardAnimationSet
        {
            private const float FrameDuration = 0.14f;
            private readonly Sprite[] front;
            private readonly Sprite[] side;
            private readonly Sprite[] right;
            private readonly Sprite[] back;
            private readonly Sprite idleFront;
            private readonly Sprite idleBack;
            private readonly Sprite idleLeft;
            private readonly Sprite idleRight;

            public Sprite FrontFrame => idleFront;
            public bool HasFootPivot { get; }
            public bool IsFloating { get; }
            public bool MirrorRightWalk { get; }
            public bool MirrorLeftWalk => HasFootPivot && !IsFloating;

            public WizardAnimationSet(Sprite[] frontFrames, Sprite[] sideFrames, Sprite[] rightFrames, Sprite[] backFrames,
                Sprite frontIdle, Sprite backIdle, Sprite leftIdle, Sprite rightIdle, bool footAnchored, bool isFloating = false)
            {
                front = frontFrames;
                side = sideFrames;
                right = rightFrames;
                back = backFrames;
                idleFront = frontIdle;
                idleBack = backIdle;
                idleLeft = leftIdle;
                idleRight = rightIdle;
                HasFootPivot = footAnchored;
                IsFloating = isFloating;
                MirrorRightWalk = !footAnchored;
            }

            public Sprite GetFrame(CharacterFacing facing, bool isMoving, float animationTime)
            {
                if (!isMoving || IsFloating)
                {
                    if (facing == CharacterFacing.Front) return idleFront;
                    if (facing == CharacterFacing.Back) return idleBack;
                    return facing == CharacterFacing.Left ? idleLeft : idleRight;
                }

                Sprite[] frames = facing == CharacterFacing.Front
                    ? front
                    : facing == CharacterFacing.Back ? back
                    : facing == CharacterFacing.Right ? right : side;
                int frameIndex = Mathf.FloorToInt(animationTime / FrameDuration) % frames.Length;
                return frames[frameIndex];
            }
        }

        private static Sprite CreatePickupGlowSprite()
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Runtime Pickup Glow";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Runtime Circle";
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float radius = size * 0.48f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }

        private static Sprite CreateRingSprite()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Runtime Aim Ring";
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float outerRadius = size * 0.48f;
            float innerRadius = size * 0.455f;
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

        private static Sprite CreateArrowHeadSprite()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Runtime Aim Arrow Head";
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            float left = size * 0.08f;
            float right = size * 0.92f;
            float centerY = (size - 1) * 0.5f;
            float halfHeight = size * 0.43f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float progress = Mathf.InverseLerp(left, right, x);
                float allowedHalfHeight = (1f - progress) * halfHeight;
                bool inside = x >= left && x <= right && Mathf.Abs(y - centerY) <= allowedHalfHeight;
                pixels[y * size + x] = inside ? Color.white : Color.clear;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }
    }
}
