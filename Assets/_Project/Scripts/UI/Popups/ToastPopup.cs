using TMPro;
using UnityEngine;

namespace LazyAngler.UI
{
    /// <summary>
    /// Thông báo ngắn tự đóng (args: nội dung chuỗi), vd. nhận thưởng bộ sưu tập. Khung nhỏ nằm trên cùng,
    /// không phủ kín màn hình nên không chặn thao tác ở vùng còn lại. Mở lại khi đang hiện thì thay nội dung và tính giờ lại.
    /// </summary>
    public class ToastPopup : UIPanel
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private float showSeconds = 3.5f;

        private float _closeAt;

        protected override void OnOpen(object args)
        {
            messageText.text = args as string ?? "";
            _closeAt = Time.unscaledTime + showSeconds;
        }

        private void Update()
        {
            if (IsOpen && Time.unscaledTime >= _closeAt) Close();
        }
    }
}
