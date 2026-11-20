using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.Editor
{
    public static class CharacterMenuBuilder
    {
        public const string PrefabPath = "Assets/RhythmGame/Prefabs/CharacterPanel.prefab";

        public static void BuildAndConnect()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath);
            CharacterPopup prefab = GetOrCreatePrefab();
            if (prefab == null) throw new System.InvalidOperationException("Character prefab is missing its controller.");
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assign(menu, "characterPrefab", prefab);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("CHARACTER_MENU_READY");
        }

        // Creates the bare panel (blocker, typography, controller); PixelCharacterBuilder builds its Card.
        public static CharacterPopup GetOrCreatePrefab()
        {
            if (File.Exists(PrefabPath)) return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CharacterPopup>();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            RectTransform root = Rect("CharacterPanel", null, 0, 0, 0, 0);
            Stretch(root);
            Image blocker = root.gameObject.AddComponent<Image>();
            blocker.color = Palette.Dim;
            root.gameObject.AddComponent<MenuTypography>();
            root.gameObject.AddComponent<CharacterPopup>();
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            Object.DestroyImmediate(root.gameObject);
            PixelCharacterBuilder.Rebuild();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CharacterPopup>();
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void Assign(Object target, params object[] pairs)
        {
            var serialized = new SerializedObject(target);
            for (int i = 0; i < pairs.Length; i += 2) serialized.FindProperty((string)pairs[i]).objectReferenceValue = (Object)pairs[i + 1];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
