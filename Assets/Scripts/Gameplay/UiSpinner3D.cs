using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Ambient decoration: constant spin plus a gently pulsing tint (stage glyphs, rings).</summary>
    public sealed class UiSpinner3D : MonoBehaviour
    {
        [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 0f, 12f);
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Range(0f, 1f)] private float pulse = 0.25f;
        [SerializeField, Min(0.01f)] private float pulseSpeed = 1.2f;

        private static readonly int TintId = Shader.PropertyToID("_TintColor");
        private MaterialPropertyBlock block;
        private Renderer rend;

        private void Awake()
        {
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
            if (!rend) return;
            Color c = tint;
            c.a *= 1f - pulse * 0.5f * (1f + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f));
            rend.GetPropertyBlock(block);
            block.SetColor(TintId, c);
            rend.SetPropertyBlock(block);
        }
    }
}
