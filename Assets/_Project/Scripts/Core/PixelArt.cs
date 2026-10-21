using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 纯代码生成的工具贴图（1x1 白块）。**不再从 Resources 读取任何素材**——
    /// 正式素材一律走 ArtLibrary 的直接引用。
    /// </summary>
    public static class PixelArt
    {
        static Sprite _white;

        /// <summary>1x1 纯白 sprite，用来做纯色块 / 速度线 / 气流条（缩放后当矩形用）。</summary>
        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;

                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                _white = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                _white.name = "White1x1";
                return _white;
            }
        }

        public static Sprite Pick(Sprite[] frames, float t, float fps)
        {
            return ArtLibrary.Pick(frames, t, fps);
        }
    }
}
