using UnityEngine;

namespace LazyAngler.Data
{
    /// <summary>Trang phục ngư dân bán trong Cửa hàng. Chỉ đổi hình (tô màu thân + tay), không đổi chỉ số.</summary>
    [CreateAssetMenu(menuName = "LazyAngler/Skin", fileName = "Skin_")]
    public class SkinData : ScriptableObject
    {
        public string skinId;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Giá (xu). 0 = có sẵn từ đầu.")]
        public long price;
        [Tooltip("Màu nhân lên sprite thân ngư dân.")]
        public Color bodyTint = Color.white;
        [Tooltip("Màu nhân lên sprite tay.")]
        public Color armTint = Color.white;
        [Tooltip("Ảnh trong Cửa hàng. Để trống thì dùng khung ngư dân đã tô màu.")]
        public Sprite icon;

        public bool IsFree => price <= 0;
    }
}
