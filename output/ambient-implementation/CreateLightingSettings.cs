const string folder = "Assets/_Game/Resources/Lighting";
const string path = folder + "/ArenaLightingSettings.asset";
if (!UnityEditor.AssetDatabase.IsValidFolder(folder)) UnityEditor.AssetDatabase.CreateFolder("Assets/_Game/Resources", "Lighting");
var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<WizardRoyale.Presentation.ArenaLightingSettings>(path);
if (settings == null)
{
    settings = ScriptableObject.CreateInstance<WizardRoyale.Presentation.ArenaLightingSettings>();
    UnityEditor.AssetDatabase.CreateAsset(settings, path);
    UnityEditor.AssetDatabase.SaveAssets();
}
UnityEditor.Selection.activeObject = settings;
return new { path, intensity = settings.PersonalLightIntensity };
