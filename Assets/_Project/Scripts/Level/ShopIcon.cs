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
            _base = transform.position;
            _openX = transform.position.x;
            _sr = GetComponentInChildren<SpriteRenderer>();

            // 招牌要压在角色(20)、场景建筑之上，不然会被楼挡掉 —— 这就是"后面几家看不到店"的原因
            if (_sr != null) _sr.sortingOrder = 25;

            var hp = FindObjectOfType<PlayerHealth>();
            if (hp != null) _player = hp.transform;

            var col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                col.size = new Vector2(2.4f, 3.2f);
                col.offset = new Vector2(0f, 1.2f);
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

            if (_player.position.x >= _openX) OpenShop();
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
