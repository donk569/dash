namespace TiantaiDash
{
    /// <summary>全局游戏状态（唯一裁决权在 GameManager）。</summary>
    public enum GameState
    {
        Playing,
        UpgradeSelecting,
        /// <summary>金币商店：同样暂停世界，但卡片要花金币。</summary>
        ShopOpen,
        Paused,
        Failed,
        Completed
    }
}
