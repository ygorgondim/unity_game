using System.Collections.Generic;
using UnityEngine;
using WizardRoyale.Domain;

namespace WizardRoyale.Presentation
{
    // The base elevation is shared with the body hitbox; the gentle bobbing stays visual only.
    public sealed class FloatingCharacterPresentation
    {
        private static readonly int HemClockId = Shader.PropertyToID("_HemClock");
        private static readonly int HemPhaseId = Shader.PropertyToID("_HemPhase");
        private static readonly int HemStrengthId = Shader.PropertyToID("_HemStrength");
        private static readonly int HemDragId = Shader.PropertyToID("_HemDrag");
        private static readonly int ShadowModeId = Shader.PropertyToID("_ShadowMode");
        private static readonly int ShadowEyeAId = Shader.PropertyToID("_ShadowEyeA");
        private static readonly int ShadowEyeBId = Shader.PropertyToID("_ShadowEyeB");
        private readonly Dictionary<int, FloatingView> views = new Dictionary<int, FloatingView>();
        private readonly Sprite shadowSprite;
        private readonly Material robeMaterial;

        private sealed class FloatingView
        {
            public SpriteRenderer Body;
            public SpriteRenderer Shadow;
            public SpriteRenderer Flash;
            public SpriteRenderer Outline;
            public float HemTime;
            public float LastHemUpdate;
            public Vector2 PreviousPosition;
            public float HemDrag;
            public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        }

        public FloatingCharacterPresentation(Sprite softShadowSprite)
        {
            shadowSprite = softShadowSprite;
            Shader shader = Resources.Load<Shader>("Shaders/FloatingRobe");
            if (shader != null) robeMaterial = new Material(shader);
        }

        public void Reset()
        {
            views.Clear();
        }

        public void Register(PlayerState player, Transform groundAnchor, SpriteRenderer body)
        {
            GameObject shadowObject = new GameObject("Floating Character - Ground Shadow");
            shadowObject.transform.SetParent(groundAnchor, false);
            shadowObject.transform.localPosition = new Vector3(0f, 0.015f * GameTuning.CharacterScale, 0.01f);
            SpriteRenderer shadow = shadowObject.AddComponent<SpriteRenderer>();
            shadow.sprite = shadowSprite;
            shadow.sortingOrder = 1;
            if (robeMaterial != null) body.sharedMaterial = robeMaterial;
            views[player.Id] = new FloatingView
            {
                Body = body,
                LastHemUpdate = Time.unscaledTime,
                PreviousPosition = player.Position,
                Shadow = shadow,
                Flash = body.transform.Find("Hit Flash").GetComponent<SpriteRenderer>(),
                Outline = body.transform.Find("Identity Outline").GetComponent<SpriteRenderer>()
            };
            Sync(player);
        }

        public void Sync(PlayerState player, bool isMoving = false)
        {
            FloatingView view;
            if (!views.TryGetValue(player.Id, out view)) return;
            float time = Time.unscaledTime;
            float elapsed = Mathf.Max(0f, time - view.LastHemUpdate);
            Vector2 movement = player.Position - view.PreviousPosition;
            bool movingSideways = isMoving && !player.IsFalling && !player.IsEliminated
                && movement.sqrMagnitude > 0.000001f
                && Mathf.Abs(movement.x) >= Mathf.Abs(movement.y);
            float targetDrag = movingSideways ? movement.normalized.x * 0.055f : 0f;
            view.HemDrag = Mathf.Lerp(view.HemDrag, targetDrag, 1f - Mathf.Exp(-8f * elapsed));
            view.PreviousPosition = player.Position;
            // Integrate speed rather than multiplying absolute time to preserve wave continuity.
            view.HemTime += elapsed * (isMoving ? 5f : 1f);
            view.LastHemUpdate = time;
            float phase = player.Id * 1.31f;
            float bob = Mathf.Sin(time * 1.80f + phase);
            view.Body.transform.localPosition = Vector3.up
                * (CharacterHitbox.BodyElevation + bob * 0.025f * GameTuning.CharacterScale);

            float altitude = (bob + 1f) * 0.5f;
            view.Shadow.enabled = !player.IsEliminated && !player.IsFalling
                && GoldenArenaLayout.Current.ContainsWorld(player.Position);
            view.Shadow.transform.localScale = new Vector3(
                Mathf.Lerp(0.34f, 0.41f, altitude) * GameTuning.CharacterScale,
                Mathf.Lerp(0.11f, 0.14f, altitude) * GameTuning.CharacterScale, 1f);
            view.Shadow.color = new Color(0.06f, 0.08f, 0.14f, Mathf.Lerp(0.32f, 0.24f, altitude));

            // All three renderers sample the same moving hem: no stationary outline or flash ghost.
            SetHem(view.Body, view.Properties, view.HemTime, phase, view.HemDrag);
            SetHem(view.Flash, view.Properties, view.HemTime, phase, view.HemDrag);
            SetHem(view.Outline, view.Properties, view.HemTime, phase, view.HemDrag);
            view.Body.GetPropertyBlock(view.Properties);
            view.Properties.SetFloat(ShadowModeId, player.IsShadowForm ? 1f : 0f);
            Vector4 eyeA, eyeB;
            EyeRegions(player.Character, view.Body.sprite.name, out eyeA, out eyeB);
            view.Properties.SetVector(ShadowEyeAId, eyeA);
            view.Properties.SetVector(ShadowEyeBId, eyeB);
            view.Body.SetPropertyBlock(view.Properties);
        }

        private static Vector4 Eye(int left, int top, int right, int bottom)
        {
            return new Vector4((left - 2f) / 512f, 1f - (bottom + 3f) / 512f,
                (right + 3f) / 512f, 1f - (top - 2f) / 512f);
        }

        private static void EyeRegions(CharacterType character, string pose, out Vector4 a, out Vector4 b)
        {
            a = b = Vector4.zero;
            if (pose.Contains("back")) return;
            bool front = pose.Contains("front"), left = pose.Contains("left");
            switch (character)
            {
                case CharacterType.FloatingWizard:
                    a = front ? Eye(215,260,238,289) : left ? Eye(141,262,161,290) : Eye(352,261,371,284);
                    if (front) b = Eye(283,260,306,289);
                    break;
                case CharacterType.DarkFloatingWizard:
                    a = front ? Eye(216,238,239,267) : left ? Eye(151,248,170,280) : Eye(351,234,369,266);
                    if (front) b = Eye(287,239,310,270);
                    break;
                case CharacterType.LeafFloatingWizard:
                    a = front ? Eye(218,262,240,299) : left ? Eye(118,255,135,293) : Eye(377,238,396,278);
                    if (front) b = Eye(282,262,303,299);
                    break;
                case CharacterType.AutumnFloatingWizard:
                    a = front ? Eye(214,253,233,291) : left ? Eye(142,247,158,286) : Eye(347,242,363,281);
                    if (front) b = Eye(283,253,302,291);
                    break;
            }
        }

        private void SetHem(SpriteRenderer renderer, MaterialPropertyBlock properties, float time, float phase,
            float drag)
        {
            renderer.GetPropertyBlock(properties);
            properties.SetFloat(HemClockId, time);
            properties.SetFloat(HemPhaseId, phase);
            properties.SetFloat(HemStrengthId, robeMaterial != null ? 1f : 0f);
            properties.SetFloat(HemDragId, renderer.flipX ? -drag : drag);
            renderer.SetPropertyBlock(properties);
        }
    }
}
