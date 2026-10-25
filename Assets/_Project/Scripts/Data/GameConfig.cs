using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace LazyAngler.Data
{
    /// <summary>
    /// Cấu hình cân bằng cho Prototype. Mọi con số có thể chỉnh trong Inspector,
    /// code chỉ đọc từ đây (không hard-code giá trị cân bằng).
    /// </summary>
    [CreateAssetMenu(menuName = "LazyAngler/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Cá")]
        public List<FishData> fish = new List<FishData>();

        [Header("Bản đồ")]
        [Tooltip("Theo thứ tự mở khóa. Cá thuộc map có FishData.zoneIndex = vị trí trong danh sách.")]
        public List<ZoneData> zones = new List<ZoneData>();

        [Tooltip("Trọng số cơ sở theo độ hiếm (Common, Uncommon, Rare, Epic, Legendary). Tổng nhóm cá hiếm (Rare+) do " +
                 "Tỉ lệ cá hiếm quyết định; trọng số chỉ chia tỉ lệ trong từng nhóm.")]
        public float[] rarityWeights = { 60f, 25f, 10f, 4f, 1f };

        [Header("Rác")]
        [Range(0f, 0.5f)] public float trashChance = 0.05f;
        public int trashPrice = 1;

        [Header("Tốc độ câu (Fishing Speed)")]
        [Tooltip("Thời gian kéo một con cá ở cấp 0 (giây/lượt). effectiveDuration = baseFishingDuration / fishingSpeedMultiplier.")]
        [FormerlySerializedAs("baseCastSeconds")]
        public float baseFishingDuration = 5f;
        [Tooltip("Mỗi cấp cần câu nhân thời gian lượt với hệ số này (tương đương hệ số tốc độ 1/hệ số^cấp).")]
        [Range(0.5f, 1f)] public float castReductionPerLevel = 0.97f;
        [Tooltip("Thời gian lượt không bao giờ thấp hơn giá trị này.")]
        public float minCastSeconds = 1f;

        [Header("Tỉ lệ cá cắn (Bite Chance)")]
        [Tooltip("Xác suất cá cắn mỗi lần kiểm tra ở cấp 0 (0..1).")]
        [Range(0f, 1f)] public float baseBiteChance = 0.5f;
        [Tooltip("Cộng thêm mỗi cấp nâng cấp Tỉ lệ cá cắn.")]
        [Range(0f, 0.2f)] public float biteChancePerLevel = 0.03f;
        [Tooltip("Trần tỉ lệ cá cắn của người chơi (cần câu + nâng cấp), trước khi nhân độ khó của map.")]
        [Range(0f, 1f)] public float maxBiteChance = 0.5f;
        [Tooltip("Khoảng thời gian giữa hai lần kiểm tra cá cắn (giây).")]
        public float biteCheckInterval = 1f;

        [Header("Nhịp câu: Câu → Đợi cá → Cá cắn → Kéo cần")]
        [Tooltip("Thời gian quăng câu trước khi bắt đầu đợi cá (giây). Nên khớp animation quăng.")]
        public float castSeconds = 1.8f;
        [Tooltip("Thời gian đợi tối thiểu trước khi cá có thể cắn, ở tốc độ cá cắn ×1 (giây).")]
        public float baseBiteWaitSeconds = 4f;
        [Tooltip("Độ dao động ngẫu nhiên của thời gian đợi (0,3 = ±30%).")]
        [Range(0f, 0.9f)] public float biteWaitRandomness = 0.3f;
        [Tooltip("Mỗi cấp Mồi câu tăng tốc độ cá cắn câu thêm bấy nhiêu (0,05 = +5%).")]
        public float baitBiteSpeedPerLevel = 0.04f;
        [Tooltip("Trần hệ số tốc độ cá cắn (cần câu × mồi câu). Chạm trần thì Mồi câu báo Tối đa.")]
        [Min(1f)] public float maxBiteSpeedMultiplier = 2.5f;
        [Tooltip("Thời gian từ lúc cá cắn đến lúc giật cần (giây).")]
        public float biteReactSeconds = 0.6f;

        [Header("Câu hụt")]
        [Tooltip("Tỉ lệ câu hụt khi chưa cấu hình cấp cần câu.")]
        [Range(0f, 1f)] public float baseMissChance = 0.25f;
        [Tooltip("Mỗi cấp Mồi câu hoặc Tỉ lệ cá cắn giảm tỉ lệ câu hụt bấy nhiêu (0,01 = 1%).")]
        [Range(0f, 0.1f)] public float missReductionPerUpgradeLevel = 0.01f;
        [Tooltip("Sàn tỉ lệ câu hụt (trước khi cộng độ khó của map).")]
        [Range(0f, 1f)] public float minMissChance = 0.05f;
        [Tooltip("Thời gian thu dây sau khi câu hụt rồi mới quăng lại (giây).")]
        public float missRecoverSeconds = 1.2f;

        [Header("Cá hiếm")]
        [Tooltip("Không tạo cá hiếm nếu người chơi không chạm/nhấn gì trong khoảng này (giây). 0 = luôn cho phép khi app đang mở.")]
        public float rareIdleTimeout = 120f;
        [Tooltip("Thua minigame thì vẫn thưởng một con cá thường.")]
        public bool rewardNormalFishOnRareFail = false;
        [Tooltip("Tỉ lệ cá hiếm (Hiếm, Sử thi, Huyền thoại) mỗi lần cá cắn khi chưa nâng May mắn.")]
        [Range(0f, 1f)] public float baseRareChance = 0.08f;
        [Tooltip("Mỗi cấp May mắn cộng thêm vào tỉ lệ cá hiếm.")]
        [Range(0f, 0.1f)] public float rareChancePerLevel = 0.01f;
        [Tooltip("Trần tỉ lệ cá hiếm.")]
        [Range(0f, 1f)] public float maxRareChance = 0.3f;

        [Header("Cấp cần câu")]
        [Tooltip("Các cấp cần câu theo thứ tự. Có dữ liệu thì Fishing Speed và Bite Chance cơ sở lấy từ đây " +
                 "(thay cho castReductionPerLevel/baseBiteChance) và nâng cấp Cần câu dừng ở cấp cuối.")]
        public List<RodTierData> rodTiers = new List<RodTierData>();

        [Header("Trang phục (Cửa hàng)")]
        [Tooltip("Skin đầu tiên là mặc định khi chưa chọn.")]
        public List<SkinData> skins = new List<SkinData>();

        [Header("Nâng cấp")]
        public UpgradeDefinition rodUpgrade;
        public UpgradeDefinition baitUpgrade;
        [Tooltip("Thuyền: mỗi cấp thêm một cần câu chạy song song (tối đa EconomyCalc.MaxRods).")]
        public UpgradeDefinition boatUpgrade;
        [Tooltip("Két: tăng thời gian tối đa được tính thu nhập offline.")]
        public UpgradeDefinition vaultUpgrade;
        [Tooltip("Giá trị cá: tăng số xu nhận được mỗi con.")]
        public UpgradeDefinition fishValueUpgrade;
        [Tooltip("May mắn: tăng tỉ lệ cá hiếm (có trần maxRareChance).")]
        public UpgradeDefinition luckUpgrade;
        [Tooltip("Mỗi cấp Giá trị cá cộng thêm bấy nhiêu vào hệ số giá (0,1 = +10%).")]
        public float fishValuePerLevel = 0.1f;
        public UpgradeDefinition biteUpgrade;

        [Header("Cá boss")]
        [Tooltip("Xác suất mỗi lần cá cắn (khi người chơi đang online) là cá boss của map. 0,004 ≈ 1 lần/250 lần cắn.")]
        [Range(0f, 0.2f)] public float bossChancePerBite = 0.004f;
        [Tooltip("Ngọc trai nhận mỗi lần bắt được boss, nhân với (chỉ số map + 1).")]
        public int bossPearlsPerCatch = 3;
        [Tooltip("Ngọc trai thưởng thêm cho lần đầu bắt boss của map, nhân với (chỉ số map + 1).")]
        public int bossFirstCatchPearls = 10;

        [Header("Thời gian & thời tiết (giờ máy)")]
        [Tooltip("Từ giờ này là hoàng hôn (0–23).")]
        [Range(0, 23)] public int sunsetStartHour = 17;
        [Tooltip("Từ giờ này là ban đêm.")]
        [Range(0, 23)] public int nightStartHour = 19;
        [Tooltip("Đến giờ này thì hết đêm (sáng).")]
        [Range(0, 23)] public int nightEndHour = 5;
        [Tooltip("Số đợt mưa mỗi ngày (giờ bắt đầu ngẫu nhiên, cố định theo ngày). 0 = không mưa.")]
        [Min(0)] public int rainWindowsPerDay = 2;
        [Tooltip("Mỗi đợt mưa kéo dài ngẫu nhiên trong khoảng này (phút).")]
        public Vector2 rainMinutes = new Vector2(30f, 60f);
        [Tooltip("Hoàng hôn: giá cá cộng thêm (0,25 = +25%).")]
        public float sunsetValueBonus = 0.25f;
        [Tooltip("Ban đêm: cộng thêm vào tỉ lệ cá hiếm (0,05 = +5 điểm %).")]
        public float nightRareBonus = 0.05f;
        [Tooltip("Trời mưa: cá cắn nhanh hơn (0,25 = đợi ngắn đi 1/1,25).")]
        public float rainBiteSpeedBonus = 0.25f;
        [Tooltip("Khi điều kiện đang diễn ra, cá sự kiện được chọn với trọng số này (cá thường = 1).")]
        [Min(1f)] public float eventFishWeight = 3f;
        [Tooltip("Ngọc trai thưởng khi bắt đủ cá sự kiện của một map, nhân với (chỉ số map + 1).")]
        public int eventSetPearls = 4;

        [Header("Bể cá trưng bày")]
        [Tooltip("Số ô bể cá ban đầu.")]
        [Min(1)] public int aquariumBaseSlots = 3;
        [Tooltip("Giá (ngọc trai) mở từng ô thêm theo thứ tự; số phần tử = số ô có thể mở thêm.")]
        public int[] aquariumSlotPearlCosts = { 2, 3, 5, 8, 12, 18, 25 };
        [Tooltip("Cá thường (không biến thể) chỉ được đặt vào bể nếu độ hiếm từ mức này trở lên. Cá biến thể đặt được mọi độ hiếm.")]
        public Rarity aquariumMinRarity = Rarity.Rare;
        [Tooltip("Cộng thêm vào giá cá cho mỗi con trong bể, theo độ hiếm (Common, Uncommon, Rare, Epic, Legendary). 0,02 = +2%.")]
        public float[] aquariumRarityBonus = { 0.005f, 0.01f, 0.02f, 0.04f, 0.08f };
        [Tooltip("Cá biến thể trong bể được bonus nhân hệ số này.")]
        [Min(1f)] public float aquariumVariantMultiplier = 2f;

        [Header("Mồi tiêu hao (theo map)")]
        [Tooltip("Mồi dùng một lần, mỗi mồi thuộc một map (BaitData.zoneIndex).")]
        public List<BaitData> baits = new List<BaitData>();

        [Header("Nhiệm vụ")]
        [Tooltip("Mỗi ngày rút ngẫu nhiên dailyQuestCount nhiệm vụ khác nhau từ danh sách này.")]
        public List<QuestData> dailyQuestPool = new List<QuestData>();
        [Min(1)] public int dailyQuestCount = 3;
        [Tooltip("Nhiệm vụ tuần hoàn: luôn có mặt; nhận thưởng xong thì lặp lại với chỉ tiêu và thưởng cao hơn.")]
        public List<QuestData> repeatingQuests = new List<QuestData>();

        [Header("Bộ sưu tập: biến thể & thưởng")]
        [Tooltip("Xác suất một con cá bắt được là biến thể (màu khác).")]
        [Range(0f, 0.5f)] public float variantChance = 0.03f;
        [Tooltip("Cá biến thể bán được giá gốc nhân hệ số này.")]
        [Min(1f)] public float variantPriceMultiplier = 3f;
        [Tooltip("Ngọc trai thưởng khi bắt đủ mọi loài của một độ hiếm trong một map (Common, Uncommon, Rare, Epic, Legendary). " +
                 "Nhân với (chỉ số map + 1). Mỗi mốc chỉ nhận một lần.")]
        public int[] rarityCompletionPearls = { 1, 1, 2, 3, 5 };
        [Tooltip("Ngọc trai thưởng khi bắt được biến thể của mọi loài trong một map. Nhân với (chỉ số map + 1).")]
        public int variantCompletionPearls = 5;

        [Header("Tái sinh (Prestige)")]
        [Tooltip("Ngọc trai nhận được = căn bậc hai của (xu kiếm được lượt này / số này). Càng lớn càng khó có ngọc trai.")]
        [Min(1f)] public float prestigeCoinDivisor = 10000f;
        [Tooltip("Nâng cấp mua bằng ngọc trai (Cửa hàng ngọc trai), giữ vĩnh viễn qua tái sinh.")]
        public List<PearlUpgradeData> pearlUpgrades = new List<PearlUpgradeData>();

        [Header("Khởi đầu")]
        public long startingCoins = 0;
        public int startingBoatLevel = 1;
        public int startingVaultLevel = 1;

        [Tooltip("Phiên bản bộ số cân bằng mà builder đã áp; tăng khi đổi số mặc định để asset cũ được cập nhật một lần.")]
        [HideInInspector] public int balanceVersion;
    }
}
