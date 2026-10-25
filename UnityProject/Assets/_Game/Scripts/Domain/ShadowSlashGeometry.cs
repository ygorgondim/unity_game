using UnityEngine;

namespace WizardRoyale.Domain
{
    // Shared by damage and VFX: 1.4 reach, 0.1 base width, 0.5 end width and rounded front.
    public static class ShadowSlashGeometry
    {
        public const int ArcSegments = 16;
        public const int VertexCount = ArcSegments + 3;
        public const float Range = 1.4f;

        public static Vector2 Vertex(Vector2 origin, Vector2 forward, int index)
        {
            Vector2 side = new Vector2(-forward.y, forward.x);
            if (index == 0) return origin + side * 0.05f;
            if (index == VertexCount - 1) return origin - side * 0.05f;
            float angle = Mathf.PI * 0.5f - (index - 1f) / ArcSegments * Mathf.PI;
            return origin + forward * (Range - 0.25f + Mathf.Cos(angle) * 0.25f)
                + side * (Mathf.Sin(angle) * 0.25f);
        }

        public static bool Intersects(Vector2 position, Vector2 origin, Vector2 forward)
        {
            Vector2 center = CharacterHitbox.Center(position);
            bool inside = false;
            for (int i = 0, j = VertexCount - 1; i < VertexCount; j = i++)
            {
                Vector2 a = Vertex(origin, forward, j), b = Vertex(origin, forward, i);
                if (CharacterHitbox.IntersectsSegment(position, a, b, 0f)) return true;
                if ((a.y > center.y) != (b.y > center.y)
                    && center.x < (b.x - a.x) * (center.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
