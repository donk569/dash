using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 增益节点标记：挂在节点平台中心。运行时 DistanceTracker 会把所有标记物按 X 排序，
    /// 自动生成节点距离——所以你**拖动节点平台，节点位置自动跟着走**，不用重烘焙数据。
    /// </summary>
    public class UpgradeNode : MonoBehaviour
    {
        [Tooltip("离玩家的触发距离（世界单位），一般留 0 表示走到该 X 就触发")]
        public float triggerOffsetX;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 1.2f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
        }

        public float TriggerX => transform.position.x + triggerOffsetX;
    }
}
