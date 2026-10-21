using UnityEngine;

namespace TiantaiDash
{
    /// <summary>Layer 统一入口：项目里没配时回退到固定索引，保证代码永远能跑。</summary>
    public static class GameLayers
    {
        public const int PlayerFallback = 8;
        public const int GroundFallback = 9;
        public const int EnemyFallback = 10;
        public const int TriggerFallback = 11;

        public static int Player => Resolve("Player", PlayerFallback);
        public static int Ground => Resolve("Ground", GroundFallback);
        public static int Enemy => Resolve("Enemy", EnemyFallback);
        public static int Trigger => Resolve("Trigger", TriggerFallback);

        public static int GroundMask => 1 << Ground;
        public static int EnemyMask => 1 << Enemy;

        static int Resolve(string layerName, int fallback)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer < 0 ? fallback : layer;
        }
    }
}
