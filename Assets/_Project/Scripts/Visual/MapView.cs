using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Gắn vào gốc của prefab map. Toàn bộ bố cục (trời, nước, cầu gỗ, thùng cá, đảo...) là các object bình thường
    /// trong prefab; MapView chỉ trỏ tới vài điểm mà game cần biết. Kéo thả để thiết kế map mới, không cần sửa code.
    /// Gán prefab vào ZoneData.mapPrefab, FishingSceneBootstrap sẽ nạp khi vào map.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("Điểm cao nhất mà camera nhìn thấy (chỉ dùng Y). Camera đặt giữa theo X = 0.")]
        public Transform cameraTop;
        [Tooltip("Màu nền camera, chỉ lộ ra ngoài mép các sprite nền.")]
        public Color backgroundColor = new Color(0.72f, 0.88f, 0.98f);

        [Header("Nhân vật")]
        [Tooltip("Chân ngư dân đứng ở đây (mặt cầu).")]
        public Transform fishermanAnchor;
        [Tooltip("Thùng cá. Cá câu được sẽ nhảy vào đây.")]
        public Transform bucket;
        [Tooltip("Điểm cá được kéo vào gần bờ trước khi nhảy lên thùng.")]
        public Transform shorePoint;

        [Header("Mặt nước & điểm quăng mồi")]
        [Tooltip("Chỉ dùng Y: độ cao mặt nước.")]
        public Transform waterLine;
        [Tooltip("Mồi rơi ngẫu nhiên giữa hai điểm này (chỉ dùng X).")]
        public Transform castNear;
        public Transform castFar;

        [Header("Cá")]
        public FishZone fishZone;

        public float WaterY => waterLine != null ? waterLine.position.y : 0f;
        public Vector2 CastXRange
        {
            get
            {
                float a = castNear != null ? castNear.position.x : 0f;
                float b = castFar != null ? castFar.position.x : a + 1f;
                return new Vector2(Mathf.Min(a, b), Mathf.Max(a, b));
            }
        }

        /// <summary>Danh sách thiếu sót để báo lỗi dễ hiểu cho người thiết kế map; rỗng nghĩa là đủ.</summary>
        public string Validate()
        {
            var missing = new System.Text.StringBuilder();
            void Check(Object o, string n) { if (o == null) missing.Append(n).Append(", "); }
            Check(cameraTop, nameof(cameraTop));
            Check(fishermanAnchor, nameof(fishermanAnchor));
            Check(bucket, nameof(bucket));
            Check(shorePoint, nameof(shorePoint));
            Check(waterLine, nameof(waterLine));
            Check(castNear, nameof(castNear));
            Check(castFar, nameof(castFar));
            Check(fishZone, nameof(fishZone));
            return missing.ToString().TrimEnd(',', ' ');
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (waterLine != null) Gizmos.DrawLine(waterLine.position + Vector3.left * 5f, waterLine.position + Vector3.right * 5f);
            Gizmos.color = Color.yellow;
            if (castNear != null && castFar != null) Gizmos.DrawLine(castNear.position, castFar.position);
            Gizmos.color = Color.green;
            if (fishermanAnchor != null) Gizmos.DrawWireSphere(fishermanAnchor.position, 0.1f);
            if (shorePoint != null) Gizmos.DrawWireSphere(shorePoint.position, 0.1f);
        }
    }
}
