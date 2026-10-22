using System.Collections;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>顿帧（hitstop）：命中瞬间把 timeScale 压到很低再弹回，打击感全靠这个。</summary>
    public class TimeJuice : MonoBehaviour
    {
        public static TimeJuice Instance { get; private set; }

        Coroutine _running;
        float _stopUntil;
        float _nextAllowedTime;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void HitStop(float duration, float scale)
        {
            if (Instance == null) return;
            // 同一帧里"击杀顿帧(0.07)"和"阶段顿帧(0.12)"会连着来：
            // 已经在顿的时候不要直接丢掉后来的请求，取更长的那个，否则强力一击反而只顿 0.07s。
            if (Instance._running != null)
            {
                Instance._stopUntil = Mathf.Max(Instance._stopUntil, Time.unscaledTime + Mathf.Max(0f, duration));
                return;
            }
            if (Time.unscaledTime < Instance._nextAllowedTime) return;   // 冷却
            Instance._nextAllowedTime = Time.unscaledTime + 0.12f;
            Instance._running = Instance.StartCoroutine(Instance.Run(duration, scale));
        }

        IEnumerator Run(float duration, float scale)
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying || Time.timeScale < 0.99f)
            {
                // 没真正顿下去也要把状态清掉，否则 _running 永远非空、之后所有顿帧都被吞掉
                _running = null;
                yield break;
            }

            Time.timeScale = scale;
            _stopUntil = Time.unscaledTime + Mathf.Max(0f, duration);
            while (Time.unscaledTime < _stopUntil) yield return null;

            if (gm.IsPlaying) Time.timeScale = 1f;
            _running = null;
        }
    }
}
