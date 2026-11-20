using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame
{
    // One board, the nail being driven, and the carpenter. The hammer swings onto the nail on the beat.
    // A strike on that beat sinks the nail, shakes the board and kicks up dust. A miss pulls the hammer
    // back and bends the nail; an early hit makes him jump and clutch his thumb.
    public sealed class DrivingNailView : MonoBehaviour, IPointerDownHandler
    {
        private const float BoardDrop = 40f, BoardHeight = 184f;
        private float fullSink;
        [SerializeField] private RectTransform board, hammer, carpenter, furniture;
        [SerializeField] private Image[] nails, dust, collected;
        [SerializeField] private Image carpenterImage, furnitureImage;
        [SerializeField] private Sprite nailStraight, nailBent, carpenterIdle, carpenterScratch, chair, desk, house;
        [SerializeField] private Text countText, caption, bubble;
        // Finished nails sit to the left. Positive Z swings the head to the right.
        // The wind-up is 180 degrees. The blow stops at 90, so the side of the head meets the nail
        // instead of the face coming down in line with it.
        private const float Raised = 180f, StrikeAngle = 90f, Hang = 18f;
        private Vector2 boardHome, carpenterHome, hammerTarget;
        private Vector2[] nailHome;
        private float spacing, restY, angle;
        private bool[] seatedNail;
        private int active = -1, firstVariant, bendSlot = -1;
        private bool bendOnArrival, sinkOnArrival;
        private int sinkLeft, sinkTotal;
        private bool struck;
        private Coroutine swing, reaction, shake;
        public event Action Pressed;
        public event Action Struck;

        private void Awake()
        {
            FitBoard();
            boardHome = board.anchoredPosition;
            carpenterHome = carpenter.anchoredPosition;
            nailHome = new Vector2[nails.Length];
            seatedNail = new bool[nails.Length];
            for (int i = 0; i < nails.Length; i++)
            {
                nailHome[i] = nails[i].rectTransform.anchoredPosition;
                nails[i].gameObject.SetActive(i == 0);
            }
            spacing = nails.Length > 1 ? nailHome[1].x - nailHome[0].x : 100f;
            float length = nails[0].rectTransform.sizeDelta.y;
            float woodTop = board.sizeDelta.y * .5f;
            restY = woodTop + length * (2f / 3f);
            fullSink = length * (2f / 3f);
            PlaceRow(1);
            hammerTarget = SideHold(nailHome[0].x);
            hammer.anchoredPosition = hammerTarget;
            SetAngle(Raised);
            furnitureImage.enabled = false;
            caption.text = Strings.T("nails.remaining");
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && Pressed != null) Pressed();
        }

        public void Clear()
        {
            StopAllCoroutines();
            swing = reaction = shake = null;
            board.anchoredPosition = boardHome;
            carpenter.anchoredPosition = carpenterHome;
            SetAngle(Raised);
            hammer.anchoredPosition = SideHold(nailHome[0].x);
            carpenterImage.sprite = carpenterIdle;
            furnitureImage.sprite = chair;
            furnitureImage.enabled = false;
            furniture.localScale = Vector3.one;
            bubble.text = countText.text = "";
            caption.text = Strings.T("nails.remaining");
            for (int i = 0; i < nails.Length; i++)
            {
                nails[i].sprite = nailStraight;
                nails[i].rectTransform.anchoredPosition = nailHome[i];
                nails[i].rectTransform.localEulerAngles = Vector3.zero;
                nails[i].color = Color.white;
                nails[i].gameObject.SetActive(false);
            }
            foreach (Image speck in dust) speck.gameObject.SetActive(false);
            foreach (Image icon in collected) icon.color = new Color(1, 1, 1, .28f);
            for (int i = 0; i < seatedNail.Length; i++) seatedNail[i] = false;
            active = -1;
            firstVariant = 0;
            bendSlot = -1;
            bendOnArrival = sinkOnArrival = false;
        }

        public void Grant(NailChart.Piece piece)
        {
            furnitureImage.sprite = piece == NailChart.Piece.Chair ? chair : piece == NailChart.Piece.Desk ? desk : house;
            furnitureImage.enabled = true;
            furniture.localScale = Vector3.one;
            Collect(piece);
        }

        public void ShowProject(NailChart.Piece piece, float scale)
        {
            furnitureImage.sprite = piece == NailChart.Piece.Chair ? chair : piece == NailChart.Piece.Desk ? desk : house;
            furnitureImage.enabled = true;
            furniture.localScale = Vector3.one * scale;
        }

        public void Collect(NailChart.Piece piece)
        {
            int index = piece == NailChart.Piece.Chair ? 0 : piece == NailChart.Piece.Desk ? 1 : 2;
            collected[index].color = Color.white;
            bubble.text = Strings.T(NailChart.PieceKey(piece));
            bubble.color = Palette.MintText;
            StartCoroutine(PixelTween.Punch(collected[index].rectTransform, .35f, .24f));
        }

        // variant 0 is the only nail on the board. Each later nail is born when it becomes the one to hit.
        public void Arm(int variant, int left, int total)
        {
            while (variant - firstVariant >= nails.Length)
            {
                ShiftLeft();
                firstVariant++;
            }
            active = variant - firstVariant;
            Image born = nails[active];
            born.sprite = nailStraight;
            born.color = Color.white;
            born.rectTransform.localEulerAngles = Vector3.zero;
            born.rectTransform.anchoredPosition = new Vector2(born.rectTransform.anchoredPosition.x, restY);
            PlaceRow(active + 1);
            HoldSeated();
            hammerTarget = SideHold(nailHome[active].x);
            // A hammer that is still down on a nail stays there. Snapping it onto the next nail
            // while it is cocked is what made a miss bend the nail with the head in the air.
            if (swing == null && angle >= Raised - 1f) hammer.anchoredPosition = hammerTarget;
            ShowCount(left, total);
        }

        private void ShiftLeft()
        {
            for (int i = 0; i < nails.Length - 1; i++)
            {
                float sunk = nails[i + 1].rectTransform.anchoredPosition.y - nailHome[i + 1].y;
                nails[i].sprite = nails[i + 1].sprite;
                nails[i].color = nails[i + 1].color;
                nails[i].rectTransform.localEulerAngles = nails[i + 1].rectTransform.localEulerAngles;
                nails[i].rectTransform.anchoredPosition = new Vector2(nails[i].rectTransform.anchoredPosition.x, nailHome[i].y + sunk);
                seatedNail[i] = seatedNail[i + 1];
            }
            seatedNail[nails.Length - 1] = false;
        }

        // shown nails sit in the middle of the board. Nails past shown stay hidden until they are born.
        private void PlaceRow(int shown)
        {
            float start = -(shown - 1) * spacing * .5f;
            for (int i = 0; i < nails.Length; i++)
            {
                bool used = i < shown;
                nails[i].gameObject.SetActive(used);
                if (!used) continue;
                float sunk = nails[i].rectTransform.anchoredPosition.y - nailHome[i].y;
                float x = start + i * spacing;
                nailHome[i] = new Vector2(x, restY);
                nails[i].rectTransform.anchoredPosition = new Vector2(x, restY + sunk);
            }
        }

        public void Raise(float seconds)
        {
            Halt();
            struck = false;
            swing = StartCoroutine(CueSwing(Mathf.Max(.16f, seconds)));
        }

        public void Strike(int left, int total)
        {
            ShowCount(left, total);
            bendOnArrival = false;
            sinkLeft = left;
            sinkTotal = total;
            // A beat hit must not cut the swing off at the top and sink the nail from there.
            if (swing != null && !SideDown) { sinkOnArrival = true; return; }
            sinkOnArrival = false;
            Halt();
            swing = StartCoroutine(Drive(left, total));
        }

        public void Seat()
        {
            if (active < 0) return;
            seatedNail[active] = true;
            Hold(active);
            StartCoroutine(PixelTween.Punch(nails[active].rectTransform, .2f, .16f));
        }

        public void Bend(string text, int total)
        {
            ShowCount(0, total);
            carpenterImage.sprite = carpenterScratch;
            React(text, Palette.DangerText, jump: false);
            // The nail bends when the head arrives, not while the hammer is still at the top of the swing.
            bendSlot = active;
            bendOnArrival = true;
            if (swing != null) return;
            if (SideDown) { Connect(); ApplyBend(); return; }
            swing = StartCoroutine(LandAndBend());
        }

        public void Scratch(string text)
        {
            carpenterImage.sprite = carpenterScratch;
            React(text, Palette.Ink, jump: false);
        }

        public void Thumb(string text)
        {
            carpenterImage.sprite = carpenterIdle;
            React(text, Palette.DangerText, jump: true);
        }

        private void ShowCount(int left, int total)
        {
            countText.text = left + " / " + total;
            caption.text = Strings.T("nails.remaining");
        }

        private void Sink(int left, int total)
        {
            if (active < 0 || total <= 0) return;
            float down = fullSink * (total - left) / total;
            nails[active].rectTransform.anchoredPosition = nailHome[active] + new Vector2(0, -PixelTween.Snap(down));
        }

        // The side of the head is on the nail before it sinks. The blow ends at 90 degrees, not upright.
        private IEnumerator Drive(int left, int total)
        {
            if (!SideDown) yield return StartCoroutine(SwingBetween(angle, StrikeAngle, .06f));
            SetAngle(StrikeAngle);
            Connect();
            Sink(left, total);
            Shake();
            Dust();
            HoldSeated();
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + .05f)
            {
                SetAngle(Mathf.Lerp(StrikeAngle, StrikeAngle + 12f, Mathf.InverseLerp(start, start + .05f, Time.unscaledTime)));
                yield return null;
            }
            start = Time.unscaledTime;
            while (Time.unscaledTime < start + .06f)
            {
                SetAngle(Mathf.Lerp(StrikeAngle + 12f, StrikeAngle, Mathf.InverseLerp(start, start + .06f, Time.unscaledTime)));
                yield return null;
            }
            SetAngle(StrikeAngle);
            swing = null;
        }

        private void Shake()
        {
            if (shake != null) StopCoroutine(shake);
            shake = StartCoroutine(Wiggle());
        }

        private IEnumerator Wiggle()
        {
            for (int i = 0; i < 4; i++)
            {
                board.anchoredPosition = boardHome + new Vector2(i % 2 == 0 ? 8 : -8, 0);
                yield return new WaitForSecondsRealtime(.03f);
            }
            board.anchoredPosition = boardHome;
            shake = null;
        }

        private void Dust()
        {
            if (active < 0) return;
            Vector2 at = nails[active].rectTransform.anchoredPosition;
            for (int i = 0; i < dust.Length; i++)
            {
                Image speck = dust[i];
                speck.gameObject.SetActive(true);
                speck.color = new Color(.91f, .84f, .73f, 1f);
                float theta = (i / (float)dust.Length) * Mathf.PI;
                Vector2 dir = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta) + .4f);
                StartCoroutine(PixelTween.Run(.28f, t =>
                {
                    speck.rectTransform.anchoredPosition = PixelTween.Snap(at + dir * Mathf.Lerp(8, 48, PixelTween.OutCubic(t)));
                    Color c = speck.color;
                    c.a = 1 - PixelTween.Steps(t, 4);
                    speck.color = c;
                    if (t >= 1) speck.gameObject.SetActive(false);
                }));
            }
        }

        // Lift to 180 degrees, then strike down to 90 so the side of the head meets the nail.
        // Reaching the nail does not sink or bend it; only a judged hit or miss does that.
        private IEnumerator CueSwing(float seconds)
        {
            float from = angle;
            Vector2 fromPos = hammer.anchoredPosition;
            Vector2 target = hammerTarget;
            // The head still arrives on the beat. The lift and the blow take about half the lead.
            float motion = Mathf.Max(.16f, seconds * .5f);
            float start = Time.unscaledTime + Mathf.Max(0f, seconds - motion);
            float liftEnd = start + motion * .38f;
            float end = start + motion;
            while (Time.unscaledTime < start) yield return null;
            while (Time.unscaledTime < liftEnd)
            {
                float t = PixelTween.OutCubic(Mathf.InverseLerp(start, liftEnd, Time.unscaledTime));
                SetAngle(Mathf.Lerp(from, Raised, t));
                hammer.anchoredPosition = Vector2.Lerp(fromPos, target, t);
                yield return null;
            }
            hammer.anchoredPosition = target;
            SetAngle(Raised);
            while (Time.unscaledTime < end)
            {
                float u = Mathf.InverseLerp(liftEnd, end, Time.unscaledTime);
                float t = Mathf.Lerp(u, PixelTween.InOutSine(u), .2f);
                SetAngle(Mathf.Lerp(Raised, StrikeAngle + Hang, t));
                yield return null;
            }
            SetAngle(StrikeAngle + Hang);
            while (!sinkOnArrival && !bendOnArrival) yield return null;
            yield return StartCoroutine(SwingBetween(angle, StrikeAngle, .06f));
            SetAngle(StrikeAngle);
            Connect();
            if (sinkOnArrival)
            {
                sinkOnArrival = false;
                swing = StartCoroutine(Drive(sinkLeft, sinkTotal));
                yield break;
            }
            if (bendOnArrival) ApplyBend();
            swing = null;
        }

        // A miss that arrives with the hammer still raised comes down to 90 before the nail can bend.
        private IEnumerator LandAndBend()
        {
            if (!SideDown) yield return StartCoroutine(SwingBetween(angle, StrikeAngle, .06f));
            SetAngle(StrikeAngle);
            Connect();
            ApplyBend();
            swing = null;
        }

        private IEnumerator SwingBetween(float from, float to, float seconds)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + seconds)
            {
                float t = PixelTween.InOutSine(Mathf.InverseLerp(start, start + seconds, Time.unscaledTime));
                SetAngle(Mathf.Lerp(from, to, t));
                yield return null;
            }
            SetAngle(to);
        }

        private void ApplyBend()
        {
            bendOnArrival = false;
            if (bendSlot >= 0 && bendSlot < nails.Length && !seatedNail[bendSlot])
            {
                nails[bendSlot].sprite = nailBent;
                nails[bendSlot].rectTransform.localEulerAngles = new Vector3(0, 0, 18);
            }
            bendSlot = -1;
            HoldSeated();
        }

        private void Halt()
        {
            if (swing != null) StopCoroutine(swing);
            swing = null;
        }

        // Hit and miss both sound on this frame: the head has just met the nail.
        private void Connect()
        {
            if (struck) return;
            struck = true;
            if (Struck != null) Struck();
        }

        private void SetAngle(float degrees)
        {
            angle = degrees;
            hammer.localRotation = Quaternion.Euler(0, 0, degrees);
        }

        // The plank is shorter and the whole group sits lower. A fresh nail starts one third of the way in.
        private void FitBoard()
        {
            board.anchoredPosition += new Vector2(0, -BoardDrop);
            board.sizeDelta = new Vector2(board.sizeDelta.x, BoardHeight);
            float top = BoardHeight * .5f;
            int grains = 0;
            for (int i = 0; i < board.childCount; i++)
                if (board.GetChild(i).name.StartsWith("Grain")) grains++;
            int seen = 0;
            for (int i = 0; i < board.childCount; i++)
            {
                var child = (RectTransform)board.GetChild(i);
                if (child.name == "Lip")
                {
                    child.anchoredPosition = new Vector2(child.anchoredPosition.x, top - 8f);
                    continue;
                }
                if (!child.name.StartsWith("Grain")) continue;
                float span = grains <= 1 ? 0f : (BoardHeight - 36f) / (grains - 1);
                child.anchoredPosition = new Vector2(child.anchoredPosition.x, top - 20f - seen * span);
                seen++;
            }
        }

        private bool SideDown { get { return Mathf.Abs(angle - StrikeAngle) <= 6f; } }

        // At 90 degrees the head sticks out to the right and its side faces down.
        // The pivot sits left of the nail and low enough for that side to meet the nail head.
        private Vector2 SideHold(float nailX)
        {
            float along = hammer.sizeDelta.y * .77f;
            float half = hammer.sizeDelta.x * .5f;
            return new Vector2(nailX - along, restY - 8f + half);
        }

        // A nail that was driven in stays straight. Later swings must not tilt it.
        private void Hold(int slot)
        {
            Image nail = nails[slot];
            nail.sprite = nailStraight;
            nail.color = new Color(.75f, .78f, .82f, 1f);
            nail.rectTransform.localEulerAngles = Vector3.zero;
        }

        private void HoldSeated()
        {
            for (int i = 0; i < seatedNail.Length; i++) if (seatedNail[i]) Hold(i);
        }

        private void React(string text, Color color, bool jump)
        {
            if (reaction != null) StopCoroutine(reaction);
            reaction = StartCoroutine(ReactRoutine(text, color, jump));
        }

        private IEnumerator ReactRoutine(string text, Color color, bool jump)
        {
            bubble.text = text;
            bubble.color = color;
            if (jump)
            {
                Vector2 up = carpenterHome + new Vector2(0, 28);
                yield return PixelTween.Run(.08f, t => carpenter.anchoredPosition = PixelTween.Snap(Vector2.Lerp(carpenterHome, up, t)));
                yield return PixelTween.Run(.12f, t => carpenter.anchoredPosition = PixelTween.Snap(Vector2.Lerp(up, carpenterHome, t)));
            }
            else yield return new WaitForSecondsRealtime(.45f);
            carpenterImage.sprite = carpenterIdle;
            bubble.text = "";
            reaction = null;
        }
    }
}
