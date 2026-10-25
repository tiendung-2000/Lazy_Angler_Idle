using System.Collections.Generic;
using LazyAngler.Systems;
using TMPro;
using UnityEngine;

namespace LazyAngler.UI
{
    /// <summary>
    /// Nhiệm vụ: phần trên là nhiệm vụ ngày (làm mới lúc nửa đêm giờ máy), phần dưới là nhiệm vụ tuần hoàn
    /// (nhận thưởng xong thì lặp lại với chỉ tiêu cao hơn). Hàng dùng chung (pool) giữa hai phần.
    /// </summary>
    public class QuestPopup : UIPanel
    {
        [Tooltip("Mẫu một dòng (ẩn); được nhân bản khi cần.")]
        [SerializeField] private QuestItemView itemTemplate;
        [SerializeField] private TMP_Text dailyHeader;
        [SerializeField] private TMP_Text repeatingHeader;

        private readonly List<QuestItemView> _rows = new List<QuestItemView>();
        private float _nextClock;

        protected override void OnInit() => itemTemplate.gameObject.SetActive(false);

        protected override void OnOpen(object args)
        {
            Game.StateChanged += Refresh;
            Refresh();
        }

        protected override void OnClose() => Game.StateChanged -= Refresh;

        // Đồng hồ đếm ngược làm mới nhiệm vụ ngày.
        private void Update()
        {
            if (!IsOpen || Time.unscaledTime < _nextClock) return;
            _nextClock = Time.unscaledTime + 1f;
            UpdateHeader();
        }

        private void Refresh()
        {
            var gm = Game;
            var daily = gm.Quests.Daily;
            var repeating = gm.Quests.Repeating;
            int need = daily.Count + repeating.Count;
            while (_rows.Count < need)
            {
                var row = Instantiate(itemTemplate, itemTemplate.transform.parent);
                row.name = $"QuestRow{_rows.Count}";
                _rows.Add(row);
            }

            // Thứ tự trong danh sách: tiêu đề ngày, các nhiệm vụ ngày, tiêu đề tuần hoàn, các nhiệm vụ tuần hoàn.
            int sibling = 0, r = 0;
            dailyHeader.transform.SetSiblingIndex(sibling++);
            foreach (var p in daily) Place(gm, _rows[r++], p, false, ref sibling);
            repeatingHeader.transform.SetSiblingIndex(sibling++);
            foreach (var p in repeating) Place(gm, _rows[r++], p, true, ref sibling);
            for (; r < _rows.Count; r++) _rows[r].gameObject.SetActive(false);
            UpdateHeader();
        }

        private static void Place(GameManager gm, QuestItemView row, QuestProgress p, bool repeating, ref int sibling)
        {
            row.gameObject.SetActive(true);
            row.transform.SetSiblingIndex(sibling++);
            row.Set(gm, p, repeating);
        }

        private void UpdateHeader()
        {
            int s = (int)Game.SecondsToQuestReset;
            dailyHeader.text = $"Nhiệm vụ ngày <size=70%><color=#8A93A6>(làm mới sau {s / 3600:00}:{s / 60 % 60:00}:{s % 60:00})</color></size>";
            repeatingHeader.text = "Nhiệm vụ tuần hoàn <size=70%><color=#8A93A6>(xong là lặp lại, khó hơn)</color></size>";
        }
    }
}
