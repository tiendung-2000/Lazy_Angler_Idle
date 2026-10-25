using System.Collections;
using TMPro;
using UnityEngine;

namespace LazyAngler.UI
{
    /// <summary>
    /// Biển thông báo lớn giữa màn hình (lên cấp cần câu, mở map mới...), tự đóng sau vài giây.
    /// args: chuỗi "tiêu đề" hoặc "tiêu đề\ndòng phụ". Chỉ phủ một dải giữa màn hình nên không chặn thao tác phần còn lại.
    /// Mở lại khi đang hiện thì thay nội dung và tính giờ lại.
    /// </summary>
    public class BannerPopup : UIPanel
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private RectTransform visual;
        [SerializeField] private float showSeconds = 2.6f;
        [Tooltip("Độ phóng to lúc xuất hiện rồi thu về 1.")]
        [SerializeField] private float popScale = 1.25f;

        private float _closeAt;
        private Coroutine _pop;

        protected override void OnOpen(object args)
        {
            string text = args as string ?? "";
            int nl = text.IndexOf('\n');
            titleText.text = nl >= 0 ? text.Substring(0, nl) : text;
            subtitleText.text = nl >= 0 ? text.Substring(nl + 1) : "";
            _closeAt = Time.unscaledTime + showSeconds;
            if (_pop != null) StopCoroutine(_pop);
            if (visual != null) _pop = StartCoroutine(Pop());
        }

        protected override void OnClose()
        {
            if (_pop != null) StopCoroutine(_pop);
            _pop = null;
        }

        private void Update()
        {
            if (IsOpen && Time.unscaledTime >= _closeAt) Close();
        }

        // Phóng to bật ra rồi thu lại, tạo cảm giác "đập" vào mắt.
        private IEnumerator Pop()
        {
            const float duration = 0.35f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float u = t / duration;
                float s = Mathf.Lerp(popScale, 1f, 1f - (1f - u) * (1f - u));
                visual.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            visual.localScale = Vector3.one;
            _pop = null;
        }
    }
}
