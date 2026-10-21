using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 剑的完整动作循环：**背在背后 → 拔出举剑 → 自上而下一劈 → 收回背后**。
    ///
    /// 参考的是《死亡细胞》《空洞骑士》那类手感：
    /// 1. **武器常驻可见**（待机时插在背上），不是招之即来的光效；
    /// 2. **出鞘有极短的过渡**（约 0.07s）：剑从背后斜插位置抽到头顶，越过这个过程才开始判定，
    ///    所以不会出现"还没拔出来就把人砍了"；
    /// 3. **下劈是从北到南的大弧**（-20° → 118°，约 138° 扫角），起手快、末尾收住，
    ///    弧线覆盖"头顶 → 正前 → 前下方"，既能打到贴地的怪也能打到跳跃高度的怪；
    /// 4. **劈完一定收剑回背**，而不是淡出消失。
    ///
    /// 判定不在这里做——这里只负责"剑现在在哪、是不是在劈"，
    /// 由 <see cref="PlayerCombat"/> 按剑身采样点逐帧检测。
    ///
    /// 角度约定：0 = 剑尖朝上（北），正角度 = 朝右（顺时针），90 = 正前方，118 = 前下方。
    /// </summary>
    public class SwordSwing : MonoBehaviour
    {
        public enum Phase { Sheathed, Draw, Slash, Sheathe }

        [Header("待机：插在背后")]
        [Tooltip("剑柄相对玩家的位置（负 X = 身后）")]
        public Vector3 sheathedPos = new Vector3(-0.46f, 0.94f, 0f);
        [Tooltip("插在背后的倾角：负 = 剑尖朝左上后方")]
        public float sheathedAngle = -37f;
        [Tooltip("待机时的层级：要低于角色（角色 20），剑才会被身体挡住")]
        public int sheathedOrder = 19;

        [Header("拔出：举剑过头")]
        [Tooltip("拔剑路径的中间点（越过肩膀），让剑是\"抽\"出来而不是穿过身体")]
        public Vector3 overShoulderPos = new Vector3(0.02f, 1.58f, 0f);
        public Vector3 guardPos = new Vector3(0.22f, 1.14f, 0f);
        public float guardAngle = -18f;
        [Tooltip("出鞘时间：越短越利落")]
        public float drawTime = 0.075f;
        [Tooltip("举起后切到角色前方（角色 20 之上）")]
        public int frontOrder = 22;

        [Header("下劈：从北到南")]
        public Vector3 slashEndPos = new Vector3(0.40f, 0.96f, 0f);
        [Tooltip("118 = 剑尖指向前下方")]
        public float slashEndAngle = 118f;
        public float slashTime = 0.17f;

        [Header("收剑回背")]
        public float sheatheTime = 0.15f;

        [Header("剑")]
        public SpriteRenderer blade;
        [Tooltip("剑刃基准长度（世界单位），范围增益会按倍率放大")]
        public float baseLength = 1.75f;
        [Range(0.1f, 3f)] public float lengthScale = 1f;

        [Header("拖尾")]
        public int trailCount = 3;
        public Color trailColor = new Color(0.62f, 0.95f, 1f, 0.45f);

        [Header("音效 / 火花")]
        public string drawSfx = "sfx_impact";
        public string slashSfx = "sfx_dash";

        SpriteRenderer _sr;
        Phase _phase = Phase.Sheathed;
        float _timer;
        readonly SpriteRenderer[] _trail = new SpriteRenderer[4];

        /// <summary>正在走拔剑 / 劈 / 收剑任一阶段。</summary>
        public bool IsBusy => _phase != Phase.Sheathed;

        /// <summary>只有这一段才做伤害判定（剑已经从背后抽出来、正在往下劈）。</summary>
        public bool IsSlashing => _phase == Phase.Slash;

        public bool IsSwinging => IsBusy;

        /// <summary>当前剑的朝向角（度，0 = 朝上）。</summary>
        public float CurrentAngle { get; private set; }

        /// <summary>剑的可见长度（世界单位）。</summary>
        public float CurrentLength => baseLength * Mathf.Clamp(lengthScale, 0.2f, 3f);

        void Awake()
        {
            _sr = blade != null ? blade : GetComponentInChildren<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

            var art = ArtLibrary.Instance;
            if (_sr.sprite == null && art != null) _sr.sprite = art.sword;

            BuildTrail();
            ApplySheathed();
        }

        void OnDestroy()
        {
            // 残影是挂在场景根上的（世界空间），不主动清理会在场景里越积越多
            for (int i = 0; i < _trail.Length; i++)
                if (_trail[i] != null) Destroy(_trail[i].gameObject);
        }

        void BuildTrail()
        {
            var art = ArtLibrary.Instance;
            for (int i = 0; i < _trail.Length; i++)
            {
                var go = new GameObject("SwordTrail" + i);
                go.transform.SetParent(null, false);   // 世界空间：劈过去的弧光留在原地，不跟着剑转
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = art != null && art.white != null ? art.white : PixelArt.White;
                sr.color = trailColor;
                sr.sortingOrder = frontOrder - 1;
                sr.enabled = false;
                _trail[i] = sr;
            }
        }

        // ---------------- 对外接口 ----------------

        /// <summary>开始一次完整挥砍（拔出 → 劈 → 收剑）。</summary>
        public void Play(float speedScale = 1f)
        {
            float k = Mathf.Max(0.6f, speedScale);

            drawTime = Mathf.Max(0.03f, 0.075f / k);
            slashTime = Mathf.Max(0.06f, 0.17f / k);
            sheatheTime = Mathf.Max(0.05f, 0.15f / k);

            _phase = Phase.Draw;
            _timer = 0f;

            SetOrder(frontOrder);
            SetPose(sheathedPos, sheathedAngle);

            // 出鞘：柄部一小簇火花 + 金属摩擦声
            AudioManager.Play(drawSfx, 0.35f, 1.45f);
            SpawnDrawFlash();
        }

        /// <summary>强制回到"背在背后"（暂停 / 死亡 / 切状态时调用）。</summary>
        public void Cancel()
        {
            _phase = Phase.Sheathed;
            _timer = 0f;
            HideTrail();
            ApplySheathed();
        }

        /// <summary>等价于 Cancel，保留旧名字给外部调用。</summary>
        public void Stop() { Cancel(); }

        /// <summary>调试用：把剑钉在某个阶段的某个进度上（t=0~1），方便截图对比。</summary>
        public void DebugPose(Phase phase, float t)
        {
            _phase = phase;
            SetOrder(phase == Phase.Sheathed ? sheathedOrder : frontOrder);

            t = Mathf.Clamp01(t);

            // 把计时器一起对上，这样即使 Update 继续推进（timeScale 不为 0）姿态也一致
            _timer = phase == Phase.Draw ? drawTime * t
                   : phase == Phase.Slash ? slashTime * t
                   : phase == Phase.Sheathe ? sheatheTime * t
                   : 0f;
            switch (phase)
            {
                case Phase.Draw:
                    SetPose(Bezier(sheathedPos, overShoulderPos, guardPos, t),
                            Mathf.Lerp(sheathedAngle, guardAngle, t));
                    break;
                case Phase.Slash:
                    float e = 1f - Mathf.Pow(1f - t, 2.6f);
                    SetPose(Vector3.Lerp(guardPos, slashEndPos, e),
                            Mathf.Lerp(guardAngle, slashEndAngle, e));
                    break;
                case Phase.Sheathe:
                    SetPose(Bezier(slashEndPos, overShoulderPos, sheathedPos, t),
                            Mathf.Lerp(slashEndAngle, sheathedAngle, t));
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
                case Phase.Slash: TickSlash(); break;
                case Phase.Sheathe: TickSheathe(); break;
            }
        }

        void TickDraw()
        {
            float t = Mathf.Clamp01(_timer / drawTime);
            float e = Mathf.SmoothStep(0f, 1f, t);

            SetPose(Bezier(sheathedPos, overShoulderPos, guardPos, e),
                    Mathf.Lerp(sheathedAngle, guardAngle, e));

            if (t < 1f) return;

            _phase = Phase.Slash;
            _timer = 0f;
            AudioManager.Play(slashSfx, 0.4f, 1.25f);
        }

        void TickSlash()
        {
            float t = Mathf.Clamp01(_timer / slashTime);

            // 下劈的节奏：起手极快（蓄力已过），末尾明显减速收住
            float e = 1f - Mathf.Pow(1f - t, 2.6f);

            SetPose(Vector3.Lerp(guardPos, slashEndPos, e),
                    Mathf.Lerp(guardAngle, slashEndAngle, e));

            UpdateTrail(t);

            if (t < 1f) return;

            _phase = Phase.Sheathe;
            _timer = 0f;
        }

        void TickSheathe()
        {
            float t = Mathf.Clamp01(_timer / sheatheTime);
            float e = Mathf.SmoothStep(0f, 1f, t);

            SetPose(Bezier(slashEndPos, overShoulderPos, sheathedPos, e),
                    Mathf.Lerp(slashEndAngle, sheathedAngle, e));

            // 剑过身后一半时切回"被身体遮挡"的层级
            if (t > 0.45f) SetOrder(sheathedOrder);

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
            SetOrder(sheathedOrder);
            if (_sr != null) _sr.enabled = true;   // 待机时也一直看得见
        }

        void SetPose(Vector3 localPos, float angle)
        {
            CurrentAngle = angle;

            transform.localPosition = localPos;
            transform.localRotation = Quaternion.Euler(0f, 0f, -angle);   // 0° = 朝上，正角度往右扫

            // 剑贴图 44px @PPU16 = 2.75u；pivot 在握把(0.13)，所以"握把→剑尖"只占 87%
            const float spriteLen = 2.75f;
            const float pivotToTip = 0.87f;
            float s = CurrentLength / (spriteLen * pivotToTip);
            transform.localScale = new Vector3(s, s, 1f);

            if (_sr != null) _sr.enabled = true;
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

        void UpdateTrail(float t)
        {
            // 残影沿"来路"回退：下劈是从上往下，所以更早的角度 = 更靠上
            for (int i = 0; i < _trail.Length; i++)
            {
                var sr = _trail[i];
                if (sr == null) continue;

                if (t <= 0.05f)
                {
                    sr.enabled = false;
                    continue;
                }

                float back = (i + 1) * 11f;
                float a = CurrentAngle - back;
                if (a < guardAngle) { sr.enabled = false; continue; }

                sr.enabled = true;
                float len = CurrentLength;
                Vector3 dir = DirOf(a);

                sr.transform.position = transform.position + dir * (len * 0.55f);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, -a);
                sr.transform.localScale = new Vector3(len * 0.85f, 0.07f, 1f);

                Color c = trailColor;
                c.a *= (1f - i * 0.3f) * Mathf.Min(1f, t * 3f);
                sr.color = c;
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

        /// <summary>剑身上用于判定的采样点（世界坐标）：从柄到尖。</summary>
        public void SamplePoints(Vector3[] buffer)
        {
            float len = CurrentLength;
            Vector3 dir = DirOf(CurrentAngle);
            Vector3 origin = transform.position;

            if (buffer != null && buffer.Length >= 3)
            {
                buffer[0] = origin + dir * (len * 0.35f);
                buffer[1] = origin + dir * (len * 0.65f);
                buffer[2] = origin + dir * (len * 0.95f);
            }
        }
    }
}
