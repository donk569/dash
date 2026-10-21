using UnityEngine;

namespace TiantaiDash
{
    /// <summary>本局金币。重开/换关会清空（跟着场景重载一起走）。</summary>
    public class PlayerWallet : MonoBehaviour
    {
        public static PlayerWallet Instance { get; private set; }

        [Header("初始金币")]
        public int coins;

        public int TotalCollected { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static int Coins => Instance != null ? Instance.coins : 0;

        public static void Add(int amount)
        {
            if (Instance == null || amount <= 0) return;
            Instance.coins += amount;
            Instance.TotalCollected += amount;
            AudioManager.Play("sfx_select", 0.5f, 1.35f);
        }

        public static bool CanAfford(int price)
        {
            return Instance != null && Instance.coins >= price;
        }

        public static bool TrySpend(int price)
        {
            if (Instance == null || Instance.coins < price) return false;
            Instance.coins -= price;
            return true;
        }

        public void ResetCoins()
        {
            coins = 0;
            TotalCollected = 0;
        }
    }
}
