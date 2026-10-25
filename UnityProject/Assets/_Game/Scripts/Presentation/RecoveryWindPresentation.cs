using System.Collections.Generic;
using UnityEngine;
using WizardRoyale.Domain;

namespace WizardRoyale.Presentation
{
    // Cosmetic recovery wind. World-space particles stay behind instead of following the character.
    public sealed class RecoveryWindPresentation
    {
        private const int MaxParticles = 256;
        private readonly Transform root;
        private readonly Sprite glowSprite;
        private readonly Sprite sparkSprite;
        private readonly Material material;
        private readonly Material shadowMaterial;
        private static readonly int BubblePhaseId = Shader.PropertyToID("_BubblePhase");
        private static readonly int SmokeId = Shader.PropertyToID("_Smoke");
        private readonly Dictionary<int, WindView> views = new Dictionary<int, WindView>();
        private readonly List<Spark> sparks = new List<Spark>();
        private float time;
        private int replacementIndex;

        private sealed class WindView
        {
            public Transform Root;
            public readonly LineRenderer[] Streams = new LineRenderer[3];
            public float Opacity;
            public float EmissionClock;
            public Vector2 Previous;
            public Vector2 PreviousGround;
            public float Heading;
            public float TailHeading;
            public float MotionBlend;
            public bool WasActive;
            public bool Shadow;
        }

        private sealed class Spark
        {
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life, Duration, Size, Phase;
            public bool Shadow;
            public bool Smoke;
            public float Aspect, Spin;
            public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        }

        public RecoveryWindPresentation(Transform parent, Sprite glow, Sprite spark, Material sharedMaterial)
        {
            root = new GameObject("Recovery Wind VFX").transform;
            root.SetParent(parent, false);
            glowSprite = glow;
            sparkSprite = spark;
            material = sharedMaterial;
            Shader shader = Resources.Load<Shader>("Shaders/ShadowBubble");
            if (shader != null) shadowMaterial = new Material(shader);
        }

        public void Reset()
        {
            foreach (WindView view in views.Values) Object.Destroy(view.Root.gameObject);
            views.Clear();
            foreach (Spark spark in sparks)
            {
                spark.Life = 0f;
                spark.Renderer.gameObject.SetActive(false);
            }
            time = 0f;
            replacementIndex = 0;
        }

        public void Tick(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            time += dt;
            foreach (Spark spark in sparks)
            {
                if (spark.Life <= 0f) continue;
                spark.Life -= dt;
                if (spark.Life <= 0f)
                {
                    spark.Renderer.gameObject.SetActive(false);
                    continue;
                }
                spark.Position += spark.Velocity * dt;
                spark.Velocity *= Mathf.Exp(-1.8f * dt);
                float age = 1f - spark.Life / spark.Duration;
                float fade = Mathf.Sin(age * Mathf.PI);
                spark.Renderer.transform.position = new Vector3(spark.Position.x, spark.Position.y, -0.29f);
                float size = spark.Size * (spark.Smoke ? 1f + age * 0.8f
                    : spark.Shadow ? Mathf.Pow(1f - age, 0.85f) : 1f - age * 0.65f);
                float wobble = spark.Shadow ? Mathf.Sin(time * 5f + spark.Phase) * 0.10f : 0f;
                spark.Renderer.transform.localScale = new Vector3(size * spark.Aspect * (1f + wobble),
                    size / spark.Aspect * (1f - wobble), 1f);
                spark.Renderer.transform.Rotate(0f, 0f, spark.Spin * dt);
                float twinkle = 0.80f + 0.20f * Mathf.Sin(time * 14f + spark.Phase);
                spark.Renderer.color = spark.Smoke ? new Color(0.025f, 0.012f, 0.04f, fade * 0.16f)
                    : spark.Shadow ? new Color(0f, 0f, 0f, 0.98f)
                    : new Color(1.25f, 1.3f, 1.35f, fade * twinkle * 0.85f);
            }
        }

