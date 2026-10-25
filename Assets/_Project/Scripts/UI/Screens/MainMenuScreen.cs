using LazyAngler.Systems;
using LazyAngler.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>Màn hình chính: tên game, số xu, nút Chơi / Bộ sưu tập / Tái sinh. Cảnh câu vẫn chạy phía sau (idle).</summary>
    public class MainMenuScreen : UIPanel
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button collectionButton;
        [SerializeField] private Button prestigeButton;
        [SerializeField] private TMP_Text prestigeButtonText;
        [SerializeField] private TMP_Text coinsText;
        [Tooltip("Nút bật/tắt âm thanh hiệu ứng.")]
        [SerializeField] private Button soundButton;
        [SerializeField] private TMP_Text soundButtonText;

        protected override void OnInit()
        {
            if (soundButton != null)
                soundButton.onClick.AddListener(() =>
                {
                    Sfx.Enabled = !Sfx.Enabled;
                    Sfx.Play(SfxId.Click);
                    RefreshSound();
                });
            playButton.onClick.AddListener(() => Manager.Show<GameplayScreen>());
            if (collectionButton != null) collectionButton.onClick.AddListener(() => Manager.Show<CollectionPopup>());
            if (prestigeButton != null) prestigeButton.onClick.AddListener(() => Manager.Show<PrestigePopup>());
        }

        protected override void OnOpen(object args)
        {
            Game.StateChanged += Refresh;
            Refresh();
        }

        protected override void OnClose() => Game.StateChanged -= Refresh;

        // Màn hình gốc: Back không đóng.
        public override bool OnBack() => false;

        private void RefreshSound()
        {
            if (soundButtonText != null) soundButtonText.text = Sfx.Enabled ? "Âm: Bật" : "Âm: Tắt";
        }

        private void Refresh()
        {
            RefreshSound();
            if (coinsText != null) coinsText.text = $"{Fmt.Number(Game.Coins)} xu";
            if (prestigeButtonText != null)
            {
                int pending = Game.PendingPearls;
                prestigeButtonText.text = pending >= 1 ? $"Tái sinh (+{pending})" : "Tái sinh";
            }
        }
    }
}
