using UnityEngine;

namespace WizardRoyale.Presentation
{
    [CreateAssetMenu(menuName = "Wizard Royale/Spell VFX Settings")]
    public sealed class SpellVfxSettings : ScriptableObject
    {
        [Range(0f, 2f)] public float ParticleDensity = 1f;
        public SpellVisualStyle[] Styles = SpellParticleLibrary.CreateDefaultStyles();
    }
}
