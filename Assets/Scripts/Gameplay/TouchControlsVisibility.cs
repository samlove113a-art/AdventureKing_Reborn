using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>On-screen controls appear on phones/tablets (and any device with a touchscreen), not on desktop PC.</summary>
    public sealed class TouchControlsVisibility : MonoBehaviour
    {
        [Tooltip("Force the touch controls on (e.g. to test them with a mouse in the editor).")]
        public bool forceVisible;

        public static bool TouchPreferred
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return true;
#else
                return Application.isMobilePlatform ||
                       (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Mouse.current == null);
#endif
            }
        }

        private void Awake() => gameObject.SetActive(forceVisible || TouchPreferred);
    }
}
