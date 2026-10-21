using UnityEngine;

namespace TiantaiDash
{
    /// <summary>终点传送门：Playing 态触达即通关，只触发一次。</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PortalExit : MonoBehaviour
    {
        bool _fired;

        public void Configure(float width, float height)
        {
            var col = GetComponent<BoxCollider2D>();
            if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(width, height);
            col.offset = new Vector2(0f, height * 0.5f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_fired) return;
            if (other.GetComponentInParent<PlayerHealth>() == null) return;

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            _fired = true;
            AudioManager.Play("sfx_portal", 1f);
            gm.SetState(GameState.Completed);
        }
    }
}
