using UnityEngine;

namespace TiantaiDash
{
    /// <summary>屋顶 NPC：按帧率循环几张 idle 帧，左右随机朝向。</summary>
    public class RoofNpc : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 3f;
        public bool faceLeft;

        SpriteRenderer _sr;
        float _clock;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null && faceLeft) _sr.flipX = true;
        }

        void Update()
        {
            if (_sr == null || frames == null || frames.Length == 0) return;

            _clock += Time.deltaTime;
            int i = (int)(_clock * Mathf.Max(0.5f, fps)) % frames.Length;
            if (frames[i] != null) _sr.sprite = frames[i];
        }
    }
}
