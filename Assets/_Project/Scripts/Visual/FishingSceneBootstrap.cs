using System.Collections.Generic;
using LazyAngler.Data;
using LazyAngler.Systems;
using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Nạp prefab map (ZoneData.mapPrefab) của bản đồ đang câu và dựng ngư dân tại điểm đứng của map.
    /// Bố cục (trời, nước, cầu, thùng, vùng cá...) nằm hoàn toàn trong prefab, xem MapView. Khi đổi map,
    /// prefab cũ bị hủy và prefab mới được nạp. Camera chỉ vẽ trong viewportArea (vùng trống giữa HUD)
    /// và được chỉnh để 1 pixel art = số nguyên pixel màn hình.
    /// </summary>
    public class FishingSceneBootstrap : MonoBehaviour
    {
        [Tooltip("Material sprite của URP 2D (Sprite-Unlit-Default). Để trống thì tìm shader bằng Shader.Find.")]
        [SerializeField] private Material spriteMaterial;
        [Tooltip("Pixel art của ngư dân, cá... Để trống thì dựng tại runtime từ FishingPixelArt.")]
        [SerializeField] private FishingArtSet art;
        [Tooltip("Vùng UI (Screen Space Overlay) dành cho cảnh câu. Để trống thì dùng toàn màn hình.")]
        [SerializeField] private RectTransform viewportArea;
        [Tooltip("Camera vẽ cảnh. Để trống thì dùng Camera.main.")]
        [SerializeField] private Camera worldCamera;
        [Tooltip("Map dùng khi ZoneData chưa gán mapPrefab.")]
        [SerializeField] private GameObject defaultMapPrefab;
        [Tooltip("Nửa chiều cao mong muốn; giá trị thật được làm tròn để pixel art hiển thị nguyên pixel.")]
        [SerializeField] private float targetHalfHeight = 3.2f;

        public IdleFishingPresenter Presenter { get; private set; }
        public MapView CurrentMap { get; private set; }

        private Camera _cam;
        private Vector2Int _screenSize;
        private Dictionary<string, Sprite> _runtimeSprites;
        private SpriteRenderer _body, _arm;
        private RodVisual _rod;
        private GameManager _gm;
        private GameObject _mapInstance;
        private int _loadedZone = -1;

        // Start (không phải Awake): CanvasScaler đã áp tỉ lệ nên đo được vùng viewport đúng.
        private void Start()
        {
            if (spriteMaterial != null) ProceduralSprites.Material = spriteMaterial;
            if (art == null || !art.IsComplete)
            {
                if (art != null) Debug.LogWarning("[FishingSceneBootstrap] FishingArtSet thiếu sprite, dùng pixel art dựng tại runtime.", this);
                art = FishingArtSet.CreateRuntime(out _runtimeSprites);
            }
            FishSwimmer.SpriteProvider = r => r >= Rarity.Rare ? art.fishLarge : art.fishSmall;

            _cam = worldCamera != null ? worldCamera : Camera.main;
            if (_cam == null)
            {
                _cam = new GameObject("Main Camera").AddComponent<Camera>();
                _cam.tag = "MainCamera";
            }
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            // Giờ/thời tiết (đêm, hoàng hôn, mưa) phủ lên cảnh, đọc GameManager.ActiveWeather.
            if (_cam.GetComponent<WeatherVisuals>() == null) _cam.gameObject.AddComponent<WeatherVisuals>();
            // Cho phép chạm vào object 2D trong cảnh (cá vàng sự kiện) qua EventSystem.
            if (_cam.GetComponent<UnityEngine.EventSystems.Physics2DRaycaster>() == null)
                _cam.gameObject.AddComponent<UnityEngine.EventSystems.Physics2DRaycaster>();

            _gm = GameManager.Instance;
            if (_gm != null)
            {
                _gm.StateChanged += ApplyLoadout;
                _gm.ZoneChanged += OnZoneChanged;
            }
            LoadMap(_gm != null ? _gm.CurrentZone : null);
        }

        private void OnDestroy()
        {
            FishSwimmer.SpriteProvider = null;
            if (_gm == null) return;
            _gm.StateChanged -= ApplyLoadout;
            _gm.ZoneChanged -= OnZoneChanged;
        }

        private void OnZoneChanged(int zoneIndex)
        {
            if (zoneIndex != _loadedZone) LoadMap(_gm.CurrentZone);
        }

        private void LateUpdate()
        {
            // Đổi kích thước màn hình (xoay, đổi Game view): chỉ cần khớp lại vùng camera.
            if (CurrentMap != null && (_screenSize.x != Screen.width || _screenSize.y != Screen.height)) FitViewport(CurrentMap);
        }

        // ---------- Nạp map ----------

        private void LoadMap(ZoneData zone)
        {
            if (_mapInstance != null)
            {
                _mapInstance.SetActive(false); // OnDisable chạy ngay, Destroy xảy ra cuối frame
                Destroy(_mapInstance);
                _mapInstance = null;
                CurrentMap = null;
                Presenter = null;
                _body = _arm = null;
                _rod = null;
            }

            _loadedZone = _gm != null ? _gm.CurrentZoneIndex : -1;
            var prefab = zone != null && zone.mapPrefab != null ? zone.mapPrefab : defaultMapPrefab;
            if (prefab == null)
            {
                Debug.LogError("[FishingSceneBootstrap] Map chưa có prefab. Gán ZoneData.mapPrefab hoặc defaultMapPrefab.", this);
                return;
            }

            _mapInstance = Instantiate(prefab);
            _mapInstance.name = prefab.name;
            var view = _mapInstance.GetComponent<MapView>();
            string missing = view != null ? view.Validate() : "MapView (gốc prefab)";
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.LogError($"[FishingSceneBootstrap] Prefab map '{prefab.name}' thiếu: {missing}.", prefab);
                Destroy(_mapInstance);
                _mapInstance = null;
                return;
            }
            CurrentMap = view;

            FitViewport(view);
            _cam.backgroundColor = view.backgroundColor;

            var runtime = new GameObject("Runtime").transform;
            runtime.SetParent(_mapInstance.transform, false);
            var rig = BuildFisherman(runtime, view);

            Presenter = runtime.gameObject.AddComponent<IdleFishingPresenter>();
            Presenter.Setup(rig, view.fishZone, view.bucket, view.shorePoint.position, view.CastXRange);

            ApplyLoadout();
        }

        /// <summary>Áp cần câu và trang phục đang dùng (gọi lại khi mua cần/skin trong Cửa hàng).</summary>
        private void ApplyLoadout()
        {
            if (_gm == null) return;
            var tier = _gm.Stats.RodTier;
            if (_rod != null && _rod.Tier != tier) _rod.Apply(tier);
            var skin = _gm.EquippedSkin;
            if (_body != null) _body.color = skin != null ? skin.bodyTint : Color.white;
            if (_arm != null) _arm.color = skin != null ? skin.armTint : Color.white;
        }

        /// <summary>
        /// Đặt Camera.rect trùng viewportArea và chọn orthographicSize để mỗi pixel art phủ đúng k pixel màn hình
        /// (k nguyên, gần targetHalfHeight nhất) → pixel art sắc nét, không méo. Mép trên khớp MapView.cameraTop.
        /// </summary>
        private void FitViewport(MapView view)
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            var rect = new Rect(0, 0, 1, 1);
            if (viewportArea != null && Screen.width > 0 && Screen.height > 0)
            {
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4];
                viewportArea.GetWorldCorners(corners); // Overlay: world = pixel màn hình
                float x0 = Mathf.Clamp01(corners[0].x / Screen.width), y0 = Mathf.Clamp01(corners[0].y / Screen.height);
                float x1 = Mathf.Clamp01(corners[2].x / Screen.width), y1 = Mathf.Clamp01(corners[2].y / Screen.height);
                if (x1 - x0 >= 0.01f && y1 - y0 >= 0.01f) rect = Rect.MinMaxRect(x0, y0, x1, y1);
            }
            _cam.rect = rect;

            float bandPx = Mathf.Max(1f, rect.height * Screen.height);
            int ppu = art != null ? art.pixelsPerUnit : FishingPixelArt.PixelsPerUnit;
            int k = Mathf.Max(1, Mathf.RoundToInt(bandPx / (2f * ppu * targetHalfHeight)));
            float half = bandPx / (2f * ppu * k);
            _cam.orthographicSize = half;
            _cam.transform.position = new Vector3(0f, view.cameraTop.position.y - half, -10f);
        }

        // ---------- Ngư dân ----------

        private FishingRig BuildFisherman(Transform root, MapView view)
        {
            var man = new GameObject("Fisherman").transform;
            man.SetParent(root, false);
            man.position = view.fishermanAnchor.position;
            var body = Pixel("Body", man, art.fishermanFrames[0], Vector3.zero, 12);

            // Vai (dời theo khung animation) → tay → cần câu (các đoạn sprite theo cấp).
            var shoulder = new GameObject("Shoulder").transform;
            shoulder.SetParent(man, false);
            _body = body;
            _arm = Pixel("Arm", shoulder, art.arm, Vector3.zero, 14);
            var rodRoot = new GameObject("Rod").transform;
            rodRoot.SetParent(shoulder, false);
            rodRoot.localPosition = new Vector3(art.Unit(7), 0f, 0f);
            var rod = rodRoot.gameObject.AddComponent<RodVisual>();
            rod.Setup(13, art.sparkle, RuntimeSprite("Rod1_Butt"), RuntimeSprite("Rod1_Mid"), RuntimeSprite("Rod1_Tip"));
            var tiers = _gm != null ? _gm.Config.rodTiers : null;
            rod.Apply(RodTierData.ForLevel(tiers, _gm != null ? _gm.RodLevel : 0));
            _rod = rod;

            var animator = man.gameObject.AddComponent<FishermanAnimator>();
            animator.Setup(body, shoulder, art);

            var bobber = Pixel("Bobber", root, art.bobber, Vector3.zero, 25).transform;
            var alert = Pixel("Alert", root, art.alert, Vector3.zero, 40).transform;

            var lineGo = new GameObject("FishingLine");
            lineGo.transform.SetParent(root, false);
            var line = lineGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 16;
            line.startWidth = line.endWidth = art.Unit(1);
            line.numCapVertices = 0;
            line.sortingOrder = 24;
            if (ProceduralSprites.Material != null) line.sharedMaterial = ProceduralSprites.Material;

            var rig = man.gameObject.AddComponent<FishingRig>();
            rig.Setup(shoulder, rod, animator, bobber, alert, line, view.WaterY);
            return rig;
        }

        private static SpriteRenderer Pixel(string name, Transform parent, Sprite sprite, Vector3 localPos, int order)
            => ProceduralSprites.Create(name, parent, sprite, Color.white, localPos, Vector2.one, order);

        /// <summary>Sprite cần câu dự phòng (khi cấp cần thiếu sprite) từ pixel art runtime.</summary>
        private Sprite RuntimeSprite(string name)
        {
            _runtimeSprites ??= FishingPixelArt.BuildRuntime();
            return _runtimeSprites.TryGetValue(name, out var s) ? s : null;
        }
    }
}
