using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 进度条数据源：起点 X → 终点 X。
    /// 终点默认**从场景自动收集**（PortalExit 的位置），所以在场景里挪终点不用改任何数据。
    ///
    /// 增益节点（UpgradeNode）已移除：升级只能通过金币商店购买，
    /// 这里不再有节点距离 / 节点到达事件。
    /// </summary>
    public class DistanceTracker : MonoBehaviour
    {
        [Header("目标")]
        [Tooltip("留空自动找场景里的 PlayerHealth")]
        public Transform player;

        [Header("距离")]
        [Tooltip("起点 X（一般就是出生点）。留 0 会取玩家初始位置")]
        public float startX;
        [Tooltip("终点 X；开启自动收集时会被 PortalExit 的位置覆盖")]
        public float endX = 890f;

        [Header("自动收集")]
        public bool autoCollect = true;

        bool _collected;

        public float CurrentDistance => player != null ? Mathf.Max(0f, player.position.x - startX) : 0f;
        public float TotalDistance => Mathf.Max(1f, endX - startX);
        public float Progress01 => Mathf.Clamp01(CurrentDistance / TotalDistance);

        void Awake()
        {
            if (player == null)
            {
                var hp = FindObjectOfType<PlayerHealth>();
                if (hp != null) player = hp.transform;
            }

            if (autoCollect) CollectFromScene();
        }

        /// <summary>把场景里 PortalExit 的 X 读成终点。编辑器工具也会调这个方法。</summary>
        public void CollectFromScene()
        {
            var portal = FindObjectOfType<PortalExit>();
            if (portal != null) endX = portal.transform.position.x;

            if (player != null && startX <= 0f) startX = player.position.x;

            _collected = true;
        }

        public void ResetState()
        {
            if (!_collected && autoCollect) CollectFromScene();
        }
    }
}
