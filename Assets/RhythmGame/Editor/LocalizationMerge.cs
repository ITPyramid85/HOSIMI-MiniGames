using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RhythmGame.Editor
{
    // Adds or updates rows of strings.csv from an update file without touching any other row, so a team
    // that added its own texts can take the login / drum-test texts safely.
    // Menu: Rhythm Game > Merge Text Update. Headless: -executeMethod RhythmGame.Editor.LocalizationMerge.MergeDefault
    public static class LocalizationMerge
    {
        public const string Target = "Assets/RhythmGame/Resources/RhythmGame/Localization/strings.csv";
        public const string DefaultUpdate = "Assets/RhythmGame/Editor/Updates/strings_login_drum.csv";

        [MenuItem("Rhythm Game/Merge Text Update (login + drum test)")]
        public static void MergeDefault()
        {
            int changed = Merge(DefaultUpdate, Target);
            Debug.Log("STRINGS_MERGED " + changed);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("文字合并", "已新增或更新 " + changed + " 行文字。", "OK");
        }

        // Returns how many rows were added or changed.
        public static int Merge(string updatePath, string targetPath)
        {
            List<string[]> target = Read(targetPath), update = Read(updatePath);
            int columns = target[0].Length;
            var index = new Dictionary<string, int>();
            for (int i = 1; i < target.Count; i++) index[target[i][0]] = i;
            int changed = 0;
            for (int i = 1; i < update.Count; i++)
            {
                string[] row = Fit(update[i], columns);
                if (index.TryGetValue(row[0], out int at))
                {
                    if (string.Join("\u0001", target[at]) == string.Join("\u0001", row)) continue;
                    target[at] = row;
                }
                else { index[row[0]] = target.Count; target.Add(row); }
                changed++;
            }
            var text = new StringBuilder();
            foreach (string[] row in target) text.Append(Line(row)).Append('\n');
            File.WriteAllText(targetPath, text.ToString(), new UTF8Encoding(true));
            AssetDatabase.ImportAsset(targetPath);
            return changed;
        }

        private static string[] Fit(string[] row, int columns)
        {
            var fitted = new string[columns];
            for (int i = 0; i < columns; i++) fitted[i] = i < row.Length ? row[i] : "";
            return fitted;
        }

        private static string Line(string[] row)
        {
            var cells = new string[row.Length];
            for (int i = 0; i < row.Length; i++)
            {
                string c = row[i] ?? "";
                cells[i] = c.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + c.Replace("\"", "\"\"") + "\"" : c;
            }
            return string.Join(",", cells);
        }

        // RFC 4180 reader (quoted cells may hold commas, quotes and line breaks).
        public static List<string[]> Read(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿');
            var rows = new List<string[]>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cell.Append(c);
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString()); cell.Clear();
                    if (row.Count > 1 || row[0].Length > 0) rows.Add(row.ToArray());
                    row.Clear();
                }
                else cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row.ToArray()); }
            return rows;
        }
    }
}
