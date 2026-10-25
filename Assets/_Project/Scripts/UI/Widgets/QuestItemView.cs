using LazyAngler.Systems;
using LazyAngler.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LazyAngler.UI
{
    /// <summary>Một dòng nhiệm vụ: tên (kèm vòng nếu là nhiệm vụ tuần hoàn), thưởng, thanh tiến độ và nút Nhận.</summary>
    public class QuestItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image progressFill;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text claimButtonText;

        private QuestProgress _progress;

        private void Awake() => claimButton.onClick.AddListener(() =>
        {
            if (_progress != null && GameManager.Instance.ClaimQuest(_progress)) Sfx.Play(SfxId.Reward);
        });

        public void Set(GameManager gm, QuestProgress p, bool repeating)
        {
            _progress = p;
            var quest = gm.Quests.Find(p.questId);
            if (quest == null)
            {
                gameObject.SetActive(false);
                return;
            }
            string cycle = repeating ? $"  <size=65%><color=#8A93A6>Vòng {p.cycle + 1}</color></size>" : "";
            titleText.text = quest.TitleFor(p.target) + cycle;
            rewardText.text = $"Thưởng: {gm.QuestRewardText(quest, p.cycle)}";
            long shown = System.Math.Min(p.progress, p.target);
            progressText.text = $"{Fmt.Number(shown)}/{Fmt.Number(p.target)}";
            progressFill.fillAmount = p.target > 0 ? Mathf.Clamp01(p.progress / (float)p.target) : 1f;

            bool canClaim = gm.Quests.CanClaim(p);
            claimButton.interactable = canClaim;
            claimButtonText.text = p.claimed ? "Đã nhận" : canClaim ? "Nhận" : "Chưa xong";
        }
    }
}
