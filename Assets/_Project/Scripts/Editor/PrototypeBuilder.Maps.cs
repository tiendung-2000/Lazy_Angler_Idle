using System.Collections.Generic;
using LazyAngler.Data;
using LazyAngler.Visual;
using UnityEditor;
using UnityEngine;

namespace LazyAngler.Tools
{
    /// <summary>
    /// Tạo prefab bố cục cho từng map (Assets/_Project/Prefabs/Maps/Map_*.prefab) và gán vào ZoneData.mapPrefab.
    /// Chỉ tạo khi prefab chưa có: prefab đã chỉnh tay trong Editor được giữ nguyên.
    /// Muốn tạo lại một map: xóa prefab của nó rồi chạy lại Build Prototype.
    /// Tọa độ tính theo đơn vị world, 1 đơn vị = 32 pixel art; màn hình nhìn thấy khoảng X từ -3.2 đến 3.2.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        public const string MapFolder = "Assets/_Project/Prefabs/Maps";

        private const float MapHalfW = 3.2f;
        private const float MapWaterY = 0.9f;
        private const float MapTop = 3.9f;      // đỉnh vùng nhìn thấy
        private const float MapBottom = -2.5f;  // đáy vùng nhìn thấy
        private const float MapDockTop = MapWaterY + 0.35f;

