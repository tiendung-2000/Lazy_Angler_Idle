using System.Linq;
using LazyAngler.Core;
using LazyAngler.Data;
using LazyAngler.Systems;
using LazyAngler.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LazyAngler.Tools
{
    /// <summary>
    /// Menu: Tools > Lazy Angler > Build Prototype.
    /// Tạo 5 loài cá, các nâng cấp, skin, GameConfig, prefab UI (PrototypeBuilder.UI.cs) và scene Prototype.
    /// Chạy lại an toàn: asset đã có thì giữ nguyên số liệu, scene được dựng lại.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        private const string FishFolder = "Assets/_Project/ScriptableObjects/Fish";
        private const string UpgradeFolder = "Assets/_Project/ScriptableObjects/Upgrades";
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/GameConfig.asset";
        private const string ScenePath = "Assets/_Project/Scenes/Prototype.unity";

        private static readonly Color Bg = new Color(0.07f, 0.16f, 0.24f);
        private static readonly Color Panel = new Color(0.05f, 0.1f, 0.16f, 0.92f);
        private static readonly Color Accent = new Color(0.98f, 0.73f, 0.2f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.55f, 0.42f);

        [MenuItem("Tools/Lazy Angler/Build Prototype")]
        public static void Build()
        {
            var config = BuildData();
            BuildZones();
            ApplyBalance();
            BuildSkins();
            BuildPearlUpgrades();
            BuildQuests();
            BuildBaits();
            BuildUI();
            BuildScene(config);
            Debug.Log("[LazyAngler] Prototype đã dựng xong: " + ScenePath);
        }

        // ---------- Data ----------

        public static GameConfig BuildData()
        {
            var fish = new[]
            {
                Fish("ro_dong", "Cá Rô Đồng", Rarity.Common, 2, 8, 20),
                Fish("chep", "Cá Chép", Rarity.Uncommon, 5, 20, 60),
                Fish("tre", "Cá Trê", Rarity.Rare, 15, 25, 70),
                Fish("loc_bong", "Cá Lóc Bông", Rarity.Epic, 50, 40, 100),
                Fish("koi_vang", "Cá Koi Vàng", Rarity.Legendary, 200, 30, 90),
            };

            var rod = Upgrade("Upgrade_Rod", UpgradeType.Rod, "Cần câu", 15, "Giảm thời gian mỗi lần quăng mồi.");
            var bait = Upgrade("Upgrade_Bait", UpgradeType.Bait, "Mồi câu", 25, "Tăng tỉ lệ cá Rare, Epic, Legendary.");
            var bite = Upgrade("Upgrade_BiteChance", UpgradeType.BiteChance, "Tỉ lệ cá cắn", 20, "Tăng tỉ lệ cá cắn câu mỗi lần kiểm tra.");
            // Thuyền mạnh nhất (mỗi cấp thêm một cần, tối đa 10) nên giá tăng nhanh; Két tối đa 12 cấp (24 giờ).
            var boat = Upgrade("Upgrade_Boat", UpgradeType.Boat, "Thuyền", 200, "Thêm một cần câu chạy song song.", growth: 2.8, maxLevel: 10);
            var vault = Upgrade("Upgrade_Vault", UpgradeType.Vault, "Két offline", 150, "Tính thu nhập offline được lâu hơn.", growth: 1.7, maxLevel: 12);
            var value = Upgrade("Upgrade_FishValue", UpgradeType.FishValue, "Giá trị cá", 30, "Mỗi con cá bán được nhiều xu hơn.", growth: 1.2);
            var luck = Upgrade("Upgrade_Luck", UpgradeType.Luck, "May mắn", 50, "Tăng tỉ lệ gặp cá hiếm (Hiếm, Sử thi, Huyền thoại).", growth: 1.3);

            var config = LoadOrCreate<GameConfig>(ConfigPath);
            config.fish.Clear();
            config.fish.AddRange(fish);
            config.rodUpgrade = rod;
            config.baitUpgrade = bait;
            config.biteUpgrade = bite;
            config.boatUpgrade = boat;
            config.vaultUpgrade = vault;
            config.fishValueUpgrade = value;
            config.luckUpgrade = luck;
            ApplyDefaultMinigames(fish);

            // Pixel art + 5 cấp cần câu gắn vào nâng cấp "Cần câu" có sẵn (cấp 0..4 = Lv1..Lv5).
            FishingArtExporter.Export();
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            config.rodTiers = new System.Collections.Generic.List<RodTierData>();
            foreach (var path in AssetDatabase.FindAssets("t:RodTierData", new[] { FishingArtExporter.RodFolder })
                         .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
                config.rodTiers.Add(AssetDatabase.LoadAssetAtPath<RodTierData>(path));
            config.rodUpgrade = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>($"{UpgradeFolder}/Upgrade_Rod.asset");
            config.rodUpgrade.maxLevel = Mathf.Max(0, config.rodTiers.Count - 1);
            config.rodUpgrade.description = "Cần tốt hơn: câu nhanh hơn và cá cắn nhiều hơn.";
            EditorUtility.SetDirty(config.rodUpgrade);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static FishData Fish(string id, string name, Rarity rarity, int price, float minSize, float maxSize, int zone = 0)
        {
            string path = $"{FishFolder}/Fish_{id}.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<FishData>(path) == null;
            var f = LoadOrCreate<FishData>(path);
            if (isNew)
            {
                f.fishId = id;
                f.displayName = name;
                f.rarity = rarity;
                f.zoneIndex = zone;
                f.basePrice = price;
                f.sizeRange = new Vector2(minSize, maxSize);
                EditorUtility.SetDirty(f);
            }
            return f;
        }

        /// <summary>
        /// Gán minigame mặc định cho cá Rare trở lên chưa được cấu hình (type = None).
        /// Cá đã cấu hình trong Inspector được giữ nguyên.
        /// </summary>
        public static void ApplyDefaultMinigames(System.Collections.Generic.IEnumerable<FishData> fish)
        {
            var easy = RadialPreset("Radial_Easy", "Dễ", 100f, 50f, 1, 3);
            RadialPreset("Radial_Medium", "Trung bình", 180f, 35f, 2, 5);
            var hard = RadialPreset("Radial_Hard", "Khó", 260f, 22f, 3, 6);

            foreach (var f in fish)
            {
                if (f == null || f.rarity < Rarity.Rare) continue;
                if (f.minigame == null || f.minigame.type == MinigameType.None)
                {
                    f.minigame = DefaultMinigame(f.rarity);
                    EditorUtility.SetDirty(f);
                }
                // Cá Radial chưa chọn preset: Rare → Dễ, Legendary → Khó (Epic giữ Pendulum).
                if (f.radialConfig == null && f.minigame.type == MinigameType.Radial)
                {
                    f.radialConfig = f.rarity >= Rarity.Legendary ? hard : easy;
                    EditorUtility.SetDirty(f);
                }
            }
        }

        private const string MinigameFolder = "Assets/_Project/ScriptableObjects/Minigames";

        /// <summary>Preset độ khó Radial (thông số prototype). Đã có thì giữ nguyên số liệu đã chỉnh.</summary>
        private static RadialFishingConfig RadialPreset(string file, string name, float speed, float width, int required, int attempts)
        {
            if (!AssetDatabase.IsValidFolder(MinigameFolder))
                AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "Minigames");
            string path = $"{MinigameFolder}/{file}.asset";
            var preset = AssetDatabase.LoadAssetAtPath<RadialFishingConfig>(path);
            if (preset != null) return preset;
            preset = ScriptableObject.CreateInstance<RadialFishingConfig>();
            preset.difficultyName = name;
            preset.rotationSpeed = speed;
            preset.targetAngleWidth = width;
            preset.requiredSuccesses = required;
            preset.maxAttempts = attempts;
            preset.randomizeTarget = true;
            preset.targetCount = 1;
            AssetDatabase.CreateAsset(preset, path);
            return preset;
        }

        private static RareMinigameSettings DefaultMinigame(Rarity rarity) => rarity switch
        {
            Rarity.Rare => new RareMinigameSettings
            {
                type = MinigameType.Radial, rotationSpeed = 160f, targetAngleWidth = 50f,
                requiredSuccesses = 2, maxAttempts = 4, targetCount = 1,
            },
            Rarity.Epic => new RareMinigameSettings
            {
                type = MinigameType.Pendulum, minAngle = -70f, maxAngle = 70f, oscillationSpeed = 0.7f,
                targetAngleWidth = 26f, requiredSuccesses = 3, maxAttempts = 5,
            },
            _ => new RareMinigameSettings
            {
                type = MinigameType.Radial, rotationSpeed = 260f, targetAngleWidth = 32f,
                requiredSuccesses = 3, maxAttempts = 5, targetCount = 2,
            },
        };

        private static UpgradeDefinition Upgrade(string file, UpgradeType type, string name, double cost, string desc,
            double growth = 1.15, int maxLevel = 0)
        {
            string path = $"{UpgradeFolder}/{file}.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path) == null;
            var u = LoadOrCreate<UpgradeDefinition>(path);
            if (isNew)
            {
                u.type = type;
                u.displayName = name;
                u.baseCost = cost;
                u.costGrowth = growth;
                u.maxLevel = maxLevel;
                u.description = desc;
                EditorUtility.SetDirty(u);
            }
            return u;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---------- Scene ----------

        public static void BuildScene(GameConfig config)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene có thể unload asset không còn được tham chiếu, nên nạp lại theo đường dẫn.
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Bg;
            camGo.transform.position = new Vector3(0, 0, -10);

            var gmGo = new GameObject("GameManager", typeof(GameManager));
            SetRef(gmGo.GetComponent<GameManager>(), "config", config);

            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            var root = canvasGo.transform;

            // Vùng cảnh câu (trong suốt, không có Graphic): camera thế giới chỉ vẽ trong vùng này,
            // HUD (GameplayScreen) phủ phía trên và dưới với cùng kích thước HudTop/HudBottom.
            var water = new GameObject("WorldArea", typeof(RectTransform)).GetComponent<RectTransform>();
            water.SetParent(root, false);
            Stretch(water, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, HudBottom), new Vector2(0, -HudTop));
            BuildFishingWorld(water);

            var ctrl = new GameObject("RareFishing", typeof(RareFishingMinigameController)).GetComponent<RareFishingMinigameController>();
            SetRef(gmGo.GetComponent<GameManager>(), "rareMinigame", ctrl);

            // Mọi màn hình/popup là prefab trong UIRegistry, UIManager sinh khi được gọi (scene không chứa sẵn UI).
            var ui = canvasGo.AddComponent<UIManager>();
            SetRef(ui, "registry", AssetDatabase.LoadAssetAtPath<UIRegistry>(RegistryPath));
            canvasGo.AddComponent<GameUIFlow>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
        }

        private const string UrpSpriteUnlitMaterial =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        /// <summary>
        /// Cảnh câu: FishingSceneBootstrap nạp prefab map của bản đồ đang chơi (ZoneData.mapPrefab), hiển thị trong vùng trống của HUD.
        /// Camera nền phủ toàn màn hình để các khe ngoài vùng cảnh câu luôn có màu nền.
        /// </summary>
        private static void BuildFishingWorld(RectTransform viewport)
        {
            var bgCam = new GameObject("BackgroundCamera", typeof(Camera)).GetComponent<Camera>();
            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = Bg;
            bgCam.cullingMask = 0;
            bgCam.depth = -10;
            bgCam.orthographic = true;

            var main = Camera.main;
            if (main != null) main.depth = 0;

            var boot = new GameObject("FishingSceneBootstrap").AddComponent<LazyAngler.Visual.FishingSceneBootstrap>();
            SetRef(boot, "spriteMaterial", AssetDatabase.LoadAssetAtPath<Material>(UrpSpriteUnlitMaterial));
            SetRef(boot, "viewportArea", viewport);
            SetRef(boot, "worldCamera", main);
            // Dự phòng khi ZoneData chưa có mapPrefab: map đầu tiên.
            SetRef(boot, "defaultMapPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{MapFolder}/Map_0_{ZoneSpecs[0].id}.prefab"));
            SetRef(boot, "art", AssetDatabase.LoadAssetAtPath<LazyAngler.Visual.FishingArtSet>(FishingArtExporter.ArtSetPath));
        }

        /// <summary>Image pixel art: phủ kín cha (size null) hoặc đặt giữa với kích thước cho trước.</summary>
        private static Image PixelImage(string name, Transform parent, Sprite sprite, Color color, Vector2? size = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            if (size.HasValue)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = size.Value;
                rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            return img;
        }

        /// <summary>Hàng chỉ báo: nhãn bên trái + vùng chứa pip (HorizontalLayoutGroup) bên phải.</summary>
        private static RectTransform PipRow(string name, string label, Transform parent, Vector2 aMin, Vector2 aMax)
        {
            var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            Stretch(row, aMin, aMax, Vector2.zero, Vector2.zero);
            var text = Label("Label", row, label, 40, Color.white, new Vector2(0f, 0f), new Vector2(0.28f, 1f));
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var pips = new GameObject("Pips", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            pips.SetParent(row, false);
            Stretch(pips, new Vector2(0.28f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var layout = pips.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 14f;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return pips;
        }

        // ---------- Helpers ----------

        private static RectTransform Box(string name, Transform parent, Color color, Vector2 aMin, Vector2 aMax,
            Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt, aMin, aMax, offMin, offMax);
            return rt;
        }

        private static TMP_Text Label(string name, Transform parent, string text, float size, Color color,
            Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            Stretch(t.rectTransform, aMin, aMax, Vector2.zero, Vector2.zero);
            return t;
        }

        private static RectTransform Button(string name, Transform parent, string text, float size, Color color,
            Vector2 aMin, Vector2 aMax)
        {
            var rt = Box(name, parent, color, aMin, aMax, Vector2.zero, Vector2.zero);
            var img = rt.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            Label("Text", rt, text, size, Color.white, Vector2.zero, Vector2.one);
            return rt;
        }

        private static void Stretch(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
