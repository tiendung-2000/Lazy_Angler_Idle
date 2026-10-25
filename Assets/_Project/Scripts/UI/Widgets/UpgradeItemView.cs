using LazyAngler.Data;
using LazyAngler.Systems;
using LazyAngler.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>Một dòng nâng cấp: tên + cấp, mô tả hiệu ứng, nút mua kèm chi phí.</summary>
    public class UpgradeItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text costText;
        [Tooltip("Dòng hiệu quả: chỉ số hiện tại → sau khi nâng.")]
        [SerializeField] private TMP_Text effectText;

        private UpgradeType _type;

        public void Bind(UpgradeType type)
        {
            _type = type;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (GameManager.Instance.TryUpgrade(_type)) Sfx.Play(SfxId.Coin, 0.8f);
            });
        }

        public void Refresh(GameManager gm)
        {
            var def = gm.GetUpgrade(_type);
            gameObject.SetActive(def != null);
            if (def == null) return;

            titleText.text = $"{def.displayName} <size=75%>{gm.GetLevelLabel(_type)}</size>";
            bool maxed = gm.IsUpgradeMaxed(_type);
            costText.text = maxed ? "Tối đa" : $"{Fmt.Number(gm.GetUpgradeCost(_type))} xu";
            button.interactable = gm.CanUpgrade(_type);
            if (effectText != null) effectText.text = gm.GetUpgradeEffect(_type);
        }
    }
}
