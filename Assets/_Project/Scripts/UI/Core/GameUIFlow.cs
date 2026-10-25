using LazyAngler.Core;
using LazyAngler.Data;
using LazyAngler.Systems;
using UnityEngine;

namespace LazyAngler.UI
{
    /// <summary>
    /// Luồng UI của game, nối sự kiện gameplay với UIManager (các panel không tự tìm nhau):
    /// - Khởi động: Menu chính (hoặc vào thẳng Gameplay nếu tắt showMainMenuOnStart).
    /// - GameManager.OfflineEarned → popup AFK earn.
    /// - GameManager.ZoneUnlocked (hoàn thành bộ sưu tập map) → popup mở khóa map mới.
    /// - GameManager.CollectionRewardGranted (đủ bộ một độ hiếm / biến thể) → thông báo ngắn ToastPopup.
/// - Minigame cá hiếm bắt đầu → popup cá hiếm (lớp Top, trên mọi popup khác).
    /// - Chỉ câu cá ở màn Gameplay: Menu chính vẫn thấy cảnh câu nhưng ngư dân đứng chờ, không bắt cá,
    ///   không có cá hiếm. Bấm Chơi thì ngư dân bắt đầu quăng câu.
    /// </summary>
    [RequireComponent(typeof(UIManager))]
    public class GameUIFlow : MonoBehaviour
    {
        [SerializeField] private bool showMainMenuOnStart = true;

        private UIManager _ui;
        private GameManager _gm;
        private RareFishingMinigameController _rare;
        private WeatherFlags _lastWeather;

        // OnEnable chạy sau Awake của GameManager (thứ tự -100) và trước Start của nó, nơi báo thu nhập offline.
        private void OnEnable()
        {
            _ui = GetComponent<UIManager>();
            _gm = GameManager.Instance;
            if (_gm == null) return;
            _rare = _gm.RareMinigame;
            // Vào game ở Menu: dừng câu ngay từ đầu (trước tick đầu tiên của vòng câu).
            if (showMainMenuOnStart) _gm.SetFishingActive(false);
            _gm.OfflineEarned += OnOfflineEarned;
            _gm.ZoneUnlocked += OnZoneUnlocked;
            _gm.CollectionRewardGranted += OnCollectionReward;
            _gm.QuestCompleted += OnQuestCompleted;
            _gm.BaitExpired += OnBaitExpired;
            _gm.WeatherChanged += OnWeatherChanged;
            _gm.BossCaught += OnBossCaught;
            _gm.RodLeveledUp += OnRodLeveledUp;
            _lastWeather = _gm.ActiveWeather;
            if (_rare != null) _rare.Started += OnRareStarted;
            _ui.Opened += OnPanelChanged;
            _ui.Closed += OnPanelChanged;
        }

        private void OnDisable()
        {
            if (_gm != null)
            {
                _gm.OfflineEarned -= OnOfflineEarned;
                _gm.ZoneUnlocked -= OnZoneUnlocked;
                _gm.CollectionRewardGranted -= OnCollectionReward;
                _gm.QuestCompleted -= OnQuestCompleted;
                _gm.BaitExpired -= OnBaitExpired;
                _gm.WeatherChanged -= OnWeatherChanged;
                _gm.BossCaught -= OnBossCaught;
                _gm.RodLeveledUp -= OnRodLeveledUp;
            }
            if (_rare != null) _rare.Started -= OnRareStarted;
            if (_ui != null)
            {
                _ui.Opened -= OnPanelChanged;
                _ui.Closed -= OnPanelChanged;
            }
        }

        private void Start()
        {
            if (showMainMenuOnStart) _ui.Show<MainMenuScreen>();
            else _ui.Show<GameplayScreen>();
            // Sinh sẵn popup cá hiếm (ẩn) để lần đầu cá hiếm cắn câu mở ngay, không khựng vì Instantiate.
            _ui.Preload<RareFishPopup>();
            _ui.Preload<BossFishPopup>();
        }

        private void OnOfflineEarned(OfflineReport report) => _ui.Show<AfkEarnPopup>(report);

