using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>Marks a visual prefab as a 2D chibi sprite body (side view, flipped by scale instead of turned by yaw).</summary>
    public sealed class Chibi2DMarker : MonoBehaviour
    {
        public SpriteRenderer Renderer;
        /// <summary>Extra size on top of the hero scale: big-head chibi sprites read at a larger size than the 3D body and match the NPC height.</summary>
        public float Scale = 1.05f;

        public static float ScaleOf(GameObject model) { var m = model ? model.GetComponent<Chibi2DMarker>() : null; return m ? m.Scale : 1f; }
    }
}