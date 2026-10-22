using UnityEditor;
using UnityEngine;

namespace TiantaiDash.EditorTools
{
    /// <summary>
    /// 像素素材统一导入规则：Assets/_Project/Art/Sprites 下全部 PPU16 / Point / 无压缩 / FullRect。
    /// 角色帧额外把轴心放到脚底（(0.5, 0.045)），这样 player 根节点就在脚下，碰撞体和摆放都简单。
    /// 注：素材已移出 Resources，只有被 ArtLibrary 引用到的才会进包。
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        const string SpriteRoot = "/_Project/Art/Sprites/";

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains(SpriteRoot)) return;

            var importer = assetImporter as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            // 背景层是 Tiled 平铺、并且要横向无缝循环，必须 Repeat；
            // 角色/瓦片用 Clamp，避免相邻帧串色
            bool isBackground = path.Contains("/Sprites/Bg/");
            importer.wrapMode = isBackground ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;

            bool isPlayer = path.Contains("/Sprites/Player/");
            // 剑和鞘要以握把为轴旋转，两者 pivot 必须完全一致，鞘才能精确套在刃上
            bool isSword = path.Contains("prop_sword") || path.Contains("prop_sheath");

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            settings.spriteAlignment = (isPlayer || isSword)
                ? (int)SpriteAlignment.Custom
                : (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);

            if (isPlayer) importer.spritePivot = new Vector2(0.5f, 0.045f);
            // 剑的轴心放在**握把中部**（手握住的位置），挥砍时绕手旋转才自然；
            // 放在最底部会让剑像"从脚底长出来"，放在中心则剑柄会穿到地下
            if (isSword) importer.spritePivot = new Vector2(0.5f, 0.13f);
        }
    }
}
