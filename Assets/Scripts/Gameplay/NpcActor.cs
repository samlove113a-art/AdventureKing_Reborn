using System.Collections;
using UnityEngine;

namespace AdventureKing.Gameplay
{
    /// <summary>
    /// Town NPC body: idles, walks to a point at a speed matched to its walk clip (no foot sliding), turns to face
    /// someone, and plays one-shot gestures (wave, bow) or a talk loop. Animator states: Idle, Walk, Wave, Talk, Bow.
    /// Facing uses the same side-view yaw convention as heroes (CharacterVisual3D): +110° right, -110° left.
    /// </summary>
    public sealed class NpcActor : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visual;
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.3f;
        [SerializeField] private float sideYaw = 110f;
        [SerializeField] private float frontYaw = 180f;

        public bool Walking { get; private set; }
        public Animator Animator => animator;

        private void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!visual && animator) visual = animator.transform.parent ? animator.transform.parent : animator.transform;
            Play("Idle", 0f);
            FaceFront();
        }

        public void Play(string state, float fade = 0.2f)
        {
            if (!animator) return;
            int h = Animator.StringToHash(state);
            if (animator.HasState(0, h)) animator.CrossFadeInFixedTime(h, fade, 0);
        }

        public void FaceFront() => SetYaw(frontYaw);

        public void Face(float worldX)
        {
            SetYaw(worldX >= transform.position.x ? sideYaw : -sideYaw);
        }

        private void SetYaw(float yaw)
        {
            if (visual) visual.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Walks along X to the target, then idles. Yields until arrival.</summary>
        public IEnumerator WalkTo(float x)
        {
            Walking = true;
            Face(x);
            Play("Walk");
            while (Mathf.Abs(transform.position.x - x) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(transform.position, new Vector3(x, transform.position.y, transform.position.z), walkSpeed * Time.deltaTime);
                yield return null;
            }
            Walking = false;
            Play("Idle");
        }

        /// <summary>Plays a one-shot gesture and returns to Idle (or Talk) when it ends.</summary>
        public IEnumerator Gesture(string state, float seconds, string after = "Idle")
        {
            Play(state, 0.15f);
            yield return new WaitForSeconds(seconds);
            Play(after);
        }
    }
}