        private static void BuildMapPrefabs(List<ZoneData> zones)
        {
            EnsureFolder(MapFolder);
            var art = AssetDatabase.LoadAssetAtPath<FishingArtSet>(FishingArtExporter.ArtSetPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(UrpSpriteUnlitMaterial);

            for (int i = 0; i < zones.Count && i < ZoneSpecs.Length; i++)
            {
                var zone = zones[i];
                if (zone == null) continue;
                string path = $"{MapFolder}/Map_{i}_{ZoneSpecs[i].id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) prefab = CreateMapPrefab(path, ZoneSpecs[i], art, material);
                if (zone.mapPrefab == null)
                {
                    zone.mapPrefab = prefab;
                    zone.themeColor = ZoneSpecs[i].waterTop;
                    EditorUtility.SetDirty(zone);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreateMapPrefab(string path, ZoneSpec spec, FishingArtSet art, Material material)
        {
            var root = new GameObject("Map_" + spec.id);
            var view = root.AddComponent<MapView>();
            view.backgroundColor = spec.skyBottom;

            // ---- Nền ----
            const float width = 24f;
            float skyH = MapTop + 2f - MapWaterY;
            Layer("Sky", root.transform, material, ProceduralSpriteLayer.Shape.VerticalGradient, spec.skyTop, spec.skyBottom,
                Color.white, new Vector3(0, MapWaterY + skyH / 2f), new Vector2(width, skyH), 0);

            if (art != null)
            {
                Pixel("Sun", root.transform, material, art.sun, new Vector3(MapHalfW - 0.7f, MapTop - 0.55f), 1);
                Pixel("Cloud", root.transform, material, art.cloud, new Vector3(-MapHalfW + 1.3f, MapTop - 0.6f), 2);
                Pixel("Cloud", root.transform, material, art.cloud, new Vector3(0.6f, MapTop - 1.25f), 2);
                if (spec.island)
                    Pixel("Island", root.transform, material, art.island, new Vector3(MapHalfW - 1.3f, MapWaterY - 0.06f), 2);
            }

            float waterH = MapWaterY - MapBottom + 2f;
            Layer("Water", root.transform, material, ProceduralSpriteLayer.Shape.VerticalGradient, spec.waterTop, spec.waterBottom,
                Color.white, new Vector3(0, MapWaterY - waterH / 2f), new Vector2(width, waterH), 3);

            float seabedTop = MapBottom + 0.3f;
            Layer("SeaBed", root.transform, material, ProceduralSpriteLayer.Shape.Square, Color.white, Color.white,
                spec.seabed, new Vector3(0, seabedTop - 1f), new Vector2(width, 2f), 4);
            for (int i = 0; i < 7; i++)
            {
                float x = -MapHalfW + 0.3f + i * (2f * MapHalfW / 6.5f);
                float h = (12 + (i % 3) * 8) / 32f;
                Layer("Seaweed", root.transform, material, ProceduralSpriteLayer.Shape.Square, Color.white, Color.white,
                    new Color(0.2f, 0.55f, 0.35f), new Vector3(x, seabedTop + h / 2f), new Vector2(2f / 32f, h), 5);
            }

            // Lớp nước mờ phía trước cá + đường mặt nước sáng + vài vệt sóng.
            Layer("WaterFront", root.transform, material, ProceduralSpriteLayer.Shape.Square, Color.white, Color.white,
                new Color(0.3f, 0.65f, 0.85f, 0.14f), new Vector3(0, MapWaterY - waterH / 2f), new Vector2(width, waterH), 20);
            Layer("Surface", root.transform, material, ProceduralSpriteLayer.Shape.Square, Color.white, Color.white,
                new Color(1f, 1f, 1f, 0.7f), new Vector3(0, MapWaterY), new Vector2(width, 1f / 32f), 21);
            for (int i = 0; i < 9; i++)
            {
                float x = -MapHalfW + 0.4f + i * (2f * MapHalfW / 8.5f);
                float y = MapWaterY - (2 + (i * 5) % 7) / 32f;
                Layer("Wave", root.transform, material, ProceduralSpriteLayer.Shape.Square, Color.white, Color.white,
                    new Color(1f, 1f, 1f, 0.45f), new Vector3(x, y), new Vector2((3 + i % 3 * 2) / 32f, 1f / 32f), 21);
            }

            // ---- Cầu gỗ, thùng cá ----
            float fishermanX = -MapHalfW + 1.25f;
            float dockRight = fishermanX + 0.28f, dockLeft = -MapHalfW - 0.5f;
            if (art != null)
            {
                var plank = Pixel("Dock", root.transform, material, art.dockPlank,
                    new Vector3((dockLeft + dockRight) / 2f, MapDockTop - art.Unit(3)), 7);
                if (plank != null)
                {
                    plank.drawMode = SpriteDrawMode.Tiled;
                    plank.size = new Vector2(dockRight - dockLeft, art.Unit(6));
                }
                Pixel("Post", root.transform, material, art.dockPost, new Vector3(-MapHalfW + 0.3f, MapDockTop - art.Unit(5)), 6);
                Pixel("Post", root.transform, material, art.dockPost, new Vector3(dockRight - 0.12f, MapDockTop - art.Unit(5)), 6);
                var bucket = Pixel("Bucket", root.transform, material, art.bucket, new Vector3(-MapHalfW + 0.45f, MapDockTop), 15);
                view.bucket = bucket != null ? bucket.transform : null;
            }

            // ---- Điểm mốc (object rỗng, kéo để sắp xếp lại) ----
            view.cameraTop = Marker("CameraTop", root.transform, new Vector3(0, MapTop));
            view.waterLine = Marker("WaterLine", root.transform, new Vector3(0, MapWaterY));
            view.fishermanAnchor = Marker("FishermanAnchor", root.transform, new Vector3(fishermanX, MapDockTop));
            view.shorePoint = Marker("ShorePoint", root.transform, new Vector3(fishermanX + 0.9f, MapWaterY - 0.15f));
            view.castNear = Marker("CastNear", root.transform, new Vector3(fishermanX + 1.6f, MapWaterY));
            view.castFar = Marker("CastFar", root.transform, new Vector3(MapHalfW - 0.35f, MapWaterY));

            // ---- Vùng cá: một BoxCollider2D, cá spawn và bơi trong đây ----
            var zoneGo = new GameObject("FishZone");
            zoneGo.transform.SetParent(root.transform, false);
            float zoneYMin = MapBottom + 0.6f, zoneYMax = MapWaterY - 0.5f;
            zoneGo.transform.localPosition = new Vector3(0.05f, (zoneYMin + zoneYMax) / 2f);
            var box = zoneGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2f * MapHalfW - 0.9f, zoneYMax - zoneYMin);
            view.fishZone = zoneGo.AddComponent<FishZone>();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static Transform Marker(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        private static SpriteRenderer Pixel(string name, Transform parent, Material material, Sprite sprite, Vector3 pos, int order)
        {
            if (sprite == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        private static void Layer(string name, Transform parent, Material material, ProceduralSpriteLayer.Shape shape,
            Color top, Color bottom, Color tint, Vector3 pos, Vector2 scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = tint;
            sr.sortingOrder = order;
            if (material != null) sr.sharedMaterial = material;
            go.AddComponent<ProceduralSpriteLayer>().Configure(shape, top, bottom);
        }
    }
}
