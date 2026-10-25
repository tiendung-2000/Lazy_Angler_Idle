using System.Collections.Generic;
using LazyAngler.Core;
using LazyAngler.Data;
using UnityEditor;
using UnityEngine;

namespace LazyAngler.Tools
{
    /// <summary>
    /// 6 bản đồ (Ao nhà → Suối → Sông → Bờ biển → Đảo → Biển sâu) và cá đặc trưng của từng map.
    /// Map sau khó hơn (cá cắn ít, hụt nhiều, kéo lâu), cá đắt hơn và đòi cần câu + nâng cấp cao hơn.
    /// Asset đã có thì giữ nguyên số liệu đã chỉnh trong Inspector (chỉ tạo mới khi thiếu).
    /// </summary>
    public static partial class PrototypeBuilder
    {
        private const string ZoneFolder = "Assets/_Project/ScriptableObjects/Zones";

        private struct ZoneSpec
        {
            public string id, name, desc;
            public int rod, bite, bait;
            public float biteMul, missBonus, reelMul, waitMul;
            public Color skyTop, skyBottom, waterTop, waterBottom, seabed;
            public bool island;
        }

        private static readonly ZoneSpec[] ZoneSpecs =
        {
            new ZoneSpec { id = "ao_nha", name = "Ao nhà", desc = "Ao sau vườn, cá hiền dễ câu.",
                biteMul = 1f, missBonus = 0f, reelMul = 1f, waitMul = 1f,
                skyTop = C(0.45f, 0.72f, 0.95f), skyBottom = C(0.72f, 0.88f, 0.98f),
                waterTop = C(0.32f, 0.62f, 0.45f), waterBottom = C(0.1f, 0.28f, 0.2f), seabed = C(0.45f, 0.38f, 0.25f) },
            new ZoneSpec { id = "suoi", name = "Suối", desc = "Nước trong, chảy xiết, cá nhanh nhẹn.",
                rod = 1, biteMul = 0.9f, missBonus = 0.03f, reelMul = 1.1f, waitMul = 1.1f,
                skyTop = C(0.4f, 0.7f, 0.95f), skyBottom = C(0.75f, 0.9f, 0.95f),
                waterTop = C(0.38f, 0.78f, 0.82f), waterBottom = C(0.12f, 0.4f, 0.45f), seabed = C(0.6f, 0.6f, 0.55f) },
            new ZoneSpec { id = "song", name = "Sông", desc = "Sông lớn phù sa, cá to và khỏe.",
                rod = 2, bite = 3, biteMul = 0.8f, missBonus = 0.06f, reelMul = 1.2f, waitMul = 1.2f,
                skyTop = C(0.55f, 0.7f, 0.85f), skyBottom = C(0.82f, 0.86f, 0.9f),
                waterTop = C(0.55f, 0.6f, 0.42f), waterBottom = C(0.22f, 0.25f, 0.18f), seabed = C(0.5f, 0.42f, 0.3f) },
            new ZoneSpec { id = "bo_bien", name = "Bờ biển", desc = "Sóng vỗ bờ cát, cá biển tinh ranh.",
                rod = 2, bite = 5, bait = 3, biteMul = 0.7f, missBonus = 0.09f, reelMul = 1.35f, waitMul = 1.3f,
                skyTop = C(0.4f, 0.7f, 1f), skyBottom = C(0.75f, 0.9f, 1f),
                waterTop = C(0.2f, 0.6f, 0.85f), waterBottom = C(0.05f, 0.25f, 0.45f), seabed = C(0.85f, 0.75f, 0.5f), island = true },
            new ZoneSpec { id = "dao", name = "Đảo", desc = "Rạn san hô quanh đảo, cá rực rỡ khó bắt.",
                rod = 3, bite = 7, bait = 5, biteMul = 0.6f, missBonus = 0.12f, reelMul = 1.5f, waitMul = 1.4f,
                skyTop = C(0.95f, 0.6f, 0.45f), skyBottom = C(1f, 0.85f, 0.6f),
                waterTop = C(0.2f, 0.75f, 0.8f), waterBottom = C(0.05f, 0.3f, 0.45f), seabed = C(0.9f, 0.82f, 0.6f), island = true },
            new ZoneSpec { id = "bien_sau", name = "Biển sâu", desc = "Vực tối không đáy, quái ngư ẩn mình.",
                rod = 4, bite = 9, bait = 8, biteMul = 0.5f, missBonus = 0.15f, reelMul = 1.7f, waitMul = 1.6f,
                skyTop = C(0.1f, 0.15f, 0.3f), skyBottom = C(0.25f, 0.35f, 0.55f),
                waterTop = C(0.06f, 0.2f, 0.4f), waterBottom = C(0f, 0.03f, 0.1f), seabed = C(0.2f, 0.2f, 0.25f) },
        };

