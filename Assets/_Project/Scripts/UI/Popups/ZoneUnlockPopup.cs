using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>
    /// Popup "Mở khóa bản đồ mới" (args: chỉ số map) khi hoàn thành bộ sưu tập map trước.
    /// Đủ điều kiện thì đi câu ngay; chưa đủ thì nhắc cần nâng cấp gì (mục tiêu tiếp theo của vòng lặp).
    /// </summary>
    public class ZoneUnlockPopup : UIPanel
    {
        [SerializeField] private TMP_Text zoneNameText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button goButton;
        [SerializeField] private TMP_Text goButtonText;

        private int _zone;

        protected override void OnInit() => goButton.onClick.AddListener(OnGo);

        protected override void OnOpen(object args)
        {
            // GameUIFlow chỉ mở với chỉ số hợp lệ; kẹp lại để không lỗi nếu bị gọi sai.
            _zone = Mathf.Clamp(args is int z ? z : Game.CurrentZoneIndex + 1, 0, Game.Zones.Count - 1);
            Game.StateChanged += Refresh;
            Refresh();
        }

        protected override void OnClose() => Game.StateChanged -= Refresh;

        private void Refresh()
        {
            var gm = Game;
            var zone = gm.Zones[_zone];
            var (_, total) = gm.ZoneCollection(_zone);
            zoneNameText.text = zone.displayName;

            var sb = new StringBuilder();
            sb.Append($"{zone.description}\n<color=#FFD24A>{total} loài cá mới</color> · Độ khó {zone.DifficultyLevel}/6\n");
            bool canFish = gm.CanFishZone(_zone);
            if (!canFish)
            {
                sb.Append("\nCần chuẩn bị:\n");
                foreach (var (label, met) in gm.ZoneRequirements(_zone))
                    if (!met) sb.Append($"<color=#FF8A70>• {label}</color>\n");
            }
            bodyText.text = sb.ToString().TrimEnd('\n');
            goButtonText.text = canFish ? "Đi câu ngay!" : "Xem bản đồ";
        }

        private void OnGo()
        {
            if (Game.CanFishZone(_zone) && Game.TrySetZone(_zone))
            {
                Close();
                return;
            }
            Close();
            Manager.ShowOnly<MapPopup>();
        }
    }
}
