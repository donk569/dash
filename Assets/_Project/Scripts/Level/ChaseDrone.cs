using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 追逐段的副威胁：远程扫射无人机。
    ///
    /// 行为：跟在玩家身后上方 → 每隔 2.6~3.8s 朝玩家**前方 5u** 的地面打一条红色警戒带
    /// → 0.6s 后开火，站在带里且没跳起来就吃 1 点伤害。
    ///
    /// 三种解法都成立：
    /// - 冲刺：0.6s 内冲过 5u 的预判点
    /// - 跳跃：判定含高度，跳起来就躲开
    /// - 直接砍：挂了 <see cref="EnemyHealth"/> + Enemy 层，挥剑 / 冲刺撞击都能打掉
    ///
    /// 全部视觉程序生成，不依赖素材。
    /// </summary>
    public class ChaseDrone : MonoBehaviour
    {
        [Header("跟随")]
        [Tooltip("在玩家**前方**多远（不能放在身后：墙宽 13u、跟在身后 14u，会把无人机整个盖住）")]
        public float followAhead = 3f;
        [Tooltip("高于玩家多少")]
        public float followHeight = 7f;
        public float followLerp = 4.5f;

        [Header("攻击")]
        public float cooldownMin = 2.6f;
        public float cooldownMax = 3.8f;
        [Tooltip("红条出现到开火的时间")]
        public float warnTime = 0.6f;
        [Tooltip("红条放在玩家前方多少（预判）")]
        public float aimAhead = 5f;
        [Tooltip("红条宽度")]
        public float beamWidth = 2.6f;
        [Tooltip("玩家高于地面多少就躲开（跳跃解法）")]
        public float dodgeHeight = 2.5f;
        public int damage = 1;

        [Header("尺寸")]
        public float bodyWidth = 2.2f;
        public float bodyHeight = 0.85f;

        enum Phase { Idle, Warning, Firing }

        static Sprite _unit;

        Phase _phase = Phase.Idle;
        float _cooldown;
        float _phaseTimer;
        float _barX;
        float _barY;
        float _bobPhase;

        Transform _player;
        PlayerHealth _playerHealth;
        EnemyHealth _health;

        SpriteRenderer _eye;
        SpriteRenderer _barCore;
        SpriteRenderer _barGlow;
        SpriteRenderer _beam;
        Transform _rotorA;
        Transform _rotorB;

        static readonly Color32 CBodyDark = new Color32(44, 56, 84, 255);   // 机身（比夜空亮一档才看得见）
        static readonly Color32 CBodyMid = new Color32(76, 94, 132, 255);   // 上盖
        static readonly Color32 CRim = new Color32(120, 156, 208, 255);     // 边缘高光
        static readonly Color32 CEye = new Color32(111, 230, 255, 255);   // 常态青
        static readonly Color32 CEyeHot = new Color32(255, 90, 110, 255);  // 预警红
        static readonly Color32 CBar = new Color32(150, 26, 44, 255);
        static readonly Color32 CBarHot = new Color32(255, 77, 109, 255);
        static readonly Color32 CBeam = new Color32(255, 140, 160, 255);
        static readonly Color32 CThruster = new Color32(255, 170, 90, 255);

        static Sprite Unit
        {
            get
            {
                if (_unit != null) return _unit;

                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.Apply();

                _unit = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                _unit.name = "DroneUnit";
                return _unit;
            }
        }

        /// <summary>生成一架无人机。ahead/height 可以给不同值，三架错开才不像排队。</summary>
        public static ChaseDrone Spawn(Vector3 at, Transform player, float ahead, float height)
        {
            var go = new GameObject("ChaseDrone");
            go.transform.position = at;
            go.layer = GameLayers.Enemy;

            var d = go.AddComponent<ChaseDrone>();
            d._player = player;
            d.followAhead = ahead;
            d.followHeight = height;
            d.Build();

            // 血量放在 Build 之后：EnemyHealth.Awake 会去抓子物体的 SpriteRenderer 做白闪
            var health = go.AddComponent<EnemyHealth>();
            health.SetMaxHealth(1);
            d._health = health;

            return d;
        }

        void Build()
        {
            // 机身主体 + 上盖 + 底缘高光（高光是暗背景下唯一能勾出轮廓的东西）
            MakePart("Body", CBodyDark, 0f, 0f, bodyWidth, bodyHeight, 12);
            MakePart("Top", CBodyMid, 0f, bodyHeight * 0.40f, bodyWidth * 0.80f, bodyHeight * 0.36f, 13);
            MakePart("Rim", CRim, 0f, -bodyHeight * 0.46f, bodyWidth * 0.92f, 0.09f, 14);

            // 两侧旋翼（横条，靠 scale 做"转"的错觉）
            _rotorA = MakePart("RotorA", CBodyMid, -bodyWidth * 0.52f, bodyHeight * 0.66f, bodyWidth * 0.55f, 0.11f, 13).transform;
            _rotorB = MakePart("RotorB", CBodyMid, bodyWidth * 0.52f, bodyHeight * 0.66f, bodyWidth * 0.55f, 0.11f, 13).transform;

            // 底部推进器
            MakePart("Thruster", CThruster, 0f, -bodyHeight * 0.70f, bodyWidth * 0.24f, 0.16f, 13);

            // 机头"眼睛"（预警时变红）
            _eye = MakePart("Eye", CEye, bodyWidth * 0.36f, 0.02f, 0.40f, 0.40f, 15);

            // 警戒带（平时隐藏）
            _barGlow = MakePart("BarGlow", CBar, 0f, 0f, beamWidth + 0.5f, 0.30f, 11);
            _barCore = MakePart("BarCore", CBarHot, 0f, 0f, beamWidth, 0.14f, 12);

            // 光束（平时隐藏）
            _beam = MakePart("Beam", CBeam, 0f, 0f, 0.34f, 1f, 15);

            _barGlow.enabled = false;
            _barCore.enabled = false;
            _beam.enabled = false;

            // 攻击判定用的碰撞体（trigger，不推挤玩家）
            var col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(bodyWidth, bodyHeight * 1.6f);
        }

        SpriteRenderer MakePart(string name, Color color, float lx, float ly, float w, float h, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(lx, ly, 0f);
            go.transform.localScale = new Vector3(w, h, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Unit;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        void Awake()
        {
            _bobPhase = Random.Range(0f, 6.28f);
            _cooldown = Random.Range(cooldownMin, cooldownMax);
        }

        void Update()
        {
            if (_health != null && !_health.IsAlive) return;

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) { HideWarn(); return; }
            if (_player == null) return;

            Follow();
            Animate();

            switch (_phase)
            {
                case Phase.Idle:
                    _cooldown -= Time.deltaTime;
                    if (_cooldown <= 0f) BeginWarning();
                    break;

                case Phase.Warning:
                    _phaseTimer -= Time.deltaTime;
                    PulseWarn();
                    if (_phaseTimer <= 0f) Fire();
                    break;

                case Phase.Firing:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f) EndFire();
                    break;
            }
        }

        void Follow()
        {
            float bob = Mathf.Sin(Time.time * 2.2f + _bobPhase) * 0.35f;
            Vector3 target = new Vector3(
                _player.position.x + followAhead,
                _player.position.y + followHeight + bob,
                0f);

            transform.position = Vector3.Lerp(transform.position, target, followLerp * Time.deltaTime);
        }

        void Animate()
        {
            // 旋翼用横向缩放抖动，远看像在转
            float spin = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 22f + _bobPhase));
            if (_rotorA != null) _rotorA.localScale = new Vector3(bodyWidth * 0.55f * spin, 0.11f, 1f);
            if (_rotorB != null) _rotorB.localScale = new Vector3(bodyWidth * 0.55f * spin, 0.11f, 1f);
        }

        void BeginWarning()
        {
            _phase = Phase.Warning;
            _phaseTimer = Mathf.Max(0.1f, warnTime);

            _barX = _player.position.x + aimAhead;
            _barY = GroundY(_barX);

            ShowBar();
        }

        void PulseWarn()
        {
            // 闪烁：靠亮度而不是 alpha（半透明色块在 URP 2D 会被渲染成实心）
            float k = Mathf.PingPong(Time.time * 8f, 1f);
            Color c = Color.Lerp(CBar, CBarHot, k);
            if (_barCore != null) _barCore.color = c;
            if (_eye != null) _eye.color = Color.Lerp(CEye, CEyeHot, k);
        }

        void Fire()
        {
            _phase = Phase.Firing;
            _phaseTimer = 0.14f;

            // 光束：从机身竖直打到地面
            if (_beam != null)
            {
                float topY = transform.position.y;
                float height = Mathf.Max(0.5f, topY - _barY);
                _beam.enabled = true;
                _beam.transform.position = new Vector3(_barX, _barY + height * 0.5f, 0f);
                _beam.transform.localScale = new Vector3(0.34f, height, 1f);
            }

            AudioManager.Play("sfx_hit", 0.55f, 1.35f);
            CameraFollow.Shake(0.12f);

            // 判定：站在带里 + 没跳过 dodgeHeight
            if (_player != null)
            {
                float dx = Mathf.Abs(_player.position.x - _barX);
                bool inBand = dx <= beamWidth * 0.5f;
                bool low = _player.position.y < _barY + dodgeHeight;

                if (inBand && low && _playerHealth != null)
                    _playerHealth.TakeDamage(damage);
            }
        }

        void EndFire()
        {
            _phase = Phase.Idle;
            _cooldown = Random.Range(cooldownMin, cooldownMax);
            if (_beam != null) _beam.enabled = false;
            if (_eye != null) _eye.color = CEye;
            HideWarn();
        }

        void ShowBar()
        {
            if (_barGlow != null)
            {
                _barGlow.enabled = true;
                _barGlow.transform.position = new Vector3(_barX, _barY + 0.1f, 0f);
            }
            if (_barCore != null)
            {
                _barCore.enabled = true;
                _barCore.transform.position = new Vector3(_barX, _barY + 0.1f, 0f);
            }
        }

        void HideWarn()
        {
            if (_barGlow != null) _barGlow.enabled = false;
            if (_barCore != null) _barCore.enabled = false;
            if (_beam != null) _beam.enabled = false;
            if (_eye != null) _eye.color = CEye;
        }

        /// <summary>向下探地面，找不到就用玩家脚下高度，保证红条永远贴着可站面。</summary>
        float GroundY(float x)
        {
            float fromY = _player != null ? _player.position.y + 6f : transform.position.y;
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down,
                40f, GameLayers.GroundMask);

            if (hit.collider != null) return hit.point.y;
            return _player != null ? _player.position.y - 0.1f : 0f;
        }

        /// <summary>由 ChaseZone 注入玩家引用（生成时可能还没找到）。</summary>
        public void SetPlayer(Transform player, PlayerHealth health)
        {
            _player = player;
            _playerHealth = health;
        }

        void Start()
        {
            if (_player == null)
            {
                var hp = FindObjectOfType<PlayerHealth>();
                if (hp != null)
                {
                    _player = hp.transform;
                    _playerHealth = hp;
                }
            }
        }
    }
}
