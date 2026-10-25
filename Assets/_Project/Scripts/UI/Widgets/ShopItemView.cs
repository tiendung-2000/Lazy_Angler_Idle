using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>Một ô trong Cửa hàng (cần câu hoặc skin): ảnh, tên, mô tả, nút hành động (Mua / Mặc / Đang dùng...).</summary>
    public class ShopItemView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionText;
        [Tooltip("Viền/nền đánh dấu món đang dùng.")]
        [SerializeField] private Graphic equippedHighlight;

        private Action _onAction;

        private void Awake() => actionButton.onClick.AddListener(() => _onAction?.Invoke());

        public void SetIcon(Sprite sprite, Color color)
        {
            if (icon == null) return;
            icon.sprite = sprite;
            icon.color = color;
            icon.enabled = sprite != null;
        }

        public void Set(string title, string desc, string action, bool interactable, bool equipped, Action onAction)
        {
            nameText.text = title;
            if (descText != null) descText.text = desc;
            actionText.text = action;
            actionButton.interactable = interactable;
            if (equippedHighlight != null) equippedHighlight.enabled = equipped;
            _onAction = onAction;
        }
    }
}