        // Cá của từng map (map 0 dùng 5 loài có sẵn của Prototype). Giá tăng ~3 lần mỗi map.
        private static readonly (int zone, string id, string name, Rarity rarity, int price, float min, float max)[] ZoneFishSpecs =
        {
            (1, "chach", "Cá Chạch", Rarity.Common, 6, 10, 25),
            (1, "bong", "Cá Bống", Rarity.Common, 7, 5, 15),
            (1, "muong", "Cá Mương", Rarity.Uncommon, 15, 10, 25),
            (1, "chinh_suoi", "Cá Chình Suối", Rarity.Rare, 45, 40, 90),
            (1, "hoi_suoi", "Cá Hồi Suối", Rarity.Epic, 150, 30, 70),
            (1, "anh_vu", "Cá Anh Vũ", Rarity.Legendary, 600, 30, 60),

            (2, "me", "Cá Mè", Rarity.Common, 16, 20, 50),
            (2, "basa", "Cá Basa", Rarity.Common, 18, 30, 70),
            (2, "bong_lau", "Cá Bông Lau", Rarity.Uncommon, 40, 30, 80),
            (2, "lang", "Cá Lăng", Rarity.Rare, 120, 40, 110),
            (2, "chien", "Cá Chiên", Rarity.Epic, 400, 50, 120),
            (2, "ho", "Cá Hô", Rarity.Legendary, 1600, 100, 250),

            (3, "doi", "Cá Đối", Rarity.Common, 40, 20, 45),
            (3, "nuc", "Cá Nục", Rarity.Common, 45, 15, 30),
            (3, "hong", "Cá Hồng", Rarity.Uncommon, 100, 25, 60),
            (3, "mu", "Cá Mú", Rarity.Rare, 300, 40, 100),
            (3, "chem", "Cá Chẽm", Rarity.Epic, 1000, 50, 120),
            (3, "song_vua", "Cá Song Vua", Rarity.Legendary, 4000, 120, 250),

            (4, "bo_da", "Cá Bò Da", Rarity.Common, 100, 20, 50),
            (4, "he", "Cá Hề", Rarity.Common, 110, 6, 12),
            (4, "buom", "Cá Bướm", Rarity.Uncommon, 250, 10, 25),
            (4, "ngu_vay_vang", "Cá Ngừ Vây Vàng", Rarity.Rare, 750, 80, 200),
            (4, "napoleon", "Cá Napoleon", Rarity.Epic, 2500, 100, 230),
            (4, "duoi_o", "Cá Đuối Ó", Rarity.Legendary, 10000, 150, 350),

            (5, "den_long", "Cá Đèn Lồng", Rarity.Common, 240, 10, 30),
            (5, "rang_nanh", "Cá Răng Nanh", Rarity.Common, 260, 10, 20),
            (5, "mat_quy", "Cá Mặt Quỷ", Rarity.Uncommon, 600, 20, 60),
            (5, "map_yeu_tinh", "Cá Mập Yêu Tinh", Rarity.Rare, 1800, 250, 400),
            (5, "muc_khong_lo", "Mực Khổng Lồ", Rarity.Epic, 6000, 500, 1200),
            (5, "vay_tay", "Cá Vây Tay", Rarity.Legendary, 24000, 120, 200),
        };

