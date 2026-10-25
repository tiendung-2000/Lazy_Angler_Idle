using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LazyAngler.UI
{
    /// <summary>
    /// Quản lý toàn bộ UI: sinh prefab khi panel được gọi lần đầu, đóng thì SetActive(false) và giữ lại để
    /// mở lần sau (không Instantiate lại, không giữ sẵn object không dùng trên scene).
    /// Screen: chỉ một màn hình mở tại một thời điểm (mở màn mới tự đóng màn cũ).
    /// Popup/Top: xếp chồng; nút Back (Esc / nút Back Android) đóng popup trên cùng.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private UIRegistry registry;
        [Tooltip("Gốc của từng lớp. Để trống thì tự tạo con phủ toàn canvas.")]
        [SerializeField] private RectTransform screenLayer;
        [SerializeField] private RectTransform popupLayer;
        [SerializeField] private RectTransform topLayer;

        public static UIManager Instance { get; private set; }

        /// <summary>Màn hình (lớp Screen) đang mở.</summary>
        public UIPanel CurrentScreen { get; private set; }
        public bool HasPopup => _stack.Count > 0;

        public event Action<UIPanel> Opened;
        public event Action<UIPanel> Closed;

        private Dictionary<Type, UIPanel> _prefabs;
        private readonly Dictionary<Type, UIPanel> _instances = new Dictionary<Type, UIPanel>();
        private readonly List<UIPanel> _stack = new List<UIPanel>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _prefabs = registry != null ? registry.BuildLookup() : new Dictionary<Type, UIPanel>();
            if (registry == null) Debug.LogError("[UIManager] Chưa gán UIRegistry.", this);
            screenLayer = EnsureLayer(screenLayer, "ScreenLayer");
            popupLayer = EnsureLayer(popupLayer, "PopupLayer");
            topLayer = EnsureLayer(topLayer, "TopLayer");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();
        }

        // ---------- API ----------

        /// <summary>Mở panel kiểu T (sinh prefab nếu chưa có). Gọi lại khi đang mở thì đưa lên trên và truyền args mới.</summary>
        public T Show<T>(object args = null) where T : UIPanel
        {
            var panel = GetOrCreate<T>();
            if (panel == null) return null;

            if (panel.Layer == UILayer.Screen)
            {
                if (CurrentScreen != null && CurrentScreen != panel) Hide(CurrentScreen);
                CurrentScreen = panel;
            }
            else
            {
                _stack.Remove(panel);
                _stack.Add(panel);
            }
            panel.Open(args);
            Opened?.Invoke(panel);
            return panel;
        }

        public void Hide<T>() where T : UIPanel
        {
            if (_instances.TryGetValue(typeof(T), out var panel)) Hide(panel);
        }

        public void Hide(UIPanel panel)
        {
            if (panel == null || !panel.IsOpen) return;
            _stack.Remove(panel);
            if (CurrentScreen == panel) CurrentScreen = null;
            var type = panel.GetType();
            panel.Hide(() =>
            {
                if (panel.KeepCached || panel.IsOpen) return;
                _instances.Remove(type);
                Destroy(panel.gameObject);
            });
            Closed?.Invoke(panel);
        }

        /// <summary>Đóng mọi popup lớp Popup (giữ màn hình hiện tại; lớp Top như minigame cá hiếm giữ nguyên trừ khi includeTop).</summary>
        public void HideAllPopups(bool includeTop = false)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
                if (includeTop || _stack[i].Layer != UILayer.Top) Hide(_stack[i]);
        }

        /// <summary>Mở popup T và đóng các popup khác (thanh điều hướng: chuyển qua lại, không chồng popup).</summary>
        public T ShowOnly<T>(object args = null) where T : UIPanel
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
                if (_stack[i].Layer == UILayer.Popup && !(_stack[i] is T)) Hide(_stack[i]);
            return Show<T>(args);
        }

        public bool IsOpen<T>() where T : UIPanel
            => _instances.TryGetValue(typeof(T), out var p) && p.IsOpen;

        /// <summary>Instance đã sinh (null nếu panel chưa từng mở). Không tự sinh prefab.</summary>
        public T Get<T>() where T : UIPanel
            => _instances.TryGetValue(typeof(T), out var p) ? (T)p : null;

        /// <summary>Sinh sẵn panel (ẩn) để lần mở đầu không bị giật, vd. popup cá hiếm cần mở tức thì.</summary>
        public void Preload<T>() where T : UIPanel => GetOrCreate<T>();

        /// <summary>Nút Back: đóng popup trên cùng, nếu không có thì chuyển cho màn hình hiện tại.</summary>
        public bool Back()
        {
            if (_stack.Count > 0) return _stack[_stack.Count - 1].OnBack();
            return CurrentScreen != null && CurrentScreen.OnBack();
        }

        // ---------- Nội bộ ----------

        private T GetOrCreate<T>() where T : UIPanel
        {
            if (_instances.TryGetValue(typeof(T), out var existing)) return (T)existing;
            if (!_prefabs.TryGetValue(typeof(T), out var prefab))
            {
                Debug.LogError($"[UIManager] Không có prefab cho {typeof(T).Name} trong UIRegistry.", this);
                return null;
            }

            var instance = Instantiate((T)prefab, LayerRoot(prefab.Layer), false);
            instance.name = prefab.name;
            instance.gameObject.SetActive(false);
            instance.Init(this);
            _instances.Add(typeof(T), instance);
            return instance;
        }

        private RectTransform LayerRoot(UILayer layer) => layer switch
        {
            UILayer.Screen => screenLayer,
            UILayer.Top => topLayer,
            _ => popupLayer,
        };

        private RectTransform EnsureLayer(RectTransform layer, string name)
        {
            if (layer != null) return layer;
            layer = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(transform, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            return layer;
        }
    }
}
