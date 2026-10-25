using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LazyAngler.UI
{
    /// <summary>
    /// Vùng nhận thao tác của minigame (phủ toàn màn hình, hoặc nút "Giật cần"): báo ngay khi nhấn xuống
    /// (PointerDown, không đợi nhả tay) để thời điểm chạm khớp với vị trí kim, và chặn raycast xuống UI phía sau.
    /// Chống bấm lặp do RareFishingMinigameController xử lý (mỗi frame + cooldown).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RareMinigameInputArea : MonoBehaviour, IPointerDownHandler
    {
        public event Action Pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            // Input System gán pointerId = deviceId (luôn > 0) nên không lọc theo id;
            // chỉ bỏ chuột phải/giữa. Chạm màn hình luôn báo là nút trái.
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Pressed?.Invoke();
        }
    }
}
