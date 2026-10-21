using System;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>生命、受击无敌闪烁、死亡只触发一次。</summary>
    public class PlayerHealth : MonoBehaviour
    {
        public int maxHealth = 3;
        public float invulnerableDuration = 0.8f;

        public int CurrentHealth { get; private set; }
        public int DamageTakenCount { get; private set; }

        public event Action<int, int> OnHealthChanged;
        public event Action OnDeath;

        SpriteRenderer[] _renderers;
        float _invulnerableUntil;
        bool _dead;

        public bool IsInvulnerable => Time.time < _invulnerableUntil;
        public bool IsDead => _dead;

        void Awake()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            CurrentHealth = maxHealth;
        }

        void Start()
        {
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        void Update()
        {
            if (_dead || _renderers == null || _renderers.Length == 0) return;

            bool visible = !IsInvulnerable || Mathf.Repeat(Time.unscaledTime, 0.12f) < 0.06f;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].enabled = visible;
            }
        }

        public void TakeDamage(int amount)
        {
            if (_dead) return;
            if (IsInvulnerable) return;

            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;

            var dash = GetComponent<DashController>();
            if (dash != null && dash.IsDashing && dash.invincibleWhileDashing) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            DamageTakenCount++;

            AudioManager.Play("sfx_hurt", 0.9f, UnityEngine.Random.Range(0.95f, 1.05f));
            CameraFollow.Shake(0.55f);
            _invulnerableUntil = Time.time + invulnerableDuration;
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth > 0) return;

            _dead = true;
            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null) _renderers[i].enabled = true;
            }
            OnDeath?.Invoke();
        }

        public void Heal(int amount)
        {
            if (_dead) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void IncreaseMaxHealth(int amount)
        {
            maxHealth += amount;
            Heal(amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void ResetState()
        {
            _dead = false;
            CurrentHealth = maxHealth;
            _invulnerableUntil = 0f;
            DamageTakenCount = 0;
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }
    }
}
