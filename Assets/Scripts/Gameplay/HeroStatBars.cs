using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Animated stat bars for the selected class (HP, nominal DPS, defense, range), normalised to the
    /// strongest class in each stat so the four roles read at a glance. Display only; the server owns
    /// real balance values.
    /// </summary>
    public sealed class HeroStatBars : MonoBehaviour
    {
        [SerializeField] private HeroSelectionPreview3D preview;
        [SerializeField] private RectTransform[] fills = new RectTransform[4];
        [SerializeField, Min(1f)] private float fullWidth = 200f;
        [SerializeField, Min(0.1f)] private float animateSpeed = 6f;

        private readonly float[] targets = new float[4];
        private HeroClass? shown;

        private void Update()
        {
            if (preview && preview.ActiveClass != shown)
            {
                shown = preview.ActiveClass;
                if (shown.HasValue) SetTargets(shown.Value);
            }
            for (int i = 0; i < fills.Length && i < targets.Length; i++)
            {
                if (!fills[i]) continue;
                Vector2 s = fills[i].sizeDelta;
                s.x = Mathf.Lerp(s.x, targets[i] * fullWidth, 1f - Mathf.Exp(-animateSpeed * Time.unscaledDeltaTime));
                fills[i].sizeDelta = s;
            }
        }

        private void SetTargets(HeroClass c)
        {
            float maxHp = 1, maxDps = 1, maxDef = 1, maxRange = 1;
            for (int i = 0; i < HeroClassCatalog.Count; i++)
            {
                HeroClassStats s = HeroClassCatalog.At(i);
                maxHp = Mathf.Max(maxHp, s.startingHealth);
                maxDps = Mathf.Max(maxDps, s.NominalDamagePerSecond);
                maxDef = Mathf.Max(maxDef, s.startingDefense);
                maxRange = Mathf.Max(maxRange, s.attackRange);
            }
            HeroClassStats st = HeroClassCatalog.Get(c);
            targets[0] = st.startingHealth / maxHp;
            targets[1] = st.NominalDamagePerSecond / maxDps;
            targets[2] = st.startingDefense / maxDef;
            targets[3] = st.attackRange / maxRange;
        }
    }
}
