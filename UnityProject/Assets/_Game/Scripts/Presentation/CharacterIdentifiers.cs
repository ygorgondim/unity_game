using System.Collections.Generic;
using UnityEngine;
using WizardRoyale.Domain;

namespace WizardRoyale.Presentation
{
    // Presentation only: identifiers follow animation frames without changing character collision.
    public sealed class CharacterIdentifiers
    {
        private static readonly int OutlineUvId = Shader.PropertyToID("_OutlineUV");
        private readonly Dictionary<int, Identifier> entries = new Dictionary<int, Identifier>();
        private readonly Camera camera;
        private readonly Sprite arrowSprite;
        private readonly Sprite glowSprite;
        private readonly Sprite barSprite;
        private readonly Material outlineMaterial;
        private const float BarWidth = 0.42f;
        private const float HealthBarHeight = 0.035f;
        private const float CooldownBarHeight = 0.022f;

        private sealed class Identifier
        {
            public Transform Root;
            public SpriteRenderer Outline;
            public TextMesh Nick;
            public TextMesh NickShadow;
            public Transform LocalMarker;
            public SpriteRenderer Marker;
            public SpriteRenderer MarkerGlow;
            public SpriteRenderer HealthFill;
            public SpriteRenderer CooldownFill;
            public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        }

        public CharacterIdentifiers(Camera targetCamera, Sprite arrow, Sprite glow)
        {
            camera = targetCamera;
            arrowSprite = arrow;
            glowSprite = glow;
            barSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                Vector2.one * 0.5f, 1f);
            Shader shader = Resources.Load<Shader>("Shaders/CharacterOutline");
            if (shader != null) outlineMaterial = new Material(shader);
        }

        public void Reset()
        {
            entries.Clear();
        }

        public void Register(PlayerState player, Transform view, SpriteRenderer wizard)
        {
            GameObject root = new GameObject("Player Identifier");
            root.transform.SetParent(view, false);
            if (GoldenArenaLayout.Current.swamp)
                root.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 4000;
            Identifier entry = new Identifier { Root = root.transform };
            entry.NickShadow = CreateLabel(root.transform, "Nickname Shadow", player.Name, 21);
            entry.NickShadow.transform.localPosition = new Vector3(0.007f, -0.008f, 0f);
            entry.NickShadow.color = new Color(0.035f, 0.025f, 0.06f, 0.85f);
            entry.Nick = CreateLabel(root.transform, "Nickname", player.Name, 22);
            entry.HealthFill = CreateBar(root.transform, "Health Bar", -0.07f, HealthBarHeight);

            GameObject outline = new GameObject("Identity Outline");
            outline.transform.SetParent(wizard.transform, false);
            entry.Outline = outline.AddComponent<SpriteRenderer>();
            entry.Outline.sprite = wizard.sprite;
            entry.Outline.enabled = outlineMaterial != null;
            if (outlineMaterial != null) entry.Outline.sharedMaterial = outlineMaterial;

            if (player.Id == 0)
            {
                // Never create another player's cooldown bar in this local presentation.
                entry.CooldownFill = CreateBar(root.transform, "Attack Cooldown", -0.12f, CooldownBarHeight);
                GameObject marker = new GameObject("Local Player - Down Arrow");
                marker.transform.SetParent(root.transform, false);
                entry.LocalMarker = marker.transform;
                entry.MarkerGlow = CreateSprite(marker.transform, "Marker Glow", glowSprite, 23);
                entry.MarkerGlow.transform.localScale = Vector3.one * 0.32f;
                entry.Marker = CreateSprite(marker.transform, "Marker", arrowSprite, 24);
                entry.Marker.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                entry.Marker.transform.localScale = new Vector3(0.13f, 0.16f, 1f);
            }

            entries[player.Id] = entry;
            Sync(player, wizard);
        }

