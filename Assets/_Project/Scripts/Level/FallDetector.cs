using UnityEngine;

namespace TiantaiDash
{
    /// <summary>掉落判定：低于 killY 直接失败（比 Trigger 区更稳，无需铺满场景）。</summary>
    public class FallDetector : MonoBehaviour
    {
        public Transform player;
        public float killY = -25f;

        void Update()
        {
            if (player == null) return;

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            if (player.position.y < killY) gm.SetState(GameState.Failed);
        }
    }
}
