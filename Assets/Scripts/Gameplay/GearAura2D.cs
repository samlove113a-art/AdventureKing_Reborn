using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Glow behind the hero for well-enhanced gear: soft halo from +7, wider at +10 and +12, a bright pulsing one at +15, in the colour of the gear tier.
    /// Uses the project's own soft_dot picture (Resources/Fx); nothing is drawn by code.
    /// </summary>
    public sealed class GearAura2D : MonoBehaviour
    {
        private SpriteRenderer halo;
        private int level;
        private Color color = Color.white;
        public int Level => level;
        public bool Visible => halo && halo.enabled;
        public static readonly int[] Milestones = { 7, 10, 12, 15 };

        public static int MilestoneOf(int enhanceLevel) { int m = 0; foreach (int x in Milestones) if (enhanceLevel >= x) m = x; return m; }

        public void Set(int enhanceLevel, Color tint)
        {
            level = MilestoneOf(enhanceLevel); color = tint;
            if (!halo)
            {
                var go = new GameObject("GearAura");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                halo = go.AddComponent<SpriteRenderer>();
                var tex = Resources.Load<Texture2D>("Fx/soft_dot");
                if (tex) halo.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width / 2.2f);
                halo.sortingOrder = 3;
            }
            halo.enabled = level > 0 && halo.sprite;
        }

        private void Update()
        {
            if (!halo || !halo.enabled) return;
            float size = level >= 15 ? 4.4f : level >= 12 ? 3.6f : level >= 10 ? 3.0f : 2.4f;
            float baseA = level >= 15 ? 0.62f : level >= 12 ? 0.46f : level >= 10 ? 0.36f : 0.26f;
            float pulse = 1f + (level >= 15 ? 0.14f : 0.06f) * Mathf.Sin(Time.time * (level >= 15 ? 4.2f : 2.4f));
            halo.transform.localScale = Vector3.one * size * pulse;
            halo.color = new Color(color.r, color.g, color.b, baseA * pulse);
        }
    }
}
