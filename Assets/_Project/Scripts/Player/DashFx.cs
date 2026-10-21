using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 冲刺手感核心：残影 + 速度线 + 冲击火花 + 震屏 + 变焦 + 音效。
    /// 冲刺一响就要"重"：先抖一下、镜头拉远、火花向后甩，再持续刷残影。
    /// </summary>
    public class DashFx : MonoBehaviour
    {
        public float ghostInterval = 0.04f;
        public float ghostLife = 0.22f;
        public float lineInterval = 0.016f;

        [Header("白块贴图（留空自动生成 1x1）")]
        public Sprite whiteSprite;

        SpriteRenderer _sr;
        DashController _dash;
        PlayerRunner _runner;
        float _ghostTimer;
        float _lineTimer;

        Sprite White { get { return whiteSprite != null ? whiteSprite : PixelArt.White; } }

        void Awake()
        {
            _sr = GetComponentInChildren<SpriteRenderer>();
            _dash = GetComponent<DashController>();
            _runner = GetComponent<PlayerRunner>();
        }

        void Start()
        {
            if (_dash == null) return;
            _dash.OnDashStarted += HandleStart;
            _dash.OnDashEnded += HandleEnd;
        }

        void OnDestroy()
        {
            if (_dash == null) return;
            _dash.OnDashStarted -= HandleStart;
            _dash.OnDashEnded -= HandleEnd;
        }

        void Update()
        {
            if (_dash == null || !_dash.IsDashing) return;

            _ghostTimer -= Time.deltaTime;
            if (_ghostTimer <= 0f)
            {
                _ghostTimer = ghostInterval;
                SpawnGhost(0.5f);
            }

            _lineTimer -= Time.deltaTime;
            if (_lineTimer <= 0f)
            {
                _lineTimer = lineInterval;
                SpawnLine();
            }
        }

        void HandleStart()
        {
            CameraFollow.Shake(0.6f);
            CameraFollow.SetZoom(1.9f);
            AudioManager.Play("sfx_dash", 1f, Random.Range(0.94f, 1.07f));

            SpawnBurst(16, -1f, 0.55f);
            SpawnGhost(0.9f);

            // 起步竖向气流条（不要大白块，会糊住角色）
            FxSprite.Spawn(White, transform.position + Vector3.up * 1.6f, Vector2.zero,
                new Color(0.7f, 1f, 1f, 0.35f), 0.1f, 1f, 12);
            GameObject streak = new GameObject("DashStreak");
            var ssr = streak.AddComponent<SpriteRenderer>();
            ssr.sprite = White;
            ssr.color = new Color(0.6f, 1f, 1f, 0.45f);
            ssr.sortingOrder = 13;
            streak.transform.position = transform.position + Vector3.up * 1.7f;
            streak.transform.localScale = new Vector3(0.12f, 3.4f, 1f);
            var sfx = streak.AddComponent<FxSprite>();
            sfx.velocity = new Vector2(-14f, 0f);
            sfx.life = 0.16f;
            sfx.drag = 2f;
            sfx.shrink = false;

            // 起步时的速度线，向后拉出长条
            for (int i = 0; i < 6; i++)
            {
                SpawnLine(2f + i * 0.5f, 0.9f);
            }
        }

        void HandleEnd()
        {
            CameraFollow.SetZoom(0f);
            CameraFollow.Shake(0.22f);
            AudioManager.Play("sfx_impact", 0.65f);
            SpawnBurst(10, 1f, 0.4f);
        }

        void SpawnGhost(float alpha)
        {
            if (_sr == null || _sr.sprite == null) return;

            var go = new GameObject("Ghost");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sr.sprite;
            sr.flipX = _sr.flipX;
            sr.sortingOrder = _sr.sortingOrder - 1;
            sr.color = new Color(0.18f, 0.42f, 0.62f, alpha * 0.8f);

            go.transform.position = _sr.transform.position + new Vector3(-0.6f, 0f, 0f);
            go.transform.rotation = _sr.transform.rotation;
            go.transform.localScale = _sr.transform.localScale;

            var fx = go.AddComponent<FxSprite>();
            fx.life = ghostLife;
            fx.drag = 6f;
            fx.shrink = false;
        }

        void SpawnLine()
        {
            SpawnLine(1f, 0.45f);
        }

        void SpawnLine(float lengthScale, float alpha)
        {
            float y = transform.position.y + Random.Range(-1.2f, 2.6f);
            float x = transform.position.x + Random.Range(-1.5f, 2.5f);
            float len = 3.5f * lengthScale;

            var go = new GameObject("SpeedLine");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = White;
            sr.sortingOrder = 11;
            sr.color = new Color(0.55f, 0.95f, 1f, alpha * 0.55f);
            go.transform.position = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(len, 0.09f, 1f);

            var fx = go.AddComponent<FxSprite>();
            fx.velocity = new Vector2(-38f, 0f);
            fx.life = 0.16f;
            fx.drag = 0.4f;
            fx.shrink = false;
        }

        void SpawnBurst(int count, float direction, float speedScale)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 vel = new Vector2(
                    direction * Random.Range(6f, 18f) * speedScale,
                    Random.Range(-7f, 7f) * speedScale);

                var fx = FxSprite.Spawn(White,
                    transform.position + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.2f, 2.2f), 0f),
                    vel,
                    new Color(0.65f, 0.98f, 1f, 0.9f),
                    Random.Range(0.18f, 0.34f),
                    Random.Range(0.18f, 0.36f),
                    12);

                fx.drag = 2.2f;
                fx.shrink = true;
            }
        }
    }
}
