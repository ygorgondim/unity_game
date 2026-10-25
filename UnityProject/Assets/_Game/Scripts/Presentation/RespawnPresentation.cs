using System.Collections.Generic;
using UnityEngine;
using WizardRoyale.Domain;

namespace WizardRoyale.Presentation
{
    public sealed class RespawnPresentation
    {
        private readonly Transform root;
        private readonly Sprite ring, glow, mote;
        private readonly Dictionary<int, View> views = new Dictionary<int, View>();
        private sealed class View
        {
            public Transform Root;
            public SpriteRenderer Ring, Light;
            public readonly SpriteRenderer[] Motes = new SpriteRenderer[12];
        }

        public RespawnPresentation(Transform parent, Sprite ringSprite, Sprite glowSprite, Sprite moteSprite)
        {
            root = parent;
            ring = ringSprite;
            glow = glowSprite;
            mote = moteSprite;
        }

        public void Reset()
        {
            foreach (View view in views.Values) Object.Destroy(view.Root.gameObject);
            views.Clear();
        }

        public void Sync(PlayerState player, SpriteRenderer body)
        {
            bool visible = !player.IsEliminated && !player.IsFalling && player.RespawnTimer <= 0f
                && (player.IsSpawning || player.InvulnerabilityTimer > 0f);
            View view;
            if (!views.TryGetValue(player.Id, out view))
            {
                if (!visible) return;
                view = new View { Root = new GameObject("Respawn Light - " + player.Id).transform };
                view.Root.SetParent(root, false);
                view.Ring = Create(view.Root, "Protection Ring", ring, 2);
                view.Light = Create(view.Root, "Materialization Light", glow, 4);
                for (int i = 0; i < view.Motes.Length; i++)
                    view.Motes[i] = Create(view.Root, "Rising Spark " + i, mote, 4);
                views.Add(player.Id, view);
            }
            view.Root.gameObject.SetActive(visible);
            if (!visible) return;
            view.Root.position = new Vector3(player.Position.x, player.Position.y, -0.27f);
            float progress = player.IsSpawning ? 1f - player.SpawnTimer / GameTuning.SpawnDuration : 1f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
            float fade = Mathf.Clamp01(player.InvulnerabilityTimer / 0.4f);
            view.Ring.transform.localPosition = Vector3.up * CharacterHitbox.BodyElevation;
            view.Ring.transform.localScale = new Vector3(0.72f, 0.24f, 1f) * (1f + pulse * 0.06f);
            view.Ring.color = new Color(0.65f, 0.9f, 1f, (0.45f + pulse * 0.2f) * fade);
            view.Light.transform.localPosition = Vector3.up * 0.40f;
            view.Light.transform.localScale = new Vector3(0.55f, 1.15f, 1f);
            view.Light.color = new Color(0.8f, 0.95f, 1f,
                player.IsSpawning ? Mathf.Sin(progress * Mathf.PI) * 0.45f : 0f);
            for (int i = 0; i < view.Motes.Length; i++)
            {
                float phase = Time.time * 2f + i * 2.4f;
                float height = Mathf.Repeat(Time.time * 0.65f + i / 12f, 1f);
                view.Motes[i].transform.localPosition = new Vector3(Mathf.Cos(phase) * 0.30f,
                    0.08f + height * 0.75f, 0f);
                view.Motes[i].transform.localScale = Vector3.one * (0.018f + pulse * 0.012f);
                view.Motes[i].color = new Color(1f, 1f, 1f,
                    Mathf.Sin(height * Mathf.PI) * fade * (player.IsSpawning ? 0.9f : 0.35f));
            }
        }

        private static SpriteRenderer Create(Transform parent, string name, Sprite sprite, int order)
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
