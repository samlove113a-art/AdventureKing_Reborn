using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AdventureKing.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerInput2D : MonoBehaviour
    {
        // (appended values only: scenes serialize these as ints)
        public enum TouchAction { Left, Right, Up, Down, Jump, Attack, Interact, Skill1, Skill2, Skill3, Pickup, PotionHp, PotionMp, Inventory, Pet }
        private readonly bool[] skillQueued = new bool[3];
        private bool pickupQueued, potionHpQueued, potionMpQueued, inventoryQueued, petQueued;

        /// <summary>PC keys: 1/2/3 skills, ↓ or S pickup (only acted on when not climbing), Q HP potion, R MP potion, I bag.</summary>
        public bool ConsumeSkill(int slot) { if (slot < 1 || slot > 3) return false; bool v = skillQueued[slot - 1]; skillQueued[slot - 1] = false; return v; }
        public bool ConsumePickup() { bool v = pickupQueued; pickupQueued = false; return v; }
        public bool ConsumePotionHp() { bool v = potionHpQueued; potionHpQueued = false; return v; }
        public bool ConsumePotionMp() { bool v = potionMpQueued; potionMpQueued = false; return v; }
        public bool ConsumeInventory() { bool v = inventoryQueued; inventoryQueued = false; return v; }
        public bool ConsumePet() { bool v = petQueued; petQueued = false; return v; }
        private bool left, right, up, down, touchJump;
        private bool jumpQueued, attackQueued, interactQueued;
        private bool keyboardJumpHeld;
        // QA fix: an Interact press that nobody consumes must expire, otherwise pressing E
        // far away from an NPC fires the NPC later when the player walks into its trigger.
        private const int InteractBufferFrames = 2;
        private int interactQueuedFrame = int.MinValue;

        public float Horizontal { get; private set; }
        public float Vertical { get; private set; }
        public bool JumpHeld => keyboardJumpHeld || touchJump;

        private float stickX, stickY;

        /// <summary>On-screen joystick. Horizontal snaps to full run past a small tilt (run playback is tuned to
        /// full speed, so a half-tilt would slide the feet); vertical needs a clear push so running diagonally
        /// never grabs a ladder by accident.</summary>
        public void SetStick(Vector2 v)
        {
            stickX = Mathf.Abs(v.x) > 0.3f ? Mathf.Sign(v.x) : 0f;
            stickY = Mathf.Abs(v.y) > 0.6f ? Mathf.Sign(v.y) : 0f;
        }

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
                    if (held) QueueInteract();
                    break;
                case TouchAction.Skill1: if (held) skillQueued[0] = true; break;
                case TouchAction.Skill2: if (held) skillQueued[1] = true; break;
                case TouchAction.Skill3: if (held) skillQueued[2] = true; break;
                case TouchAction.Pickup: if (held) pickupQueued = true; break;
                case TouchAction.PotionHp: if (held) potionHpQueued = true; break;
                case TouchAction.PotionMp: if (held) potionMpQueued = true; break;
                case TouchAction.Inventory: if (held) inventoryQueued = true; break;
                case TouchAction.Pet: if (held) petQueued = true; break;
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
                if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) skillQueued[0] = true;
                if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) skillQueued[1] = true;
                if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) skillQueued[2] = true;
                if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) pickupQueued = true;
                if (k.qKey.wasPressedThisFrame) potionHpQueued = true;
                if (k.rKey.wasPressedThisFrame) potionMpQueued = true;
                if (k.iKey.wasPressedThisFrame) inventoryQueued = true;
                if (k.pKey.wasPressedThisFrame) petQueued = true;
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
            Horizontal = Mathf.Clamp(x + stickX + (right ? 1 : 0) - (left ? 1 : 0), -1f, 1f);
            Vertical = Mathf.Clamp(y + stickY + (up ? 1 : 0) - (down ? 1 : 0), -1f, 1f);
            keyboardJumpHeld = holdJump;
            jumpQueued |= pressJump;
            attackQueued |= pressAttack;
            if (pressInteract) QueueInteract();
            if (interactQueued && Time.frameCount - interactQueuedFrame > InteractBufferFrames)
                interactQueued = false;
        }

        private void QueueInteract()
        {
            interactQueued = true;
            interactQueuedFrame = Time.frameCount;
        }

        public bool ConsumeJump() { bool v = jumpQueued; jumpQueued = false; return v; }
        public bool ConsumeAttack() { bool v = attackQueued; attackQueued = false; return v; }
        public bool ConsumeInteract()
        {
            bool v = interactQueued && Time.frameCount - interactQueuedFrame <= InteractBufferFrames;
            interactQueued = false;
            return v;
        }

        private void OnDisable()
        {
            left = right = up = down = touchJump = keyboardJumpHeld = false;
            stickX = stickY = 0f;
            jumpQueued = attackQueued = interactQueued = false;
            skillQueued[0] = skillQueued[1] = skillQueued[2] = false;
            pickupQueued = potionHpQueued = potionMpQueued = inventoryQueued = petQueued = false;
            Horizontal = Vertical = 0f;
        }
    }
}