        public void Sync(PlayerState player, SpriteRenderer body, float deltaTime)
        {
            bool dashing = player.DashTimer > 0f;
            bool active = (player.IsRecovering || dashing || player.IsShadowForm) && !player.IsUnavailable;
            WindView view;
            if (!views.TryGetValue(player.Id, out view))
            {
                if (!active) return;
                view = CreateView(player.Id);
                views.Add(player.Id, view);
            }
            float dt = Mathf.Max(0f, deltaTime);
            view.Shadow = player.IsShadowForm;
            float fadeDuration = active && dashing ? 0.05f : 0.16f;
            view.Opacity = Mathf.MoveTowards(view.Opacity, active ? 1f : 0f, dt / fadeDuration);
            // After dash/recovery, stop emission but let the ribbons and world-space motes fade.
            Vector2 anchor = (Vector2)body.transform.position;
            if (!view.WasActive)
            {
                view.Previous = anchor;
                view.PreviousGround = player.Position;
                if (active)
                {
                    Vector2 facing = dashing && player.DashVelocity.sqrMagnitude > 0.01f
                        ? player.DashVelocity
                        : player.LastMove.sqrMagnitude > 0.01f ? player.LastMove : player.Aim;
                    view.Heading = view.TailHeading = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
                    view.MotionBlend = dashing ? 1f : 0f;
                }
            }
            Vector2 velocity = dt > 0.00001f ? (player.Position - view.PreviousGround) / dt : Vector2.zero;
            velocity = Vector2.ClampMagnitude(velocity, 8f);
            if (active && velocity.sqrMagnitude > 0.0025f)
            {
                float heading = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                view.Heading = Mathf.LerpAngle(view.Heading, heading, 1f - Mathf.Exp(-10f * dt));
            }
            view.TailHeading = Mathf.LerpAngle(view.TailHeading, view.Heading, 1f - Mathf.Exp(-4f * dt));
            view.MotionBlend = Mathf.Lerp(view.MotionBlend,
                active ? Mathf.Clamp01(velocity.magnitude / GameTuning.PlayerSpeed) : 0f,
                1f - Mathf.Exp(-6f * dt));
            if (active || view.WasActive) view.Root.position = new Vector3(anchor.x, anchor.y, -0.26f);
            view.Root.gameObject.SetActive(view.Opacity > 0f);
            if (view.Opacity > 0f)
            {
                float urgency = active && player.IsRecovering
                    ? 1f - Mathf.Clamp01(player.FloatTimer / GameTuning.RecoveryDuration) : 0f;
                AnimateStreams(view, player.Id, urgency);
            }
            if (active)
            {
                Vector2 forward = new Vector2(Mathf.Cos(view.Heading * Mathf.Deg2Rad),
                    Mathf.Sin(view.Heading * Mathf.Deg2Rad));
                Vector2 side = new Vector2(-forward.y, forward.x);
                view.EmissionClock += dt;
                int count = Mathf.Min(12, Mathf.FloorToInt(view.EmissionClock / 0.025f));
                view.EmissionClock -= count * 0.025f;
                for (int i = 0; i < count; i++)
                {
                    Vector2 alongPath = Vector2.Lerp(view.Previous, anchor, (i + 1f) / count);
                    Vector2 wake = alongPath + Vector2.up * 0.20f
                        - forward * Random.Range(0.18f, 0.60f) + side * Random.Range(-0.14f, 0.14f);
                    Emit(wake, -velocity * 0.12f - forward * 0.12f
                        + side * Random.Range(-0.08f, 0.08f), false, view.Shadow);
                    if (i % 3 == 0)
                        Emit(wake, -forward * 0.10f + Vector2.up * 0.04f, true, view.Shadow);
                    if (view.Shadow && Random.value < 0.65f)
                    {
                        Vector2 around = Random.insideUnitCircle;
                        Vector2 position = alongPath + Vector2.up * 0.22f
                            + new Vector2(around.x * 0.34f, around.y * 0.29f);
                        Emit(position, around * 0.10f + Vector2.up * 0.05f, false, true);
                    }
                    if (view.Shadow && dashing)
                        Emit(alongPath + Vector2.up * 0.22f + Random.insideUnitCircle * 0.20f,
                            -velocity * 0.08f + Vector2.up * 0.12f, false, true, true);
                }
            }
            else view.EmissionClock = 0f;
            view.Previous = anchor;
            view.PreviousGround = player.Position;
            view.WasActive = active;
        }

