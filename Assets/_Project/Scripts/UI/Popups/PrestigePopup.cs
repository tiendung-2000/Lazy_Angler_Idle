using System.Text;
using LazyAngler.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>
    /// Popup "Tái sinh": đổi xu đã kiếm lượt này lấy ngọc trai (bonus giá cá vĩnh viễn) rồi làm lại từ đầu.
    /// Bấm hai lần để xác nhận vì tái sinh xóa xu và nâng cấp.
    /// </summary>
    public class PrestigePopup : UIPanel
    {
        private const float ConfirmSeconds = 4f;

        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button prestigeButton;
        [SerializeField] private TMP_Text prestigeButtonText;
        [SerializeField] private Button shopButton;

        private float _armedUntil;

        protected override void OnInit()
        {
            prestigeButton.onClick.AddListener(OnPrestige);
            if (shopButton != null) shopButton.onClick.AddListener(() => Manager.Show<PearlShopPopup>());
        }

        protected override void OnOpen(object args)
        {
            _armedUntil = 0f;
            Game.StateChanged += Refresh;
            Refresh();
        }

        protected override void OnClose() => Game.StateChanged -= Refresh;

        private void Update()
        {
            // Hết thời gian xác nhận thì nút trở lại bình thường.
            if (_armedUntil > 0f && Time.unscaledTime > _armedUntil)
            {
                _armedUntil = 0f;
                Refresh();
            }
        }

        private void Refresh()
        {
            var gm = Game;
            int pending = gm.PendingPearls;

            var sb = new StringBuilder();
            sb.Append($"Ngọc trai đang có: <color=#7FE3FF>{gm.PrestigePearls}</color> (tiêu ở Cửa hàng ngọc trai)\n");
            sb.Append($"Đã tái sinh: {gm.PrestigeCount} lần\n\n");
            sb.Append($"Xu kiếm được lượt này: {Fmt.Number(gm.RunEarned)}\n");
            if (pending >= 1)
                sb.Append($"Tái sinh ngay nhận <color=#7FE3FF>+{pending} ngọc trai</color>\n");
            sb.Append($"Kiếm thêm {Fmt.Number(gm.CoinsToNextPearl)} xu để được thêm 1 ngọc trai\n\n");
            sb.Append("<color=#FF8A70>Mất:</color> xu, nâng cấp, cần câu, thuyền, két, về lại map đầu\n");
            sb.Append("<color=#8CE08C>Giữ:</color> ngọc trai, bộ sưu tập cá, trang phục");
            bodyText.text = sb.ToString();

            prestigeButton.interactable = gm.CanPrestige;
            prestigeButtonText.text = !gm.CanPrestige ? "Chưa đủ xu"
                : _armedUntil > 0f ? "Bấm lần nữa để xác nhận!"
                : $"Tái sinh (+{pending} ngọc trai)";
        }

        private void OnPrestige()
        {
            if (!Game.CanPrestige) return;
            if (_armedUntil <= 0f)
            {
                _armedUntil = Time.unscaledTime + ConfirmSeconds;
                Refresh();
                return;
            }
            Game.TryPrestige();
            Close();
        }
    }
}
