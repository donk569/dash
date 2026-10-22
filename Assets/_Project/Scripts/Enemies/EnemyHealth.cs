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
            var cols = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

            if (_sr != null) _sr.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0f);

            // 两个事件分开：OnDied 给原有逻辑，OnKilledByPlayer 只奖励玩家击杀
            OnDied?.Invoke(this);
            if (DiedByPlayer) OnKilledByPlayer?.Invoke(this);

            Destroy(gameObject, 0.15f);
        }
    }
}
