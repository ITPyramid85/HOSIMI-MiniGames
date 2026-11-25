using UnityEngine;
using UnityEngine.EventSystems;

namespace RhythmGame
{
    // One press edge = one tap. Holding or moving a finger cannot score another beat.
    public sealed class GameInputArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private GameController controller;
        [SerializeField] private int padIndex;
        private int? pointer;
        public int PadIndex { get { return padIndex; } }
        public bool HasPointer { get { return pointer.HasValue; } }

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer.HasValue || e.button != PointerEventData.InputButton.Left || !controller.AcceptsPadPointer(padIndex)) return;
            pointer = e.pointerId;
            controller.NoteScreenTouch();
            controller.Press(padIndex);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            controller.Release(padIndex);
            ResetGesture();
        }
        public void ResetGesture() { pointer = null; }
        private void OnDisable() { ResetGesture(); }
    }
}
