using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>
    /// Bản đồ câu cá: mọi map theo thứ tự mở khóa. Map mở khi hoàn thành bộ sưu tập map trước,
    /// câu được khi đủ cần câu + nâng cấp; mỗi dòng ghi rõ điều kiện còn thiếu để người chơi biết mục tiêu tiếp theo.
    /// </summary>
    public class MapPopup : UIPanel
    {
        [SerializeField] private ZoneItemView itemTemplate;
        [SerializeField] private ScrollRect scroll;

        private const string Met = "#7CF29A";
        private const string Unmet = "#FF8A70";

        private readonly List<ZoneItemView> _items = new List<ZoneItemView>();

        protected override void OnInit()
        {
            itemTemplate.gameObject.SetActive(false);
            for (int i = 0; i < Game.Zones.Count; i++)
            {
                var item = Instantiate(itemTemplate, itemTemplate.transform.parent);
                item.name = $"Zone{i}";
                item.gameObject.SetActive(Game.Zones[i] != null);
                _items.Add(item);
            }
        }

        protected override void OnOpen(object args)
        {
            Game.StateChanged += Refresh;
            Refresh();
            ScrollToCurrent();
        }

        protected override void OnClose() => Game.StateChanged -= Refresh;

        private void Refresh()
        {
            var gm = Game;
            var sb = new StringBuilder();
            for (int i = 0; i < _items.Count; i++)
            {
                var zone = gm.Zones[i];
                if (zone == null) continue;
                int index = i;
                bool unlocked = gm.IsZoneUnlocked(i);
                bool canFish = gm.CanFishZone(i);
                bool current = gm.CurrentZoneIndex == i;
                var (caught, total) = gm.ZoneCollection(i);

                string difficulty = $"Độ khó {zone.DifficultyLevel}/6";
                string info = unlocked
                    ? $"{zone.description}\n<color=#FFD24A>{difficulty}</color>  ·  Bộ sưu tập {caught}/{total}"
                    : $"<color=#FFD24A>{difficulty}</color>  ·  {total} loài cá chờ khám phá";

                // Chỉ liệt kê điều kiện khi chưa câu được (map đang/đã câu được không cần nhắc).
                sb.Clear();
                if (!canFish)
                    foreach (var (label, met) in gm.ZoneRequirements(i))
                        sb.Append($"<color={(met ? Met : Unmet)}>{(met ? "• Đạt:" : "• Cần:")} {label}</color>\n");
                string reqs = sb.ToString().TrimEnd('\n');

                string action = current ? "Đang câu" : canFish ? "Câu ở đây" : unlocked ? "Chưa đủ\nđiều kiện" : "Khóa";
                string title = unlocked ? $"{i + 1}. {zone.displayName}" : $"{i + 1}. {zone.displayName} <size=70%>(khóa)</size>";
                _items[i].Set(zone.themeColor, title, info, reqs, action, canFish && !current && !gm.IsRareMinigameActive,
                    current, !unlocked, () =>
                    {
                        if (Game.TrySetZone(index)) Close();
                    });
            }
        }

        /// <summary>Cuộn tới map đang câu (danh sách dài hơn khung).</summary>
        private void ScrollToCurrent()
        {
            if (scroll == null || _items.Count <= 1) return;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(Game.CurrentZoneIndex / (float)(_items.Count - 1));
        }
    }
}
