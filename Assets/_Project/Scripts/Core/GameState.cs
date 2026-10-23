namespace TiantaiDash
{
    /// <summary>全局游戏状态（唯一裁决权在 GameManager）。</summary>
    public enum GameState
    {
        Playing,
        /// <summary>离店后的 3-2-1-GO 准备阶段：世界暂停，玩家还不能动。</summary>
        Ready,
        /// <summary>金币商店：暂停世界，卡片要花金币。</summary>
        ShopOpen,
        Paused,
        Failed,
        Completed
    }
}
