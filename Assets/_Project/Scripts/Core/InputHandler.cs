using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 旧版 Input Manager 封装：玩法脚本只读这里，不散落 Input.GetKey。
    ///
    /// 居合斩需要"按下 / 按住 / 松开"三种语义，所以攻击拆成三个只读属性。
    /// 跳跃改成鼠标右键（Space 作为兼容保留）。
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        public bool JumpPressed { get; private set; }

        /// <summary>攻击键（鼠标左键）这一帧按下。</summary>
        public bool AttackPressed { get; private set; }

        /// <summary>攻击键持续按住（蓄力用）。</summary>
        public bool AttackHeld { get; private set; }

        /// <summary>攻击键这一帧松开（释放斩击用）。</summary>
        public bool AttackReleased { get; private set; }

        public bool PausePressed { get; private set; }
        public bool RestartPressed { get; private set; }
        public bool ConfirmPressed { get; private set; }
        public int SelectUpgradeIndex { get; private set; } = -1;

        /// <summary>冲刺键（鼠标中键）这一帧按下。</summary>
        public bool DashPressed { get; private set; }

        /// <summary>
        /// 取走这一帧的"按下跳跃"。同一帧只允许一个系统消费，
        /// 这样"蓄力跳带冷却"就能把冷却期内的跳真正吞掉，而不是两个系统各跳一次。
        /// </summary>
        public bool ConsumeJump()
        {
            if (!JumpPressed) return false;
            JumpPressed = false;
            return true;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            bool playing = gm != null && gm.CurrentState == GameState.Playing;

            // 鼠标左键 = 攻击（蓄力），右键 = 跳跃；Space / J 作为兼容保留
            bool attackDown = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J);
            bool attackUp = Input.GetMouseButtonUp(0) || Input.GetKeyUp(KeyCode.J);
            bool attackHold = Input.GetMouseButton(0) || Input.GetKey(KeyCode.J);

            // 玩法输入只在 Playing 态生效
            JumpPressed = playing && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space));

            AttackPressed = playing && attackDown;
            AttackHeld = playing && attackHold;
            AttackReleased = playing && attackUp;

            PausePressed = gm != null
                           && Input.GetKeyDown(KeyCode.Escape)
                           && (gm.CurrentState == GameState.Playing || gm.CurrentState == GameState.Paused);

            RestartPressed = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.R);
            ConfirmPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

            // 冲刺：鼠标中键（单独一条线，和跳跃/攻击都不冲突）
            DashPressed = playing && Input.GetMouseButtonDown(2);

            SelectUpgradeIndex = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectUpgradeIndex = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectUpgradeIndex = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectUpgradeIndex = 2;
        }
    }
}
