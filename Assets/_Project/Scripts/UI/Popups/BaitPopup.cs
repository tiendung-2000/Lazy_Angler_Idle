using System.Collections.Generic;
using LazyAngler.Data;
using TMPro;
using UnityEngine;

namespace LazyAngler.UI
{
    /// <summary>
    /// Mồi tiêu hao của map đang câu: mua bằng xu, dùng để tăng tỉ lệ cá hiếm hoặc cá biến thể trong một khoảng thời gian.
    /// Mỗi lúc chỉ một mồi có hiệu lực; mồi của map khác tạm dừng cho tới khi bạn quay lại map đó.
    /// </summary>
    public class BaitPopup : UIPanel
    {
        [Tooltip("Mẫu một dòng (ẩn); được nhân bản khi cần.")]
        [SerializeField] private BaitItemView itemTemplate;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text activeText;

        private readonly List<BaitItemView> _rows = new List<BaitItemView>();
        private readonly List<BaitData> _shown = new List<BaitData>();
        private float _nextClock;

        protected override void OnInit() => itemTemplate.gameObject.SetActive(false);

        protected override void OnOpen(object args)
        {
            Game.StateChanged += Refresh;
            Refresh();
        }

        protected override void OnClose() => Game.StateChanged -= Refresh;

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime < _nextClock) return;
            _nextClock = Time.unscaledTime + 1f;
            UpdateActive();
        }

        private void Refresh()
        {
            var gm = Game;
            coinsText.text = $"{Utils.Fmt.Number(gm.Coins)} xu · Mồi của {(gm.CurrentZone != null ? gm.CurrentZone.displayName : "map này")}";

            _shown.Clear();
            foreach (var b in gm.Baits)
                if (b != null && b.zoneIndex == gm.CurrentZoneIndex) _shown.Add(b);

            while (_rows.Count < _shown.Count)
            {
                var row = Instantiate(itemTemplate, itemTemplate.transform.parent);
                row.name = $"BaitRow{_rows.Count}";
                _rows.Add(row);
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                bool on = i < _shown.Count;
                _rows[i].gameObject.SetActive(on);
                if (on) _rows[i].Set(gm, _shown[i]);
            }
            UpdateActive();
        }

        private void UpdateActive()
        {
            var gm = Game;
            var bait = gm.ActiveBait;
            if (bait == null)
            {
                activeText.text = "Chưa dùng mồi nào";
                return;
            }
            int s = Mathf.CeilToInt(gm.ActiveBaitRemaining);
            string time = $"{s / 60}:{s % 60:00}";
            activeText.text = gm.IsBaitInEffect
                ? $"Đang dùng: <color=#7CF29A>{bait.displayName}</color> · còn {time}"
                : $"<color=#FFD24A>{bait.displayName}</color> · còn {time} <size=75%>(tạm dừng, chỉ chạy ở map {gm.Zones[bait.zoneIndex].displayName})</size>";
        }
    }
}
