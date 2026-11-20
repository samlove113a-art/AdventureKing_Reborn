using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Fades an NPC's floating name plate while it sits under a HUD corner cluster (player frame top-left,
    /// minimap + shortcut icons top-right), so world text never fights the HUD for the same pixels.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class NamePlateFade : MonoBehaviour
    {
        private CanvasGroup group;
        private Camera cam;

        /// <summary>True when a viewport point lies under one of the HUD corner clusters.</summary>
        public static bool UnderHud(Vector3 vp) =>
            vp.y > 0.55f && vp.x > 0.66f ||   // minimap + shortcut grid
            vp.y > 0.78f && vp.x < 0.38f;     // player frame + purse

        private void Awake() => group = GetComponent<CanvasGroup>();

        private void LateUpdate()
        {
            if (!cam) cam = Camera.main;
            if (!cam) return;
            Vector3 vp = cam.WorldToViewportPoint(transform.position);
            float target = UnderHud(vp) ? 0.18f : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 4f);
        }
    }
}
