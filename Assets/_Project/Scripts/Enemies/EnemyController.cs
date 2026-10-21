using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 近战怪。两种行为：
    /// - <c>chase = false</c>：在出生点附近水平巡逻（原来的行为）
    /// - <c>chase = true</c>：**追踪玩家**，持续朝玩家靠近（速度比玩家慢，追得上但追不死）
    ///
    /// 另外加了"踩踏"：从上方落到怪头上时，怪受伤、玩家被弹起，玩家不掉血。
    /// 之前玩家跳起来撞到浮空怪只会自己掉血，手感很差。
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyController : MonoBehaviour
    {
        [Header("接触伤害")]
        public int contactDamage = 1;
        [Tooltip("踩踏时玩家的弹起速度")]
        public float stompBounce = 13f;
        [Tooltip("玩家脚底高于怪中心多少才算踩踏")]
        public float stompHight = 0.45f;

        [Header("巡逻（chase = false 时生效）")]
        public float patrolRange = 2.2f;
        public float patrolSpeed = 1.2f;
        public float knockbackStep = 0.55f;

        [Header("追踪（chase = true 时生效）")]
        public bool chase;
        public float chaseSpeed = 6.5f;
        [Tooltip("超过这个距离就不追了，省性能")]
        public float chaseRange = 42f;
        [Tooltip("追到玩家身边后稍微抬高一点，避免整个人贴脸挡住")]
        public float chaseLift = 0.9f;

        EnemyHealth _health;
        PlayerHealth _player;
        float _originX;
        float _dir = 1f;
        float _wobble;

        public int Damage => Mathf.Max(1, contactDamage);

        void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            _originX = transform.position.x;
        }

        void Update()
        {
            if (_health == null || !_health.IsAlive) return;

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            if (chase) ChasePlayer();
            else Patrol();
        }

        void Patrol()
        {
            transform.position += new Vector3(_dir * patrolSpeed * Time.deltaTime, 0f, 0f);
            if (transform.position.x > _originX + patrolRange) _dir = -1f;
            else if (transform.position.x < _originX - patrolRange) _dir = 1f;
        }

        void ChasePlayer()
        {
            if (_player == null)
            {
                _player = FindObjectOfType<PlayerHealth>();
                if (_player == null) return;
            }

            Vector3 target = _player.transform.position + Vector3.up * chaseLift;
            Vector2 gap = target - transform.position;

            if (gap.magnitude > chaseRange) return;

            // 慢慢追：速度比玩家慢，但一直咬在后面
            _wobble += Time.deltaTime * 3.2f;
            Vector3 step = ((Vector2)target - (Vector2)transform.position).normalized
                           * chaseSpeed * Time.deltaTime;
            step.y += Mathf.Sin(_wobble) * 0.9f * Time.deltaTime;

            transform.position += step;
        }

        void OnTriggerEnter2D(Collider2D other) { TryDamage(other); }
        void OnTriggerStay2D(Collider2D other) { TryDamage(other); }

        void TryDamage(Collider2D other)
        {
            if (other == null) return;

            var playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null) return;

            if (_player == null) _player = playerHealth;

            // 踩踏：玩家正在下落，且脚底高于怪的中心 → 怪受伤、玩家弹起
            Rigidbody2D rb = other.attachedRigidbody;
            bool falling = rb != null && rb.velocity.y < -0.5f;
            bool above = other.bounds.min.y > transform.position.y + stompHight;

            if (falling && above && _health != null && _health.IsAlive)
            {
                _health.TakeDamage(1);
                if (rb != null) rb.velocity = new Vector2(rb.velocity.x, stompBounce);

                AudioManager.Play("sfx_hit", 0.85f, 1.15f);
                CameraFollow.Shake(0.2f);
                return;
            }

            playerHealth.TakeDamage(Damage);
        }

        public void ApplyKnockback(float dir)
        {
            transform.position += new Vector3(dir * knockbackStep, 0f, 0f);
            _originX += dir * knockbackStep;
        }
    }
}
