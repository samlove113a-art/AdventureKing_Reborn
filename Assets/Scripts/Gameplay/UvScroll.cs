using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Scrolls a renderer's base map (streams, waterfalls). Uses one material instance per renderer.</summary>
    public sealed class UvScroll : MonoBehaviour
    {
        public Vector2 speed = new Vector2(0.15f, 0f);
        private Material mat;
        private Vector2 offset;
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        private void Awake()
        {
            var r = GetComponent<Renderer>();
            if (r) mat = r.material;
        }

        private void Update()
        {
            if (!mat) return;
            offset += speed * Time.deltaTime;
            offset.x %= 1f;
            offset.y %= 1f;
            Vector4 st = mat.GetVector(BaseMapSt);
            mat.SetVector(BaseMapSt, new Vector4(st.x, st.y, offset.x, offset.y));
        }

        private void OnDestroy()
        {
            if (mat) Destroy(mat);
        }
    }
}
