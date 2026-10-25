using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LazyAngler.UI
{
    /// <summary>
    /// Cửa hàng ngọc trai: tiêu ngọc trai (nhận khi tái sinh) mua nâng cấp vĩnh viễn.
    /// Danh sách lấy từ GameConfig.pearlUpgrades; mỗi nâng cấp có cấp tối đa riêng.
    /// </summary>
    public class PearlShopPopup : UIPanel
    {
        [Tooltip("Mẫu một dòng (ẩn); được nhân bản cho từng nâng cấp ngọc trai.")]
        [SerializeField] private PearlUpgradeItemView itemTemplate;
        [SerializeField] private TMP_Text pearlsText;

        private readonly List<PearlUpgradeItemView> _items = new List<PearlUpgradeItemView>();

        protected override void OnInit()
        {
            itemTemplate.gameObject.SetActive(false);
            foreach (var def in Game.PearlUpgrades)
            {
                if (def == null) continue;
                var item = Instantiate(itemTemplate, itemTemplate.transform.parent);
                item.name = $"Pearl_{def.upgradeId}";
                item.gameObject.SetActive(true);
                item.Bind(def);
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
            pearlsText.text = $"Ngọc trai: <color=#7FE3FF>{Game.PrestigePearls}</color>";
            foreach (var item in _items) item.Refresh(Game);
        }
    }
}