        private void AnimateStreams(WindView view, int playerId, float urgency)
        {
            float phase = time * 3.8f + playerId * 1.7f;
            for (int stream = 0; stream < view.Streams.Length; stream++)
            {
                LineRenderer line = view.Streams[stream];
                line.enabled = !view.Shadow;
                if (view.Shadow) continue;
                float length = Mathf.Lerp(0.35f, 0.95f, view.MotionBlend) * (1f + urgency * 0.08f);
                for (int i = 0; i < line.positionCount; i++)
                {
                    float t = i / (float)(line.positionCount - 1);
                    // The older tail turns more slowly than the shoulder, producing a soft curved wake.
                    float angle = Mathf.LerpAngle(view.Heading, view.TailHeading, t) * Mathf.Deg2Rad;
                    Vector2 forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 side = new Vector2(-forward.y, forward.x);
                    float ripple = Mathf.Sin(phase - t * 7f + stream * 2.1f) * 0.055f * t;
                    float idleCurl = Mathf.Sin(t * Mathf.PI * 2f - phase + stream)
                        * 0.09f * t * (1f - view.MotionBlend);
                    float spread = (stream - 1f) * (0.08f + t * 0.07f);
                    Vector2 point = Vector2.up * 0.20f - forward * (0.15f + t * length)
                        + side * (spread + ripple + idleCurl);
                    line.SetPosition(i, new Vector3(point.x, point.y, 0f));
                }
                float alpha = view.Opacity * (stream == 1 ? 0.70f : 0.45f);
                line.startColor = new Color(1.2f, 1.25f, 1.3f, alpha);
                line.endColor = new Color(1f, 1f, 1f, 0f);
                line.startWidth = stream == 1 ? 0.035f : 0.022f;
                line.endWidth = 0.002f;
            }
        }

        private WindView CreateView(int id)
        {
            WindView view = new WindView { Root = new GameObject("Recovery Wind - Player " + id).transform };
            view.Root.SetParent(root, false);
            for (int i = 0; i < view.Streams.Length; i++)
            {
                GameObject node = new GameObject("Trailing Back Wind Ribbon " + i);
                node.transform.SetParent(view.Root, false);
                LineRenderer line = node.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = false;
                line.positionCount = 40;
                line.numCapVertices = 3;
                line.sortingOrder = 2;
                view.Streams[i] = line;
            }
            return view;
        }

        private void Emit(Vector2 position, Vector2 velocity, bool soft, bool shadow, bool smoke = false)
        {
            Spark spark = null;
            foreach (Spark candidate in sparks)
                if (candidate.Life <= 0f) { spark = candidate; break; }
            if (spark == null && sparks.Count < MaxParticles)
            {
                GameObject node = new GameObject("Recovery Wind - Pooled Mote");
                node.transform.SetParent(root, false);
                spark = new Spark { Renderer = node.AddComponent<SpriteRenderer>() };
                spark.Renderer.sortingOrder = 2;
                sparks.Add(spark);
            }
            if (spark == null) spark = sparks[replacementIndex++ % MaxParticles];
            spark.Position = position;
            spark.Velocity = velocity;
            spark.Life = spark.Duration = Random.Range(0.45f, 0.85f);
            spark.Size = soft ? Random.Range(0.08f, 0.14f) : Random.Range(0.018f, 0.038f);
            spark.Phase = Random.value * Mathf.PI * 2f;
            spark.Shadow = shadow;
            spark.Smoke = smoke;
            spark.Aspect = shadow ? Random.Range(0.82f, 1.22f) : 1f;
            spark.Spin = shadow ? Random.Range(-35f, 35f) : 0f;
            if (shadow) spark.Size = Mathf.Lerp(0.045f, 0.26f, Mathf.Pow(Random.value, 1.6f));
            if (smoke)
            {
                spark.Size = Random.Range(0.30f, 0.52f);
                spark.Life = spark.Duration = Random.Range(0.40f, 0.70f);
            }
            spark.Renderer.sprite = soft && !shadow ? glowSprite : sparkSprite;
            spark.Renderer.sharedMaterial = shadow && shadowMaterial != null ? shadowMaterial : material;
            spark.Properties.Clear();
            spark.Properties.SetFloat(BubblePhaseId, spark.Phase);
            spark.Properties.SetFloat(SmokeId, smoke ? 1f : 0f);
            spark.Renderer.SetPropertyBlock(spark.Properties);
            spark.Renderer.sortingOrder = 2;
            spark.Renderer.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            spark.Renderer.transform.position = new Vector3(position.x, position.y, -0.29f);
            spark.Renderer.transform.localScale = new Vector3(spark.Size * spark.Aspect,
                spark.Size / spark.Aspect, 1f);
            spark.Renderer.color = shadow && !smoke ? new Color(0f, 0f, 0f, 0.98f) : Color.clear;
            spark.Renderer.gameObject.SetActive(true);
        }
    }
}
