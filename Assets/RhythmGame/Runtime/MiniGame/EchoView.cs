using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame
{
    // The rival sits across a drum. A phrase plays, then the bubble asks it back.
    public sealed class EchoView : MonoBehaviour
    {
        [SerializeField] private Image rival, player;
        [SerializeField] private Sprite calm, smug, haughty, fluster, laugh, playerFace;
        [SerializeField] private Image[] drums;
        [SerializeField] private Text bubble;
        [SerializeField] private Image bubbleBack;
        [SerializeField] private Text beats, turn;
        private CanvasGroup[] groups;
        private float[] restAlpha;
        private float[] flash;
        private Coroutine face;

        private void Awake()
        {
            groups = new CanvasGroup[drums.Length];
            restAlpha = new float[drums.Length];
            flash = new float[drums.Length];
            for (int i = 0; i < drums.Length; i++)
            {
                if (drums[i] == null) continue;
                groups[i] = drums[i].GetComponent<CanvasGroup>();
                restAlpha[i] = groups[i] != null ? groups[i].alpha : 1f;
            }
            Face(calm);
            if (player != null && playerFace != null) player.sprite = playerFace;
            HideBubble();
            if (beats != null) beats.text = "";
            if (turn != null) turn.text = "";
        }

        // Stage, round, phrase progress and chances left. The record is the longest phrase echoed.
        public void Phrase(int stage, int round, int rounds, int answered, int length, int chances)
        {
            if (beats == null) return;
            beats.text = "STAGE " + stage + "    " + round + "/" + rounds + Strings.T("echo.round_unit")
                + "\n" + answered + "/" + length + " " + Strings.T("echo.beat_unit")
                + "    " + Strings.T("echo.chances") + chances;
        }

        // The rival is playing: watch and remember, keys do nothing yet.
        public void Listen()
        {
            if (turn == null) return;
            turn.text = Strings.T("echo.watch");
            turn.color = new Color32(118, 114, 130, 255);
        }

        // A stage is cleared and the next one opens more pads.
        public void Announce(int stage)
        {
            if (turn == null) return;
            turn.text = "STAGE " + stage + "\n" + Strings.T(stage >= 3 ? "echo.stage3" : "echo.stage2");
            turn.color = new Color32(204, 112, 40, 255);
        }

        public void Clear()
        {
            if (face != null) StopCoroutine(face);
            face = null;
            Face(calm);
            HideBubble();
            if (beats != null) beats.text = "";
            if (turn != null) turn.text = "";
            if (flash == null) return;
            for (int i = 0; i < drums.Length; i++)
            {
                flash[i] = 0f;
                if (drums[i] == null) continue;
                drums[i].rectTransform.localScale = Vector3.one;
                ApplyAlpha(i);
            }
        }

        public void Open(int[] pads)
        {
            for (int i = 0; i < drums.Length; i++)
            {
                if (drums[i] == null) continue;
                bool on = System.Array.IndexOf(pads, i) >= 0;
                restAlpha[i] = on ? 1f : .34f;
                if (flash[i] <= 0f) ApplyAlpha(i);
                Button button = drums[i].GetComponent<Button>();
                if (button != null) button.interactable = on;
            }
        }

        public void Bind(EchoController owner)
        {
            for (int i = 0; i < drums.Length; i++)
            {
                if (drums[i] == null) continue;
                EchoPad pad = drums[i].GetComponent<EchoPad>();
                if (pad == null) pad = drums[i].gameObject.AddComponent<EchoPad>();
                pad.Bind(owner, i);
            }
        }

        public void Strike(int pad)
        {
            if (pad < 0 || pad >= drums.Length || drums[pad] == null) return;
            flash[pad] = .14f;
            drums[pad].rectTransform.localScale = new Vector3(1.12f, 1.12f, 1f);
            if (groups[pad] != null) groups[pad].alpha = 1f;
        }

        // The rival swells as the stage goes on, and starts each new stage already sure of himself.
        public void Pride(int stage, int round)
        {
            if (face != null) StopCoroutine(face);
            face = null;
            Face(stage >= 3 || round >= 4 ? haughty : stage >= 2 || round >= 2 ? smug : calm);
        }

        // The phrase is over: the rival asks for it back and the pads start listening.
        public void Huh()
        {
            if (bubble != null)
            {
                bubble.text = Strings.T("echo.huh");
                bubble.color = new Color32(25, 35, 50, 255);
            }
            if (bubbleBack != null) bubbleBack.gameObject.SetActive(true);
            if (turn == null) return;
            turn.text = Strings.T("echo.answer");
            turn.color = new Color32(204, 112, 40, 255);
        }

        public void Fluster()
        {
            HideBubble();
            Hold(fluster, .7f);
        }

        public void Laugh()
        {
            HideBubble();
            Hold(laugh, .9f);
        }

        private void HideBubble()
        {
            if (bubble != null) bubble.text = "";
            if (bubbleBack != null) bubbleBack.gameObject.SetActive(false);
        }

        private void Hold(Sprite sprite, float seconds)
        {
            if (face != null) StopCoroutine(face);
            face = StartCoroutine(HoldFace(sprite, seconds));
        }

        private IEnumerator HoldFace(Sprite sprite, float seconds)
        {
            Face(sprite);
            yield return new WaitForSecondsRealtime(seconds);
            face = null;
        }

        private void Face(Sprite sprite)
        {
            if (rival != null && sprite != null) rival.sprite = sprite;
        }

        private void Update()
        {
            if (flash == null) return;
            for (int i = 0; i < drums.Length; i++)
            {
                if (drums[i] == null || flash[i] <= 0f) continue;
                flash[i] -= Time.unscaledDeltaTime;
                if (flash[i] > 0f) continue;
                drums[i].rectTransform.localScale = Vector3.one;
                ApplyAlpha(i);
            }
        }

        private void ApplyAlpha(int pad)
        {
            if (groups[pad] != null) groups[pad].alpha = restAlpha[pad];
        }

        private sealed class EchoPad : MonoBehaviour, IPointerDownHandler
        {
            private EchoController owner;
            private int pad;

            public void Bind(EchoController owner, int pad)
            {
                this.owner = owner;
                this.pad = pad;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (eventData.button != PointerEventData.InputButton.Left || owner == null) return;
                owner.Echo(pad);
            }
        }
    }
}
