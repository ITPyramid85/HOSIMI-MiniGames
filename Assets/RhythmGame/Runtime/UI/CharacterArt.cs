using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    // Portraits live under Resources and are referenced by CharacterConfig.portraitResource.
    public static class CharacterArt
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        // Null when the character has no portrait yet; callers show a text placeholder instead.
        public static Sprite Portrait(CharacterConfig character)
        {
            if (character == null || string.IsNullOrEmpty(character.portraitResource)) return null;
            Sprite sprite;
            if (!cache.TryGetValue(character.portraitResource, out sprite) || sprite == null)
                cache[character.portraitResource] = sprite = Resources.Load<Sprite>(character.portraitResource);
            return sprite;
        }
    }
}
