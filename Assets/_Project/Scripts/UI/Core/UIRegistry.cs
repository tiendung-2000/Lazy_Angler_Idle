using System;
using System.Collections.Generic;
using UnityEngine;

namespace LazyAngler.UI
{
    /// <summary>
    /// Danh sách prefab UI. UIManager tra prefab theo kiểu component (mỗi kiểu UIPanel một prefab),
    /// chỉ Instantiate khi panel được gọi lần đầu nên scene không chứa sẵn UI không dùng tới.
    /// </summary>
    [CreateAssetMenu(menuName = "LazyAngler/UI Registry", fileName = "UIRegistry")]
    public class UIRegistry : ScriptableObject
    {
        [SerializeField] private List<UIPanel> panels = new List<UIPanel>();

        public IReadOnlyList<UIPanel> Panels => panels;

        public Dictionary<Type, UIPanel> BuildLookup()
        {
            var map = new Dictionary<Type, UIPanel>();
            foreach (var p in panels)
            {
                if (p == null) continue;
                var type = p.GetType();
                if (map.ContainsKey(type)) Debug.LogWarning($"[UIRegistry] Trùng prefab cho {type.Name}: {p.name}", this);
                else map.Add(type, p);
            }
            return map;
        }

#if UNITY_EDITOR
        public void EditorSet(IEnumerable<UIPanel> prefabs)
        {
            panels = new List<UIPanel>(prefabs);
        }
#endif
    }
}
