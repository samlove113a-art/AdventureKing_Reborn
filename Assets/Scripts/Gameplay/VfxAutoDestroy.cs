using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// One-shot effect lifetime plus simple procedural motion for mesh-based effects (slash arcs,
    /// glyph rings): scale-up, spin and fade via MaterialPropertyBlock (no material instancing).
    /// </summary>
    public sealed class VfxAutoDestroy : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float lifetime = 0.6f;
        [SerializeField] private Renderer[] fadeRenderers = new Renderer[0];
        [SerializeField] private AnimationCurve alphaOverLife = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private AnimationCurve scaleOverLife = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [SerializeField] private Vector3 spinDegreesPerSecond;
        [SerializeField] private Color tint = Color.white;

        private static readonly int TintId = Shader.PropertyToID("_TintColor");
        private MaterialPropertyBlock block;
        private Vector3 baseScale;
        private float age;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            baseScale = transform.localScale;
            Apply(0f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            transform.Rotate(spinDegreesPerSecond * Time.deltaTime, Space.Self);
            Apply(t);
            if (age >= lifetime) Destroy(gameObject);
        }

        private void Apply(float t)
        {
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            if (scaleOverLife != null && scaleOverLife.length > 0)
                transform.localScale = baseScale * scaleOverLife.Evaluate(t);
            Color c = tint;
            c.a *= alphaOverLife != null && alphaOverLife.length > 0 ? alphaOverLife.Evaluate(t) : 1f;
            foreach (Renderer r in fadeRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(block);
                block.SetColor(TintId, c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