        // Cá sự kiện: mỗi map một loài chỉ có ban đêm (Hiếm, có minigame) và một loài chỉ có khi mưa (Khá).
        // Không bắt buộc để mở khóa map kế tiếp; hoàn thành đủ thì nhận thưởng ngọc trai riêng.
        private static readonly (int zone, string id, string name, Rarity rarity, int price, float min, float max, FishCondition condition)[] EventFishSpecs =
        {
            (0, "dem_luon", "Lươn Đêm", Rarity.Rare, 24, 30, 80, FishCondition.Night),
            (0, "mua_sac", "Cá Sặc Mưa", Rarity.Uncommon, 8, 8, 18, FishCondition.Rain),
            (1, "dem_chach", "Cá Chạch Trăng", Rarity.Rare, 72, 25, 60, FishCondition.Night),
            (1, "mua_suoi", "Cá Suối Mưa", Rarity.Uncommon, 24, 10, 22, FishCondition.Rain),
            (2, "dem_lang", "Cá Lăng Đêm", Rarity.Rare, 190, 40, 100, FishCondition.Night),
            (2, "mua_tre", "Cá Trê Mưa", Rarity.Uncommon, 64, 25, 60, FishCondition.Rain),
            (3, "dem_mu", "Cá Mú Đêm", Rarity.Rare, 480, 40, 100, FishCondition.Night),
            (3, "mua_nhai", "Cá Nhái Mưa", Rarity.Uncommon, 160, 20, 50, FishCondition.Rain),
            (4, "dem_he", "Cá Hề Đêm", Rarity.Rare, 1200, 15, 40, FishCondition.Night),
            (4, "mua_buom", "Cá Bướm Mưa", Rarity.Uncommon, 400, 10, 25, FishCondition.Rain),
            (5, "dem_den", "Cá Đèn Vực Đêm", Rarity.Rare, 2900, 60, 150, FishCondition.Night),
            (5, "mua_vay", "Cá Vây Mưa", Rarity.Uncommon, 960, 20, 60, FishCondition.Rain),
        };

        // Cá boss: mỗi map một con khổng lồ, rất hiếm, minigame thanh ngang khó dần theo map. Giá bán rất cao.
        private static readonly (int zone, string id, string name, int price, float min, float max)[] BossSpecs =
        {
            (0, "boss_loc", "Cá Lóc Chúa", 300, 80, 150),
            (1, "boss_hoi", "Hồi Vương Suối", 1000, 100, 180),
            (2, "boss_ho", "Hà Bá Sông", 3500, 200, 400),
            (3, "boss_nuc", "Hải Thần Bờ Biển", 11000, 200, 400),
            (4, "boss_ray", "Quỷ Đuối Rạn San Hô", 33000, 300, 500),
            (5, "boss_muc", "Thủy Quái Vực Sâu", 100000, 600, 1500),
        };

        // Độ khó minigame boss theo map: (tốc độ nhỏ nhất, lớn nhất, độ rộng vùng xanh %, số lần trúng cần, số lượt tối đa).
        private static readonly (float speedMin, float speedMax, float width, int required, int attempts)[] BossDifficulty =
        {
            (45f, 70f, 16f, 3, 6),
            (55f, 85f, 14f, 3, 6),
            (65f, 100f, 12f, 4, 7),
            (75f, 115f, 10f, 4, 7),
            (85f, 130f, 9f, 5, 8),
            (95f, 145f, 8f, 5, 8),
        };

        private static RareMinigameSettings BossMinigame(int zone)
        {
            var d = BossDifficulty[Mathf.Clamp(zone, 0, BossDifficulty.Length - 1)];
            return new RareMinigameSettings
            {
                type = MinigameType.Boss,
                requiredSuccesses = d.required,
                maxAttempts = d.attempts,
                targetAngleWidth = d.width,
                randomizeTarget = true,
                bossSpeedMin = d.speedMin,
                bossSpeedMax = d.speedMax,
                bossMinLeg = 25f,
                bossEndBias = 0.4f,
                bossBurstChance = 0.2f,
                bossSpeedGrowthPerHit = 0.10f,
                bossWidthShrinkPerHit = 1.2f,
                bossMinWidth = 4f,
            };
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b);

        /// <summary>Tạo map + cá theo map, gán vào GameConfig (zones, fish). Cá Prototype có sẵn giữ ở map 0.</summary>
        public static void BuildZones()
        {
            EnsureFolder(ZoneFolder);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null) return;

