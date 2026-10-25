using UnityEngine;
using WizardRoyale.Domain;

namespace WizardRoyale.Presentation
{
    // The filled cone and curved edge use exactly the same polygon as the damaging cut.
    public sealed class ShadowSlashView : MonoBehaviour
    {
        private Mesh mesh;
        private Color[] colors;
        private LineRenderer edge;
        private readonly SpriteRenderer[] bubbles = new SpriteRenderer[18];
        private readonly SpriteRenderer[] rims = new SpriteRenderer[18];

        public void Initialize(Material material, Sprite circle, Sprite ring)
        {
            mesh = new Mesh { name = "Shadow Slash Cone" };
            Vector3[] vertices = new Vector3[ShadowSlashGeometry.VertexCount + 1];
            colors = new Color[vertices.Length];
            int[] triangles = new int[ShadowSlashGeometry.VertexCount * 3];
            vertices[0] = new Vector3(0.6f, 0f, 0f);
            for (int i = 0; i < ShadowSlashGeometry.VertexCount; i++)
            {
                Vector2 point = ShadowSlashGeometry.Vertex(Vector2.zero, Vector2.right, i);
                vertices[i + 1] = new Vector3(point.x, point.y, 0f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % ShadowSlashGeometry.VertexCount + 1;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = new Vector2[vertices.Length];
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 7;
            GameObject border = new GameObject("Violet Arched Edge");
            border.transform.SetParent(transform, false);
            edge = border.AddComponent<LineRenderer>();
            edge.sharedMaterial = material;
            edge.useWorldSpace = false;
            edge.loop = true;
            edge.positionCount = ShadowSlashGeometry.VertexCount;
            edge.startWidth = edge.endWidth = 0.025f;
            edge.sortingOrder = 8;
            for (int i = 0; i < edge.positionCount; i++) edge.SetPosition(i, vertices[i + 1]);
            for (int i = 0; i < bubbles.Length; i++)
            {
                rims[i] = Create("Violet Bubble Rim", ring, 9);
                bubbles[i] = Create("Black Shadow Bubble", circle, 10);
            }
        }

        public void Sync(ProjectileState projectile)
        {
            float t = Mathf.Clamp01(1f - projectile.Life / projectile.MaxLife);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, t));
            transform.position = new Vector3(projectile.BeamStart.x, projectile.BeamStart.y, -0.32f);
            transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(projectile.Velocity.y, projectile.Velocity.x) * Mathf.Rad2Deg);
            transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(t * 6f));
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0f, 0f, 0f, fade * 0.97f);
            mesh.colors = colors;
            edge.startColor = edge.endColor = new Color(0.55f, 0.08f, 0.9f, fade * 0.9f);
            for (int i = 0; i < bubbles.Length; i++)
            {
                float along = 0.12f + (i % 7) / 6f * 1.13f;
                float spread = Mathf.Lerp(0.05f, 0.25f, along / 1.4f);
                float side = Mathf.Sin(i * 7.1f) * spread;
                Vector3 position = new Vector3(along + t * (0.10f + i % 3 * 0.07f),
                    side + Mathf.Sin(i * 2.3f) * t * 0.18f, -0.01f);
                float size = (0.045f + (i % 4) * 0.022f) * (1f + t * 0.7f);
                bubbles[i].transform.localPosition = rims[i].transform.localPosition = position;
                bubbles[i].transform.localScale = Vector3.one * size;
                rims[i].transform.localScale = Vector3.one * (size * 1.12f);
                bubbles[i].color = new Color(0f, 0f, 0f, fade);
                rims[i].color = new Color(0.45f, 0.035f, 0.75f, fade * 0.7f);
            }
        }

        private SpriteRenderer Create(string name, Sprite sprite, int order)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(transform, false);
            SpriteRenderer renderer = node.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
