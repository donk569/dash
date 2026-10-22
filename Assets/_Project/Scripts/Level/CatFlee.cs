using UnityEngine;

namespace TiantaiDash
{
    /// <summary>屋顶的猫：玩家靠近就朝反方向窜走，边跑边淡出。</summary>
    public class CatFlee : MonoBehaviour
    {
        public float detectRange = 7f;
        public float runSpeed = 10f;

        Transform _player;
        SpriteRenderer _sr;
        bool _fled;
        float _dirX = 1f;
        float _vy;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        void Start()
        {
            var hp = FindObjectOfType<PlayerHealth>();
            if (hp != null) _player = hp.transform;
        }

        void Update()
        {
            if (_player == null) return;

            if (!_fled)
            {
                if (Mathf.Abs(_player.position.x - transform.position.x) > detectRange) return;
                if (Mathf.Abs(_player.position.y - transform.position.y) > detectRange) return;

                _fled = true;
                _dirX = transform.position.x >= _player.position.x ? 1f : -1f;
                if (_sr != null) _sr.flipX = _dirX < 0f;
                _vy = 6.5f;
                AudioManager.Play("sfx_jump", 0.25f, 2.1f);
                return;
            }

            _vy -= 26f * Time.deltaTime;
            transform.position += new Vector3(_dirX * runSpeed, _vy, 0f) * Time.deltaTime;

            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = Mathf.Max(0f, c.a - Time.deltaTime * 0.8f);
                _sr.color = c;
                if (c.a <= 0.01f) Destroy(gameObject);
            }
        }
    }
}
