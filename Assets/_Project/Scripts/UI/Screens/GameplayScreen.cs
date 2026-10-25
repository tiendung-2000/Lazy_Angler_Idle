using System.Collections;
using LazyAngler.Data;
using LazyAngler.Systems;
using LazyAngler.Utils;
using LazyAngler.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>
    /// HUD khi câu: số xu (cộng ngay khi câu được cá), chỉ số câu, trạng thái câu, cá vừa bắt và thanh điều hướng
    /// (Nâng cấp / Cửa hàng / Bộ sưu tập / Menu). Chỉ cập nhật khi đang mở.
    /// </summary>
    public class GameplayScreen : UIPanel
    {
        [Header("HUD")]
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text incomeText;
        [SerializeField] private TMP_Text lastCatchText;
        [Tooltip("Trạng thái cần câu: Đang câu / Đợi cá / Cá cắn / Đang kéo / Hụt.")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image rodProgressFill;
        [Tooltip("Tên map đang câu + tiến độ bộ sưu tập; bấm để mở Bản đồ.")]
        [SerializeField] private TMP_Text zoneText;
        [Tooltip("Giờ/thời tiết hiện tại và tác dụng của nó.")]
        [SerializeField] private TMP_Text weatherText;

        [Header("Điều hướng")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button collectionButton;
        [SerializeField] private Button questButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button mapButton;
        [Tooltip("Nút Mồi trong khung cảnh; hiện thời gian còn lại khi đang dùng mồi.")]
        [SerializeField] private Button baitButton;
        [SerializeField] private Button aquariumButton;

        private const string FishingText = "Ngư dân đang câu cá...";

        [Tooltip("Độ nảy của số xu khi câu được cá (0,15 = to thêm 15%).")]
        [SerializeField] private float coinPunchScale = 0.15f;

        private RodState _lastRodState = RodState.Casting;
        private string _status;
        private Coroutine _punch;
        private FishingSceneBootstrap _scene;
        private TMP_Text _questLabel;
        private TMP_Text _baitLabel;
        private int _baitSeconds = -2;

        protected override void OnInit()
        {
            upgradeButton.onClick.AddListener(() => Manager.ShowOnly<UpgradePopup>());
            shopButton.onClick.AddListener(() => Manager.ShowOnly<ShopPopup>());
            collectionButton.onClick.AddListener(() => Manager.ShowOnly<CollectionPopup>());
            mapButton.onClick.AddListener(() => Manager.ShowOnly<MapPopup>());
            if (aquariumButton != null) aquariumButton.onClick.AddListener(() => Manager.ShowOnly<AquariumPopup>());
            if (baitButton != null)
            {
                baitButton.onClick.AddListener(() => Manager.ShowOnly<BaitPopup>());
                _baitLabel = baitButton.GetComponentInChildren<TMP_Text>();
            }
            if (questButton != null)
            {
                questButton.onClick.AddListener(() => Manager.ShowOnly<QuestPopup>());
                _questLabel = questButton.GetComponentInChildren<TMP_Text>();
            }
            menuButton.onClick.AddListener(() =>
            {
                Manager.HideAllPopups();
                Manager.Show<MainMenuScreen>();
            });
            lastCatchText.text = FishingText;
        }

        protected override void OnOpen(object args)
        {
            Game.StateChanged += Refresh;
            Game.FishCaught += OnFishCaught;
            Game.FishMissed += OnFishMissed;
            _status = null;
            Refresh();
        }

        protected override void OnClose()
        {
            Game.StateChanged -= Refresh;
            Game.FishCaught -= OnFishCaught;
            Game.FishMissed -= OnFishMissed;
            if (_punch != null) StopCoroutine(_punch);
            _punch = null;
            coinsText.rectTransform.localScale = Vector3.one;
        }

        public override bool OnBack()
        {
            Manager.HideAllPopups();
            Manager.Show<MainMenuScreen>();
            return true;
        }

        private void Update()
        {
            var gm = Game;
            if (gm == null || gm.Rods == null) return;
            if (gm.Rods.Count > 0) rodProgressFill.fillAmount = gm.Rods[0].Progress;
            UpdateBaitLabel(gm);

            // Trạng thái câu (chỉ hiển thị, câu cá hoàn toàn tự động).
            var state = gm.Rods.Count > 0 ? gm.Rods[0].State : RodState.Waiting;

            // Lượt câu mới: quăng xong, dây đã xuống nước → bỏ kết quả lượt trước, về "đang câu cá".
            if (state == RodState.Waiting && _lastRodState != RodState.Waiting) lastCatchText.text = FishingText;
            _lastRodState = state;

            string status = gm.IsRareMinigameActive ? "<color=#FFD24A>Cá hiếm!</color>"
                : state == RodState.Casting ? "Đang câu..."
                : state == RodState.Waiting ? "Đợi cá..."
                : state == RodState.Biting ? "<color=#FFD24A>Cá cắn!</color>"
                : state == RodState.Reeling ? "Đang kéo..."
                : state == RodState.Missed ? "<color=#FF8A70>Hụt!</color>"
                : "";
            // Chỉ gán khi đổi để TMP không dựng lại mesh mỗi frame.
            if (!ReferenceEquals(status, _status))
            {
                _status = status;
                statusText.text = status;
            }
        }

        /// <summary>Nút Mồi: "Mồi" hoặc thời gian còn lại của mồi đang dùng (chỉ gán khi đổi giây để TMP không dựng lại mesh mỗi frame).</summary>
        private void UpdateBaitLabel(GameManager gm)
        {
            if (_baitLabel == null) return;
            int s = gm.ActiveBait != null ? Mathf.CeilToInt(gm.ActiveBaitRemaining) : -1;
            if (s == _baitSeconds) return;
            _baitSeconds = s;
            _baitLabel.text = s < 0 ? "Mồi" : $"Mồi\n<size=75%><color=#7CF29A>{s / 60}:{s % 60:00}</color></size>";
        }

        private void Refresh()
        {
            var gm = Game;
            coinsText.text = $"{Fmt.Number(gm.Coins)} xu";
            // Có nhiệm vụ đã xong chờ nhận thưởng thì nút có dấu chấm than.
            if (weatherText != null)
            {
                string effect = gm.WeatherEffectText();
                weatherText.text = WeatherUtil.Describe(gm.ActiveWeather) + (effect.Length > 0 ? $"\n<size=75%><color=#9FE8B0>{effect}</color></size>" : "");
            }
            if (_questLabel != null) _questLabel.text = gm.HasClaimableQuest ? "Nhiệm vụ <color=#FFD24A>!</color>" : "Nhiệm vụ";
            if (zoneText != null)
            {
                var zone = gm.CurrentZone;
                var (caught, total) = gm.ZoneCollection(gm.CurrentZoneIndex);
                zoneText.text = zone != null ? $"{zone.displayName}\n<size=75%>{caught}/{total} loài</size>" : "";
            }
            var s = gm.Stats;
            string wait = double.IsInfinity(s.ExpectedWaitSeconds) ? "∞" : $"{s.ExpectedWaitSeconds:0.#}s";
            incomeText.text = $"~{gm.IncomePerSecond:0.##} xu/s · {gm.Rods.Count} cần · Chờ ~{wait}\n" +
                              $"<color=#9FE8B0>Cắn {s.BiteChance * 100:0}%</color> · Hụt {s.MissChance * 100:0}% · " +
                              $"<color=#FFD24A>Hiếm {s.RareChance * 100:0}%</color> · Kéo {s.EffectiveDuration:0.#}s";
        }

        private void OnFishMissed(CatchResult r)
        {
            string name = r.fish != null ? r.fish.displayName : "Cá";
            lastCatchText.text = $"<color=#FF8A70>Câu hụt!</color> {name} đã thoát";
        }

        private void OnFishCaught(CatchResult r)
        {
            StartCoroutine(CoinsAfterLanding(r));
            if (r.IsTrash)
            {
                lastCatchText.text = $"Rác... <color=#FFD24A>+{r.price} xu</color>";
                return;
            }
            // Lần đầu bắt được loài này: báo "Loài mới!" (bộ sưu tập tiến thêm một bước).
            bool isNew = Game.CaughtTotals.TryGetValue(r.fishId, out int n) && n == 1;
            string tag = (isNew ? "<color=#FFD24A>Loài mới!</color> " : "")
                         + (r.isVariant ? "<color=#FF9BF0>Biến thể!</color> " : "")
                         + (Game.LastCatchWasRecord ? "<color=#7FE3FF>Kỷ lục!</color> " : "");
            lastCatchText.text = tag +
                                 $"{r.fish.displayName} <size=70%>({r.fish.rarity}, {r.size:0.#} cm)</size> · <color=#FFD24A>+{Fmt.Number(r.price)} xu</color>";
        }

        /// <summary>
        /// Khi cá nhảy vào thùng (khoảng nửa giây sau kết quả), xu bay từ thùng lên số xu ở HUD; mỗi xu tới nơi thì số xu nảy
        /// và phát tiếng xu. Không tìm thấy cảnh/thùng thì nảy và phát tiếng ngay.
        /// </summary>
        private IEnumerator CoinsAfterLanding(CatchResult r)
        {
            yield return new WaitForSecondsRealtime(0.55f);
            if (!IsOpen) yield break;

            int count = r.IsTrash ? 1 : Mathf.Clamp(1 + (int)r.fish.rarity + (r.isVariant ? 2 : 0) + (r.fish.isBoss ? 3 : 0), 1, 8);
            bool big = !r.IsTrash && (r.fish.rarity >= Rarity.Rare || r.isVariant);
            _scene ??= FindFirstObjectByType<FishingSceneBootstrap>();
            var map = _scene != null ? _scene.CurrentMap : null;
            if (map == null || map.bucket == null || Camera.main == null)
            {
                PunchCoins();
                Sfx.Play(big ? SfxId.CoinBig : SfxId.Coin);
                yield break;
            }

            CoinFlyEffect.Play(this, (RectTransform)transform, Camera.main, map.bucket.position + Vector3.up * 0.3f,
                coinsText.rectTransform, count, i =>
                {
                    PunchCoins();
                    // Xu đầu tiên có tiếng đầy đủ; các xu sau nhỏ hơn, cao dần như tiếng leng keng.
                    if (i == 0) Sfx.Play(big ? SfxId.CoinBig : SfxId.Coin, 1f, 1f);
                    else if (i < 4) Sfx.Play(SfxId.Coin, 0.35f, 1f + 0.08f * i);
                });
        }

        /// <summary>Số xu nảy lên khi được cộng tiền (phản hồi trực tiếp thay cho nút Bán cũ).</summary>
        private void PunchCoins()
        {
            if (_punch != null) StopCoroutine(_punch);
            _punch = StartCoroutine(PunchRoutine(coinsText.rectTransform));
        }

        private IEnumerator PunchRoutine(RectTransform target)
        {
            const float duration = 0.25f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                target.localScale = Vector3.one * (1f + coinPunchScale * Mathf.Sin(t / duration * Mathf.PI));
                yield return null;
            }
            target.localScale = Vector3.one;
            _punch = null;
        }
    }
}
