using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>Một dòng trong popup Bản đồ: tên, mô tả, độ khó + bộ sưu tập, danh sách yêu cầu (đạt/thiếu), nút Câu ở đây.</summary>
    public class ZoneItemView : MonoBehaviour
    {
        [SerializeField] private Image preview;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private TMP_Text requirementText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionText;
        [Tooltip("Viền đánh dấu map đang câu.")]
        [SerializeField] private Graphic currentHighlight;
        [Tooltip("Lớp phủ tối khi map còn khóa.")]
        [SerializeField] private GameObject lockOverlay;

        private Action _onAction;

        private void Awake() => actionButton.onClick.AddListener(() => _onAction?.Invoke());

        public void Set(Color previewColor, string title, string info, string requirements, string action,
            bool interactable, bool current, bool locked, Action onAction)
        {
            if (preview != null) preview.color = previewColor;
            nameText.text = title;
            infoText.text = info;
            requirementText.text = requirements;
            requirementText.gameObject.SetActive(!string.IsNullOrEmpty(requirements));
            actionText.text = action;
            actionButton.interactable = interactable;
            if (currentHighlight != null) currentHighlight.enabled = current;
            if (lockOverlay != null) lockOverlay.SetActive(locked);
            _onAction = onAction;
        }
    }
}
