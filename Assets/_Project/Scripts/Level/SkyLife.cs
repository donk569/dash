using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 天空活起来：背景撒一层会呼吸闪烁的星星，每隔几秒放一发烟花。
    /// 星星/烟花都在城市剪影后面（sortingOrder 更低），只在"远处"出现。
    /// </summary>
    public class SkyLife : MonoBehaviour
    {
        [Header("星星")]
        [Tooltip("额外撒的星星数量；天空贴图本身已带星点，所以默认 0（多了会像噪点）")]
        public int starCount = 0;
        public float minX = 0f;
        public float maxX = 1750f;
        public float minY = 10f;
        public float maxY = 42f;
        public int starSortingOrder = -380;

        [Header("烟花")]
        public float fireworkMinDelay = 6f;
        public float fireworkMaxDelay = 14f;
        public int fireworkSortingOrder = -370;

        Sprite _star;
        Sprite _spark;
        float _nextFirework;

        void Start()
        {
            var art = ArtLibrary.Instance;
            if (art != null)
            {
                _star = art.starSprite;
                _spark = art.sparkSprite;
            }

            if (_star != null && starCount > 0) BuildStars();
            _nextFirework = Time.time + Random.Range(2f, 5f);
        }

        void BuildStars()
        {
            var rng = new System.Random(424242);
            var root = new GameObject("Stars").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < starCount; i++)
            {
                var go = new GameObject("Star");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(
                    Mathf.Lerp(minX, maxX, (float)rng.NextDouble()),
                    Mathf.Lerp(minY, maxY, (float)rng.NextDouble()),
                    0f);
                go.transform.localScale = Vector3.one * (0.5f + (float)rng.NextDouble() * 0.9f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _star;
                sr.sortingOrder = starSortingOrder;
                sr.color = new Color(1f, 1f, 1f, 0.8f);

                var tw = go.AddComponent<StarTwinkle>();
                tw.phase = (float)rng.NextDouble() * 6.283f;
                tw.speed = 0.7f + (float)rng.NextDouble() * 2.4f;
            }
        }

        void Update()
        {
            if (Time.time < _nextFirework) return;
            _nextFirework = Time.time + Random.Range(fireworkMinDelay, fireworkMaxDelay);
            SpawnFirework();
        }

        void SpawnFirework()
        {
            if (_spark == null) return;

            Camera cam = Camera.main;
            float cx = cam != null ? cam.transform.position.x : 0f;
            Vector3 pos = new Vector3(cx + Random.Range(-12f, 12f), Random.Range(12f, 24f), 0f);

            Color[] palette =
            {
                new Color(1f, 0.36f, 0.72f),
                new Color(0.42f, 0.92f, 1f),
                new Color(1f, 0.82f, 0.45f),
            };
            Color col = palette[Random.Range(0, palette.Length)];

            int n = Random.Range(16, 24);
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 2f * i / n + Random.Range(-0.12f, 0.12f);
                float sp = Random.Range(5f, 11f);
                FxSprite fx = FxSprite.Spawn(_spark, pos,
                    new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp,
                    col, 1.15f, 2f, fireworkSortingOrder);
                if (fx != null)
                {
                    fx.drag = 1.1f;
                    fx.shrink = true;
                }
            }

            AudioManager.Play("sfx_portal", 0.22f, 1.6f);
        }
    }
}
