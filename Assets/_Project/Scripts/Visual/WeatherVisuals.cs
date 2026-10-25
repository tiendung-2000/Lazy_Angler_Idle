using LazyAngler.Data;
using LazyAngler.Systems;
using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Hiệu ứng giờ/thời tiết phủ lên cảnh câu (gắn vào camera cảnh): lớp màu hoàng hôn / đêm / mưa mờ dần khi đổi,
    /// trăng và sao ban đêm, mưa rơi. Dựng hoàn toàn bằng code nên không phụ thuộc prefab map; muốn map nào đó
    /// có giao diện riêng thì cứ thêm vật thể vào prefab map. Đọc GameManager.ActiveWeather.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class WeatherVisuals : MonoBehaviour
    {
        private const int OverlayOrder = 30;
        private const int StarCount = 14;

        private Camera _cam;
        private GameManager _gm;
        private SpriteRenderer _overlay, _moon;
        private SpriteRenderer[] _stars;
        private Vector2[] _starPos;
        private float[] _starPhase;
        private ParticleSystem _rain;
        private Color _overlayTarget;
        private float _nightBlend;   // 0..1, mờ dần trăng/sao
        private bool _nightTarget;

        private void Start()
        {
            _cam = GetComponent<Camera>();
            _overlay = ProceduralSprites.Create("WeatherOverlay", transform, ProceduralSprites.Square(), Color.clear,
                new Vector3(0, 0, 5f), new Vector2(80f, 80f), OverlayOrder);

            _moon = ProceduralSprites.Create("Moon", transform, ProceduralSprites.Circle(), new Color(1f, 0.97f, 0.8f, 0f),
                new Vector3(0, 0, 4f), new Vector2(0.55f, 0.55f), OverlayOrder + 1);

            _stars = new SpriteRenderer[StarCount];
            _starPos = new Vector2[StarCount];
            _starPhase = new float[StarCount];
            var rng = new System.Random(4242);
            for (int i = 0; i < StarCount; i++)
            {
                _stars[i] = ProceduralSprites.Create("Star", transform, ProceduralSprites.Square(), new Color(1f, 1f, 0.9f, 0f),
                    new Vector3(0, 0, 4f), new Vector2(0.05f, 0.05f), OverlayOrder + 1);
                _starPos[i] = new Vector2((float)rng.NextDouble() * 2f - 1f, 0.15f + (float)rng.NextDouble() * 0.8f); // x -1..1, y 0.15..0.95 của nửa trên
                _starPhase[i] = (float)rng.NextDouble() * 6.28f;
            }

            BuildRain();

            _gm = GameManager.Instance;
            if (_gm != null) _gm.WeatherChanged += Apply;
            Apply(_gm != null ? _gm.ActiveWeather : WeatherFlags.None);
            _overlay.color = _overlayTarget; // lần đầu vào thẳng, không mờ dần từ trong suốt
        }

        private void OnDestroy()
        {
            if (_gm != null) _gm.WeatherChanged -= Apply;
        }

        private void BuildRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // phát xuống dưới
            _rain = go.AddComponent<ParticleSystem>();
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.1f;
            main.startSpeed = 9f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.75f, 0.88f, 1f, 0.65f);
            main.maxParticles = 400;

            var emission = _rain.emission;
            emission.rateOverTime = 110f;

            var shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 1f, 0.1f);

            var vel = _rain.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = -1.2f; // hơi xiên

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 3f;
            rend.velocityScale = 0.03f;
            rend.sortingOrder = OverlayOrder + 1;
            if (ProceduralSprites.Material != null) rend.sharedMaterial = ProceduralSprites.Material;
        }

        private void Apply(WeatherFlags w)
        {
            bool night = (w & WeatherFlags.Night) != 0, sunset = (w & WeatherFlags.Sunset) != 0, rain = (w & WeatherFlags.Rain) != 0;
            Color c = night ? new Color(0.05f, 0.08f, 0.25f, 0.55f)
                : sunset ? new Color(1f, 0.55f, 0.2f, 0.28f)
                : new Color(0f, 0f, 0f, 0f);
            if (rain) // trời âm u: pha xám xanh, tối hơn một chút
            {
                var gray = new Color(0.42f, 0.5f, 0.6f, 1f);
                c = new Color(Mathf.Lerp(c.r, gray.r, 0.45f), Mathf.Lerp(c.g, gray.g, 0.45f), Mathf.Lerp(c.b, gray.b, 0.45f),
                    Mathf.Clamp01(c.a + 0.18f));
            }
            _overlayTarget = c;
            _nightTarget = night;

            var emission = _rain.emission;
            emission.enabled = rain;
            if (rain && !_rain.isPlaying) _rain.Play();
        }

        private void LateUpdate()
        {
            if (_overlay == null) return;
            _overlay.color = Color.Lerp(_overlay.color, _overlayTarget, Time.deltaTime * 1.5f);
            _nightBlend = Mathf.MoveTowards(_nightBlend, _nightTarget ? 1f : 0f, Time.deltaTime * 0.8f);

            float halfH = _cam.orthographicSize, halfW = halfH * _cam.aspect;
            bool visible = _nightBlend > 0.01f;
            _moon.enabled = visible;
            if (visible)
            {
                _moon.transform.localPosition = new Vector3(halfW - 0.9f, halfH - 0.8f, 4f);
                _moon.color = new Color(1f, 0.97f, 0.8f, _nightBlend);
                for (int i = 0; i < StarCount; i++)
                {
                    _stars[i].enabled = true;
                    _stars[i].transform.localPosition = new Vector3(_starPos[i].x * halfW, _starPos[i].y * halfH, 4f);
                    float tw = 0.55f + 0.45f * Mathf.Sin(Time.time * 2f + _starPhase[i]);
                    _stars[i].color = new Color(1f, 1f, 0.9f, _nightBlend * tw);
                }
            }
            else
            {
                for (int i = 0; i < StarCount; i++) _stars[i].enabled = false;
            }

            // Mưa phủ cả khung nhìn: đặt nguồn phát ở mép trên.
            _rain.transform.localPosition = new Vector3(0f, halfH + 0.6f, 3f);
        }
    }
}
