var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<WizardRoyale.Presentation.ArenaLightingSettings>("Assets/_Game/Resources/Lighting/ArenaLightingSettings.asset");
if (settings == null) throw new System.InvalidOperationException("ArenaLightingSettings asset is missing.");
UnityEditor.Undo.RecordObject(settings, "Reduce environment darkening by 50 percent");
settings.GroundTint = new Color(0.625f, 0.65f, 0.745f);
settings.OceanTint = new Color(0.61f, 0.66f, 0.765f);
settings.Exposure = -0.15f;
UnityEditor.EditorUtility.SetDirty(settings);
UnityEditor.AssetDatabase.SaveAssets();
return "Environment darkening reduced by 50 percent.";
