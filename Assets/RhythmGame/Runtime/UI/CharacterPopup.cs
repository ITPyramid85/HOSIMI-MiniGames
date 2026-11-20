using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmGame
{
    // Character select (plan 2-2): one character per page, flipped left/right by swipe or arrows.
    // Left is the character's home world, right its story card and the three fixed buttons:
    // get / equip (drumsticks), claim the physical figure (drumsticks), buy merch (official store, cash).
    // Page state changes immediately; only the stage slide and world colour are animated.
    public sealed class CharacterPopup : MonoBehaviour
    {
        private const float SlideDistance = 160, SlideSeconds = .22f, SwipeThreshold = 48;
        private static readonly Color Silhouette = Palette.Ink;
        private static readonly Color DotOff = new Color(1, 1, 1, .6f);

        [SerializeField] private Button closeButton, primaryButton, claimButton, merchButton, previousButton, nextButton, retrySaveButton;
        [SerializeField] private Text title, balance, worldLabel, characterName, story, ownership, prices, status;
        [SerializeField] private Image world, portrait, dotTemplate;
        [SerializeField] private RectTransform stage;
        [SerializeField] private Image[] tags;
        private readonly List<Image> dots = new List<Image>();
        private RhythmGameData data;
        private int selectedIndex;
        private Coroutine slide;
        private PopupTransition transition;
        private Vector2 dragStart;
        public event Action Closed;
        public bool IsOpen { get { return transition.IsOpen; } }
        public bool IsAnimating { get; private set; }
        public int PageIndex { get { return selectedIndex; } }
        public string SelectedCharacterId { get { return data.Config.characters[selectedIndex].id; } }
        // Physical merch only ships within Korea (plan 2-3); elsewhere buttons 2 and 3 only explain that.
        public static bool DeliversHere = DetectKorea();

        private static void Label(Button button, string text) { button.GetComponentInChildren<Text>().text = text; }

        private static bool DetectKorea()
        {
            try { return RegionInfo.CurrentRegion.TwoLetterISORegionName == "KR"; }
            catch (Exception) { return false; }
        }

        private void Awake()
        {
            data = GameManager.EnsureInstance().Data;
            transition = PopupTransition.For(gameObject);
            closeButton.onClick.AddListener(Close);
            primaryButton.onClick.AddListener(PurchaseOrEquip);
            claimButton.onClick.AddListener(ClaimPhysical);
            merchButton.onClick.AddListener(BuyMerch);
            previousButton.onClick.AddListener(Previous);
            nextButton.onClick.AddListener(Next);
            retrySaveButton.onClick.AddListener(RetrySave);
            for (int i = 0; i < data.Config.characters.Length; i++)
            {
                Image dot = Instantiate(dotTemplate, dotTemplate.transform.parent, false);
                dot.name = "Dot" + i;
                dot.gameObject.SetActive(true);
                dots.Add(dot);
            }
            GetComponent<MenuTypography>().Apply();
        }

        private void OnEnable()
        {
            // Every visit opens on the current guide character.
            selectedIndex = Math.Max(0, Array.FindIndex(data.Config.characters, c => c.id == data.Player.equippedCharacterId));
            StopSlide();
            stage.anchoredPosition = Vector2.zero;
            world.color = WorldColor(data.Config.characters[selectedIndex]);
            Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        public void Next() { Flip(1); }
        public void Previous() { Flip(-1); }

        // Pages wrap around so a swipe always moves.
        private void Flip(int direction)
        {
            int count = data.Config.characters.Length;
            ShowPage((selectedIndex + direction + count) % count, direction);
        }

        public void SelectCharacter(string id)
        {
            int index = Array.FindIndex(data.Config.characters, c => c.id == id);
            if (index < 0) throw new ArgumentException("Unknown character.", "id");
            if (index != selectedIndex) ShowPage(index, index > selectedIndex ? 1 : -1);
        }

        private void ShowPage(int index, int direction)
        {
            Color fromColor = world.color;
            selectedIndex = index;
            Refresh();
            StopSlide();
            IsAnimating = isActiveAndEnabled;
            if (IsAnimating) slide = StartCoroutine(Slide(direction, fromColor, WorldColor(data.Config.characters[index])));
            else { stage.anchoredPosition = Vector2.zero; world.color = WorldColor(data.Config.characters[index]); }
        }

        // The new character slides in from the side it was flipped towards; the world colour steps over.
        private IEnumerator Slide(int direction, Color from, Color to)
        {
            yield return PixelTween.Run(SlideSeconds, t =>
            {
                float eased = PixelTween.OutCubic(t);
                stage.anchoredPosition = new Vector2(PixelTween.Snap(direction * SlideDistance * (1 - eased)), 0);
                world.color = Color.Lerp(from, to, PixelTween.Steps(t, 4));
            });
            IsAnimating = false;
        }

        private void StopSlide()
        {
            if (slide != null) StopCoroutine(slide);
            slide = null;
            IsAnimating = false;
        }

        private static Color WorldColor(CharacterConfig character)
        {
            Color color;
            return Palette.Lift(ColorUtility.TryParseHtmlString(character.worldColorHtml, out color) ? color : Palette.Sky);
        }

        private void Refresh(string message = "")
        {
            CharacterConfig[] all = data.Config.characters;
            CharacterConfig character = all[selectedIndex];
            OwnedCharacter owned = data.Player.FindCharacter(character.id);
            bool equipped = data.Player.equippedCharacterId == character.id;
            int ownedCount = 0;
            foreach (CharacterConfig c in all) if (data.Player.FindCharacter(c.id) != null) ownedCount++;

            title.text = Strings.T("character.character") + (selectedIndex + 1) + " / " + all.Length;
            balance.text = Strings.T("character.drumsticks") + data.Player.coins.ToString("N0");
            Label(closeButton, Strings.T("character.back"));
            Label(retrySaveButton, Strings.T("character.retry_save"));
            worldLabel.text = Strings.T("character.world") + (selectedIndex + 1) + Strings.T("character.world_separator") + character.World.Current;

            Sprite sprite = CharacterArt.Portrait(character);
            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
            // Unowned characters show as a silhouette (plan 2-2).
            portrait.color = owned == null ? Silhouette : Color.white;
            for (int i = 0; i < dots.Count; i++)
                dots[i].color = i == selectedIndex ? Palette.Sun : data.Player.FindCharacter(all[i].id) != null ? Palette.Ink : DotOff;

            characterName.text = character.DisplayName + Strings.T("character.working_name");
            RefreshTags(character.Tags.Current);
            story.text = character.Story.Current;
            ownership.text = owned == null ? Strings.T("character.not_owned") :
                equipped ? Strings.T("character.guide") : Strings.T("character.owned");
            ownership.color = owned == null ? Palette.Muted : Palette.MintText;
            prices.text = Strings.T("character.price_prefix") + character.purchasePrice.ToString("N0") + Strings.T("character.figure") +
                character.physicalPrice.ToString("N0");

            // Button 1: get → equip → in use.
            if (owned == null)
            {
                bool affordable = data.Player.coins >= character.purchasePrice;
                Label(primaryButton, Two(Strings.T("character.get_character"),
                    Strings.T("character.drumsticks_2") + character.purchasePrice.ToString("N0") + (affordable ? "" : Strings.T("character.not_enough"))));
                primaryButton.interactable = affordable;
            }
            else
            {
                Label(primaryButton, Two(equipped ? Strings.T("character.in_use") : Strings.T("character.equip"),
                    Strings.T("character.set_as_your_guide")));
                primaryButton.interactable = !equipped;
            }
            // Button 2: greyed until the character is owned.
            claimButton.interactable = owned != null;
            Label(claimButton, Two(Strings.T("character.claim_figure"), Strings.T("character.drumsticks_2") +
                character.physicalPrice.ToString("N0") + (owned == null ? Strings.T("character.get_the_character_first") : "")));
            // Button 3: always available, ownership does not matter.
            Label(merchButton, Two(Strings.T("character.buy_merch"), Strings.T("character.official_store_cash"), true));
            Tint(primaryButton, Palette.Sun, Palette.Ink);
            Tint(claimButton, Palette.Mint, Palette.Ink);
            Tint(merchButton, Palette.Sky, Palette.Ink);

            retrySaveButton.gameObject.SetActive(data.HasUnsavedChanges);
            status.text = data.HasUnsavedChanges ? Strings.T("character.changes_applied_but_not_saved_please_ret") :
                !string.IsNullOrEmpty(message) ? message :
                Strings.T("character.collected") + ownedCount + " / " + all.Length;
            status.color = data.HasUnsavedChanges ? Palette.WarnText : Palette.MintText;
        }

        // Button label plus a dimmer detail line (price or note).
        private static string Two(string label, string detail, bool onDark = false)
        {
            return label + "\n<color=" + (onDark ? "#FFFFFF99" : Palette.Hex(Palette.Ink) + "AA") + ">" + detail + "</color>";
        }

        private static void Tint(Button button, Color fill, Color text)
        {
            button.GetComponent<Image>().color = Palette.Lift(fill);
            Text label = button.GetComponentInChildren<Text>();
            label.color = button.interactable ? text : new Color(text.r, text.g, text.b, .75f);
        }

        // Chips are laid out left to right at their text width.
        private void RefreshTags(string joined)
        {
            string[] parts = string.IsNullOrEmpty(joined) ? new string[0] : joined.Split('|');
            float x = 0;
            for (int i = 0; i < tags.Length; i++)
            {
                bool shown = i < parts.Length;
                tags[i].gameObject.SetActive(shown);
                if (!shown) continue;
                Text text = tags[i].GetComponentInChildren<Text>();
                text.text = parts[i];
                float width = PixelTween.Snap(text.preferredWidth + 24);
                // Chips that would run past the card are dropped rather than clipped.
                if (x + width > ((RectTransform)tags[i].transform.parent).rect.width) { tags[i].gameObject.SetActive(false); continue; }
                tags[i].rectTransform.sizeDelta = new Vector2(width, tags[i].rectTransform.sizeDelta.y);
                tags[i].rectTransform.anchoredPosition = new Vector2(x, 0);
                x += width + 8;
            }
        }

        public void PurchaseOrEquip()
        {
            if (!isActiveAndEnabled || !primaryButton.interactable) return;
            string id = SelectedCharacterId;
            bool purchase = data.Player.FindCharacter(id) == null;
            Apply(() => purchase ? data.Purchase(id) : data.Equip(id), purchase ?
                Strings.T("character.new_character_you_can_equip_it_as_your_g") :
                Strings.T("character.guide_character_updated"));
            if (purchase && data.Player.FindCharacter(id) != null && isActiveAndEnabled) StartCoroutine(PixelTween.Punch(stage, .12f, .24f));
        }

        // The address form and notification mail (plan 2-3) are not built yet; the button already follows the state rules.
        public void ClaimPhysical()
        {
            if (!isActiveAndEnabled || !claimButton.interactable) return;
            CharacterConfig character = data.Config.characters[selectedIndex];
            Refresh(!DeliversHere ? KoreaOnly() : data.Player.coins < character.physicalPrice ?
                Strings.T("character.not_enough_drumsticks_to_claim_the_figur") :
                Strings.T("character.figure_claims_delivery_address_are_comin"));
        }

        public void BuyMerch()
        {
            if (!isActiveAndEnabled) return;
            CharacterConfig character = data.Config.characters[selectedIndex];
            if (!DeliversHere) { Refresh(KoreaOnly()); return; }
            if (string.IsNullOrEmpty(character.merchUrl))
            {
                Refresh(Strings.T("character.the_official_store_page_is_not_set_up_ye"));
                return;
            }
            Application.OpenURL(character.merchUrl);
        }

        private static string KoreaOnly() { return Strings.T("character.delivery_is_currently_available_in_korea"); }

        private void Apply(Func<bool> action, string success)
        {
            try { Refresh(action() ? success : Strings.T("character.requirements_not_met_nothing_changed")); }
            catch (InvalidOperationException) { Refresh(Strings.T("character.finish_the_current_game_before_changing")); }
        }

        public void RetrySave()
        {
            try { data.Save(); Refresh(Strings.T("character.saved")); }
            catch (IOException) { Refresh(); }
            catch (UnauthorizedAccessException) { Refresh(); }
        }

        public void Show() { transition.Show(); }

        private void Update()
        {
            if (!IsOpen) return;
            if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)) Close();
            else if ((Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.dpad.right.wasPressedThisFrame)) Next();
            else if ((Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.dpad.left.wasPressedThisFrame)) Previous();
            TrackSwipe();
        }

        // A horizontal swipe that starts on the world panel flips the page: finger to the left shows the next one.
        private void TrackSwipe()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 position = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
                dragStart = RectTransformUtility.RectangleContainsScreenPoint(world.rectTransform, position, CanvasCamera()) ? position : new Vector2(float.NaN, 0);
            else if (pointer.press.wasReleasedThisFrame && !float.IsNaN(dragStart.x))
            {
                Vector2 delta = position - dragStart;
                float scale = GetComponentInParent<Canvas>().scaleFactor;
                if (Mathf.Abs(delta.x) / scale >= SwipeThreshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) Flip(delta.x < 0 ? 1 : -1);
                dragStart = new Vector2(float.NaN, 0);
            }
        }

        private Camera CanvasCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        public void Close()
        {
            if (!IsOpen) return;
            transition.Hide();
            if (Closed != null) Closed();
        }
    }
}
