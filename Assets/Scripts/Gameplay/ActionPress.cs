using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdventureKing.Gameplay
{
    /// <summary>Pointer-down button (fires on press, like a game pad; shows a pressed squash).</summary>
    public sealed class ActionPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action Down, Up;
        private Vector3 baseScale = Vector3.one;
        private void Awake() => baseScale = transform.localScale;
        public void OnPointerDown(PointerEventData e) { transform.localScale = baseScale * 0.92f; Down?.Invoke(); }
        public void OnPointerUp(PointerEventData e) { transform.localScale = baseScale; Up?.Invoke(); }
        public void OnPointerExit(PointerEventData e) => transform.localScale = baseScale;
        /// <summary>Same as a tap (tests / keyboard feedback).</summary>
        public void Press() { Down?.Invoke(); Up?.Invoke(); }
    }
}
