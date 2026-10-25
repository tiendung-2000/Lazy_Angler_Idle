using System.Collections.Generic;
using LazyAngler.Data;
using LazyAngler.UI;
using LazyAngler.Visual;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.Tools
{
    /// <summary>
    /// Menu: Tools > Lazy Angler > Build UI Prefabs.
    /// Dựng prefab cho từng màn hình/popup (Prefabs/UI) và UIRegistry để UIManager sinh khi cần.
    /// Chạy lại sẽ ghi đè prefab (layout do code quyết định); skin đã có giữ nguyên số liệu.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        private const string UiPrefabFolder = "Assets/_Project/Prefabs/UI";
        private const string RegistryPath = "Assets/_Project/ScriptableObjects/UIRegistry.asset";
        private const string SkinFolder = "Assets/_Project/ScriptableObjects/Skins";

        // Vùng cảnh câu giữa HUD: thanh trên cao 220, thanh điều hướng phía dưới cao 220.
        private const float HudTop = 220f;
        private const float HudBottom = 220f;
        // Khối nút của Menu chính (cao hơn thanh điều hướng, phủ lên phần dưới cảnh câu).
        private const float MenuBottom = 420f;

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color CardBorder = new Color(0.98f, 0.73f, 0.2f, 0.9f);
        private static readonly Color Row = new Color(0.1f, 0.2f, 0.3f, 1f);
        private static readonly Color NavColor = new Color(0.12f, 0.33f, 0.45f);

        [MenuItem("Tools/Lazy Angler/Build UI Prefabs")]
        public static void BuildUIMenu()
        {
            BuildSkins();
            BuildUI();
            Debug.Log("[LazyAngler] Đã dựng prefab UI: " + UiPrefabFolder);
        }

        // ---------- Skin ----------

        public static void BuildSkins()
        {
            EnsureFolder(SkinFolder);
            var skins = new List<SkinData>
            {
                Skin("default", "Ngư dân", "Bộ đồ quen thuộc.", 0, Color.white, Color.white),
                Skin("sunset", "Áo hoàng hôn", "Áo cam ấm áp cho buổi chiều.", 300, new Color(1f, 0.72f, 0.55f), new Color(1f, 0.8f, 0.65f)),
                Skin("ocean", "Áo biển xanh", "Màu của biển khơi.", 1500, new Color(0.6f, 0.8f, 1f), new Color(0.7f, 0.85f, 1f)),
                Skin("forest", "Áo rừng tràm", "Hòa mình cùng thiên nhiên.", 5000, new Color(0.65f, 1f, 0.65f), new Color(0.75f, 1f, 0.75f)),
                Skin("gold", "Ngư dân hoàng kim", "Dành cho cần thủ huyền thoại.", 25000, new Color(1f, 0.88f, 0.4f), new Color(1f, 0.92f, 0.5f)),
            };
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config != null)
            {
                config.skins = skins;
                EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
        }

        private static SkinData Skin(string id, string name, string desc, long price, Color body, Color arm)
        {
            string path = $"{SkinFolder}/Skin_{id}.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<SkinData>(path) == null;
            var s = LoadOrCreate<SkinData>(path);
            if (isNew)
            {
                s.skinId = id;
                s.displayName = name;
                s.description = desc;
                s.price = price;
                s.bodyTint = body;
                s.armTint = arm;
                EditorUtility.SetDirty(s);
            }
            return s;
        }

        // ---------- Prefab UI ----------

        /// <summary>Dựng mọi prefab UI và UIRegistry, trả về registry.</summary>
        public static UIRegistry BuildUI()
        {
            EnsureFolder(UiPrefabFolder);
            var art = AssetDatabase.LoadAssetAtPath<FishingArtSet>(FishingArtExporter.ArtSetPath);

            // Dựng tạm dưới một canvas để RectTransform/CanvasScaler tính đúng, lưu prefab rồi xóa.
            var temp = new GameObject("_UIBuild", typeof(Canvas), typeof(CanvasScaler));
            var root = temp.transform;
            var prefabs = new List<UIPanel>
            {
                Save(BuildMainMenu(root)),
                Save(BuildGameplay(root)),
                Save(BuildUpgradePopup(root)),
                Save(BuildShopPopup(root, art)),
                Save(BuildCollectionPopup(root, art)),
                Save(BuildAfkPopup(root)),
                Save(BuildRareFishPopup(root, art)),
                Save(BuildMapPopup(root)),
                Save(BuildZoneUnlockPopup(root)),
                Save(BuildPrestigePopup(root)),
                Save(BuildPearlShopPopup(root)),
                Save(BuildToastPopup(root)),
                Save(BuildQuestPopup(root)),
                Save(BuildBaitPopup(root)),
                Save(BuildAquariumPopup(root, art)),
                Save(BuildBossFishPopup(root, art)),
                Save(BuildBannerPopup(root)),
            };
            Object.DestroyImmediate(temp);

            var registry = LoadOrCreate<UIRegistry>(RegistryPath);
            registry.EditorSet(prefabs);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            return registry;
        }

        private static UIPanel Save(UIPanel panel)
        {
            string path = $"{UiPrefabFolder}/{panel.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(panel.gameObject, path);
            return prefab.GetComponent<UIPanel>();
        }

        private static T NewPanel<T>(string name, Transform parent, UILayer layer, bool keepCached = true, bool closeOnBack = true)
            where T : UIPanel
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var panel = go.AddComponent<T>();
            var so = new SerializedObject(panel);
            so.FindProperty("layer").enumValueIndex = (int)layer;
            so.FindProperty("keepCached").boolValue = keepCached;
            so.FindProperty("closeOnBack").boolValue = closeOnBack;
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        private static void SetPanelChrome(UIPanel panel, RectTransform content, params Button[] closeButtons)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("content").objectReferenceValue = content;
            var arr = so.FindProperty("closeButtons");
            arr.arraySize = closeButtons.Length;
            for (int i = 0; i < closeButtons.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = closeButtons[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Khung popup chuẩn: nền tối (bấm để đóng) + thẻ có viền, tiêu đề, nút X. Trả về thẻ.</summary>
        private static RectTransform PopupFrame(UIPanel panel, string title, Vector2 aMin, Vector2 aMax, bool dimCloses = true)
        {
            var dim = Box("Dim", panel.transform, Dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var border = Box("Card", panel.transform, CardBorder, aMin, aMax, Vector2.zero, Vector2.zero);
            Sliced(border.GetComponent<Image>());
            var card = Box("Inner", border, Panel, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
            Sliced(card.GetComponent<Image>());
            card.GetComponent<Image>().color = new Color(Panel.r, Panel.g, Panel.b, 1f);
            Label("Title", card, title, 64, Accent, new Vector2(0.1f, 1f), new Vector2(0.9f, 1f)).rectTransform.offsetMin = new Vector2(0, -130);
            var close = Button("CloseButton", card, "X", 56, new Color(0.75f, 0.3f, 0.25f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            close.offsetMin = new Vector2(-130, -130);
            close.offsetMax = new Vector2(-20, -20);

            var buttons = new List<Button> { close.GetComponent<Button>() };
            if (dimCloses)
            {
                var dimBtn = dim.gameObject.AddComponent<Button>();
                dimBtn.transition = Selectable.Transition.None;
                buttons.Add(dimBtn);
            }
            SetPanelChrome(panel, border, buttons.ToArray());
            return card;
        }

        // ---------- Màn hình ----------

        private static UIPanel BuildMainMenu(Transform parent)
        {
            // Không có nền phủ kín: vùng giữa (WorldArea) để trống cho thấy ngư dân đứng chờ trên cầu.
            var panel = NewPanel<MainMenuScreen>("MainMenuScreen", parent, UILayer.Screen, closeOnBack: false);
            var root = panel.transform;

            var top = Box("TitleBar", root, Panel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -HudTop), Vector2.zero);
            Label("Title", top, "LAZY ANGLER", 100, Accent, new Vector2(0f, 0.38f), new Vector2(1f, 1f));
            Label("Subtitle", top, "Câu cá thư giãn · Idle", 38, new Color(1f, 1f, 1f, 0.75f), new Vector2(0f, 0.06f), new Vector2(1f, 0.4f));

            var bottom = Box("MenuBar", root, Panel, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, MenuBottom));
            var coins = Label("Coins", bottom, "0 xu", 56, Color.white, new Vector2(0f, 0.74f), new Vector2(1f, 0.96f));
            var play = Button("PlayButton", bottom, "CHƠI", 80, ButtonColor, new Vector2(0.18f, 0.37f), new Vector2(0.82f, 0.7f));
            var collection = Button("CollectionButton", bottom, "Bộ sưu tập", 46, NavColor, new Vector2(0.06f, 0.08f), new Vector2(0.48f, 0.31f));
            var prestige = Button("PrestigeButton", bottom, "Tái sinh", 46, new Color(0.25f, 0.45f, 0.62f), new Vector2(0.52f, 0.08f), new Vector2(0.94f, 0.31f));

            SetRef(panel, "playButton", play.GetComponent<Button>());
            SetRef(panel, "collectionButton", collection.GetComponent<Button>());
            var sound = Button("SoundButton", bottom, "Âm: Bật", 30, new Color(0.3f, 0.32f, 0.38f), new Vector2(0.79f, 0.76f), new Vector2(0.97f, 0.94f));
            var soundLabel = sound.GetComponentInChildren<TMP_Text>();
            soundLabel.enableAutoSizing = true;
            soundLabel.fontSizeMin = 18;
            soundLabel.fontSizeMax = 30;
            SetRef(panel, "soundButton", sound.GetComponent<Button>());
            SetRef(panel, "soundButtonText", soundLabel);
            SetRef(panel, "prestigeButton", prestige.GetComponent<Button>());
            SetRef(panel, "prestigeButtonText", prestige.GetComponentInChildren<TMP_Text>());
            SetRef(panel, "coinsText", coins);
            return panel;
        }

        private static UIPanel BuildGameplay(Transform parent)
        {
            var panel = NewPanel<GameplayScreen>("GameplayScreen", parent, UILayer.Screen);
            var root = panel.transform;

            // Thanh trên: xu + thu nhập
            var top = Box("TopBar", root, Panel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -HudTop), Vector2.zero);
            // Huy hiệu map (bấm mở Bản đồ) bên trái; xu + chỉ số bên phải.
            var zoneBadge = Button("ZoneButton", top, "", 34, NavColor, new Vector2(0.02f, 0.12f), new Vector2(0.3f, 0.88f));
            var zoneText = zoneBadge.GetComponentInChildren<TMP_Text>();
            zoneText.enableAutoSizing = true;
            zoneText.fontSizeMin = 24;
            zoneText.fontSizeMax = 38;
            var coins = Label("Coins", top, "0 xu", 80, Accent, new Vector2(0.31f, 0.42f), new Vector2(1, 1));
            var income = Label("Income", top, "", 28, Color.white, new Vector2(0.31f, 0.02f), new Vector2(0.99f, 0.46f));

            // Khung trùng WorldArea của scene (camera vẽ cảnh câu ở đây): dải cá vừa bắt, tiến trình, trạng thái.
            var frame = new GameObject("WorldFrame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(root, false);
            Stretch(frame, Vector2.zero, Vector2.one, new Vector2(0, HudBottom), new Vector2(0, -HudTop));
            var strip = Box("CatchStrip", frame, new Color(0, 0, 0, 0.35f), new Vector2(0, 0.9f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            strip.GetComponent<Image>().raycastTarget = false;
            var lastCatch = Label("LastCatch", strip, "", 40, Color.white, new Vector2(0.03f, 0f), new Vector2(0.97f, 1f));
            // Giờ/thời tiết hiện tại (dưới dải cá vừa bắt, bên trái).
            var weatherBox = Box("Weather", frame, new Color(0, 0, 0, 0.35f), new Vector2(0.02f, 0.78f), new Vector2(0.52f, 0.87f), Vector2.zero, Vector2.zero);
            weatherBox.GetComponent<Image>().raycastTarget = false;
            var weatherLabel = Label("WeatherText", weatherBox, "", 30, Color.white, new Vector2(0.03f, 0f), new Vector2(0.97f, 1f));
            weatherLabel.enableAutoSizing = true;
            weatherLabel.fontSizeMin = 16;
            weatherLabel.fontSizeMax = 30;
            var barBg = Box("RodProgress", frame, new Color(0, 0, 0, 0.35f), new Vector2(0.2f, 0.875f), new Vector2(0.8f, 0.89f), Vector2.zero, Vector2.zero);
            barBg.GetComponent<Image>().raycastTarget = false;
            var fill = Box("Fill", barBg, Accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            var statusBox = Box("RodStatus", frame, new Color(0, 0, 0, 0.4f), new Vector2(0.62f, 0.03f), new Vector2(0.97f, 0.12f), Vector2.zero, Vector2.zero);
            statusBox.GetComponent<Image>().raycastTarget = false;
            var status = Label("Status", statusBox, "", 44, Color.white, Vector2.zero, Vector2.one);
            // Nút Mồi (góc dưới trái khung cảnh): hiện thời gian còn lại khi đang dùng mồi.
            var baitBtn = Button("BaitButton", frame, "Mồi", 34, NavColor, new Vector2(0.03f, 0.03f), new Vector2(0.22f, 0.14f));
            var baitLabel = baitBtn.GetComponentInChildren<TMP_Text>();
            baitLabel.enableAutoSizing = true;
            baitLabel.fontSizeMin = 18;
            baitLabel.fontSizeMax = 34;
            // Nút Bể cá (ngay trên nút Mồi).
            var aquariumBtn = Button("AquariumButton", frame, "Bể cá", 34, NavColor, new Vector2(0.03f, 0.16f), new Vector2(0.22f, 0.27f));
            var aquariumLabel = aquariumBtn.GetComponentInChildren<TMP_Text>();
            aquariumLabel.enableAutoSizing = true;
            aquariumLabel.fontSizeMin = 18;
            aquariumLabel.fontSizeMax = 34;

            // Thanh điều hướng (cao HudBottom)
            var nav = Box("NavBar", root, Bg, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, HudBottom));
            string[] names = { "Nâng cấp", "Cửa hàng", "Bộ sưu tập", "Nhiệm vụ", "Menu" };
            var navButtons = new Button[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                float x0 = 0.015f + i * 0.1955f;
                var b = Button($"Nav{i}", nav, names[i], 32, i == names.Length - 1 ? new Color(0.3f, 0.32f, 0.38f) : NavColor,
                    new Vector2(x0, 0.12f), new Vector2(x0 + 0.18f, 0.88f));
                var label = b.GetComponentInChildren<TMP_Text>();
                label.enableAutoSizing = true;
                label.fontSizeMin = 20;
                label.fontSizeMax = 32;
                navButtons[i] = b.GetComponent<Button>();
            }

            SetRef(panel, "coinsText", coins);
            SetRef(panel, "incomeText", income);
            SetRef(panel, "lastCatchText", lastCatch);
            SetRef(panel, "statusText", status);
            SetRef(panel, "rodProgressFill", fill);
            SetRef(panel, "upgradeButton", navButtons[0]);
            SetRef(panel, "shopButton", navButtons[1]);
            SetRef(panel, "collectionButton", navButtons[2]);
            SetRef(panel, "questButton", navButtons[3]);
            SetRef(panel, "menuButton", navButtons[4]);
            SetRef(panel, "mapButton", zoneBadge.GetComponent<Button>());
            SetRef(panel, "weatherText", weatherLabel);
            SetRef(panel, "baitButton", baitBtn.GetComponent<Button>());
            SetRef(panel, "aquariumButton", aquariumBtn.GetComponent<Button>());
            SetRef(panel, "zoneText", zoneText);
            return panel;
        }

        // ---------- Popup ----------

        private static UIPanel BuildUpgradePopup(Transform parent)
        {
            var panel = NewPanel<UpgradePopup>("UpgradePopup", parent, UILayer.Popup);
            var card = PopupFrame(panel, "Nâng cấp", new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f));
            var coins = Label("Coins", card, "", 44, Color.white, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f));
            coins.rectTransform.offsetMin = new Vector2(0, -200);
            coins.rectTransform.offsetMax = new Vector2(0, -130);

            // Danh sách cuộn: số nâng cấp nhiều hơn chiều cao thẻ trên màn hình thấp.
            ScrollList(card, new Vector2(24, 24), new Vector2(-24, -220), out var list);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var template = UpgradeItem("UpgradeItem", list);
            SetRef(panel, "itemTemplate", template);
            SetRef(panel, "coinsText", coins);
            return panel;
        }

        private static UpgradeItemView UpgradeItem(string name, Transform parent)
        {
            var btn = Button(name, parent, "", 48, ButtonColor, Vector2.zero, Vector2.one);
            btn.sizeDelta = new Vector2(0, 230);
            var title = btn.GetComponentInChildren<TMP_Text>();
            title.name = "Title";
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.rectTransform.anchorMin = new Vector2(0.05f, 0.45f);
            title.rectTransform.anchorMax = new Vector2(0.65f, 1);
            var cost = Label("Cost", btn, "", 48, Accent, new Vector2(0.62f, 0.45f), new Vector2(0.95f, 1));
            cost.alignment = TextAlignmentOptions.MidlineRight;
            var effect = Label("Effect", btn, "", 30, new Color(0.75f, 0.95f, 0.85f), new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.48f));
            effect.alignment = TextAlignmentOptions.MidlineLeft;

            var view = btn.gameObject.AddComponent<UpgradeItemView>();
            SetRef(view, "button", btn.GetComponent<Button>());
            SetRef(view, "titleText", title);
            SetRef(view, "costText", cost);
            SetRef(view, "effectText", effect);
            return view;
        }

        private static UIPanel BuildShopPopup(Transform parent, FishingArtSet art)
        {
            var panel = NewPanel<ShopPopup>("ShopPopup", parent, UILayer.Popup);
            var card = PopupFrame(panel, "Cửa hàng", new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f));
            var coins = Label("Coins", card, "", 44, Color.white, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f));
            coins.rectTransform.offsetMin = new Vector2(0, -200);
            coins.rectTransform.offsetMax = new Vector2(0, -130);

            var rodsTab = Tab("RodsTab", card, "Cần câu", new Vector2(0.04f, 1f), new Vector2(0.49f, 1f));
            var skinsTab = Tab("SkinsTab", card, "Trang phục", new Vector2(0.51f, 1f), new Vector2(0.96f, 1f));

            var scroll = ScrollList(card, new Vector2(24, 24), new Vector2(-24, -340), out var content);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 18;
            vl.childControlWidth = true;
            vl.childControlHeight = false;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            var template = ShopItem(content);
            SetRef(panel, "rodsTab", rodsTab);
            SetRef(panel, "skinsTab", skinsTab);
            SetRef(panel, "itemTemplate", template);
            SetRef(panel, "scroll", scroll);
            SetRef(panel, "coinsText", coins);
            SetRef(panel, "art", art);
            return panel;
        }

        private static Button Tab(string name, Transform parent, string text, Vector2 aMin, Vector2 aMax)
        {
            var rt = Button(name, parent, text, 46, NavColor, aMin, aMax);
            rt.offsetMin = new Vector2(0, -320);
            rt.offsetMax = new Vector2(0, -215);
            var btn = rt.GetComponent<Button>();
            // Tab đang chọn = nút bị tắt: hiện màu nổi bật thay vì mờ. Màu nằm ở ColorBlock (Image để trắng).
            rt.GetComponent<Image>().color = Color.white;
            var colors = btn.colors;
            colors.normalColor = NavColor;
            colors.highlightedColor = colors.selectedColor = Color.Lerp(NavColor, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(NavColor, Color.black, 0.2f);
            colors.disabledColor = Accent;
            btn.colors = colors;
            var label = rt.GetComponentInChildren<TMP_Text>();
            label.color = Color.white;
            return btn;
        }

        private static ShopItemView ShopItem(Transform parent)
        {
            var row = Box("ShopItem", parent, Accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            row.sizeDelta = new Vector2(0, 210);
            var highlight = row.GetComponent<Image>();
            Sliced(highlight);
            var bg = Box("Bg", row, Row, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            Sliced(bg.GetComponent<Image>());

            var iconBox = Box("IconBox", bg, new Color(0, 0, 0, 0.3f), new Vector2(0.02f, 0.1f), new Vector2(0.2f, 0.9f), Vector2.zero, Vector2.zero);
            var icon = PixelImage("Icon", iconBox, null, Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.offsetMin = new Vector2(12, 12);
            icon.rectTransform.offsetMax = new Vector2(-12, -12);

            var nameText = Label("Name", bg, "", 46, Color.white, new Vector2(0.23f, 0.52f), new Vector2(0.68f, 0.95f));
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            // Tên dài (vd. "Lv5 Cần huyền thoại") thu nhỏ trên một dòng thay vì xuống dòng đè mô tả.
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 28;
            nameText.fontSizeMax = 46;
            var desc = Label("Desc", bg, "", 26, new Color(0.75f, 0.95f, 0.85f), new Vector2(0.23f, 0.06f), new Vector2(0.68f, 0.52f));
            desc.alignment = TextAlignmentOptions.TopLeft;
            var action = Button("ActionButton", bg, "Mua", 38, ButtonColor, new Vector2(0.7f, 0.15f), new Vector2(0.97f, 0.85f));

            var view = row.gameObject.AddComponent<ShopItemView>();
            SetRef(view, "icon", icon);
            SetRef(view, "nameText", nameText);
            SetRef(view, "descText", desc);
            SetRef(view, "actionButton", action.GetComponent<Button>());
            SetRef(view, "actionText", action.GetComponentInChildren<TMP_Text>());
            SetRef(view, "equippedHighlight", highlight);
            return view;
        }

        private static UIPanel BuildCollectionPopup(Transform parent, FishingArtSet art)
        {
            var panel = NewPanel<CollectionPopup>("CollectionPopup", parent, UILayer.Popup);
            var card = PopupFrame(panel, "Bộ sưu tập", new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f));
            // Hàng chọn map: < Tên map >
            var prev = Button("PrevZone", card, "<", 56, NavColor, new Vector2(0.04f, 1f), new Vector2(0.18f, 1f));
            prev.offsetMin = new Vector2(0, -250);
            prev.offsetMax = new Vector2(0, -145);
            var next = Button("NextZone", card, ">", 56, NavColor, new Vector2(0.82f, 1f), new Vector2(0.96f, 1f));
            next.offsetMin = new Vector2(0, -250);
            next.offsetMax = new Vector2(0, -145);
            var zoneText = Label("ZoneName", card, "", 52, Color.white, new Vector2(0.2f, 1f), new Vector2(0.8f, 1f));
            zoneText.rectTransform.offsetMin = new Vector2(0, -250);
            zoneText.rectTransform.offsetMax = new Vector2(0, -145);
            var progress = Label("Progress", card, "", 38, new Color(1, 1, 1, 0.85f), new Vector2(0.05f, 1f), new Vector2(0.95f, 1f));
            progress.rectTransform.offsetMin = new Vector2(0, -320);
            progress.rectTransform.offsetMax = new Vector2(0, -260);

            // Mốc thưởng bộ sưu tập của map (chữ tự co để vừa hai dòng).
            var rewards = Label("Rewards", card, "", 26, new Color(1, 1, 1, 0.85f), new Vector2(0.05f, 1f), new Vector2(0.95f, 1f));
            rewards.rectTransform.offsetMin = new Vector2(0, -410);
            rewards.rectTransform.offsetMax = new Vector2(0, -325);
            rewards.enableAutoSizing = true;
            rewards.fontSizeMin = 18;
            rewards.fontSizeMax = 28;

            var scroll = ScrollList(card, new Vector2(24, 24), new Vector2(-24, -420), out var content);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(290, 420);
            grid.spacing = new Vector2(18, 18);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;

            var item = Box("FishItem", content, Row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Sliced(item.GetComponent<Image>());
            var frame = Box("Frame", item, new Color(1, 1, 1, 0.1f), new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.95f), Vector2.zero, Vector2.zero);
            Sliced(frame.GetComponent<Image>());
            var icon = PixelImage("Icon", frame, null, Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.offsetMin = new Vector2(16, 16);
            icon.rectTransform.offsetMax = new Vector2(-16, -16);
            var nameText = Label("Name", item, "???", 36, Color.white, new Vector2(0.04f, 0.38f), new Vector2(0.96f, 0.5f));
            var info = Label("Info", item, "", 24, new Color(1, 1, 1, 0.75f), new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.38f));
            info.enableAutoSizing = true;
            info.fontSizeMin = 16;
            info.fontSizeMax = 24;

            var view = item.gameObject.AddComponent<CollectionItemView>();
            SetRef(view, "icon", icon);
            SetRef(view, "frame", frame.GetComponent<Image>());
            SetRef(view, "nameText", nameText);
            SetRef(view, "infoText", info);

            SetRef(panel, "itemTemplate", view);
            SetRef(panel, "zoneText", zoneText);
            SetRef(panel, "progressText", progress);
            SetRef(panel, "rewardText", rewards);
            SetRef(panel, "prevButton", prev.GetComponent<Button>());
            SetRef(panel, "nextButton", next.GetComponent<Button>());
            SetRef(panel, "scroll", scroll);
            SetRef(panel, "art", art);
            return panel;
        }

        private static UIPanel BuildMapPopup(Transform parent)
        {
            var panel = NewPanel<MapPopup>("MapPopup", parent, UILayer.Popup);
            var card = PopupFrame(panel, "Bản đồ", new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            var scroll = ScrollList(card, new Vector2(24, 24), new Vector2(-24, -150), out var content);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 18;
            vl.childControlWidth = true;
            vl.childControlHeight = false;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            var row = Box("ZoneItem", content, Accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            row.sizeDelta = new Vector2(0, 380);
            var highlight = row.GetComponent<Image>();
            Sliced(highlight);
            var bg = Box("Bg", row, Row, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            Sliced(bg.GetComponent<Image>());

            // Ô màu nước của map (thay cho ảnh minh họa).
            var preview = Box("Preview", bg, Color.white, new Vector2(0.02f, 0.08f), new Vector2(0.15f, 0.92f), Vector2.zero, Vector2.zero);
            Sliced(preview.GetComponent<Image>());

            var nameText = Label("Name", bg, "", 46, Accent, new Vector2(0.18f, 0.8f), new Vector2(0.7f, 0.97f));
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            var info = Label("Info", bg, "", 28, new Color(0.85f, 0.95f, 1f), new Vector2(0.18f, 0.5f), new Vector2(0.7f, 0.8f));
            info.alignment = TextAlignmentOptions.TopLeft;
            var reqs = Label("Requirements", bg, "", 26, Color.white, new Vector2(0.18f, 0.03f), new Vector2(0.97f, 0.5f));
            reqs.alignment = TextAlignmentOptions.TopLeft;
            var action = Button("ActionButton", bg, "Câu ở đây", 36, ButtonColor, new Vector2(0.72f, 0.55f), new Vector2(0.97f, 0.92f));

            var lockOverlay = Box("Lock", bg, new Color(0, 0, 0, 0.35f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            lockOverlay.GetComponent<Image>().raycastTarget = false;
            Sliced(lockOverlay.GetComponent<Image>());

            var view = row.gameObject.AddComponent<ZoneItemView>();
            SetRef(view, "preview", preview.GetComponent<Image>());
            SetRef(view, "nameText", nameText);
            SetRef(view, "infoText", info);
            SetRef(view, "requirementText", reqs);
            SetRef(view, "actionButton", action.GetComponent<Button>());
            SetRef(view, "actionText", action.GetComponentInChildren<TMP_Text>());
            SetRef(view, "currentHighlight", highlight);
            SetRef(view, "lockOverlay", lockOverlay.gameObject);

            SetRef(panel, "itemTemplate", view);
            SetRef(panel, "scroll", scroll);
            return panel;
        }

        private static UIPanel BuildZoneUnlockPopup(Transform parent)
        {
            // Hiếm khi hiện (mỗi map một lần): không giữ cache.
            var panel = NewPanel<ZoneUnlockPopup>("ZoneUnlockPopup", parent, UILayer.Popup, keepCached: false);
            var card = PopupFrame(panel, "Mở khóa bản đồ!", new Vector2(0.06f, 0.3f), new Vector2(0.94f, 0.7f), dimCloses: false);
            var zoneName = Label("ZoneName", card, "", 80, Color.white, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.8f));
            var body = Label("Body", card, "", 36, new Color(0.9f, 0.95f, 1f), new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.62f));
            var go = Button("GoButton", card, "Đi câu ngay!", 50, ButtonColor, new Vector2(0.22f, 0.05f), new Vector2(0.78f, 0.22f));
            SetRef(panel, "zoneNameText", zoneName);
            SetRef(panel, "bodyText", body);
            SetRef(panel, "goButton", go.GetComponent<Button>());
            SetRef(panel, "goButtonText", go.GetComponentInChildren<TMP_Text>());
            return panel;
        }

        private static UIPanel BuildPrestigePopup(Transform parent)
        {
            var panel = NewPanel<PrestigePopup>("PrestigePopup", parent, UILayer.Popup);
            var card = PopupFrame(panel, "Tái sinh", new Vector2(0.05f, 0.24f), new Vector2(0.95f, 0.76f));
            var body = Label("Body", card, "", 36, new Color(0.9f, 0.95f, 1f), new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.86f));
            body.alignment = TextAlignmentOptions.TopLeft;
            var shop = Button("ShopButton", card, "Cửa hàng ngọc trai", 44, NavColor, new Vector2(0.14f, 0.2f), new Vector2(0.86f, 0.33f));
            var go = Button("PrestigeButton", card, "Tái sinh", 46, new Color(0.25f, 0.45f, 0.62f), new Vector2(0.14f, 0.04f), new Vector2(0.86f, 0.17f));
            SetRef(panel, "shopButton", shop.GetComponent<Button>());
            SetRef(panel, "bodyText", body);
            SetRef(panel, "prestigeButton", go.GetComponent<Button>());
            SetRef(panel, "prestigeButtonText", go.GetComponentInChildren<TMP_Text>());
            return panel;
        }

        /// <summary>Ô Bể cá: nút phủ kín cha với ảnh cá, tên, thông tin; dạng dòng (row) có thêm chữ hành động bên phải.</summary>
        private static AquariumEntryView AquariumEntry(string name, Transform parent, bool row)
        {
            var btn = Button(name, parent, "", 30, Row, Vector2.zero, Vector2.one);
            var action = btn.GetComponentInChildren<TMP_Text>();
            action.name = "Action";
            var icon = PixelImage("Icon", btn, null, Color.white);
            icon.preserveAspect = true;
            TMP_Text title, info;
            if (row)
            {
                btn.sizeDelta = new Vector2(0, 130);
                Stretch(icon.rectTransform, new Vector2(0.02f, 0.08f), new Vector2(0.2f, 0.92f), Vector2.zero, Vector2.zero);
                title = Label("Title", btn, "", 32, Color.white, new Vector2(0.22f, 0.5f), new Vector2(0.72f, 0.96f));
                info = Label("Info", btn, "", 26, new Color(1, 1, 1, 0.8f), new Vector2(0.22f, 0.04f), new Vector2(0.72f, 0.5f));
                title.alignment = info.alignment = TextAlignmentOptions.MidlineLeft;
                Stretch(action.rectTransform, new Vector2(0.72f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero);
                action.color = Accent;
                action.fontSize = 36;
            }
            else
            {
                btn.sizeDelta = new Vector2(172, 230);
                Stretch(icon.rectTransform, new Vector2(0.1f, 0.52f), new Vector2(0.9f, 0.97f), Vector2.zero, Vector2.zero);
                title = Label("Title", btn, "", 22, Color.white, new Vector2(0.03f, 0.27f), new Vector2(0.97f, 0.52f));
                info = Label("Info", btn, "", 20, new Color(1, 1, 1, 0.8f), new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.29f));
                action.gameObject.SetActive(false);
            }
            foreach (var t in new[] { title, info })
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = 14;
                t.fontSizeMax = t.fontSize;
            }

            var view = btn.gameObject.AddComponent<AquariumEntryView>();
            SetRef(view, "button", btn.GetComponent<Button>());
            SetRef(view, "icon", icon);
            SetRef(view, "titleText", title);
            SetRef(view, "infoText", info);
            if (row) SetRef(view, "actionText", action);
            return view;
        }

        /// <summary>Biển thông báo lớn giữa màn hình (lớp Top), chỉ là một dải nên không chặn phần còn lại của màn hình.</summary>
        private static UIPanel BuildBannerPopup(Transform parent)
        {
            var panel = NewPanel<BannerPopup>("BannerPopup", parent, UILayer.Top, closeOnBack: false);
            Stretch(panel.GetComponent<RectTransform>(), new Vector2(0f, 0.56f), new Vector2(1f, 0.72f), Vector2.zero, Vector2.zero);
            var visual = new GameObject("Visual", typeof(RectTransform)).GetComponent<RectTransform>();
            visual.SetParent(panel.transform, false);
            Stretch(visual, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bg = Box("Band", visual, new Color(0.03f, 0.06f, 0.12f, 0.88f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bg.GetComponent<Image>().raycastTarget = false;
            Box("TopLine", visual, Accent, new Vector2(0, 0.96f), new Vector2(1, 1), Vector2.zero, Vector2.zero).GetComponent<Image>().raycastTarget = false;
            Box("BottomLine", visual, Accent, new Vector2(0, 0), new Vector2(1, 0.04f), Vector2.zero, Vector2.zero).GetComponent<Image>().raycastTarget = false;
            var title = Label("Title", visual, "", 84, Accent, new Vector2(0.04f, 0.5f), new Vector2(0.96f, 0.95f));
            title.enableAutoSizing = true;
            title.fontSizeMin = 40;
            title.fontSizeMax = 84;
            var subtitle = Label("Subtitle", visual, "", 50, Color.white, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.5f));
            subtitle.enableAutoSizing = true;
            subtitle.fontSizeMin = 28;
            subtitle.fontSizeMax = 50;
            SetRef(panel, "titleText", title);
            SetRef(panel, "subtitleText", subtitle);
            SetRef(panel, "visual", visual);
            SetPanelChrome(panel, null);
            return panel;
        }

        private static UIPanel BuildAquariumPopup(Transform parent, FishingArtSet art)
        {
            var panel = NewPanel<AquariumPopup>("AquariumPopup", parent, UILayer.Popup);
            var card = PopupFrame(panel, "Bể cá", new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f));
            var summary = Label("Summary", card, "", 36, Color.white, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f));
            summary.rectTransform.offsetMin = new Vector2(0, -215);
            summary.rectTransform.offsetMax = new Vector2(0, -135);
            summary.enableAutoSizing = true;
            summary.fontSizeMin = 22;
            summary.fontSizeMax = 36;

            // Lưới ô trong bể (5 cột).
            var tank = new GameObject("Tank", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<RectTransform>();
            tank.SetParent(card, false);
            tank.anchorMin = new Vector2(0, 1);
            tank.anchorMax = new Vector2(1, 1);
            tank.offsetMin = new Vector2(24, -950);
            tank.offsetMax = new Vector2(-24, -225);
            var grid = tank.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(172, 230);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperCenter;
            var cell = AquariumEntry("Cell", tank, row: false);

            var header = Label("CandidatesHeader", card, "", 34, Accent, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f));
            header.rectTransform.offsetMin = new Vector2(0, -1020);
            header.rectTransform.offsetMax = new Vector2(0, -960);
            header.enableAutoSizing = true;
            header.fontSizeMin = 20;
            header.fontSizeMax = 34;

            ScrollList(card, new Vector2(24, 24), new Vector2(-24, -1030), out var list);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var row = AquariumEntry("Row", list, row: true);

            SetRef(panel, "cellTemplate", cell);
            SetRef(panel, "rowTemplate", row);
            SetRef(panel, "summaryText", summary);
            SetRef(panel, "candidatesHeader", header);
            SetRef(panel, "art", art);
            return panel;
        }

        private static UIPanel BuildBaitPopup(Transform parent)
        {
            var panel = NewPanel<BaitPopup>("BaitPopup", parent, UILayer.Popup);
            var card = PopupFrame(panel, "Mồi câu", new Vector2(0.04f, 0.2f), new Vector2(0.96f, 0.8f));
            var coins = Label("Coins", card, "", 38, Color.white, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f));
            coins.rectTransform.offsetMin = new Vector2(0, -200);
            coins.rectTransform.offsetMax = new Vector2(0, -130);
            var active = Label("Active", card, "", 34, new Color(1, 1, 1, 0.9f), new Vector2(0.05f, 1f), new Vector2(0.95f, 1f));
            active.rectTransform.offsetMin = new Vector2(0, -275);
            active.rectTransform.offsetMax = new Vector2(0, -205);
            active.enableAutoSizing = true;
            active.fontSizeMin = 20;
            active.fontSizeMax = 34;

            ScrollList(card, new Vector2(24, 24), new Vector2(-24, -290), out var list);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var row = Box("BaitRow", list, Row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Sliced(row.GetComponent<Image>());
            row.sizeDelta = new Vector2(0, 230);
            var title = Label("Title", row, "", 38, Color.white, new Vector2(0.04f, 0.55f), new Vector2(0.6f, 0.97f));
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.enableAutoSizing = true;
            title.fontSizeMin = 22;
            title.fontSizeMax = 38;
            var effect = Label("Effect", row, "", 28, new Color(0.75f, 0.95f, 0.85f), new Vector2(0.04f, 0.06f), new Vector2(0.6f, 0.52f));
            effect.alignment = TextAlignmentOptions.MidlineLeft;
            effect.enableAutoSizing = true;
            effect.fontSizeMin = 18;
            effect.fontSizeMax = 28;
            var buy = Button("BuyButton", row, "Mua", 34, ButtonColor, new Vector2(0.62f, 0.52f), new Vector2(0.97f, 0.94f));
            var buyText = buy.GetComponentInChildren<TMP_Text>();
            buyText.enableAutoSizing = true;
            buyText.fontSizeMin = 18;
            buyText.fontSizeMax = 34;
            var use = Button("UseButton", row, "Dùng", 34, new Color(0.25f, 0.45f, 0.62f), new Vector2(0.62f, 0.06f), new Vector2(0.97f, 0.48f));
            var useText = use.GetComponentInChildren<TMP_Text>();

            var view = row.gameObject.AddComponent<BaitItemView>();
            SetRef(view, "titleText", title);
            SetRef(view, "effectText", effect);
            SetRef(view, "buyButton", buy.GetComponent<Button>());
            SetRef(view, "buyText", buyText);
            SetRef(view, "useButton", use.GetComponent<Button>());
            SetRef(view, "useText", useText);

            SetRef(panel, "itemTemplate", view);
            SetRef(panel, "coinsText", coins);
            SetRef(panel, "activeText", active);
            return panel;
        }

        private static UIPanel BuildQuestPopup(Transform parent)
        {
            var panel = NewPanel<QuestPopup>("QuestPopup", parent, UILayer.Popup);
            var card = PopupFrame(panel, "Nhiệm vụ", new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f));

            ScrollList(card, new Vector2(24, 24), new Vector2(-24, -140), out var list);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TMP_Text Header(string name)
            {
                var h = Label(name, list, "", 42, Accent, Vector2.zero, Vector2.one);
                h.alignment = TextAlignmentOptions.MidlineLeft;
                h.rectTransform.sizeDelta = new Vector2(0, 80);
                return h;
            }
            var dailyHeader = Header("DailyHeader");

            // Mẫu một dòng nhiệm vụ.
            var row = Box("QuestRow", list, Row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Sliced(row.GetComponent<Image>());
            row.sizeDelta = new Vector2(0, 200);
            var title = Label("Title", row, "", 38, Color.white, new Vector2(0.04f, 0.56f), new Vector2(0.66f, 0.97f));
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.enableAutoSizing = true;
            title.fontSizeMin = 24;
            title.fontSizeMax = 38;
            var reward = Label("Reward", row, "", 28, new Color(0.75f, 0.95f, 0.85f), new Vector2(0.04f, 0.33f), new Vector2(0.66f, 0.58f));
            reward.alignment = TextAlignmentOptions.MidlineLeft;
            var barBg = Box("Bar", row, new Color(0, 0, 0, 0.45f), new Vector2(0.04f, 0.08f), new Vector2(0.66f, 0.3f), Vector2.zero, Vector2.zero);
            var fill = Box("Fill", barBg, Accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            var progress = Label("Progress", barBg, "", 24, Color.white, Vector2.zero, Vector2.one);
            var claim = Button("ClaimButton", row, "Nhận", 36, ButtonColor, new Vector2(0.7f, 0.15f), new Vector2(0.97f, 0.85f));
            var claimText = claim.GetComponentInChildren<TMP_Text>();
            claimText.enableAutoSizing = true;
            claimText.fontSizeMin = 20;
            claimText.fontSizeMax = 36;

            var view = row.gameObject.AddComponent<QuestItemView>();
            SetRef(view, "titleText", title);
            SetRef(view, "rewardText", reward);
            SetRef(view, "progressText", progress);
            SetRef(view, "progressFill", fill);
            SetRef(view, "claimButton", claim.GetComponent<Button>());
            SetRef(view, "claimButtonText", claimText);

            var repeatingHeader = Header("RepeatingHeader");
            SetRef(panel, "itemTemplate", view);
            SetRef(panel, "dailyHeader", dailyHeader);
            SetRef(panel, "repeatingHeader", repeatingHeader);
            return panel;
        }

        /// <summary>Thông báo ngắn (lớp Top): panel chỉ là khung nhỏ phía trên để không chặn chạm phần còn lại của màn hình.</summary>
        private static UIPanel BuildToastPopup(Transform parent)
        {
            var panel = NewPanel<ToastPopup>("ToastPopup", parent, UILayer.Top, closeOnBack: false);
            Stretch(panel.GetComponent<RectTransform>(), new Vector2(0.06f, 0.8f), new Vector2(0.94f, 0.92f), Vector2.zero, Vector2.zero);
            var border = Box("Card", panel.transform, CardBorder, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Sliced(border.GetComponent<Image>());
            border.GetComponent<Image>().raycastTarget = false;
            var inner = Box("Inner", border, new Color(Panel.r, Panel.g, Panel.b, 1f), Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            Sliced(inner.GetComponent<Image>());
            inner.GetComponent<Image>().raycastTarget = false;
            var msg = Label("Message", inner, "", 36, Color.white, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f));
            msg.enableAutoSizing = true;
            msg.fontSizeMin = 20;
            msg.fontSizeMax = 38;
            SetRef(panel, "messageText", msg);
            SetPanelChrome(panel, border);
            return panel;
        }

        private static UIPanel BuildPearlShopPopup(Transform parent)
        {
            var panel = NewPanel<PearlShopPopup>("PearlShopPopup", parent, UILayer.Popup);
            var card = PopupFrame(panel, "Cửa hàng ngọc trai", new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f));
            var pearls = Label("Pearls", card, "", 44, Color.white, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f));
            pearls.rectTransform.offsetMin = new Vector2(0, -200);
            pearls.rectTransform.offsetMax = new Vector2(0, -130);

            ScrollList(card, new Vector2(24, 24), new Vector2(-24, -220), out var list);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Mẫu dòng giống nâng cấp xu nhưng gắn PearlUpgradeItemView.
            var btn = Button("PearlItem", list, "", 48, new Color(0.25f, 0.45f, 0.62f), Vector2.zero, Vector2.one);
            btn.sizeDelta = new Vector2(0, 230);
            var title = btn.GetComponentInChildren<TMP_Text>();
            title.name = "Title";
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.rectTransform.anchorMin = new Vector2(0.05f, 0.45f);
            title.rectTransform.anchorMax = new Vector2(0.62f, 1);
            var cost = Label("Cost", btn, "", 44, Accent, new Vector2(0.55f, 0.45f), new Vector2(0.95f, 1));
            cost.alignment = TextAlignmentOptions.MidlineRight;
            var effect = Label("Effect", btn, "", 30, new Color(0.75f, 0.95f, 0.85f), new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.48f));
            effect.alignment = TextAlignmentOptions.MidlineLeft;
            var view = btn.gameObject.AddComponent<PearlUpgradeItemView>();
            SetRef(view, "button", btn.GetComponent<Button>());
            SetRef(view, "titleText", title);
            SetRef(view, "costText", cost);
            SetRef(view, "effectText", effect);

            SetRef(panel, "itemTemplate", view);
            SetRef(panel, "pearlsText", pearls);
            return panel;
        }

        private static UIPanel BuildAfkPopup(Transform parent)
        {
            // Thường chỉ hiện một lần mỗi phiên: không giữ cache, đóng là Destroy.
            var panel = NewPanel<AfkEarnPopup>("AfkEarnPopup", parent, UILayer.Popup, keepCached: false);
            var dim = Box("Dim", panel.transform, Dim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            var border = Box("Card", panel.transform, CardBorder, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.68f), Vector2.zero, Vector2.zero);
            Sliced(border.GetComponent<Image>());
            var card = Box("Inner", border, new Color(Panel.r, Panel.g, Panel.b, 1f), Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
            Sliced(card.GetComponent<Image>());
            Label("Title", card, "Khi bạn vắng mặt", 64, Accent, new Vector2(0, 0.78f), new Vector2(1, 0.96f));
            var body = Label("Body", card, "", 44, Color.white, new Vector2(0.06f, 0.3f), new Vector2(0.94f, 0.78f));
            var claim = Button("ClaimButton", card, "Nhận", 56, ButtonColor, new Vector2(0.3f, 0.06f), new Vector2(0.7f, 0.24f));
            SetRef(panel, "bodyText", body);
            SetPanelChrome(panel, border, claim.GetComponent<Button>());
            return panel;
        }

        /// <summary>
        /// Popup minigame cá hiếm (lớp Top), phủ toàn màn hình và chặn chạm xuống UI phía sau.
        /// Mỗi lớp là một Image riêng (đổi sprite/màu/kích thước trong prefab). Sprite pixel art phóng 7×.
        /// </summary>
        private static UIPanel BuildRareFishPopup(Transform parent, FishingArtSet art)
        {
            const float px = 7f; // canvas unit / pixel art (số nguyên để pixel đều)
            var panel = NewPanel<RareFishPopup>("RareFishPopup", parent, UILayer.Top, closeOnBack: false);
            var blocker = panel.transform;
            var blockerImg = panel.gameObject.AddComponent<Image>();
            blockerImg.color = new Color(0, 0, 0, 0.72f);
            var input = panel.gameObject.AddComponent<RareMinigameInputArea>();

            var border = Box("CardBorder", blocker, new Color(0.45f, 0.16f, 0.2f, 1f), new Vector2(0.04f, 0.17f), new Vector2(0.96f, 0.85f), Vector2.zero, Vector2.zero);
            border.GetComponent<Image>().raycastTarget = false;
            var card = Box("Card", border, new Color(0.06f, 0.09f, 0.15f, 1f), Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
            card.GetComponent<Image>().raycastTarget = false;
            var title = Label("Title", card, "Cá hiếm cắn câu!", 54, Accent, new Vector2(0.04f, 0.875f), new Vector2(0.96f, 0.995f));

            var dial = new GameObject("Dial", typeof(RectTransform)).GetComponent<RectTransform>();
            dial.SetParent(card, false);
            dial.anchorMin = dial.anchorMax = new Vector2(0.5f, 0.575f);
            dial.sizeDelta = new Vector2(96 * px, 96 * px);

            var background = PixelImage("RadialBackground", dial, art != null ? art.ringBase : null, Color.white);
            var track = PixelImage("RadialTrack", dial, art != null ? art.ringTrack : null, new Color(1f, 1f, 1f, 0.12f));
            var target = PixelImage("TargetZone", dial, art != null ? art.ringTrack : null, new Color(0.36f, 0.95f, 0.45f, 1f));
            var fishIcon = PixelImage("FishIcon", dial, art != null ? art.fishLarge : null, Color.white, new Vector2(22 * px, 11 * px));
            var needle = PixelImage("RotatingNeedle", dial, art != null ? art.needle : null, Color.white, new Vector2(7 * px, 44 * px));
            needle.rectTransform.pivot = new Vector2(0.5f, 2f / 44f);
            PixelImage("Hub", dial, art != null ? art.hub : null, Color.white, new Vector2(11 * px, 11 * px));
            var feedback = PixelImage("FeedbackIcon", dial, art != null ? art.check : null, Color.white, new Vector2(16 * px, 16 * px));
            feedback.rectTransform.anchorMin = feedback.rectTransform.anchorMax = new Vector2(0.86f, 0.86f);

            var progress = PipRow("ProgressIndicator", "Trúng", card, new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.31f));
            var attempts = PipRow("AttemptIndicator", "Lượt", card, new Vector2(0.06f, 0.19f), new Vector2(0.94f, 0.25f));
            var hint = Label("Hint", card, "", 34, new Color(1f, 1f, 1f, 0.8f), new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.88f));

            // Nút "Giật cần": nhận thao tác khi nhấn xuống, có hiệu ứng nhấn của Button nhưng không dùng onClick.
            var hookBtn = Button("HookButton", card, "GIẬT CẦN!", 64, new Color(0.85f, 0.32f, 0.22f), new Vector2(0.12f, 0.03f), new Vector2(0.88f, 0.165f));
            var hookArea = hookBtn.gameObject.AddComponent<RareMinigameInputArea>();

            // Màn hình kết quả (THÀNH CÔNG / THẤT BẠI + nút OK).
            var resultBorder = Box("ResultPanel", blocker, new Color(0.45f, 0.16f, 0.2f, 1f), new Vector2(0.1f, 0.33f), new Vector2(0.9f, 0.67f), Vector2.zero, Vector2.zero);
            var resultCard = Box("ResultCard", resultBorder, new Color(0.04f, 0.07f, 0.12f, 1f), Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
            resultCard.GetComponent<Image>().raycastTarget = false;
            var resultTitle = Label("ResultTitle", resultCard, "THÀNH CÔNG!", 72, Color.white, new Vector2(0.04f, 0.74f), new Vector2(0.96f, 0.96f));
            var resultFish = PixelImage("ResultFishIcon", resultCard, art != null ? art.fishLarge : null, Color.white, new Vector2(22 * 9f, 11 * 9f));
            resultFish.rectTransform.anchorMin = resultFish.rectTransform.anchorMax = new Vector2(0.5f, 0.56f);
            var resultMessage = Label("ResultMessage", resultCard, "", 44, Color.white, new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.42f));
            var ok = Button("OkButton", resultCard, "OK", 56, ButtonColor, new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.23f));

            SetRef(panel, "art", art);
            SetRef(panel, "inputArea", input);
            SetRef(panel, "hookButtonArea", hookArea);
            SetRef(panel, "hookButton", hookBtn.GetComponent<Button>());
            SetRef(panel, "hookButtonLabel", hookBtn.GetComponentInChildren<TMP_Text>());
            SetRef(panel, "titleText", title);
            SetRef(panel, "hintText", hint);
            SetRef(panel, "radialBackground", background);
            SetRef(panel, "radialTrack", track);
            SetRef(panel, "targetTemplate", target);
            SetRef(panel, "rotatingNeedle", needle.rectTransform);
            SetRef(panel, "needleImage", needle);
            SetRef(panel, "fishIcon", fishIcon);
            SetRef(panel, "feedbackIcon", feedback);
            SetRef(panel, "progressIndicator", progress);
            SetRef(panel, "attemptIndicator", attempts);
            SetRef(panel, "resultPanel", resultBorder.gameObject);
            SetRef(panel, "resultTitle", resultTitle);
            SetRef(panel, "resultFishIcon", resultFish);
            SetRef(panel, "resultMessage", resultMessage);
            SetRef(panel, "okButton", ok.GetComponent<Button>());
            var so = new SerializedObject(panel);
            so.FindProperty("pipSize").floatValue = 9 * 5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetPanelChrome(panel, border);
            resultBorder.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>Popup minigame cá boss (lớp Top): cá boss lớn ở trên, thanh ngang với vùng xanh và con lăn ở phía dưới.</summary>
        private static UIPanel BuildBossFishPopup(Transform parent, FishingArtSet art)
        {
            var panel = NewPanel<BossFishPopup>("BossFishPopup", parent, UILayer.Top, closeOnBack: false);
            var blocker = panel.transform;
            panel.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.78f);
            var input = panel.gameObject.AddComponent<RareMinigameInputArea>();

            var border = Box("CardBorder", blocker, new Color(0.6f, 0.22f, 0.12f, 1f), new Vector2(0.04f, 0.17f), new Vector2(0.96f, 0.85f), Vector2.zero, Vector2.zero);
            border.GetComponent<Image>().raycastTarget = false;
            var card = Box("Card", border, new Color(0.08f, 0.07f, 0.12f, 1f), Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
            card.GetComponent<Image>().raycastTarget = false;
            var title = Label("Title", card, "BOSS!", 56, Accent, new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.995f));

            var bossIcon = PixelImage("BossIcon", card, art != null ? art.fishLarge : null, new Color(1f, 0.55f, 0.45f), new Vector2(22 * 16f, 11 * 16f));
            bossIcon.rectTransform.anchorMin = bossIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.66f);
            var hint = Label("Hint", card, "", 34, new Color(1f, 1f, 1f, 0.85f), new Vector2(0.04f, 0.46f), new Vector2(0.96f, 0.52f));

            // Thanh ngang: nền, vùng xanh (anchor theo %), con lăn (anchor theo %).
            var bar = Box("BarTrack", card, new Color(1f, 1f, 1f, 0.14f), new Vector2(0.06f, 0.35f), new Vector2(0.94f, 0.43f), Vector2.zero, Vector2.zero);
            Sliced(bar.GetComponent<Image>());
            bar.GetComponent<Image>().raycastTarget = false;
            var zone = Box("TargetZone", bar, new Color(0.36f, 0.95f, 0.45f, 0.9f), new Vector2(0.4f, 0f), new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero);
            zone.GetComponent<Image>().raycastTarget = false;
            var marker = new GameObject("Marker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            marker.SetParent(bar, false);
            marker.anchorMin = new Vector2(0.5f, -0.3f);
            marker.anchorMax = new Vector2(0.5f, 1.3f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(44f, 0f);
            marker.anchoredPosition = Vector2.zero;
            var markerImg = marker.GetComponent<Image>();
            markerImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            markerImg.type = Image.Type.Sliced;
            markerImg.color = Color.white;
            markerImg.raycastTarget = false;
            var feedback = Label("Feedback", card, "", 56, Color.white, new Vector2(0.2f, 0.285f), new Vector2(0.8f, 0.35f));

            var progress = PipRow("ProgressIndicator", "Trúng", card, new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.28f));
            var attempts = PipRow("AttemptIndicator", "Lượt", card, new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.22f));

            var hookBtn = Button("HookButton", card, "GIẬT CẦN!", 64, new Color(0.85f, 0.32f, 0.22f), new Vector2(0.12f, 0.02f), new Vector2(0.88f, 0.145f));
            var hookArea = hookBtn.gameObject.AddComponent<RareMinigameInputArea>();

            var resultBorder = Box("ResultPanel", blocker, new Color(0.6f, 0.22f, 0.12f, 1f), new Vector2(0.1f, 0.33f), new Vector2(0.9f, 0.67f), Vector2.zero, Vector2.zero);
            var resultCard = Box("ResultCard", resultBorder, new Color(0.04f, 0.07f, 0.12f, 1f), Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
            resultCard.GetComponent<Image>().raycastTarget = false;
            var resultTitle = Label("ResultTitle", resultCard, "", 64, Color.white, new Vector2(0.04f, 0.74f), new Vector2(0.96f, 0.96f));
            var resultFish = PixelImage("ResultFishIcon", resultCard, art != null ? art.fishLarge : null, Color.white, new Vector2(22 * 9f, 11 * 9f));
            resultFish.rectTransform.anchorMin = resultFish.rectTransform.anchorMax = new Vector2(0.5f, 0.56f);
            var resultMessage = Label("ResultMessage", resultCard, "", 40, Color.white, new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.42f));
            var ok = Button("OkButton", resultCard, "OK", 56, ButtonColor, new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.23f));

            SetRef(panel, "art", art);
            SetRef(panel, "inputArea", input);
            SetRef(panel, "hookButtonArea", hookArea);
            SetRef(panel, "hookButton", hookBtn.GetComponent<Button>());
            SetRef(panel, "hookButtonLabel", hookBtn.GetComponentInChildren<TMP_Text>());
            SetRef(panel, "titleText", title);
            SetRef(panel, "hintText", hint);
            SetRef(panel, "bossIcon", bossIcon);
            SetRef(panel, "barTrack", bar);
            SetRef(panel, "targetZone", zone.GetComponent<Image>());
            SetRef(panel, "marker", marker);
            SetRef(panel, "markerImage", markerImg);
            SetRef(panel, "feedbackText", feedback);
            SetRef(panel, "progressIndicator", progress);
            SetRef(panel, "attemptIndicator", attempts);
            SetRef(panel, "resultPanel", resultBorder.gameObject);
            SetRef(panel, "resultTitle", resultTitle);
            SetRef(panel, "resultFishIcon", resultFish);
            SetRef(panel, "resultMessage", resultMessage);
            SetRef(panel, "okButton", ok.GetComponent<Button>());
            var so = new SerializedObject(panel);
            so.FindProperty("pipSize").floatValue = 9 * 5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetPanelChrome(panel, border);
            resultBorder.gameObject.SetActive(false);
            return panel;
        }

        // ---------- Tiện ích UI ----------

        /// <summary>ScrollRect dọc (Viewport có RectMask2D, Content tự giãn theo nội dung).</summary>
        private static ScrollRect ScrollList(Transform parent, Vector2 offMin, Vector2 offMax, out RectTransform content)
        {
            var scrollRt = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect)).GetComponent<RectTransform>();
            scrollRt.SetParent(parent, false);
            Stretch(scrollRt, Vector2.zero, Vector2.one, offMin, offMax);
            scrollRt.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f); // nhận kéo cuộn ở khe trống

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(scrollRt, false);
            Stretch(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            content = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollRt.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        private static void Sliced(Image img)
        {
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
