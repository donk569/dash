namespace TiantaiDash
{
    /// <summary>
    /// 计分事件类型。基础分与连击增益见 <see cref="ScoreKeeper"/>。
    /// </summary>
    public enum ScoreEvent
    {
        /// <summary>普通斩击杀：100 分，链 +1</summary>
        KillSword,
        /// <summary>冲刺撞死：150 分（同一次冲刺的第 2 个起 75），链 +1</summary>
        KillDash,
        /// <summary>空中斩击杀（离地状态下命中）：200 分，链 +1</summary>
        KillAir,
        /// <summary>满蓄（第三段）击杀：300 分，链 +2</summary>
        KillIai,
        /// <summary>擦身（0.9–1.6u 掠过敌人且没受伤）：50 分，链 +1</summary>
        NearMiss,
        /// <summary>踩踏击杀：80 分，链 +1</summary>
        Stomp,
        /// <summary>通关奖励：不参与连击链</summary>
        Clear,
    }

    /// <summary>评级。名字直接拿来显示，所以故意用单字母。</summary>
    public enum ScoreRank
    {
        C = 0,
        B = 1,
        A = 2,
        S = 3,
    }
}
