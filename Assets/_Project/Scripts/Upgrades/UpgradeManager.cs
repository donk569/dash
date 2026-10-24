using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>候选抽取 + 本局等级记录 + 效果分发。不承载具体效果实现。</summary>
    public class UpgradeManager : MonoBehaviour
    {
        public List<UpgradeDefinition> catalog = new List<UpgradeDefinition>();
        public List<UpgradeDefinition> CurrentChoices { get; } = new List<UpgradeDefinition>();
        public int SelectedCount { get; private set; }

        readonly Dictionary<string, int> _levels = new Dictionary<string, int>();
        readonly System.Random _rng = new System.Random();

        PlayerJump _jump;
        DashController _dash;
        PlayerCombat _combat;
        PlayerHealth _health;

        public void Configure(PlayerJump jump, DashController dash, PlayerCombat combat, PlayerHealth health)
        {
            _jump = jump;
            _dash = dash;
            _combat = combat;
            _health = health;
            BuildDefaultCatalog();
        }

        /// <summary>首批 6 个增益。描述里的数值必须和实际实现一致，否则玩家会觉得"买了没效果"。</summary>
        void BuildDefaultCatalog()
        {
            catalog.Clear();
            catalog.Add(Define("double_jump", "二段跳", "空中额外跳跃 1 次", UpgradeKind.DoubleJump, 1, false, true));
            catalog.Add(Define("dash_distance", "疾行延长", "冲刺时长 +40%", UpgradeKind.DashDistance, 3, true, false));
            catalog.Add(Define("dash_shockwave", "冲击波", "冲刺收尾击退周围敌人", UpgradeKind.DashShockwave, 1, false, true));
            catalog.Add(Define("attack_range", "扩大攻击", "判定 +12%  剑身 +15%", UpgradeKind.AttackRange, 3, true, false));
            catalog.Add(Define("attack_cd", "快速出手", "攻击冷却 -18%", UpgradeKind.AttackCooldown, 3, true, false));
            catalog.Add(Define("max_hp", "生命提升", "最大生命 +1 并回血", UpgradeKind.MaxHealth, 2, true, false));
        }

        static UpgradeDefinition Define(string id, string name, string desc, UpgradeKind kind,
                                        int maxLevel, bool repeatable, bool oneShot)
        {
            var def = ScriptableObject.CreateInstance<UpgradeDefinition>();
            def.id = id;
            def.displayName = name;
            def.description = desc;
            def.kind = kind;
            def.maxLevel = maxLevel;
            def.repeatable = repeatable;
            def.oneShot = oneShot;
            return def;
        }

        public int GetLevel(string id)
        {
            int level;
            return _levels.TryGetValue(id, out level) ? level : 0;
        }

        public bool IsAvailable(UpgradeDefinition def)
        {
            if (def == null) return false;
            return GetLevel(def.id) < def.maxLevel;
        }

        public void RollUpgrades()
        {
            var pool = new List<UpgradeDefinition>();
            for (int i = 0; i < catalog.Count; i++)
                if (IsAvailable(catalog[i])) pool.Add(catalog[i]);

            // 洗牌
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                UpgradeDefinition tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            CurrentChoices.Clear();
            for (int i = 0; i < pool.Count && CurrentChoices.Count < 3; i++)
                CurrentChoices.Add(pool[i]);

            // 不足 3 张：从"还没满级、且这次没被选过"的池子里补，
            // 不能像以前那样从全表随机取 —— 会把已满级的项塞进来，玩家花了金币却没有任何效果。
            int guard = 0;
            while (CurrentChoices.Count < 3 && guard++ < 24)
            {
                var spare = new List<UpgradeDefinition>();
                for (int i = 0; i < catalog.Count; i++)
                {
                    UpgradeDefinition c = catalog[i];
                    if (!IsAvailable(c)) continue;
                    if (CurrentChoices.Contains(c)) continue;
                    spare.Add(c);
                }

                if (spare.Count == 0) break;
                CurrentChoices.Add(spare[_rng.Next(spare.Count)]);
            }

            SelectedCount = 0;
        }

        public void ApplyUpgrade(int index)
        {
            if (index < 0 || index >= CurrentChoices.Count) return;

            UpgradeDefinition def = CurrentChoices[index];

            // 满级不再生效：避免钱花了、属性没变
            if (def == null || !IsAvailable(def)) return;

            _levels[def.id] = GetLevel(def.id) + 1;
            AudioManager.Play("sfx_select", 0.9f);

            switch (def.kind)
            {
                case UpgradeKind.DoubleJump:
                    if (_jump != null) _jump.ExtraJumps = 1;
                    break;
                case UpgradeKind.DashDistance:
                    if (_dash != null) _dash.AddDistanceLevel();
                    break;
                case UpgradeKind.DashShockwave:
                    if (_dash != null) _dash.UnlockShockwave();
                    break;
                case UpgradeKind.AttackRange:
                    if (_combat != null) _combat.RangeLevel++;
                    break;
                case UpgradeKind.AttackCooldown:
                    if (_combat != null) _combat.CooldownLevel++;
                    break;
                case UpgradeKind.MaxHealth:
                    if (_health != null) _health.IncreaseMaxHealth(1);
                    break;
            }

            SelectedCount++;
        }

        public void ResetState()
        {
            _levels.Clear();
            CurrentChoices.Clear();
            SelectedCount = 0;
        }
    }
}
