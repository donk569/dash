using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 素材总表（ScriptableObject）。所有贴图都是**直接引用**，不走 Resources.Load，
    /// 打包时只会带上这里被引用到的图；换素材直接在 Inspector 里拖，不用改代码。
    /// 资产位置：Assets/_Project/Art/ArtLibrary.asset
    /// </summary>
    [CreateAssetMenu(fileName = "ArtLibrary", menuName = "天台疾行/素材总表")]
    public class ArtLibrary : ScriptableObject
    {
        [Header("主角（顺序即播放顺序）")]
        public Sprite[] playerIdle;
        public Sprite[] playerRun;
        public Sprite[] playerRise;
        public Sprite[] playerFall;
        [Tooltip("落地缓冲（蹲姿）")]
        public Sprite[] playerLand;
        public Sprite[] playerShoot;
        public Sprite[] playerHurt;

        [Header("敌人")]
        public Sprite[] droneFrames;
        public Sprite[] turretFrames;

        [Header("地形瓦片")]
        public Sprite tileRoof;
        public Sprite tileWall;
        public Sprite tileFloat;
        public Sprite tileNeon;
        public Sprite tilePipe;

        [Header("道具")]
        public Sprite dashArrow;
        public Sprite antenna;
        [Tooltip("楼体侧面的大招牌，会随机挑一张")]
        public Sprite[] banners;
        public Sprite[] torchFrames;

        [Header("终点")]
        public Sprite[] flagFrames;
        public Sprite openSign;
        public Sprite controlBox;
        public Sprite[] bigBanners;

        [Header("特效 / UI")]
        public Sprite[] hitFrames;
        public Sprite[] explosionFrames;
        public Sprite heart;
        [Tooltip("纯白块，昼夜遮罩 / 速度线用")]
        public Sprite white;
        public Sprite[] dustFrames;
        [Tooltip("像素位图数字（0-9 /.smx 一张图）")]
        public Sprite uiDigits;
        [Tooltip("2x2 扫描线，用于全屏遮罩")]
        public Sprite uiScanline;

        [Header("战斗 / 拾取")]
        public Sprite sword;
        public Sprite coin;
        public Sprite shopIcon;

        [Header("背景动画")]
        public Sprite moonGlow;
        public Sprite meteorHead;
        public Sprite lightCone;
        [Tooltip("漂浮广告牌用的素材")]
        public Sprite billboard;

        [Header("背景")]
        public Sprite sky;
        public Sprite bgFar;
        public Sprite bgMid;
        public Sprite bgNear;

        static ArtLibrary _cached;

        /// <summary>
        /// 编辑器便利入口：预制体 / 场景里的显式引用才是**唯一正式路径**，
        /// 这里只是给"新建对象还没拖引用"时兜底（仅编辑器有效，打包后返回 null）。
        /// </summary>
        public static ArtLibrary Instance
        {
            get
            {
                if (_cached != null) return _cached;
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ArtLibrary");
                if (guids.Length > 0)
                {
                    _cached = UnityEditor.AssetDatabase.LoadAssetAtPath<ArtLibrary>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                }
#endif
                return _cached;
            }
        }

        public Sprite FirstFrame(Sprite[] frames, Sprite fallback = null)
        {
            if (frames != null && frames.Length > 0) return frames[0];
            return fallback;
        }

        public static Sprite Pick(Sprite[] frames, float t, float fps)
        {
            if (frames == null || frames.Length == 0) return null;
            if (frames.Length == 1) return frames[0];
            int index = Mathf.Abs(Mathf.FloorToInt(t * fps)) % frames.Length;
            return frames[index];
        }
    }
}
