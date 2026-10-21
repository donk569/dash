using System.Collections;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>顿帧（hitstop）：命中瞬间把 timeScale 压到很低再弹回，打击感全靠这个。</summary>
    public class TimeJuice : MonoBehaviour
    {
        public static TimeJuice Instance { get; private set; }

        Coroutine _running;
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
            if (Instance._running != null) return;                       // 不叠加
            if (Time.unscaledTime < Instance._nextAllowedTime) return;   // 冷却
            Instance._nextAllowedTime = Time.unscaledTime + 0.12f;
            Instance._running = Instance.StartCoroutine(Instance.Run(duration, scale));
        }

        IEnumerator Run(float duration, float scale)
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying || Time.timeScale < 0.99f) yield break;

            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(duration);

            if (gm.IsPlaying) Time.timeScale = 1f;
            _running = null;
        }
    }
}
