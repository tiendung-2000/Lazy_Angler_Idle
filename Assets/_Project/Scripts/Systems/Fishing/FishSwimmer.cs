using System;
using System.Collections;
using LazyAngler.Data;
using LazyAngler.Visual;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LazyAngler.Systems
{
    /// <summary>
    /// Một con cá bơi dưới nước. Tự bơi lang thang trong FishZone, quay đầu khi đổi hướng.
    /// IdleFishingPresenter điều khiển khi cá lao tới mồi, bị móc câu, bỏ chạy hoặc nhảy vào thùng.
    /// </summary>
    public class FishSwimmer : MonoBehaviour
    {
        public enum Mode { Wander, Approach, Hooked, Fleeing, Jumping }

        public Rarity Rarity { get; private set; }
        public Mode CurrentMode { get; private set; }
        /// <summary>Cá đang bơi tự do và đã hiện rõ, có thể được chọn để cắn câu.</summary>
        public bool IsAvailable => CurrentMode == Mode.Wander && _alpha >= 1f;
        /// <summary>Sprite cá theo độ hiếm (pixel art, do cảnh gán). Null thì dùng sprite vẽ bằng code.</summary>
        public static Func<Rarity, Sprite> SpriteProvider;

        /// <summary>Nửa chiều dài thân (đơn vị world), dùng để tính vị trí miệng.</summary>
        public float HalfLength => 0.5f * Scale * BodyWidth;

        private float BodyWidth => _sr != null && _sr.sprite != null ? _sr.sprite.bounds.size.x : 1f;
        /// <summary>Vị trí miệng cá theo hướng đang quay.</summary>
        public Vector3 MouthPosition => transform.position + Vector3.right * (_facing * HalfLength * 0.95f);

        private SpriteRenderer _sr;
        private SpriteRenderer _glow;
        private FishZone _zone;
        private float _size;
        private float _bossScale = 1f;

        /// <summary>Kích thước hiển thị thực (theo độ hiếm, nhân thêm nếu là cá boss).</summary>
        private float Scale => _size * _bossScale;

        /// <summary>Cá boss: to gấp đôi rưỡi.</summary>
        public void SetBoss(bool boss) => _bossScale = boss ? 2.5f : 1f;
        private float _speed;
        private Vector2 _target;
        private float _facing = 1f;        // hướng mong muốn: 1 phải, -1 trái
        private float _visualFacing = 1f;  // hướng đang hiển thị, đi qua 0 khi quay đầu
        private float _alpha = 1f;
        private bool _variant;
        private float _swimPhase;
        private float _struggle;
        private Action _onArrive;

        public static Color ColorFor(Rarity r) => r switch
        {
            Rarity.Common => new Color(0.62f, 0.72f, 0.78f),
            Rarity.Uncommon => new Color(0.38f, 0.82f, 0.42f),
            Rarity.Rare => new Color(0.28f, 0.55f, 1f),
            Rarity.Epic => new Color(0.72f, 0.38f, 1f),
            Rarity.Legendary => new Color(1f, 0.78f, 0.15f),
            _ => Color.white,
        };

        public void Init(Rarity rarity, FishZone zone, bool fadeIn)
        {
            _zone = zone;
            _speed = Random.Range(0.35f, 0.7f);
            _swimPhase = Random.value * 10f;
            _facing = _visualFacing = Random.value < 0.5f ? -1f : 1f;

            _sr = ProceduralSprites.Create("Body", transform, ProceduralSprites.Fish(), ColorFor(rarity),
                Vector3.zero, Vector2.one, 10);
            SetRarity(rarity);

            CurrentMode = Mode.Wander;
            PickTarget();
            _alpha = fadeIn ? 0f : 1f;
            ApplyVisual(0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _swimPhase += dt * (CurrentMode == Mode.Approach ? 18f : 7f);
            if (_alpha < 1f && CurrentMode != Mode.Fleeing) _alpha = Mathf.Min(1f, _alpha + dt * 1.5f);

            switch (CurrentMode)
            {
                case Mode.Wander:
                    SwimTowards(_target, _speed, dt);
                    if (Vector2.Distance(transform.position, _target) < 0.1f || Random.value < dt * 0.05f) PickTarget();
                    break;

                case Mode.Approach:
                    SwimTowards(_target, 2.6f, dt);
                    if (Vector2.Distance(transform.position, _target) < 0.03f)
                    {
                        var cb = _onArrive;
                        _onArrive = null;
                        cb?.Invoke();
                    }
                    break;

                case Mode.Fleeing:
                    transform.position += new Vector3(_facing * 3.2f, -0.6f) * dt;
                    _alpha -= dt * 1.4f;
                    if (_alpha <= 0f) Destroy(gameObject);
                    break;
            }

            float tilt = CurrentMode == Mode.Hooked
                ? Mathf.Sin(_swimPhase * 2.2f) * (10f + 25f * _struggle)
                : Mathf.Sin(_swimPhase) * 4f;
            ApplyVisual(tilt);
        }

        /// <summary>Cá biến thể: đổi màu lấp lánh (cầu vồng nhạt) và có quầng sáng.</summary>
        public void SetVariant(bool variant)
        {
            if (_variant == variant) return;
            _variant = variant;
            SetRarity(Rarity);
        }

        /// <summary>Đổi độ hiếm (màu, kích thước, quầng sáng), vd. khi kết quả câu khác màu cá đang bơi.</summary>
        public void SetRarity(Rarity rarity)
        {
            Rarity = rarity;
            var pixelSprite = SpriteProvider?.Invoke(rarity);
            if (pixelSprite != null)
            {
                // Pixel art: giữ tỉ lệ 1:1, kích thước khác nhau nằm trong sprite (nhỏ/lớn).
                _sr.sprite = pixelSprite;
                _size = 1f;
            }
            else
            {
                _sr.sprite = ProceduralSprites.Fish();
                _size = 0.6f + 0.1f * (int)rarity + Random.Range(-0.05f, 0.05f);
            }
            float w = BodyWidth;

            // Cá Epic/Legendary có quầng sáng phía sau để dễ nhận ra.
            bool glow = rarity >= Rarity.Epic || _variant;
            if (glow && _glow == null)
            {
                _glow = ProceduralSprites.Create("Glow", transform, ProceduralSprites.Circle(), ColorFor(rarity),
                    Vector3.zero, new Vector2(1.3f * w, 0.8f * w), 9);
            }
            if (_glow != null)
            {
                _glow.gameObject.SetActive(glow);
                var c = ColorFor(rarity);
                c.a = 0.35f;
                _glow.color = c;
            }
        }

        // ---------- Lệnh từ controller ----------

        /// <summary>Lao tới mồi: miệng cá chạm điểm lure thì gọi onArrive.</summary>
        public void ApproachLure(Vector3 lure, Action onArrive)
        {
            CurrentMode = Mode.Approach;
            _facing = lure.x >= transform.position.x ? 1f : -1f;
            _target = new Vector2(lure.x - _facing * HalfLength * 0.95f, lure.y);
            _onArrive = onArrive;
        }

        /// <summary>Bị móc câu: controller đặt vị trí miệng, hướng quay và mức giãy (0..1) mỗi frame.</summary>
        public void SetHookedPose(Vector3 mouth, float facing, float struggle)
        {
            CurrentMode = Mode.Hooked;
            _facing = Mathf.Sign(facing);
            _struggle = Mathf.Clamp01(struggle);
            transform.position = mouth - Vector3.right * (_facing * HalfLength * 0.95f);
        }

        /// <summary>Trở lại bơi tự do (khi không cắn được mồi).</summary>
        public void ReturnToWander()
        {
            CurrentMode = Mode.Wander;
            _onArrive = null;
            PickTarget();
        }

        /// <summary>Thoát: quay đầu bơi nhanh ra xa, mờ dần rồi tự hủy.</summary>
        public void Flee(float awayFromX)
        {
            CurrentMode = Mode.Fleeing;
            _onArrive = null;
            _facing = transform.position.x >= awayFromX ? 1f : -1f;
        }

        /// <summary>Nhảy khỏi mặt nước theo cung, xoay mình, rơi vào thùng.</summary>
        public IEnumerator JumpTo(Vector3 target, float duration)
        {
            CurrentMode = Mode.Jumping;
            // Bay phía trước ngư dân và thùng.
            _sr.sortingOrder = 17;
            if (_glow != null) _glow.sortingOrder = 16;
            Vector3 start = transform.position;
            float apex = Mathf.Max(start.y, target.y) + 1.6f;
            _facing = target.x >= start.x ? 1f : -1f;
            _visualFacing = _facing;
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                float u = e / duration;
                // Parabol qua start → apex → target (Bezier bậc 2).
                Vector3 control = new Vector3((start.x + target.x) * 0.5f, 2f * apex - (start.y + target.y) * 0.5f, 0f);
                transform.position = (1 - u) * (1 - u) * start + 2 * (1 - u) * u * control + u * u * target;
                float spin = Mathf.Lerp(40f, -70f, u) * _facing;
                transform.localRotation = Quaternion.Euler(0, 0, spin);
                float s = Mathf.Lerp(1f, 0.55f, u * u);
                transform.localScale = new Vector3(Scale * _visualFacing * s, Scale * s, 1f);
                yield return null;
            }
            transform.position = target;
        }

        // ---------- Nội bộ ----------

        private void SwimTowards(Vector2 target, float speed, float dt)
        {
            Vector2 pos = transform.position;
            float dx = target.x - pos.x;
            if (Mathf.Abs(dx) > 0.05f) _facing = Mathf.Sign(dx);
            transform.position = Vector2.MoveTowards(pos, target, speed * dt);
        }

        private void PickTarget()
        {
            _target = _zone != null ? _zone.RandomPoint() : (Vector2)transform.position;
            _speed = Random.Range(0.35f, 0.7f);
        }

        private void ApplyVisual(float tilt)
        {
            if (CurrentMode == Mode.Jumping) return;
            // Quay đầu mượt: scale.x đi từ +1 qua 0 sang -1 nên thấy cá lật mình.
            _visualFacing = Mathf.MoveTowards(_visualFacing, _facing, Time.deltaTime * 5f);
            transform.localScale = new Vector3(Scale * _visualFacing, Scale, 1f);
            transform.localRotation = Quaternion.Euler(0, 0, tilt * Mathf.Sign(_visualFacing == 0 ? 1 : _visualFacing));

            var c = _variant ? Color.HSVToRGB(Mathf.Repeat(Time.time * 0.35f, 1f), 0.5f, 1f) : ColorFor(Rarity);
            c.a = _alpha;
            _sr.color = c;
            if (_glow != null)
            {
                var g = _glow.color;
                g.a = _alpha * (0.25f + 0.15f * Mathf.Sin(Time.time * 4f));
                _glow.color = g;
            }
        }
    }
}
