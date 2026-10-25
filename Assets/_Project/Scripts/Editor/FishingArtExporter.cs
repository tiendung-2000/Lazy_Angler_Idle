using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LazyAngler.Data;
using LazyAngler.Visual;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace LazyAngler.Tools
{
    /// <summary>
    /// Menu: Tools > Lazy Angler > Export Fishing Pixel Art.
    /// Xuất pixel art của FishingPixelArt ra PNG (Art/Sprites/Fishing), import dạng sprite Multiple
    /// (Point, PPU 32, không mipmap, không nén, FullRect), cắt ô với spriteID cố định theo tên
    /// (chạy lại không làm mất tham chiếu), rồi tạo/cập nhật FishingArtSet và 5 cấp cần câu.
    /// </summary>
    public static class FishingArtExporter
    {
        public const string ArtFolder = "Assets/_Project/Art/Sprites/Fishing";
        public const string ArtSetPath = "Assets/_Project/ScriptableObjects/FishingArtSet.asset";
        public const string RodFolder = "Assets/_Project/ScriptableObjects/Rods";

        private struct TierSpec
        {
            public string file, name;
            public float speed, bite, biteSpeed, miss;
            public long cost;
        }

        // Thông số prototype theo concept (chỉnh tiếp trong Inspector của từng asset Rod_*).
        private static readonly TierSpec[] Tiers =
        {
            new TierSpec { file = "Rod_1_Wood", name = "Cần gỗ", speed = 1.0f, bite = 0.10f, biteSpeed = 1.0f, miss = 0.30f, cost = 0 },
            new TierSpec { file = "Rod_2_Bamboo", name = "Cần tre", speed = 1.3f, bite = 0.14f, biteSpeed = 1.15f, miss = 0.24f, cost = 60 },
            new TierSpec { file = "Rod_3_Fiberglass", name = "Cần sợi thủy tinh", speed = 1.7f, bite = 0.18f, biteSpeed = 1.3f, miss = 0.18f, cost = 400 },
            new TierSpec { file = "Rod_4_Carbon", name = "Cần carbon", speed = 2.3f, bite = 0.23f, biteSpeed = 1.5f, miss = 0.12f, cost = 2500 },
            new TierSpec { file = "Rod_5_Legend", name = "Cần huyền thoại", speed = 3.0f, bite = 0.28f, biteSpeed = 1.8f, miss = 0.06f, cost = 15000 },
        };

        [MenuItem("Tools/Lazy Angler/Export Fishing Pixel Art")]
        public static void ExportMenu()
        {
            var set = Export();
            Selection.activeObject = set;
            Debug.Log("[LazyAngler] Đã xuất pixel art và cấp cần câu: " + ArtFolder);
        }

        /// <summary>Xuất toàn bộ, trả về FishingArtSet đã gán sprite.</summary>
        public static FishingArtSet Export()
        {
            EnsureFolder(ArtFolder);
            EnsureFolder(RodFolder);

            var sprites = new Dictionary<string, Sprite>();
            Merge(sprites, WriteSheet(FishingPixelArt.PaintFisherman(), FishingPixelArt.FishermanSheet));
            Merge(sprites, WriteSheet(FishingPixelArt.PaintRods(), FishingPixelArt.RodSheet));
            Merge(sprites, WriteSheet(FishingPixelArt.PaintProps(), FishingPixelArt.PropSheet));
            Merge(sprites, WriteSheet(FishingPixelArt.PaintMinigameUI(), FishingPixelArt.MinigameSheet));

            var set = AssetDatabase.LoadAssetAtPath<FishingArtSet>(ArtSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<FishingArtSet>();
                AssetDatabase.CreateAsset(set, ArtSetPath);
            }
            set.Assign(sprites);
            EditorUtility.SetDirty(set);

            ExportRodTiers(sprites);
            AssetDatabase.SaveAssets();
            return set;
        }

        /// <summary>Tạo 5 asset cấp cần câu; asset đã có thì giữ chỉ số đã chỉnh, chỉ cập nhật sprite.</summary>
        public static List<RodTierData> ExportRodTiers(IReadOnlyDictionary<string, Sprite> sprites)
        {
            var list = new List<RodTierData>();
            for (int i = 0; i < Tiers.Length; i++)
            {
                var spec = Tiers[i];
                string path = $"{RodFolder}/{spec.file}.asset";
                var tier = AssetDatabase.LoadAssetAtPath<RodTierData>(path);
                if (tier == null)
                {
                    tier = ScriptableObject.CreateInstance<RodTierData>();
                    tier.displayName = spec.name;
                    tier.fishingSpeedMultiplier = spec.speed;
                    tier.biteChance = spec.bite;
                    tier.unlockCost = spec.cost;
                    tier.midSegments = 2;
                    tier.sparkle = i == Tiers.Length - 1;
                    AssetDatabase.CreateAsset(tier, path);
                }
                // Dữ liệu v2: thêm tốc độ cá cắn và tỉ lệ câu hụt (gán một lần cho asset cũ, sau đó giữ số đã chỉnh).
                if (tier.dataVersion < 2)
                {
                    tier.biteSpeedMultiplier = spec.biteSpeed;
                    tier.missChance = spec.miss;
                    tier.dataVersion = 2;
                }
                sprites.TryGetValue(FishingPixelArt.RodCellName(i, 0), out tier.buttSprite);
                sprites.TryGetValue(FishingPixelArt.RodCellName(i, 1), out tier.midSprite);
                sprites.TryGetValue(FishingPixelArt.RodCellName(i, 2), out tier.tipSprite);
                EditorUtility.SetDirty(tier);
                list.Add(tier);
            }
            return list;
        }

        // ---------- PNG + import ----------

        private static Dictionary<string, Sprite> WriteSheet(PixelCanvas canvas, string sheetName)
        {
            string path = $"{ArtFolder}/{sheetName}.png";
            var tex = canvas.ToTexture(sheetName);
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = FishingPixelArt.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // cho phép SpriteRenderer Tiled (cầu gỗ)
            importer.SetTextureSettings(settings);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = canvas.Cells.Select(cell => new SpriteRect
            {
                name = cell.name,
                rect = new Rect(cell.rect.x, cell.rect.y, cell.rect.width, cell.rect.height),
                alignment = SpriteAlignment.Custom,
                pivot = cell.pivot,
                spriteID = StableId(sheetName + "/" + cell.name),
            }).ToArray();
            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
            provider.Apply();
            importer.SaveAndReimport();

            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name, s => s);
        }

        /// <summary>GUID cố định theo tên, để xuất lại không đổi spriteID (giữ tham chiếu trong scene/asset).</summary>
        private static GUID StableId(string key)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("LazyAngler/" + key));
            var sb = new StringBuilder(32);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return new GUID(sb.ToString());
        }

        private static void Merge(Dictionary<string, Sprite> into, Dictionary<string, Sprite> from)
        {
            foreach (var kv in from) into[kv.Key] = kv.Value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
