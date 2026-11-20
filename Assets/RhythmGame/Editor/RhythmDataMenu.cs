using UnityEditor;
using UnityEngine;

namespace RhythmGame.Editor
{
    public static class RhythmDataMenu
    {
        [MenuItem("Rhythm Game/Validate Default Data")]
        public static void Validate()
        {
            GameConfig config = RhythmGameData.LoadDefaultConfig();
            Debug.Log("音游配置有效：" + config.characters.Length + " 个角色，" + config.charts.Length + " 张示例谱面。");
            Selection.activeObject = Resources.Load<TextAsset>("RhythmGame/default_config");
        }

        [MenuItem("Rhythm Game/Simulate Perfect Run (No Save)")]
        public static void Simulate()
        {
            GameConfig config = RhythmGameData.LoadDefaultConfig();
            var service = new ProgressionService(config, PlayerData.Create(config));
            PlaySession session = service.BeginPlay("first_beat_easy");
            for (int i = 0; i < session.NoteCount; i++) session.SubmitTiming(i, 0);
            PlayResult result = service.Complete(session);
            Debug.Log("模拟完成（未写存档）：得分 " + result.Score + "，准确率 " + result.Accuracy.ToString("P1") +
                "，评级 " + result.Grade + "，最大连击 " + result.MaxCombo + "，鼓棒 +" + result.TotalCoins);
        }
    }
}
