using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RhythmGame
{
    // Fires on press, not release: Button.onClick would add the finger's hold time to the measurement.
    public sealed class CalibrationTapPad : MonoBehaviour, IPointerDownHandler
    {
        public event Action Tapped;
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && Tapped != null) Tapped();
        }
    }
}
