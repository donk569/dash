using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 剑的完整动作循环：**腰间收刀 → 手握刀柄蓄力 → 掏刀横抹 → 收刀回腰**。
    ///
    /// 三个关键点：
    /// 1. **待机时剑斜挂在腰后**（层级低于角色，被身体挡住），不是招之即来的光效；
    /// 2. **出鞘翻转藏在身后**：从鞘里的 -102° 到起手角，整段刀身隐藏、只看到手在腰前动，
    ///    所以不会出现"刀在身后转半圈"的穿帮；
    /// 3. **斩击是横向抹刀**：可见段只转 30° 左右（约 122°→92°），主体是**握把向前横移**，
    ///    刀身全程贴着水平线走，不再经过正下方。这是"横向斩"和"从上往下劈"的分水岭。
    ///
    /// 角度约定：0 = 剑尖朝上（北），正角度 = 朝右（顺时针），90 = 正前方，118 = 前下方。
    /// 运行时角度走负方向连续展开（-102° → -268°），避免插值绕远路。
    ///
    /// 判定不在这里做——这里只负责"剑现在在哪、是不是在劈"，
    /// 由 <see cref="PlayerCombat"/> 按剑身采样点逐帧检测。
    /// </summary>
    public class SwordSwing : MonoBehaviour
    {
        public enum Phase { Sheathed, Draw, Guard, Slash, Sheathe }

        [Header("待机：腰间收刀")]
        [Tooltip("握把位置：贴腰侧、略偏身后（角色身高约 2.4u，腰高约 1.2）")]
        public Vector3 sheathedPos = new Vector3(-0.28f, 1.16f, 0f);
        [Tooltip("收刀倾角：刀身向后近水平、略下垂 = 腰间佩刀")]
        public float sheathedAngle = -104f;
        [Tooltip("鞘的层级：低于角色（20）")]
        public int sheathedOrder = 19;
        [Tooltip("收刀/蓄力时隐藏刀身，只留鞘（否则鞘没盖住的刃尖会露出来，看着很唐突）")]
        public bool hideBladeWhenSheathed = true;

        [Header("蓄力：手握刀柄、刀还在鞘里")]
        [Tooltip("抽刀路径的中间点；蓄力位贴着腰侧，这个点只做轻微外撇")]
        public Vector3 overShoulderPos = new Vector3(-0.24f, 1.20f, 0f);
        [Tooltip("蓄力位：几乎和收刀位重合，视觉上就是\"手握上刀柄、刀没出鞘\"")]
        public Vector3 guardPos = new Vector3(-0.02f, 1.12f, 0f);
        [Tooltip("-102 = 刀身指向后下方。别往正数调，那就又变成\"举剑\"了")]
        public float guardAngle = -102f;
        [Tooltip("起步时间：现在只是把手挪到刀柄上，很短")]
        public float drawTime = 0.05f;
        [Tooltip("出鞘后的层级（角色 20 之上）")]
        public int frontOrder = 22;
        [Tooltip("蓄力时刀身沿刀轴往外滑出的距离（刀身隐藏时不生效）")]
        public float chargeDrawOut = 0.04f;

        [Header("出刃：横向斩（南 → 东，掏刀横抹）")]
        [Tooltip("起刀握把位置：对齐攻击姿势的右手（胸高、略收回）")]
        public Vector3 cutStartPos = new Vector3(0.44f, 1.85f, 0f);
        [Tooltip("中段握把位置：向前推，横向位移的主体")]
        public Vector3 cutMidPos = new Vector3(0.58f, 1.88f, 0f);
        [Tooltip("收刀握把位置：对齐攻击姿势伸出去的右手")]
        public Vector3 cutEndPos = new Vector3(0.72f, 1.90f, 0f);
        [Tooltip("起手角比终点角大多少度（0 = 纯平移；20~30 = 略带上撩的横抹）")]
        public float cutSweepDegrees = 24f;
        [Tooltip("拔刀翻转占缓动进度的比例：这段刀身隐藏、只看到手在动")]
        [Range(0.05f, 0.6f)] public float drawFlipEnd = 0.30f;
        [Tooltip("92 = 剑尖指正前方（略偏下）；越大收得越深。横斩封顶 96，避免又变成下劈")]
        public float slashEndAngle = 92f;
        public float slashTime = 0.14f;
        [Tooltip("从斩击进度的这个比例开始才判定")]
        [Range(0f, 0.5f)] public float hitWindowStart = 0.10f;
        [Tooltip("缓动指数：越大越\"起手爆冲 + 末尾急停\"，打击感越重")]
        public float slashEase = 3.4f;

        [Header("收剑回腰")]
        public float sheatheTime = 0.12f;

        [Header("跟随身体前倾")]
        [Tooltip("挥剑时 PlayerVisual 会让身体前压，这里让剑绕握把跟着转，避免\"身体动了剑没动\"")]
        public bool followVisualLean = true;

        [Header("剑")]
        public SpriteRenderer blade;
        [Tooltip("剑刃基准长度（世界单位），范围增益会按倍率放大。角色身高约 2.4u，1.4 ≈ 身高 58%")]
        public float baseLength = 1.4f;
        [Range(0.1f, 3f)] public float lengthScale = 1f;

        [Header("拖尾")]
        [Tooltip("默认残影层数；蓄力阶段可以临时覆盖")]
        public int trailCount = 5;
        public Color trailColor = new Color(0.62f, 0.95f, 1f, 0.45f);

        [Header("音效 / 火花")]
        public string drawSfx = "sfx_iai_draw";
        public string slashSfx = "sfx_iai_slash";

        [Header("刀鞘（腰间收刀）")]
        [Tooltip("刀鞘的 SpriteRenderer：独立物件，永远钉在腰间收刀姿态上；留空会自动在 Player 下找名为 Sheath 的物体")]
        public SpriteRenderer sheath;
        [Tooltip("收刀时刀身的层级：比鞘低 1，鞘就会把刃盖住 → 看起来是\"刀收在鞘里\"")]
        public int sheathedBladeOrder = 18;

        [Header("居合表现（留空用素材总表）")]
        [Tooltip("刀光条：残影贴图（替代白块）")]
        public Sprite slashArc;
        [Tooltip("新月弧光：奥义时额外叠的弧光")]
        public Sprite slashCrescent;
        [Tooltip("横向居合刃光：横斩的视觉主体")]
        public Sprite iaiHorizontal;
        [Tooltip("蓄力能量环（3 帧循环）")]
        public Sprite[] chargeRing;
        [Tooltip("奥义释放的十字闪光")]
        public Sprite iaiFlash;
        [Header("横斩刃光摆位（贴图本身 6x2 世界单位）")]
        [Tooltip("刃光世界长度。太长会糊住角色，3.5~4.0 比较像刀风")]
        public float iaiHorizontalLength = 3.6f;
        [Tooltip("刃光世界厚度。宁可薄，厚了就是一团光斑")]
        public float iaiHorizontalThickness = 0.38f;
        [Tooltip("刃光中心相对玩家前移多少（必须大于半个身位，否则盖在躯干上）")]
        public float iaiHorizontalForward = 1.85f;
        [Tooltip("刃光高度（相对玩家根节点，和攻击姿势的右手同高）")]
        public float iaiHorizontalY = 1.90f;
        [Tooltip("刃光倾斜角（负 = 沿刀路略向下压；横斩中段刀约 105°，所以 -12 左右最贴）")]
        public float iaiHorizontalTilt = -12f;
        [Tooltip("出刀时向前的速度线条数")]
        public int slashSpeedLines = 5;

        const int MaxTrail = 8;

        // prop_sword：44px @PPU16 = 2.75u，pivot 在握把中段 → 握把到剑尖占 87%
        const float SpriteLength = 2.75f;
        const float PivotToTip = 0.87f;

        SpriteRenderer _sr;
        SpriteRenderer _ring;      // 蓄力能量环（跟着握把）
        PlayerVisual _visual;
        Phase _phase = Phase.Sheathed;
        bool _holdAfterDraw;
        float _timer;
        float _slashEndAngle;      // 运行时：蓄力阶段会把它推得更深
        float _chargeRatio;        // 运行时：蓄力表现用
        int _trailCount;
        int _stage;                // 运行时：当前蓄力阶段（0/1/2），决定刀光强度
        bool _slashFxDone;
        float _ringClock;
        float _drawTime, _slashTime, _sheatheTime;   // 运行时时长（不回写 public 字段）
        readonly SpriteRenderer[] _trail = new SpriteRenderer[MaxTrail];

        public float SlashEndAngle => _slashEndAngle;
        public int TrailCount => _trailCount;

        /// <summary>当前动作相位。</summary>
        public Phase CurrentPhase => _phase;

        /// <summary>正在走拔剑 / 持剑 / 劈 / 收剑任一阶段。</summary>
        public bool IsBusy => _phase != Phase.Sheathed;

        /// <summary>已经停在持剑位（蓄力中）。</summary>
        public bool IsGuard => _phase == Phase.Guard;

        /// <summary>只有这一段才做伤害判定（剑已经从腰间拔出来、正在往前扫）。</summary>
        public bool IsSlashing => _phase == Phase.Slash;

        /// <summary>
        /// 有效攻击窗口：Slash 阶段的前 <see cref="hitWindowStart"/> 不算（刀还没扫到人）。
        /// 判定统一看这个，不要直接看 IsSlashing。
        /// </summary>
        public bool IsInHitWindow =>
            _phase == Phase.Slash && _slashTime > 0f && _timer / _slashTime >= hitWindowStart;

        public bool IsSwinging => IsBusy;

        /// <summary>当前剑的朝向角（度，0 = 朝上）。</summary>
        public float CurrentAngle { get; private set; }

        /// <summary>配置的拔刀基准时长。</summary>
        public float DrawTime => Mathf.Max(0.01f, _drawTime > 0f ? _drawTime : drawTime);

        /// <summary>剑的可见长度（世界单位）。</summary>
        public float CurrentLength => baseLength * Mathf.Clamp(lengthScale, 0.2f, 3f);

        void Awake()
        {
            _sr = blade != null ? blade : GetComponentInChildren<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

            var art = ArtLibrary.Instance;
            if (art != null)
            {
                if (_sr.sprite == null) _sr.sprite = art.sword;
                if (slashArc == null) slashArc = art.slashArc;
                if (slashCrescent == null) slashCrescent = art.slashCrescent;
                if (iaiFlash == null) iaiFlash = art.iaiFlash;
                if (chargeRing == null || chargeRing.Length == 0) chargeRing = art.chargeRing;
            }

            _visual = GetComponentInParent<PlayerVisual>();

            _slashEndAngle = slashEndAngle;
            _trailCount = Mathf.Clamp(trailCount, 0, MaxTrail);
            ResetTiming();

            ResolveSheath();
            BuildRing();
            BuildTrail();
            ApplySheathed();
        }

        /// <summary>
        /// 找刀鞘。它必须是**独立物件**（不能是 Sword 的子物体），
        /// 否则拔刀时鞘会跟着剑一起飞出去。约定放在 Player 下，名字叫 Sheath。
        /// </summary>
        void ResolveSheath()
        {
            if (sheath == null && transform.parent != null)
            {
                Transform t = transform.parent.Find("Sheath");
                if (t != null) sheath = t.GetComponent<SpriteRenderer>();
            }

            if (sheath == null) sheath = CreateSheath();

            if (sheath != null)
            {
                var art = ArtLibrary.Instance;
                if (sheath.sprite == null && art != null) sheath.sprite = art.sheath;
                sheath.sortingOrder = sheathedOrder;
                sheath.enabled = true;
            }

            ApplySheathPose();
        }

        /// <summary>没有现成的鞘就在 Player 下建一个（场景/预制体里没有时兜底）。</summary>
        SpriteRenderer CreateSheath()
        {
            var art = ArtLibrary.Instance;
            if (art == null || art.sheath == null) return null;

            Transform parent = transform.parent != null ? transform.parent : null;
            var go = new GameObject("Sheath");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art.sheath;
            sr.sortingOrder = sheathedOrder;
            return sr;
        }

        /// <summary>蓄力能量环：不进 Sword 的子节点，避免被剑的缩放压扁。</summary>
        void BuildRing()
        {
            if (chargeRing == null || chargeRing.Length == 0) return;

            var go = new GameObject("ChargeRing");
            go.transform.SetParent(null, false);
            _ring = go.AddComponent<SpriteRenderer>();
            _ring.sprite = chargeRing[0];
            _ring.sortingOrder = frontOrder + 2;
            _ring.enabled = false;
        }

        void OnDestroy()
        {
            // 残影 / 能量环都是挂在场景根上的（世界空间），不主动清理会在场景里越积越多
            for (int i = 0; i < _trail.Length; i++)
                if (_trail[i] != null) Destroy(_trail[i].gameObject);
            if (_ring != null) Destroy(_ring.gameObject);
        }

        void BuildTrail()
        {
            var art = ArtLibrary.Instance;
            Sprite s = slashArc != null ? slashArc
                     : (art != null && art.white != null ? art.white : PixelArt.White);

            for (int i = 0; i < _trail.Length; i++)
            {
                var go = new GameObject("SwordTrail" + i);
                go.transform.SetParent(null, false);   // 世界空间：劈过去的弧光留在原地，不跟着剑转
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = s;
                sr.color = trailColor;
                sr.sortingOrder = frontOrder - 1;
                sr.enabled = false;
                _trail[i] = sr;
            }
        }

        // ---------------- 对外接口 ----------------

        /// <summary>把三个运行时时长复位成 Inspector 里配置的基准值。</summary>
        void ResetTiming()
        {
            _drawTime = Mathf.Max(0.03f, drawTime);
            _slashTime = Mathf.Max(0.06f, slashTime);
            _sheatheTime = Mathf.Max(0.05f, sheatheTime);
        }

        /// <summary>按速度倍率换算运行时时长（**不改 public 字段**，Inspector 里的手感参数不会被运行时覆盖）。</summary>
        void ApplySpeed(float speedScale)
        {
            float k = Mathf.Max(0.6f, speedScale);
            _drawTime = Mathf.Max(0.03f, drawTime / k);
            _slashTime = Mathf.Max(0.06f, slashTime / k);
            _sheatheTime = Mathf.Max(0.05f, sheatheTime / k);
        }

        /// <summary>开始一次完整挥砍（拔出 → 斩 → 收剑）。</summary>
        public void Play(float speedScale = 1f)
        {
            ApplySpeed(speedScale);

            _phase = Phase.Draw;
            _timer = 0f;
            _holdAfterDraw = false;
            _chargeRatio = 0f;
            _stage = 0;
            _slashFxDone = false;
            HideRing();

            // 起手时刀仍在鞘里（层级低于鞘且刀身隐藏），出鞘交给 TickSlash 按进度切换
            SetOrder(sheathedBladeOrder);
            SetBladeVisible(false);
            SetPose(sheathedPos, sheathedAngle);

            // 出鞘：柄部一小簇火花 + 金属摩擦声
            AudioManager.Play(drawSfx, 0.35f, 1.45f);
            SpawnDrawFlash();
        }

        /// <summary>只做"从腰间拔到持剑位"，停在 Guard。按下左键蓄力时调用。</summary>
        public void PlayDraw(float speedScale = 1f)
        {
            if (_phase != Phase.Sheathed && _phase != Phase.Guard) return;

            ApplySpeed(speedScale);

            _phase = Phase.Draw;
            _timer = 0f;
            _holdAfterDraw = true;
            _chargeRatio = 0f;
            _slashFxDone = false;
            HideRing();
            // 只是把手挪到刀柄上，刀还在鞘里
            SetOrder(sheathedBladeOrder);
            SetBladeVisible(false);
            SetPose(sheathedPos, sheathedAngle);

            AudioManager.Play(drawSfx, 0.35f, 1.45f);
            SpawnDrawFlash();
        }

        /// <summary>从持剑位向前上撩斩（斩完自动收刀）。蓄力释放时调用。</summary>
        public void PlaySlash(float speedScale = 1f)
        {
            if (_phase != Phase.Guard && _phase != Phase.Sheathed) return;

            ApplySpeed(speedScale);

            _phase = Phase.Slash;
            _timer = 0f;
            _holdAfterDraw = false;
            _chargeRatio = 0f;
            _slashFxDone = false;
            HideRing();
            // 从腰侧起刀：翻转段刀身隐藏，翻转结束后才在身前出现
            SetOrder(sheathedBladeOrder);
            SetBladeVisible(false);
            SetPose(guardPos, guardAngle);

            AudioManager.Play(slashSfx, 0.4f, 1.25f);
            SpawnSlashFx();
        }

        /// <summary>蓄力时的姿态：沿刀轴把刀往外带一点点，让鞘口露出一线钢。</summary>
        Vector3 ChargePos(float ratio)
        {
            // dir(-100°) ≈ (-0.985, -0.174)，取反即"往鞘口外抽"的方向
            Vector3 outDir = new Vector3(0.985f, 0.174f, 0f);
            return guardPos + outDir * (chargeDrawOut * ratio);
        }

        /// <summary>
        /// 蓄力表现：刀身冷光渐亮 + 刀沿刀轴微微出鞘 + 腰侧转能量环。
        /// 只在 Guard 阶段生效，不会干扰正在进行的斩击。
        /// </summary>
        public void SetChargeVisual(float ratio)
        {
            _chargeRatio = Mathf.Clamp01(ratio);

            if (_phase != Phase.Guard)
            {
                if (_sr != null) _sr.color = Color.white;
                HideRing();
                return;
            }

            // 蓄力时刀仍"在鞘里"（刀身隐藏），只把鞘口那一线留着
            SetOrder(sheathedBladeOrder);
            SetBladeVisible(false);
            SetPose(ChargePos(_chargeRatio), guardAngle);

            if (_sr != null)
            {
                Color cold = new Color(0.72f, 0.95f, 1f, 1f);
                _sr.color = Color.Lerp(Color.white, cold, _chargeRatio);
            }

            UpdateRing();
        }

        /// <summary>当前蓄力阶段（0 快斩 / 1 强斩 / 2 奥义）。由 IaiCharge 在释放前写入。</summary>
        public void SetStage(int stage)
        {
            _stage = Mathf.Clamp(stage, 0, 2);
        }

        /// <summary>覆盖斩击终点角度（蓄力越高扫得更深，但横斩封顶 96° = 近水平）。</summary>
        public void SetSlashEndAngle(float angle)
        {
            _slashEndAngle = Mathf.Clamp(angle, 70f, 96f);
        }

        /// <summary>覆盖残影层数（蓄力越高越密）。</summary>
        public void SetTrailCount(int count)
        {
            _trailCount = Mathf.Clamp(count, 0, MaxTrail);
        }

        public void Cancel()
        {
            _phase = Phase.Sheathed;
            _timer = 0f;
            _holdAfterDraw = false;
            _chargeRatio = 0f;
            _stage = 0;
            _slashFxDone = false;
            // 剑长倍率只该在这一次出刀里生效；收刀后复位，免得刀鞘被撑大
            lengthScale = 1f;
            HideTrail();
            HideRing();
            ApplySheathed();
        }

        /// <summary>等价于 Cancel，保留旧名字给外部调用。</summary>
        public void Stop() { Cancel(); }

        /// <summary>调试用：把剑钉在某个阶段的某个进度上（t=0~1），方便截图对比。</summary>
        public void DebugPose(Phase phase, float t)
        {
            _phase = phase;
            SetOrder(phase == Phase.Sheathed ? sheathedBladeOrder : frontOrder);

            t = Mathf.Clamp01(t);

            // 把计时器一起对上，这样即使 Update 继续推进（timeScale 不为 0）姿态也一致
            _timer = phase == Phase.Draw ? _drawTime * t
                   : phase == Phase.Slash ? _slashTime * t
                   : phase == Phase.Sheathe ? _sheatheTime * t
                   : 0f;
            switch (phase)
            {
                case Phase.Draw:
                    SetPose(Bezier(sheathedPos, overShoulderPos, guardPos, t),
                            Mathf.Lerp(sheathedAngle, guardAngle, t));
                    break;
                case Phase.Slash:
                    Vector3 sp;
                    float sa;
                    SlashPose(t, out sp, out sa);
                    SetPose(sp, sa);
                    // 拔刀翻转段刀身隐藏；翻转结束后才在身前出现，避免看到刀在身后转半圈
                    bool front = EaseSlash(t) >= drawFlipEnd;
                    SetOrder(front ? frontOrder : sheathedBladeOrder);
                    SetBladeVisible(front);
                    break;
                case Phase.Sheathe:
                    SetPose(Bezier(cutEndPos, overShoulderPos, sheathedPos, t),
                            Mathf.Lerp(SlashEndAngleUnwrapped, sheathedAngle, t));
                    break;
                default:
                    ApplySheathed();
                    break;
            }
        }

        // ---------------- 每帧推进 ----------------

        void Update()
        {
            if (_phase == Phase.Sheathed) return;

            _timer += Time.deltaTime;

            switch (_phase)
            {
                case Phase.Draw: TickDraw(); break;
                case Phase.Guard: SetPose(ChargePos(_chargeRatio), guardAngle); break;
                case Phase.Slash: TickSlash(); break;
                case Phase.Sheathe: TickSheathe(); break;
            }
        }

        void TickDraw()
        {
            float t = Mathf.Clamp01(_timer / Mathf.Max(0.001f, _drawTime));
            float e = Mathf.SmoothStep(0f, 1f, t);

            SetPose(Bezier(sheathedPos, overShoulderPos, guardPos, e),
                    Mathf.Lerp(sheathedAngle, guardAngle, e));

            if (t < 1f) return;

            // 拔到持剑位就停住（蓄力用）；完整攻击流程则直接接着斩
            if (_holdAfterDraw)
            {
                _phase = Phase.Guard;
                _timer = 0f;
                SetPose(guardPos, guardAngle);
                SetBladeVisible(false);
                return;
            }

            _phase = Phase.Slash;
            _timer = 0f;
            _slashFxDone = false;
            AudioManager.Play(slashSfx, 0.4f, 1.25f);
            SpawnSlashFx();
        }

        /// <summary>
        /// 斩击缓动：起手爆冲、末尾急停（n 越大越狠）。"有力气"主要靠这条曲线。
        /// </summary>
        float EaseSlash(float t)
        {
            return 1f - Mathf.Pow(1f - t, Mathf.Max(1.2f, slashEase));
        }

        /// <summary>
        /// 终点角度往**负方向**连续展开：92° → -268°，这样插值一路递减、
        /// 不会绕到北边那条弧上去。
        /// </summary>
        float SlashEndAngleUnwrapped => _slashEndAngle - 360f;

        /// <summary>
        /// 横斩起手角：终点角 + 30°（仍是负方向展开的表示）。
        /// 约 -238° = 122°，比终点低 30°，所以可见段是"从下前方抹到正前方"。
        /// </summary>
        float CutStartAngle => SlashEndAngleUnwrapped + cutSweepDegrees;

        void SlashPose(float t, out Vector3 pos, out float angle)
        {
            float e = EaseSlash(t);

            // 第一段：拔刀翻转。刀还在腰侧、整段隐藏，只有手握把在动；
            // 这一下把 132° 的翻身一次走完，避免可见段里出现"竖直向下"。
            if (e < drawFlipEnd)
            {
                float u = e / Mathf.Max(0.001f, drawFlipEnd);
                pos = Vector3.Lerp(guardPos, cutStartPos, u);
                angle = Mathf.Lerp(guardAngle, CutStartAngle, u);
                return;
            }

            // 第二段：可见的横抹。只转 cutSweepDegrees，主体靠握把向前横移。
            float v = (e - drawFlipEnd) / Mathf.Max(0.001f, 1f - drawFlipEnd);
            pos = Bezier(cutStartPos, cutMidPos, cutEndPos, v);
            angle = Mathf.Lerp(CutStartAngle, SlashEndAngleUnwrapped, v);
        }

        void TickSlash()
        {
            float t = Mathf.Clamp01(_timer / Mathf.Max(0.001f, _slashTime));

            Vector3 pos;
            float angle;
            SlashPose(t, out pos, out angle);
            SetPose(pos, angle);

            // 拔刀翻转段刀身隐藏（只看到手在动），翻转结束才在身前出现
            bool front = EaseSlash(t) >= drawFlipEnd;
            SetOrder(front ? frontOrder : sheathedBladeOrder);
            SetBladeVisible(front);

            UpdateTrail(t);

            if (t < 1f) return;

            _phase = Phase.Sheathe;
            _timer = 0f;
        }

        void TickSheathe()
        {
            float t = Mathf.Clamp01(_timer / Mathf.Max(0.001f, _sheatheTime));
            float e = Mathf.SmoothStep(0f, 1f, t);

            SetPose(Bezier(cutEndPos, overShoulderPos, sheathedPos, e),
                    Mathf.Lerp(SlashEndAngleUnwrapped, sheathedAngle, e));

            // 剑过身后一半时切回"被身体遮挡"的层级，并把刀身收起来（只剩鞘）
            if (t > 0.3f) SetOrder(sheathedBladeOrder);
            if (t > 0.55f) SetBladeVisible(false);

            HideTrail();

            if (t >= 1f) Cancel();
        }

        // ---------------- 姿态 ----------------

        /// <summary>二次贝塞尔：拔剑 / 收剑走一条绕过肩膀的弧，而不是直线穿身体。</summary>
        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            return Vector3.Lerp(Vector3.Lerp(a, b, t), Vector3.Lerp(b, c, t), t);
        }

        /// <summary>角度 → 剑尖方向（0 = 朝上，正角度偏右）。</summary>
        static Vector3 DirOf(float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f);
        }

        void ApplySheathed()
        {
            SetPose(sheathedPos, sheathedAngle);
            SetOrder(sheathedBladeOrder);
            SetSheathVisible(true);
            // 收刀时刀身整个收进鞘里：直接隐藏，否则鞘没盖住的部分（尤其刃尖）会露出来
            SetBladeVisible(false);
            if (_sr != null) _sr.color = Color.white;
        }

        void SetBladeVisible(bool visible)
        {
            if (_sr == null) return;
            _sr.enabled = hideBladeWhenSheathed ? visible : true;
        }

        void SetSheathVisible(bool visible)
        {
            if (sheath != null) sheath.enabled = visible;
        }

        /// <summary>把鞘钉在腰间收刀姿态上。鞘是独立物件，拔刀时留在原地不动。</summary>
        void ApplySheathPose()
        {
            if (sheath == null) return;

            sheath.transform.localPosition = sheathedPos;
            sheath.transform.localRotation = Quaternion.Euler(0f, 0f, -sheathedAngle - VisualLean);

            float s = CurrentLength / (SpriteLength * PivotToTip);
            sheath.transform.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>身体前倾角（度）：让剑绕握把跟着身体转，而不是身体动了剑不动。</summary>
        float VisualLean
        {
            get { return (followVisualLean && _visual != null) ? _visual.CurrentLean : 0f; }
        }

        void SetPose(Vector3 localPos, float angle)
        {
            CurrentAngle = angle;

            transform.localPosition = localPos;
            // 0° = 朝上，正角度往右扫；再叠加身体前倾，避免剑和身体脱节
            transform.localRotation = Quaternion.Euler(0f, 0f, -angle - VisualLean);

            // 剑贴图 44px @PPU16 = 2.75u；pivot 在握把(0.13)，所以"握把→剑尖"只占 87%
            float s = CurrentLength / (SpriteLength * PivotToTip);
            transform.localScale = new Vector3(s, s, 1f);

            // 鞘永远钉在腰间，位置/缩放都跟收刀姿态走
            ApplySheathPose();
        }

        void SetOrder(int order)
        {
            if (_sr != null) _sr.sortingOrder = order;
        }

        void HideTrail()
        {
            for (int i = 0; i < _trail.Length; i++)
                if (_trail[i] != null) _trail[i].enabled = false;
        }

        /// <summary>刀光贴图的世界尺寸（把 localScale 换算成"想要的世界长度"用）。</summary>
        Vector2 TrailSpriteSize()
        {
            var sr = _trail[0];
            if (sr == null || sr.sprite == null) return new Vector2(1f, 1f);
            Vector3 b = sr.sprite.bounds.size;
            return new Vector2(Mathf.Max(0.0001f, b.x), Mathf.Max(0.0001f, b.y));
        }

        void UpdateTrail(float t)
        {
            Vector2 fit = TrailSpriteSize();

            // 残影沿"来路"回退。现在是"南 → 东"的下潜上撩：角度一路递减，
            // 所以更早的角度 = 更大的角度（往 +back 走），和旧版方向相反。
            for (int i = 0; i < _trail.Length; i++)
            {
                var sr = _trail[i];
                if (sr == null) continue;

                if (i >= _trailCount || t <= hitWindowStart * 0.4f)
                {
                    sr.enabled = false;
                    continue;
                }

                // 残影只画横抹这一段：起手角之前的残影属于"拔刀翻转"，画出来会多一条向下的扇面
                float back = (i + 1) * 7f;
                float a = CurrentAngle + back;   // 角度递减，回退 = 加大
                if (a > CutStartAngle)
                {
                    sr.enabled = false;
                    continue;
                }

                sr.enabled = true;
                float len = CurrentLength;
                Vector3 dir = DirOf(a);

                sr.transform.position = transform.position + dir * (len * 0.60f);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, -a);
                // 按贴图原始世界尺寸归一化：贴图换成刀光条后长度口径不变
                sr.transform.localScale = new Vector3(
                    (len * 1.10f) / fit.x,
                    0.18f / fit.y,
                    1f);

                Color c = trailColor;
                // 越靠后的残影越淡；按实际层数归一化，避免层数多了出现负 alpha
                float fade = 1f - i / (float)Mathf.Max(1, _trailCount);
                c.a *= Mathf.Max(0f, fade) * Mathf.Min(1f, t * 3f);
                sr.color = c;
            }
        }

        // ---------------- 居合专属表现 ----------------

        /// <summary>蓄力能量环：跟着握把转，越蓄越大越亮。</summary>
        void UpdateRing()
        {
            if (_ring == null) return;
            if (_phase != Phase.Guard || _chargeRatio <= 0.01f)
            {
                HideRing();
                return;
            }

            _ringClock += Time.deltaTime;

            if (chargeRing != null && chargeRing.Length > 1)
                _ring.sprite = chargeRing[(int)(_ringClock * 14f) % chargeRing.Length];

            _ring.transform.position = transform.position;
            _ring.transform.rotation = Quaternion.Euler(0f, 0f, _ringClock * 70f);

            float s = 0.40f + 0.22f * _chargeRatio;
            _ring.transform.localScale = new Vector3(s, s, 1f);

            Color c = Color.Lerp(new Color(0.55f, 0.9f, 1f, 0.35f), new Color(1f, 1f, 1f, 0.95f), _chargeRatio);
            _ring.color = c;
            _ring.sortingOrder = frontOrder + 2;
            _ring.enabled = true;
        }

        void HideRing()
        {
            if (_ring != null) _ring.enabled = false;
        }

        /// <summary>释放瞬间：横贯的居合刃光 + 速度线；奥义额外叠十字闪光。</summary>
        void SpawnSlashFx()
        {
            if (_slashFxDone) return;
            _slashFxDone = true;

            var art = ArtLibrary.Instance;
            if (iaiHorizontal == null && art != null) iaiHorizontal = art.iaiHorizontal;
            if (slashCrescent == null && art != null) slashCrescent = art.slashCrescent;
            if (iaiFlash == null && art != null) iaiFlash = art.iaiFlash;

            // 刃光钉在**身前**的横抹轨迹上：中心必须离开躯干，否则整块贴图盖住人物
            Vector3 playerPos = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 streak = playerPos + new Vector3(iaiHorizontalForward, iaiHorizontalY, 0f);

            // 横斩只用横贯刃光。新月是"圆形弧光"，放在横斩里会把画面读成转圈，删掉。
            if (iaiHorizontal != null)
            {
                Vector2 b = iaiHorizontal.bounds.size;
                FxSprite fx = FxSprite.Spawn(iaiHorizontal, streak, new Vector2(2.2f + 0.6f * _stage, 0f),
                    new Color(1f, 1f, 1f, 0.92f),
                    0.12f + 0.04f * _stage, 1f, frontOrder + 2);
                fx.drag = 0f;
                fx.grow = 0f;
                fx.shrink = false;
                fx.transform.rotation = Quaternion.Euler(0f, 0f, iaiHorizontalTilt - 2f * _stage);
                // 按贴图世界尺寸直接换算成"想要的长度/厚度"（FxSprite 的 grow 会覆盖各向异性缩放，所以不用 grow）
                fx.transform.localScale = new Vector3(
                    iaiHorizontalLength / Mathf.Max(0.001f, b.x),
                    iaiHorizontalThickness / Mathf.Max(0.001f, b.y),
                    1f);
            }

            if (iaiFlash != null && _stage >= 2)
            {
                FxSprite flash = FxSprite.Spawn(iaiFlash, streak + new Vector3(-0.45f, 0f, 0f), Vector2.zero,
                    new Color(1f, 1f, 1f, 0.95f), 0.16f, 0.7f, frontOrder + 3);
                flash.drag = 0f;
                flash.grow = 1.6f;
                flash.spin = 45f;
            }

            SpawnSpeedLines(streak);
        }

        /// <summary>斩击瞬间向前甩出的速度线，制造"被刀风撕开"的感觉。</summary>
        void SpawnSpeedLines(Vector3 origin)
        {
            if (slashSpeedLines <= 0) return;
            var art = ArtLibrary.Instance;
            Sprite white = art != null ? art.white : PixelArt.White;
            if (white == null) return;

            // ui_white 是 1x1 像素 @PPU16 = 0.0625 世界单位，缩放要按这个换算，
            // 否则速度线小到看不见。
            Vector2 unit = Vector2.one * 0.0625f;
            if (white.bounds.size.x > 0.0001f) unit = new Vector2(white.bounds.size.x, white.bounds.size.y);

            for (int i = 0; i < slashSpeedLines; i++)
            {
                float y = Random.Range(-0.28f, 0.42f);
                float len = Random.Range(1.5f, 2.7f);
                float thick = Random.Range(0.035f, 0.055f);
                // 围绕刃光中心散开，并跟刃光同一个倾角，否则会读成"飘着的碎线"
                Vector3 at = origin + new Vector3(Random.Range(-0.55f, 0.45f), y, 0f);

                FxSprite fx = FxSprite.Spawn(white, at, new Vector2(Random.Range(10f, 21f), 0f),
                    new Color(0.78f, 0.98f, 1f, 0.62f),
                    Random.Range(0.08f, 0.14f), 1f, frontOrder + 1);
                fx.drag = 0.6f;
                fx.shrink = false;
                fx.transform.rotation = Quaternion.Euler(0f, 0f, iaiHorizontalTilt);
                fx.transform.localScale = new Vector3(len / unit.x, thick / unit.y, 1f);
            }
        }

        void SpawnDrawFlash()
        {
            ArtLibrary art = ArtLibrary.Instance;
            if (art == null || art.hitFrames == null || art.hitFrames.Length == 0) return;

            Vector3 world = transform.parent != null
                ? transform.parent.TransformPoint(sheathedPos)
                : transform.position;

            for (int i = 0; i < 3; i++)
            {
                FxSprite fx = FxSprite.Spawn(
                    art.hitFrames[Random.Range(0, art.hitFrames.Length)],
                    world + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.2f), 0f),
                    new Vector2(Random.Range(-4f, 4f), Random.Range(1f, 5f)),
                    new Color(0.85f, 0.95f, 1f, 0.9f),
                    0.2f, 1.2f, frontOrder + 1);
                fx.drag = 4f;
                fx.shrink = true;
            }
        }

        /// <summary>
        /// 剑身上用于判定的采样点（世界坐标）。
        /// 五点均布 + 判定半径，整条刀身形成一条连续的命中带
        /// （这样"只要进了刀光范围就能打到"，而不是只有三个点能打中）。
        /// </summary>
        public const int SampleCount = 5;

        public void SamplePoints(Vector3[] buffer)
        {
            if (buffer == null || buffer.Length < SampleCount) return;

            float len = CurrentLength;
            Vector3 dir = DirOf(CurrentAngle);
            Vector3 origin = transform.position;

            for (int i = 0; i < SampleCount; i++)
            {
                float u = Mathf.Lerp(0.22f, 1f, i / (float)(SampleCount - 1));
                buffer[i] = origin + dir * (len * u);
            }
        }
    }
}
