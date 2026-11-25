using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
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
        [SerializeField] private Image nailTemplate, carpenterImage, furnitureImage;
        [FormerlySerializedAs("nails")]
        [SerializeField] private Image[] nailSlots;
        [SerializeField] private Image[] dust, collected;
        [SerializeField] private Sprite nailStraight, nailBent, carpenterIdle, carpenterScratch, chair, desk, house;
        [SerializeField] private Text countText, caption, bubble;
        // Finished nails sit to the left. Positive Z swings the head to the right.
        // The wind-up is 180 degrees. The blow stops at 90, so the side of the head meets the nail
        // instead of the face coming down in line with it.
        private const float Raised = 180f, StrikeAngle = 90f, Hang = 18f;
        private const int VisibleNails = 6;
        private const float NailSpacing = 100f;
        private Vector2 boardHome, carpenterHome, hammerTarget;
        private readonly List<Image> nails = new List<Image>();
        private readonly List<Vector2> nailHome = new List<Vector2>();
        private readonly List<bool> seatedNail = new List<bool>();
        private readonly List<int> slotVariant = new List<int>();
        private float spacing, restY, angle;
        private int active = -1, firstVariant;
        private Image pendingBend, bendShown;
        private bool laidOut;
        private bool bendOnArrival, sinkOnArrival;
        private int sinkLeft, sinkTotal;
        private bool struck;
        private Coroutine swing, reaction, shake;
        public event Action Pressed;
        public event Action Struck;

        private void Awake() { EnsureLayout(); }

        // The scene keeps a single nail. Every nail the player hits is a copy of that one.
        private void ResolveTemplate()
        {
            if (nailTemplate == null && nailSlots != null && nailSlots.Length > 0) nailTemplate = nailSlots[0];
            if (nailSlots == null) return;
            for (int i = 0; i < nailSlots.Length; i++)
            {
                Image extra = nailSlots[i];
                if (extra == null || extra == nailTemplate) continue;
                Destroy(extra.gameObject);
            }
            nailSlots = null;
        }

        private void EnsureLayout()
        {
            if (laidOut) return;
            laidOut = true;
            ResolveTemplate();
            FitBoard();
            boardHome = board.anchoredPosition;
            carpenterHome = carpenter.anchoredPosition;
            spacing = NailSpacing;
            float length = nailTemplate.rectTransform.sizeDelta.y;
            float woodTop = board.sizeDelta.y * .5f;
            restY = woodTop + length * (2f / 3f);
            fullSink = length * (2f / 3f);
            nailTemplate.gameObject.SetActive(false);
            hammerTarget = SideHold(0);
            hammer.anchoredPosition = hammerTarget;
            SetAngle(Raised);
            furnitureImage.enabled = false;
            caption.text = Strings.T("nails.remaining");
        }

        public RectTransform Hammer { get { return hammer; } }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && Pressed != null) Pressed();
        }

        public void Clear()
        {
            EnsureLayout();
            StopAllCoroutines();
            swing = reaction = shake = null;
            board.anchoredPosition = boardHome;
            carpenter.anchoredPosition = carpenterHome;
            SetAngle(Raised);
            hammer.anchoredPosition = SideHold(0);
            carpenterImage.sprite = carpenterIdle;
            furnitureImage.sprite = chair;
            furnitureImage.enabled = false;
            furniture.localScale = Vector3.one;
            bubble.text = countText.text = "";
            caption.text = Strings.T("nails.remaining");
            for (int i = nails.Count - 1; i >= 0; i--)
                if (nails[i] != null) Destroy(nails[i].gameObject);
            nails.Clear();
            nailHome.Clear();
            seatedNail.Clear();
            slotVariant.Clear();
            foreach (Image speck in dust) speck.gameObject.SetActive(false);
            foreach (Image icon in collected) icon.color = new Color(1, 1, 1, .28f);
            active = -1;
            firstVariant = 0;
            pendingBend = bendShown = null;
            bendOnArrival = sinkOnArrival = false;
        }

        public void Grant(NailChart.Piece piece)
        {
            furnitureImage.sprite = piece == NailChart.Piece.Chair ? chair : piece == NailChart.Piece.Desk ? desk : house;
            furnitureImage.enabled = true;
            furniture.localScale = Vector3.one;
            Collect(piece);
        }

        // The next five nails are a new job: the furniture and the three icons start over.
        public void NewJob()
        {
            furnitureImage.enabled = false;
            furniture.localScale = Vector3.one;
            foreach (Image icon in collected) icon.color = new Color(1, 1, 1, .28f);
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

        // Each nail is created when it becomes the one to hit. Six fit on the board; an older one leaves as a new one arrives.
        public void Arm(int variant, int left, int total)
        {
            EnsureLayout();
            while (variant - firstVariant >= nails.Count)
            {
                if (nails.Count >= VisibleNails) DropOldest();
                else SpawnNail();
            }
            active = variant - firstVariant;
            slotVariant[active] = variant;
            Image born = nails[active];
            Straighten(born);
            born.rectTransform.anchoredPosition = new Vector2(born.rectTransform.anchoredPosition.x, restY);
            PlaceRow();
            HoldSeated();
            Straighten(born);
            hammerTarget = SideHold(nailHome[active].x);
            // A hammer that is still down on a nail stays there. Snapping it onto the next nail
            // while it is cocked is what made a miss bend the nail with the head in the air.
            if (swing == null && angle >= Raised - 1f) hammer.anchoredPosition = hammerTarget;
            ShowCount(left, total);
        }

        // A new nail is a new object. Copying the template would also copy a bend if that object had been hit.
        private Image SpawnNail()
        {
            var go = new GameObject("Nail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = nailTemplate.gameObject.layer;
            go.transform.SetParent(board, false);
            RectTransform src = nailTemplate.rectTransform;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = src.anchorMin;
            rect.anchorMax = src.anchorMax;
            rect.pivot = src.pivot;
            rect.sizeDelta = src.sizeDelta;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = new Vector2(0, restY);
            Image nail = go.GetComponent<Image>();
            nail.raycastTarget = false;
            nail.preserveAspect = nailTemplate.preserveAspect;
            nail.type = nailTemplate.type;
            nail.pixelsPerUnitMultiplier = nailTemplate.pixelsPerUnitMultiplier;
            Straighten(nail);
            go.transform.SetSiblingIndex(nailTemplate.transform.GetSiblingIndex());
            nails.Add(nail);
            nailHome.Add(new Vector2(0, restY));
            seatedNail.Add(false);
            slotVariant.Add(-1);
            return nail;
        }

        private void Straighten(Image nail)
        {
            if (nail == null) return;
            nail.sprite = nailStraight != null ? nailStraight : nailTemplate.sprite;
            nail.color = Color.white;
            nail.rectTransform.localRotation = Quaternion.identity;
        }

        private void DropOldest()
        {
            Image gone = nails[0];
            nails.RemoveAt(0);
            nailHome.RemoveAt(0);
            seatedNail.RemoveAt(0);
            slotVariant.RemoveAt(0);
            if (gone != null) Destroy(gone.gameObject);
            firstVariant++;
        }

        // The nails on the board sit in a row, centered. Each keeps however far it has already been driven.
        private void PlaceRow()
        {
            int shown = nails.Count;
            float start = -(shown - 1) * spacing * .5f;
            for (int i = 0; i < shown; i++)
            {
                float sunk = nails[i].rectTransform.anchoredPosition.y - nailHome[i].y;
                float x = start + i * spacing;
                nailHome[i] = new Vector2(x, restY);
                nails[i].rectTransform.anchoredPosition = new Vector2(x, restY + sunk);
            }
        }

        public void Raise(float seconds)
        {
            // The next nail gets its own swing. A miss whose hammer never arrived does not bend it.
            if (SideDown) CommitBend();
            else pendingBend = null;
            Halt();
            bendOnArrival = false;
            sinkOnArrival = false;
            struck = false;
            swing = StartCoroutine(CueSwing(Mathf.Max(.16f, seconds)));
        }

        public void Strike(int left, int total)
        {
            ShowCount(left, total);
            bendOnArrival = false;
            pendingBend = null;
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

        public void Bend(int variant, string text, int total)
        {
            ShowCount(0, total);
            carpenterImage.sprite = carpenterScratch;
            React(text, Palette.DangerText, jump: false);
            // Remember which nail was missed. It bends only once the hammer head actually meets it.
            pendingBend = FindLoose(variant);
            bendOnArrival = pendingBend != null;
            if (!bendOnArrival || swing != null) return;
            if (SideDown) { CommitBend(); Connect(); return; }
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
            while (Time.unscaledTime < start && !bendOnArrival && !sinkOnArrival) yield return null;
            if (!bendOnArrival && !sinkOnArrival)
            {
                while (Time.unscaledTime < liftEnd && !bendOnArrival && !sinkOnArrival)
                {
                    float t = PixelTween.OutCubic(Mathf.InverseLerp(start, liftEnd, Time.unscaledTime));
                    SetAngle(Mathf.Lerp(from, Raised, t));
                    hammer.anchoredPosition = Vector2.Lerp(fromPos, target, t);
                    yield return null;
                }
                if (!bendOnArrival && !sinkOnArrival)
                {
                    hammer.anchoredPosition = target;
                    SetAngle(Raised);
                    while (Time.unscaledTime < end && !bendOnArrival && !sinkOnArrival)
                    {
                        float u = Mathf.InverseLerp(liftEnd, end, Time.unscaledTime);
                        float t = Mathf.Lerp(u, PixelTween.InOutSine(u), .2f);
                        SetAngle(Mathf.Lerp(Raised, StrikeAngle + Hang, t));
                        yield return null;
                    }
                }
            }
            if (!bendOnArrival && !sinkOnArrival) SetAngle(StrikeAngle + Hang);
            else hammer.anchoredPosition = target;
            while (!sinkOnArrival && !bendOnArrival) yield return null;
            yield return StartCoroutine(SwingBetween(angle, StrikeAngle, .06f));
            SetAngle(StrikeAngle);
            Connect();
            if (sinkOnArrival)
            {
                sinkOnArrival = false;
                bendOnArrival = false;
                pendingBend = null;
                swing = StartCoroutine(Drive(sinkLeft, sinkTotal));
                yield break;
            }
            CommitBend();
            swing = null;
        }

        // A miss that arrives with the hammer still raised comes down to 90, and only then does the nail bend.
        private IEnumerator LandAndBend()
        {
            if (!SideDown) yield return StartCoroutine(SwingBetween(angle, StrikeAngle, .06f));
            SetAngle(StrikeAngle);
            CommitBend();
            Connect();
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

        // The bent picture is applied only after the hammer has come down on that nail.
        private void CommitBend()
        {
            Image nail = pendingBend;
            pendingBend = null;
            bendOnArrival = false;
            if (nail == null) return;
            bendShown = nail;
            nail.sprite = nailBent;
            nail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        }

        // A nail that has not been struck stays straight, including one that just slid into the row.
        private void LateUpdate()
        {
            if (active < 0 || active >= nails.Count) return;
            Image nail = nails[active];
            if (nail == null || nail == bendShown) return;
            if (nail.sprite == nailBent || Quaternion.Angle(nail.rectTransform.localRotation, Quaternion.identity) > 1f)
                Straighten(nail);
        }

        private Image FindLoose(int variant)
        {
            for (int i = 0; i < slotVariant.Count; i++)
                if (slotVariant[i] == variant && !seatedNail[i]) return nails[i];
            return null;
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
            nail.rectTransform.localRotation = Quaternion.identity;
        }

        private void HoldSeated()
        {
            for (int i = 0; i < seatedNail.Count; i++) if (seatedNail[i]) Hold(i);
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
