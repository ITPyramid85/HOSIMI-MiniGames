using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    public sealed class DrumPadView : MonoBehaviour
    {
        [SerializeField] private Image body, approach;
        [SerializeField] private Text label, feedback;
        [SerializeField] private GameInputArea input;
        private float feedbackUntil;
        private bool wasLit;
        private Coroutine punch;
        public GameInputArea Input { get { return input; } }

        private void Awake()
        {
            if (!Application.isMobilePlatform) return;
            Text key = transform.Find("Button/Key").GetComponent<Text>();
            key.text = (input.PadIndex + 1).ToString();
        }

        public void Render(bool lit, float progress)
        {
            if (lit && !wasLit) Punch(.1f);
            wasLit = lit;
            Color accent = Palette.Sky;
            body.color = lit ? accent : Palette.Well;
            label.color = lit ? Palette.Ink : Palette.Muted;
            label.text = lit ? Strings.T("game.tap") : "•";
            approach.fillAmount = 1;
            Color outline = accent;
            outline.a = lit ? 1 : progress > 0 ? .2f + .5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8)) : 0;
            approach.color = outline;
            if (Time.unscaledTime >= feedbackUntil) feedback.text = "";
        }

        public void ShowFeedback(HitResult hit, Color color)
        {
            feedback.text = hit.Judgement.ToString().ToUpperInvariant();
            feedback.color = color;
            feedbackUntil = Time.unscaledTime + .65f;
            Punch(hit.Judgement == Judgement.Miss ? -.08f : .16f);
        }
        public void ResetFeedback()
        {
            feedback.text = ""; feedbackUntil = 0; wasLit = false;
            if (punch != null) StopCoroutine(punch);
            transform.localScale = Vector3.one;
        }

        private void Punch(float amount)
        {
            if (!isActiveAndEnabled) return;
            if (punch != null) StopCoroutine(punch);
            punch = StartCoroutine(PixelTween.Punch(transform, amount));
        }
    }
}
