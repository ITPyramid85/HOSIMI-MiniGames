using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // Looping visual demonstration only; it never submits input or touches player data.
    public sealed class MiniGameGuide : MonoBehaviour
    {
        [SerializeField] private Image[] demoPads;
        [SerializeField] private RectTransform finger;
        [SerializeField] private Text instruction, characterName, feedback;
        [SerializeField] private Image portrait;
        [SerializeField] private GameObject placeholderAvatar;
        private float startedAt;
        private void OnEnable()
        {
            startedAt = Time.unscaledTime;
            var data = GameManager.EnsureInstance().Data;
            CharacterConfig character = data.Config.Character(data.Player.equippedCharacterId);
            characterName.text = character.DisplayName;
            if (portrait != null)
            {
                portrait.sprite = CharacterArt.Portrait(character);
                portrait.enabled = portrait.sprite != null;
                if (placeholderAvatar != null) placeholderAvatar.SetActive(!portrait.enabled);
            }
            instruction.text = Strings.T("guide.light_on_tap");
        }
        private void Update()
        {
            float elapsed = Time.unscaledTime - startedAt;
            float phase = elapsed % 3;
            int pad = 5 + ((int)(elapsed / 3) % 3);
            for (int i = 0; i < demoPads.Length; i++)
                demoPads[i].color = i == pad && phase >= 1 && phase < 2 ? Palette.Sky : Palette.Well;
            Vector2 target = ((RectTransform)demoPads[pad].transform).anchoredPosition;
            finger.anchoredPosition = Vector2.Lerp(target + new Vector2(23, -43), target, Mathf.Clamp01((phase - 1) * 2));
            finger.localScale = Vector3.one * (phase >= 2 && phase < 2.25f ? .8f : 1);
            feedback.text = phase >= 2 ? "PERFECT" : "";
        }
    }
}
