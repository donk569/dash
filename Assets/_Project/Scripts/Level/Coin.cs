using UnityEngine;

namespace TiantaiDash
{
    /// <summary>金币：上下浮动 + 碰到就收，收的时候爆一圈小光点。</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Coin : MonoBehaviour
    {
        [Header("数值")]
        public int value = 1;

        [Header("浮动")]
        public float bobAmplitude = 0.14f;
        public float bobSpeed = 2.6f;

        [Header("拾取特效")]
        public Sprite glowSprite;

        Vector3 _base;
        bool _taken;
        float _phase;

        void Awake()
        {
            _base = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);

            var col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                col.size = new Vector2(1.1f, 1.1f);
            }
        }

        void Update()
        {
            transform.position = _base + Vector3.up * Mathf.Sin(Time.time * bobSpeed + _phase) * bobAmplitude;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_taken) return;
            if (other.GetComponentInParent<PlayerHealth>() == null) return;

            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;

            _taken = true;
            PlayerWallet.Add(value);
            SpawnPickupFx();
            Destroy(gameObject);
        }

        void SpawnPickupFx()
        {
            Sprite s = glowSprite;
            if (s == null)
            {
                ArtLibrary art = ArtLibrary.Instance;
                if (art != null) s = art.coin;
            }
            if (s == null) return;

            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 2f * i / 4f + 0.4f;
                FxSprite fx = FxSprite.Spawn(
                    s,
                    transform.position,
                    new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f,
                    new Color(1f, 0.92f, 0.6f, 1f),
                    0.24f,
                    0.9f,
                    17);
                fx.drag = 4f;
                fx.shrink = true;
            }
        }
    }
}
