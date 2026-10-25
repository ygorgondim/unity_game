using System.Collections.Generic;
using UnityEngine;
using WizardRoyale.Domain;

namespace WizardRoyale.Presentation
{
    // World-space effects: camera motion changes framing, never the position of water highlights or clouds.
    public sealed class ArenaWaterAtmosphere
    {
        private static readonly int IslandTextureId = Shader.PropertyToID("_IslandTex");
        private static readonly int IslandRectId = Shader.PropertyToID("_IslandRect");
        private static readonly int AmbientTimeId = Shader.PropertyToID("_AmbientTime");
        private readonly List<Object> ownedAssets = new List<Object>();
        private readonly List<Cloud> clouds = new List<Cloud>();
        private readonly System.Random random = new System.Random(2941);
        private readonly Camera camera;
        private readonly Transform root;
        private readonly float minimumX;
        private readonly float maximumX;
        private Material reflectionMaterial;
        private Transform reflections;

        private sealed class Cloud
        {
            public SpriteRenderer Body;
            public SpriteRenderer Shadow;
            public Vector2 Anchor;
            public float Width;
            public float Height;
            public float Speed;
            public float Phase;
            public float Opacity;
        }

        public ArenaWaterAtmosphere(Transform parent, Camera targetCamera, GoldenArenaLayout layout)
        {
            camera = targetCamera;
            root = new GameObject("Water - Sunset Reflections and Passing Clouds").transform;
            root.SetParent(parent, false);
            Vector2 artSize = new Vector2(layout.width, layout.height) / layout.pixelsPerUnit;
            Vector2 artMinimum = layout.ArtCenter - artSize * 0.5f;
            Vector4 islandRect = new Vector4(artMinimum.x, artMinimum.y, 1f / artSize.x, 1f / artSize.y);
            Texture2D islandTexture = Resources.Load<Texture2D>(layout.resourceRoot + "/arena");
            minimumX = layout.ArtCenter.x - artSize.x * 0.5f - 8f;
            maximumX = layout.ArtCenter.x + artSize.x * 0.5f + 8f;
            if (islandTexture == null)
            {
                Debug.LogWarning("Arena art missing: water atmosphere skipped to avoid drawing over land.");
                return;
            }

            Shader reflectionShader = Resources.Load<Shader>("Shaders/AmbientWaterReflections");
            if (reflectionShader != null)
            {
                reflectionMaterial = CreateMaterial(reflectionShader, islandTexture, islandRect);
                Sprite surface = Own(Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                    Vector2.one * 0.5f, 1f, 0, SpriteMeshType.FullRect));
                SpriteRenderer renderer = CreateRenderer("Golden Water Shimmer", surface, reflectionMaterial, -25);
                renderer.color = Color.white;
                reflections = renderer.transform;
            }

            Shader cloudShader = Resources.Load<Shader>("Shaders/AmbientCloud");
            if (cloudShader == null) return;
            Material cloudMaterial = CreateMaterial(cloudShader, islandTexture, islandRect);
            cloudMaterial.SetFloat("_WaterOnly", 1f);
            Material shadowMaterial = CreateMaterial(cloudShader, islandTexture, islandRect);
            shadowMaterial.SetFloat("_WaterOnly", 0f);

            Sprite[] shapes = { CreateCloudSprite(0), CreateCloudSprite(1), CreateCloudSprite(2) };
            for (int i = 0; i < 12; i++)
            {
                Sprite shape = shapes[i % shapes.Length];
                // Keep some shadows crossing the arena from the beginning, with the other clouds over the water.
                float cloudY = i >= 4 && i <= 7
                    ? layout.ArtCenter.y + Range(-artSize.y * 0.20f, artSize.y * 0.20f)
                    : layout.ArtCenter.y + Range(-artSize.y * 0.5f - 2f, artSize.y * 0.5f + 2f);
                Cloud cloud = new Cloud
                {
                    Body = CreateRenderer("Translucent Water Cloud " + (i + 1), shape, cloudMaterial, 10),
                    Shadow = CreateRenderer("Drifting Cloud Shadow " + (i + 1), shape, shadowMaterial, -8),
                    Anchor = new Vector2(Mathf.Lerp(minimumX, maximumX, (i + 0.5f) / 12f),
                        cloudY),
                    Width = Range(3.2f, 5.8f),
                    Height = Range(1.4f, 2.7f),
                    Speed = Range(0.075f, 0.145f),
                    Phase = Range(0f, Mathf.PI * 2f),
                    Opacity = Range(0.30f, 0.45f)
                };
                cloud.Body.flipX = cloud.Shadow.flipX = i % 2 == 0;
                clouds.Add(cloud);
            }
        }

