using System.Collections.Generic;
using LazyAngler.Core;
using LazyAngler.Data;
using LazyAngler.Systems;
using LazyAngler.Utils;
using LazyAngler.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>
    /// Bộ sưu tập cá theo từng bản đồ (nút trước/sau để đổi map, mở ở map đang câu).
    /// Loài chưa bắt hiện "???"; map chưa mở khóa vẫn xem được số loài để người chơi có mục tiêu.
    /// Ô dùng chung (pool) giữa các map.
    /// </summary>
    public class CollectionPopup : UIPanel
    {
        [SerializeField] private CollectionItemView itemTemplate;
        [SerializeField] private TMP_Text zoneText;
        [SerializeField] private TMP_Text progressText;
        [Tooltip("Các mốc thưởng bộ sưu tập của map đang xem.")]
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private ScrollRect scroll;
        [Tooltip("Ảnh cá mặc định cho loài không có icon riêng.")]
        [SerializeField] private FishingArtSet art;

        private readonly List<CollectionItemView> _items = new List<CollectionItemView>();
        private readonly List<FishData> _fish = new List<FishData>();
        private int _zone;

        protected override void OnInit()
        {
            itemTemplate.gameObject.SetActive(false);
            prevButton.onClick.AddListener(() => SelectZone(_zone - 1));
            nextButton.onClick.AddListener(() => SelectZone(_zone + 1));
        }

        /// <summary>args: chỉ số map để mở thẳng (mặc định map đang câu).</summary>
        protected override void OnOpen(object args)
        {
            Game.StateChanged += Refresh;
            SelectZone(args is int z ? z : Game.CurrentZoneIndex);
        }

        protected override void OnClose() => Game.StateChanged -= Refresh;

        private void SelectZone(int zone)
        {
            int count = Mathf.Max(1, Game.Zones.Count);
            _zone = Mathf.Clamp(zone, 0, count - 1);
            prevButton.interactable = _zone > 0;
            nextButton.interactable = _zone < count - 1;

            _fish.Clear();
            foreach (var f in Game.Config.fish)
                if (f != null && f.zoneIndex == _zone) _fish.Add(f);
            // Cá thường trước, cá sự kiện (chỉ có theo giờ/thời tiết) xếp cuối.
            static int Group(FishData f) => f.isBoss ? 2 : f.IsEventFish ? 1 : 0;
            _fish.Sort((a, b) => Group(a) != Group(b) ? Group(a).CompareTo(Group(b))
                : a.rarity != b.rarity ? a.rarity.CompareTo(b.rarity) : a.basePrice.CompareTo(b.basePrice));

            while (_items.Count < _fish.Count)
            {
                var view = Instantiate(itemTemplate, itemTemplate.transform.parent);
                view.name = $"FishItem{_items.Count}";
                _items.Add(view);
            }
            for (int i = 0; i < _items.Count; i++) _items[i].gameObject.SetActive(i < _fish.Count);
            Refresh();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void Refresh()
        {
            var gm = Game;
            var totals = gm.CaughtTotals;
            int found = 0;
            for (int i = 0; i < _fish.Count; i++)
            {
                var fish = _fish[i];
                totals.TryGetValue(fish.fishId, out int count);
                bool discovered = count > 0;
                if (discovered && !fish.IsEventFish && !fish.isBoss) found++;
                var sprite = fish.icon != null ? fish.icon
                    : art != null ? (fish.rarity >= Rarity.Rare ? art.fishLarge : art.fishSmall) : null;
                // Ảnh riêng giữ màu gốc; ảnh mặc định (xám) tô theo độ hiếm.
                var color = fish.icon != null ? Color.white : FishSwimmer.ColorFor(fish.rarity);
                var record = gm.GetRecord(fish.fishId);
                string info;
                if (discovered)
                {
                    string recordLine = record != null && record.maxSize > 0f
                        ? $"Kỷ lục: <color=#7FE3FF>{record.maxSize:0.#} cm</color>"
                        : "";
                    string variantLine = record != null && record.variants > 0
                        ? $"<color=#FF9BF0>Biến thể ×{record.variants}</color>"
                        : "<color=#8A93A6>Chưa có biến thể</color>";
                    info = $"<color=#{ColorUtility.ToHtmlStringRGB(FishSwimmer.ColorFor(fish.rarity))}>{RarityText.Name(fish.rarity)}</color> · {Fmt.Number(fish.basePrice)} xu · ×{Fmt.Number(count)}\n{recordLine}\n{variantLine}";
                }
                else
                {
                    info = fish.isBoss
                        ? $"<color=#FF9A55>BOSS</color>\nRất hiếm, cần minigame\nChưa bắt"
                        : fish.IsEventFish
                            ? $"{RarityText.Name(fish.rarity)}\n<color=#FFD24A>Chỉ có {WeatherUtil.Name(fish.requiredCondition)}</color>\nChưa bắt"
                            : $"{RarityText.Name(fish.rarity)}\nChưa bắt";
                }
                if (fish.isBoss && discovered) info += "\n<color=#FF9A55>BOSS</color>";
                if (fish.IsEventFish && discovered) info += $"\n<color=#FFD24A>Cá sự kiện ({WeatherUtil.Name(fish.requiredCondition)})</color>";
                _items[i].Set(sprite, color, fish.displayName, info, discovered);
            }

            var zone = _zone < gm.Zones.Count ? gm.Zones[_zone] : null;
            bool unlocked = gm.IsZoneUnlocked(_zone);
            zoneText.text = zone == null ? "" : unlocked ? zone.displayName : $"{zone.displayName} <size=70%>(khóa)</size>";
            if (progressText != null)
            {
                var (_, regularTotal) = gm.ZoneCollection(_zone);
                var (eCaught, eTotal) = gm.EventCollection(_zone);
                bool complete = found >= regularTotal && regularTotal > 0;
                var (vHave, vTotal) = gm.VariantCollection(_zone);
                progressText.text = (complete
                    ? $"<color=#7CF29A>Hoàn thành {found}/{regularTotal}</color>"
                    : $"Đã khám phá {found}/{regularTotal}")
                    + $" · <color=#FF9BF0>Biến thể {vHave}/{vTotal}</color>"
                    + (eTotal > 0 ? $" · <color=#FFD24A>Sự kiện {eCaught}/{eTotal}</color>" : "")
                    + (gm.BossOf(_zone) != null ? $" · <color=#FF9A55>Boss {(gm.IsBossCaught(_zone) ? "1" : "0")}/1</color>" : "");
            }
            if (rewardText != null) rewardText.text = RewardSummary(gm, _zone);
        }

        /// <summary>Các mốc thưởng của map: đã nhận hay còn bao nhiêu ngọc trai.</summary>
        private static string RewardSummary(GameManager gm, int zone)
        {
            var sb = new System.Text.StringBuilder("Thưởng bộ: ");
            for (int i = 0; i < LootTable.RarityCount; i++)
            {
                var rarity = (Rarity)i;
                var (caught, total) = gm.RarityCollection(zone, rarity);
                if (total == 0) continue;
                sb.Append(Reward(RarityText.Name(rarity), gm.IsRewardClaimed(GameManager.RarityRewardKey(zone, rarity)),
                    gm.RarityRewardPearls(zone, rarity), $"{caught}/{total}")).Append("  ");
            }
            var (have, vTotal) = gm.VariantCollection(zone);
            sb.Append(Reward("Biến thể", gm.IsRewardClaimed(GameManager.VariantRewardKey(zone)), gm.VariantRewardPearls(zone), $"{have}/{vTotal}"));
            var (eCaught, eTotal) = gm.EventCollection(zone);
            if (eTotal > 0)
                sb.Append("  ").Append(Reward("Sự kiện", gm.IsRewardClaimed(GameManager.EventRewardKey(zone)), gm.EventRewardPearls(zone), $"{eCaught}/{eTotal}"));
            if (gm.BossOf(zone) != null)
                sb.Append("  ").Append(Reward("Boss", gm.IsRewardClaimed(GameManager.BossRewardKey(zone)), gm.BossFirstCatchPearls(zone),
                    gm.IsBossCaught(zone) ? "1/1" : "0/1"));
            return sb.ToString();
        }

        private static string Reward(string label, bool claimed, int pearls, string progress)
            => claimed ? $"<color=#7CF29A>{label} xong</color>" : $"{label} {progress} <color=#7FE3FF>(+{pearls} ngọc)</color>";
    }
}