            var zones = new List<ZoneData>();
            foreach (var spec in ZoneSpecs) zones.Add(Zone(spec, zones.Count));
            config.zones = zones;

            var fish = new List<FishData>();
            foreach (var f in config.fish)
                if (f != null && f.zoneIndex == 0 && !f.IsEventFish && !f.isBoss) fish.Add(f); // cá sự kiện/boss map 0 được thêm lại bên dưới
            var created = new List<FishData>();
            foreach (var s in ZoneFishSpecs)
            {
                var f = Fish(s.id, s.name, s.rarity, s.price, s.min, s.max, s.zone);
                fish.Add(f);
                created.Add(f);
            }
            foreach (var s in EventFishSpecs)
            {
                var f = Fish(s.id, s.name, s.rarity, s.price, s.min, s.max, s.zone);
                if (f.requiredCondition != s.condition)
                {
                    f.requiredCondition = s.condition;
                    EditorUtility.SetDirty(f);
                }
                fish.Add(f);
                created.Add(f);
            }
            foreach (var s in BossSpecs)
            {
                var f = Fish(s.id, s.name, Rarity.Legendary, s.price, s.min, s.max, s.zone);
                bool changed = false;
                if (!f.isBoss) { f.isBoss = true; changed = true; }
                if (f.minigame == null || f.minigame.type != MinigameType.Boss)
                {
                    f.minigame = BossMinigame(s.zone);
                    f.radialConfig = null; // preset Radial sẽ đè cấu hình Boss nếu còn
                    changed = true;
                }
                if (changed) EditorUtility.SetDirty(f);
                fish.Add(f);
            }
            ApplyZoneMinigames(created);
            config.fish = fish;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            BuildMapPrefabs(zones);
        }

        private static ZoneData Zone(ZoneSpec s, int index)
        {
            string path = $"{ZoneFolder}/Zone_{index}_{s.id}.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<ZoneData>(path) == null;
            var z = LoadOrCreate<ZoneData>(path);
            if (!isNew) return z;
            z.zoneId = s.id;
            z.displayName = s.name;
            z.description = s.desc;
            z.requiredRodTier = s.rod;
            z.requiredBiteLevel = s.bite;
            z.requiredBaitLevel = s.bait;
            z.biteChanceMultiplier = s.biteMul;
            z.missChanceBonus = s.missBonus;
            z.reelDurationMultiplier = s.reelMul;
            z.biteWaitMultiplier = s.waitMul;
            EditorUtility.SetDirty(z);
            return z;
        }

        /// <summary>
        /// Minigame cá hiếm khó dần theo map: Hiếm dùng Radial Dễ → Trung bình → Khó; Sử thi lắc kim (2 map đầu)
        /// rồi Radial Trung bình/Khó; Huyền thoại luôn Radial Khó. Cá đã cấu hình trong Inspector giữ nguyên.
        /// </summary>
        private static void ApplyZoneMinigames(IEnumerable<FishData> fish)
        {
            ApplyDefaultMinigames(new FishData[0]); // đảm bảo 3 preset Radial đã tồn tại
            var easy = AssetDatabase.LoadAssetAtPath<RadialFishingConfig>($"{MinigameFolder}/Radial_Easy.asset");
            var medium = AssetDatabase.LoadAssetAtPath<RadialFishingConfig>($"{MinigameFolder}/Radial_Medium.asset");
            var hard = AssetDatabase.LoadAssetAtPath<RadialFishingConfig>($"{MinigameFolder}/Radial_Hard.asset");

            foreach (var f in fish)
            {
                if (f == null || f.rarity < Rarity.Rare) continue;
                if (f.minigame != null && f.minigame.type != MinigameType.None) continue;
                int z = f.zoneIndex;
                if (f.rarity == Rarity.Epic && z <= 1)
                {
                    f.minigame = DefaultMinigame(Rarity.Epic); // Pendulum
                }
                else
                {
                    f.minigame = new RareMinigameSettings { type = MinigameType.Radial };
                    f.radialConfig = f.rarity == Rarity.Legendary || z >= 4 ? hard
                        : f.rarity == Rarity.Epic || z >= 2 ? medium
                        : easy;
                }
                EditorUtility.SetDirty(f);
            }
        }
    }
}
