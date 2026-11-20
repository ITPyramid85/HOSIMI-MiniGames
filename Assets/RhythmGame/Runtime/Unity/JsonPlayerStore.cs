using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RhythmGame
{
    public sealed class JsonPlayerStore
    {
        public string FilePath { get; private set; }
        public bool RecoveredFromBackup { get; private set; }

        public JsonPlayerStore(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("Save path is empty.");
            FilePath = Path.GetFullPath(filePath);
        }

        public PlayerData LoadOrCreate(GameConfig config)
        {
            RecoveredFromBackup = false;
            if (!File.Exists(FilePath) && !File.Exists(FilePath + ".bak")) return PlayerData.Create(config);
            Exception primaryError = null;
            if (File.Exists(FilePath))
            {
                try { return Read(FilePath, config); }
                catch (Exception e) when (IsRecoverable(e)) { primaryError = e; }
            }
            if (File.Exists(FilePath + ".bak"))
            {
                try
                {
                    PlayerData player = Read(FilePath + ".bak", config);
                    RecoveredFromBackup = true;
                    return player;
                }
                catch (Exception e) when (IsRecoverable(e))
                {
                    throw new InvalidDataException("Primary and backup saves could not be loaded. Existing files were preserved.", e);
                }
            }
            throw new InvalidDataException("Save could not be loaded. Existing file was preserved.", primaryError);
        }

        private static bool IsRecoverable(Exception e)
        {
            return e is IOException || e is ArgumentException || e is InvalidOperationException;
        }

        private static PlayerData Read(string path, GameConfig config)
        {
            PlayerData player = JsonUtility.FromJson<PlayerData>(File.ReadAllText(path, Encoding.UTF8));
            if (player == null) throw new InvalidDataException("Empty save.");
            player.MigrateToCurrentVersion();
            player.Validate(config);
            return player;
        }

        public void Save(PlayerData player, GameConfig config)
        {
            player.Validate(config);
            string directory = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(directory);
            string tempPath = FilePath + ".tmp";
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(player, true));
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(FilePath)) File.Replace(tempPath, FilePath, FilePath + ".bak");
            else File.Move(tempPath, FilePath);
        }
    }
}
