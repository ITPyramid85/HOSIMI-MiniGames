using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmGame
{
    // One full-screen cover for every scene change. It fades in over the scene that is still
    // showing, stays up until the next scene has started, then fades out. The camera clear
    // colour never gets a frame of its own, and the purple shutter bars are not used.
    public sealed class SceneTransition : MonoBehaviour
    {
        private const float OutSeconds = .12f, InSeconds = .18f;
        private static SceneTransition instance;
        private Image cover;
        private Coroutine running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; }

        public static void Load(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return;
            if (instance == null)
            {
                var root = new GameObject("SceneTransition");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<SceneTransition>();
            }
            instance.Begin(scene);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            gameObject.AddComponent<CanvasScaler>();
            cover = new GameObject("Cover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            cover.transform.SetParent(transform, false);
            Color color = Palette.Background;
            color.a = 0f;
            cover.color = color;
            cover.raycastTarget = false;
            RectTransform rect = cover.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void Begin(string scene)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Go(scene));
        }

        private IEnumerator Go(string scene)
        {
            cover.raycastTarget = true;
            yield return Fade(cover.color.a, 1f, OutSeconds);
            AsyncOperation load = SceneManager.LoadSceneAsync(scene);
            if (load != null)
                while (!load.isDone) yield return null;
            // Awake and Start of the new scene have run. One more frame lets its canvas draw
            // under this cover before the cover lifts.
            yield return null;
            yield return Fade(1f, 0f, InSeconds);
            cover.raycastTarget = false;
            running = null;
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            Color color = cover.color;
            color.a = alpha;
            cover.color = color;
        }
    }
}
