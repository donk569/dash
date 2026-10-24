using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 验证用 UI（IMGUI），**像素风**：硬边矩形面板 + 位图数字 + 文字 2px 硬阴影。
    ///
    /// 做法上的几个取舍：
    /// - 面板不贴图、不圆角、不渐变，全部用整数像素矩形画（外框 2px 亮 / 内 1px 暗 / 四角像素点），
    ///   这样在任何分辨率下边缘都是硬的，不会有现代 UI 那种柔和感。
    /// - 数字用 3x5 点阵放大 2 倍的位图字体（ui_digits），中文仍走系统字体但加硬阴影。
    /// - 字体图集强制 Point 采样，避免中文边缘被双线性糊掉。
    /// - 进度条按 8px 分段，填充色带 1px 顶部高光，像老游戏的 HP 条。
    /// </summary>
    public class PrototypeUI : MonoBehaviour
    {
        public PlayerHealth health;
        public DistanceTracker tracker;
        public KillReward killReward;
        ShopIcon[] _shops;
        Sprite _dashIcon;
        public UpgradeManager upgrades;
        public DashController dash;
        public PlayerCombat combat;
        public InputHandler input;
        public LevelManager levelManager;
        public PlayerWallet wallet;
        public IaiCharge iai;
        public ScoreKeeper score;

        // 连击数字的动画状态：加链瞬间放大回弹 + 一圈扩散冲击环
        const float ComboPunchTime = 0.16f;
        const float ComboRingTime = 0.30f;
        const float ComboBaseScale = 2.0f;
        int _lastComboChain;
        float _comboPunchUntil;
        float _comboRingFrom = -99f;

        [Header("商店")]
        [Tooltip("由 ShopIcon 在打开时写入")]
        public int currentShopPrice = 3;

        const float VirtualHeight = 720f;

        // ---- 调色板 ----
        static readonly Color Cyan = new Color32(143, 248, 255, 255);
        static readonly Color CyanMid = new Color32(42, 107, 130, 255);
        static readonly Color CyanDim = new Color32(26, 58, 74, 255);
        static readonly Color PanelBg = new Color32(11, 18, 32, 245);
        static readonly Color PanelBgHi = new Color32(22, 34, 54, 245);
        static readonly Color Deep = new Color32(6, 10, 18, 255);
        static readonly Color Yellow = new Color32(255, 209, 102, 255);
        static readonly Color Purple = new Color32(186, 140, 255, 255);
        static readonly Color White = Color.white;
        static readonly Color TextDim = new Color32(168, 186, 206, 255);
        static readonly Color Shadow = new Color32(0, 0, 0, 190);

        [Header("像素 UI 贴图（留空用素材总表）")]
        public Sprite heart;
        public Sprite coinSprite;
        public Sprite digitSheet;
        public Sprite scanline;
        [Tooltip("UI 中文字体（Zpix 12px 像素字体）。字号必须是 12 的倍数")]
        public Font uiFont;
        [Tooltip("蓄力条凹槽 / 发光填充（留空用素材总表）")]
        public Sprite uiChargeFrame;
        public Sprite uiChargeFill;

        // 位图字体参数，必须和 _AssetInbox/gen_pixel_ui.py 一致
        const int DigitW = 6;
        const int DigitH = 10;
        const int DigitPad = 1;
        const string DigitOrder = "0123456789/.smx";

        Texture2D _white;
        Texture2D _digitTex;
        Font _font;

        GUIStyle _hudText;
        GUIStyle _hudTextR;
        GUIStyle _title;
        GUIStyle _sub;
        GUIStyle _cardTitle;
        GUIStyle _cardDesc;
        GUIStyle _center;
        GUIStyle _small;
        GUIStyle _countdown;

        readonly List<GUIStyle> _styles = new List<GUIStyle>();

        void EnsureInit()
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
                _white.filterMode = FilterMode.Point;
            }

            if (digitSheet == null && ArtLibrary.Instance != null) digitSheet = ArtLibrary.Instance.uiDigits;
            if (scanline == null && ArtLibrary.Instance != null) scanline = ArtLibrary.Instance.uiScanline;
            if (heart == null && ArtLibrary.Instance != null) heart = ArtLibrary.Instance.heart;
            if (coinSprite == null && ArtLibrary.Instance != null) coinSprite = ArtLibrary.Instance.coin;
            if (wallet == null) wallet = FindObjectOfType<PlayerWallet>();

            if (_digitTex == null && digitSheet != null) _digitTex = digitSheet.texture;

            if (_font == null)
            {
                // 优先用场景/素材总表里的 Zpix 像素字体（12px 点阵，中文也是像素的）
                _font = uiFont;
                if (_font == null && ArtLibrary.Instance != null) _font = ArtLibrary.Instance.pixelFont;
                // 兜底：系统字体（点采样后边缘偏硬，但字形不是像素的）
                if (_font == null)
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 24);
            }

            if (_hudText != null) return;

            // 字号必须是 12 的倍数 —— Zpix 是 12px 点阵，非整数倍会被重采样糊掉。
            // 虚拟坐标系 1280x720，屏幕实际 640x360，所以 24 = 屏幕 12px。
            _hudText = Make(24, TextAnchor.MiddleLeft, White);
            _hudTextR = Make(24, TextAnchor.MiddleRight, White);
            _title = Make(60, TextAnchor.MiddleCenter, White);
            _sub = Make(24, TextAnchor.MiddleCenter, TextDim);
            _cardTitle = Make(36, TextAnchor.MiddleLeft, White);
            _cardDesc = Make(24, TextAnchor.UpperLeft, TextDim, true);
            _center = Make(24, TextAnchor.MiddleCenter, White);
            _small = Make(24, TextAnchor.MiddleLeft, TextDim);
            _countdown = Make(120, TextAnchor.MiddleCenter, White);
        }

        GUIStyle Make(int size, TextAnchor anchor, Color color, bool wrap = false)
        {
            var st = new GUIStyle
            {
                font = _font,
                fontSize = size,
                alignment = anchor,
                wordWrap = wrap,
                clipping = TextClipping.Overflow,
            };
            st.normal.textColor = color;
            _styles.Add(st);
            return st;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || input == null) return;

            if (gm.CurrentState == GameState.ShopOpen)
            {
                // 右键 = 不买、直接离开
                if (input.RestartPressed || input.PausePressed) { LeavePanel(); return; }

                int pick = input.SelectUpgradeIndex;
                if (pick >= 0 && pick < upgrades.CurrentChoices.Count) TryBuy(pick);
                return;
            }

            if (gm.CurrentState == GameState.Paused ||
                gm.CurrentState == GameState.Failed ||
                gm.CurrentState == GameState.Completed)
            {
                if (input.RestartPressed && levelManager != null) levelManager.RestartLevel();
            }

            if (gm.CurrentState == GameState.Completed && input.ConfirmPressed && levelManager != null)
                levelManager.LoadNextLevel();
        }

        /// <summary>离开商店面板：交给 RunFlow 站回安全平台并倒计时。</summary>
        void LeavePanel()
        {
            var flow = RunFlow.Instance;
            if (flow != null) { flow.ResumeRun(); return; }
            GameManager.Instance.SetState(GameState.Playing);
        }

        /// <summary>商店购买：先扣金币，扣得起才生效。</summary>
        void TryBuy(int index)
        {
            if (!PlayerWallet.CanAfford(currentShopPrice))
            {
                AudioManager.Play("sfx_impact", 0.5f, 0.9f);
                return;
            }

            PlayerWallet.TrySpend(currentShopPrice);
            upgrades.ApplyUpgrade(index);
            AudioManager.Play("sfx_portal", 0.65f, 1.25f);
            LeavePanel();
        }

        void OnGUI()
        {
            EnsureInit();

            var gm = GameManager.Instance;
            if (gm == null) return;

            float scale = Screen.height / VirtualHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float vw = Screen.width / scale;

            // 面板态（商店/暂停/失败/通关）隐藏游戏 HUD，避免两套信息叠在一起
            bool panelOpen = gm.CurrentState == GameState.ShopOpen
                          || gm.CurrentState == GameState.Paused
                          || gm.CurrentState == GameState.Failed
                          || gm.CurrentState == GameState.Completed;
            if (!panelOpen) DrawHud(gm, vw);

            switch (gm.CurrentState)
            {
                case GameState.ShopOpen: DrawShop(gm, vw); break;
                case GameState.Ready: DrawCountdown(vw); break;
                case GameState.Paused: DrawPanel(vw, "已暂停", "Esc 继续      右键 重开本关", Cyan); break;
                case GameState.Failed: DrawFailed(gm, vw); break;
                case GameState.Completed: DrawCompleted(gm, vw); break;
            }

            Scanlines(vw);
        }

        /// <summary>离店后的 3-2-1-GO：压暗全屏 + 巨大数字，给玩家反应时间。</summary>
        void DrawCountdown(float vw)
        {
            var flow = RunFlow.Instance;
            if (flow == null || string.IsNullOrEmpty(flow.Label)) return;

            Fill(new Rect(0f, 0f, vw, VirtualHeight), new Color(0f, 0f, 0f, 0.35f));
            LabelShadow(new Rect(0f, VirtualHeight * 0.30f, vw, 160f), flow.Label, _countdown, White, 4);
            LabelShadow(new Rect(0f, VirtualHeight * 0.30f + 150f, vw, 30f), "准备", _sub, TextDim, 2);
        }

        // ============================================================ 绘制原语

        void Fill(Rect r, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        /// <summary>
        /// 全息 HUD 面板：1px 细线框 + 四角 L 型角标 + 极暗底。
        /// 没有切角、没有四角像素点 —— 这套风格靠"线条少而准"存在感，不靠加装饰。
        /// </summary>
        void Panel(Rect r, Color border, Color bg, bool corners = true)
        {
            Fill(r, bg);

            Color line = border;
            line.a *= 0.5f;
            Fill(new Rect(r.x, r.y, r.width, 1f), line);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), line);
            Fill(new Rect(r.x, r.y, 1f, r.height), line);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), line);

            if (corners) Corners(r, border, 16f, 2f);
        }

        /// <summary>四角 L 型角标：这是全息 HUD 唯一的"装饰"，所以给足长度和 2px 粗细。</summary>
        void Corners(Rect r, Color c, float len, float thick)
        {
            Fill(new Rect(r.x, r.y, len, thick), c);
            Fill(new Rect(r.x, r.y, thick, len), c);

            Fill(new Rect(r.xMax - len, r.y, len, thick), c);
            Fill(new Rect(r.xMax - thick, r.y, thick, len), c);

            Fill(new Rect(r.x, r.yMax - thick, len, thick), c);
            Fill(new Rect(r.x, r.yMax - len, thick, len), c);

            Fill(new Rect(r.xMax - len, r.yMax - thick, len, thick), c);
            Fill(new Rect(r.xMax - thick, r.yMax - len, thick, len), c);
        }

        /// <summary>面板标题栏：左标题 + 右侧小读数 + 一条分隔线。取代居中大标题。</summary>
        void PanelHeader(Rect r, string title, string readout, Color accent, GUIStyle titleStyle)
        {
            Fill(new Rect(r.x, r.y + 42f, r.width, 1f), new Color(accent.r, accent.g, accent.b, 0.28f));

            LabelShadow(new Rect(r.x + 18f, r.y + 6f, r.width - 200f, 36f), title, titleStyle, accent, 2);

            if (!string.IsNullOrEmpty(readout))
            {
                Color dim = accent;
                dim.a = 0.72f;
                LabelShadow(new Rect(r.xMax - 220f, r.y + 10f, 200f, 30f), readout, _hudTextR, dim, 1);
            }
        }

        /// <summary>全屏扫描线 + 上下氛围光，压一层廉价 CRT 感。</summary>
        void Scanlines(float vw)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.055f);
            for (float y = 0f; y < VirtualHeight; y += 3f)
                GUI.DrawTexture(new Rect(0f, y, vw, 1f), _white);

            GUI.color = new Color(0.2f, 0.8f, 1f, 0.05f);
            GUI.DrawTexture(new Rect(0f, 0f, vw, 36f), _white);
            GUI.DrawTexture(new Rect(0f, VirtualHeight - 26f, vw, 26f), _white);
            GUI.color = old;
        }

        /// <summary>偶发故障感的标题（青/品红错位）。</summary>
        void GlitchLabel(Rect r, string text, GUIStyle style, Color? color = null)
        {
            Color body = color ?? White;
            float t = Time.unscaledTime;
            bool glitch = Mathf.Repeat(t * 0.7f, 4.1f) < 0.10f;
            if (!glitch)
            {
                LabelShadow(r, text, style, body, 3);
                return;
            }

            LabelShadow(new Rect(r.x - 2f, r.y, r.width, r.height), text, style, Cyan, 0);
            LabelShadow(new Rect(r.x + 2f, r.y + 1f, r.width, r.height), text, style, new Color(1f, 0.28f, 0.66f), 0);
            LabelShadow(r, text, style, body, 3);
        }

        /// <summary>
        /// 文字 + 硬阴影。像素字体本身边缘就是硬的，阴影只补 1px 就够；
        /// 给到 2px 会在 12px 汉字周围糊出一圈黑框，反而更脏。
        /// </summary>
        void LabelShadow(Rect r, string text, GUIStyle style, Color? color = null, int shadow = 1)
        {
            Color body = color ?? style.normal.textColor;

            Color old = style.normal.textColor;
            style.normal.textColor = Shadow;
            GUI.Label(new Rect(r.x + shadow, r.y + shadow, r.width, r.height), text, style);

            style.normal.textColor = body;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        /// <summary>位图数字：0-9 / . s m x，3x5 点阵 2 倍放大。</summary>
        void PixelNumber(float x, float y, string text, float scale, Color color, bool rightAlign = false)
        {
            if (_digitTex == null || string.IsNullOrEmpty(text)) return;

            float cw = DigitW * scale;
            float padX = 2f * scale;
            float totalW = text.Length * (cw + padX) - padX;
            float cursor = rightAlign ? x - totalW : x;

            Color old = GUI.color;
            GUI.color = color;

            float tw = _digitTex.width;
            float th = _digitTex.height;

            for (int i = 0; i < text.Length; i++)
            {
                int idx = DigitOrder.IndexOf(text[i]);
                if (idx < 0) { cursor += cw + padX; continue; }

                float gx = DigitPad + idx * (DigitW + DigitPad);
                float gy = DigitPad;
                var uv = new Rect(gx / tw, (th - gy - DigitH) / th, DigitW / tw, DigitH / th);
                GUI.DrawTextureWithTexCoords(new Rect(cursor, y, cw, DigitH * scale), _digitTex, uv, true);
                cursor += cw + padX;
            }

            GUI.color = old;
        }

        float PixelNumberWidth(string text, float scale)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return text.Length * (DigitW * scale + 2f * scale) - 2f * scale;
        }

        /// <summary>全屏遮罩：深底 + 很淡的隔行扫描线（2px 周期）。</summary>
        void Backdrop(float vw)
        {
            Fill(new Rect(0f, 0f, vw, VirtualHeight), new Color(0.015f, 0.025f, 0.05f, 0.91f));

            Color line = new Color(0f, 0f, 0f, 0.07f);
            for (float y = 0f; y < VirtualHeight; y += 8f)
            {
                Fill(new Rect(0f, y, vw, 2f), line);
            }
        }

        void DrawSprite(Rect rect, Sprite sprite)
        {
            DrawSpritePart(rect, sprite, new Rect(0f, 0f, 1f, 1f));
        }

        /// <summary>只画 sprite 的一块（normalized 子区域，原点左下）。</summary>
        void DrawSpritePart(Rect rect, Sprite sprite, Rect sub)
        {
            if (sprite == null) return;

            Texture tex = sprite.texture;
            Rect tr = sprite.textureRect;
            var uv = new Rect(
                (tr.x + tr.width * sub.x) / tex.width,
                (tr.y + tr.height * sub.y) / tex.height,
                tr.width * sub.width / tex.width,
                tr.height * sub.height / tex.height);

            GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
        }

        // ============================================================ HUD

        /// <summary>
        /// 连击表现：角色头顶 `x12` + 右侧倍率竖条。
        /// 倍率颜色 白 → 黄 → 紫 → 红；断链前 0.6s 数字抖动并转红（ChainWarning）。
        /// 头顶数字刻意做得小（2.0×），靠"加链弹一下 + 一圈冲击环"给反馈，不靠体积压人。
        /// </summary>
        void DrawCombo(float vw)
        {
            float now = Time.unscaledTime;

            // 链数上涨 → 弹入 + 冲击环
            if (score.Chain > _lastComboChain)
            {
                _comboPunchUntil = now + ComboPunchTime;
                _comboRingFrom = now;
            }
            _lastComboChain = score.Chain;

            float m = score.Multiplier;

            Color tier;
            if (m >= 4f) tier = new Color32(255, 92, 96, 255);        // 红
            else if (m >= 3f) tier = Purple;                          // 紫
            else if (m >= 1.75f) tier = Yellow;                       // 黄
            else tier = White;                                        // 白

            bool warn = score.ChainWarning;
            Color chainColor = warn ? new Color32(255, 92, 96, 255) : tier;

            // ---- 角色头顶：世界坐标投影到虚拟屏幕坐标 ----
            if (health != null && Camera.main != null)
            {
                float scale = Screen.height / VirtualHeight;
                Vector3 world = health.transform.position + Vector3.up * 3.1f;
                Vector3 sp = Camera.main.WorldToScreenPoint(world);

                if (sp.z > 0f)
                {
                    float vx = sp.x / Mathf.Max(0.0001f, scale);
                    float vy = (Screen.height - sp.y) / Mathf.Max(0.0001f, scale);

                    // 刚加链：瞬间放大再回弹（1 + 0.32 → 1）
                    float punch = 0f;
                    if (now < _comboPunchUntil)
                        punch = Mathf.Clamp01((_comboPunchUntil - now) / ComboPunchTime);
                    float txtScale = ComboBaseScale * (1f + 0.32f * punch);

                    // 告警时高频细抖，像数字"快撑不住了"
                    float jitter = warn ? Mathf.Sin(now * 62f) * 2f : 0f;

                    string txt = "x" + score.Chain;
                    float tw = PixelNumberWidth(txt, txtScale);
                    float tx = vx - tw * 0.5f + jitter;
                    float ty = vy - 14f + jitter;

                    // ---- 冲击环：一圈方框向外扩散并淡出 ----
                    float ringAge = now - _comboRingFrom;
                    if (ringAge >= 0f && ringAge < ComboRingTime)
                    {
                        float t = ringAge / ComboRingTime;
                        float grow = 1f + 1.15f * t;
                        float rw = tw * grow;
                        float rh = 16f * txtScale * 0.5f * grow;
                        Color rc = chainColor;
                        rc.a = 0.8f * (1f - t);

                        float cx = vx - rw * 0.5f + jitter;
                        float cy = ty + (16f * txtScale * 0.5f) - rh * 0.5f;
                        const float th = 2f;
                        Fill(new Rect(cx, cy, rw, th), rc);                    // 上
                        Fill(new Rect(cx, cy + rh - th, rw, th), rc);           // 下
                        Fill(new Rect(cx, cy, th, rh), rc);                     // 左
                        Fill(new Rect(cx + rw - th, cy, th, rh), rc);           // 右
                    }

                    // ---- 硬描边：四向偏移各画一次深色，像素字才立得住 ----
                    Color outline = new Color(0.02f, 0.03f, 0.07f, 0.92f);
                    PixelNumber(tx - 1f, ty, txt, txtScale, outline);
                    PixelNumber(tx + 1f, ty, txt, txtScale, outline);
                    PixelNumber(tx, ty - 1f, txt, txtScale, outline);
                    PixelNumber(tx, ty + 1f, txt, txtScale, outline);

                    PixelNumber(tx, ty, txt, txtScale, chainColor);
                }
            }

            // ---- 右侧：倍率竖条（长度 = 断链倒计时剩余比例）----
            var bar = new Rect(vw - 34f, VirtualHeight * 0.34f, 14f, 190f);
            Panel(bar, CyanDim, PanelBg, false);

            float ratio = score.ChainRatio;
            float fillH = (bar.height - 4f) * ratio;
            if (fillH > 0.5f)
                Fill(new Rect(bar.x + 2f, bar.y + bar.height - 2f - fillH, bar.width - 4f, fillH), tier);

            string mtxt = "x" + m.ToString("0.0");
            float mw = PixelNumberWidth(mtxt, 2.4f);
            PixelNumber(vw - 44f - mw, bar.y - 34f, mtxt, 2.4f, tier);
        }

        /// <summary>
        /// 追逐段的压迫感：左侧屏边红渐变（8 条不同 alpha 的竖条拼出渐变，不用贴图，避开半透明色块老坑）
        /// + 顶部闪烁红条 + 预警倒计时 / "跑！"。
        /// </summary>
        void DrawChaseAlert(float vw)
        {
            ChaseZone zone = ChaseZone.Current;
            if (zone == null || !zone.IsHot) return;

            Color red = new Color32(255, 60, 72, 255);
            float pulse = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 2.4f, 1f);

            // ---- 左侧屏边 ----
            float edgeW = vw * 0.09f;
            const int steps = 8;
            float sw = edgeW / steps;
            for (int i = 0; i < steps; i++)
            {
                float k = 1f - (float)i / steps;
                Color c = red;
                c.a = 0.40f * k * pulse;
                Fill(new Rect(i * sw, 0f, sw + 0.6f, VirtualHeight), c);
            }

            // ---- 顶部横条 ----
            Color barC = red;
            barC.a = 0.50f + 0.35f * pulse;
            Fill(new Rect(0f, 0f, vw, 6f), barC);

            // ---- 文案（避开顶部击杀计数 y≈16 和冲刺条 y≈30）----
            if (zone.IsWarning)
            {
                LabelShadow(new Rect(0f, 62f, vw, 46f), "崩塌来了", _title, red, 3);

                string cnt = Mathf.Max(1, Mathf.CeilToInt(zone.WarnLeft)).ToString();
                float cw = PixelNumberWidth(cnt, 3.2f);
                PixelNumber(vw * 0.5f - cw * 0.5f, 116f, cnt, 3.2f, White);
            }
            else if (zone.IsChasing)
            {
                LabelShadow(new Rect(0f, 56f, vw, 40f), "跑！", _title, red, 3);
            }
        }

        void DrawHud(GameManager gm, float vw)
        {
            // ---- 追逐段：屏边红渐变 + 顶部警告（最先画，垫在其它 HUD 下面）----
            DrawChaseAlert(vw);

            // ---- 最上方中间：击杀进度（每 N 杀回 1 血，满 N 重新计数）----
            DrawKillCounter(vw);

            // ---- 路程显示下方：中键冲刺冷却条 ----
            DrawDashCooldownBar(vw);

            // ---- 生命：像素心形 ----
            if (health != null && heart != null)
            {
                // 最多画 12 颗，防止 maxHealth 被一路叠加后把 HUD 挤爆
                int maxHp = Mathf.Min(health.maxHealth, 12);
                // 图集：图片上半是红色实心、下半是灰色空心；Unity 纹理 y 从下往上，所以反过来取
                var fullUv = new Rect(0f, 0.5f, 1f, 0.5f);
                var emptyUv = new Rect(0f, 0f, 1f, 0.5f);

                for (int i = 0; i < maxHp; i++)
                {
                    var rect = new Rect(20f + i * 36f, 16f, 30f, 28f);
                    bool full = i < health.CurrentHealth;

                    Color old = GUI.color;
                    GUI.color = full ? White : new Color(0.30f, 0.34f, 0.42f, 1f);
                    DrawSpritePart(rect, heart, full ? fullUv : emptyUv);
                    GUI.color = old;
                }
            }

            // ---- 进度条：外框 + 8px 分段填充 ----
            if (tracker != null)
            {
                var bar = new Rect(20f, 56f, vw - 40f, 14f);
                PixelBar(bar, tracker.Progress01);

                // 商店标记：按招牌的 X 换算到进度条上；已触发过的变灰
                if (_shops == null) _shops = FindObjectsOfType<ShopIcon>();
                if (_shops != null && _shops.Length > 0)
                {
                    float total = tracker.TotalDistance;
                    for (int i = 0; i < _shops.Length; i++)
                    {
                        var shop = _shops[i];
                        if (shop == null) continue;
                        float p = Mathf.Clamp01((shop.transform.position.x - tracker.startX) / total);
                        var marker = new Rect(bar.x + p * bar.width - 2f, bar.y - 4f, 4f, bar.height + 8f);
                        Fill(marker, shop.Opened
                            ? new Color(0.45f, 0.50f, 0.60f, 0.85f)
                            : new Color32(255, 214, 120, 255));
                    }
                }

                string dist = Mathf.RoundToInt(tracker.CurrentDistance) + "/" + Mathf.RoundToInt(tracker.TotalDistance);
                PixelNumber(20f, 78f, dist, 2.2f, Cyan);
                LabelShadow(new Rect(20f + PixelNumberWidth(dist, 2.2f) + 10f, 74f, 60f, 24f), "米", _small, TextDim, 1);
            }

            // ---- 连击 + 倍率 ----
            if (score != null && score.Chain > 0) DrawCombo(vw);

            // ---- 用时（右上）----
            string time = gm.RunTime.ToString("0.0") + "s";
            PixelNumber(vw - 20f, 18f, time, 2.4f, White, true);

            // ---- 金币（右上，用时下面）----
            if (wallet != null)
            {
                string coins = wallet.coins.ToString();
                PixelNumber(vw - 20f, 56f, coins, 2.2f, new Color32(255, 214, 120, 255), true);

                if (coinSprite != null)
                {
                    float cw = PixelNumberWidth(coins, 2.2f);
                    var cr = new Rect(vw - 20f - cw - 34f, 54f, 26f, 26f);
                    DrawSprite(cr, coinSprite);
                }
            }

            // ---- 玩家头顶的蓄力条 ----
            DrawChargeBar();

            // ---- 底部操作提示：只在真正能操作时显示 ----
            if (gm.CurrentState != GameState.Playing) return;

            string hint = "左键 拔刀蓄力    右键 跳跃    中键 冲刺    Esc 暂停";
            float hy = VirtualHeight - 42f;
            Fill(new Rect(20f, hy + 7f, 6f, 6f), CyanDim);
            LabelShadow(new Rect(32f, hy, vw - 60f, 24f), hint, _small, TextDim, 1);

            if (dash != null && dash.IsDashing)
            {
                LabelShadow(new Rect(vw - 200f, hy - 4f, 180f, 28f), "冲刺中", _hudTextR, Yellow);
            }
        }

        /// <summary>顶部的击杀计数：x / N，满 N 回 1 血并归零。</summary>
        void DrawKillCounter(float vw)
        {
            if (killReward == null || killReward.killsPerHeal <= 0) return;

            int need = killReward.killsPerHeal;
            int have = killReward.KillProgress;
            bool nextHeals = have >= need - 1;

            string txt = "击杀 " + have + "/" + need;
            Color c = nextHeals
                ? new Color(1f, 0.84f, 0.47f, 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f))
                : TextDim;
            LabelShadow(new Rect(vw * 0.5f - 110f, 6f, 220f, 22f), txt, _center, c, 2);
        }

        /// <summary>
        /// 冲刺冷却条：放在路程显示（进度条 + 距离）的下方，左对齐。
        /// 全息 HUD 风格：细线框 + 四段刻度 + 就绪扫光。
        /// </summary>
        void DrawDashCooldownBar(float vw)
        {
            if (dash == null || !dash.allowManualDash) return;

            const float x = 20f;
            const float y = 126f;
            const float labelW = 58f;
            const float barW = 260f;
            const float barH = 10f;

            bool ready = dash.ManualDashReady;
            float p = ready ? 1f : 1f - dash.ManualDashCooldownLeft / dash.ManualCooldown;
            p = Mathf.Clamp01(p);

            LabelShadow(new Rect(x, y - 9f, labelW, 28f), "冲刺", _small, ready ? White : TextDim, 1);

            var bar = new Rect(x + labelW, y, barW, barH);

            Fill(bar, new Color(0f, 0f, 0f, 0.38f));

            if (p > 0f)
            {
                Color fillCol = ready
                    ? Color.Lerp(Cyan, White, 0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f))
                    : CyanDim;
                Fill(new Rect(bar.x, bar.y, Mathf.Max(1f, Mathf.Floor(barW * p)), barH), fillCol);
            }

            Color line = ready ? Cyan : CyanDim;
            line.a = ready ? 0.85f : 0.45f;
            Fill(new Rect(bar.x, bar.y - 1f, barW, 1f), line);
            Fill(new Rect(bar.x, bar.yMax, barW, 1f), line);
            Fill(new Rect(bar.x - 1f, bar.y - 1f, 1f, barH + 2f), line);
            Fill(new Rect(bar.xMax, bar.y - 1f, 1f, barH + 2f), line);

            // 四段刻度
            for (int i = 1; i < 4; i++)
                Fill(new Rect(bar.x + Mathf.Round(barW * 0.25f * i), bar.y, 1f, barH),
                     new Color(0.02f, 0.03f, 0.06f, 0.72f));

            if (ready)
            {
                float sweep = Mathf.Repeat(Time.unscaledTime * 1.1f, 1f);
                Fill(new Rect(bar.x + Mathf.Floor(barW * sweep), bar.y, 8f, barH),
                     new Color(1f, 1f, 1f, 0.55f));
            }

            string status = ready ? "READY" : Mathf.CeilToInt(dash.ManualDashCooldownLeft) + "s";
            LabelShadow(new Rect(bar.xMax + 12f, y - 9f, 140f, 28f), status, _small, ready ? Cyan : TextDim, 1);
        }

        /// <summary>
        /// 居合蓄力条：很小的一条（96x10 虚拟像素），钉在玩家头顶。
        /// 三段用配色区分（青 → 黄 → 紫），刻度线标阈值，满蓄整条闪白。
        /// 不再画外框面板和阶段大字——之前那版在 1080p 下太占地方。
        /// </summary>
        void DrawChargeBar()
        {
            if (iai == null || !iai.IsCharging) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 sp = cam.WorldToScreenPoint(iai.transform.position + Vector3.up * 2.05f);
            if (sp.z <= 0f) return;

            float scale = Screen.height / VirtualHeight;
            if (scale <= 0f) return;

            float cx = sp.x / scale;
            float cy = VirtualHeight - sp.y / scale;

            const float barW = 96f;
            const float barH = 10f;
            const float pad = 2f;

            var outer = new Rect(Mathf.Round(cx - barW * 0.5f), Mathf.Round(cy - barH), barW, barH);

            float p = Mathf.Clamp01(iai.ChargeRatio);
            bool full = iai.IsFull;
            Color accent = full ? White : StageAccent(p);

            float innerW = barW - pad * 2f;
            float fillW = Mathf.Floor(innerW * p);

            if (uiChargeFrame == null && ArtLibrary.Instance != null)
                uiChargeFrame = ArtLibrary.Instance.uiChargeFrame;
            if (uiChargeFill == null && ArtLibrary.Instance != null)
                uiChargeFill = ArtLibrary.Instance.uiChargeFill;

            if (uiChargeFrame != null && uiChargeFill != null)
            {
                DrawSprite(outer, uiChargeFrame);

                if (fillW >= 1f)
                {
                    Color old = GUI.color;
                    GUI.color = accent;
                    DrawSpritePart(new Rect(outer.x + pad, outer.y + pad, fillW, barH - pad * 2f),
                                   uiChargeFill,
                                   new Rect(0f, 0f, Mathf.Clamp01(fillW / innerW), 1f));
                    GUI.color = old;
                }
            }
            else
            {
                // 没素材时的兜底：同尺寸的纯矩形条
                Fill(outer, new Color(0.03f, 0.05f, 0.10f, 0.92f));
                Fill(new Rect(outer.x, outer.y, outer.width, 1f), CyanMid);
                Fill(new Rect(outer.x, outer.yMax - 1f, outer.width, 1f), CyanMid);
                Fill(new Rect(outer.x, outer.y, 1f, outer.height), CyanMid);
                Fill(new Rect(outer.xMax - 1f, outer.y, 1f, outer.height), CyanMid);
                if (fillW >= 1f) Fill(new Rect(outer.x + pad, outer.y + pad, fillW, barH - pad * 2f), accent);
            }

            // 阶段刻度
            IaiCharge.Stage[] st = iai.Stages;
            if (st != null)
            {
                for (int i = 1; i < st.Length; i++)
                {
                    float tx = outer.x + pad + innerW * Mathf.Clamp01(st[i].threshold);
                    Fill(new Rect(Mathf.Round(tx), outer.y + 1f, 1f, barH - 2f), new Color(0f, 0f, 0f, 0.6f));
                }
            }

            // 满蓄：顶部一条 1px 白线闪烁
            if (full && Mathf.Repeat(Time.unscaledTime, 0.12f) < 0.06f)
                Fill(new Rect(outer.x, outer.y, outer.width, 1f), White);
        }

        /// <summary>蓄力比例 → 三段配色。</summary>
        Color StageAccent(float p)
        {
            if (p < 0.4f) return Cyan;
            if (p < 0.8f) return Yellow;
            return Purple;
        }

        /// <summary>像素进度条：1px 外框 + 8px 一格的分段填充 + 顶部高光。</summary>
        void PixelBar(Rect r, float t)
        {
            Fill(r, Deep);

            // 外框
            Fill(new Rect(r.x, r.y, r.width, 1f), CyanMid);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), CyanMid);
            Fill(new Rect(r.x, r.y, 1f, r.height), CyanMid);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), CyanMid);

            float innerW = r.width - 2f;
            float fillW = Mathf.Floor(innerW * Mathf.Clamp01(t));

            if (fillW >= 1f)
            {
                var fillRect = new Rect(r.x + 1f, r.y + 1f, fillW, r.height - 2f);
                Fill(fillRect, new Color32(54, 214, 234, 255));
                Fill(new Rect(fillRect.x, fillRect.y, fillRect.width, 1f), new Color32(168, 248, 255, 255));

                // 分段暗线，每 8px 一条
                Color seg = new Color(0f, 0f, 0f, 0.22f);
                for (float sx = r.x + 8f; sx < r.x + 1f + fillW; sx += 8f)
                {
                    Fill(new Rect(sx, fillRect.y, 1f, fillRect.height), seg);
                }
            }
        }

        // ============================================================ 弹窗

        /// <summary>金币商店：布局和升级三选一一致，但卡片带价格，买不起会整体压暗。</summary>
        void DrawShop(GameManager gm, float vw)
        {
            Backdrop(vw);

            bool canBuy = PlayerWallet.CanAfford(currentShopPrice);
            int count = upgrades.CurrentChoices.Count;

            // ================= 顶部：左标题 + 右余额 =================
            const float padX = 40f;
            const float headY = 58f;

            LabelShadow(new Rect(padX, headY, 420f, 48f), "商店", _title, Yellow, 3);
            LabelShadow(new Rect(padX + 2f, headY + 50f, 520f, 30f),
                canBuy ? "选一件带走" : "金币不够，先多收一些", _small, TextDim, 1);

            // 余额（右上，图标 + 数字）
            string bal = PlayerWallet.Coins.ToString();
            float bw = PixelNumberWidth(bal, 2.6f);
            float bx = vw - padX - bw;
            PixelNumber(bx, headY + 8f, bal, 2.6f, new Color32(255, 214, 120, 255));
            if (coinSprite != null) DrawSprite(new Rect(bx - 40f, headY + 6f, 34f, 34f), coinSprite);

            // 标题下的分隔线
            Fill(new Rect(padX, headY + 94f, vw - padX * 2f, 1f), new Color(Yellow.r, Yellow.g, Yellow.b, 0.22f));

            // ================= 卡片 =================
            const float cardW = 340f;
            const float cardH = 270f;
            const float gap = 34f;
            float totalW = count * cardW + (count - 1) * gap;
            float startX = (vw - totalW) * 0.5f;
            const float cardY = 224f;

            Color[] accents = { Cyan, Yellow, Purple };

            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(startX + i * (cardW + gap), cardY, cardW, cardH);
                UpgradeDefinition def = upgrades.CurrentChoices[i];
                bool hover = rect.Contains(Event.current.mousePosition) && canBuy;
                Color accent = accents[i % accents.Length];

                // 底色几乎透明，存在感全靠线条
                Fill(rect, new Color(0.02f, 0.035f, 0.07f, 0.74f));

                Color line = canBuy ? accent : CyanDim;
                line.a = hover ? 0.95f : 0.42f;
                Fill(new Rect(rect.x, rect.y, rect.width, 1f), line);
                Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), line);
                Fill(new Rect(rect.x, rect.y, 1f, rect.height), line);
                Fill(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), line);

                Corners(rect, canBuy ? accent : CyanDim, hover ? 22f : 18f, 2f);

                // 左上序号 / 右上等级
                LabelShadow(new Rect(rect.x + 16f, rect.y + 12f, 80f, 30f), "0" + (i + 1), _small, accent, 1);

                int lv = upgrades.GetLevel(def.id);
                LabelShadow(new Rect(rect.xMax - 120f, rect.y + 12f, 104f, 30f),
                    lv + "/" + def.maxLevel, _hudTextR, TextDim, 1);

                // 名称
                LabelShadow(new Rect(rect.x + 18f, rect.y + 58f, rect.width - 36f, 48f),
                    def.displayName, _cardTitle, canBuy ? White : TextDim);

                // 描述
                LabelShadow(new Rect(rect.x + 18f, rect.y + 114f, rect.width - 36f, 96f),
                    def.description, _cardDesc, TextDim, 1);

                // 底部：价格
                Fill(new Rect(rect.x + 18f, rect.yMax - 58f, rect.width - 36f, 1f),
                     new Color(accent.r, accent.g, accent.b, 0.25f));

                string price = currentShopPrice.ToString();
                float px0 = rect.x + 18f;
                if (coinSprite != null) DrawSprite(new Rect(px0, rect.yMax - 46f, 26f, 26f), coinSprite);
                PixelNumber(px0 + 32f, rect.yMax - 43f, price, 2.2f, canBuy ? Yellow : TextDim);

                LabelShadow(new Rect(rect.xMax - 140f, rect.yMax - 42f, 122f, 30f),
                    hover ? "购买" : "按 " + (i + 1), _hudTextR, hover ? Yellow : TextDim, 1);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && canBuy) TryBuy(i);
            }

            LabelShadow(new Rect(0f, VirtualHeight - 48f, vw, 30f), "Esc 离开", _center, TextDim, 1);
        }

        void DrawFailed(GameManager gm, float vw)
        {
            string reason = !string.IsNullOrEmpty(gm.FailReason)
                ? gm.FailReason
                : (health != null && health.CurrentHealth <= 0) ? "生命归零" : "掉落屋顶";
            DrawPanel(vw, "失败", "右键 重开", new Color32(255, 77, 109, 255), reason);
        }

        void DrawCompleted(GameManager gm, float vw)
        {
            Backdrop(vw);

            ScoreRank rank = score != null ? score.Rank : ScoreRank.C;
            Color rankColor;
            switch (rank)
            {
                case ScoreRank.S: rankColor = new Color32(255, 209, 102, 255); break;   // 金
                case ScoreRank.A: rankColor = Cyan; break;                              // 青
                case ScoreRank.B: rankColor = Purple; break;                            // 紫
                default: rankColor = new Color32(150, 160, 176, 255); break;            // 灰
            }

            float cx = vw * 0.5f;

            // ---- 左：评级 / 右：总分，中间一条竖线 ----
            Color edge = new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f);

            // 像素字体里大写 S 和数字 5 几乎一样，所以大字用中文评语，字母降级成小注
            string rankWord;
            switch (rank)
            {
                case ScoreRank.S: rankWord = "完美"; break;
                case ScoreRank.A: rankWord = "优秀"; break;
                case ScoreRank.B: rankWord = "良好"; break;
                default: rankWord = "合格"; break;
            }

            LabelShadow(new Rect(cx - 250f, 148f, 240f, 104f), rankWord, _title, rankColor, 3);
            LabelShadow(new Rect(cx - 250f, 256f, 240f, 30f), "评级 " + rank, _sub, TextDim, 1);

            Fill(new Rect(cx - 6f, 158f, 1f, 104f), edge);

            if (score != null)
            {
                string total = score.Score.ToString();
                PixelNumber(cx + 34f, 176f, total, 4.4f, White);
                LabelShadow(new Rect(cx + 34f, 256f, 320f, 30f),
                    score.NewRecord ? "总分   NEW RECORD" : "总分",
                    _small, score.NewRecord ? Yellow : TextDim, 1);
            }

            // ---- 数据表：上下两条横线 + 竖分隔，没有外框 ----
            var tab = new Rect(cx - 340f, 338f, 680f, 92f);
            Fill(new Rect(tab.x, tab.y, tab.width, 1f), edge);
            Fill(new Rect(tab.x, tab.yMax, tab.width, 1f), edge);

            string[] keys = { "最高连击", "击杀", "受伤", "用时" };
            string[] vals =
            {
                (score != null ? score.BestChain : 0).ToString(),
                (killReward != null ? killReward.KillCount : 0).ToString(),
                (health != null ? health.DamageTakenCount : 0).ToString(),
                gm.RunTime.ToString("0.0") + "s",
            };

            float colW = tab.width / 4f;
            for (int i = 0; i < 4; i++)
            {
                float c0 = tab.x + colW * i + colW * 0.5f;
                LabelShadow(new Rect(c0 - colW * 0.5f, tab.y + 10f, colW, 28f), keys[i], _sub, TextDim, 1);

                float w2 = PixelNumberWidth(vals[i], 2.4f);
                PixelNumber(c0 - w2 * 0.5f, tab.y + 46f, vals[i], 2.4f, White);

                if (i > 0)
                    Fill(new Rect(tab.x + colW * i, tab.y + 18f, 1f, tab.height - 36f),
                         new Color(Cyan.r, Cyan.g, Cyan.b, 0.14f));
            }

            // ---- 底部 ----
            if (score != null)
                LabelShadow(new Rect(0f, tab.yMax + 30f, vw, 30f), "最高分 " + score.HighScore, _center, TextDim, 1);

            LabelShadow(new Rect(0f, tab.yMax + 74f, vw, 30f), "右键 重开        Enter 下一关（首版仅一关）", _center, TextDim, 1);
        }

        void DrawPanel(float vw, string title, string hint, Color accent, string sub = null)
        {
            Backdrop(vw);

            var panel = new Rect(vw * 0.5f - 270f, 246f, 540f, 186f);
            Panel(panel, accent, new Color(0.02f, 0.035f, 0.07f, 0.86f));

            // 左侧一条 3px 强调色，代替原来的顶部粗色条
            Fill(new Rect(panel.x, panel.y + 18f, 3f, panel.height - 36f), accent);

            GlitchLabel(new Rect(panel.x + 32f, panel.y + 26f, panel.width - 64f, 62f), title, _title, accent);

            if (sub != null)
            {
                Fill(new Rect(panel.x + 32f, panel.y + 94f, panel.width - 64f, 1f),
                     new Color(accent.r, accent.g, accent.b, 0.25f));
                LabelShadow(new Rect(panel.x + 32f, panel.y + 104f, panel.width - 64f, 30f), sub, _center, White);
            }

            LabelShadow(new Rect(panel.x + 32f, panel.y + 142f, panel.width - 64f, 30f), hint, _center, TextDim, 1);
        }
    }
}
