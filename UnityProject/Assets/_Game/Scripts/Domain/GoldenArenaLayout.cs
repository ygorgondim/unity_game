using System;
using UnityEngine;

namespace WizardRoyale.Domain
{
    /// <summary>Shared pixel-to-world mapping for artwork, floor mask and gameplay. Coordinates use the top-left of the PNG.</summary>
    [Serializable]
    public sealed class GoldenArenaLayout
    {
        public int width;
        public int height;
        public float pixelsPerUnit;
        public Vector2 origin;
        public Vector2[] floor;
        public string resourceRoot = "Art/GoldenArena";
        public bool swamp;
        public Vector2[] spawnPixels;
        public ArenaProp[] props;
        public ArenaProp[] foliage;
        private Texture2D floorMask;

        [Serializable]
        public sealed class ArenaProp
        {
            public string name;
            public Vector4 rect;
            public Vector2 basePixel;
            public Vector3[] collisionPixels;
        }

        private static GoldenArenaLayout current;
        public static GoldenArenaLayout Current
        {
            get
            {
                if (current == null)
                {
                    TextAsset asset = Resources.Load<TextAsset>("Art/SwampArena/layout");
                    if (asset == null) asset = Resources.Load<TextAsset>("Art/GoldenArena/layout");
                    if (asset == null) throw new InvalidOperationException("Golden arena layout is missing.");
                    current = JsonUtility.FromJson<GoldenArenaLayout>(asset.text);
                    if (string.IsNullOrEmpty(current.resourceRoot)) current.resourceRoot = "Art/GoldenArena";
                    if (current.floor == null || current.floor.Length < 3 || current.pixelsPerUnit <= 0f)
                        throw new InvalidOperationException("Golden arena layout is invalid.");
                }
                return current;
            }
        }

        public Vector2 PixelToWorld(Vector2 pixel) =>
            new Vector2(pixel.x - origin.x, origin.y - pixel.y) / pixelsPerUnit;

        public Vector2 WorldToPixel(Vector2 world) =>
            new Vector2(origin.x + world.x * pixelsPerUnit, origin.y - world.y * pixelsPerUnit);

        public Vector2 ArtCenter => PixelToWorld(new Vector2(width, height) * 0.5f);

        public Texture2D FloorMaskTexture
        {
            get
            {
                if (!swamp) return Resources.Load<Texture2D>(resourceRoot + "/floor-mask");
                if (floorMask != null) return floorMask;
                const int w = 384;
                int h = Mathf.RoundToInt(w * height / (float)width);
                floorMask = new Texture2D(w, h, TextureFormat.RGBA32, false);
                floorMask.name = "Swamp - Shared Walkable Mask";
                floorMask.wrapMode = TextureWrapMode.Clamp;
                Color[] pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = ContainsPixel(new Vector2((x + 0.5f) / w * width,
                        (1f - (y + 0.5f) / h) * height)) ? Color.white : Color.clear;
                floorMask.SetPixels(pixels);
                floorMask.Apply();
                return floorMask;
            }
        }

        public bool ContainsWorld(Vector2 position) => ContainsPixel(WorldToPixel(position));

        public bool ContainsPixel(Vector2 pixel)
        {
            bool inside = false;
            for (int i = 0, j = floor.Length - 1; i < floor.Length; j = i++)
            {
                Vector2 a = floor[j];
                Vector2 b = floor[i];
                Vector2 edge = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(pixel - a, edge) / edge.sqrMagnitude);
                if ((pixel - (a + edge * t)).sqrMagnitude <= 0.0001f) return true;
                if ((a.y > pixel.y) != (b.y > pixel.y) &&
                    pixel.x < (b.x - a.x) * (pixel.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        public Vector2 ClosestFloorPoint(Vector2 position, out Vector2 inwardNormal)
        {
            Vector2 closest = Vector2.zero;
            inwardNormal = Vector2.zero;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < floor.Length; i++)
            {
                Vector2 a = PixelToWorld(floor[i]);
                Vector2 b = PixelToWorld(floor[(i + 1) % floor.Length]);
                Vector2 edge = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(position - a, edge) / edge.sqrMagnitude);
                Vector2 candidate = a + edge * t;
                float distance = (position - candidate).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                closest = candidate;
                // Pixel contour runs clockwise; flipping Y makes the world contour clockwise's opposite.
                inwardNormal = new Vector2(edge.y, -edge.x).normalized;
                if (!ContainsWorld(candidate + inwardNormal * 0.01f)) inwardNormal = -inwardNormal;
            }
            return closest;
        }
    }
}
