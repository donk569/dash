using UnityEngine;

namespace TiantaiDash
{
    /// <summary>敌人动画：直接把帧序列拖进来。无人机 4 帧悬停、炮塔 6 帧扫描。</summary>
    public class EnemyVisual : MonoBehaviour
    {
        [Header("帧序列（留空则按 variant 从素材总表取）")]
        public Sprite[] frames;

        [Header("variant: 0 = 无人机, 1 = 炮塔")]
        public int variant;
        public float fps = 8f;

        SpriteRenderer _sr;
        float _clock;

        public void SetFrames(Sprite[] newFrames, float newFps)
        {
            frames = newFrames;
            fps = newFps;
            if (_sr == null) _sr = GetComponentInChildren<SpriteRenderer>();
            if (_sr != null && frames != null && frames.Length > 0) _sr.sprite = frames[0];
        }

        /// <summary>兼容旧调用：variant 决定外观。</summary>
        public void SetVariant(int index)
        {
            variant = index;
            ArtLibrary art = ArtLibrary.Instance;
            if (art == null) return;
            SetFrames(index == 0 ? art.droneFrames : art.turretFrames, fps);
        }

        void Awake()
        {
            if (_sr == null) _sr = GetComponentInChildren<SpriteRenderer>();
            if ((frames == null || frames.Length == 0) && ArtLibrary.Instance != null) SetVariant(variant);
            else if (_sr != null && frames != null && frames.Length > 0) _sr.sprite = frames[0];
        }

        void Update()
        {
            if (_sr == null || frames == null || frames.Length < 2) return;
            _clock += Time.deltaTime;
            Sprite frame = ArtLibrary.Pick(frames, _clock, fps);
            if (frame != null) _sr.sprite = frame;
        }
    }
}
