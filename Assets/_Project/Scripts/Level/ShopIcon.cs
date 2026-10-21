using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 商店招牌：玩家碰到就打开金币商店（暂停世界、三选一用金币买）。
    /// 每次进关每个招牌只能用一次，避免原地反复刷。
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
        bool _opened;
        SpriteRenderer _sr;

        void Awake()
        {
            _base = transform.position;
            _sr = GetComponentInChildren<SpriteRenderer>();

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
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_opened) return;
            if (other.GetComponentInParent<PlayerHealth>() == null) return;

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            UpgradeManager upgrades = FindObjectOfType<UpgradeManager>();
            if (upgrades == null) return;

            _opened = true;
            upgrades.RollUpgrades();

            var ui = FindObjectOfType<PrototypeUI>();
            if (ui != null) ui.currentShopPrice = price;

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
