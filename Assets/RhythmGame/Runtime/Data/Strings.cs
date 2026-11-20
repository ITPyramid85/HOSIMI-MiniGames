using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RhythmGame
{
    // All player-facing text lives in Resources/RhythmGame/Localization/strings.csv: one row per key,
    // one column per language code (header row). Adding a language is a new column plus a GameLanguage
    // value; empty cells fall back to Chinese so untranslated text never shows up blank.
    public static class Strings
    {
        public const string Resource = "RhythmGame/Localization/strings";
        private const string FallbackCode = "zh-Hans";
        private static Dictionary<string, Dictionary<string, string>> table;

        public static string T(string key) { return Get(key, CurrentLanguage); }

        public static string Get(string key, GameLanguage language)
        {
            Dictionary<string, string> row;
            if (key == null || !Table.TryGetValue(key, out row))
            {
                Debug.LogWarning("Missing text key: " + key);
                return key;
            }
            string text;
            if (row.TryGetValue(PlayerSettings.CodeOf(language), out text) && !string.IsNullOrEmpty(text)) return text;
            return row.TryGetValue(FallbackCode, out text) ? text : key;
        }

        public static bool Has(string key)
        {
            Dictionary<string, string> row;
            string text;
            return key != null && Table.TryGetValue(key, out row) && row.TryGetValue(FallbackCode, out text) && !string.IsNullOrEmpty(text);
        }

        public static GameLanguage CurrentLanguage
        {
            get
            {
                return GameManager.Instance != null && GameManager.Instance.IsReady
                    ? GameManager.Instance.Data.Settings.language : GameLanguage.SimplifiedChinese;
            }
        }

        private static Dictionary<string, Dictionary<string, string>> Table
        {
            get
            {
                if (table == null)
                {
                    TextAsset asset = Resources.Load<TextAsset>(Resource);
                    if (asset == null) throw new InvalidOperationException("Missing string table: " + Resource);
                    table = Parse(asset.text);
                }
                return table;
            }
        }

        // RFC 4180 CSV (quoted cells may hold commas, quotes and line breaks), as saved by Excel or Sheets.
        public static Dictionary<string, Dictionary<string, string>> Parse(string csv)
        {
            List<List<string>> records = ReadCsv(csv.TrimStart('﻿'));
            if (records.Count == 0 || records[0].Count < 2 || records[0][0] != "key")
                throw new InvalidOperationException("String table needs a 'key' header followed by language codes.");
            List<string> header = records[0];
            var result = new Dictionary<string, Dictionary<string, string>>();
            for (int r = 1; r < records.Count; r++)
            {
                List<string> cells = records[r];
                if (cells.Count == 0 || string.IsNullOrEmpty(cells[0])) continue;
                if (result.ContainsKey(cells[0])) throw new InvalidOperationException("Duplicate text key: " + cells[0]);
                var row = new Dictionary<string, string>();
                for (int c = 1; c < header.Count && c < cells.Count; c++) row[header[c]] = cells[c];
                result[cells[0]] = row;
            }
            return result;
        }

        private static List<List<string>> ReadCsv(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (quoted)
                {
                    if (ch != '"') cell.Append(ch);
                    else if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = false;
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { record.Add(cell.ToString()); cell.Length = 0; }
                else if (ch == '\n' || ch == '\r')
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    record.Add(cell.ToString()); cell.Length = 0;
                    records.Add(record); record = new List<string>();
                }
                else cell.Append(ch);
            }
            if (cell.Length > 0 || record.Count > 0) { record.Add(cell.ToString()); records.Add(record); }
            return records;
        }
    }
}
