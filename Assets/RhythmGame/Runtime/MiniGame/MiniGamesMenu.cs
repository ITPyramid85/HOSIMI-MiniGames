using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    // The ten drum mini-games of plan §4, in plan order. A card shows the longest time only.
    // A card is playable once its scene exists. Best stage stays in the save.
    public sealed class MiniGamesMenu : MonoBehaviour
    {
        public sealed class Entry
        {
            public string Id, Scene;
            public Entry(string id, string scene) { Id = id; Scene = scene; }
        }

        public static readonly Entry[] Games = {
            new Entry("mochi", "01.Mochi"), new Entry("whack", "02.WhackAMole"), new Entry("nails", "03.DrivingNail"), new Entry("wood", "04.ChoppingWood"),
            new Entry("smith", "05.Smithy"), new Entry("ice", "06.IceFishing"), new Entry("laundry", "07.FabricBeating"), new Entry("echo", "08.TappingBeat"),
            new Entry("race", "09.RabbitTurtle"), new Entry("wall", "10.BreakingBricks") };

        [SerializeField] private Button[] cards;   // same order as Games
        [SerializeField] private Button backButton;
        [SerializeField] private Text title, balance;
        private bool loading;

        private void Awake()
        {
            GameManager.EnsureInstance();
            for (int i = 0; i < cards.Length; i++)
            {
                Entry game = Games[i];
                cards[i].onClick.AddListener(() => Open(game));
            }
            backButton.onClick.AddListener(() => Load("MainMenu"));
            Refresh();
            GetComponent<MenuTypography>().Apply();
        }

        private void Refresh()
        {
            RhythmGameData data = GameManager.Instance.Data;
            title.text = Strings.T("games.title");
            balance.text = Strings.T("menu.balance") + data.Player.coins.ToString("N0");
            backButton.GetComponentInChildren<Text>().text = Strings.T("games.back");
            for (int i = 0; i < cards.Length; i++)
            {
                Entry game = Games[i];
                Transform card = cards[i].transform;
                card.Find("Name").GetComponent<Text>().text = Strings.T("games." + game.Id);
                MiniGameRecord record = data.Player.FindMiniGame(game.Id);
                bool open = IsPlayable(game, data.Player);
                card.Find("Best").GetComponent<Text>().text = !open ? Strings.T("games.locked") :
                    record == null ? Strings.T("games.not_played") : LongestTime(record);
                cards[i].interactable = open;
                Image face = cards[i].targetGraphic as Image;
                if (face != null) face.color = open ? Palette.Rainbow[i % Palette.Rainbow.Length] : Palette.Well;
            }
        }

        // Cards, and any later stats screen, name a run by its longest time. Best stage stays in the save.
        public static string LongestTime(MiniGameRecord record)
        {
            return Strings.T("games.longest") + MiniGameController.Clock(record.bestSeconds);
        }

        // Every built game opens from the list, including Wall Breaker.
        public static bool IsPlayable(Entry game, PlayerData player)
        {
            return game != null && !string.IsNullOrEmpty(game.Scene);
        }

        public void Open(Entry game)
        {
            if (IsPlayable(game, GameManager.Instance.Data.Player)) Load(game.Scene);
        }

        private void Load(string scene)
        {
            if (loading) return;
            loading = true;
            SceneTransition.Load(scene);
        }
    }
}
