using System;
using System.Collections;
using LazyAngler.Systems;
using LazyAngler.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>
    /// Xu bay từ một điểm trong cảnh (thùng cá) lên số xu ở HUD. Các đồng xu là Image con của root (Canvas Overlay),
    /// bay theo cung, nhỏ dần rồi biến mất khi tới đích và gọi onArrive(chỉ số xu) để HUD nảy số / phát tiếng.
    /// </summary>
    public static class CoinFlyEffect
    {
        private static readonly Color CoinColor = new Color(1f, 0.84f, 0.2f, 1f);

        /// <param name="host">Đối tượng chạy coroutine (phải đang active).</param>
        /// <param name="root">RectTransform chứa xu (phủ toàn màn hình).</param>
        /// <param name="worldCamera">Camera cảnh, dùng đổi vị trí thế giới sang màn hình.</param>
        public static void Play(MonoBehaviour host, RectTransform root, Camera worldCamera, Vector3 startWorld,
            RectTransform target, int count, Action<int> onArrive)
        {
            if (host == null || root == null || target == null || worldCamera == null || count <= 0) return;
            Vector2 screen = worldCamera.WorldToScreenPoint(startWorld);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 start);
            Vector2 end = root.InverseTransformPoint(target.position); // Overlay: world = màn hình
            for (int i = 0; i < count; i++)
                host.StartCoroutine(Fly(root, start, end, i, onArrive));
        }

        private static IEnumerator Fly(RectTransform root, Vector2 start, Vector2 end, int index, Action<int> onArrive)
        {
            var go = new GameObject("FlyingCoin", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            go.transform.SetAsLastSibling();
            var img = go.GetComponent<Image>();
            img.sprite = ProceduralSprites.Circle();
            img.color = CoinColor;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(34f, 34f);
            rt.anchoredPosition = start;
            img.enabled = false;

            // Mỗi xu bắt đầu lệch nhau một chút, bắn lên rồi bay về HUD.
            float delay = index * 0.07f, duration = 0.65f + index * 0.03f;
            yield return new WaitForSecondsRealtime(delay);
            img.enabled = true;
            Vector2 control = start + new Vector2(UnityEngine.Random.Range(-160f, 160f), UnityEngine.Random.Range(160f, 320f));
            for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
            {
                float u = e / duration;
                float k = u * u * (3f - 2f * u); // smoothstep: bắn lên rồi hút về
                rt.anchoredPosition = (1 - k) * (1 - k) * start + 2 * (1 - k) * k * control + k * k * end;
                float pulse = 1f + Mathf.Sin(u * Mathf.PI * 4f) * 0.12f;
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, u) * pulse;
                yield return null;
            }
            UnityEngine.Object.Destroy(go);
            onArrive?.Invoke(index);
        }
    }
}
