using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 商店招牌：**按距离触发**（玩家跑到招牌的 X 就自动开商店）。
    /// 原来靠 OnTriggerEnter2D，冲刺速度下一帧就穿过触发器，判定会被隧穿掉，
    /// 所以改成位置比较 —— 跟玩家跑多快无关。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ShopIcon : MonoBehaviour
    {
        [Header("价格（金币）")]
        public int price = 3;

        [Header("外观")]
        [Tooltip("保留编辑器里的尺寸与位置，运行时不再缩放/移动")]
        public bool keepEditorTransform = true;

        [Header("圆形检测范围")]
        [Tooltip("玩家进入这个半径就自动进店（世界单位）")]
        public float detectRadius = 10f;
        [Tooltip("圆心相对商店的偏移（一般抬到店面中部）")]
        public Vector2 detectOffset = new Vector2(4f, 7f);

        [Header("浮动")]
        public float bobAmplitude = 0.18f;
        public float bobSpeed = 1.8f;

        Vector3 _base;
        float _openX;
        Transform _player;
        bool _opened;
        SpriteRenderer _sr;

        /// <summary>最近一次打开的商店（离店后就地复活用）。</summary>
        public static ShopIcon LastOpened;

        /// <summary>这家店是否已经触发过（进度条标记用）。</summary>
        public bool Opened => _opened;

        void Awake()
        {
            _sr = GetComponentInChildren<SpriteRenderer>();

            // 招牌要压在角色(20)、场景建筑之上，不然会被楼挡掉
            if (_sr != null) _sr.sortingOrder = 25;

            // 尺寸/位置完全保留编辑器里的样子
            if (!keepEditorTransform)
            {
                SnapToRoof();
            }

            bobAmplitude = 0f;   // 站在屋面上，不上下浮动
            _base = transform.position;
            _openX = transform.position.x;

            var hp = FindObjectOfType<PlayerHealth>();
            if (hp != null) _player = hp.transform;

            // 圆形检测范围（触发用），碰撞体本身不再当判定依据
            var box = GetComponent<BoxCollider2D>();
            if (box != null) box.enabled = false;

            var circle = GetComponent<CircleCollider2D>();
            if (circle == null) circle = gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = Mathf.Max(0.5f, detectRadius);
            circle.offset = detectOffset;
        }

        /// <summary>向下（必要时左右）找屋面，把店面底部贴上去。找不到就不动。</summary>
        void SnapToRoof()
        {
            float bestScore = float.MaxValue;
            Vector3 best = transform.position;

            for (float dx = 0f; dx <= 8f; dx += 1f)
            {
                int tries = dx <= 0f ? 1 : 2;
                for (int s = 0; s < tries; s++)
                {
                    float x = transform.position.x + (s == 0 ? dx : -dx);
                    RaycastHit2D hit = Physics2D.Raycast(
                        new Vector2(x, transform.position.y + 6f), Vector2.down,
                        26f, GameLayers.GroundMask);
                    if (hit.collider == null) continue;

                    float score = Mathf.Abs(x - transform.position.x)
                                + Mathf.Abs(hit.point.y - transform.position.y) * 0.3f;
                    if (score >= bestScore) continue;

                    bestScore = score;
                    best = new Vector3(x, hit.point.y + 0.02f, transform.position.z);
                }
            }

            if (bestScore < float.MaxValue) transform.position = best;

            // 子物体自带缩放/偏移，缩放到目标尺寸后按"实际渲染底边"对齐屋面，
            // 否则店面会浮在屋顶上方（之前就是这样悬空的）
            if (_sr != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    float gap = transform.position.y - _sr.bounds.min.y;
                    if (Mathf.Abs(gap) < 0.02f) break;
                    _sr.transform.position += new Vector3(0f, gap, 0f);
                }
            }
        }

        void Update()
        {
            transform.position = _base + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;

            // 打开过之后压暗，提示"这家的已经用过了"
            if (_opened && _sr != null) _sr.color = new Color(0.35f, 0.4f, 0.5f, 0.7f);

            if (_opened) return;
            if (_player == null)
            {
                var hp = FindObjectOfType<PlayerHealth>();
                if (hp != null) _player = hp.transform;
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            // 只有"碰到商店碰撞体"才开门：每帧比对包围盒，
            // 高速冲刺也不会像 OnTriggerEnter 那样被隧穿过去
            if (TouchingPlayer()) OpenShop();
        }

        /// <summary>玩家是否进入圆形检测范围（圆心 = 商店位置 + detectOffset）。</summary>
        bool TouchingPlayer()
        {
            if (_player == null) return false;
            Vector2 center = (Vector2)transform.position + detectOffset;
            return Vector2.Distance(_player.position, center) <= detectRadius;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.84f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere((Vector2)transform.position + detectOffset, detectRadius);
        }

        void OpenShop()
        {
            UpgradeManager upgrades = FindObjectOfType<UpgradeManager>();
            if (upgrades == null) return;

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            _opened = true;
            LastOpened = this;
            upgrades.RollUpgrades();

            // 到店回满血
            if (_player != null)
            {
                var hp = _player.GetComponent<PlayerHealth>();
                if (hp != null) hp.Heal(hp.maxHealth);
            }

            // 离店后在这个招牌的位置复活
            var flow = RunFlow.Instance;
            if (flow != null) flow.pendingSpawnX = _openX;

            var ui = FindObjectOfType<PrototypeUI>();
            if (ui != null) ui.currentShopPrice = price;

            // 冲刺中到店：先把冲刺收掉，免得关店后继续冲
            var dash = FindObjectOfType<DashController>();
            if (dash != null) dash.CancelDash();

            gm.SetState(GameState.ShopOpen);
            AudioManager.Play("sfx_select", 0.9f, 0.85f);
        }

        public void ResetState()
        {
            _opened = false;
            if (_sr != null) _sr.color = Color.white;
        }
    }
}
