using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>Một ô Bộ sưu tập: ảnh cá (bóng tối nếu chưa bắt), tên, độ hiếm, số đã bắt.</summary>
    public class CollectionItemView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image frame;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text infoText;

        private static readonly Color Undiscovered = new Color(0.12f, 0.14f, 0.2f, 1f);

        public void Set(Sprite sprite, Color rarityColor, string displayName, string info, bool discovered)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.color = discovered ? rarityColor : Undiscovered;
            }
            if (frame != null) frame.color = discovered ? rarityColor * new Color(1f, 1f, 1f, 0.2f) : new Color(1f, 1f, 1f, 0.08f);
            nameText.text = discovered ? displayName : "???";
            infoText.text = info;
        }
    }
}
