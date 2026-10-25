using System.Collections.Generic;
using LazyAngler.Data;
using LazyAngler.Utils;
using TMPro;
using UnityEngine;

namespace LazyAngler.UI
{
    /// <summary>
    /// Popup Nâng cấp: Giá trị cá, Thuyền (thêm cần), các chỉ số câu (Tỉ lệ cá cắn, May mắn, Mồi câu) và Két offline.
    /// Chỉ số có trần (cắn, cá hiếm, tốc độ cắn) tự báo "Tối đa" khi chạm trần.
    /// Cần câu mới mua trong Cửa hàng.
    /// </summary>
    public class UpgradePopup : UIPanel
    {
        [SerializeField] private UpgradeType[] upgrades =
            { UpgradeType.FishValue, UpgradeType.Boat, UpgradeType.BiteChance, UpgradeType.Luck, UpgradeType.Bait, UpgradeType.Vault };
        [Tooltip("Mẫu một dòng (ẩn); được nhân bản cho từng nâng cấp.")]
        [SerializeField] private UpgradeItemView itemTemplate;
        [SerializeField] private TMP_Text coinsText;

        private readonly List<UpgradeItemView> _items = new List<UpgradeItemView>();

        protected override void OnInit()
        {
            itemTemplate.gameObject.SetActive(false);
            foreach (var type in upgrades)
            {
                var item = Instantiate(itemTemplate, itemTemplate.transform.parent);
                item.name = $"Upgrade_{type}";
                item.gameObject.SetActive(true);
                item.Bind(type);
                _items.Add(item);
            }
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
            if (coinsText != null) coinsText.text = $"{Fmt.Number(gm.Coins)} xu";
            foreach (var item in _items) item.Refresh(gm);
        }
    }
}
