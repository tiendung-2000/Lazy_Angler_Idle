using System.Collections.Generic;
using LazyAngler.Core;
using LazyAngler.Data;
using LazyAngler.Systems;
using LazyAngler.Visual;
using NUnit.Framework;
using UnityEngine;

namespace LazyAngler.Tests
{
    /// <summary>Radial theo brief mục 10: preset độ khó, góc bắt đầu, tốc độ, ranh giới 0/360, lượt, chống trùng.</summary>
    public class RadialFishingTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private RadialFishingConfig Preset(float speed, float width, int required, int attempts, float target = 0f,
            bool random = false, float start = 0f)
        {
            var p = ScriptableObject.CreateInstance<RadialFishingConfig>();
            p.rotationSpeed = speed;
            p.targetAngleWidth = width;
            p.requiredSuccesses = required;
            p.maxAttempts = attempts;
            p.targetAngle = target;
            p.randomizeTarget = random;
            p.startAngle = start;
            _created.Add(p);
            return p;
        }

        private FishData Fish(RadialFishingConfig preset)
        {
            var f = ScriptableObject.CreateInstance<FishData>();
            f.fishId = "rare";
            f.displayName = "Rare";
            f.rarity = Rarity.Rare;
            f.radialConfig = preset;
            _created.Add(f);
            return f;
        }

        [Test]
        public void Preset_IsUsedForFish_AndMarksItRare()
        {
            var easy = Preset(100f, 50f, 1, 3);
            var fish = Fish(easy);
            Assert.IsTrue(fish.IsRareMinigameFish);
            var s = fish.EffectiveMinigame;
            Assert.AreEqual(MinigameType.Radial, s.type);
            Assert.AreEqual(100f, s.rotationSpeed);
            Assert.AreEqual(50f, s.targetAngleWidth);
            Assert.AreEqual(1, s.requiredSuccesses);
            Assert.AreEqual(3, s.maxAttempts);

            fish.radialConfig = null;
            Assert.IsFalse(fish.IsRareMinigameFish, "Bỏ preset thì về cấu hình riêng (None = cá thường)");
        }

        [Test]
        public void Preset_MaxAttemptsNeverBelowRequired()
        {
            Assert.AreEqual(4, Preset(100f, 50f, 4, 2).ToSettings().maxAttempts);
        }

        [Test]
        public void Needle_StartsAtStartAngle_AndRotatesAtConfiguredSpeed()
        {
            var m = new RadialFishingMode();
            m.StartMinigame(Preset(180f, 35f, 2, 5, start: 90f).ToSettings());
            Assert.AreEqual(90f, m.NeedleAngle, 1e-4f);
            // 1,5 vòng/giây = 540°/s; 1 giây chia nhiều bước nhỏ vẫn đúng góc.
            var fast = new RadialFishingMode();
            fast.StartMinigame(Preset(540f, 35f, 2, 5).ToSettings());
            for (int i = 0; i < 60; i++) fast.Tick(1f / 60f);
            Assert.AreEqual(180f, fast.NeedleAngle, 0.05f);
        }

        [TestCase(355f, 10f, true)]
        [TestCase(5f, 10f, true)]
        [TestCase(0f, 10f, true)]
        [TestCase(10f, 10f, true)]
        [TestCase(11f, 10f, false)]
        [TestCase(349f, 10f, false)]
        public void HitTest_AcrossZero(float needle, float halfWidth, bool expected)
        {
            var m = new RadialFishingMode();
            m.StartMinigame(Preset(100f, halfWidth * 2f, 1, 3, target: 0f, start: needle).ToSettings());
            Assert.AreEqual(expected, m.SubmitInput());
        }

