using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 攻击：拔剑下劈。冷却 + 逐帧按剑身采样点判定（剑扫到哪打到哪），
    /// 同一目标每次挥砍只吃一次伤害。
    ///
    /// 判定窗口只看 <see cref="SwordSwing.IsSlashing"/> —— 也就是"剑已经从背后抽出来、
    /// 正在自上往下劈"的这一段。拔剑和收剑阶段不判定，所以不会出现背后剑砍到人的怪事。
    ///
    /// 范围增益会同时放大剑的可见长度（<see cref="SwordSwing.lengthScale"/>）和判定半径，
    /// 所以"加攻击范围"在画面上是真能看出来的。
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("冷却")]
        public float baseCooldown = 0.34f;
        [Range(0f, 0.6f)] public float cooldownReductionPerLevel = 0.18f;

        [Header("伤害")]
        public int damage = 1;

        [Header("剑")]
        public SwordSwing sword;
        [Tooltip("剑身判定圆半径（世界单位）")]
        public float swordRadius = 0.72f;
        [Tooltip("每级范围增益：剑长 +15%，判定半径 +12%")]
        public float lengthPerLevel = 0.15f;
        public float radiusPerLevel = 0.12f;

        [Header("命中特效（留空用素材总表）")]
        public Sprite hitFx;

        [Header("引用（留空自动找）")]
        public InputHandler input;
        public DashController dash;

        public int RangeLevel { get; set; }
        public int CooldownLevel { get; set; }
        public int AttackCount { get; private set; }
        public int UpdateTicks { get; private set; }

        /// <summary>正在下劈（剑已出鞘、判定生效）。角色姿态用它切"发力帧"。</summary>
        public bool IsAttacking => sword != null && sword.IsSlashing;

        /// <summary>拔剑 / 劈 / 收剑任一段（这段时间不接受新的攻击输入）。</summary>
        public bool IsBusy => sword != null && sword.IsBusy;

        readonly HashSet<int> _hitThisSwing = new HashSet<int>();
        readonly Vector3[] _samples = new Vector3[3];
        float _cooldownUntil;

        public float CurrentCooldown =>
            baseCooldown * Mathf.Pow(1f - cooldownReductionPerLevel, CooldownLevel);

        public float CurrentRadius => swordRadius * (1f + radiusPerLevel * RangeLevel);

        void Awake()
        {
            if (input == null) input = GetComponentInParent<InputHandler>();
            if (dash == null) dash = GetComponent<DashController>();
            if (sword == null) sword = GetComponentInChildren<SwordSwing>();

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

            if (sword != null && sword.IsSlashing) ResolveSwordHits();

            if (input != null && input.AttackPressed) RequestAttack();
        }

        public void SetInput(InputHandler handler) { input = handler; }
        public void SetDash(DashController dashController) { dash = dashController; }
        public void SetSword(SwordSwing swing) { sword = swing; }

        public void RequestAttack()
        {
            if (Time.time < _cooldownUntil) return;
            if (dash != null && dash.IsDashing) return;
            if (sword == null || sword.IsBusy) return;   // 动作没做完不接受新输入

            _cooldownUntil = Time.time + CurrentCooldown;
            _hitThisSwing.Clear();
            AttackCount++;

            // 范围增益直接作用到剑的可见长度上
            sword.lengthScale = 1f + lengthPerLevel * RangeLevel;
            sword.Play(1f);
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

                    target.TakeDamage(damage);
                    CameraFollow.Shake(0.16f);
                    AudioManager.Play("sfx_hit", 0.9f, Random.Range(0.95f, 1.1f));

                    SpawnSpark(hits[i].bounds.center);
                }
            }
        }

        void SpawnSpark(Vector3 at)
        {
            Sprite spark = ResolveHitFx();
            if (spark == null) return;

            FxSprite fx = FxSprite.Spawn(spark, at, Vector2.zero,
                new Color(1f, 0.95f, 0.6f, 1f), 0.18f, 1.6f, 12);
            fx.drag = 0f;
        }

        Sprite ResolveHitFx()
        {
            if (hitFx != null) return hitFx;
            ArtLibrary art = ArtLibrary.Instance;
            return art != null ? art.FirstFrame(art.hitFrames) : null;
        }
    }
}
