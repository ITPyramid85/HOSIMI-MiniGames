using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // Pixel "window unfold": the card opens as a thin line that widens, then grows tall; closing
    // reverses it. IsOpen flips immediately so callers treat the popup as closed while it animates out.
    [DisallowMultipleComponent]
    public sealed class PopupTransition : MonoBehaviour
    {
        private const float OpenSeconds = .28f, CloseSeconds = .18f, LineHeight = .06f;
        private const int ScaleSteps = 24;
        private RectTransform card;
        private Graphic blocker;
        private CanvasGroup group;
        private float blockerAlpha;
        public bool IsOpen { get; private set; }
        public bool IsAnimating { get; private set; }

        public static PopupTransition For(GameObject popup)
        {
            PopupTransition transition = popup.GetComponent<PopupTransition>();
            return transition != null ? transition : popup.AddComponent<PopupTransition>();
        }

        private void Awake()
        {
            card = (RectTransform)transform.Find("Card");
            blocker = GetComponent<Graphic>();
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            if (blocker != null) blockerAlpha = blocker.color.a;
        }

        public void Show()
        {
            if (IsOpen && gameObject.activeSelf) return;
            // Re-enable from scratch so the popup's OnEnable refresh runs even mid-close.
            if (gameObject.activeSelf) { StopAllCoroutines(); gameObject.SetActive(false); }
            IsOpen = true;
            gameObject.SetActive(true);
            group.interactable = group.blocksRaycasts = true;
            StartCoroutine(Animate(true));
        }

        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            group.interactable = group.blocksRaycasts = false;
            if (!gameObject.activeInHierarchy) { gameObject.SetActive(false); return; }
            StopAllCoroutines();
            StartCoroutine(Animate(false));
        }

        // Content swapped while already open (e.g. pause -> result): a quick vertical squash-and-settle.
        public void Pulse()
        {
            if (!IsOpen || IsAnimating || card == null || !gameObject.activeInHierarchy) return;
            StartCoroutine(PixelTween.Run(.2f, t =>
                card.localScale = new Vector3(1, PixelTween.Steps(Mathf.LerpUnclamped(.86f, 1, PixelTween.OutBack(t)), ScaleSteps), 1)));
        }

        private IEnumerator Animate(bool opening)
        {
            IsAnimating = true;
            yield return PixelTween.Run(opening ? OpenSeconds : CloseSeconds, t => Apply(opening ? t : 1 - t, opening));
            IsAnimating = false;
            if (!opening) gameObject.SetActive(false);
        }

        // progress 0 = hidden, 1 = fully open. Opening widens first then grows tall; closing does the reverse.
        private void Apply(float progress, bool opening)
        {
            if (blocker != null)
            {
                Color color = blocker.color;
                color.a = blockerAlpha * PixelTween.Steps(progress, 4);
                blocker.color = color;
            }
            if (card == null) return;
            const float split = .4f;
            float x, y;
            if (progress < split)
            {
                float t = progress / split;
                x = opening ? Mathf.LerpUnclamped(.1f, 1.04f, PixelTween.OutCubic(t)) : Mathf.Lerp(0, 1, t);
                y = LineHeight;
            }
            else
            {
                float t = (progress - split) / (1 - split);
                x = opening ? Mathf.Lerp(1.04f, 1, t) : 1;
                y = Mathf.LerpUnclamped(LineHeight, 1, opening ? PixelTween.OutBack(t) : t);
            }
            card.localScale = new Vector3(PixelTween.Steps(x, ScaleSteps), PixelTween.Steps(y, ScaleSteps), 1);
        }
    }
}
