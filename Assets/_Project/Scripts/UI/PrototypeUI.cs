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
        public UpgradeManager upgrades;
        public DashController dash;
        public PlayerCombat combat;
        public InputHandler input;
        public LevelManager levelManager;
        public PlayerWallet wallet;

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

            if (gm.CurrentState == GameState.UpgradeSelecting)
            {
                int index = input.SelectUpgradeIndex;
                if (index >= 0 && index < upgrades.CurrentChoices.Count) Choose(index);
                return;
            }

            if (gm.CurrentState == GameState.ShopOpen)
            {
                if (input.PausePressed)
                {
                    gm.SetState(GameState.Playing);
                    return;
                }

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

        void Choose(int index)
        {
            upgrades.ApplyUpgrade(index);
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
            GameManager.Instance.SetState(GameState.Playing);
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
                case GameState.UpgradeSelecting: DrawUpgradeSelect(gm, vw); break;
                case GameState.ShopOpen: DrawShop(gm, vw); break;
                case GameState.Paused: DrawPanel(vw, "已暂停", "Esc 继续      R 重开本关", Cyan); break;
                case GameState.Failed: DrawFailed(gm, vw); break;
                case GameState.Completed: DrawCompleted(gm, vw); break;
            }
        }

        // ============================================================ 绘制原语

        void Fill(Rect r, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        /// <summary>像素面板：深底 + 2px 亮框 + 1px 内暗线 + 四角像素点。全部整数矩形，永远硬边。</summary>
        void Panel(Rect r, Color border, Color bg, bool corners = true)
        {
            Fill(r, bg);

            // 外框 1px 亮
            Fill(new Rect(r.x, r.y, r.width, 1f), border);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), border);
            Fill(new Rect(r.x, r.y, 1f, r.height), border);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), border);

            // 第二圈暗（做一点厚度，但仍然是硬边）
            Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, 1f), new Color(border.r * 0.45f, border.g * 0.45f, border.b * 0.45f, 1f));
            Fill(new Rect(r.x + 1f, r.yMax - 2f, r.width - 2f, 1f), new Color(border.r * 0.3f, border.g * 0.3f, border.b * 0.3f, 1f));

            if (!corners) return;

            // 四角向内 3px 的装饰点
            Fill(new Rect(r.x + 4f, r.y + 4f, 2f, 2f), border);
            Fill(new Rect(r.xMax - 6f, r.y + 4f, 2f, 2f), border);
            Fill(new Rect(r.x + 4f, r.yMax - 6f, 2f, 2f), border);
            Fill(new Rect(r.xMax - 6f, r.yMax - 6f, 2f, 2f), border);
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

        void DrawHud(GameManager gm, float vw)
        {
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

                string dist = Mathf.RoundToInt(tracker.CurrentDistance) + "/" + Mathf.RoundToInt(tracker.TotalDistance);
                PixelNumber(20f, 78f, dist, 2.2f, Cyan);
                LabelShadow(new Rect(20f + PixelNumberWidth(dist, 2.2f) + 10f, 74f, 60f, 24f), "米", _small, TextDim, 1);
            }

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

            // ---- 底部操作提示：只在真正能操作时显示 ----
            if (gm.CurrentState != GameState.Playing) return;

            string hint = "空格 跳跃    J 攻击    Esc 暂停    R 重开";
            float hy = VirtualHeight - 42f;
            Fill(new Rect(20f, hy + 7f, 6f, 6f), CyanDim);
            LabelShadow(new Rect(32f, hy, vw - 60f, 24f), hint, _small, TextDim, 1);

            if (dash != null && dash.IsDashing)
            {
                LabelShadow(new Rect(vw - 200f, hy - 4f, 180f, 28f), "冲刺中", _hudTextR, Yellow);
            }
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

        void DrawUpgradeSelect(GameManager gm, float vw)
        {
            Backdrop(vw);

            LabelShadow(new Rect(0f, 108f, vw, 60f), "选择一个增益", _title);
            LabelShadow(new Rect(0f, 170f, vw, 26f), "点击卡片 或 按 1 / 2 / 3", _sub, TextDim, 1);

            const float cardW = 304f;
            const float cardH = 272f;
            const float gap = 26f;

            int count = upgrades.CurrentChoices.Count;
            float totalW = count * cardW + (count - 1) * gap;
            float startX = (vw - totalW) * 0.5f;

            Color[] accents = { Cyan, Yellow, Purple };

            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(startX + i * (cardW + gap), 214f, cardW, cardH);
                UpgradeDefinition def = upgrades.CurrentChoices[i];
                bool hover = rect.Contains(Event.current.mousePosition);

                Color accent = accents[i % accents.Length];
                Panel(rect, hover ? Yellow : CyanMid, hover ? PanelBgHi : PanelBg);

                // 顶部 4px 强调条（像素硬边，不做渐变）
                Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 4f), accent);

                // 第一行：序号（左）+ 等级（右）
                PixelNumber(rect.x + 16f, rect.y + 20f, (i + 1).ToString(), 2.2f, accent);

                int lv = upgrades.GetLevel(def.id);
                string lvText = lv + "/" + def.maxLevel;
                PixelNumber(rect.x + rect.width - 16f, rect.y + 22f, lvText, 1.8f, TextDim, true);

                // 标题
                LabelShadow(new Rect(rect.x + 16f, rect.y + 54f, rect.width - 32f, 40f), def.displayName, _cardTitle);

                // 描述
                LabelShadow(new Rect(rect.x + 24f, rect.y + 106f, rect.width - 48f, 120f), def.description, _cardDesc, TextDim, 1);

                // 分割线
                Fill(new Rect(rect.x + 20f, rect.y + rect.height - 56f, rect.width - 40f, 1f), CyanDim);

                // 底部按键框（居中）
                var keyBox = new Rect(rect.x + rect.width * 0.5f - 34f, rect.y + rect.height - 44f, 68f, 30f);
                Panel(keyBox, hover ? Yellow : CyanMid, Deep, false);
                LabelShadow(new Rect(keyBox.x, keyBox.y, keyBox.width, keyBox.height), "按" + (i + 1), _center, hover ? Yellow : White, 1);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) Choose(i);
            }
        }

        /// <summary>金币商店：布局和升级三选一一致，但卡片带价格，买不起会整体压暗。</summary>
        void DrawShop(GameManager gm, float vw)
        {
            Backdrop(vw);

            LabelShadow(new Rect(0f, 86f, vw, 56f), "商店", _title, Yellow);

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
            DrawPanel(vw, "失败", "R 重开", new Color32(255, 77, 109, 255), reason);
        }

        void DrawCompleted(GameManager gm, float vw)
        {
            Backdrop(vw);
            LabelShadow(new Rect(0f, 150f, vw, 64f), "通关", _title, Cyan);

            var panel = new Rect(vw * 0.5f - 340f, 236f, 680f, 210f);
            Panel(panel, Cyan, PanelBg);

            Fill(new Rect(panel.x + 2f, panel.y + 2f, panel.width - 4f, 4f), Cyan);

            string[] keys = { "用时", "受伤", "冲刺", "攻击" };
            string[] vals =
            {
                gm.RunTime.ToString("0.0") + "s",
                (health != null ? health.DamageTakenCount : 0).ToString(),
                (dash != null ? dash.DashCount : 0).ToString(),
                (combat != null ? combat.AttackCount : 0).ToString(),
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

            LabelShadow(new Rect(panel.x, panel.y + 146f, panel.width, 26f), "R 重开      Enter 下一关（首版仅一关）", _center, TextDim);
        }

        void DrawPanel(float vw, string title, string hint, Color accent, string sub = null)
        {
            Backdrop(vw);

            var panel = new Rect(vw * 0.5f - 250f, 244f, 500f, 184f);
            Panel(panel, accent, PanelBg);
            Fill(new Rect(panel.x + 2f, panel.y + 2f, panel.width - 4f, 4f), accent);

            LabelShadow(new Rect(panel.x, panel.y + 30f, panel.width, 56f), title, _title, accent);

            if (sub != null)
                LabelShadow(new Rect(panel.x, panel.y + 92f, panel.width, 28f), sub, _center, White);

            LabelShadow(new Rect(panel.x, panel.y + 134f, panel.width, 26f), hint, _center, TextDim);
        }
    }
}
