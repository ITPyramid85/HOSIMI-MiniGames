using UnityEngine;

namespace RhythmGame
{
    public enum AudioKind { Music, Effects }

    // Keeps an AudioSource at the player's saved volume for its channel (master × music or
    // master × effects) and follows changes from the settings screen while it plays.
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioChannel : MonoBehaviour
    {
        [SerializeField] private AudioKind kind;
        [SerializeField, Range(0, 1)] private float baseVolume = 1;
        private AudioSource source;
        private RhythmGameData data;

        public static AudioChannel Bind(AudioSource source, AudioKind kind, float baseVolume = 1)
        {
            AudioChannel channel = source.GetComponent<AudioChannel>();
            if (channel == null) channel = source.gameObject.AddComponent<AudioChannel>();
            channel.kind = kind;
            channel.baseVolume = baseVolume;
            channel.Apply();
            return channel;
        }

        // One channel per GameObject, so a source sharing its object with another needs its own child.
        public static AudioSource Create(Transform parent, string name, AudioKind kind)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            Bind(source, kind);
            return source;
        }

        private void OnEnable()
        {
            GameManager manager = GameManager.EnsureInstance();
            data = manager.IsReady ? manager.Data : null;
            if (data != null) data.SettingsChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            if (data != null) data.SettingsChanged -= Apply;
            data = null;
        }

        private void Apply()
        {
            if (source == null) source = GetComponent<AudioSource>();
            if (data == null) return;
            PlayerSettings settings = data.Settings;
            source.volume = baseVolume * (kind == AudioKind.Music ? settings.EffectiveMusicVolume : settings.EffectiveSoundEffectsVolume);
        }
    }
}
