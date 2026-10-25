using System.Collections.Generic;
using LazyAngler.Data;
using LazyAngler.Utils;
using LazyAngler.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>
    /// Cửa hàng 2 tab: Cần câu (mua lần lượt theo cấp, cần mới thay cần cũ) và Trang phục (mua rồi mặc).
    /// Dùng chung một danh sách ShopItemView (pool) cho cả hai tab.
    /// </summary>
    public class ShopPopup : UIPanel
    {
        public enum Tab { Rods, Skins }

        [SerializeField] private Button rodsTab;
        [SerializeField] private Button skinsTab;
        [Tooltip("Mẫu một ô (ẩn); được nhân bản theo số món.")]
        [SerializeField] private ShopItemView itemTemplate;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private TMP_Text coinsText;
        [Tooltip("Ảnh ngư dân cho ô skin không có icon riêng.")]
        [SerializeField] private FishingArtSet art;

        private readonly List<ShopItemView> _items = new List<ShopItemView>();
        private Tab _tab;

        protected override void OnInit()
        {
            itemTemplate.gameObject.SetActive(false);
            rodsTab.onClick.AddListener(() => SelectTab(Tab.Rods));
            skinsTab.onClick.AddListener(() => SelectTab(Tab.Skins));
        }

        /// <summary>args: Tab để mở thẳng một tab (mặc định Cần câu).</summary>
        protected override void OnOpen(object args)
        {
            Game.StateChanged += Refresh;
            SelectTab(args is Tab t ? t : Tab.Rods);
        }

        protected override void OnClose() => Game.StateChanged -= Refresh;

        private void SelectTab(Tab tab)
        {
            _tab = tab;
            // Tab đang chọn không bấm được (màu "disabled" của nút là màu được chọn).
            rodsTab.interactable = tab != Tab.Rods;
            skinsTab.interactable = tab != Tab.Skins;
            Refresh();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void Refresh()
        {
            var gm = Game;
            if (coinsText != null) coinsText.text = $"{Fmt.Number(gm.Coins)} xu";
            if (_tab == Tab.Rods) RefreshRods();
            else RefreshSkins();
        }

        private void RefreshRods()
        {
            var gm = Game;
            var tiers = gm.Config.rodTiers;
            int current = gm.RodTierIndex;
            EnsureItems(tiers.Count);
            for (int i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];
                var item = _items[i];
                if (tier == null)
                {
                    item.gameObject.SetActive(false);
                    continue;
                }
                int index = i;
                string desc = $"Kéo {gm.Stats.BaseFishingDuration / tier.fishingSpeedMultiplier:0.#}s · " +
                              $"Cắn {tier.biteChance * 100:0}% · Hụt {tier.missChance * 100:0}%";
                string zones = ZonesNeedingTier(i);
                if (zones != null) desc += $"\n<color=#FFD24A>Mở map: {zones}</color>";
                item.SetIcon(tier.tipSprite != null ? tier.tipSprite : tier.buttSprite, Color.white);

                if (i == current) item.Set($"Lv{i + 1} {tier.displayName}", desc, "Đang dùng", false, true, null);
                else if (i < current) item.Set($"Lv{i + 1} {tier.displayName}", desc, "Đã có", false, false, null);
                else if (i == current + 1)
                    item.Set($"Lv{i + 1} {tier.displayName}", desc, $"Mua\n{Fmt.Number(tier.unlockCost)} xu",
                        gm.CanBuyRod(index), false, () => Game.TryBuyRod(index));
                else item.Set($"Lv{i + 1} {tier.displayName}", desc, "Khóa", false, false, null);
            }
        }

        private void RefreshSkins()
        {
            var gm = Game;
            var skins = gm.Config.skins;
            EnsureItems(skins.Count);
            for (int i = 0; i < skins.Count; i++)
            {
                var skin = skins[i];
                var item = _items[i];
                if (skin == null)
                {
                    item.gameObject.SetActive(false);
                    continue;
                }
                var sprite = skin.icon != null ? skin.icon
                    : art != null && art.fishermanFrames != null && art.fishermanFrames.Length > 0 ? art.fishermanFrames[0] : null;
                item.SetIcon(sprite, skin.icon != null ? Color.white : skin.bodyTint);

                if (gm.IsSkinEquipped(skin)) item.Set(skin.displayName, skin.description, "Đang dùng", false, true, null);
                else if (gm.IsSkinOwned(skin)) item.Set(skin.displayName, skin.description, "Mặc", true, false, () => Game.EquipSkin(skin));
                else
                    item.Set(skin.displayName, skin.description, $"Mua\n{Fmt.Number(skin.price)} xu",
                        gm.CanBuySkin(skin), false, () => Game.TryBuySkin(skin));
            }
        }

        /// <summary>Tên các map yêu cầu đúng cấp cần này (null nếu không có), để người chơi biết mua cần để làm gì.</summary>
        private string ZonesNeedingTier(int tier)
        {
            string names = null;
            foreach (var z in Game.Zones)
                if (z != null && z.requiredRodTier == tier && tier > 0)
                    names = names == null ? z.displayName : $"{names}, {z.displayName}";
            return names;
        }

        /// <summary>Đủ số ô cần hiển thị; ô thừa ẩn đi để tái sử dụng khi đổi tab.</summary>
        private void EnsureItems(int count)
        {
            while (_items.Count < count)
            {
                var item = Instantiate(itemTemplate, itemTemplate.transform.parent);
                item.name = $"ShopItem{_items.Count}";
                _items.Add(item);
            }
            for (int i = 0; i < _items.Count; i++) _items[i].gameObject.SetActive(i < count);
        }
    }
}
