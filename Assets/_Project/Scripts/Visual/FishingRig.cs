using System.Collections;
using LazyAngler.Systems;
using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Phần hình ảnh của ngư dân + cần câu + dây + phao. Không chứa luật chơi:
    /// IdleFishingPresenter ra lệnh (quăng, cắn câu, kéo, đứt dây), rig chỉ diễn hoạt.
    /// </summary>
    public class FishingRig : MonoBehaviour
    {
        private enum BobberMode { Hanging, Flying, Floating, Biting, Hooked, Hidden }

        [Header("Góc cần (độ, 0 = nằm ngang hướng ra nước)")]
        public float idleRodAngle = 38f;
        public float backRodAngle = 120f;
        public float forwardRodAngle = 8f;
        [Header("Quăng mồi")]
        public float castArcHeight = 1.4f;
        public float castFlightTime = 0.7f;

        private Transform _rodPivot, _bobber, _alert;
        private RodVisual _rod;
        private FishermanAnimator _fisher;
        private SpriteRenderer _bobberRenderer;
        private LineRenderer _line;
        private BobberMode _mode = BobberMode.Hanging;
        private Vector3 _floatPoint;
        private float _sag = 0.15f;
        private float _rodAngle;
        private float _tension;
        private Coroutine _anim;

        public float WaterY { get; private set; }
        public Vector3 RodTip => _rod != null ? _rod.Tip.position : transform.position;
        public RodVisual Rod => _rod;
        public FishermanAnimator Fisherman => _fisher;
        public Vector3 BobberPosition => _bobber.position;
        public bool IsAnimating => _anim != null;

        public void Setup(Transform rodPivot, RodVisual rod, FishermanAnimator fisher, Transform bobber, Transform alert,
            LineRenderer line, float waterY)
        {
            _rodPivot = rodPivot;
            _rod = rod;
            _fisher = fisher;
            _bobber = bobber;
            _alert = alert;
            _line = line;
            _bobberRenderer = bobber.GetComponent<SpriteRenderer>();
            WaterY = waterY;
            ResetToRod();
        }

        // ---------- Lệnh từ controller ----------

        /// <summary>Thu phao về treo dưới đầu cần, tắt "!", dây trở lại bình thường.</summary>
        public void ResetToRod()
        {
            StopAnim();
            _mode = BobberMode.Hanging;
            _alert.gameObject.SetActive(false);
            _bobberRenderer.enabled = true;
            _line.enabled = true;
            _sag = 0.15f;
            _tension = 0f;
            _bobber.localRotation = Quaternion.identity;
            SetRodAngle(idleRodAngle);
        }

        /// <summary>Vung cần (ra sau rồi ra trước), phao bay theo cung tới landing rồi nảy trên mặt nước.</summary>
        public Coroutine CastTo(Vector3 landing)
        {
            StopAnim();
            _anim = StartCoroutine(CastRoutine(landing));
            Sfx.Play(SfxId.Cast, 0.6f);
            return _anim;
        }

        /// <summary>Đặt phao nổi ngay tại điểm (không diễn hoạt), dùng khi cần bắt kịp trạng thái logic.</summary>
        public void PlaceFloating(Vector3 point)
        {
            StopAnim();
            _floatPoint = new Vector3(point.x, WaterY, 0f);
            _mode = BobberMode.Floating;
            _alert.gameObject.SetActive(false);
            _bobberRenderer.enabled = true;
            _line.enabled = true;
            _sag = 0.35f;
            _tension = 0f;
            SetRodAngle(idleRodAngle);
        }

        /// <summary>Cá cắn: phao lặn xuống, hiện "!".</summary>
        public void Bite()
        {
            StopAnim();
            _mode = BobberMode.Biting;
            _alert.gameObject.SetActive(true);
            Splash(_floatPoint, 0.5f, silent: true);
            Sfx.Play(SfxId.Bite);
        }

        /// <summary>Đang kéo: phao/lưỡi câu theo miệng cá, cần cong theo tension, dây đỏ dần khi căng.</summary>
        public void SetHooked(Vector3 hookPos, float tension)
        {
            _mode = BobberMode.Hooked;
            _alert.gameObject.SetActive(false);
            _bobber.position = hookPos;
            _tension = Mathf.Clamp01(tension);
            _sag = Mathf.Lerp(0.25f, 0.01f, _tension);
            if (_fisher != null) _fisher.ReelTension = _tension;
            SetRodAngle(idleRodAngle);
        }

        /// <summary>Dây đứt: ẩn dây và phao, cần bật ngược lên.</summary>
        public Coroutine SnapLine()
        {
            StopAnim();
            _anim = StartCoroutine(SnapRoutine());
            return _anim;
        }

        /// <summary>Gợn nước tròn lan ra rồi mờ dần.</summary>
        public void Splash(Vector3 pos, float size = 0.6f, bool silent = false)
        {
            var at = new Vector3(pos.x, WaterY, 0f);
            StartCoroutine(SplashRoutine(at, size));
            StartCoroutine(DropletsRoutine(at, size));
            if (!silent) Sfx.Play(SfxId.Splash, Mathf.Clamp(0.5f + size * 0.5f, 0.4f, 1f), Random.Range(0.92f, 1.08f));
        }

        /// <summary>Giọt nước bắn lên theo cung rồi rơi lại mặt nước.</summary>
        private IEnumerator DropletsRoutine(Vector3 origin, float size)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt(5 + size * 8f), 5, 12);
            var drops = new SpriteRenderer[count];
            var velocity = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                drops[i] = ProceduralSprites.Create("Droplet", transform, ProceduralSprites.Square(),
                    new Color(0.9f, 0.97f, 1f, 0.95f), origin, Vector2.one * Random.Range(0.035f, 0.06f), 31);
                drops[i].transform.position = origin + new Vector3(Random.Range(-0.08f, 0.08f) * size, 0f, 0f);
                velocity[i] = new Vector2(Random.Range(-0.9f, 0.9f) * (0.5f + size), Random.Range(1.6f, 2.8f) * (0.6f + size * 0.5f));
            }
            const float gravity = 7f, life = 0.7f;
            for (float e = 0f; e < life; e += Time.deltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    if (drops[i] == null) continue;
                    velocity[i].y -= gravity * Time.deltaTime;
                    drops[i].transform.position += (Vector3)(velocity[i] * Time.deltaTime);
                    var c = drops[i].color;
                    c.a = Mathf.Clamp01(1f - e / life);
                    drops[i].color = c;
                    // Chạm mặt nước thì biến mất sớm.
                    if (velocity[i].y < 0f && drops[i].transform.position.y < origin.y) drops[i].enabled = false;
                }
                yield return null;
            }
            foreach (var d in drops) if (d != null) Destroy(d.gameObject);
        }

        // ---------- Vòng đời ----------

        private void LateUpdate()
        {
            if (_rod == null) return;
            float t = Time.time;

            switch (_mode)
            {
                case BobberMode.Hanging:
                    _bobber.position = RodTip + Vector3.down * 0.45f;
                    break;
                case BobberMode.Floating:
                    _bobber.position = _floatPoint + Vector3.up * (Mathf.Sin(t * 2.3f) * 0.03f);
                    _bobber.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.7f) * 6f);
                    break;
                case BobberMode.Biting:
                    _bobber.position = _floatPoint + new Vector3(Random.Range(-0.03f, 0.03f), -0.22f + Mathf.Sin(t * 30f) * 0.04f);
                    _alert.position = _floatPoint + Vector3.up * (0.3f + Mathf.Abs(Mathf.Sin(t * 8f)) * 0.06f);
                    break;
            }

            // Góc và độ cong cần = góc của rig (quăng, bật khi đứt dây) + tư thế animation của ngư dân.
            var pose = _fisher != null ? _fisher.Pose : default;
            float jitter = _tension > 0.75f ? Random.Range(-2f, 2f) : 0f;
            _rodPivot.localRotation = Quaternion.Euler(0, 0, _rodAngle + pose.rodAngleOffset);
            _rod.SetBend(pose.rodBend + jitter);
            UpdateLine();
        }

        private void UpdateLine()
        {
            if (!_line.enabled) return;
            Vector3 a = RodTip;
            Vector3 b = _bobber.position + Vector3.up * 0.15f;
            float dist = Vector3.Distance(a, b);
            Vector3 control = (a + b) * 0.5f + Vector3.down * (_sag * dist);
            int n = _line.positionCount;
            for (int i = 0; i < n; i++)
            {
                float u = i / (n - 1f);
                _line.SetPosition(i, (1 - u) * (1 - u) * a + 2 * (1 - u) * u * control + u * u * b);
            }
            Color c = Color.Lerp(new Color(1f, 1f, 1f, 0.85f), new Color(1f, 0.2f, 0.15f, 1f), Mathf.InverseLerp(0.4f, 1f, _tension));
            _line.startColor = _line.endColor = c;
        }

        // ---------- Coroutine ----------

        private IEnumerator CastRoutine(Vector3 landing)
        {
            _mode = BobberMode.Hanging;
            _alert.gameObject.SetActive(false);
            if (_fisher != null) _fisher.BeginCast();

            // Vung ra sau (chậm), rồi quất ra trước (nhanh).
            yield return RotateRod(idleRodAngle, backRodAngle, 0.32f, EaseOutQuad);
            yield return RotateRod(backRodAngle, forwardRodAngle, 0.12f, EaseInQuad);

            // Phao bay theo cung parabol.
            _mode = BobberMode.Flying;
            _sag = 0.04f;
            Vector3 start = _bobber.position;
            float time = 0f;
            while (time < castFlightTime)
            {
                time += Time.deltaTime;
                float u = Mathf.Clamp01(time / castFlightTime);
                Vector3 p = Vector3.Lerp(start, landing, u);
                p.y += castArcHeight * 4f * u * (1f - u);
                _bobber.position = p;
                _bobber.localRotation = Quaternion.Euler(0, 0, u * -540f);
                SetRodAngle(Mathf.Lerp(forwardRodAngle, idleRodAngle, u));
                yield return null;
            }
            _bobber.localRotation = Quaternion.identity;
            _bobber.position = landing;
            Splash(landing);

            // Nảy tắt dần khi chạm nước: lún xuống, bật lên, lún nhẹ...
            float[] amps = { -0.22f, 0.12f, -0.06f, 0.025f };
            foreach (float amp in amps)
            {
                float d = 0.16f;
                for (float e = 0f; e < d; e += Time.deltaTime)
                {
                    _bobber.position = landing + Vector3.up * (amp * Mathf.Sin(Mathf.PI * e / d));
                    yield return null;
                }
            }

            _floatPoint = landing;
            _sag = 0.35f;
            _mode = BobberMode.Floating;
            if (_fisher != null) _fisher.EndCast();
            _anim = null;
        }

        private IEnumerator SnapRoutine()
        {
            _mode = BobberMode.Hidden;
            _line.enabled = false;
            _bobberRenderer.enabled = false;
            _alert.gameObject.SetActive(false);
            _tension = 0f;
            // Cần bật ngược lên rồi rung về vị trí nghỉ.
            yield return RotateRod(_rodAngle, idleRodAngle + 35f, 0.08f, EaseOutQuad);
            float[] wobble = { idleRodAngle - 12f, idleRodAngle + 6f, idleRodAngle };
            float from = idleRodAngle + 35f;
            foreach (float to in wobble)
            {
                yield return RotateRod(from, to, 0.12f, EaseInOut);
                from = to;
            }
            _anim = null;
        }

        private IEnumerator RotateRod(float from, float to, float duration, System.Func<float, float> ease)
        {
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                SetRodAngle(Mathf.LerpUnclamped(from, to, ease(e / duration)));
                yield return null;
            }
            SetRodAngle(to);
        }

        private IEnumerator SplashRoutine(Vector3 pos, float size)
        {
            for (int i = 0; i < 2; i++)
            {
                var ring = ProceduralSprites.Create("Splash", transform, ProceduralSprites.Ring(),
                    new Color(1f, 1f, 1f, 0.9f), Vector3.zero, Vector2.one * 0.1f, 30);
                ring.transform.position = pos;
                StartCoroutine(FadeRing(ring, size * (1f + i * 0.6f), 0.55f + i * 0.15f));
                yield return new WaitForSeconds(0.1f);
            }
        }

        private static IEnumerator FadeRing(SpriteRenderer ring, float size, float duration)
        {
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                float u = e / duration;
                // Gợn nước dẹt (nhìn nghiêng) nên y nhỏ hơn x.
                ring.transform.localScale = new Vector3(size * u, size * u * 0.35f, 1f);
                ring.color = new Color(1f, 1f, 1f, 0.9f * (1f - u));
                yield return null;
            }
            Destroy(ring.gameObject);
        }

        private void SetRodAngle(float angle) => _rodAngle = angle;

        private void StopAnim()
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = null;
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInQuad(float t) => t * t;
        private static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }
}
