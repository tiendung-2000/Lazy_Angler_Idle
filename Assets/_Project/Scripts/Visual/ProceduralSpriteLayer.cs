using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Gắn cạnh một SpriteRenderer trong prefab map để tự gán sprite vẽ bằng code (ô vuông, dải chuyển màu)
    /// lúc chạy, nhờ vậy không cần file ảnh. Kích thước chỉnh bằng Scale của Transform (sprite rộng 1 đơn vị).
    /// Muốn dùng ảnh riêng thì xóa component này và gán sprite vào SpriteRenderer.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ProceduralSpriteLayer : MonoBehaviour
    {
        public enum Shape { Square, VerticalGradient }

        [SerializeField] private Shape shape = Shape.Square;
        [Tooltip("Square: màu đã nằm ở SpriteRenderer.Color. VerticalGradient: màu phía trên.")]
        [SerializeField] private Color top = Color.white;
        [Tooltip("VerticalGradient: màu phía dưới.")]
        [SerializeField] private Color bottom = Color.white;

        public Color Top { get => top; set { top = value; Apply(); } }
        public Color Bottom { get => bottom; set { bottom = value; Apply(); } }

        public void Configure(Shape newShape, Color newTop, Color newBottom)
        {
            shape = newShape;
            top = newTop;
            bottom = newBottom;
        }

        private void Awake() => Apply();

        private void Apply()
        {
            var sr = GetComponent<SpriteRenderer>();
            sr.sprite = shape == Shape.VerticalGradient ? ProceduralSprites.VerticalGradient(top, bottom) : ProceduralSprites.Square();
            if (shape == Shape.VerticalGradient) sr.color = Color.white;
        }
    }
}
