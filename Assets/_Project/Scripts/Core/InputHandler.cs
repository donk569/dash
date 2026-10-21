using UnityEngine;

namespace TiantaiDash
{
    /// <summary>旧版 Input Manager 封装：玩法脚本只读这里，不散落 Input.GetKey。</summary>
    public class InputHandler : MonoBehaviour
    {
        public bool JumpPressed { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool PausePressed { get; private set; }
        public bool RestartPressed { get; private set; }
        public bool ConfirmPressed { get; private set; }
        public int SelectUpgradeIndex { get; private set; } = -1;

        void Update()
        {
            var gm = GameManager.Instance;
            bool playing = gm != null && gm.CurrentState == GameState.Playing;

            // 玩法输入只在 Playing 态生效
            JumpPressed = playing && Input.GetKeyDown(KeyCode.Space);
            AttackPressed = playing && Input.GetKeyDown(KeyCode.J);

            PausePressed = gm != null
                           && Input.GetKeyDown(KeyCode.Escape)
                           && (gm.CurrentState == GameState.Playing || gm.CurrentState == GameState.Paused);

            RestartPressed = Input.GetKeyDown(KeyCode.R);
            ConfirmPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

            SelectUpgradeIndex = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectUpgradeIndex = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectUpgradeIndex = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectUpgradeIndex = 2;
        }
    }
}
