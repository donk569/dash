using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 攻击的"执行层"：冷却 + 逐帧按剑身采样点判定，同一目标每次挥砍只吃一次伤害。
    ///
    /// 谁来驱动它有两种方式：
    /// 1. 挂了 <see cref="IaiCharge"/>（居合蓄力）：由它按蓄力阶段调用 <see cref="BeginAttack"/>；
    /// 2. 没挂：退回按攻击键的快速斩（<see cref="RequestAttack"/>）。
    ///
    /// 判定窗口只看 <see cref="SwordSwing.IsSlashing"/> —— 拔刀和收刀阶段不判定。
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("冷却（仅快速斩使用，蓄力斩不受冷却限制）")]
        public float baseCooldown = 0.34f;
        [Range(0f, 0.6f)] public float cooldownReductionPerLevel = 0.18f;

        [Header("伤害")]
        public int damage = 1;

        [Header("剑")]
        public SwordSwing sword;
        [Tooltip("剑身判定圆半径（世界单位）。够大才能做到『进刀光就打得到』，0.62 ≈ 刀宽 ×1.3")]
        public float swordRadius = 0.62f;
        [Tooltip("每级范围增益：剑长 +15%，判定半径 +12%")]
        public float lengthPerLevel = 0.15f;
        public float radiusPerLevel = 0.12f;

        [Header("命中反馈")]
        [Tooltip("命中时的顿帧时长（蓄力阶段会覆盖；0 = 不顿）")]
        public float hitStop = 0.03f;

        [Header("命中特效（留空用素材总表）")]
        public Sprite hitFx;
        [Tooltip("命中冲击闪光（十字星），会盖在敌人身上做\"打实了\"的一帧")]
        public Sprite impactFlash;

        [Header("引用（留空自动找）")]
        public InputHandler input;
        public DashController dash;
        public IaiCharge iai;

        public int RangeLevel { get; set; }
        public int CooldownLevel { get; set; }
        public int AttackCount { get; private set; }
        public int UpdateTicks { get; private set; }

        /// <summary>本次出刀使用的伤害（蓄力阶段会覆盖）。</summary>
        public int CurrentDamage { get; private set; } = 1;

        /// <summary>正在下劈（剑已出鞘、判定生效）。角色姿态用它切"发力帧"。</summary>
        public bool IsAttacking => sword != null && sword.IsSlashing;

        /// <summary>拔剑 / 劈 / 收剑任一段（这段时间不接受新的攻击输入）。</summary>
        public bool IsBusy => sword != null && sword.IsBusy;

        readonly HashSet<int> _hitThisSwing = new HashSet<int>();
        readonly Vector3[] _samples = new Vector3[SwordSwing.SampleCount];
        float _cooldownUntil;
        float _stageRadiusScale = 1f;   // 蓄力阶段的判定放大（不含范围增益，避免重复叠加）

        public float CurrentCooldown =>
            baseCooldown * Mathf.Pow(1f - cooldownReductionPerLevel, CooldownLevel);

        public float CurrentRadius =>
            swordRadius * (1f + radiusPerLevel * RangeLevel) * _stageRadiusScale;

        /// <summary>范围增益给的剑长倍率。</summary>
        public float RangeLengthScale => 1f + lengthPerLevel * RangeLevel;

        void Awake()
        {
            if (input == null) input = GetComponentInParent<InputHandler>();
            if (dash == null) dash = GetComponent<DashController>();
            if (sword == null) sword = GetComponentInChildren<SwordSwing>();
            if (iai == null) iai = GetComponent<IaiCharge>();

            CurrentDamage = damage;

            // 剑默认是"插在背后"的可见状态，不是隐藏
            if (sword != null) sword.Cancel();
        }

        void Update()
        {
            UpdateTicks++;

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying)
            {
                if (sword != null && sword.IsBusy) sword.Cancel();
                return;
            }

            if (sword != null && sword.IsInHitWindow) ResolveSwordHits();

            // 有蓄力组件时，攻击键归它管；没有才退回即时攻击
            if (iai == null && input != null && input.AttackPressed) RequestAttack();
        }

        public void SetInput(InputHandler handler) { input = handler; }
        public void SetDash(DashController dashController) { dash = dashController; }
        public void SetSword(SwordSwing swing) { sword = swing; }

        /// <summary>快速斩：受冷却限制，由攻击键直接触发（未接蓄力时的兜底）。</summary>
        public void RequestAttack()
        {
            if (Time.time < _cooldownUntil) return;
            if (dash != null && dash.IsDashing) return;
            if (sword == null || sword.IsBusy) return;

            _cooldownUntil = Time.time + CurrentCooldown;
            BeginAttack(-1, 1f, -1f, -1f, -1);
        }

        /// <summary>
        /// 出刀的统一入口。蓄力斩和快速斩都走这里，保证"同一刀每个敌人只吃一次伤害"。
        /// </summary>
        /// <param name="damageOverride">大于 0 时覆盖基础伤害</param>
        /// <param name="speedScale">出刀速度倍率（1 = 基准）</param>
        /// <param name="lengthScaleOverride">大于 0 时覆盖剑长倍率</param>
        /// <param name="endAngleOverride">大于 0 时覆盖下劈终点角度</param>
        /// <param name="trailOverride">大于 0 时覆盖残影层数</param>
        public void BeginAttack(int damageOverride = -1, float speedScale = 1f,
                                float lengthScaleOverride = -1f, float endAngleOverride = -1f,
                                int trailOverride = -1)
        {
            if (sword == null) return;

            _hitThisSwing.Clear();
            AttackCount++;
            CurrentDamage = damageOverride > 0 ? damageOverride : damage;

            // 判定半径只跟随"阶段倍率"，范围增益那部分已经算进 CurrentRadius，别重复乘
            float stageScale = lengthScaleOverride > 0f ? lengthScaleOverride / Mathf.Max(0.01f, RangeLengthScale) : 1f;
            _stageRadiusScale = Mathf.Clamp(stageScale, 0.5f, 2f);

            sword.lengthScale = lengthScaleOverride > 0f ? lengthScaleOverride : RangeLengthScale;
            sword.SetSlashEndAngle(endAngleOverride > 0f ? endAngleOverride : sword.slashEndAngle);
            sword.SetTrailCount(trailOverride > 0 ? trailOverride : sword.trailCount);

            // 剑已经拔出来停在持剑位（蓄力）→ 从这里直接劈；
            // 否则（快速斩）走完整的"拔刀 → 劈 → 收刀"
            if (sword.CurrentPhase == SwordSwing.Phase.Guard) sword.PlaySlash(speedScale);
            else sword.Play(speedScale);
        }

        /// <summary>沿剑身取三个采样点做圆形重叠，剑扫过去就能打到。</summary>
        void ResolveSwordHits()
        {
            sword.SamplePoints(_samples);
            float radius = CurrentRadius;

            for (int s = 0; s < _samples.Length; s++)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(_samples[s], radius, GameLayers.EnemyMask);

                for (int i = 0; i < hits.Length; i++)
                {
                    IDamageable target = hits[i].GetComponentInParent<IDamageable>();
                    if (target == null || !target.IsAlive) continue;

                    int id = hits[i].GetInstanceID();
                    if (_hitThisSwing.Contains(id)) continue;
                    _hitThisSwing.Add(id);

                    // 玩家造成的伤害要标来源：击杀后才有冲刺奖励
                    EnemyHealth enemy = hits[i].GetComponentInParent<EnemyHealth>();
                    if (enemy != null) enemy.TakeDamage(CurrentDamage, true);
                    else target.TakeDamage(CurrentDamage);

                    CameraFollow.Shake(0.28f);
                    AudioManager.Play("sfx_hit", 0.9f, Random.Range(0.95f, 1.1f));
                    if (hitStop > 0f) TimeJuice.HitStop(hitStop, 0.06f);

                    SpawnSpark(hits[i].bounds.center);
                    SpawnImpactFlash(hits[i].bounds.center);
                }
            }
        }

        void SpawnSpark(Vector3 at)
        {
            Sprite spark = ResolveHitFx();
            if (spark == null) return;

            FxSprite fx = FxSprite.Spawn(spark, at, Vector2.zero,
                new Color(1f, 0.95f, 0.6f, 1f), 0.18f, 1.6f, 24);
            fx.drag = 0f;
        }

        Sprite ResolveHitFx()
        {
            if (hitFx != null) return hitFx;
            ArtLibrary art = ArtLibrary.Instance;
            return art != null ? art.FirstFrame(art.hitFrames) : null;
        }

        /// <summary>命中冲击闪光：一帧白亮十字，盖在敌人身上，打击感的重心在这里。</summary>
        void SpawnImpactFlash(Vector3 at)
        {
            ArtLibrary art = ArtLibrary.Instance;
            if (impactFlash == null && art != null) impactFlash = art.iaiFlash;
            if (impactFlash == null) return;

            // 层级要压在敌人(5)和角色(20/22)之上，尺寸别太小——太小会被血和敌人吃掉
            FxSprite fx = FxSprite.Spawn(impactFlash, at, Vector2.zero,
                new Color(1f, 1f, 1f, 1f),
                0.13f, Random.Range(0.55f, 0.75f), 26);
            fx.drag = 0f;
            fx.grow = 1.5f;
            fx.spin = Random.Range(0f, 90f);
        }
    }
}
