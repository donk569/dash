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
            if (!IsAlive) return;

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
            OnDied?.Invoke(this);
            Destroy(gameObject, 0.15f);
        }
    }
}
