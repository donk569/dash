using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 天空层的动态元素。全部挂在**相机子节点**，跟着相机走，不依赖关卡长度。
    ///
    /// 元素清单：
    /// - 月亮光晕：双周期呼吸（忽明忽暗）
    /// - 流星：定时从右上划到左下
    /// - 探照灯：楼顶射出的光锥，左右缓慢扫动
    /// - 漂浮广告牌：从右侧升起、缓慢上浮、霓虹闪烁
    /// - 无人机编队：3~5 只排成一列横穿画面
    ///
    /// 之前那辆自制卡车和整体画风不搭，已经去掉。
    /// </summary>
    public class SkyEvents : MonoBehaviour
    {
        [Header("素材（留空用素材总表）")]
        public Sprite meteorSprite;
        public Sprite moonGlowSprite;
        public Sprite lightConeSprite;
        public Sprite billboardSprite;
        public Sprite[] droneFrames;

        [Header("月亮")]
        public Transform moonGlow;
        public float moonPulseCycle = 9f;
        public float moonGlowMin = 0.10f;
        public float moonGlowMax = 0.30f;

        [Header("流星")]
        public bool meteorEnabled = true;
        public float meteorIntervalMin = 3.5f;
        public float meteorIntervalMax = 9f;
        public float meteorSpeed = 62f;
        public float meteorLength = 4.5f;

        [Header("探照灯")]
        public bool searchlightEnabled = true;
        [Range(0, 4)] public int searchlightCount = 2;
        public float searchlightSweep = 16f;
        public float searchlightSpeed = 0.38f;

        [Header("漂浮广告牌")]
        public bool billboardEnabled = true;
        public float billboardIntervalMin = 5.5f;
        public float billboardIntervalMax = 12f;
        public float billboardScale = 1.8f;

        [Header("无人机编队")]
        public bool squadEnabled = true;
        public float squadIntervalMin = 7f;
        public float squadIntervalMax = 15f;
        public float squadSpeed = 13f;

        [Header("生成范围（相机局部坐标）")]
        public float spawnX = 46f;
        public float despawnX = -46f;
        public float spawnYMin = 4f;
        public float spawnYMax = 22f;

        class Mover
        {
            public Transform Tr;
            public Vector2 Vel;
            public float Life;
            public float Fade;
        }

        readonly List<Mover> _movers = new List<Mover>();
        readonly List<Transform> _searchlights = new List<Transform>();
        readonly List<float> _searchPhase = new List<float>();
        readonly List<SpriteRenderer> _billboards = new List<SpriteRenderer>();

        float _meteorTimer;
        float _billboardTimer;
        float _squadTimer;
        float _clock;
        Color _moonBase = Color.white;
        float _moonBaseScale = 1f;

        void Awake()
        {
            FetchAssets();

            _meteorTimer = Random.Range(meteorIntervalMin, meteorIntervalMax) * 0.5f;
            _billboardTimer = Random.Range(billboardIntervalMin, billboardIntervalMax) * 0.6f;
            _squadTimer = Random.Range(squadIntervalMin, squadIntervalMax) * 0.5f;

            if (moonGlow != null)
            {
                var sr = moonGlow.GetComponent<SpriteRenderer>();
                if (sr != null) _moonBase = sr.color;
                _moonBaseScale = moonGlow.localScale.x;
            }

            BuildSearchlights();
        }

        void FetchAssets()
        {
            ArtLibrary art = ArtLibrary.Instance;
            if (art == null) return;

            if (meteorSprite == null) meteorSprite = art.meteorHead;
            if (moonGlowSprite == null) moonGlowSprite = art.moonGlow;
            if (lightConeSprite == null) lightConeSprite = art.lightCone;
            if (billboardSprite == null) billboardSprite = art.billboard;
            if (droneFrames == null || droneFrames.Length == 0) droneFrames = art.droneFrames;
        }

        /// <summary>蓄力时把流星 / 无人机 / 广告牌也停住，强化"世界静止"的观感。</summary>
        public void SetPaused(bool paused)
        {
            _paused = paused;
        }

        bool _paused;

        void Update()
        {
            if (_paused) return;

            float dt = Time.deltaTime;
            _clock += dt;

            TickMoon();
            TickSearchlights();
            TickMeteors(dt);
            TickBillboards(dt);
            TickSquads(dt);
            TickMovers(dt);
        }

        // ---------------- 月亮 ----------------

        void TickMoon()
        {
            if (moonGlow == null) return;

            var sr = moonGlow.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            float a = Mathf.Sin(_clock * Mathf.PI * 2f / Mathf.Max(0.01f, moonPulseCycle));
            float b = Mathf.Sin(_clock * Mathf.PI * 2f / Mathf.Max(0.01f, moonPulseCycle * 0.37f));
            float t = Mathf.Clamp01(0.5f + 0.35f * a + 0.15f * b);

            Color c = _moonBase;
            c.a = Mathf.Lerp(moonGlowMin, moonGlowMax, t);
            sr.color = c;

            // 以场景里设定的 scale 为基准做呼吸，不再写死 1~1.12，
            // 否则烘焙时调好的光晕大小会被这里覆盖掉
            float s = _moonBaseScale * Mathf.Lerp(1f, 1.08f, t);
            moonGlow.localScale = new Vector3(s, s, 1f);
        }

        // ---------------- 探照灯 ----------------

        void BuildSearchlights()
        {
            if (!searchlightEnabled || lightConeSprite == null) return;

            float[] xs = { -14f, 9f, 22f, -26f };

            for (int i = 0; i < Mathf.Min(searchlightCount, xs.Length); i++)
            {
                var go = new GameObject("Searchlight" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(xs[i], -12f, 21f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = lightConeSprite;
                sr.color = new Color(0.72f, 0.92f, 1f, 0.20f);
                sr.sortingOrder = -340;

                _searchlights.Add(go.transform);
                _searchPhase.Add(i * 1.7f);
            }
        }

        void TickSearchlights()
        {
            for (int i = 0; i < _searchlights.Count; i++)
            {
                Transform t = _searchlights[i];
                if (t == null) continue;

                float a = Mathf.Sin(_clock * searchlightSpeed * Mathf.PI * 2f + _searchPhase[i]);
                t.localRotation = Quaternion.Euler(0f, 0f, a * searchlightSweep);

                var sr = t.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = 0.13f + 0.10f * (0.5f + 0.5f * a);
                    sr.color = c;
                }
            }
        }

        // ---------------- 流星 ----------------

        void TickMeteors(float dt)
        {
            if (!meteorEnabled) return;

            _meteorTimer -= dt;
            if (_meteorTimer > 0f) return;

            _meteorTimer = Random.Range(meteorIntervalMin, meteorIntervalMax);
            SpawnMeteor();
        }

        void SpawnMeteor()
        {
            var go = new GameObject("Meteor");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(
                spawnX * Random.Range(0.55f, 1f),
                Random.Range(spawnYMin + 6f, spawnYMax + 4f),
                22f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = meteorSprite != null ? meteorSprite : PixelArt.White;
            sr.color = new Color(1f, 1f, 0.95f, 0.9f);
            sr.sortingOrder = -300;

            go.transform.localScale = new Vector3(meteorLength, 1.2f, 1f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, -32f);

            _movers.Add(new Mover
            {
                Tr = go.transform,
                Vel = new Vector2(-meteorSpeed, -meteorSpeed * 0.62f),
                Life = 1.1f,
                Fade = 0.9f,
            });
        }

        // ---------------- 漂浮广告牌 ----------------

        void TickBillboards(float dt)
        {
            if (!billboardEnabled || billboardSprite == null) return;

            _billboardTimer -= dt;
            if (_billboardTimer <= 0f)
            {
                _billboardTimer = Random.Range(billboardIntervalMin, billboardIntervalMax);
                SpawnBillboard();
            }

            // 霓虹闪烁（偶尔来一下接触不良的暗闪）
            for (int i = _billboards.Count - 1; i >= 0; i--)
            {
                var sr = _billboards[i];
                if (sr == null) { _billboards.RemoveAt(i); continue; }

                float flick = 0.75f + 0.25f * Mathf.Sin(_clock * 11f + i * 2.1f);
                if (Mathf.Sin(_clock * 3.3f + i) > 0.97f) flick *= 0.35f;

                Color c = sr.color;
                c.a = flick;
                sr.color = c;
            }
        }

        void SpawnBillboard()
        {
            var go = new GameObject("Billboard");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(spawnX * 0.9f, Random.Range(-10f, 4f), 19f);
            go.transform.localScale = Vector3.one * billboardScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = billboardSprite;
            sr.color = new Color(0.85f, 0.95f, 1f, 0.9f);
            sr.sortingOrder = -308;

            _billboards.Add(sr);
            _movers.Add(new Mover
            {
                Tr = go.transform,
                Vel = new Vector2(-6.5f, 1.1f),
                Life = 9f,
                Fade = 1.5f,
            });
        }

        // ---------------- 无人机编队 ----------------

        void TickSquads(float dt)
        {
            if (!squadEnabled) return;

            _squadTimer -= dt;
            if (_squadTimer > 0f) return;

            _squadTimer = Random.Range(squadIntervalMin, squadIntervalMax);
            SpawnSquad();
        }

        void SpawnSquad()
        {
            if (droneFrames == null || droneFrames.Length == 0) return;

            int n = Random.Range(3, 6);
            float baseY = Random.Range(spawnYMin + 2f, spawnYMax);

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("SquadDrone" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(spawnX + i * 3.2f, baseY + i * 0.55f, 20f);
                go.transform.localScale = Vector3.one * 1.4f;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = droneFrames[Random.Range(0, droneFrames.Length)];
                sr.color = new Color(0.75f, 0.9f, 1f, 0.85f);
                sr.sortingOrder = -306;

                var anim = go.AddComponent<SpriteAnim>();
                anim.frames = droneFrames;
                anim.fps = 8f;

                _movers.Add(new Mover
                {
                    Tr = go.transform,
                    Vel = new Vector2(-squadSpeed, 0.2f),
                    Life = 12f,
                    Fade = 2f,
                });
            }
        }

        // ---------------- 通用移动 / 淡出 ----------------

        void TickMovers(float dt)
        {
            for (int i = _movers.Count - 1; i >= 0; i--)
            {
                Mover m = _movers[i];

                if (m.Tr == null || m.Life <= 0f || m.Tr.localPosition.x < despawnX)
                {
                    if (m.Tr != null) Destroy(m.Tr.gameObject);
                    _movers.RemoveAt(i);
                    continue;
                }

                m.Life -= dt;
                m.Tr.localPosition += (Vector3)(m.Vel * dt);

                if (m.Life < m.Fade)
                {
                    var sr = m.Tr.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Color c = sr.color;
                        c.a = Mathf.Clamp01(m.Life / m.Fade) * 0.9f;
                        sr.color = c;
                    }
                }
            }
        }
    }
}
