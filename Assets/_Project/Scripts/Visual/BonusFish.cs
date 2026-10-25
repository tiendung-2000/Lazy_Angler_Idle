using LazyAngler.Data;
using LazyAngler.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Cá vàng sự kiện: bơi nhanh ngang qua FishZone rồi biến mất. Chạm (hoặc bấm) kịp thì nhận xu thưởng.
    /// Nhận chuột/cảm ứng qua EventSystem + Physics2DRaycaster trên camera (FishingSceneBootstrap tự thêm),
    /// nên UI phủ lên trên sẽ chặn đúng như bình thường.
    /// </summary>
    public class BonusFish : MonoBehaviour, IPointerClickHandler
    {
        private SpriteRenderer _sr;
        private SpriteRenderer _glow;
        private Vector2 _from, _to;
        private float _speed, _phase, _baseY, _time;
        private double _rewardSeconds;
        private long _minReward;
        private bool _caught;

        public static BonusFish Spawn(Transform parent, Rect area, double rewardSeconds, long minReward)
        {
            var go = new GameObject("BonusFish");
            go.transform.SetParent(parent, false);
            var fish = go.AddComponent<BonusFish>();
            fish.Init(area, rewardSeconds, minReward);
            return fish;
        }

        private void Init(Rect area, double rewardSeconds, long minReward)
        {
            _rewardSeconds = rewardSeconds;
            _minReward = minReward;
            bool fromLeft = Random.value < 0.5f;
            _baseY = Random.Range(area.yMin, area.yMax);
            _from = new Vector2(fromLeft ? area.xMin - 0.6f : area.xMax + 0.6f, _baseY);
            _to = new Vector2(fromLeft ? area.xMax + 0.6f : area.xMin - 0.6f, _baseY);
            _speed = 1.7f;
            _phase = Random.value * 6f;
            transform.position = _from;

            var sprite = FishSwimmer.SpriteProvider?.Invoke(Rarity.Legendary) ?? ProceduralSprites.Fish();
            var gold = new Color(1f, 0.85f, 0.2f);
            _sr = ProceduralSprites.Create("Body", transform, sprite, gold, Vector3.zero, Vector2.one, 11);
            _glow = ProceduralSprites.Create("Glow", transform, ProceduralSprites.Circle(), new Color(1f, 0.9f, 0.3f, 0.4f),
                Vector3.zero, new Vector2(1.6f, 1f), 10);

            // Vùng chạm rộng hơn thân cá cho dễ bấm.
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.6f, 1.1f);

            float dir = Mathf.Sign(_to.x - _from.x);
            transform.localScale = new Vector3(dir, 1f, 1f);
        }

        private void Update()
        {
            if (_caught) return;
            _time += Time.deltaTime;
            Vector2 pos = Vector2.MoveTowards(transform.position, _to, _speed * Time.deltaTime);
            pos.y = _baseY + Mathf.Sin(_time * 4f + _phase) * 0.18f;
            transform.position = pos;
            float pulse = 0.3f + 0.15f * Mathf.Sin(_time * 8f);
            _glow.color = new Color(1f, 0.9f, 0.3f, pulse);
            if (Mathf.Abs(pos.x - _to.x) < 0.05f) Destroy(gameObject);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_caught) return;
            _caught = true;
            var gm = GameManager.Instance;
            long amount = gm != null ? gm.GrantBonusCoins(_rewardSeconds, _minReward) : _minReward;
            gm?.NotifyPlayerActivity();
            gm?.ReportQuest(QuestMetric.TapBonusFish, 1);
            Sfx.Play(SfxId.CoinBig);
            Destroy(GetComponent<Collider2D>());
            StartCoroutine(PopRoutine(amount));
        }

        private System.Collections.IEnumerator PopRoutine(long amount)
        {
            _glow.enabled = false;
            var textGo = new GameObject("BonusText");
            textGo.transform.position = transform.position + Vector3.up * 0.4f;
            var text = textGo.AddComponent<TextMeshPro>();
            text.text = "+" + amount;
            text.fontSize = 6f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.9f, 0.25f);
            text.sortingOrder = 50;

            const float duration = 0.9f;
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                float u = e / duration;
                textGo.transform.position += Vector3.up * (0.9f * Time.deltaTime);
                var c = text.color;
                c.a = 1f - u * u;
                text.color = c;
                _sr.color = new Color(1f, 0.85f, 0.2f, 1f - u);
                transform.localScale = new Vector3(Mathf.Sign(transform.localScale.x) * (1f + u * 0.6f), 1f + u * 0.6f, 1f);
                yield return null;
            }
            Destroy(textGo);
            Destroy(gameObject);
        }
    }
}
