using System;
using System.Collections;
using LazyAngler.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>Lớp hiển thị của UI: Screen (một màn hình tại một thời điểm) → Popup (xếp chồng) → Top (cao nhất).</summary>
    public enum UILayer { Screen, Popup, Top }

    /// <summary>
    /// Lớp cơ sở cho mọi màn hình/popup do <see cref="UIManager"/> quản lý.
    /// Vòng đời: prefab chỉ được sinh ra lần đầu Show → OnInit (một lần) → OnOpen(args) mỗi lần mở → OnClose khi đóng.
    /// Đóng thì SetActive(false) và giữ lại để mở lần sau (hoặc Destroy nếu tắt keepCached).
    /// Chỉ đăng ký sự kiện game trong OnOpen/OnClose để panel ẩn không tốn chi phí cập nhật.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPanel : MonoBehaviour
    {
        [SerializeField] private UILayer layer = UILayer.Popup;
        [Tooltip("Bật: đóng thì SetActive(false) và tái sử dụng. Tắt: Destroy khi đóng (popup hiếm khi dùng).")]
        [SerializeField] private bool keepCached = true;
        [Tooltip("Nút Back (Esc / Android) có đóng panel này không.")]
        [SerializeField] private bool closeOnBack = true;
        [Tooltip("Khối nội dung chính (thẻ popup) để chạy hiệu ứng phóng to khi mở. Để trống thì chỉ mờ dần.")]
        [SerializeField] private RectTransform content;
        [Tooltip("Các nút đóng panel (nút X, nền tối...). Có thể để trống.")]
        [SerializeField] private Button[] closeButtons;
        [SerializeField] private float animSeconds = 0.15f;

        private CanvasGroup _group;
        private Coroutine _anim;

        public UILayer Layer => layer;
        public bool KeepCached => keepCached;
        public bool IsOpen { get; private set; }

        protected UIManager Manager { get; private set; }
        protected static GameManager Game => GameManager.Instance;

        internal void Init(UIManager manager)
        {
            Manager = manager;
            _group = GetComponent<CanvasGroup>();
            if (closeButtons != null)
                foreach (var b in closeButtons)
                    if (b != null) b.onClick.AddListener(Close);
            OnInit();
        }

        internal void Open(object args)
        {
            bool wasOpen = IsOpen;
            // Mở lại khi đang mở: đóng/mở logic để OnOpen luôn chạy trên trạng thái sạch (không đăng ký trùng).
            if (wasOpen) OnClose();
            IsOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            OnOpen(args);
            if (!wasOpen) Play(true, null);
        }

        internal void Hide(Action onHidden)
        {
            if (!IsOpen) return;
            IsOpen = false;
            OnClose();
            Play(false, onHidden);
        }

        /// <summary>Đóng panel này (qua UIManager để cập nhật ngăn xếp popup).</summary>
        public void Close()
        {
            if (Manager != null) Manager.Hide(this);
        }

        /// <summary>Nút Back. Mặc định đóng panel nếu closeOnBack; trả về false nếu không xử lý.</summary>
        public virtual bool OnBack()
        {
            if (!closeOnBack) return false;
            Close();
            return true;
        }

        /// <summary>Gọi một lần sau khi prefab được sinh ra (gán listener nút, tạo item...).</summary>
        protected virtual void OnInit() { }
        /// <summary>Gọi mỗi lần mở. args là tham số truyền vào UIManager.Show.</summary>
        protected virtual void OnOpen(object args) { }
        /// <summary>Gọi mỗi lần đóng, trước hiệu ứng ẩn. Gỡ đăng ký sự kiện ở đây.</summary>
        protected virtual void OnClose() { }

        // ---------- Hiệu ứng mở/đóng (thời gian thực, không phụ thuộc timeScale) ----------

        private void Play(bool show, Action done)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = null;
            if (animSeconds <= 0f || !isActiveAndEnabled)
            {
                Finish(show, done);
                return;
            }
            _anim = StartCoroutine(Animate(show, done));
        }

        private IEnumerator Animate(bool show, Action done)
        {
            _group.interactable = false;
            _group.blocksRaycasts = true;
            float from = show ? 0f : _group.alpha, to = show ? 1f : 0f;
            for (float t = 0f; t < animSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / animSeconds;
                k = show ? 1f - (1f - k) * (1f - k) : k * k; // ease out khi mở, ease in khi đóng
                _group.alpha = Mathf.Lerp(from, to, k);
                if (content != null) content.localScale = Vector3.one * Mathf.Lerp(show ? 0.9f : 1f, show ? 1f : 0.92f, k);
                yield return null;
            }
            _anim = null;
            Finish(show, done);
        }

        private void Finish(bool show, Action done)
        {
            _group.alpha = show ? 1f : 0f;
            _group.interactable = show;
            _group.blocksRaycasts = show;
            if (content != null) content.localScale = Vector3.one;
            if (!show) gameObject.SetActive(false);
            done?.Invoke();
        }
    }
}