        // Hoàn thành bộ sưu tập (thường ngay sau minigame cá hiếm): popup nằm dưới lớp Top, hiện ra khi kết quả đóng.
        private void OnZoneUnlocked(int zone)
        {
            // Biển lớn ngay lập tức + âm fanfare; popup mở khóa (có nút đi câu) hiện sau khi kết quả đóng.
            string name = zone >= 0 && zone < _gm.Zones.Count ? _gm.Zones[zone].displayName : "";
            Sfx.Play(SfxId.LevelUp);
            _ui.Show<BannerPopup>($"<color=#7CF29A>MAP MỚI MỞ KHÓA!</color>\n{name}");
            _ui.Show<ZoneUnlockPopup>(zone);
        }

        private void OnRodLeveledUp(string tierName)
        {
            Sfx.Play(SfxId.LevelUp);
            _ui.Show<BannerPopup>($"<color=#FFD24A>LÊN CẤP CẦN CÂU!</color>\n{tierName}");
        }

        private void OnCollectionReward(string message, int pearls)
        {
            Sfx.Play(SfxId.Reward);
            _ui.Show<ToastPopup>("<color=#FFD24A>Thưởng bộ sưu tập!</color>\n" + message);
        }

        private void OnQuestCompleted(string title)
        {
            // Chỉ báo khi đang ở màn câu cá; ở Menu chính người chơi không bắt cá nên không có nhiệm vụ mới hoàn thành.
            if (!(_ui.CurrentScreen is GameplayScreen)) return;
            Sfx.Play(SfxId.Quest);
            if (!_ui.IsOpen<QuestPopup>())
                _ui.Show<ToastPopup>($"<color=#7CF29A>Nhiệm vụ hoàn thành!</color>\n{title}\n<size=75%>Vào Nhiệm vụ để nhận thưởng</size>");
        }

        // Báo khi vừa bước vào hoàng hôn / đêm / mưa lúc đang câu (không báo lúc thoát ra hay ở Menu chính).
        private void OnWeatherChanged(WeatherFlags now)
        {
            var added = now & ~_lastWeather;
            _lastWeather = now;
            if (added == WeatherFlags.None || !(_ui.CurrentScreen is GameplayScreen)) return;
            string msg = (added & WeatherFlags.Night) != 0 ? "<color=#9BB4FF>Đêm xuống!</color>\nCá hiếm dễ gặp hơn, cá ban đêm xuất hiện"
                : (added & WeatherFlags.Sunset) != 0 ? "<color=#FFB070>Hoàng hôn!</color>\nGiá cá tăng trong lúc này"
                : "<color=#9FD6FF>Trời mưa!</color>\nCá cắn nhanh hơn, cá mưa xuất hiện";
            _ui.Show<ToastPopup>(msg);
        }

        private void OnBossCaught(string bossName, int pearls, bool first)
            => ShowBossToast(bossName, pearls, first);

        private void ShowBossToast(string bossName, int pearls, bool first)
        {
            Sfx.Play(SfxId.Win);
            _ui.Show<ToastPopup>($"<color=#FF9A55>Hạ gục {bossName}!</color>\n+{pearls} ngọc trai" + (first ? "\n<size=75%>Lần đầu hạ boss của map</size>" : ""));
        }

        private void OnBaitExpired(string baitName)
            => _ui.Show<ToastPopup>($"<color=#FFD24A>Mồi đã hết tác dụng</color>\n{baitName}");

        private void OnRareStarted(IRareFishingMinigame mode, FishData fish)
        {
            // Popup đang mở (vd. còn màn kết quả) tự nhận lượt mới qua sự kiện Started.
            // Cá boss dùng popup thanh ngang riêng; cá hiếm thường dùng popup vòng tròn.
            if (mode.Type == MinigameType.Boss)
            {
                if (!_ui.IsOpen<BossFishPopup>()) _ui.Show<BossFishPopup>();
            }
            else if (!_ui.IsOpen<RareFishPopup>())
            {
                _ui.Show<RareFishPopup>();
            }
        }

        private void OnPanelChanged(UIPanel _)
        {
            // Màn hình mới mở ngay sau khi màn cũ đóng: lúc giữa chừng không có màn nào, giữ nguyên trạng thái.
            if (_ui.CurrentScreen == null) return;
            // Chỉ câu cá ở màn Gameplay; Menu chính vẫn thấy cảnh nhưng ngư dân đứng chờ, không bắt cá.
            bool fishing = _ui.CurrentScreen is GameplayScreen;
            _gm.SetFishingActive(fishing);
            // GameManager.IsRareAllowed đọc enabled của controller: tắt khi không ở màn câu cá.
            if (_rare != null && !_rare.IsRunning) _rare.enabled = fishing;
        }
    }
}
