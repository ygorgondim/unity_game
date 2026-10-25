using UnityEngine;

namespace WizardRoyale.Presentation
{
    [CreateAssetMenu(menuName = "Wizard Royale/Arena Lighting Settings")]
    public sealed class ArenaLightingSettings : ScriptableObject
    {
        public Color GroundTint = new Color(0.625f, 0.65f, 0.745f);
        public Color OceanTint = new Color(0.61f, 0.66f, 0.765f);
        public Color SunColor = new Color(1f, 0.68f, 0.32f);
        public Color PersonalLightColor = new Color(0.75f, 0.82f, 1f);
        [Range(-2f, 1f)] public float Exposure = -0.15f;
        [Range(0f, 2f)] public float SunbeamIntensity = 0.85f;
        [Range(0f, 2f)] public float PersonalLightIntensity = 0.8f;
        public Vector2 PersonalLightSize = new Vector2(2.8f, 1.9f);
        [Range(0f, 1f)] public float BloomIntensity = 0.30f;
    }
}
