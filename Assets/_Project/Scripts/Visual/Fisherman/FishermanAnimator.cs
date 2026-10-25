using System;
using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Áp tư thế của FishermanPoseMachine lên nhân vật: đổi khung sprite thân và dời vai (gốc tay + cần)
    /// theo khung. Góc/độ cong cần do FishingRig đọc từ <see cref="Pose"/>.
    /// Dự án không dùng Animator Controller nên dùng hệ thống animation bằng code như phần còn lại của cảnh.
    /// </summary>
    public class FishermanAnimator : MonoBehaviour
    {
        private SpriteRenderer _body;
        private Transform _shoulder;
        private FishingArtSet _art;
        private int _frame = -1;

        public FishermanPoseMachine Machine { get; } = new FishermanPoseMachine();
        public FishermanPose Pose { get; private set; }
        public FishermanState State => Machine.State;

        /// <summary>Hoạt cảnh thu cá / thất bại đã xong, nhân vật về tư thế chờ.</summary>
        public event Action<FishermanState> OnFishingAnimationCompleted;

        public void Setup(SpriteRenderer body, Transform shoulder, FishingArtSet art)
        {
            _body = body;
            _shoulder = shoulder;
            _art = art;
            Machine.AnimationCompleted -= ForwardCompleted;
            Machine.AnimationCompleted += ForwardCompleted;
            Apply(Machine.Evaluate());
        }

        private void OnDestroy() => Machine.AnimationCompleted -= ForwardCompleted;

        private void ForwardCompleted(FishermanState s) => OnFishingAnimationCompleted?.Invoke(s);

        // ---------- Lệnh từ gameplay (chỉ đổi hình, không đổi kết quả) ----------

        public bool OnFishBite() => Machine.FishBite();
        public bool OnHookStarted() => Machine.HookStarted();
        public bool OnCatchSucceeded() => Machine.CatchSucceeded();
        public bool OnCatchFailed() => Machine.CatchFailed();
        public void BeginCast() => Machine.BeginCast();
        public void EndCast() => Machine.EndCast();
        public void ResetToIdle() => Machine.ResetToIdle();
        public float ReelTension { set => Machine.ReelTension = value; }

        private void Update()
        {
            Machine.Tick(Time.deltaTime);
            Apply(Machine.Evaluate());
        }

        private void Apply(FishermanPose pose)
        {
            Pose = pose;
            if (_body == null || _art == null || _art.fishermanFrames == null) return;
            int frame = Mathf.Clamp(pose.frame, 0, _art.fishermanFrames.Length - 1);
            if (frame == _frame) return;
            _frame = frame;
            var sprite = _art.fishermanFrames[frame];
            if (sprite != null) _body.sprite = sprite;

            // Vai theo pixel của khung, đổi sang tọa độ cục bộ so với pivot của khung.
            if (_shoulder != null && sprite != null && _art.shoulderPixels != null && frame < _art.shoulderPixels.Length)
            {
                Vector2 px = _art.shoulderPixels[frame];
                Vector2 pivotPx = sprite.pivot;
                _shoulder.localPosition = (px - pivotPx) / sprite.pixelsPerUnit;
            }
        }
    }
}
