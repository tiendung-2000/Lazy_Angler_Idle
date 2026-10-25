using System.Collections;
using System.Collections.Generic;
using LazyAngler.Core;
using LazyAngler.Data;
using LazyAngler.Systems;
using LazyAngler.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>
    /// Popup minigame cá boss (lớp Top): một thanh ngang ở phía dưới, con lăn chạy qua lại (đôi khi quay đầu giữa chừng),
    /// vùng xanh trên thanh; bấm GIẬT CẦN khi con lăn nằm trong vùng xanh. Đủ số lần trúng trước khi hết lượt thì bắt được boss.
    /// Chỉ hiển thị và chuyển input; logic nằm trong RareFishingMinigameController / BossBarFishingMode.
    /// Vị trí con lăn và vùng xanh đặt bằng anchor theo % thanh nên không phụ thuộc kích thước màn hình.
    /// </summary>
    public class BossFishPopup : UIPanel
    {
        [Tooltip("Sprite pixel art (ô tiến trình, cá). Để trống thì dựng tại runtime.")]
        [SerializeField] private FishingArtSet art;

        [Header("Panel")]
        [SerializeField] private RareMinigameInputArea inputArea;
        [SerializeField] private RareMinigameInputArea hookButtonArea;
        [SerializeField] private Button hookButton;
        [SerializeField] private TMP_Text hookButtonLabel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Image bossIcon;

        [Header("Thanh ngang")]
        [SerializeField] private RectTransform barTrack;
        [Tooltip("Vùng xanh: co giãn bằng anchor theo % thanh.")]
        [SerializeField] private Image targetZone;
        [Tooltip("Con lăn: dời bằng anchor theo % thanh.")]
        [SerializeField] private RectTransform marker;
        [SerializeField] private Image markerImage;
        [SerializeField] private TMP_Text feedbackText;

        [Header("Tiến trình")]
        [SerializeField] private RectTransform progressIndicator;
        [SerializeField] private RectTransform attemptIndicator;
        [SerializeField] private float pipSize = 48f;

        [Header("Kết quả")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private Image resultFishIcon;
        [SerializeField] private TMP_Text resultMessage;
        [SerializeField] private Button okButton;
        [SerializeField] private float resultAutoCloseSeconds = 3.5f;
        [SerializeField] private float resultDelaySeconds = 0.35f;

        [Header("Màu")]
        [SerializeField] private Color targetColor = new Color(0.36f, 0.95f, 0.45f, 0.9f);
        [SerializeField] private Color targetFlashColor = new Color(1f, 1f, 0.85f, 1f);
        [SerializeField] private Color markerColor = Color.white;
        [SerializeField] private Color hitColor = new Color(0.36f, 0.95f, 0.45f);
        [SerializeField] private Color missColor = new Color(1f, 0.35f, 0.3f);
        [SerializeField] private Color pipOnColor = new Color(0.36f, 0.95f, 0.45f);
        [SerializeField] private Color attemptOnColor = new Color(1f, 0.82f, 0.3f);
        [SerializeField] private Color pipOffColor = new Color(1f, 1f, 1f, 0.3f);

        private readonly List<Image> _progressPips = new List<Image>();
        private readonly List<Image> _attemptPips = new List<Image>();
        private IRareFishingMinigame _mode;
        private RareFishingMinigameController _controller;
        private Coroutine _resultRoutine;
        private float _flashUntil;
        private float _feedbackUntil;
        private float _buttonPressedUntil;
        private bool _resultOpen;

        protected override void OnInit()
        {
            _controller = Game.RareMinigame;
            if (art == null || art.pipFull == null) art = FishingArtSet.CreateRuntime(out _);
            if (bossIcon != null && bossIcon.sprite == null) bossIcon.sprite = art.fishLarge;
            if (resultFishIcon != null && resultFishIcon.sprite == null) resultFishIcon.sprite = art.fishLarge;
            if (resultPanel != null) resultPanel.SetActive(false);
            if (feedbackText != null) feedbackText.text = "";
            if (okButton != null) okButton.onClick.AddListener(CloseResult);
        }

        protected override void OnOpen(object args)
        {
            if (_controller == null) return;
            _controller.Started += OnStarted;
            _controller.InputEvaluated += OnInputEvaluated;
            _controller.Completed += OnCompleted;
            if (inputArea != null) inputArea.Pressed += OnPressed;
            if (hookButtonArea != null) hookButtonArea.Pressed += OnHookButton;
            if (_controller.IsRunning) OnStarted(_controller.Current, _controller.CurrentFish);
        }

        protected override void OnClose()
        {
            if (inputArea != null) inputArea.Pressed -= OnPressed;
            if (hookButtonArea != null) hookButtonArea.Pressed -= OnHookButton;
            StopResultRoutine();
            _mode = null;
            if (_controller == null) return;
            _controller.Started -= OnStarted;
            _controller.InputEvaluated -= OnInputEvaluated;
            _controller.Completed -= OnCompleted;

            // Bị đóng từ ngoài (đổi màn, HideAll...): hủy minigame đang chạy, báo cảnh câu diễn tiếp.
            bool wasRunning = _controller.IsRunning;
            if (wasRunning) _controller.Stop();
            if (wasRunning || _resultOpen)
            {
                _resultOpen = false;
                _controller.NotifyResultDismissed();
            }
        }

        /// <summary>Back: đang xem kết quả thì đóng kết quả; đang chơi thì bỏ qua.</summary>
        public override bool OnBack()
        {
            CloseResult();
            return true;
        }

        private void Update()
        {
            if (_mode == null || !IsOpen) return;

            UpdateHookButton();
            SetMarker(_mode.NeedleAngle);

            float now = Time.unscaledTime;
            if (now >= _flashUntil)
            {
                markerImage.color = markerColor;
                targetZone.color = targetColor;
            }
            if (feedbackText != null && feedbackText.text.Length > 0 && now >= _feedbackUntil) feedbackText.text = "";

            if (_mode.Status == MinigameStatus.Running)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.spaceKey.wasPressedThisFrame) _controller.SubmitInput();
            }
        }

        private void OnPressed()
        {
            if (_mode != null && _mode.Status == MinigameStatus.Running) _controller.SubmitInput();
        }

        private void OnHookButton()
        {
            if (hookButton != null && !hookButton.interactable) return;
            _buttonPressedUntil = Time.unscaledTime + 0.12f;
            OnPressed();
        }

        private void UpdateHookButton()
        {
            if (hookButton == null) return;
            bool running = _mode != null && _mode.Status == MinigameStatus.Running;
            hookButton.interactable = running;
            float s = Time.unscaledTime < _buttonPressedUntil ? 0.94f : 1f;
            hookButton.transform.localScale = new Vector3(s, s, 1f);
        }

        // ---------- Sự kiện từ controller ----------

        private void OnStarted(IRareFishingMinigame mode, FishData fish)
        {
            StopResultRoutine();
            _resultOpen = false;
            _mode = mode;
            if (resultPanel != null) resultPanel.SetActive(false);
            _flashUntil = 0f;

            string name = fish != null ? fish.displayName : "Cá boss";
            titleText.text = $"<color=#FF6A55>BOSS!</color>\n<size=70%>{name}</size>";
            hintText.text = "Bấm GIẬT CẦN khi con lăn nằm trong vùng xanh";
            if (hookButtonLabel != null) hookButtonLabel.text = "GIẬT CẦN!";
            if (bossIcon != null) bossIcon.color = new Color(1f, 0.55f, 0.45f);
            markerImage.color = markerColor;
            UpdateHookButton();
            SetMarker(mode.NeedleAngle);
            RefreshTarget();
            RefreshPips();
        }

        private void OnInputEvaluated(bool hit)
        {
            _flashUntil = Time.unscaledTime + 0.25f;
            Sfx.Play(hit ? SfxId.Hit : SfxId.Miss);
            markerImage.color = hit ? hitColor : missColor;
            if (hit) targetZone.color = targetFlashColor;
            if (feedbackText != null)
            {
                feedbackText.text = hit ? "<color=#5CF27A>TRÚNG!</color>" : "<color=#FF6A55>TRƯỢT!</color>";
                _feedbackUntil = Time.unscaledTime + 0.7f;
            }
            RefreshPips();
            // Dời vùng xanh sau khi đã nháy (sau mỗi lần trúng, vùng xanh dời chỗ và hẹp đi).
            if (hit) StartCoroutine(RefreshTargetNextFrame());
        }

        private IEnumerator RefreshTargetNextFrame()
        {
            yield return new WaitForSecondsRealtime(0.12f);
            if (_mode != null) RefreshTarget();
        }

        private void OnCompleted(MinigameResult result, FishData fish)
        {
            RefreshPips();
            if (result.status == MinigameStatus.Cancelled)
            {
                _mode = null;
                Close();
                _controller.NotifyResultDismissed();
                return;
            }
            StopResultRoutine();
            _resultOpen = true;
            _resultRoutine = StartCoroutine(ShowResult(result, fish));
        }

        // ---------- Kết quả ----------

        private IEnumerator ShowResult(MinigameResult result, FishData fish)
        {
            hintText.text = "";
            yield return new WaitForSecondsRealtime(resultDelaySeconds);

            string name = fish != null ? fish.displayName : "boss";
            Sfx.Play(result.Success ? SfxId.Win : SfxId.Lose);
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
                resultPanel.transform.SetAsLastSibling();
                resultTitle.text = result.Success ? "<color=#5CF27A>HẠ GỤC BOSS!</color>" : "<color=#FF6A55>BOSS ĐÃ THOÁT</color>";
                resultMessage.text = result.Success ? $"Bắt được {name}!" : $"{name} giật đứt dây và biến mất...";
                if (resultFishIcon != null)
                    resultFishIcon.color = result.Success ? new Color(1f, 0.55f, 0.45f) : new Color(0.25f, 0.28f, 0.35f, 1f);
            }
            else
            {
                hintText.text = result.Success ? $"<color=#5CF27A>Bắt được {name}!</color>" : $"<color=#FF6A55>{name} đã thoát...</color>";
            }

            yield return new WaitForSecondsRealtime(resultAutoCloseSeconds);
            _resultRoutine = null;
            CloseResult();
        }

        private void CloseResult()
        {
            if (!_resultOpen) return;
            _resultOpen = false;
            StopResultRoutine();
            _mode = null;
            Close();
            _controller.NotifyResultDismissed();
        }

        private void StopResultRoutine()
        {
            if (_resultRoutine != null) StopCoroutine(_resultRoutine);
            _resultRoutine = null;
        }

        // ---------- Hiển thị ----------

        /// <summary>Con lăn ở vị trí percent (0–100) của thanh, bằng anchor nên đúng với mọi độ rộng thanh.</summary>
        private void SetMarker(float percent)
        {
            float u = Mathf.Clamp01(percent / 100f);
            marker.anchorMin = new Vector2(u, marker.anchorMin.y);
            marker.anchorMax = new Vector2(u, marker.anchorMax.y);
            marker.anchoredPosition = new Vector2(0f, marker.anchoredPosition.y);
        }

        private void RefreshTarget()
        {
            var centers = _mode.TargetAngles;
            targetZone.gameObject.SetActive(centers.Count > 0);
            if (centers.Count == 0) return;
            float half = _mode.TargetWidth * 0.5f;
            var rt = targetZone.rectTransform;
            rt.anchorMin = new Vector2(Mathf.Clamp01((centers[0] - half) / 100f), rt.anchorMin.y);
            rt.anchorMax = new Vector2(Mathf.Clamp01((centers[0] + half) / 100f), rt.anchorMax.y);
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
            targetZone.color = targetColor;
        }

        private void RefreshPips()
        {
            if (_mode == null) return;
            EnsurePips(_progressPips, progressIndicator, _mode.RequiredSuccesses);
            for (int i = 0; i < _progressPips.Count; i++)
            {
                bool on = i < _mode.Successes;
                _progressPips[i].sprite = on ? art.pipFull : art.pipEmpty;
                _progressPips[i].color = on ? pipOnColor : pipOffColor;
            }
            EnsurePips(_attemptPips, attemptIndicator, _mode.MaxAttempts);
            int left = _mode.MaxAttempts - _mode.Attempts;
            for (int i = 0; i < _attemptPips.Count; i++)
            {
                bool on = i < left;
                _attemptPips[i].sprite = on ? art.pipFull : art.pipEmpty;
                _attemptPips[i].color = on ? attemptOnColor : pipOffColor;
            }
        }

        private void EnsurePips(List<Image> pips, RectTransform parent, int count)
        {
            if (parent == null) return;
            while (pips.Count < count)
            {
                var go = new GameObject("Pip", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
                ((RectTransform)go.transform).sizeDelta = new Vector2(pipSize, pipSize);
                pips.Add(img);
            }
            for (int i = 0; i < pips.Count; i++) pips[i].gameObject.SetActive(i < count);
        }
    }
}
