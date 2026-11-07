using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AdventureKing.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerInput2D : MonoBehaviour
    {
        public enum TouchAction { Left, Right, Up, Down, Jump, Attack, Interact }
        private bool left, right, up, down, touchJump;
        private bool jumpQueued, attackQueued, interactQueued;
        private bool keyboardJumpHeld;

        public float Horizontal { get; private set; }
        public float Vertical { get; private set; }
        public bool JumpHeld => keyboardJumpHeld || touchJump;

        public void SetTouchHeld(TouchAction action, bool held)
        {
            switch (action)
            {
                case TouchAction.Left: left = held; break;
                case TouchAction.Right: right = held; break;
                case TouchAction.Up: up = held; break;
                case TouchAction.Down: down = held; break;
                case TouchAction.Jump:
                    if (held && !touchJump) jumpQueued = true;
                    touchJump = held;
                    break;
                case TouchAction.Attack:
                    if (held) attackQueued = true;
                    break;
                case TouchAction.Interact:
                    if (held) interactQueued = true;
                    break;
            }
        }

        private void Update()
        {
            float x = 0f, y = 0f;
            bool pressJump = false, holdJump = false, pressAttack = false, pressInteract = false;
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f)
                  - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
                y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f)
                  - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
                pressJump = k.spaceKey.wasPressedThisFrame;
                holdJump = k.spaceKey.isPressed;
                pressAttack = k.jKey.wasPressedThisFrame;
                pressInteract = k.eKey.wasPressedThisFrame;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (Mathf.Abs(stick.x) > Mathf.Abs(x)) x = stick.x;
                if (Mathf.Abs(stick.y) > Mathf.Abs(y)) y = stick.y;
                pressJump |= pad.buttonSouth.wasPressedThisFrame;
                holdJump |= pad.buttonSouth.isPressed;
                pressAttack |= pad.buttonWest.wasPressedThisFrame;
                pressInteract |= pad.buttonNorth.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            x = Input.GetAxisRaw("Horizontal");
            y = Input.GetAxisRaw("Vertical");
            pressJump = Input.GetKeyDown(KeyCode.Space);
            holdJump = Input.GetKey(KeyCode.Space);
            pressAttack = Input.GetKeyDown(KeyCode.J);
            pressInteract = Input.GetKeyDown(KeyCode.E);
#endif
            Horizontal = Mathf.Clamp(x + (right ? 1 : 0) - (left ? 1 : 0), -1f, 1f);
            Vertical = Mathf.Clamp(y + (up ? 1 : 0) - (down ? 1 : 0), -1f, 1f);
            keyboardJumpHeld = holdJump;
            jumpQueued |= pressJump;
            attackQueued |= pressAttack;
            interactQueued |= pressInteract;
        }

        public bool ConsumeJump() { bool v = jumpQueued; jumpQueued = false; return v; }
        public bool ConsumeAttack() { bool v = attackQueued; attackQueued = false; return v; }
        public bool ConsumeInteract() { bool v = interactQueued; interactQueued = false; return v; }

        private void OnDisable()
        {
            left = right = up = down = touchJump = keyboardJumpHeld = false;
            jumpQueued = attackQueued = interactQueued = false;
            Horizontal = Vertical = 0f;
        }
    }
}
