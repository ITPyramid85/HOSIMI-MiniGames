using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame
{
    // The picture's wide edge is the top. A mirrored 180 degree rest turns that edge down,
    // with the blade pointing left into wood that travels left to right.
    // The chop still lifts seventy degrees and comes back down.
    public sealed class WoodView : MonoBehaviour, IPointerDownHandler
    {
        private const float Raised = 110f, Strike = 180f, Hang = 0f, BeltSpeed = 220f, AxeLift = 48f, BoardX = 280f, BoardLift = 8f, StopSeconds = .5f;
        [SerializeField] private RectTransform piece, axe, board, leftHalf, rightHalf;
        [SerializeField] private Image pieceImage, axeImage, leftImage, rightImage, personImage;
        [SerializeField] private Image[] stripes;
        [SerializeField] private RectTransform[] rollers;
        [SerializeField] private Sprite log, thickLog, cracked, half, melon, stone, axePlain, axeGold, personIdle, personHappy, personFrown;
        [SerializeField] private Text bubble;
        private const float ArtScale = 6f;
        private Vector2 pieceHome, axeHome;
        private float beltOffset, rollerSpin, angle, nextStrikeAt = -1f, chopWindow = .17f, approachSpeed = BeltSpeed;
        private bool beltRunning;
        private bool landOnArrival, splitOnArrival, bonusOnArrival, failOnArrival, bounceOnArrival, passOnArrival, releaseOnArrival;
        private Coroutine travel, swing, reaction, mood;
        private bool rebounding, gold, goldSpent, plainOnArrival, armed;
        private float stoneFrom = -1f, stoneUntil = -1f, reboundUntil = -1f;
        public bool Gold { get { return gold && axeImage != null && axeImage.sprite == axeGold; } }
        public event Action Pressed;
        public event Action StoneHit;

        private void Awake()
        {
            SeatOnBelt();
            PlaceBoard();
            PlaceAxe();
            piece.gameObject.SetActive(false);
            leftHalf.gameObject.SetActive(false);
            rightHalf.gameObject.SetActive(false);
            SetAngle(Strike);
            SetMood(null);
            if (bubble != null) bubble.text = "";
        }

        public void SetBeltRunning(bool running) { beltRunning = running; }

        private void Update()
        {
            if (!beltRunning || stripes == null || stripes.Length == 0 || stripes[0] == null) return;
            var belt = stripes[0].rectTransform.parent as RectTransform;
            float width = belt != null ? belt.sizeDelta.x : 768f;
            float span = width / stripes.Length;
            float deck = belt != null ? belt.sizeDelta.y * .5f - 6f - stripes[0].rectTransform.sizeDelta.y * .5f : 0f;
            beltOffset = (beltOffset + Time.unscaledDeltaTime * BeltSpeed) % span;
            for (int i = 0; i < stripes.Length; i++)
            {
                if (stripes[i] == null) continue;
                float x = -width * .5f + span * .5f + i * span + beltOffset;
                stripes[i].rectTransform.anchoredPosition = new Vector2(x, deck);
            }
            if (rollers == null) return;
            rollerSpin = (rollerSpin + Time.unscaledDeltaTime * BeltSpeed * 2f) % 360f;
            for (int i = 0; i < rollers.Length; i++)
                if (rollers[i] != null) rollers[i].localEulerAngles = new Vector3(0f, 0f, rollerSpin);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && Pressed != null) Pressed();
        }

        public void Clear(bool showAxe)
        {
            StopAllCoroutines();
            travel = swing = reaction = mood = null;
            rebounding = false;
            gold = false;
            goldSpent = false;
            armed = false;
            stoneFrom = stoneUntil = reboundUntil = -1f;
            nextStrikeAt = -1f;
            ClearFlags();
            piece.gameObject.SetActive(false);
            leftHalf.gameObject.SetActive(false);
            rightHalf.gameObject.SetActive(false);
            leftHalf.localScale = rightHalf.localScale = Vector3.one;
            piece.localEulerAngles = Vector3.zero;
            pieceImage.sprite = log;
            pieceImage.color = Color.white;
            Fit(piece, log);
            SeatOnBelt();
            piece.anchoredPosition = pieceHome;
            PlaceBoard();
            SetAngle(Strike);
            if (showAxe)
            {
                PlaceAxe();
                SetAngle(Strike);
                axe.gameObject.SetActive(true);
                if (axeImage != null && axePlain != null) axeImage.sprite = axePlain;
            }
            else if (axe != null)
                axe.gameObject.SetActive(false);
            SetMood(null);
            if (bubble != null) bubble.text = "";
        }

        public void Bring(BeatCue cue, bool thick, float seconds, float hold)
        {
            piece.gameObject.SetActive(true);
            leftHalf.gameObject.SetActive(false);
            rightHalf.gameObject.SetActive(false);
            Sprite sprite = cue.Kind == CueKind.Bonus ? melon : cue.Kind == CueKind.Forbidden ? stone : thick ? thickLog : log;
            pieceImage.sprite = sprite;
            pieceImage.color = Color.white;
            piece.localEulerAngles = Vector3.zero;
            piece.localScale = Vector3.one;
            Fit(piece, sprite);
            SeatOnBelt();
            PlaceBoard();
            if (!rebounding) PlaceAxe();
            float travelTime = Mathf.Max(.05f, seconds);
            if (cue.Kind == CueKind.Forbidden)
            {
                stoneFrom = Time.unscaledTime + travelTime - .12f;
                stoneUntil = Time.unscaledTime + travelTime + Mathf.Max(0f, hold) + .45f;
            }
            // The belt is only so fast. A log that would have to sprint the whole width starts
            // further along, so it still reaches the board on the beat.
            float edge = -BeltHalf() - piece.sizeDelta.x * .5f;
            float span = Mathf.Max(0f, pieceHome.x - edge);
            approachSpeed = Mathf.Min(BeltSpeed, span / travelTime);
            float fromLeft = pieceHome.x - approachSpeed * travelTime;
            piece.anchoredPosition = new Vector2(fromLeft, pieceHome.y);
            if (travel != null) StopCoroutine(travel);
            travel = StartCoroutine(Ride(piece, 0f, hold, true));
            Say("");
        }

        // A stone gets no scheduled stroke, but a chop aimed at it still uses this cue's window.
        public void SetChopWindow(float goodSeconds)
        {
            if (goodSeconds > 0f) chopWindow = goodSeconds;
        }

        // goodSeconds is this cue's Good window. The downward stroke fits inside it and still lands on the beat,
        // so a chop that starts as the log arrives is Good and a chop that meets the log is Perfect.
        public void Raise(float seconds, float goodSeconds)
        {
            if (goodSeconds > 0f) chopWindow = goodSeconds;
            float at = Time.unscaledTime + Mathf.Max(.05f, seconds);
            if (swing != null)
            {
                nextStrikeAt = at;
                return;
            }
            swing = StartCoroutine(RunSwing(at));
        }

        public void Chop(bool split, bool bonus)
        {
            landOnArrival = true;
            splitOnArrival = split;
            bonusOnArrival = bonus;
            if (swing == null) ApplyChop();
        }

        public void Fail()
        {
            nextStrikeAt = -1f;
            failOnArrival = true;
            if (swing == null) ApplyFail();
        }

        // No stroke is scheduled for a stone, so the player's chop starts one here. The contact is
        // settled where that blade lands: on the stone it flies off, on bare belt the stone rides on.
        public void CommitStoneChop()
        {
            if (Rebounding) return;
            armed = true;
            if (swing == null) swing = StartCoroutine(Twitch());
            else if (BladeDown) SettleStone();
        }

        private bool BladeDown { get { return Mathf.Abs(Mathf.DeltaAngle(angle, Strike)) <= 16f; } }

        // The stone is tracked by the clock, not by the piece object: the next log claims that object
        // half a second after the beat, while the stone is still standing under the blade on screen.
        public bool StoneAtBlade
        {
            get
            {
                if (stoneUntil <= 0f) return false;
                float now = Time.unscaledTime;
                return now >= stoneFrom && now <= stoneUntil;
            }
        }

        public bool AwaitingStone { get { return armed || Rebounding; } }

        // A deadline, not a latch: an interrupted fly can never leave the next round unable to resolve.
        public bool Rebounding { get { return rebounding && Time.unscaledTime < reboundUntil; } }

        private void ResolveContact()
        {
            if (Rebounding) return;
            armed = false;
            stoneFrom = stoneUntil = -1f;   // one stone, one contact
            // The gilding is a one-time shield. Spending it leaves the plain axe for the rest of the
            // run, so the next stone is fatal even after another clean streak.
            if (Gold)
            {
                goldSpent = true;
                ApplyPlain();
                releaseOnArrival = true;
                return;
            }
            rebounding = true;
            reboundUntil = Time.unscaledTime + .6f;
            if (travel != null) StopCoroutine(travel);
            travel = null;
            Coroutine falling = swing;
            swing = null;
            if (falling != null) StopCoroutine(falling);
            swing = StartCoroutine(FlyThenReport());
        }

        // The stroke has landed. A stone under the blade is a real hit; one still riding in is not,
        // and that chop is simply spent.
        private bool SettleStone()
        {
            if (!armed) return false;
            armed = false;
            if (!StoneAtBlade)
            {
                if (!landOnArrival && !failOnArrival && !passOnArrival && !plainOnArrival) releaseOnArrival = true;
                return false;
            }
            ResolveContact();
            return rebounding;
        }

        private IEnumerator FlyThenReport()
        {
            if (StoneHit != null) StoneHit();
            yield return StartCoroutine(ApplyBounce());
            rebounding = false;
            swing = null;
        }

        public void Pass()
        {
            nextStrikeAt = -1f;
            passOnArrival = true;
            if (swing == null) ApplyPass();
        }

        public void Whiff()
        {
            if (swing != null) return;
            swing = StartCoroutine(Twitch());
        }

        public void SetGold(bool gold)
        {
            if (!gold || goldSpent || axeImage == null || axeGold == null) return;
            this.gold = true;
            axeImage.sprite = axeGold;
        }

        public void UsePlainAxe()
        {
            gold = false;
            if (axeImage != null && axePlain != null) axeImage.sprite = axePlain;
        }

        // One stroke ends on its beat. A thick log queues the next beat instead of cutting this one short.
        // The downward stroke stays inside the Good window, so chopping as the head comes down still counts.
        private IEnumerator RunSwing(float strikeAt)
        {
            ClearFlags();
            while (strikeAt > 0f)
            {
                float remain = Mathf.Max(0f, strikeAt - Time.unscaledTime);
                float down = Downswing(remain);
                float lift = down * .38f / .62f;
                float delay = Mathf.Max(0f, remain - lift - down);
                if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

                remain = Mathf.Max(.05f, strikeAt - Time.unscaledTime);
                down = Downswing(remain);
                lift = Mathf.Max(0f, remain - down);
                yield return SwingToStrike(lift, down);
                if (SettleStone()) yield break;
                while (!landOnArrival && !failOnArrival && !bounceOnArrival && !passOnArrival && !plainOnArrival && !releaseOnArrival) yield return null;
                if (!passOnArrival && !bounceOnArrival)
                {
                    yield return StartCoroutine(SwingBetween(angle, Strike, .06f));
                    SetAngle(Strike);
                }
                if (releaseOnArrival)
                {
                }
                else if (bounceOnArrival)
                {
                    SetAngle(Strike);
                    yield return StartCoroutine(ApplyBounce());
                    rebounding = false;
                    swing = null;
                    yield break;
                }
                else if (plainOnArrival) ApplyPlain();
                else if (landOnArrival) ApplyChop();
                else if (failOnArrival) ApplyFail();
                else ApplyPass();
                ClearFlags();
                strikeAt = nextStrikeAt;
                nextStrikeAt = -1f;
            }
            swing = null;
        }

        // Same peak whether or not a log is on the belt: the head rises to Raised, then comes back down.
        private float Downswing(float remain)
        {
            float inside = Mathf.Max(.04f, chopWindow * .92f);
            float share = remain * .62f;
            return Mathf.Min(inside, Mathf.Max(.04f, share));
        }

        private void ApplyPlain()
        {
            SetAngle(Strike);
            UsePlainAxe();
            Say(Strings.T("wood.clang"));
            SetMood(false);
        }

        // The stone kicks the axe up and back, off the belt.
        private IEnumerator ApplyBounce()
        {
            UsePlainAxe();
            Say(Strings.T("wood.clang"));
            SetMood(false);
            Vector2 from = axe.anchoredPosition;
            Vector2 to = from + new Vector2(240f, 320f);
            float fromAngle = angle;
            float start = Time.unscaledTime;
            const float seconds = .45f;
            while (Time.unscaledTime < start + seconds)
            {
                float t = (Time.unscaledTime - start) / seconds;
                float kicked = t * t;
                axe.anchoredPosition = Vector2.Lerp(from, to, kicked);
                SetAngle(Mathf.Lerp(fromAngle, 20f, kicked));
                yield return null;
            }
        }

        private void ApplyChop()
        {
            SetAngle(Strike);
            SetMood(true);
            if (!splitOnArrival)
            {
                if (cracked != null)
                {
                    pieceImage.sprite = cracked;
                    Fit(piece, cracked);
                }
                return;
            }
            bool wood = pieceImage.sprite == log || pieceImage.sprite == thickLog || pieceImage.sprite == cracked;
            Sprite shown = wood && half != null ? half : pieceImage.sprite;
            leftImage.sprite = rightImage.sprite = shown;
            if (wood && half != null)
            {
                leftHalf.sizeDelta = rightHalf.sizeDelta = new Vector2(piece.sizeDelta.x * .55f, piece.sizeDelta.y);
                leftHalf.localScale = Vector3.one;
                rightHalf.localScale = new Vector3(-1f, 1f, 1f);
            }
            else
            {
                leftHalf.sizeDelta = rightHalf.sizeDelta = piece.sizeDelta;
                leftHalf.localScale = rightHalf.localScale = Vector3.one;
            }
            piece.gameObject.SetActive(false);
            leftHalf.gameObject.SetActive(true);
            rightHalf.gameObject.SetActive(true);
            Vector2 at = piece.anchoredPosition;
            leftHalf.anchoredPosition = at + new Vector2(-6f, 0f);
            rightHalf.anchoredPosition = at + new Vector2(6f, 0f);
            if (travel != null) StopCoroutine(travel);
            travel = null;
            StartCoroutine(Ride(leftHalf, -90f, StopSeconds, false));
            StartCoroutine(Ride(rightHalf, 70f, StopSeconds, false));
            Say(bonusOnArrival ? Strings.T("wood.bonus") : "");
        }

        private void ApplyFail()
        {
            SetAngle(Strike);
            SetMood(false);
            Say(Strings.T("wood.miss"));
        }

        private void ApplyPass()
        {
            SetAngle(Strike);
            SetMood(true);
        }

        private IEnumerator Twitch()
        {
            float down = Mathf.Max(.04f, chopWindow * .92f);
            float lift = down * .38f / .62f;
            yield return SwingToStrike(lift, down);
            if (SettleStone()) yield break;
            SetAngle(Strike);
            float queued = nextStrikeAt;
            nextStrikeAt = -1f;
            if (queued > 0f) swing = StartCoroutine(RunSwing(queued));
            else swing = null;
        }

        // Every chop, empty or on a log, lifts to the same angle and comes back down onto the belt.
        private IEnumerator SwingToStrike(float lift, float down)
        {
            float from = angle;
            float start = Time.unscaledTime;
            float liftEnd = start + Mathf.Max(0f, lift);
            float downEnd = liftEnd + Mathf.Max(.04f, down);
            if (lift > 0f)
            {
                while (Time.unscaledTime < liftEnd)
                {
                    float t = PixelTween.OutCubic(Mathf.InverseLerp(start, liftEnd, Time.unscaledTime));
                    SetAngle(Mathf.Lerp(from, Raised, t));
                    yield return null;
                }
            }
            SetAngle(Raised);
            while (Time.unscaledTime < downEnd)
            {
                float u = Mathf.InverseLerp(liftEnd, downEnd, Time.unscaledTime);
                float eased = Mathf.Lerp(u, PixelTween.InOutSine(u), .2f);
                SetAngle(Mathf.Lerp(Raised, Strike + Hang, eased));
                yield return null;
            }
            SetAngle(Strike + Hang);
        }

        private IEnumerator SwingBetween(float from, float to, float seconds)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime < start + seconds)
            {
                float t = Mathf.InverseLerp(start, start + seconds, Time.unscaledTime);
                SetAngle(Mathf.Lerp(from, to, t));
                yield return null;
            }
            SetAngle(to);
        }

        // Left to right at belt speed. A whole piece stops on the board while the axe lands, then goes on.
        private IEnumerator Ride(RectTransform target, float extra, float pause, bool holdAtBoard)
        {
            var belt = target.parent as RectTransform;
            float limit = belt != null ? belt.sizeDelta.x * .5f + target.sizeDelta.x : 520f;
            if (!holdAtBoard && pause > 0f) yield return new WaitForSecondsRealtime(pause);
            if (holdAtBoard)
            {
                while (target.anchoredPosition.x < pieceHome.x)
                {
                    StepAt(target, approachSpeed);
                    yield return null;
                }
                Vector2 held = target.anchoredPosition;
                held.x = pieceHome.x;
                target.anchoredPosition = held;
                if (pause > 0f) yield return new WaitForSecondsRealtime(pause);
            }
            while (target.anchoredPosition.x < limit)
            {
                Step(target, extra);
                yield return null;
            }
            target.gameObject.SetActive(false);
        }

        private static void Step(RectTransform target, float extra)
        {
            StepAt(target, BeltSpeed + extra);
        }

        private static void StepAt(RectTransform target, float speed)
        {
            Vector2 p = target.anchoredPosition;
            p.x += speed * Time.unscaledDeltaTime;
            target.anchoredPosition = p;
        }

        private float BeltHalf()
        {
            var belt = piece.parent as RectTransform;
            return belt != null ? belt.sizeDelta.x * .5f : 480f;
        }

        private void SetMood(bool? pleased)
        {
            if (personImage == null) return;
            if (mood != null) StopCoroutine(mood);
            mood = null;
            if (pleased == null)
            {
                if (personIdle != null) personImage.sprite = personIdle;
                return;
            }
            Sprite face = pleased.Value ? personHappy : personFrown;
            if (face != null) personImage.sprite = face;
            mood = StartCoroutine(ResetMood());
        }

        private IEnumerator ResetMood()
        {
            yield return new WaitForSecondsRealtime(.6f);
            if (personImage != null && personIdle != null) personImage.sprite = personIdle;
            mood = null;
        }

        private void Say(string text)
        {
            if (bubble == null) return;
            bubble.text = text;
            if (reaction != null) StopCoroutine(reaction);
            if (text.Length > 0) reaction = StartCoroutine(ClearBubble());
        }

        private IEnumerator ClearBubble()
        {
            yield return new WaitForSecondsRealtime(.6f);
            bubble.text = "";
            reaction = null;
        }

        private void ClearFlags()
        {
            landOnArrival = splitOnArrival = bonusOnArrival = failOnArrival = bounceOnArrival = passOnArrival = plainOnArrival = releaseOnArrival = false;
        }

        // The piece sits on the belt's top edge. Its bottom lines up with that edge.
        private void SeatOnBelt()
        {
            var belt = piece.parent as RectTransform;
            float top = belt != null ? belt.sizeDelta.y * .5f : 0f;
            pieceHome = new Vector2(BoardX, top + piece.sizeDelta.y * .5f + BoardLift);
        }

        private static void Fit(RectTransform rect, Sprite sprite)
        {
            if (sprite == null) return;
            rect.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height) * ArtScale;
        }

        // The left side is the wide edge. Its height matches a log, so the melon is cut by the same axe.
        private void PlaceAxe()
        {
            var belt = piece.parent as RectTransform;
            float beltTop = belt != null ? belt.anchoredPosition.y + belt.sizeDelta.y * .5f : 0f;
            float spotX = belt != null ? belt.anchoredPosition.x : 0f;
            float logH = log != null ? log.rect.height * ArtScale : 96f;
            float logW = log != null ? log.rect.width * ArtScale : 48f;
            float nativeW = axeImage != null && axeImage.sprite != null ? axeImage.sprite.rect.width : 24f;
            float nativeH = axeImage != null && axeImage.sprite != null ? axeImage.sprite.rect.height : 16f;
            float edge = logH;
            float reach = edge * nativeW / nativeH;
            axe.pivot = new Vector2(1f, .5f);
            axe.sizeDelta = new Vector2(reach, edge);
            axe.localScale = new Vector3(-1f, 1f, 1f);
            // Mirrored, the head is on the left of the handle end, so the pivot sits right of the log.
            axeHome = new Vector2(spotX + BoardX - logW * .5f + reach, beltTop + edge * .5f + AxeLift + BoardLift);
            axe.anchoredPosition = axeHome;
            axe.gameObject.SetActive(true);
        }

        // The board's top is the belt's top, so a stopped log rests on it under the blade.
        private void PlaceBoard()
        {
            if (board == null) return;
            var belt = piece.parent as RectTransform;
            float top = belt != null ? belt.sizeDelta.y * .5f : 32f;
            board.anchoredPosition = new Vector2(BoardX, top - board.sizeDelta.y * .5f + BoardLift);
        }

        private void SetAngle(float degrees)
        {
            angle = degrees;
            axe.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }
    }
}
