using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 进度 / 节点触发。默认 **从场景自动收集**：所有 UpgradeNode 的 X 就是增益节点位置，
    /// PortalExit 的 X 就是终点。这样你在场景里拖动平台、增删节点、挪终点都不需要改任何数据。
    ///
    /// 想手动指定就把 autoCollect 关掉，然后自己填 nodeDistances / startX / endX。
    /// </summary>
    public class DistanceTracker : MonoBehaviour
    {
        [Header("目标")]
        [Tooltip("留空自动找场景里的 PlayerHealth")]
        public Transform player;

        [Header("距离")]
        [Tooltip("起点 X（一般就是出生点）。留 0 会取玩家初始位置")]
        public float startX;
        [Tooltip("终点 X；开启自动收集时会被 PortalExit 的位置覆盖")]
        public float endX = 890f;
        [Tooltip("增益节点 X 坐标；开启自动收集时会被所有 UpgradeNode 覆盖")]
        public float[] nodeDistances = new float[0];

        [Header("自动收集")]
        public bool autoCollect = true;

        public event Action<int> OnNodeReached;

        bool[] _reached;
        bool _collected;

        public float CurrentDistance => player != null ? Mathf.Max(0f, player.position.x - startX) : 0f;
        public float TotalDistance => Mathf.Max(1f, endX - startX);
        public float Progress01 => Mathf.Clamp01(CurrentDistance / TotalDistance);

        void Awake()
        {
            if (player == null)
            {
                var hp = FindObjectOfType<PlayerHealth>();
                if (hp != null) player = hp.transform;
            }

            if (autoCollect) CollectFromScene();
            else _reached = new bool[nodeDistances.Length];
        }

        /// <summary>把场景里的 UpgradeNode / PortalExit 读进来。编辑器工具也会调这个方法。</summary>
        public void CollectFromScene()
        {
            var nodes = FindObjectsOfType<UpgradeNode>();
            var xs = new List<float>(nodes.Length);
            for (int i = 0; i < nodes.Length; i++) xs.Add(nodes[i].TriggerX);
            xs.Sort();

            // 去掉重复的（同一个 X 上有两个节点没意义）
            for (int i = xs.Count - 1; i > 0; i--)
            {
                if (Mathf.Abs(xs[i] - xs[i - 1]) < 0.5f) xs.RemoveAt(i);
            }

            nodeDistances = xs.ToArray();

            var portal = FindObjectOfType<PortalExit>();
            if (portal != null) endX = portal.transform.position.x;

            if (player != null && startX <= 0f) startX = player.position.x;

            _reached = new bool[nodeDistances.Length];
            _collected = true;
        }

        public void Configure(Transform playerTransform, float start, float end, float[] nodes)
        {
            player = playerTransform;
            startX = start;
            endX = end;
            nodeDistances = nodes ?? new float[0];
            _reached = new bool[nodeDistances.Length];
            _collected = true;
        }

        void Update()
        {
            if (player == null) return;

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            if (_reached == null || _reached.Length != nodeDistances.Length)
                _reached = new bool[nodeDistances.Length];

            float x = player.position.x;
            for (int i = 0; i < nodeDistances.Length; i++)
            {
                if (_reached[i] || x < nodeDistances[i]) continue;
                _reached[i] = true;
                OnNodeReached?.Invoke(i);
            }
        }

        public void ResetState()
        {
            if (!_collected && autoCollect) CollectFromScene();
            _reached = new bool[nodeDistances.Length];
        }
    }
}
