using System.Collections;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 离店/离增益节点后的流程：
    /// 1. 把玩家放到**最近的安全平台**上（向下探测地面，取水平最近的一块）；
    /// 2. 世界暂停，播 3 → 2 → 1 → GO!（英文语音）；
    /// 3. 倒计时结束才恢复 Playing，玩家才开始跑。
    /// 用 WaitForSecondsRealtime，所以 timeScale = 0 也能正常计时。
    /// </summary>
    public class RunFlow : MonoBehaviour
    {
        public static RunFlow Instance { get; private set; }

        [Header("倒计时")]
        [Tooltip("每个数字停留多久（秒，真实时间）")]
        public float stepSeconds = 0.9f;
        [Tooltip("GO! 之后再停多久才开始跑")]
        public float goHoldSeconds = 0.45f;
        [Tooltip("英文播报：3 / 2 / 1 / GO")]
        public string[] countdownSfx = { "sfx_count_3", "sfx_count_2", "sfx_count_1", "sfx_count_go" };

        [Header("传送到安全平台")]
        [Tooltip("从玩家上方多高往下探地面")]
        public float probeHeight = 12f;
        [Tooltip("水平方向最多找多远（找不到就左右各试这么多）")]
        public float horizontalSearch = 14f;

        public Transform player;

        /// <summary>当前要显示的倒计时文字（"" = 不显示）。</summary>
        public string Label { get; private set; } = "";
        public bool Counting { get; private set; }

        Rigidbody2D _rb;

        void Awake()
        {
            Instance = this;
        }

        /// <summary>离开商店 / 增益面板：先站回平台，再 3-2-1-GO。</summary>
        public void ResumeRun()
        {
            if (Counting) return;
            TeleportToSafeGround();
            StartCoroutine(CountdownRoutine());
        }

        /// <summary>向下探测最近的地面并站上去；找不到就不动。</summary>
        public void TeleportToSafeGround()
        {
            if (player == null) return;
            if (_rb == null) _rb = player.GetComponent<Rigidbody2D>();

            Vector3 from = player.position;
            float bestScore = float.MaxValue;
            Vector3 bestPoint = from;
            bool found = false;

            for (float dx = 0f; dx <= horizontalSearch; dx += 1f)
            {
                for (int sign = 0; sign < (dx <= 0f ? 1 : 2); sign++)
                {
                    float x = from.x + (sign == 0 ? dx : -dx);
                    RaycastHit2D hit = Physics2D.Raycast(
                        new Vector2(x, from.y + probeHeight), Vector2.down,
                        probeHeight + 40f, GameLayers.GroundMask);
                    if (hit.collider == null) continue;

                    float score = Mathf.Abs(x - from.x) + Mathf.Abs(hit.point.y - from.y) * 0.15f;
                    if (score >= bestScore) continue;

                    bestScore = score;
                    bestPoint = hit.point;
                    found = true;
                }
            }

            if (!found) return;

            // 注意：这里 timeScale 会被倒计时设成 0，物理不再步进，
            // 只写 rb.position 的话 transform 不会立刻更新，所以两边都写。
            Vector3 target = new Vector3(bestPoint.x, bestPoint.y + 0.06f, player.position.z);
            player.position = target;

            if (_rb != null)
            {
                _rb.position = new Vector2(target.x, target.y);
                _rb.velocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
        }

        IEnumerator CountdownRoutine()
        {
            Counting = true;
            var gm = GameManager.Instance;
            if (gm != null) gm.SetState(GameState.Ready);

            for (int i = 0; i < 3; i++)
            {
                Label = (3 - i).ToString();
                if (countdownSfx != null && i < countdownSfx.Length)
                    AudioManager.Play(countdownSfx[i], 1f, 1f);
                yield return new WaitForSecondsRealtime(stepSeconds);
            }

            Label = "GO!";
            if (countdownSfx != null && countdownSfx.Length > 3)
                AudioManager.Play(countdownSfx[3], 1f, 1f);
            yield return new WaitForSecondsRealtime(goHoldSeconds);

            Label = "";
            Counting = false;
            if (gm != null) gm.SetState(GameState.Playing);
        }
    }
}