        public void Sync(PlayerState player, SpriteRenderer wizard)
        {
            Identifier entry;
            if (!entries.TryGetValue(player.Id, out entry)) return;
            bool visible = !player.IsUnavailable;
            entry.Root.gameObject.SetActive(visible);
            entry.Outline.enabled = visible && outlineMaterial != null && !player.IsShadowForm;
            if (!visible) return;

            bool local = player.Id == 0;
            float zoomScale = Mathf.Clamp(camera.orthographicSize / 3f, 0.85f, 2.4f);
            entry.Root.localScale = Vector3.one * zoomScale;
            float top = wizard.transform.localPosition.y
                + wizard.sprite.bounds.max.y * wizard.transform.localScale.y;
            entry.Root.localPosition = Vector3.up * (top + 0.20f * zoomScale);
            entry.Nick.text = entry.NickShadow.text = player.Name;
            Color nickColor = Color.Lerp(player.Color, Color.white, 0.70f);
            nickColor.a = local ? 0.95f : 0.80f;
            entry.Nick.color = nickColor;
            float health = Mathf.Clamp01(player.Hp / GameTuning.MaxHp);
            SetBar(entry.HealthFill, health, HealthBarHeight,
                Color.Lerp(new Color(0.98f, 0.30f, 0.28f), new Color(0.40f, 0.88f, 0.53f), health));
            if (entry.CooldownFill != null)
            {
                float duration = Mathf.Max(0.001f, WeaponCatalog.Get(player.Weapon).Cooldown);
                float ready = 1f - Mathf.Clamp01(player.AttackCooldown / duration);
                SetBar(entry.CooldownFill, ready, CooldownBarHeight,
                    ready >= 1f ? new Color(0.72f, 0.93f, 1f) : new Color(0.40f, 0.66f, 0.92f));
            }

            entry.Outline.sprite = wizard.sprite;
            entry.Outline.flipX = wizard.flipX;
            entry.Outline.flipY = wizard.flipY;
            entry.Outline.sortingOrder = wizard.sortingOrder - 1;
            Color outlineColor = player.Color;
            outlineColor.a = local ? 0.85f : 0.55f;
            entry.Outline.color = outlineColor;
            float width = (local ? 0.012f : 0.008f) * zoomScale;
            Vector3 spriteScale = wizard.transform.lossyScale;
            Sprite sprite = wizard.sprite;
            entry.Outline.GetPropertyBlock(entry.Properties);
            entry.Properties.SetVector(OutlineUvId, new Vector4(
                width * sprite.pixelsPerUnit / (sprite.texture.width * Mathf.Max(0.01f, Mathf.Abs(spriteScale.x))),
                width * sprite.pixelsPerUnit / (sprite.texture.height * Mathf.Max(0.01f, Mathf.Abs(spriteScale.y))),
                0f, 0f));
            entry.Outline.SetPropertyBlock(entry.Properties);

            if (entry.LocalMarker != null)
            {
                float pulse = Mathf.Sin(Time.unscaledTime * 2.4f);
                entry.LocalMarker.localPosition = Vector3.up * (0.19f + pulse * 0.015f);
                entry.Marker.color = Color.Lerp(player.Color, Color.white, 0.30f);
                Color glowColor = player.Color;
                glowColor.a = 0.22f + pulse * 0.035f;
                entry.MarkerGlow.color = glowColor;
            }
        }

        private SpriteRenderer CreateBar(Transform parent, string name, float y, float height)
        {
            GameObject bar = new GameObject(name);
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(0f, y, 0f);
            SpriteRenderer border = CreateSprite(bar.transform, "Border", barSprite, 23);
            border.transform.localScale = new Vector3(BarWidth + 0.012f, height + 0.012f, 1f);
            border.color = new Color(0.025f, 0.025f, 0.045f, 0.80f);
            SpriteRenderer background = CreateSprite(bar.transform, "Background", barSprite, 24);
            background.transform.localScale = new Vector3(BarWidth, height, 1f);
            background.color = new Color(0.12f, 0.14f, 0.20f, 0.85f);
            return CreateSprite(bar.transform, "Fill", barSprite, 25);
        }

        private static void SetBar(SpriteRenderer fill, float fraction, float height, Color color)
        {
            float width = BarWidth * Mathf.Clamp01(fraction);
            fill.enabled = width > 0f;
            fill.transform.localScale = new Vector3(width, height, 1f);
            fill.transform.localPosition = new Vector3((width - BarWidth) * 0.5f, 0f, 0f);
            fill.color = color;
        }

        private static TextMesh CreateLabel(Transform parent, string name, string text, int order)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            TextMesh label = node.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.02f;
            label.fontStyle = FontStyle.Normal;
            node.GetComponent<MeshRenderer>().sortingOrder = order;
            return label;
        }

        private static SpriteRenderer CreateSprite(Transform parent, string name, Sprite sprite, int order)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            SpriteRenderer renderer = node.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
