using UnityEditor;
using UnityEngine;

namespace LazyAngler.Tools
{
    /// <summary>
    /// Menu: Tools > Lazy Angler > Create Folder Structure.
    /// Tạo cây thư mục chuẩn của project. Chạy nhiều lần vẫn an toàn.
    /// </summary>
    public static class ProjectFolderSetup
    {
        private static readonly string[] Folders =
        {
            "Assets/_Project/Scenes",
            "Assets/_Project/Scripts/Core",
            "Assets/_Project/Scripts/Data",
            "Assets/_Project/Scripts/Systems",
            "Assets/_Project/Scripts/UI",
            "Assets/_Project/Scripts/Utils",
            "Assets/_Project/Scripts/Editor",
            "Assets/_Project/Art/Sprites/Fish",
            "Assets/_Project/Art/Sprites/Rods",
            "Assets/_Project/Art/Sprites/UI",
            "Assets/_Project/Art/Sprites/Backgrounds",
            "Assets/_Project/Art/VFX",
            "Assets/_Project/Audio/SFX",
            "Assets/_Project/Audio/Music",
            "Assets/_Project/Prefabs/UI",
            "Assets/_Project/Prefabs/Gameplay",
            "Assets/_Project/ScriptableObjects/Fish",
            "Assets/_Project/ScriptableObjects/Upgrades",
            "Assets/_Project/ScriptableObjects/Zones",
            "Assets/_Project/Settings",
            "Assets/_Project/Fonts",
            "Assets/Tests/EditMode",
            "Assets/Tests/PlayMode",
            "Assets/Plugins",
        };

        [MenuItem("Tools/Lazy Angler/Create Folder Structure")]
        public static void CreateFolders()
        {
            int created = 0;
            foreach (var path in Folders)
            {
                if (EnsureFolder(path)) created++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[LazyAngler] Folder structure ready. Created {created} new folder(s).");
        }

        private static bool EnsureFolder(string fullPath)
        {
            var parts = fullPath.Split('/');
            string current = parts[0];
            bool createdAny = false;

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                    createdAny = true;
                }
                current = next;
            }
            return createdAny;
        }
    }
}
