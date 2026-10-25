using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using WizardRoyale.Domain;

public static class PrepareGoldenArena
{
    public static string Main()
    {
        const string folder = "Assets/_Game/Resources/Art/GoldenArena/";
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        GoldenArenaLayout layout = GoldenArenaLayout.Current;
        int w = layout.width, h = layout.height;
        Color32[] maskPixels = new Color32[w * h];
        Color32 black = new Color32(0, 0, 0, 255);
        Color32 green = new Color32(0, 255, 0, 255);
        for (int i = 0; i < maskPixels.Length; i++) maskPixels[i] = black;
        var crossings = new List<float>();
        for (int y = 0; y < h; y++)
        {
            float scan = y + 0.5f;
            crossings.Clear();
            for (int i = 0, j = layout.floor.Length - 1; i < layout.floor.Length; j = i++)
            {
                Vector2 a = layout.floor[j], b = layout.floor[i];
                if ((a.y > scan) != (b.y > scan))
                    crossings.Add(a.x + (scan - a.y) * (b.x - a.x) / (b.y - a.y));
            }
            crossings.Sort();
            for (int pair = 0; pair + 1 < crossings.Count; pair += 2)
            {
                int start = Mathf.Max(0, Mathf.CeilToInt(crossings[pair] - 0.5f));
                int end = Mathf.Min(w, Mathf.CeilToInt(crossings[pair + 1] - 0.5f));
                for (int x = start; x < end; x++) maskPixels[(h - 1 - y) * w + x] = green;
            }
        }
        var mask = new Texture2D(w, h, TextureFormat.RGBA32, false);
        mask.SetPixels32(maskPixels);
        mask.Apply();
        string output = Path.GetFullPath("../output/golden-arena/");
        File.WriteAllBytes(folder + "floor-mask.png", mask.EncodeToPNG());
        File.Copy(folder + "floor-mask.png", output + "floor-mask.png", true);

        var arena = new Texture2D(2, 2);
        arena.LoadImage(File.ReadAllBytes(folder + "arena.png"));
        var ocean = new Texture2D(2, 2);
        ocean.LoadImage(File.ReadAllBytes(folder + "ocean.png"));
        Color32[] ground = arena.GetPixels32(), water = ocean.GetPixels32();
        int transparent = 0, opaque = 0;
        for (int i = 0; i < water.Length; i++)
        {
            water[i].a = 255;
            if (ground[i].a == 0) transparent++;
            if (ground[i].a == 255) opaque++;
        }
        ocean.SetPixels32(water);
        ocean.Apply();
        File.WriteAllBytes(folder + "ocean.png", ocean.EncodeToPNG());
        File.Copy(folder + "ocean.png", output + "ocean.png", true);
        var composite = new Color32[w * h];
        var preview = new Color32[w * h];
        for (int i = 0; i < composite.Length; i++)
        {
            Color baseColor = Color.Lerp(water[i], ground[i], ground[i].a / 255f);
            baseColor.a = 1f;
            composite[i] = baseColor;
            Color tint = maskPixels[i].g > 0 ? new Color(0f, 1f, 0.2f) : new Color(1f, 0.05f, 0.12f);
            preview[i] = Color.Lerp(baseColor, tint, maskPixels[i].g > 0 ? 0.32f : 0.38f);
        }
        mask.SetPixels32(composite);
        mask.Apply();
        File.WriteAllBytes(output + "composite.png", mask.EncodeToPNG());
        mask.SetPixels32(preview);
        mask.Apply();
        File.WriteAllBytes(output + "walkable-preview.png", mask.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(mask);
        UnityEngine.Object.DestroyImmediate(arena);
        UnityEngine.Object.DestroyImmediate(ocean);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string name in new[] { "arena", "ocean", "floor-mask" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(folder + name + ".png");
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = name == "arena";
            importer.wrapMode = name == "ocean" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = name == "floor-mask" ? FilterMode.Point : FilterMode.Bilinear;
            importer.isReadable = name == "floor-mask";
            importer.sRGBTexture = name != "floor-mask";
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        return "Prepared 2710x2320 layers, shared floor mask and previews. Arena alpha: " + transparent + " transparent / " + opaque + " opaque pixels.";
    }
}
