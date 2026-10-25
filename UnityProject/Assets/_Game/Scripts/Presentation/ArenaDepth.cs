using UnityEngine;
using UnityEngine.Rendering;
using WizardRoyale.Domain;

namespace WizardRoyale.Presentation
{
    public static class ArenaDepth
    {
        public static int Order(float groundY) => 1000 - Mathf.RoundToInt(groundY * 100f);

        public static void Set(Transform target, float groundY, bool sinking = false)
        {
            if (!GoldenArenaLayout.Current.swamp) return;
            SortingGroup group = target.GetComponent<SortingGroup>();
            if (group == null) group = target.gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = sinking ? -1100 : Order(groundY);
        }
    }
}
