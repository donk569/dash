namespace TiantaiDash
{
    /// <summary>可受伤目标：怪物、可破坏障碍都走这个接口，玩法层不依赖具体怪物类。</summary>
    public interface IDamageable
    {
        void TakeDamage(int amount);
        bool IsAlive { get; }
    }
}
