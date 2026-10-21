using UnityEngine;

namespace TiantaiDash
{
    /// <summary>通用逐帧动画：帧序列直接在 Inspector 里拖，按 fps 循环换图。</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnim : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 8f;
        [Tooltip("每次循环随机起点，多个同款动画不会完全同步")]
        public bool randomStart = false;

        SpriteRenderer _sr;
        float _clock;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = GetComponentInChildren<SpriteRenderer>();
            if (randomStart) _clock = Random.Range(0f, 4f);
            if (_sr != null && frames != null && frames.Length > 0) _sr.sprite = frames[0];
        }

        public void Play(Sprite[] newFrames, float newFps)
        {
            frames = newFrames;
            fps = Mathf.Max(0.01f, newFps);
            _clock = 0f;
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null && frames != null && frames.Length > 0) _sr.sprite = frames[0];
        }

        void Update()
        {
            if (_sr == null || frames == null || frames.Length < 2) return;
            _clock += Time.deltaTime;
            _sr.sprite = ArtLibrary.Pick(frames, _clock, fps);
        }
    }
}
