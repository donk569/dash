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
        bool _fontPixelized;

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
                _font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 20);
            }

            // 中文走系统字体，但把图集采样设成 Point，边缘硬一点，跟像素元素统一
            if (!_fontPixelized && _font != null && _font.material != null && _font.material.mainTexture != null)
            {
                _font.material.mainTexture.filterMode = FilterMode.Point;
                _fontPixelized = true;
            }

            if (_hudText != null) return;

            _hudText = Make(20, TextAnchor.MiddleLeft, White);
            _hudTextR = Make(20, TextAnchor.MiddleRight, White);
            _title = Make(48, TextAnchor.MiddleCenter, White);
            _sub = Make(18, TextAnchor.MiddleCenter, TextDim);
            _cardTitle = Make(28, TextAnchor.UpperCenter, White);
            _cardDesc = Make(17, TextAnchor.UpperCenter, TextDim, true);
            _center = Make(18, TextAnchor.MiddleCenter, White);
            _small = Make(17, TextAnchor.MiddleLeft, TextDim);
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

            DrawHud(gm, vw);

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

        /// <summary>像素面板：深底 + 切角亮框（赛博风）+ 四角装饰点。</summary>
        void Panel(Rect r, Color border, Color bg, bool corners = true)
        {
            Fill(r, bg);

            const float c = 6f;
            // 切角外框：四角各留 c 像素不画，再用阶梯补角
            Fill(new Rect(r.x + c, r.y, r.width - c * 2f, 1f), border);
            Fill(new Rect(r.x + c, r.yMax - 1f, r.width - c * 2f, 1f), border);
            Fill(new Rect(r.x, r.y + c, 1f, r.height - c * 2f), border);
            Fill(new Rect(r.xMax - 1f, r.y + c, 1f, r.height - c * 2f), border);

            if (corners)
            {
                for (int i = 0; i < 4; i++)
                {
                    float o = c - i - 1f;
                    Fill(new Rect(r.x + i, r.y + o, 1f, 1f), border);
                    Fill(new Rect(r.xMax - 1f - i, r.y + o, 1f, 1f), border);
                    Fill(new Rect(r.x + i, r.yMax - 1f - o, 1f, 1f), border);
                    Fill(new Rect(r.xMax - 1f - i, r.yMax - 1f - o, 1f, 1f), border);
                }
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

        /// <summary>文字 + 2px 硬阴影（像素游戏的标准做法，比描边更像老游戏）。</summary>
        void LabelShadow(Rect r, string text, GUIStyle style, Color? color = null, int shadow = 2)
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

        /// <summary>全屏遮罩：深底 + 隔行扫描线（2px 周期），替代平淡的半透明黑。</summary>
        void Backdrop(float vw)
        {
            Fill(new Rect(0f, 0f, vw, VirtualHeight), new Color(0.02f, 0.03f, 0.06f, 0.74f));

            Color line = new Color(0f, 0f, 0f, 0.10f);
            for (float y = 0f; y < VirtualHeight; y += 6f)
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
        /// </summary>
        void DrawCombo(float vw)
        {
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
                Vector3 world = health.transform.position + Vector3.up * 3.4f;
                Vector3 sp = Camera.main.WorldToScreenPoint(world);

                if (sp.z > 0f)
                {
                    float vx = sp.x / Mathf.Max(0.0001f, scale);
                    float vy = (Screen.height - sp.y) / Mathf.Max(0.0001f, scale);

                    // 告警时高频抖动，像数字"快撑不住了"
                    float jitter = warn ? Mathf.Sin(Time.unscaledTime * 62f) * 2.5f : 0f;

                    string txt = "x" + score.Chain;
                    float w = PixelNumberWidth(txt, 3f);
                    PixelNumber(vx - w * 0.5f + jitter, vy - 16f + jitter, txt, 3f, chainColor);
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

        void DrawHud(GameManager gm, float vw)
        {
            // ---- 最上方中间：击杀进度（每 N 杀回 1 血，满 N 重新计数）----
            DrawKillCounter(vw);

            // ---- 顶部中间：中键冲刺冷却条 ----
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
                var bar = new Rect(20f, 56f, Mathf.Min(vw - 40f, 620f), 16f);
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

        /// <summary>顶部中间的冲刺冷却条：满了才亮，冷却中显示剩余秒数。</summary>
        void DrawDashCooldownBar(float vw)
        {
            if (dash == null || !dash.allowManualDash) return;

            const float barW = 170f;
            const float barH = 9f;
            float x = Mathf.Round((vw - barW) * 0.5f);
            float y = 32f;

            Fill(new Rect(x - 2f, y - 2f, barW + 4f, barH + 4f), new Color(0f, 0f, 0f, 0.55f));

            bool ready = dash.ManualDashReady;
            float p = ready ? 1f : 1f - dash.ManualDashCooldownLeft / dash.ManualCooldown;
            p = Mathf.Clamp01(p);

            if (p > 0f)
            {
                Color fillCol = ready
                    ? Color.Lerp(Cyan, White, 0.2f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f))
                    : CyanDim;
                Fill(new Rect(x, y, Mathf.Max(1f, Mathf.Floor(barW * p)), barH), fillCol);
            }

            // 就绪时一道高光扫过
            if (ready)
            {
                float sweep = Mathf.Repeat(Time.unscaledTime * 1.1f, 1f);
                Fill(new Rect(x + Mathf.Floor(barW * sweep), y, 7f, barH), new Color(1f, 1f, 1f, 0.7f));
            }

            // 冲刺图标（新画的霓虹箭头）
            if (_dashIcon == null && ArtLibrary.Instance != null) _dashIcon = ArtLibrary.Instance.dashIcon;
            if (_dashIcon != null)
            {
                float icon = 22f;
                DrawSprite(new Rect(x - icon - 6f, y + barH * 0.5f - icon * 0.5f, icon, icon), _dashIcon);
            }

            string label = ready ? "中键 冲刺" : "冲刺 " + Mathf.CeilToInt(dash.ManualDashCooldownLeft) + "s";
            LabelShadow(new Rect(x + barW + 8f, y - 6f, 140f, 22f), label, _small, ready ? White : TextDim, 1);
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

            LabelShadow(new Rect(0f, 86f, vw, 56f), "商店", _title, Yellow, 3);

            bool canBuy = PlayerWallet.CanAfford(currentShopPrice);

            // 余额
            string bal = PlayerWallet.Coins.ToString();
            float bw = PixelNumberWidth(bal, 2.6f);
            float groupW = bw + (coinSprite != null ? 42f : 0f);
            float gx = vw * 0.5f - groupW * 0.5f;

            if (coinSprite != null)
                DrawSprite(new Rect(gx, 150f, 30f, 30f), coinSprite);

            PixelNumber(gx + (coinSprite != null ? 42f : 0f), 154f, bal, 2.6f, new Color32(255, 214, 120, 255));

            LabelShadow(new Rect(0f, 192f, vw, 24f),
                canBuy ? "挑一件带走吧" : "金币不够，先多收一些", _sub, TextDim, 1);

            const float cardW = 300f;
            const float cardH = 258f;
            const float gap = 26f;

            int count = upgrades.CurrentChoices.Count;
            float totalW = count * cardW + (count - 1) * gap;
            float startX = (vw - totalW) * 0.5f;
            Color[] accents = { Cyan, Yellow, Purple };

            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(startX + i * (cardW + gap), 228f, cardW, cardH);
                UpgradeDefinition def = upgrades.CurrentChoices[i];
                bool hover = rect.Contains(Event.current.mousePosition) && canBuy;
                Color accent = accents[i % accents.Length];

                Panel(rect, hover ? Yellow : CyanMid, hover ? PanelBgHi : PanelBg);
                Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 4f), canBuy ? accent : CyanDim);

                PixelNumber(rect.x + 16f, rect.y + 18f, (i + 1).ToString(), 2.2f, canBuy ? accent : CyanDim);

                int lv = upgrades.GetLevel(def.id);
                PixelNumber(rect.x + rect.width - 16f, rect.y + 20f, lv + "/" + def.maxLevel, 1.8f, TextDim, true);

                LabelShadow(new Rect(rect.x + 16f, rect.y + 50f, rect.width - 32f, 40f),
                    def.displayName, _cardTitle, canBuy ? White : TextDim);

                LabelShadow(new Rect(rect.x + 24f, rect.y + 100f, rect.width - 48f, 92f),
                    def.description, _cardDesc, TextDim, 1);

                Fill(new Rect(rect.x + 20f, rect.y + rect.height - 50f, rect.width - 40f, 1f), CyanDim);

                // 价格
                string price = currentShopPrice.ToString();
                float pw = PixelNumberWidth(price, 2f);
                float px0 = rect.x + rect.width * 0.5f - (pw + 34f) * 0.5f;

                if (coinSprite != null)
                    DrawSprite(new Rect(px0, rect.y + rect.height - 42f, 24f, 24f), coinSprite);

                PixelNumber(px0 + 34f, rect.y + rect.height - 38f, price, 2f, canBuy ? Yellow : TextDim);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && canBuy) TryBuy(i);
            }

            LabelShadow(new Rect(0f, 548f, vw, 26f), "按 1 / 2 / 3 购买      Esc 离开", _center, TextDim, 1);
        }

        void DrawFailed(GameManager gm, float vw)
        {
            string reason = (health != null && health.CurrentHealth <= 0) ? "生命归零" : "掉落屋顶";
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

            // ---- 评级大字 ----
            LabelShadow(new Rect(0f, 66f, vw, 100f), rank.ToString(), _title, rankColor, 6);
            LabelShadow(new Rect(0f, 166f, vw, 28f), "通关", _sub, TextDim, 2);

            // ---- 总分 + 新纪录 ----
            if (score != null)
            {
                string total = score.Score.ToString();
                float tw = PixelNumberWidth(total, 4.6f);
                PixelNumber(vw * 0.5f - tw * 0.5f, 204f, total, 4.6f, White);

                if (score.NewRecord)
                    LabelShadow(new Rect(0f, 252f, vw, 24f), "NEW RECORD", _center, Yellow, 2);
            }

            var panel = new Rect(vw * 0.5f - 340f, 288f, 680f, 210f);
            Panel(panel, Cyan, PanelBg);

            Fill(new Rect(panel.x + 2f, panel.y + 2f, panel.width - 4f, 4f), Cyan);

            string[] keys = { "最高连击", "击杀", "受伤", "用时" };
            string[] vals =
            {
                (score != null ? score.BestChain : 0).ToString(),
                (killReward != null ? killReward.KillCount : 0).ToString(),
                (health != null ? health.DamageTakenCount : 0).ToString(),
                gm.RunTime.ToString("0.0") + "s",
            };

            float colW = panel.width / 4f;
            for (int i = 0; i < 4; i++)
            {
                float cx = panel.x + colW * i + colW * 0.5f;
                LabelShadow(new Rect(cx - colW * 0.5f, panel.y + 34f, colW, 26f), keys[i], _sub, TextDim, 1);

                float vw2 = PixelNumberWidth(vals[i], 2.4f);
                PixelNumber(cx - vw2 * 0.5f, panel.y + 66f, vals[i], 2.4f, White);

                if (i < 3)
                {
                    Fill(new Rect(panel.x + colW * (i + 1) - 1f, panel.y + 40f, 1f, 60f), CyanDim);
                }
            }

            Fill(new Rect(panel.x + 24f, panel.y + 130f, panel.width - 48f, 1f), CyanDim);

            string best = score != null ? "最高分 " + score.HighScore + "      右键 重开      Enter 下一关（首版仅一关）"
                                        : "右键 重开      Enter 下一关（首版仅一关）";
            LabelShadow(new Rect(panel.x, panel.y + 146f, panel.width, 26f), best, _center, TextDim);
        }

        void DrawPanel(float vw, string title, string hint, Color accent, string sub = null)
        {
            Backdrop(vw);

            var panel = new Rect(vw * 0.5f - 250f, 244f, 500f, 184f);
            Panel(panel, accent, PanelBg);
            Fill(new Rect(panel.x + 2f, panel.y + 2f, panel.width - 4f, 4f), accent);

            GlitchLabel(new Rect(panel.x, panel.y + 30f, panel.width, 56f), title, _title, accent);

            if (sub != null)
                LabelShadow(new Rect(panel.x, panel.y + 92f, panel.width, 28f), sub, _center, White);

            LabelShadow(new Rect(panel.x, panel.y + 134f, panel.width, 26f), hint, _center, TextDim);
        }
    }
}
