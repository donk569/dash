using UnityEngine;

namespace TiantaiDash
{
    /// <summary>横版跟随 + 震屏 + 变焦（冲刺时会拉远制造速度感）。</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(12f, 6f, -10f);
        public float smoothTime = 0.12f;
        public float minY = 8f;
        public float maxY = 22f;
        public float maxShake = 0.9f;

        public static CameraFollow Instance { get; private set; }

        Camera _cam;
        float _baseOrtho = 16.875f;
        Vector3 _velocity;
        float _trauma;
        float _zoom;
        float _zoomTarget;

        void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            if (_cam != null && _cam.orthographic) _baseOrtho = _cam.orthographicSize;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Shake(float amount)
        {
            if (Instance == null) return;
            Instance._trauma = Mathf.Clamp01(Instance._trauma + amount);
        }

        /// <summary>正数 = 拉远视野（冲刺提速用）。</summary>
        public static void SetZoom(float delta)
        {
            if (Instance == null) return;
            Instance._zoomTarget = delta;
        }

        void LateUpdate()
        {
            if (_cam != null)
            {
                _zoom = Mathf.Lerp(_zoom, _zoomTarget, 9f * Time.unscaledDeltaTime);
                _cam.orthographicSize = _baseOrtho + _zoom;
            }

            if (target == null) return;

            float desiredY = Mathf.Clamp(target.position.y + offset.y, minY, maxY);
            Vector3 desired = new Vector3(target.position.x + offset.x, desiredY, offset.z);
            Vector3 smooth = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);

            _trauma = Mathf.Max(0f, _trauma - Time.unscaledDeltaTime * 1.6f);
            float s = _trauma * _trauma;
            Vector3 shake = new Vector3(
                Random.Range(-1f, 1f) * s * maxShake,
                Random.Range(-1f, 1f) * s * maxShake,
                0f);

            transform.position = smooth + shake;
        }
    }
}
