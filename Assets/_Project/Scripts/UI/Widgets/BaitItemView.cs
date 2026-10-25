using System.Text;
using LazyAngler.Data;
using LazyAngler.Systems;
using LazyAngler.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>Một dòng mồi tiêu hao: tên + số đang có, hiệu quả và thời gian, nút Mua (kèm giá) và nút Dùng.</summary>
    public class BaitItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyText;
        [SerializeField] private Button useButton;
        [SerializeField] private TMP_Text useText;

        private BaitData _bait;

        private void Awake()
        {
            buyButton.onClick.AddListener(() => GameManager.Instance.TryBuyBait(_bait));
            useButton.onClick.AddListener(() => GameManager.Instance.TryUseBait(_bait));
        }

        public void Set(GameManager gm, BaitData bait)
        {
            _bait = bait;
            titleText.text = $"{bait.displayName}  <size=70%><color=#8A93A6>đang có: {gm.GetBaitCount(bait)}</color></size>";

            var sb = new StringBuilder();
            if (bait.rareBonus > 0f) sb.Append($"Cá hiếm +{bait.rareBonus * 100f:0.#}%  ");
            if (bait.variantBonus > 0f) sb.Append($"Cá biến thể +{bait.variantBonus * 100f:0.#}%  ");
            sb.Append($"· {Mathf.RoundToInt(bait.durationSeconds / 60f * 10f) / 10f:0.#} phút");
            effectText.text = sb.ToString();

            buyText.text = $"Mua {Fmt.Number(bait.price)}";
            buyButton.interactable = gm.CanBuyBait(bait);
            bool active = gm.ActiveBait == bait;
            useText.text = active ? "Đang dùng" : "Dùng";
            useButton.interactable = gm.CanUseBait(bait);
        }
    }
}
