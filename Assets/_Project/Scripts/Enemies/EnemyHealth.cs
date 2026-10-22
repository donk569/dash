using System;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>怪物血量：2 血、受击白闪、死亡关碰撞并淡出。</summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        public int maxHealth = 2;

        public int CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0;

        public event Action<EnemyHealth> OnDied;
        /// <summary>被玩家打死时才触发（击杀冲刺奖励用）。环境伤害走 OnDied。</summary>
        public event Action<EnemyHealth> OnKilledByPlayer;

        /// <summary>致死的那一下是不是玩家打的。</summary>
        public bool DiedByPlayer { get; private set; }

        [Header("血")]
        [Tooltip("受击/死亡时喷血（留空则用素材总表的血滴）")]
        public Sprite[] bloodFrames;
        public Sprite bloodMist;
        [Tooltip("受击喷几滴血")]
        public int hitBloodCount = 8;
        [Tooltip("死亡血滴数量")]
        public int deathBloodCount = 22;
        [Tooltip("喷血速度倍率")]
        public float bloodSpeed = 1f;

        SpriteRenderer _sr;
        Color _baseColor = Color.white;
        float _flashUntil;

        void Awake()
        {
            CurrentHealth = maxHealth;
            _sr = GetComponentInChildren<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        /// <summary>生成时配置血量（必须在 AddComponent 之后调用，Awake 里已初始化过 CurrentHealth）。</summary>
        public void SetMaxHealth(int value)
        {
            maxHealth = Mathf.Max(1, value);
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(int amount)
        {
            TakeDamage(amount, false);
        }

        /// <summary>
        /// 带来源的受伤入口。玩家侧的伤害（挥剑 / 冲刺撞击 / 冲击波）必须传 byPlayer = true，
        /// 否则击杀不会算到玩家头上、也就不会触发击杀冲刺。
        /// </summary>
        public void TakeDamage(int amount, bool byPlayer)
        {
            if (!IsAlive) return;

            if (byPlayer) DiedByPlayer = true;
            CurrentHealth -= amount;
            _flashUntil = Time.time + 0.08f;
            if (_sr != null) _sr.color = Color.white;

            SpawnBlood(hitBloodCount, 1f);

            if (!IsAlive) Die();
    }

        void Update()
        {
            if (_sr == null || !IsAlive) return;
            if (Time.time > _flashUntil && _sr.color != _baseColor) _sr.color = _baseColor;
        }

        void Die()
        {
            AudioManager.Play("sfx_kill", 0.8f, UnityEngine.Random.Range(0.92f, 1.08f));
            CameraFollow.Shake(0.2f);

            // 死亡：一大蓬血 + 血雾
            SpawnBlood(deathBloodCount, 1.6f);
            SpawnMist();

            var cols = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

            if (_sr != null) _sr.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0f);

            // 两个事件分开：OnDied 给原有逻辑，OnKilledByPlayer 只奖励玩家击杀
            OnDied?.Invoke(this);
            if (DiedByPlayer) OnKilledByPlayer?.Invoke(this);

            Destroy(gameObject, 0.15f);
        }

        /// <summary>喷血：带重力的血滴，往玩家反方向甩出去。</summary>
        void SpawnBlood(int count, float speedScale)
        {
            if (count <= 0) return;

            ArtLibrary art = ArtLibrary.Instance;
            Sprite[] frames = bloodFrames != null && bloodFrames.Length > 0 ? bloodFrames
                             : (art != null ? art.bloodFrames : null);
            if (frames == null || frames.Length == 0) return;

            Vector3 at = transform.position + Vector3.up * 0.1f;
            for (int i = 0; i < count; i++)
            {
                Sprite s = frames[UnityEngine.Random.Range(0, frames.Length)];
                Vector2 vel = new Vector2(
                    UnityEngine.Random.Range(-5f, 11f),
                    UnityEngine.Random.Range(-2f, 9f)) * speedScale * bloodSpeed;

                FxSprite fx = FxSprite.Spawn(s,
                    at + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f),
                                     UnityEngine.Random.Range(-0.35f, 0.55f), 0f),
                    vel, Color.white,
                    UnityEngine.Random.Range(0.30f, 0.65f),
                    UnityEngine.Random.Range(1.0f, 1.8f),
                    24);
                fx.gravity = 30f;      // 落下去，才有重量
                fx.drag = 0.7f;
                fx.shrink = false;
            }
        }

        /// <summary>血雾：死亡瞬间在尸体处散开一团。</summary>
        void SpawnMist()
        {
            ArtLibrary art = ArtLibrary.Instance;
            Sprite mist = bloodMist != null ? bloodMist : (art != null ? art.bloodMist : null);
            if (mist == null) return;

            for (int i = 0; i < 2; i++)
            {
                FxSprite fx = FxSprite.Spawn(mist,
                    transform.position + Vector3.up * (0.15f + i * 0.25f),
                    new Vector2(UnityEngine.Random.Range(-1.5f, 2.5f), UnityEngine.Random.Range(0.5f, 2f)),
                    new Color(1f, 1f, 1f, 0.9f),
                    0.28f + i * 0.08f,
                    UnityEngine.Random.Range(1.2f, 1.9f),
                    23);
                fx.drag = 1.2f;
                fx.grow = 0.9f;
                fx.shrink = false;
            }
        }
    }
}
