using LazyAngler.Systems;
using LazyAngler.Utils;
using TMPro;
using UnityEngine;

namespace LazyAngler.UI
{
    /// <summary>Popup "Khi bạn vắng mặt" (args: OfflineReport). Xu đã được cộng sẵn, nút Nhận chỉ đóng popup.</summary>
    public class AfkEarnPopup : UIPanel
    {
        [SerializeField] private TMP_Text bodyText;

        protected override void OnOpen(object args)
        {
            if (!(args is OfflineReport r)) return;
            string capped = r.countedSeconds < r.awaySeconds ? " <size=75%>(Két đầy)</size>" : "";
            bodyText.text =
                $"Bạn đã vắng {Fmt.Duration(r.awaySeconds)}{capped}\n\n" +
                $"Cần câu đã bắt khoảng <b>{Fmt.Number(r.fish)}</b> con cá\n" +
                $"và kiếm được <b>{Fmt.Number(r.coins)} xu</b>";
        }
    }
}
