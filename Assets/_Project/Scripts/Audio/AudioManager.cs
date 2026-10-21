using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 极简音频管理：10 个 AudioSource 轮转播 SFX + 一条 BGM。
    /// 音频片段全部在上面序列化（可直接在 Inspector 里换 / 调音量），不走 Resources。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Serializable]
        public class SfxClip
        {
            public string id;
            public AudioClip clip;
            [Range(0f, 2f)] public float volumeScale = 1f;
        }

        public static AudioManager Instance { get; private set; }

        const int Voices = 10;

        [Header("音量")]
        [Range(0f, 1f)] public float masterVolume = 0.85f;
        [Range(0f, 1f)] public float bgmVolume = 0.30f;

        [Header("BGM")]
        public AudioClip bgm;

        [Header("音效（id 必须和代码里的一致）")]
        public SfxClip[] sfx =
        {
            new SfxClip { id = "sfx_dash" },
            new SfxClip { id = "sfx_impact" },
            new SfxClip { id = "sfx_hit" },
            new SfxClip { id = "sfx_kill" },
            new SfxClip { id = "sfx_jump" },
            new SfxClip { id = "sfx_land" },
            new SfxClip { id = "sfx_hurt" },
            new SfxClip { id = "sfx_select" },
            new SfxClip { id = "sfx_portal" },
        };

        AudioSource[] _sources;
        AudioSource _bgmSource;
        int _next;

        readonly Dictionary<string, SfxClip> _map = new Dictionary<string, SfxClip>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _map.Clear();
            if (sfx != null)
            {
                for (int i = 0; i < sfx.Length; i++)
                {
                    if (sfx[i] == null || string.IsNullOrEmpty(sfx[i].id)) continue;
                    _map[sfx[i].id] = sfx[i];
                }
            }

            _sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("SFX_" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.volume = masterVolume;
                _sources[i] = src;
            }

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.volume = bgmVolume;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Play(string id, float volume = 1f, float pitch = 1f)
        {
            if (Instance == null || string.IsNullOrEmpty(id)) return;
            if (!Instance._map.TryGetValue(id, out SfxClip entry) || entry.clip == null) return;

            AudioSource src = Instance._sources[Instance._next];
            Instance._next = (Instance._next + 1) % Voices;

            src.pitch = pitch;
            src.volume = Instance.masterVolume * volume * entry.volumeScale;
            src.clip = entry.clip;
            src.Play();
        }

        public static void PlayBgm(float volume = 1f)
        {
            if (Instance == null || Instance._bgmSource == null) return;
            if (Instance._bgmSource.isPlaying) return;
            if (Instance.bgm == null) return;

            Instance._bgmSource.clip = Instance.bgm;
            Instance._bgmSource.volume = Instance.bgmVolume * volume;
            Instance._bgmSource.Play();
        }
    }
}
