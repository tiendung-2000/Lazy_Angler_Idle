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
    /// Popup minigame cá hiếm (Radial, dùng chung cho Pendulum), lớp Top. Chỉ hiển thị và chuyển input;
    /// logic nằm trong RareFishingMinigameController / RadialFishingMode.
    /// GameUIFlow mở popup khi controller.Started; popup tự đóng khi người chơi đóng kết quả hoặc minigame bị hủy.
    /// Các lớp UI tách riêng để đổi sprite/màu/kích thước: RadialBackground, RadialTrack, TargetZone,
    /// RotatingNeedle, FishIcon, ProgressIndicator, AttemptIndicator, phản hồi ✓/✗, màn hình kết quả.
    /// </summary>
    public class RareFishPopup : UIPanel
    {
        [Tooltip("Sprite pixel art (vòng, kim, pip, ✓/✗, cá). Để trống thì dựng tại runtime.")]
        [SerializeField] private FishingArtSet art;

        [Header("Panel")]
        [SerializeField] private RareMinigameInputArea inputArea;
        [Tooltip("Nút \"Giật cần\": nhận thao tác ngay khi nhấn xuống (giống chạm màn hình).")]
        [SerializeField] private RareMinigameInputArea hookButtonArea;
        [SerializeField] private Button hookButton;
        [SerializeField] private TMP_Text hookButtonLabel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text hintText;

        [Header("Vòng tròn")]
        [SerializeField] private Image radialBackground;
        [SerializeField] private Image radialTrack;
        [SerializeField] private Image targetTemplate;
        [SerializeField] private RectTransform rotatingNeedle;
        [SerializeField] private Image needleImage;
        [SerializeField] private Image fishIcon;
        [SerializeField] private Image feedbackIcon;

        [Header("Tiến trình")]
        [SerializeField] private RectTransform progressIndicator;
        [SerializeField] private RectTransform attemptIndicator;
        [SerializeField] private float pipSize = 44f;

        [Header("Kết quả")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private Image resultFishIcon;
        [SerializeField] private TMP_Text resultMessage;
        [SerializeField] private Button okButton;
        [Tooltip("Tự đóng màn hình kết quả sau bao lâu nếu người chơi không bấm OK (giây, thời gian thực).")]
        [SerializeField] private float resultAutoCloseSeconds = 3f;
        [SerializeField] private float resultDelaySeconds = 0.35f;

        [Header("Màu")]
        [SerializeField] private Color targetColor = new Color(0.36f, 0.95f, 0.45f, 1f);
        [SerializeField] private Color targetFlashColor = new Color(1f, 1f, 0.85f, 1f);
        [SerializeField] private Color trackColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private Color needleColor = Color.white;
        [SerializeField] private Color hitColor = new Color(0.36f, 0.95f, 0.45f);
        [SerializeField] private Color missColor = new Color(1f, 0.35f, 0.3f);
        [SerializeField] private Color pipOnColor = new Color(0.36f, 0.95f, 0.45f);
        [SerializeField] private Color attemptOnColor = new Color(1f, 0.82f, 0.3f);
        [SerializeField] private Color pipOffColor = new Color(1f, 1f, 1f, 0.3f);

        private readonly List<Image> _targets = new List<Image>();
        private readonly List<Image> _progressPips = new List<Image>();
        private readonly List<Image> _attemptPips = new List<Image>();
        private IRareFishingMinigame _mode;
        private Coroutine _resultRoutine;
        private float _flashUntil;
        private float _feedbackUntil;
        private bool _resultOpen;
        private float _buttonPressedUntil;
        private RareFishingMinigameController _controller;

        protected override void OnInit()
        {
            _controller = Game.RareMinigame;
            if (art == null || art.ringBase == null) art = FishingArtSet.CreateRuntime(out _);
            AssignIfMissing(radialBackground, art.ringBase);
            AssignIfMissing(radialTrack, art.ringTrack);
            AssignIfMissing(targetTemplate, art.ringTrack);
            AssignIfMissing(needleImage, art.needle);
            AssignIfMissing(fishIcon, art.fishLarge);
            AssignIfMissing(resultFishIcon, art.fishLarge);

            if (resultPanel != null) resultPanel.SetActive(false);
            targetTemplate.gameObject.SetActive(false);
            if (feedbackIcon != null) feedbackIcon.enabled = false;
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

        /// <summary>Back: đang xem kết quả thì đóng kết quả; đang chơi thì bỏ qua (không lọt xuống màn dưới).</summary>
        public override bool OnBack()
        {
            CloseResult();
            return true;
        }

        private void Update()
        {
            if (_mode == null || !IsOpen) return;

            UpdateHookButton();
            rotatingNeedle.localRotation = Quaternion.Euler(0f, 0f, -_mode.NeedleAngle);
            float now = Time.unscaledTime;
            if (now >= _flashUntil)
            {
                needleImage.color = needleColor;
                foreach (var t in _targets) t.color = targetColor;
            }
            if (feedbackIcon != null && feedbackIcon.enabled)
            {
                float left = _feedbackUntil - now;
                if (left <= 0f) feedbackIcon.enabled = false;
                else feedbackIcon.rectTransform.localScale = Vector3.one * (1f + Mathf.Clamp01(left - 0.35f) * 0.6f);
            }

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

        /// <summary>Nút chỉ bấm được khi minigame đang chạy; nhún nhẹ khi vừa bấm.</summary>
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
            if (feedbackIcon != null) feedbackIcon.enabled = false;
            _flashUntil = 0f;

            string name = fish != null ? fish.displayName : "Cá hiếm";
            titleText.text = $"Cá hiếm cắn câu!\n<size=70%>{name}</size>";
            hintText.text = mode.Type == MinigameType.Radial
                ? "Bấm GIẬT CẦN khi kim quay vào vùng xanh"
                : "Bấm GIẬT CẦN khi kim lắc qua vùng xanh";
            if (hookButtonLabel != null) hookButtonLabel.text = "GIẬT CẦN!";
            UpdateHookButton();
            needleImage.color = needleColor;
            radialTrack.color = trackColor;

            var fishColor = fish != null ? FishSwimmer.ColorFor(fish.rarity) : Color.white;
            if (fishIcon != null) fishIcon.color = fishColor;

            if (mode is PendulumFishingMode p)
                SetArc(radialTrack, p.MinAngle, p.MaxAngle - p.MinAngle);
            else
                SetArc(radialTrack, 0f, 360f);

            RefreshTargets();
            RefreshPips();
        }

        private void OnInputEvaluated(bool hit)
        {
            _flashUntil = Time.unscaledTime + 0.25f;
            Sfx.Play(hit ? SfxId.Hit : SfxId.Miss);
            needleImage.color = hit ? hitColor : missColor;
            if (hit)
            {
                // Vùng mục tiêu nháy sáng tại vị trí vừa trúng (trước khi dời đi).
                foreach (var t in _targets) t.color = targetFlashColor;
            }
            ShowFeedback(hit);
            RefreshPips();
            // Dời vùng mục tiêu sau khi đã nháy (nếu cấu hình đặt ngẫu nhiên).
            if (hit) StartCoroutine(RefreshTargetsNextFrame());
        }

        private IEnumerator RefreshTargetsNextFrame()
        {
            yield return new WaitForSecondsRealtime(0.12f);
            if (_mode != null) RefreshTargets();
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
            _resultOpen = true; // chờ người chơi xem kết quả (đóng sớm vẫn báo ResultDismissed)
            _resultRoutine = StartCoroutine(ShowResult(result, fish));
        }

        // ---------- Kết quả ----------

        private IEnumerator ShowResult(MinigameResult result, FishData fish)
        {
            hintText.text = "";
            yield return new WaitForSecondsRealtime(resultDelaySeconds);

            string name = fish != null ? fish.displayName : "cá hiếm";
            Sfx.Play(result.Success ? SfxId.Win : SfxId.Lose);
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
                resultPanel.transform.SetAsLastSibling();
                resultTitle.text = result.Success ? "<color=#5CF27A>THÀNH CÔNG!</color>" : "<color=#FF6A55>THẤT BẠI</color>";
                resultMessage.text = result.Success ? $"Bắt được {name}!" : $"{name} đã thoát...";
                if (resultFishIcon != null)
                {
                    resultFishIcon.color = result.Success && fish != null
                        ? FishSwimmer.ColorFor(fish.rarity)
                        : new Color(0.25f, 0.28f, 0.35f, 1f); // bóng tối khi trượt
                }
            }
            else
            {
                hintText.text = result.Success ? $"<color=#5CF27A>Bắt được {name}!</color>" : $"<color=#FF6A55>{name} đã thoát...</color>";
            }

            yield return new WaitForSecondsRealtime(resultAutoCloseSeconds);
            _resultRoutine = null;
            CloseResult();
        }

        /// <summary>Đóng màn hình kết quả (nút OK hoặc tự đóng). Gọi nhiều lần vẫn an toàn.</summary>
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

        private void ShowFeedback(bool hit)
        {
            if (feedbackIcon == null) return;
            feedbackIcon.sprite = hit ? art.check : art.cross;
            feedbackIcon.enabled = feedbackIcon.sprite != null;
            _feedbackUntil = Time.unscaledTime + 0.6f;
        }

        private void RefreshPips()
        {
            if (_mode == null) return;
            // ProgressIndicator: ô sáng = số lần đã trúng / cần trúng.
            EnsurePips(_progressPips, progressIndicator, _mode.RequiredSuccesses);
            for (int i = 0; i < _progressPips.Count; i++)
            {
                bool on = i < _mode.Successes;
                _progressPips[i].sprite = on ? art.pipFull : art.pipEmpty;
                _progressPips[i].color = on ? pipOnColor : pipOffColor;
            }
            // AttemptIndicator: ô sáng = lượt còn lại.
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

        private void RefreshTargets()
        {
            var centers = _mode.TargetAngles;
            while (_targets.Count < centers.Count)
            {
                var img = Instantiate(targetTemplate, targetTemplate.transform.parent);
                img.name = "TargetZone";
                img.transform.SetSiblingIndex(targetTemplate.transform.GetSiblingIndex() + 1);
                _targets.Add(img);
            }
            for (int i = 0; i < _targets.Count; i++)
            {
                bool on = i < centers.Count;
                _targets[i].gameObject.SetActive(on);
                if (!on) continue;
                _targets[i].color = targetColor;
                SetArc(_targets[i], centers[i] - _mode.TargetWidth * 0.5f, _mode.TargetWidth);
            }
        }

        /// <summary>
        /// Vẽ cung bằng Image Filled Radial360 (gốc 12 giờ, chiều kim đồng hồ) trên sprite dải đường chạy:
        /// xoay ảnh để cung bắt đầu tại startAngle, fillAmount = độ rộng / 360. Cùng quy ước góc với logic.
        /// </summary>
        private static void SetArc(Image img, float startAngle, float width)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = Mathf.Clamp01(width / 360f);
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -startAngle);
        }

        private static void AssignIfMissing(Image img, Sprite sprite)
        {
            if (img != null && img.sprite == null && sprite != null) img.sprite = sprite;
        }
    }
}
