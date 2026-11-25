using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame
{
    // Flat race: a course band, two runners, a crowd of flags and three drums.
    public sealed class RaceView : MonoBehaviour
    {
        private static readonly Color[] Skies = {
            new Color32(201, 230, 195, 255), new Color32(197, 215, 242, 255),
            new Color32(183, 224, 240, 255), new Color32(28, 33, 64, 255) };
        private static readonly Color[] Tracks = {
            new Color32(228, 196, 138, 255), new Color32(196, 180, 154, 255),
            new Color32(230, 211, 161, 255), new Color32(58, 64, 96, 255) };

        [SerializeField] private Image sky, track;
        [SerializeField] private RectTransform turtle, rabbit, mallet;
        [SerializeField] private Image rabbitOpen, rabbitShut, rabbitMouth;
        [SerializeField] private Image ribbon, flower, star;
        [SerializeField] private Image[] flags;
        [SerializeField] private Image[] drums;
        [SerializeField] private Text bubble, steps, course, sprint, zzz;
        [SerializeField] private Image bubbleBack;
        private static readonly Color StageTwoSky = new Color32(201, 182, 240, 255);
        private static readonly Color StageTwoTrack = new Color32(236, 224, 250, 255);
        private CanvasGroup[] groups;
        private float[] restAlpha, flash;
        private Vector2[] drumHome;
        private Vector3[] restScale;
        private Vector2 ribbonHome, flowerHome, starHome;
        // Per flag: time left until the called beat, the whole lead-in, and the swing after the beat.
        private float[] lead, leadTotal, swing, swingTotal, swingSize;
        private RectTransform[] sticks, fans;
        private Vector2[] fanHome;
        private Coroutine stepCo, stumbleCo, sayCo, swingCo;
        private float turtleBase, rabbitBase, lift;
        private bool sleeping;

        private void Awake()
        {
            int count = drums == null ? 0 : drums.Length;
            groups = new CanvasGroup[count];
            restAlpha = new float[count];
            flash = new float[count];
            drumHome = new Vector2[count];
            restScale = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                restScale[i] = Vector3.one;
                if (drums[i] == null) continue;
                groups[i] = drums[i].GetComponent<CanvasGroup>();
                restAlpha[i] = groups[i] != null ? groups[i].alpha : 1f;
                drumHome[i] = drums[i].rectTransform.anchoredPosition;
            }
            if (ribbon != null) ribbonHome = ribbon.rectTransform.anchoredPosition;
            if (flower != null) flowerHome = flower.rectTransform.anchoredPosition;
            if (star != null) starHome = star.rectTransform.anchoredPosition;
            int flagCount = flags == null ? 0 : flags.Length;
            lead = new float[flagCount];
            leadTotal = new float[flagCount];
            swing = new float[flagCount];
            swingTotal = new float[flagCount];
            swingSize = new float[flagCount];
            sticks = new RectTransform[flagCount];
            fans = new RectTransform[flagCount];
            fanHome = new Vector2[flagCount];
            for (int i = 0; i < flagCount; i++)
            {
                if (flags[i] == null) continue;
                RectTransform stick = flags[i].rectTransform.parent as RectTransform;
                // Scenes built before the flag sticks swing only the cloth and never move the field.
                bool held = stick != null && stick.name == "Stick";
                sticks[i] = held ? stick : flags[i].rectTransform;
                fans[i] = held ? stick.parent as RectTransform : null;
                if (fans[i] != null) fanHome[i] = fans[i].anchoredPosition;
            }
            if (turtle != null) turtleBase = turtle.anchoredPosition.y;
            if (rabbit != null) rabbitBase = rabbit.anchoredPosition.y;
            HideSay();
            if (zzz != null) zzz.gameObject.SetActive(false);
            if (sprint != null) sprint.gameObject.SetActive(false);
            if (rabbitMouth != null) rabbitMouth.gameObject.SetActive(false);
            if (rabbitShut != null) rabbitShut.gameObject.SetActive(false);
            ShowOpening();
        }

        // Every run opens on the plain meadow. Unlocked trim waits until a later stage.
        public void ShowOpening()
        {
            SetCourse(0, 0);
        }

        public void Bind(RaceController owner)
        {
            if (drums == null) return;
            for (int i = 0; i < drums.Length; i++)
            {
                if (drums[i] == null) continue;
                RacePad pad = drums[i].GetComponent<RacePad>();
                if (pad == null) pad = drums[i].gameObject.AddComponent<RacePad>();
                pad.Bind(owner, i);
            }
        }

        public void SetCourse(int which, int shell)
        {
            which = Mathf.Clamp(which, 0, Skies.Length - 1);
            if (sky != null) sky.color = Skies[which];
            if (track != null) track.color = Tracks[which];
            PlaceDecor(ribbon, ribbonHome, shell >= 1);
            PlaceDecor(flower, flowerHome, shell >= 2);
            PlaceDecor(star, starHome, shell >= 3);
        }

        // Stage 2 opens after the turtle crosses the line: purple court and plain drums. The HUD shows the stage.
        public void BeginStageTwo()
        {
            if (sky != null) sky.color = StageTwoSky;
            if (track != null) track.color = StageTwoTrack;
            Peel(ribbon);
            Peel(flower);
            Peel(star);
            RestoreDrums();
            SprintPads(false);
            if (sprint != null) sprint.gameObject.SetActive(false);
        }

        public void ShowSprint()
        {
            SprintPads(true);
            ShowSprintDrum();
        }

        public void CourseName(string text) { if (course != null) course.text = text; }

        public void ShowSteps(int count)
        {
            if (steps != null) steps.text = count + " / " + RaceChart.FinishSteps;
        }

        public void Place(float turtleSteps, float rabbitSteps)
        {
            if (turtle != null)
                turtle.anchoredPosition = new Vector2(X(turtleSteps), turtleBase + lift);
            if (rabbit != null)
            {
                float bob = sleeping || rabbitSteps <= 0f ? 0f : Mathf.Sin(Time.time * 8f) * 4f;
                rabbit.anchoredPosition = new Vector2(X(rabbitSteps), rabbitBase + bob);
            }
        }

        // The beat preview: the fans for this pad raise their flags back over the lead-in and
        // bring them down exactly on the beat. Left four call S, right four call D, everyone calls Z.
        public void Preview(int pad, float secondsToBeat)
        {
            if (lead == null) return;
            float time = Mathf.Max(0.05f, secondsToBeat);
            for (int i = 0; i < lead.Length; i++)
            {
                if (!Calls(pad, i)) continue;
                lead[i] = time;
                leadTotal[i] = time;
            }
        }

        // A struck beat: the same fans wave once more and jump.
        public void Hit(int pad)
        {
            if (swing == null) return;
            for (int i = 0; i < swing.Length; i++)
                if (Calls(pad, i)) Swing(i, 0.32f, 1f);
        }

        public void Cheer()
        {
            if (swing == null) return;
            for (int i = 0; i < swing.Length; i++)
            {
                lead[i] = 0f;
                Swing(i, 0.9f, 1.5f);
            }
        }

        private bool Calls(int pad, int flag)
        {
            if (pad == RaceChart.LeftPad) return flag < lead.Length / 2;
            if (pad == RaceChart.RightPad) return flag >= lead.Length / 2;
            return true;
        }

        private void Swing(int flag, float seconds, float size)
        {
            swing[flag] = seconds;
            swingTotal[flag] = seconds;
            swingSize[flag] = size;
        }

        private void AnimateFlags(float dt)
        {
            if (lead == null) return;
            for (int i = 0; i < lead.Length; i++)
            {
                if (sticks[i] == null) continue;
                float angle = 0f, hop = 0f;
                if (lead[i] > 0f)
                {
                    lead[i] -= dt;
                    float u = 1f - Mathf.Clamp01(lead[i] / leadTotal[i]);
                    angle += 55f * u * u;
                    hop += 6f * u;
                    if (lead[i] <= 0f) Swing(i, 0.28f, 1f);
                }
                if (swing[i] > 0f)
                {
                    swing[i] -= dt;
                    float p = 1f - Mathf.Clamp01(swing[i] / swingTotal[i]);
                    float bend = Mathf.Sin(p * Mathf.PI);
                    angle -= 50f * swingSize[i] * Mathf.Sin(p * Mathf.PI * Mathf.Max(1f, swingSize[i] * 2f)) * (1f - p * 0.5f);
                    hop += 16f * swingSize[i] * bend;
                }
                sticks[i].localEulerAngles = new Vector3(0f, 0f, angle);
                if (fans[i] != null) fans[i].anchoredPosition = fanHome[i] + new Vector2(0f, hop);
            }
        }

        private void StillFlags()
        {
            if (lead == null) return;
            for (int i = 0; i < lead.Length; i++)
            {
                lead[i] = swing[i] = 0f;
                if (sticks[i] != null) sticks[i].localEulerAngles = Vector3.zero;
                if (fans[i] != null) fans[i].anchoredPosition = fanHome[i];
            }
        }

        public RectTransform Drum(int pad)
        {
            if (drums == null || pad < 0 || pad >= drums.Length || drums[pad] == null) return null;
            return drums[pad].rectTransform;
        }

        public void Strike(int pad)
        {
            if (drums == null || pad < 0 || pad >= drums.Length || drums[pad] == null) return;
            flash[pad] = 0.14f;
            Vector3 rest = restScale != null && pad < restScale.Length ? restScale[pad] : Vector3.one;
            drums[pad].rectTransform.localScale = rest * 1.12f;
            if (groups[pad] != null) groups[pad].alpha = 1f;
            if (mallet == null) return;
            if (swingCo != null) StopCoroutine(swingCo);
            mallet.anchoredPosition = drums[pad].rectTransform.anchoredPosition + new Vector2(0f, 78f);
            mallet.SetAsLastSibling();
            swingCo = StartCoroutine(Swing());
        }

        public void Step()
        {
            if (stepCo != null) StopCoroutine(stepCo);
            stepCo = StartCoroutine(Hop());
        }

        public void Stumble()
        {
            if (stumbleCo != null) StopCoroutine(stumbleCo);
            stumbleCo = StartCoroutine(Squash());
        }

        public void Sleep(bool on)
        {
            sleeping = on;
            if (rabbitOpen != null) rabbitOpen.gameObject.SetActive(!on);
            if (rabbitShut != null) rabbitShut.gameObject.SetActive(on);
            if (zzz != null) zzz.gameObject.SetActive(on);
            if (on && rabbitMouth != null) rabbitMouth.gameObject.SetActive(false);
        }

        public void Wake(string text)
        {
            Sleep(false);
            Say(text);
        }

        public void Tease(string text)
        {
            Sleep(false);
            if (rabbitMouth != null) rabbitMouth.gameObject.SetActive(true);
            Say(text);
        }

        public void SprintOn(string text)
        {
            SprintPads(true);
            if (sprint == null) return;
            sprint.text = text;
            sprint.gameObject.SetActive(true);
        }

        public void SprintPads(bool on)
        {
            SetRest(RaceChart.LeftPad, on ? 0.16f : 1f);
            SetRest(RaceChart.RightPad, on ? 0.16f : 1f);
            SetRest(RaceChart.SprintPad, on ? 1f : 0.38f);
        }

        public void ResetTrack()
        {
            Sleep(false);
            HideSay();
            if (rabbitMouth != null) rabbitMouth.gameObject.SetActive(false);
            if (sprint != null) sprint.gameObject.SetActive(false);
            lift = 0f;
            if (turtle != null) turtle.localScale = Vector3.one;
            RestoreDrums();
            SprintPads(false);
            StillFlags();
            Place(0f, 0f);
            ShowSteps(0);
        }

        private void Update()
        {
            AnimateFlags(Time.deltaTime);
            if (flash == null) return;
            for (int i = 0; i < flash.Length; i++)
            {
                if (flash[i] <= 0f || drums[i] == null) continue;
                flash[i] -= Time.deltaTime;
                if (flash[i] > 0f) continue;
                drums[i].rectTransform.localScale = restScale[i];
                if (groups[i] != null) groups[i].alpha = restAlpha[i];
            }
        }

        private void ShowSprintDrum()
        {
            int pad = RaceChart.SprintPad;
            if (drums == null || pad >= drums.Length || drums[pad] == null) return;
            RectTransform rect = drums[pad].rectTransform;
            rect.anchoredPosition = new Vector2(0f, -210f);
            restScale[pad] = new Vector3(2.1f, 2.1f, 1f);
            rect.localScale = restScale[pad];
            rect.SetAsLastSibling();
            if (groups[pad] != null) groups[pad].alpha = 1f;
        }

        private void RestoreDrums()
        {
            if (drums == null || drumHome == null) return;
            for (int i = 0; i < drums.Length; i++)
            {
                if (drums[i] == null) continue;
                restScale[i] = Vector3.one;
                drums[i].rectTransform.localScale = Vector3.one;
                drums[i].rectTransform.anchoredPosition = drumHome[i];
            }
        }

        private void Peel(Image decor)
        {
            if (decor == null || !decor.gameObject.activeSelf) return;
            StartCoroutine(PeelOff(decor.rectTransform));
        }

        private static IEnumerator PeelOff(RectTransform decor)
        {
            Image image = decor.GetComponent<Image>();
            Color color = image != null ? image.color : Color.white;
            float t = 0f;
            while (t < 0.35f && decor != null)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 0.35f);
                decor.localScale = Vector3.one * (1f + u * 0.45f);
                decor.anchoredPosition += new Vector2(0f, 90f * Time.deltaTime);
                if (image != null)
                {
                    color.a = 1f - u;
                    image.color = color;
                }
                yield return null;
            }
            if (decor != null) decor.gameObject.SetActive(false);
        }

        private static void PlaceDecor(Image decor, Vector2 home, bool on)
        {
            if (decor == null) return;
            decor.rectTransform.anchoredPosition = home;
            decor.rectTransform.localScale = Vector3.one;
            Color color = decor.color;
            color.a = 1f;
            decor.color = color;
            decor.gameObject.SetActive(on);
        }

        private void SetRest(int pad, float alpha)
        {
            if (restAlpha == null || pad < 0 || pad >= restAlpha.Length || groups[pad] == null) return;
            restAlpha[pad] = alpha;
            if (flash[pad] <= 0f) groups[pad].alpha = alpha;
        }

        private void Say(string text)
        {
            if (bubble != null) bubble.text = text;
            if (bubbleBack != null) bubbleBack.gameObject.SetActive(true);
            if (sayCo != null) StopCoroutine(sayCo);
            sayCo = StartCoroutine(HideSoon());
        }

        private void HideSay()
        {
            if (bubbleBack != null) bubbleBack.gameObject.SetActive(false);
        }

        private IEnumerator HideSoon()
        {
            yield return new WaitForSeconds(0.9f);
            HideSay();
        }

        private IEnumerator Hop()
        {
            lift = 12f;
            yield return new WaitForSeconds(0.09f);
            lift = 0f;
        }

        private IEnumerator Squash()
        {
            if (turtle == null) yield break;
            turtle.localScale = new Vector3(1.08f, 0.8f, 1f);
            yield return new WaitForSeconds(0.12f);
            turtle.localScale = Vector3.one;
        }

        private IEnumerator Swing()
        {
            mallet.localEulerAngles = new Vector3(0f, 0f, 30f);
            yield return new WaitForSeconds(0.08f);
            mallet.localEulerAngles = new Vector3(0f, 0f, -18f);
        }

        private static float X(float count)
        {
            return Mathf.Lerp(-480f, 400f, Mathf.Clamp01(count / RaceChart.FinishSteps));
        }

        private sealed class RacePad : MonoBehaviour, IPointerDownHandler
        {
            private RaceController owner;
            private int pad;

            public void Bind(RaceController owner, int pad)
            {
                this.owner = owner;
                this.pad = pad;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (eventData.button != PointerEventData.InputButton.Left || owner == null) return;
                owner.NoteScreenTouch();
                owner.Press(pad);
            }
        }
    }
}
