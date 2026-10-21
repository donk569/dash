using UnityEngine;

namespace TiantaiDash
{
    public enum UpgradeKind
    {
        DoubleJump,
        DashDistance,
        DashShockwave,
        AttackRange,
        AttackCooldown,
        MaxHealth
    }

    /// <summary>增益静态数据。运行时等级不改资产（见 UpgradeManager）。</summary>
    [CreateAssetMenu(menuName = "TiantaiDash/Upgrade Definition")]
    public class UpgradeDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public UpgradeKind kind;
        public int maxLevel = 1;
        public bool repeatable = true;
        public bool oneShot;
    }
}
