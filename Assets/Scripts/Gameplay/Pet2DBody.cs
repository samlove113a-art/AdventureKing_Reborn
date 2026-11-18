using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>2D chibi pet body (Resources/Chibi2D/Pets/{id}_Visual2D): one side-view sprite facing right plus a hungry variant.
    /// PetDirector drives hop / squash / flip; this only swaps the sprite and mirrors it.</summary>
    public class Pet2DBody : MonoBehaviour
    {
        public SpriteRenderer Renderer;
        public Sprite Normal, Hungry;

        public static GameObject Load(string petId) => Resources.Load<GameObject>("Chibi2D/Pets/" + petId + "_Visual2D");

        public void Set(bool hungry, int facing)
        {
            if (!Renderer) return;
            Sprite s = hungry && Hungry ? Hungry : Normal;
            if (Renderer.sprite != s) Renderer.sprite = s;
            Renderer.flipX = facing < 0;
        }
    }
}
