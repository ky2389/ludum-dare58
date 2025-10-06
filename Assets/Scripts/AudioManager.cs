using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 简单稳健的 BGM 播放管理：播放列表 + 顺序/随机 + 循环 + 交叉淡化 + Next/Prev。
/// 使用方式：
/// 1) 在场景放一个物体挂上本脚本（或首次访问 Instance 自动生成）
/// 2) 在 Inspector 的 Default Playlist 填入 AudioClip 列表，勾选 Play On Awake
/// 3) 或者运行时：AudioManager.Instance.SetPlaylist(list, shuffle:true, loop:true); AudioManager.Instance.Play();
/// </summary>
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    #region Singleton
    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSources();
        ApplyVolumesImmediate();
        if (playOnAwake && defaultPlaylist != null && defaultPlaylist.Count > 0)
        {
            SetPlaylist(defaultPlaylist, shuffle, loopPlaylist);
            Play();
        }
    }
    #endregion

    [Header("Default Playlist")]
    public List<AudioClip> defaultPlaylist = new List<AudioClip>();
    public bool playOnAwake = true;
    public bool loopPlaylist = true;
    public bool shuffle = false;

    [Header("Playback")]
    [Tooltip("交叉淡入淡出时长（秒）。0=无交叉，直接切）")]
    [Range(0f, 10f)] public float crossfadeDuration = 1.5f;
    [Tooltip("两首歌之间的静默时间（秒），交叉时同样会插入（一般为0）")]
    [Range(0f, 5f)] public float silenceBetweenTracks = 0f;

    [Header("Volume")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 0.8f;
    [Tooltip("淡入第一首曲目的时长（不写就用 crossfadeDuration）")]
    [Range(0f, 10f)] public float firstTrackFadeIn = 1.5f;

    //[Header("Events")]
    [System.Serializable]
    public class TrackChangedEvent : UnityEvent<AudioClip, int> { } // clip, playlistIndex
    public TrackChangedEvent OnTrackStarted;

    [Header("Debug")]
    public bool logDebug = false;

    // 内部状态
    private AudioSource _a, _b;
    private bool _aIsActive = true;         // true=使用A作为当前播放
    private Coroutine _loopRoutine;

    private readonly List<AudioClip> _playlist = new List<AudioClip>();
    private readonly List<int> _order = new List<int>();  // 播放顺序（索引数组）
    private int _orderPos = -1;                           // 当前顺序位置
    private bool _isPlaying = false;

    // 跳转控制
    private bool _requestNext = false;
    private bool _requestPrev = false;
    private bool _requestStop = false;

    #region Public API
    /// <summary>替换播放列表（立即生效）。</summary>
    public void SetPlaylist(IEnumerable<AudioClip> clips, bool shuffleOrder = false, bool loop = true)
    {
        _playlist.Clear();
        if (clips != null)
            _playlist.AddRange(clips);

        shuffle = shuffleOrder;
        loopPlaylist = loop;

        BuildOrder();
        _orderPos = -1;
    }

    /// <summary>追加到播放列表末尾（不会自动打乱）。</summary>
    public void Enqueue(AudioClip clip)
    {
        if (!clip) return;
        _playlist.Add(clip);
        BuildOrder(); // 简便做法：重建顺序（如果在随机模式会重排）
    }

    /// <summary>开始播放（若未设置列表则播放默认列表）。</summary>
    public void Play()
    {
        if (_playlist.Count == 0 && defaultPlaylist.Count > 0)
        {
            SetPlaylist(defaultPlaylist, shuffle, loopPlaylist);
        }
        if (_loopRoutine != null) StopCoroutine(_loopRoutine);
        _requestStop = _requestNext = _requestPrev = false;
        _loopRoutine = StartCoroutine(PlayLoop());
    }

    public void Stop(bool immediate = false)
    {
        _requestStop = true;
        if (immediate)
        {
            if (_loopRoutine != null) StopCoroutine(_loopRoutine);
            _loopRoutine = null;
            HardStopAll();
        }
    }

    public void Pause()
    {
        ActiveSource().Pause();
        IdleSource().Pause();
        _isPlaying = false;
    }

    public void Resume()
    {
        ActiveSource().UnPause();
        IdleSource().UnPause();
        _isPlaying = true;
    }

    /// <summary>立刻切到下一首（使用较短交叉 0.25s）。</summary>
    public void Next() => _requestNext = true;

    /// <summary>立刻回到上一首（使用较短交叉 0.25s）。</summary>
    public void Previous() => _requestPrev = true;

    /// <summary>设置主音量与BGM音量。</summary>
    public void SetVolumes(float master, float bgm)
    {
        masterVolume = Mathf.Clamp01(master);
        bgmVolume = Mathf.Clamp01(bgm);
        ApplyVolumesImmediate();
    }
    #endregion

    #region Internals
    private void EnsureSources()
    {
        if (!_a) _a = gameObject.AddComponent<AudioSource>();
        if (!_b) _b = gameObject.AddComponent<AudioSource>();

        foreach (var s in new[] { _a, _b })
        {
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f; // 2D
            s.volume = 0f;
        }
    }

    private void ApplyVolumesImmediate()
    {
        float target = masterVolume * bgmVolume;
        // 不直接把两个都设成 target，保持“只有当前声道是 target，另一个为0”的逻辑
        ActiveSource().volume = Mathf.Min(ActiveSource().volume, target);
        IdleSource().volume = 0f;
    }

    private AudioSource ActiveSource() => _aIsActive ? _a : _b;
    private AudioSource IdleSource() => _aIsActive ? _b : _a;
    private void SwapActive() => _aIsActive = !_aIsActive;

    private void BuildOrder()
    {
        _order.Clear();
        for (int i = 0; i < _playlist.Count; i++) _order.Add(i);

        if (shuffle)
        {
            // Fisher-Yates
            for (int i = _order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = _order[i]; _order[i] = _order[j]; _order[j] = tmp;
            }
        }
    }

    private void HardStopAll()
    {
        _isPlaying = false;
        foreach (var s in new[] { _a, _b })
        {
            s.Stop();
            s.clip = null;
            s.volume = 0f;
        }
    }

    private int NextOrderPos(int from)
    {
        int p = from + 1;
        if (p >= _order.Count)
        {
            if (!loopPlaylist) return -1;
            BuildOrder();       // 重建顺序（随机模式会重新打乱）
            p = 0;
        }
        return p;
    }

    private int PrevOrderPos(int from)
    {
        int p = from - 1;
        if (p < 0)
        {
            if (!loopPlaylist) return -1;
            BuildOrder();       // 重新生成顺序，定位到最后一个
            p = Mathf.Max(0, _order.Count - 1);
        }
        return p;
    }

    private IEnumerator PlayLoop()
    {
        EnsureSources();
        _isPlaying = true;

        if (_playlist.Count == 0) yield break;
        if (_order.Count == 0) BuildOrder();
        if (_order.Count == 0) yield break;

        // 初次定位
        if (_orderPos < 0 || _orderPos >= _order.Count) _orderPos = 0;

        // -------- 播放第一首：淡入 --------
        int curIndex = _order[_orderPos];
        AudioClip curClip = _playlist[curIndex];
        if (!curClip)
        {
            if (logDebug) Debug.LogWarning("[AudioManager] Null clip in playlist, skipping.");
            yield return null;
        }
        else
        {
            var cur = ActiveSource();
            PrepareSource(cur, curClip);
            cur.volume = 0f;
            cur.Play();
            OnTrackStarted?.Invoke(curClip, curIndex);
            if (logDebug) Debug.Log($"[AudioManager] ▶ {curClip.name}");

            float targetVol = masterVolume * bgmVolume;
            float fadeIn = (firstTrackFadeIn > 0f) ? firstTrackFadeIn : crossfadeDuration;

            if (fadeIn > 0f)
            {
                float t = 0f;
                while (t < fadeIn && !_requestNext && !_requestPrev && !_requestStop)
                {
                    t += Time.deltaTime;
                    cur.volume = Mathf.Lerp(0f, targetVol, t / fadeIn);
                    yield return null;
                }
                cur.volume = targetVol;
            }
            else
            {
                cur.volume = targetVol;
            }
        }

        // 主循环：每首曲目在临近结束时与下一首交叉淡入
        while (_isPlaying && !_requestStop)
        {
            var cur = ActiveSource();
            if (cur.clip == null)
            {
                // 安全兜底
                _orderPos = NextOrderPos(_orderPos);
                if (_orderPos < 0) break;
                int idx = _order[_orderPos];
                var clip = _playlist[idx];
                PrepareSource(cur, clip);
                cur.volume = masterVolume * bgmVolume;
                cur.Play();
                OnTrackStarted?.Invoke(clip, idx);
            }

            // 等待到交叉时刻 / 或者“用户请求”提前切歌
            float targetVolNow = masterVolume * bgmVolume;
            float remain = cur.clip.length - cur.time;                       // 距离结束的剩余时长
            float waitBeforeCross = Mathf.Max(0f, remain - crossfadeDuration - silenceBetweenTracks);

            float timer = 0f;
            while (timer < waitBeforeCross && !_requestNext && !_requestPrev && !_requestStop)
            {
                // 动态跟随音量（Inspector改音量时）
                cur.volume = Mathf.Min(cur.volume, masterVolume * bgmVolume);
                timer += Time.deltaTime;
                yield return null;
            }

            // 处理“上一首/下一首/停止”的外部请求
            float forceCross = _requestPrev ? 0.25f : (_requestNext ? 0.25f : crossfadeDuration);
            if (_requestStop) break;

            int nextPos = _requestPrev ? PrevOrderPos(_orderPos) : NextOrderPos(_orderPos);
            _requestPrev = _requestNext = false;

            if (nextPos < 0)
            {
                // 没有下一首并且不循环
                // 等完当前曲目剩余部分后退出
                while (cur.isPlaying && !_requestStop)
                {
                    cur.volume = Mathf.Min(cur.volume, masterVolume * bgmVolume);
                    yield return null;
                }
                break;
            }

            int nextIndex = _order[nextPos];
            AudioClip nextClip = _playlist[nextIndex];
            if (!nextClip)
            {
                // 跳过无效 clip
                _orderPos = nextPos;
                continue;
            }

            // 插入静默（如需要）
            if (silenceBetweenTracks > 0f)
                yield return new WaitForSeconds(silenceBetweenTracks);

            // 准备下一首（在空闲声道）
            var nxt = IdleSource();
            PrepareSource(nxt, nextClip);
            nxt.volume = 0f;
            nxt.Play();
            OnTrackStarted?.Invoke(nextClip, nextIndex);
            if (logDebug) Debug.Log($"[AudioManager] ▶ {nextClip.name}");

            // 交叉淡化
            float cross = Mathf.Max(0f, forceCross);
            float t2 = 0f;
            float curStart = cur.volume;              // 可能不满（动态调过）
            float nxtTarget = targetVolNow;           // 以当前设置为目标音量

            while (t2 < cross && !_requestStop)
            {
                t2 += Time.deltaTime;
                float a = cross > 0f ? (t2 / cross) : 1f;
                cur.volume = Mathf.Lerp(curStart, 0f, a);
                nxt.volume = Mathf.Lerp(0f, nxtTarget, a);
                yield return null;
            }

            // 切换声道
            cur.Stop();
            cur.volume = 0f;
            SwapActive();
            _orderPos = nextPos;
        }

        // 退出：停掉所有播放
        HardStopAll();
        _loopRoutine = null;
    }

    private static void PrepareSource(AudioSource s, AudioClip clip)
    {
        s.clip = clip;
        s.loop = false;
        s.pitch = 1f;
        s.time = 0f;
    }
    #endregion

    #region Convenience - Auto bootstrap
    // 如果场景里没有手动放置 AudioManager，可通过此静态方法一键保证存在
    public static AudioManager Ensure()
    {
        if (Instance) return Instance;
        var go = new GameObject("~AudioManager");
        var m = go.AddComponent<AudioManager>();
        return m;
    }
    #endregion
}
