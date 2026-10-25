using System.Collections;
using LazyAngler.Core;
using LazyAngler.Data;
using LazyAngler.Systems;
using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Diễn hoạt vòng câu tự động bằng ngư dân + đàn cá. Chỉ đọc trạng thái cần câu đầu tiên
    /// (GameManager.Rods[0]) và sự kiện minigame; không quyết định kết quả, không trao thưởng.
    /// Khi hình ảnh còn bận (đang quăng, đang thả cá vào thùng) mà logic đã xong lượt, lượt đó
    /// vẫn được thưởng bình thường, chỉ không diễn; riêng cá hiếm luôn được diễn ngay.
    /// </summary>
    public class IdleFishingPresenter : MonoBehaviour
    {
        private enum Visual { Idle, Casting, Floating, Approaching, Reeling, Missing, Rare, Resolving, Landing }

        public float landingJumpTime = 0.8f;
        [Tooltip("Chờ tối đa bấy lâu (giây thực) để người chơi đóng màn hình kết quả.")]
        public float rareResultMaxWait = 6f;
        public float rarePullSeconds = 0.7f;

        private FishingRig _rig;
        private FishZone _school;
        private Transform _bucket;
        private Vector3 _bucketPos;
        private Vector3 _bucketScale;
        private Vector3 _shore;
        private Vector2 _castXRange;

        private GameManager _gm;
        private RareFishingMinigameController _rare;

        private Visual _state = Visual.Idle;
        private long _ticketId = -1;
        private FishSwimmer _fish;
        private FishSwimmer _flyingFish;
        private Vector3 _lure;
        private Vector3 _hookStart;
        private float _progressAtHook;
        private bool _hooked;
        private Coroutine _anim;
        private bool _resultDismissed;
        private bool _stopped;

        public void Setup(FishingRig rig, FishZone school, Transform bucket, Vector3 shore, Vector2 castXRange)
        {
            _rig = rig;
            _school = school;
            _bucket = bucket;
            _bucketPos = bucket.position + Vector3.up * 0.25f;
            _bucketScale = bucket.localScale;
            _shore = shore;
            _castXRange = castXRange;
        }

        private void OnEnable() => TryBind();

        private void OnDisable()
        {
            if (_rare != null)
            {
                _rare.Completed -= OnRareCompleted;
                _rare.ResultDismissed -= OnResultDismissed;
            }
            _rare = null;
            _gm = null;
            StopAnim();
            _state = Visual.Idle;
        }

        private void TryBind()
        {
            if (_gm != null) return;
            _gm = GameManager.Instance;
            if (_gm == null) return;
            _rare = _gm.RareMinigame;
            if (_rare != null)
            {
                _rare.Completed += OnRareCompleted;
                _rare.ResultDismissed += OnResultDismissed;
            }
        }

        private void Update()
        {
            if (_rig == null) return;
            if (_gm == null)
            {
                TryBind();
                if (_gm == null) return;
            }
            if (_gm.Rods == null || _gm.Rods.Count == 0) return;

            // Vòng câu tắt (Menu chính): đứng chờ, không quăng câu.
            if (!_gm.IsFishingActive)
            {
                if (!_stopped) EnterStopped();
                return;
            }
            _stopped = false;

            var rod = _gm.Rods[0];

            // Đổi cấp cần câu (nâng cấp) → đổi hình cần, chỉ số đã do FishingStatProvider đọc.
            var tier = _gm.Stats.RodTier;
            if (_rig.Rod != null && tier != _rig.Rod.Tier) _rig.Rod.Apply(tier);
            var ticket = rod.Ticket;

            // Cá hiếm luôn được diễn ngay, kể cả khi đang bận diễn lượt khác. Có nhiều cần (Thuyền) thì cá hiếm
            // có thể cắn ở cần bất kỳ: ngư dân vẫn diễn nó (cảnh chỉ vẽ một cần, các cần khác câu ngầm).
            var rare = _gm.AutoFishing.PendingRare;
            if (rare != null && rare.Id != _ticketId)
            {
                BeginRare(rare);
                return;
            }

            bool newBite = ticket != null && ticket.Id != _ticketId &&
                           (rod.State == RodState.Biting || rod.State == RodState.Reeling || rod.State == RodState.Missed);

            switch (_state)
            {
                case Visual.Idle:
                case Visual.Casting:
                    // Cá đã cắn (tự động hoặc bấm "Câu!") khi hình ảnh còn đang quăng mồi:
                    // bỏ phần quăng còn lại, đặt phao xuống nước và diễn cá cắn ngay để phản hồi tức thì.
                    if (newBite)
                    {
                        StopAnim();
                        if (_state == Visual.Idle)
                            _lure = new Vector3(Random.Range(_castXRange.x, _castXRange.y), _rig.WaterY, 0f);
                        _rig.PlaceFloating(_lure);
                        Fisher?.EndCast();
                        _state = Visual.Floating;
                        BeginBite(ticket);
                        break;
                    }
                    // Câu: quăng mồi khi cần câu bắt đầu lượt mới (Casting/Waiting).
                    if (_state == Visual.Idle && (rod.State == RodState.Casting || rod.State == RodState.Waiting))
                    {
                        _state = Visual.Casting;
                        _lure = new Vector3(Random.Range(_castXRange.x, _castXRange.y), _rig.WaterY, 0f);
                        _anim = StartCoroutine(CastRoutine());
                    }
                    break;

                case Visual.Floating:
                    // Đợi cá cắn câu.
                    if (newBite) BeginBite(ticket);
                    break;

                case Visual.Approaching:
                case Visual.Reeling:
                    if (ticket == null || ticket.Id != _ticketId)
                    {
                        StartLanding(); // lượt đã xong và đã được trao thưởng
                    }
                    else if (rod.State == RodState.Missed)
                    {
                        StartMiss();
                    }
                    else if (rod.State == RodState.Reeling)
                    {
                        if (_state == Visual.Approaching) HookNow(); // cá chưa tới mồi mà đã giật: bắt kịp
                        UpdateReel(rod);
                    }
                    else if (_state == Visual.Reeling)
                    {
                        UpdateReel(rod); // Biting: giữ tư thế cá cắn
                    }
                    break;

                case Visual.Rare:
                    if (_fish != null)
                        _fish.SetHookedPose(_lure + new Vector3(Random.Range(-0.05f, 0.05f), -0.2f), FacingTowardRod(_lure), 0.9f);
                    break;
            }
        }

        // ---------- Cá thường ----------

        private IEnumerator CastRoutine()
        {
            yield return _rig.CastTo(_lure);
            _anim = null;
            _state = Visual.Floating;
        }

        /// <summary>Cá cắn câu: con cá phù hợp lao tới mồi rồi cắn ("!"). Rác: phao lặn luôn.</summary>
        private void BeginBite(CatchTicket ticket)
        {
            _ticketId = ticket.Id;
            _hooked = false;
            var result = ticket.Result;

            if (result.IsTrash)
            {
                _fish = null;
                _hookStart = _lure + Vector3.down * 0.2f;
                HookNow();
                return;
            }

            _fish = _school.TakeFor(result.fish.rarity, _lure);
            _fish.SetVariant(result.isVariant);
            _state = Visual.Approaching;
            var fish = _fish;
            fish.ApproachLure(_lure + Vector3.down * 0.2f, () =>
            {
                if (_state != Visual.Approaching || _fish != fish) return;
                HookNow();
            });
        }

        /// <summary>Cá đã ngậm mồi: phao lặn, hiện "!", nhân vật phản ứng.</summary>
        private void HookNow()
        {
            _hookStart = _fish != null ? _fish.MouthPosition : _lure + Vector3.down * 0.2f;
            _rig.Bite();
            Fisher?.OnFishBite();
            _state = Visual.Reeling;
        }

        private void UpdateReel(FishingRod rod)
        {
            // Cá cắn: giữ "!" cho tới khi gameplay giật cần (hết BiteReactSeconds).
            if (rod.State == RodState.Biting)
            {
                if (_fish != null)
                    _fish.SetHookedPose(_hookStart + new Vector3(Random.Range(-0.03f, 0.03f), 0f), FacingTowardRod(_hookStart), 0.6f);
                return;
            }
            // Kéo cần: giật một lần rồi kéo theo tiến độ thật của lượt câu.
            if (!_hooked)
            {
                _hooked = true;
                _progressAtHook = rod.Progress;
                Fisher?.OnHookStarted();
            }
            float progress = rod.Progress;

            float u = Mathf.InverseLerp(_progressAtHook, 1f, progress);
            Vector3 mouth = Vector3.Lerp(_hookStart, _shore, u);
            float wobble = 0.15f * (1f - u * 0.6f);
            mouth += new Vector3(Mathf.Sin(Time.time * 5.3f) * wobble, Mathf.Sin(Time.time * 3.1f) * wobble * 0.4f);
            mouth.y = Mathf.Min(mouth.y, _rig.WaterY - 0.12f);
            if (_fish != null) _fish.SetHookedPose(mouth, FacingTowardRod(mouth), 0.45f);
            _rig.SetHooked(mouth, 0.3f + 0.15f * Mathf.Sin(Time.time * 4f));
        }

        private void StartLanding()
        {
            StopAnim();
            _state = Visual.Landing;
            var fish = _fish;
            _fish = null;
            // Lượt câu đã xong (gameplay đã trao thưởng): diễn thu cá.
            if (Fisher != null && !Fisher.OnCatchSucceeded()) Fisher.ResetToIdle();
            _anim = StartCoroutine(LandRoutine(fish));
        }

        /// <summary>Câu hụt: giật cần nhưng cá tuột lưỡi bỏ chạy, nhân vật thất vọng, thu dây rồi câu lại.</summary>
        private void StartMiss()
        {
            StopAnim();
            _state = Visual.Missing;
            var fish = _fish;
            _fish = null;
            _anim = StartCoroutine(MissRoutine(fish));
        }

        private IEnumerator MissRoutine(FishSwimmer fish)
        {
            if (Fisher != null && !Fisher.OnHookStarted() && Fisher.State != FishermanState.HookPull) Fisher.ResetToIdle();
            _rig.Splash(_hookStart, 0.6f);
            yield return new WaitForSeconds(0.25f);
            if (fish != null) fish.Flee(_shore.x);
            Fisher?.OnCatchFailed();
            _rig.ResetToRod();
            _school.Refill(0.8f);
            yield return new WaitForSeconds(0.8f);
            _anim = null;
            _state = Visual.Idle;
        }

        private IEnumerator LandRoutine(FishSwimmer fish)
        {
            _rig.ResetToRod();
            if (fish != null)
            {
                _flyingFish = fish;
                _rig.Splash(fish.transform.position, 0.8f);
                yield return fish.JumpTo(_bucketPos, landingJumpTime);
                Destroy(fish.gameObject);
                _flyingFish = null;
                yield return BounceBucket();
                _school.Refill();
            }
            yield return new WaitForSeconds(0.25f);
            _anim = null;
            _state = Visual.Idle;
        }

        // ---------- Cá hiếm ----------

        private void BeginRare(CatchTicket ticket)
        {
            StopAnim();
            if (_fish != null)
            {
                _fish.Flee(_shore.x);
                _fish = null;
                _school.Refill();
            }

            _ticketId = ticket.Id;
            if (_state != Visual.Floating && _state != Visual.Approaching && _state != Visual.Reeling)
                _lure = new Vector3(Random.Range(_castXRange.x, _castXRange.y), _rig.WaterY, 0f);
            _rig.PlaceFloating(_lure);

            var rarity = ticket.Result.fish != null ? ticket.Result.fish.rarity : Rarity.Rare;
            _fish = _school.TakeFor(rarity, _lure);
            _fish.SetVariant(ticket.Result.isVariant);
            _fish.SetBoss(ticket.Result.fish != null && ticket.Result.fish.isBoss);
            _fish.SetHookedPose(_lure + Vector3.down * 0.2f, FacingTowardRod(_lure), 0.9f);
            _rig.Bite();
            // Giữ tư thế "phát hiện cá cắn" trong suốt minigame.
            Fisher?.ResetToIdle();
            Fisher?.OnFishBite();
            _state = Visual.Rare;
        }

        private void OnResultDismissed() => _resultDismissed = true;

        private void OnRareCompleted(MinigameResult result, FishData fish)
        {
            if (_state != Visual.Rare) return;
            _resultDismissed = false;
            StopAnim();
            _state = Visual.Resolving;
            _anim = StartCoroutine(RareOutcome(result));
        }

        private IEnumerator RareOutcome(MinigameResult result)
        {
            if (result.status != MinigameStatus.Cancelled)
            {
                // Chờ màn hình kết quả đóng (OK hoặc tự đóng) rồi mới diễn kết cục trong cảnh.
                float until = Time.unscaledTime + rareResultMaxWait;
                while (!_resultDismissed && Time.unscaledTime < until) yield return null;
            }

            var fish = _fish;
            if (result.status != MinigameStatus.Cancelled) Fisher?.OnHookStarted();
            if (result.Success && fish != null)
            {
                // Giật cần, kéo nhanh vào bờ rồi nhảy vào thùng.
                Vector3 start = fish.MouthPosition;
                for (float e = 0f; e < rarePullSeconds; e += Time.deltaTime)
                {
                    Vector3 mouth = Vector3.Lerp(start, _shore, e / rarePullSeconds);
                    mouth.y = Mathf.Min(mouth.y, _rig.WaterY - 0.12f);
                    fish.SetHookedPose(mouth, FacingTowardRod(mouth), 0.8f);
                    _rig.SetHooked(mouth, 0.6f);
                    yield return null;
                }
                _anim = null;
                StartLanding();
                yield break;
            }

            // Thua hoặc bị hủy: cá thoát.
            _fish = null;
            if (fish != null) fish.Flee(_shore.x);
            _school.Refill(0.8f);
            if (result.status == MinigameStatus.Lost)
            {
                Fisher?.OnCatchFailed();
                yield return _rig.SnapLine();
                yield return new WaitForSeconds(0.6f);
            }
            else
            {
                Fisher?.ResetToIdle();
            }
            _rig.ResetToRod();
            _anim = null;
            _state = Visual.Idle;
        }

        /// <summary>Dừng câu: bỏ lượt đang diễn, cá đang cắn bơi đi, thu phao về đầu cần, ngư dân đứng chờ.</summary>
        private void EnterStopped()
        {
            _stopped = true;
            StopAnim();
            if (_fish != null)
            {
                _fish.Flee(_shore.x);
                _fish = null;
                _school.Refill();
            }
            _rig.ResetToRod();
            Fisher?.ResetToIdle();
            _state = Visual.Idle;
            _ticketId = -1;
        }

        // ---------- Tiện ích ----------

        private FishermanAnimator Fisher => _rig != null ? _rig.Fisherman : null;

        private float FacingTowardRod(Vector3 mouth) => _rig.RodTip.x < mouth.x ? -1f : 1f;

        private IEnumerator BounceBucket()
        {
            Vector3 baseScale = _bucketScale;
            for (float e = 0f; e < 0.25f; e += Time.deltaTime)
            {
                float k = 1f + Mathf.Sin(e / 0.25f * Mathf.PI) * 0.15f;
                _bucket.localScale = new Vector3(baseScale.x * k, baseScale.y / k, 1f);
                yield return null;
            }
            _bucket.localScale = baseScale;
        }

        private void StopAnim()
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = null;
            // Cá đang bay vào thùng mà bị ngắt: bỏ luôn, không để lơ lửng.
            if (_flyingFish != null) Destroy(_flyingFish.gameObject);
            _flyingFish = null;
            if (_bucket != null) _bucket.localScale = _bucketScale;
        }
    }
}
