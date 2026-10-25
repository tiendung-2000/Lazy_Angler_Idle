using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LazyAngler.Systems
{
    [Serializable]
    public class FishStack
    {
        public string fishId;
        public int count;
    }

    /// <summary>Một con cá đang trưng bày trong bể (loài + dạng thường hay biến thể).</summary>
    [Serializable]
    public class AquariumEntry
    {
        public string fishId;
        public bool variant;
    }

    /// <summary>Kỷ lục theo loài: cá to nhất từng câu và số con biến thể đã bắt.</summary>
    [Serializable]
    public class FishRecord
    {
        public string fishId;
        public float maxSize;
        public int variants;
    }

    /// <summary>Tiến độ một nhiệm vụ (ngày hoặc tuần hoàn).</summary>
    [Serializable]
    public class QuestProgress
    {
        public string questId;
        public long progress;
        public long target;
        /// <summary>Nhiệm vụ tuần hoàn: số vòng đã hoàn thành (quyết định chỉ tiêu và thưởng vòng này).</summary>
        public int cycle;
        /// <summary>Nhiệm vụ ngày: đã nhận thưởng.</summary>
        public bool claimed;

        public bool IsComplete => progress >= target;
    }

    [Serializable]
    public class PearlLevel
    {
        public string id;
        public int level;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public long coins;
        public long gems;
        public int rodLevel;
        public int baitLevel;
        public int biteLevel;
        public int boatLevel = 1;
        public int vaultLevel = 1;
        public int valueLevel;
        public int luckLevel;
        /// <summary>Cũ (đã bỏ giỏ cá): chỉ đọc khi tải để quy cá còn trong giỏ ra xu, không còn ghi.</summary>
        public List<FishStack> basket;
        /// <summary>Tổng số đã bắt theo loài (nền cho Bách khoa cá ở Alpha).</summary>
        public List<FishStack> caughtTotals = new List<FishStack>();
        public List<string> ownedSkins = new List<string>();
        public List<FishRecord> records = new List<FishRecord>();
        public List<AquariumEntry> aquarium = new List<AquariumEntry>();
        /// <summary>Số ô bể cá đã mở thêm ngoài số ô ban đầu.</summary>
        public int aquariumExtraSlots;
        /// <summary>Số mồi tiêu hao còn trong kho (fishId = mã mồi).</summary>
        public List<FishStack> baitStock = new List<FishStack>();
        /// <summary>Mồi đang dùng (rỗng nếu không) và số giây hiệu lực còn lại.</summary>
        public string activeBaitId;
        public float activeBaitRemaining;
        /// <summary>Ngày (yyyyMMdd) của bộ nhiệm vụ ngày đang lưu.</summary>
        public string questDate;
        public List<QuestProgress> dailyQuests = new List<QuestProgress>();
        public List<QuestProgress> repeatingQuests = new List<QuestProgress>();
        /// <summary>Mã các mốc bộ sưu tập đã nhận thưởng ("r:map:độ hiếm", "v:map").</summary>
        public List<string> claimedRewards = new List<string>();
        public string equippedSkin;
        /// <summary>Bản đồ đang câu (chỉ số trong GameConfig.zones).</summary>
        public int currentZone;
        /// <summary>Ngọc trai chưa tiêu (giữ vĩnh viễn).</summary>
        public int prestigePearls;
        /// <summary>Cấp đã mua của từng nâng cấp ngọc trai.</summary>
        public List<PearlLevel> pearlLevels = new List<PearlLevel>();
        public int prestigeCount;
        /// <summary>Xu đã kiếm từ lần tái sinh gần nhất (quyết định số ngọc trai nhận được).</summary>
        public long runEarned;
        /// <summary>DateTime.UtcNow.Ticks lúc lưu.</summary>
        public long lastSaveUtc;
    }

    /// <summary>
    /// Lưu/tải JSON trong persistentDataPath. Ghi qua file tạm rồi thay thế
    /// để app bị kill giữa chừng không làm hỏng save cũ.
    /// </summary>
    public static class SaveSystem
    {
        private const string FileName = "save.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(SaveData data)
        {
            data.lastSaveUtc = DateTime.UtcNow.Ticks;
            string json = JsonUtility.ToJson(data);
            string tmp = SavePath + ".tmp";
            try
            {
                File.WriteAllText(tmp, json);
                if (File.Exists(SavePath)) File.Replace(tmp, SavePath, SavePath + ".bak");
                else File.Move(tmp, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Lưu thất bại: {e}");
            }
        }

        /// <summary>Trả về null nếu chưa có save hoặc save hỏng (thử bản .bak trước).</summary>
        public static SaveData Load()
        {
            return TryRead(SavePath) ?? TryRead(SavePath + ".bak");
        }

        public static void Delete()
        {
            foreach (var p in new[] { SavePath, SavePath + ".bak", SavePath + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }

        private static SaveData TryRead(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                return data != null && data.version > 0 ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Không đọc được {path}: {e.Message}");
                return null;
            }
        }
    }
}
