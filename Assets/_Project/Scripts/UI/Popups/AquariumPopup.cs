using System.Collections.Generic;
using LazyAngler.Data;
using LazyAngler.Systems;
using LazyAngler.Utils;
using LazyAngler.Visual;
using TMPro;
using UnityEngine;

namespace LazyAngler.UI
{
    /// <summary>
    /// Bể cá trưng bày: đặt cá hiếm (hoặc cá biến thể) đã bắt vào bể để nhận bonus giá cá vĩnh viễn.
    /// Phần trên là các ô trong bể (bấm để lấy ra, ô cuối để mở thêm ô bằng ngọc trai), phần dưới là cá có thể thêm.
    /// </summary>
    public class AquariumPopup : UIPanel
    {
        private static readonly Color VariantColor = new Color(1f, 0.62f, 0.94f);

        [Tooltip("Mẫu một ô trong bể (ẩn); nhân bản cho từng ô.")]
        [SerializeField] private AquariumEntryView cellTemplate;
        [Tooltip("Mẫu một dòng gợi ý cá có thể thêm (ẩn).")]
        [SerializeField] private AquariumEntryView rowTemplate;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text candidatesHeader;
        [Tooltip("Ảnh cá mặc định cho loài không có icon riêng.")]
        [SerializeField] private FishingArtSet art;

        private readonly List<AquariumEntryView> _cells = new List<AquariumEntryView>();
        private readonly List<AquariumEntryView> _rows = new List<AquariumEntryView>();

        protected override void OnInit()
        {
            cellTemplate.gameObject.SetActive(false);
            rowTemplate.gameObject.SetActive(false);
        }

        protected override void OnOpen(object args)
        {
            Game.StateChanged += Refresh;
            Refresh();
        }

        protected override void OnClose() => Game.StateChanged -= Refresh;

        private void Refresh()
        {
            var gm = Game;
            int capacity = gm.AquariumCapacity;
            var tank = gm.Aquarium;
            summaryText.text = $"Trong bể: {tank.Count}/{capacity} ô · Giá cá <color=#7CF29A>+{gm.AquariumBonus * 100:0.#}%</color>";

            // Ô trong bể + một ô "mở thêm" nếu còn.
            bool canExpand = gm.NextAquariumSlotCost >= 0;
            int cellCount = capacity + (canExpand ? 1 : 0);
            Ensure(_cells, cellTemplate, cellCount, "Cell");
            for (int i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                cell.gameObject.SetActive(i < cellCount);
                if (i >= cellCount) continue;
                if (i < capacity) ShowSlot(gm, cell, i, i < tank.Count ? tank[i] : null);
                else ShowExpand(gm, cell);
            }

            var candidates = gm.AquariumCandidates();
            candidatesHeader.text = candidates.Count > 0
                ? "Có thể thêm vào bể"
                : "Chưa có cá nào để thêm. Hãy bắt cá hiếm hoặc cá biến thể!";
            Ensure(_rows, rowTemplate, candidates.Count, "Row");
            bool full = tank.Count >= capacity;
            for (int i = 0; i < _rows.Count; i++)
            {
                bool on = i < candidates.Count;
                _rows[i].gameObject.SetActive(on);
                if (!on) continue;
                var c = candidates[i];
                _rows[i].Show(IconOf(c.fish), ColorOf(c.fish, c.variant),
                    Name(c.fish, c.variant),
                    $"{RarityText.Name(c.fish.rarity)} · giá cá <color=#7CF29A>+{c.bonus * 100:0.#}%</color>",
                    full ? "Bể đầy" : "Thêm", !full, () => Game.TryAddToAquarium(c.fish, c.variant));
            }
        }

        private void ShowSlot(GameManager gm, AquariumEntryView cell, int index, AquariumEntry entry)
        {
            var fish = entry != null ? gm.FindFish(entry.fishId) : null;
            if (fish == null)
            {
                cell.Show(null, Color.white, "<color=#8A93A6>Trống</color>", "", "", false, null);
                return;
            }
            cell.Show(IconOf(fish), ColorOf(fish, entry.variant), Name(fish, entry.variant),
                $"+{gm.AquariumEntryBonus(fish, entry.variant) * 100:0.#}%\n<size=75%><color=#8A93A6>bấm để lấy ra</color></size>",
                "", true, () => Game.TryRemoveFromAquarium(index));
        }

        private static void ShowExpand(GameManager gm, AquariumEntryView cell)
        {
            int cost = gm.NextAquariumSlotCost;
            cell.Show(null, Color.white, "Mở thêm ô", $"{cost} ngọc trai\n<size=75%><color=#8A93A6>(đang có {gm.PrestigePearls})</color></size>",
                "", gm.CanBuyAquariumSlot, () => GameManager.Instance.TryBuyAquariumSlot());
        }

        private Sprite IconOf(FishData fish)
            => fish.icon != null ? fish.icon : art != null ? (fish.rarity >= Rarity.Rare ? art.fishLarge : art.fishSmall) : null;

        private static Color ColorOf(FishData fish, bool variant)
            => variant ? VariantColor : fish.icon != null ? Color.white : FishSwimmer.ColorFor(fish.rarity);

        private static string Name(FishData fish, bool variant)
            => variant ? $"{fish.displayName}\n<size=70%><color=#FF9BF0>Biến thể</color></size>" : fish.displayName;

        private static void Ensure(List<AquariumEntryView> list, AquariumEntryView template, int count, string prefix)
        {
            while (list.Count < count)
            {
                var v = Instantiate(template, template.transform.parent);
                v.name = $"{prefix}{list.Count}";
                list.Add(v);
            }
        }
    }
}