        [Test]
        public void Difficulties_FromTable_PlayOutCorrectly()
        {
            // Khó: cần 3 lần trúng trong tối đa 6 lượt.
            var m = new RadialFishingMode();
            MinigameResult? result = null;
            m.OnMinigameCompleted += r => result = r;
            m.StartMinigame(Preset(260f, 22f, 3, 6, target: 180f).ToSettings()); // kim ở 0°, luôn trượt
            for (int i = 0; i < 3; i++) Assert.IsFalse(m.SubmitInput());
            Assert.AreEqual(MinigameStatus.Running, m.Status, "Còn 3 lượt, vẫn có thể thắng");
            Assert.AreEqual(3, m.MaxAttempts - m.Attempts);
            m.SubmitInput();
            Assert.AreEqual(MinigameStatus.Lost, m.Status, "Không còn đủ lượt để thắng");
            Assert.AreEqual(0, result.Value.successes);
            Assert.IsFalse(m.SubmitInput(), "Không nhận input sau khi kết thúc");
            Assert.AreEqual(4, m.Attempts);
        }

        [Test]
        public void Controller_EvaluatesAtInputTime_AndBlocksDoubleInput()
        {
            var go = new GameObject("rare");
            _created.Add(go);
            var ctrl = go.AddComponent<RareFishingMinigameController>();
            var fish = Fish(Preset(100f, 40f, 2, 5, target: 0f));
            int inputs = 0, done = 0;
            ctrl.InputEvaluated += _ => inputs++;
            ctrl.Completed += (_, __) => done++;
            Assert.IsTrue(ctrl.TryStart(fish, null));
            ctrl.SubmitInput();
            ctrl.SubmitInput(); // cùng frame: chỉ tính một lần
            Assert.AreEqual(1, inputs);
            Assert.AreEqual(1, ctrl.Current.Successes, "Kim ở 0° trong vùng 0±20°");
            ctrl.Stop();
            Assert.AreEqual(1, done);
            ctrl.Stop();
            Assert.AreEqual(1, done, "Dừng lần hai không phát thêm kết quả");
        }

        [Test]
        public void Controller_ResultDismissed_RaisedOncePerCall()
        {
            var go = new GameObject("rare");
            _created.Add(go);
            var ctrl = go.AddComponent<RareFishingMinigameController>();
            int n = 0;
            ctrl.ResultDismissed += () => n++;
            ctrl.NotifyResultDismissed();
            Assert.AreEqual(1, n);
        }

        [TestCase(-1)]
        [TestCase(1)]
        [TestCase(7)]
        public void InputArea_AcceptsAnyPointerId_LeftButtonOnly(int pointerId)
        {
            // Input System UI module đặt pointerId = deviceId (> 0): trước đây bị lọc nên chạm không phản hồi.
            var go = new GameObject("area", typeof(RectTransform));
            _created.Add(go);
            var area = go.AddComponent<LazyAngler.UI.RareMinigameInputArea>();
            int pressed = 0;
            area.Pressed += () => pressed++;

            var left = new UnityEngine.EventSystems.PointerEventData(null)
            {
                pointerId = pointerId,
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
            };
            area.OnPointerDown(left);
            Assert.AreEqual(1, pressed);

            var right = new UnityEngine.EventSystems.PointerEventData(null)
            {
                pointerId = pointerId,
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Right,
            };
            area.OnPointerDown(right);
            Assert.AreEqual(1, pressed, "Chuột phải không tính");
        }

        [Test]
        public void MinigameUiSheet_HasAllCells()
        {
            var c = FishingPixelArt.PaintMinigameUI();
            var names = new HashSet<string>();
            foreach (var cell in c.Cells) names.Add(cell.name);
            foreach (var n in new[] { "RingBase", "RingTrack", "Needle", "Hub", "PipFull", "PipEmpty", "Check", "Cross" })
                Assert.IsTrue(names.Contains(n), n);
            for (int i = 0; i < c.Cells.Count; i++)
                for (int j = i + 1; j < c.Cells.Count; j++)
                    Assert.IsFalse(c.Cells[i].rect.Overlaps(c.Cells[j].rect), c.Cells[i].name + "/" + c.Cells[j].name);
        }
    }
}
