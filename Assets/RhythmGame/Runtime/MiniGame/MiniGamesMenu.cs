using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmGame
{
    // The ten drum mini-games of plan §4, in plan order. A card shows the longest time only.
    // Wall Breaker stays locked until every other game has a recorded time. Best stage stays in the save.
    public sealed class MiniGamesMenu : MonoBehaviour
    {
        public sealed class Entry
        {
            public string Id, Scene;
            public Entry(string id, string scene) { Id = id; Scene = scene; }
        }

        public static readonly Entry[] Games = {
            new Entry("mochi", "Mochi"), new Entry("whack", "WhackAMole"), new Entry("nails", "DrivingNail"), new Entry("wood", "ChoppingWood"),
            new Entry("smith", "Smithy"), new Entry("ice", "IceFishing"), new Entry("laundry", "FabricBeating"), new Entry("echo", "TappingBeat"),
            new Entry("race", "RabbitTurtle"), new Entry("wall", "BreakingBricks") };

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
                card.Find("Best").GetComponent<Text>().text = !open ? LockedLine(game, data.Player) :
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

        private static string LockedLine(Entry game, PlayerData player)
        {
            if (game.Id == "wall" && game.Scene != null)
                return Strings.T("wall.locked") + WallChart.RecordedTimes(player) + "/" + WallChart.TimesToOpen;
            return Strings.T("games.locked");
        }

        // Wall Breaker is the last card. It opens once every other mini-game has a longest time.
        public static bool IsPlayable(Entry game, PlayerData player)
        {
            if (game == null || string.IsNullOrEmpty(game.Scene)) return false;
            if (game.Id != "wall") return true;
            return WallChart.Unlocked(player);
        }

        public void Open(Entry game)
        {
            if (IsPlayable(game, GameManager.Instance.Data.Player)) Load(game.Scene);
        }

        private void Load(string scene)
        {
            if (loading) return;
            loading = true;
            SceneManager.LoadSceneAsync(scene);
        }
    }
}
