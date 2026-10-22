using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 场景装配入口。**运行时不再生成任何东西**——关卡、玩家、敌人、背景全是场景里的真实对象。
    /// 这里只做三件事：把引用串起来、绑事件、开声音。
    /// 所有字段都摆在 Inspector 里，留空会自动在场景里找，也可以手动拖。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("核心模块（留空自动在场景里找）")]
        public GameManager gameManager;
        public InputHandler input;
        public LevelManager levelManager;
        public DistanceTracker tracker;
        public UpgradeManager upgrades;
        public FallDetector fallDetector;
        public AudioManager audioManager;

        [Header("玩家")]
        public PlayerHealth playerHealth;
        public PlayerRunner playerRunner;
        public PlayerJump playerJump;
        public PlayerCombat playerCombat;
        public DashController dashController;
        public IaiCharge iaiCharge;
        public KillReward killReward;

        [Header("镜头 / 表现")]
        public CameraFollow cameraFollow;
        public ParallaxBackdrop backdrop;
        public PrototypeUI ui;

        [Header("启动设置")]
        public bool playBgmOnStart = true;
        [Tooltip("关卡结束后自动重开的时间（0 = 不自动）")]
        public float autoRestartDelay;

        void Awake()
        {
            Time.timeScale = 1f;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;   // 编辑器失焦时也继续跑（也方便自测读数）

            ResolveRefs();
            Wire();
        }

        void Start()
        {
            if (playBgmOnStart) AudioManager.PlayBgm();
        }

        static T Pick<T>(T a, T b) where T : Object
        {
            return a != null ? a : b;
        }

        /// <summary>把没拖的引用按类型补上，方便临时搭场景。</summary>
        void ResolveRefs()
        {
            if (gameManager == null) gameManager = Pick(GetComponent<GameManager>(), FindObjectOfType<GameManager>());
            if (input == null) input = Pick(GetComponent<InputHandler>(), FindObjectOfType<InputHandler>());
            if (levelManager == null) levelManager = Pick(GetComponent<LevelManager>(), FindObjectOfType<LevelManager>());
            if (tracker == null) tracker = Pick(GetComponent<DistanceTracker>(), FindObjectOfType<DistanceTracker>());
            if (upgrades == null) upgrades = Pick(GetComponent<UpgradeManager>(), FindObjectOfType<UpgradeManager>());
            if (fallDetector == null) fallDetector = Pick(GetComponent<FallDetector>(), FindObjectOfType<FallDetector>());
            if (audioManager == null) audioManager = Pick(GetComponent<AudioManager>(), FindObjectOfType<AudioManager>());

            if (playerHealth == null) playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth != null)
            {
                Transform p = playerHealth.transform;
                if (playerRunner == null) playerRunner = p.GetComponent<PlayerRunner>();
                if (playerJump == null) playerJump = p.GetComponent<PlayerJump>();
                if (playerCombat == null) playerCombat = p.GetComponent<PlayerCombat>();
                if (dashController == null) dashController = p.GetComponent<DashController>();
                if (iaiCharge == null) iaiCharge = p.GetComponent<IaiCharge>();
            }

            if (iaiCharge == null) iaiCharge = FindObjectOfType<IaiCharge>();
            if (killReward == null) killReward = Pick(GetComponent<KillReward>(), FindObjectOfType<KillReward>());

            if (cameraFollow == null) cameraFollow = FindObjectOfType<CameraFollow>();
            if (backdrop == null) backdrop = FindObjectOfType<ParallaxBackdrop>();
            if (ui == null) ui = Pick(GetComponent<PrototypeUI>(), FindObjectOfType<PrototypeUI>());
        }

        void Wire()
        {
            if (gameManager == null) gameManager = gameObject.AddComponent<GameManager>();
            gameManager.Input = input;

            if (playerHealth != null && playerRunner != null && playerJump != null &&
                playerCombat != null && dashController != null)
            {
                playerJump.SetInput(input);
                playerCombat.SetInput(input);
                playerCombat.SetDash(dashController);
                dashController.SetRunner(playerRunner);
                dashController.SetInput(input);
            }

            // InputHandler 挂在 Game 上，不是 Player 的父级 —— 必须显式注入，
            // 否则 IaiCharge 的 GetComponentInParent 会拿到 null，左键蓄力整个不响应。
            if (iaiCharge != null && input != null) iaiCharge.SetInput(input);

            if (tracker != null && playerHealth != null)
            {
                tracker.player = playerHealth.transform;
                tracker.CollectFromScene();
            }

            if (fallDetector != null && playerHealth != null) fallDetector.player = playerHealth.transform;

            if (upgrades != null && playerJump != null && dashController != null &&
                playerCombat != null && playerHealth != null)
            {
                upgrades.Configure(playerJump, dashController, playerCombat, playerHealth);
            }

            if (ui != null)
            {
                ui.health = playerHealth;
                ui.tracker = tracker;
                ui.upgrades = upgrades;
                ui.dash = dashController;
                ui.combat = playerCombat;
                ui.input = input;
                ui.levelManager = levelManager;
                ui.iai = iaiCharge;
                ui.killReward = killReward;
            }

            // 离店后的 3-2-1-GO 流程（组件挂在 Game 上，不需要额外往场景里拖东西）
            RunFlow flow = GetComponent<RunFlow>();
            if (flow == null) flow = gameObject.AddComponent<RunFlow>();
            if (playerHealth != null) flow.player = playerHealth.transform;

            // 屋顶生活：给已有屋顶程序化加装饰和 NPC
            if (GetComponent<RoofLife>() == null) gameObject.AddComponent<RoofLife>();

            // 楼体立面变体 + 消防梯/管道/霓虹条
            if (GetComponent<BuildingVariety>() == null) gameObject.AddComponent<BuildingVariety>();

            // 星星 + 烟花
            if (GetComponent<SkyLife>() == null) gameObject.AddComponent<SkyLife>();

            if (killReward != null)
            {
                killReward.dash = dashController;
                killReward.iai = iaiCharge;
            }

            if (tracker != null && upgrades != null)
            {
                tracker.OnNodeReached += index =>
                {
                    if (gameManager == null || !gameManager.IsPlaying) return;
                    upgrades.RollUpgrades();
                    gameManager.SetState(GameState.UpgradeSelecting);
                };
            }

            if (playerHealth != null)
            {
                playerHealth.OnDeath += () =>
                {
                    if (gameManager != null) gameManager.SetState(GameState.Failed);
                };
            }

            if (cameraFollow != null && playerHealth != null)
            {
                cameraFollow.target = playerHealth.transform;
            }
        }
    }
}
