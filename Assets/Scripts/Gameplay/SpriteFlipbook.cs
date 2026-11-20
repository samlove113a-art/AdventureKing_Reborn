using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Plays a strip of sprite frames on a SpriteRenderer (pixel-art skill effects). One-shot effects destroy themselves.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFlipbook : MonoBehaviour
    {
        public Sprite[] Frames = new Sprite[0];
        public float Fps = 12f;
        public bool Loop;
        public bool DestroyOnEnd = true;
        private float t;
        private SpriteRenderer sr;

        private void Awake() { sr = GetComponent<SpriteRenderer>(); if (Frames.Length > 0) sr.sprite = Frames[0]; }

        private void Update()
        {
            if (Frames.Length == 0) return;
            t += Time.deltaTime * Fps;
            int i = (int)t;
            if (i >= Frames.Length)
            {
                if (!Loop) { if (DestroyOnEnd) Destroy(gameObject); return; }
                t %= Frames.Length; i = (int)t;
            }
            sr.sprite = Frames[i];
        }
    }
}