        public void Tick(float time)
        {
            if (reflections != null)
            {
                reflections.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
                float height = camera.orthographicSize * 2f + 2f;
                reflections.localScale = new Vector3(height * camera.aspect + 2f, height, 1f);
                reflectionMaterial.SetFloat(AmbientTimeId, time);
            }

            float span = maximumX - minimumX;
            foreach (Cloud cloud in clouds)
            {
                float x = minimumX + Mathf.Repeat(cloud.Anchor.x - minimumX + time * cloud.Speed, span);
                float y = cloud.Anchor.y + Mathf.Sin(time * 0.10f + cloud.Phase) * 0.18f;
                float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Min(x - minimumX, maximumX - x) / 2f);
                float breath = 0.94f + Mathf.Sin(time * 0.18f + cloud.Phase) * 0.06f;
                cloud.Body.transform.localPosition = new Vector3(x, y, 0f);
                cloud.Body.transform.localScale = new Vector3(cloud.Width, cloud.Height * 2f, 1f);
                cloud.Body.color = new Color(0.48f, 0.57f, 0.78f, cloud.Opacity * 0.65f * breath * edgeFade);
                cloud.Shadow.transform.localPosition = new Vector3(x + 0.55f, y - 0.40f, 0f);
                cloud.Shadow.transform.localScale = new Vector3(cloud.Width * 1.12f, cloud.Height * 2.2f, 1f);
                cloud.Shadow.color = new Color(0.045f, 0.06f, 0.16f, 0.14f * breath * edgeFade);
            }
        }

        private Material CreateMaterial(Shader shader, Texture2D islandTexture, Vector4 islandRect)
        {
            Material material = Own(new Material(shader));
            material.SetTexture(IslandTextureId, islandTexture);
            material.SetVector(IslandRectId, islandRect);
            return material;
        }

        private SpriteRenderer CreateRenderer(string label, Sprite sprite, Material material, int order)
        {
            GameObject node = new GameObject(label);
            node.transform.SetParent(root, false);
            SpriteRenderer renderer = node.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.color = Color.clear;
            return renderer;
        }

        private Sprite CreateCloudSprite(int variant)
        {
            const int width = 256;
            const int height = 128;
            Texture2D texture = Own(new Texture2D(width, height, TextureFormat.RGBA32, false));
            texture.name = "Soft Anime Cloud " + variant;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Vector4[] puffs =
            {
                new Vector4(-0.62f, -0.12f, 0.30f, 0.38f),
                new Vector4(-0.36f, 0.10f, 0.34f, 0.48f),
                new Vector4(-0.05f, 0.24f, 0.36f, 0.55f),
                new Vector4(0.28f, 0.13f, 0.34f, 0.46f),
                new Vector4(0.60f, -0.12f, 0.31f, 0.36f),
                new Vector4(-0.22f, -0.23f, 0.48f, 0.34f),
                new Vector4(0.28f, -0.22f, 0.42f, 0.34f)
            };
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float nx = (x + 0.5f) / width * 2f - 1f;
                float ny = (y + 0.5f) / height * 2f - 1f;
                float alpha = 0f;
                for (int p = 0; p < puffs.Length; p++)
                {
                    Vector4 puff = puffs[p];
                    float shift = Mathf.Sin(variant * 2.4f + p * 1.8f) * 0.07f;
                    float dx = (nx - puff.x) / puff.z;
                    float dy = (ny - puff.y - shift) / puff.w;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    alpha = Mathf.Max(alpha, 1f - Mathf.SmoothStep(0f, 1f, (distance - 0.40f) / 0.60f));
                }
                alpha *= 0.92f + Mathf.PerlinNoise(nx * 5f + variant * 3f, ny * 5f + 10f) * 0.08f;
                Color color = Color.Lerp(new Color(0.70f, 0.72f, 0.86f),
                    new Color(1f, 0.97f, 0.91f), Mathf.SmoothStep(0f, 1f, (ny + 0.65f) / 1.2f));
                color.a = alpha;
                pixels[y * width + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Own(Sprite.Create(texture, new Rect(0f, 0f, width, height),
                Vector2.one * 0.5f, width, 0, SpriteMeshType.FullRect));
            sprite.name = texture.name;
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

        public void Dispose()
        {
            foreach (Object asset in ownedAssets)
                if (asset != null) Object.Destroy(asset);
            ownedAssets.Clear();
        }
    }
}
