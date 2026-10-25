using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>
    /// Ô dùng chung của Bể cá: ô trong bể (ảnh cá, tên, bonus; bấm để lấy ra / mở ô mới) và dòng gợi ý cá có thể thêm.
    /// Toàn bộ ô là một nút; nội dung và hành động do AquariumPopup đặt.
    /// </summary>
    public class AquariumEntryView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text infoText;
        [Tooltip("Chữ trên nút hành động (chỉ dòng gợi ý có, vd. \"Thêm\"). Có thể để trống.")]
        [SerializeField] private TMP_Text actionText;

        private Action _onClick;

        private void Awake() => button.onClick.AddListener(() => _onClick?.Invoke());

        public void Show(Sprite sprite, Color color, string title, string info, string action, bool interactable, Action onClick)
        {
            _onClick = onClick;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.color = color;
            }
            titleText.text = title;
            infoText.text = info;
            if (actionText != null) actionText.text = action;
            button.interactable = interactable;
        }
    }
}
