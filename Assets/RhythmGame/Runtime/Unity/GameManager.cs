using System;
using UnityEngine;

namespace RhythmGame
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public RhythmGameData Data { get; private set; }
        public string InitializationError { get; private set; }
        public bool IsReady { get { return Data != null; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            ConfigureFrameRate();
            EnsureInstance();
        }

        // Mobile players default to 30 fps. Run at the panel's refresh rate (capped at 120) so
        // cues animate smoothly and input is sampled often; vSync is left to targetFrameRate.
        public static int ConfigureFrameRate()
        {
            QualitySettings.vSyncCount = 0;
            double hz = Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate = hz >= 59 ? (int)Math.Round(Math.Min(hz, 120)) : 60;
            return Application.targetFrameRate;
        }

        public static GameManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            GameManager existing = FindFirstObjectByType<GameManager>();
            if (existing != null)
            {
                existing.Initialize();
                return existing;
            }
            GameManager prefab = Resources.Load<GameManager>("RhythmGame/GameManager");
            if (prefab == null) throw new InvalidOperationException("Missing RhythmGame/GameManager prefab.");
            GameManager manager = Instantiate(prefab);
            manager.name = "GameManager";
            return manager;
        }

        private void Awake() { Initialize(); }

        private void Initialize()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            if (Data != null || InitializationError != null) return;
            try { Data = RhythmGameData.Open(); }
            catch (Exception e)
            {
                // Keep the original save intact; do not silently replace it with a new player.
                InitializationError = e.Message;
                Debug.LogError("GameManager could not load player data: " + e.Message, this);
            }
        }

        public void Save()
        {
            if (Data == null) return;
            try { Data.Save(); }
            catch (Exception e) { Debug.LogError("GameManager could not save player data: " + e.Message, this); }
        }

        public void QuitGame()
        {
            Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() { Save(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
