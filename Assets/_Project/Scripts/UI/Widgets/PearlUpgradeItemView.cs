using LazyAngler.Data;
using LazyAngler.Systems;
using LazyAngler.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>Một dòng nâng cấp ngọc trai: tên + cấp/tối đa, hiệu quả (hiện tại → sau khi nâng), nút mua kèm giá ngọc trai.</summary>
    public class PearlUpgradeItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text effectText;

        private PearlUpgradeData _def;

        public void Bind(PearlUpgradeData def)
        {
            _def = def;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => GameManager.Instance.TryBuyPearlUpgrade(_def));
        }

        public void Refresh(GameManager gm)
        {
            int level = gm.GetPearlLevel(_def);
            bool maxed = gm.IsPearlMaxed(_def);
            titleText.text = $"{_def.displayName} <size=75%>{level}/{_def.MaxLevel}</size>";
            costText.text = maxed ? "Tối đa" : $"{Fmt.Number(gm.GetPearlCost(_def))} ngọc";
            button.interactable = gm.CanBuyPearlUpgrade(_def);
            effectText.text = Effect(level, maxed);
        }

        private string Effect(int level, bool maxed)
        {
            float v = _def.valuePerLevel;
            string Show(float now, float next, string fmt, string unit)
                => maxed ? $"{now.ToString(fmt)}{unit}" : $"{now.ToString(fmt)}→{next.ToString(fmt)}{unit}";

            switch (_def.type)
            {
                case PearlUpgradeType.FishValue:
                    return $"Giá cá {Show(v * level * 100f, v * (level + 1) * 100f, "+0.#", "%")}";
                case PearlUpgradeType.RareLuck:
                    return $"Tỉ lệ cá hiếm {Show(v * level * 100f, v * (level + 1) * 100f, "+0.#", "%")}";
                case PearlUpgradeType.BiteSpeed:
                    return $"Cá cắn nhanh hơn {Show(v * level * 100f, v * (level + 1) * 100f, "+0.#", "%")}";
                case PearlUpgradeType.OfflineEfficiency:
                    return $"Thu nhập offline {Show((0.5f + v * level) * 100f, (0.5f + v * (level + 1)) * 100f, "0.#", "%")}";
                case PearlUpgradeType.StartingCoins:
                    return $"Xu khởi đầu sau tái sinh {Show(v * level, v * (level + 1), "0", " xu")}";
                default:
                    return _def.description;
            }
        }
    }
}
