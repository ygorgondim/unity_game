using UnityEngine;
using WizardRoyale.Domain;

namespace WizardRoyale.Presentation
{
    public static class SwampArenaPresentation
    {
        public static void Build(Transform parent, GoldenArenaLayout layout)
        {
            Sprite background = LoadLayer(layout, "background");
            Vector2 size = background.bounds.size;
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                SpriteRenderer renderer = Node(parent, "Swamp Background", background, -3000);
                renderer.transform.position = (Vector3)layout.ArtCenter + new Vector3(x * size.x, y * size.y, 0f);
                renderer.flipX = x != 0;
                renderer.flipY = y != 0;
            }
            SpriteRenderer floor = Node(parent, "Swamp Platform - Floor and Cliffs", LoadLayer(layout, "arena"), -1000);
            floor.transform.position = layout.ArtCenter;
            GameObject contour = new GameObject("Exact Walkable Floor - Outside Is Fall Zone");
            contour.transform.SetParent(parent, false);
            PolygonCollider2D trigger = contour.AddComponent<PolygonCollider2D>();
            trigger.isTrigger = true;
            Vector2[] points = new Vector2[layout.floor.Length];
            for (int i = 0; i < points.Length; i++) points[i] = layout.PixelToWorld(layout.floor[i]);
            trigger.points = points;
            for (int i = 0; i < layout.props.Length; i++) BuildProp(parent, layout, layout.props[i], i);
            for (int i = 0; i < layout.foliage.Length; i++)
            {
                GoldenArenaLayout.ArenaProp plant = layout.foliage[i];
                Texture2D texture = Resources.Load<Texture2D>(layout.resourceRoot + "/Foliage/plant_" + i);
                if (texture == null) throw new System.InvalidOperationException("Missing swamp foliage: " + plant.name);
                Vector4 box = plant.rect;
                Vector2 pivot = new Vector2((plant.basePixel.x - box.x) / box.z,
                    1f - (plant.basePixel.y - box.y) / box.w);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot,
                    texture.height / (box.w / layout.pixelsPerUnit), 0, SpriteMeshType.FullRect);
                Vector2 basePosition = layout.PixelToWorld(plant.basePixel);
                SpriteRenderer renderer = Node(parent, plant.name + " - Decorative Only", sprite, ArenaDepth.Order(basePosition.y));
                renderer.transform.position = basePosition;
            }
        }

        private static void BuildProp(Transform parent, GoldenArenaLayout layout, GoldenArenaLayout.ArenaProp prop, int index)
        {
            Texture2D texture = Resources.Load<Texture2D>(layout.resourceRoot + "/Props/prop_" + index);
            if (texture == null) throw new System.InvalidOperationException("Missing swamp prop: " + prop.name);
            Vector4 rect = prop.rect;
            Vector2 pivot = new Vector2((prop.basePixel.x - rect.x) / rect.z,
                1f - (prop.basePixel.y - rect.y) / rect.w);
            float ppu = texture.height / (rect.w / layout.pixelsPerUnit);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot,
                ppu, 0, SpriteMeshType.FullRect);
            Vector2 position = layout.PixelToWorld(prop.basePixel);
            SpriteRenderer renderer = Node(parent, prop.name, sprite, ArenaDepth.Order(position.y));
            renderer.transform.position = position;
            foreach (Vector3 circle in prop.collisionPixels)
            {
                GameObject node = new GameObject(prop.name + " - Base Collision");
                node.transform.SetParent(renderer.transform, false);
                node.transform.position = layout.PixelToWorld(new Vector2(circle.x, circle.y));
                node.AddComponent<CircleCollider2D>().radius = circle.z / layout.pixelsPerUnit;
            }
        }

        private static Sprite LoadLayer(GoldenArenaLayout layout, string name)
        {
            Texture2D texture = Resources.Load<Texture2D>(layout.resourceRoot + "/" + name);
            if (texture == null) throw new System.InvalidOperationException("Missing swamp layer: " + name);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f,
                texture.width / (layout.width / layout.pixelsPerUnit), 0, SpriteMeshType.FullRect);
        }

        private static SpriteRenderer Node(Transform parent, string name, Sprite sprite, int order)
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
