const string folder = "Assets/_Game/Resources/Vfx";
const string path = folder + "/SpellVfxSettings.asset";
if (!UnityEditor.AssetDatabase.IsValidFolder(folder)) UnityEditor.AssetDatabase.CreateFolder("Assets/_Game/Resources", "Vfx");
var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<WizardRoyale.Presentation.SpellVfxSettings>(path);
if (asset == null)
{
    asset = ScriptableObject.CreateInstance<WizardRoyale.Presentation.SpellVfxSettings>();
    UnityEditor.AssetDatabase.CreateAsset(asset, path);
    UnityEditor.AssetDatabase.SaveAssets();
}
UnityEditor.Selection.activeObject = asset;
return new { path, profiles = asset.Styles.Length, density = asset.ParticleDensity };
