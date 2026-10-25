using System.Collections.Generic;
using LazyAngler.Data;
using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Cần câu ghép từ các đoạn sprite (tay cầm → các đoạn giữa → đầu cần), gắn nối tiếp nhau.
    /// Cong cần bằng cách xoay dần từng đoạn (đoạn càng gần đầu cần càng cong). Đổi cấp cần chỉ đổi sprite.
    /// </summary>
    public class RodVisual : MonoBehaviour
    {
        // Tỉ lệ cong của đoạn thứ i (đoạn tay cầm gần như cứng).
        private static readonly float[] BendWeights = { 0.05f, 0.2f, 0.3f, 0.45f, 0.55f, 0.65f };

        private readonly List<Transform> _segments = new List<Transform>();
        private readonly List<SpriteRenderer> _sparkles = new List<SpriteRenderer>();
        private Transform _tip;
        private int _sortingOrder = 13;
        private Sprite _sparkleSprite;
        private Color _sparkleColor;

        public Transform Tip => _tip != null ? _tip : transform;
        public RodTierData Tier { get; private set; }

        private Sprite _fallbackButt, _fallbackMid, _fallbackTip;

        public void Setup(int sortingOrder, Sprite sparkleSprite, Sprite fallbackButt, Sprite fallbackMid, Sprite fallbackTip)
        {
            _sortingOrder = sortingOrder;
            _sparkleSprite = sparkleSprite;
            _fallbackButt = fallbackButt;
            _fallbackMid = fallbackMid;
            _fallbackTip = fallbackTip;
        }

        /// <summary>Dựng lại cần theo cấp. Cấp null hoặc thiếu sprite thì dùng sprite dự phòng (cần gỗ).</summary>
        public void Apply(RodTierData tier)
        {
            Sprite fallbackButt = _fallbackButt, fallbackMid = _fallbackMid, fallbackTip = _fallbackTip;
            Tier = tier;
            foreach (var s in _segments) if (s != null) Destroy(s.gameObject);
            _segments.Clear();
            _sparkles.Clear();
            _tip = null;

            var butt = tier != null && tier.buttSprite != null ? tier.buttSprite : fallbackButt;
            var mid = tier != null && tier.midSprite != null ? tier.midSprite : fallbackMid;
            var tipSprite = tier != null && tier.tipSprite != null ? tier.tipSprite : fallbackTip;
            int mids = tier != null ? tier.midSegments : 2;

            var chain = new List<Sprite> { butt };
            for (int i = 0; i < mids; i++) chain.Add(mid);
            chain.Add(tipSprite);

            Transform parent = transform;
            float offset = 0f;
            foreach (var sprite in chain)
            {
                var seg = new GameObject($"RodSegment{_segments.Count}").transform;
                seg.SetParent(parent, false);
                seg.localPosition = new Vector3(offset, 0f, 0f);
                var sr = seg.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = _sortingOrder;
                if (ProceduralSprites.Material != null) sr.sharedMaterial = ProceduralSprites.Material;
                _segments.Add(seg);
                parent = seg;
                offset = sprite != null ? sprite.rect.width / sprite.pixelsPerUnit : 0.5f;
            }

            _tip = new GameObject("RodTip").transform;
            _tip.SetParent(parent, false);
            _tip.localPosition = new Vector3(offset, 0f, 0f);

            if (tier != null && tier.sparkle && _sparkleSprite != null)
            {
                _sparkleColor = tier.sparkleColor;
                for (int i = 1; i < _segments.Count; i++)
                {
                    var sp = ProceduralSprites.Create("Sparkle", _segments[i], _sparkleSprite, _sparkleColor,
                        new Vector3(offset * (0.3f + 0.4f * (i % 2)), 0.09f * (i % 2 == 0 ? 1 : -1), 0f), Vector2.one, _sortingOrder + 1);
                    _sparkles.Add(sp);
                }
            }
        }

        /// <summary>Cong cần (độ, dương = đầu cần cong xuống phía mặt nước).</summary>
        public void SetBend(float degrees)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                float w = BendWeights[Mathf.Min(i, BendWeights.Length - 1)];
                _segments[i].localRotation = Quaternion.Euler(0f, 0f, -degrees * w);
            }
        }

        private void Update()
        {
            // Lấp lánh lệch pha cho cần huyền thoại.
            for (int i = 0; i < _sparkles.Count; i++)
            {
                float a = Mathf.Clamp01(Mathf.Sin(Time.time * 3f + i * 2.1f) * 1.4f);
                var c = _sparkleColor;
                c.a = a;
                _sparkles[i].color = c;
            }
        }
    }
}
