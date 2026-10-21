using System;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 唯一状态源：控 timescale，广播状态变化。
    /// 终态（Failed/Completed）一旦进入只能由 LevelManager 重开复位，避免同帧双开面板。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.Playing;
        public event Action<GameState> OnStateChanged;

        public InputHandler Input { get; set; }
        public float RunTime { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            CurrentState = GameState.Playing;
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (CurrentState == GameState.Playing) RunTime += Time.deltaTime;

            if (Input == null) return;
            if (!Input.PausePressed) return;

            if (CurrentState == GameState.Playing) SetState(GameState.Paused);
            else if (CurrentState == GameState.Paused) SetState(GameState.Playing);
        }

        public bool IsPlaying => CurrentState == GameState.Playing;
        public bool IsTerminal => CurrentState == GameState.Failed || CurrentState == GameState.Completed;

        public void SetState(GameState next)
        {
            if (CurrentState == next) return;
            if (IsTerminal) return;

            CurrentState = next;
            bool paused = next == GameState.UpgradeSelecting || next == GameState.ShopOpen || next == GameState.Paused;
            Time.timeScale = paused ? 0f : 1f;

            if (next == GameState.Completed)
            {
                var lm = GetComponent<LevelManager>();
                if (lm != null) lm.UnlockLevel(2);
            }

            OnStateChanged?.Invoke(next);
        }

        /// <summary>仅重开时调用：无条件复位到 Playing。</summary>
        public void ResetRuntime()
        {
            CurrentState = GameState.Playing;
            RunTime = 0f;
            Time.timeScale = 1f;
            OnStateChanged?.Invoke(CurrentState);
        }
    }
}
