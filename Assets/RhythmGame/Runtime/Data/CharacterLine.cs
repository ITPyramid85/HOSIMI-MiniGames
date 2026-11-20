using System;

namespace RhythmGame
{
    // A piece of character text, looked up in the string table by key.
    public sealed class CharacterLine
    {
        public string Key { get; private set; }

        public CharacterLine(string key) { Key = key; }

        public string Text(GameLanguage language)
        {
            PlayerSettings.ValidateLanguage(language);
            return Strings.Get(Key, language);
        }

        public string Current { get { return Text(Strings.CurrentLanguage); } }

        public void Validate()
        {
            if (!Strings.Has(Key)) throw new InvalidOperationException("Missing character text: " + Key);
        }
    }

    public enum DialogueReason { Miss, ComboMilestone }

    public sealed class DialogueCue
    {
        public DialogueReason Reason { get; internal set; }
        public int Combo { get; internal set; }
        public CharacterLine Line { get; internal set; }
    }
}
