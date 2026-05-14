using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-400)]
public class SoundManager : MonoBehaviour
{
    [System.Serializable]
    public class BgmTrack
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    public enum MusicPlaybackMode
    {
        Sequential = 0,
        Random = 1
    }

    private const string MasterVolumePrefKey = "SoundManager.MasterVolume";
    private const string MusicVolumePrefKey = "SoundManager.MusicVolume";
    private const string SfxVolumePrefKey = "SoundManager.SfxVolume";
    private const string VoiceVolumePrefKey = "SoundManager.VoiceVolume";

    [Header("Music Playback")]
    [SerializeField] private bool dontDestroyAcrossScenes = true;
    [SerializeField] private bool playMusicOnStart = true;
    [SerializeField] private bool loopPlaylist = true;
    [SerializeField] private MusicPlaybackMode musicPlaybackMode = MusicPlaybackMode.Sequential;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource secondaryMusicSource;
    [SerializeField] private List<AudioClip> musicTracks = new List<AudioClip>();
    [SerializeField] private BgmTrack[] bgmTracks;
    [SerializeField] private bool loadDefaultPhaseTracksIfEmpty = true;
    [SerializeField] private int defaultTrackIndex;
    [SerializeField] private float crossfadeDuration = 1.5f;
    [SerializeField] [Range(0f, 1f)] private float musicSourceBaseVolume = 1f;

    [Header("Volume Defaults")]
    [SerializeField] [Range(0f, 1f)] private float defaultMasterVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float defaultMusicVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float defaultSfxVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float defaultVoiceVolume = 1f;

    private static SoundManager instance;
    private readonly List<SoundCategoryEmitter> registeredEmitters = new List<SoundCategoryEmitter>();
    private int currentTrackIndex = -1;
    private bool hasStartedMusicPlayback;
    private AudioSource activeMusicSource;
    private Coroutine musicTransitionRoutine;
    private float primaryTrackBlend;
    private float secondaryTrackBlend;

    public static SoundManager Instance => instance;

    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;
    public float VoiceVolume { get; private set; } = 1f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (dontDestroyAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        ResolveMusicSources();
        ResolveDefaultTracks();
        LoadSavedVolumes();
        ApplyVolumeState();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        RefreshAllEmittersInScene();
        if (playMusicOnStart)
        {
            TryStartMusicPlayback();
        }
    }

    private void Update()
    {
        AdvancePlaylistWhenTrackEnds();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void OnValidate()
    {
        musicSourceBaseVolume = Mathf.Clamp01(musicSourceBaseVolume);
        crossfadeDuration = Mathf.Max(0f, crossfadeDuration);
        defaultMasterVolume = Mathf.Clamp01(defaultMasterVolume);
        defaultMusicVolume = Mathf.Clamp01(defaultMusicVolume);
        defaultSfxVolume = Mathf.Clamp01(defaultSfxVolume);
        defaultVoiceVolume = Mathf.Clamp01(defaultVoiceVolume);
    }

    public void RegisterEmitter(SoundCategoryEmitter emitter)
    {
        if (emitter == null)
        {
            return;
        }

        if (!registeredEmitters.Contains(emitter))
        {
            registeredEmitters.Add(emitter);
        }

        ApplyVolumeToEmitter(emitter);
    }

    public void UnregisterEmitter(SoundCategoryEmitter emitter)
    {
        if (emitter == null)
        {
            return;
        }

        registeredEmitters.Remove(emitter);
    }

    public void RefreshEmitter(SoundCategoryEmitter emitter)
    {
        if (emitter == null)
        {
            return;
        }

        if (!registeredEmitters.Contains(emitter))
        {
            registeredEmitters.Add(emitter);
        }

        ApplyVolumeToEmitter(emitter);
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterVolumePrefKey, MasterVolume);
        ApplyVolumeState();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumePrefKey, MusicVolume);
        ApplyVolumeState();
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumePrefKey, SfxVolume);
        ApplyVolumeState();
    }

    public void SetVoiceVolume(float value)
    {
        VoiceVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VoiceVolumePrefKey, VoiceVolume);
        ApplyVolumeState();
    }

    public void TryStartMusicPlayback()
    {
        ResolveMusicSources();
        ResolveDefaultTracks();
        if (bgmTracks == null || bgmTracks.Length == 0)
        {
            return;
        }

        hasStartedMusicPlayback = true;
        var trackIndex = defaultTrackIndex;
        if (trackIndex < 0 || trackIndex >= bgmTracks.Length)
        {
            trackIndex = 0;
        }

        if (musicPlaybackMode == MusicPlaybackMode.Random && bgmTracks.Length > 1)
        {
            trackIndex = Random.Range(0, bgmTracks.Length);
        }

        PlayMusicTrack(trackIndex, true);
    }

    public void StopMusic()
    {
        StopMusic(0.8f);
    }

    public void StopMusic(float fadeDuration)
    {
        hasStartedMusicPlayback = false;
        if (musicTransitionRoutine != null)
        {
            StopCoroutine(musicTransitionRoutine);
            musicTransitionRoutine = null;
        }

        musicTransitionRoutine = StartCoroutine(FadeOutMusicRoutine(Mathf.Max(0f, fadeDuration)));
    }

    public void PlayMusicTrack(int index)
    {
        PlayMusicTrack(index, false);
    }

    public void PlayMusicTrackById(string trackId)
    {
        if (bgmTracks == null || string.IsNullOrWhiteSpace(trackId))
        {
            return;
        }

        for (var i = 0; i < bgmTracks.Length; i++)
        {
            var track = bgmTracks[i];
            if (track != null && !string.IsNullOrWhiteSpace(track.id) && track.id == trackId)
            {
                PlayMusicTrack(i, false);
                return;
            }
        }
    }

    public float GetCategoryVolume(SoundCategory category)
    {
        return category switch
        {
            SoundCategory.Music => MusicVolume,
            SoundCategory.Voice => VoiceVolume,
            _ => SfxVolume
        };
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        RefreshAllEmittersInScene();
        ResolveMusicSources();
        ResolveDefaultTracks();

        if (playMusicOnStart && hasStartedMusicPlayback && bgmTracks != null && bgmTracks.Length > 0)
        {
            var clampedIndex = Mathf.Clamp(currentTrackIndex >= 0 ? currentTrackIndex : defaultTrackIndex, 0, bgmTracks.Length - 1);
            PlayMusicTrack(clampedIndex, true);
        }
    }

    private void ResolveMusicSources()
    {
        if (musicSource != null)
        {
            ConfigureMusicSource(musicSource);
        }
        else
        {
            var childTransform = transform.Find("__SoundManagerMusic");
            if (childTransform == null)
            {
                var musicObject = new GameObject("__SoundManagerMusic", typeof(AudioSource));
                childTransform = musicObject.transform;
                childTransform.SetParent(transform, false);
            }

            musicSource = childTransform.GetComponent<AudioSource>();
            ConfigureMusicSource(musicSource);
        }

        if (secondaryMusicSource != null)
        {
            ConfigureMusicSource(secondaryMusicSource);
        }
        else
        {
            var childTransform = transform.Find("__SoundManagerMusicSecondary");
            if (childTransform == null)
            {
                var musicObject = new GameObject("__SoundManagerMusicSecondary", typeof(AudioSource));
                childTransform = musicObject.transform;
                childTransform.SetParent(transform, false);
            }

            secondaryMusicSource = childTransform.GetComponent<AudioSource>();
            ConfigureMusicSource(secondaryMusicSource);
        }

        var primaryEmitter = SoundCategoryEmitter.Ensure(musicSource, SoundCategory.Music);
        if (primaryEmitter != null)
        {
            primaryEmitter.SetBaseVolume(musicSourceBaseVolume);
        }

        var secondaryEmitter = SoundCategoryEmitter.Ensure(secondaryMusicSource, SoundCategory.Music);
        if (secondaryEmitter != null)
        {
            secondaryEmitter.SetBaseVolume(musicSourceBaseVolume);
        }

        if (activeMusicSource == null)
        {
            activeMusicSource = musicSource;
        }
    }

    private void ConfigureMusicSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    private void LoadSavedVolumes()
    {
        MasterVolume = PlayerPrefs.GetFloat(MasterVolumePrefKey, defaultMasterVolume);
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumePrefKey, defaultMusicVolume);
        SfxVolume = PlayerPrefs.GetFloat(SfxVolumePrefKey, defaultSfxVolume);
        VoiceVolume = PlayerPrefs.GetFloat(VoiceVolumePrefKey, defaultVoiceVolume);
    }

    private void ApplyVolumeState()
    {
        AudioListener.volume = Mathf.Clamp01(MasterVolume);
        ApplyMusicSourceVolumes();

        for (var i = registeredEmitters.Count - 1; i >= 0; i--)
        {
            var emitter = registeredEmitters[i];
            if (emitter == null)
            {
                registeredEmitters.RemoveAt(i);
                continue;
            }

            ApplyVolumeToEmitter(emitter);
        }
    }

    private void ApplyVolumeToEmitter(SoundCategoryEmitter emitter)
    {
        if (emitter == null)
        {
            return;
        }

        emitter.ApplyCategoryVolume(GetCategoryVolume(emitter.Category));
    }

    private void RefreshAllEmittersInScene()
    {
        var emitters = FindObjectsByType<SoundCategoryEmitter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < emitters.Length; i++)
        {
            RegisterEmitter(emitters[i]);
        }
    }

    private void PlayMusicTrack(int index, bool instant)
    {
        ResolveMusicSources();
        ResolveDefaultTracks();
        if (bgmTracks == null || bgmTracks.Length == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, bgmTracks.Length - 1);
        var track = bgmTracks[index];
        if (track == null || track.clip == null)
        {
            return;
        }

        hasStartedMusicPlayback = true;
        if (IsTrackAlreadyPlaying(track.clip))
        {
            currentTrackIndex = index;
            NormalizePlayingTrack(track.clip, Mathf.Clamp01(track.volume));
            return;
        }

        if (musicTransitionRoutine != null)
        {
            StopCoroutine(musicTransitionRoutine);
            musicTransitionRoutine = null;
        }

        StopAndResetSource(musicSource);
        StopAndResetSource(secondaryMusicSource);

        var nextSource = musicSource != null ? musicSource : secondaryMusicSource;
        if (nextSource == null)
        {
            return;
        }

        nextSource.clip = track.clip;
        nextSource.time = 0f;
        nextSource.Play();
        activeMusicSource = nextSource;
        currentTrackIndex = index;
        SetSourceBlend(nextSource, Mathf.Clamp01(track.volume));
        SetSourceBlend(nextSource == musicSource ? secondaryMusicSource : musicSource, 0f);
    }

    private IEnumerator CrossfadeMusicRoutine(AudioSource fromSource, AudioSource toSource, float duration, float targetBlend)
    {
        var startBlend = GetSourceBlend(fromSource);
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            SetSourceBlend(fromSource, Mathf.Lerp(startBlend, 0f, t));
            SetSourceBlend(toSource, Mathf.Lerp(0f, targetBlend, t));
            yield return null;
        }

        SetSourceBlend(fromSource, 0f);
        if (fromSource != null)
        {
            fromSource.Stop();
        }

        SetSourceBlend(toSource, targetBlend);
        musicTransitionRoutine = null;
    }

    private IEnumerator FadeOutMusicRoutine(float duration)
    {
        if (activeMusicSource == null || !activeMusicSource.isPlaying)
        {
            yield break;
        }

        var source = activeMusicSource;
        var startBlend = GetSourceBlend(source);
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetSourceBlend(source, Mathf.Lerp(startBlend, 0f, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetSourceBlend(source, 0f);
        source.Stop();
        currentTrackIndex = -1;
        musicTransitionRoutine = null;
    }

    private bool IsTrackAlreadyPlaying(AudioClip clip)
    {
        if (clip == null)
        {
            return false;
        }

        return IsSourcePlayingClip(musicSource, clip) || IsSourcePlayingClip(secondaryMusicSource, clip);
    }

    private bool IsSourcePlayingClip(AudioSource source, AudioClip clip)
    {
        return source != null
            && source.isPlaying
            && source.clip == clip
            && GetSourceBlend(source) > 0.001f;
    }

    private void NormalizePlayingTrack(AudioClip clip, float targetBlend)
    {
        if (clip == null)
        {
            return;
        }

        var preferredSource = IsSourcePlayingClip(activeMusicSource, clip)
            ? activeMusicSource
            : (IsSourcePlayingClip(musicSource, clip) ? musicSource : secondaryMusicSource);

        if (preferredSource == null)
        {
            return;
        }

        var otherSource = preferredSource == musicSource ? secondaryMusicSource : musicSource;
        StopAndResetSource(otherSource);
        activeMusicSource = preferredSource;
        SetSourceBlend(preferredSource, targetBlend);
    }

    private void NormalizeCurrentPlaybackState()
    {
        var primaryBlend = GetSourceBlend(musicSource);
        var secondaryBlend = GetSourceBlend(secondaryMusicSource);
        var dominantSource = primaryBlend >= secondaryBlend ? musicSource : secondaryMusicSource;
        var secondarySource = dominantSource == musicSource ? secondaryMusicSource : musicSource;

        if (dominantSource != null && dominantSource.isPlaying)
        {
            activeMusicSource = dominantSource;
        }

        StopAndResetSource(secondarySource);
    }

    private void AdvancePlaylistWhenTrackEnds()
    {
        if (!hasStartedMusicPlayback || bgmTracks == null || bgmTracks.Length == 0)
        {
            return;
        }

        if (musicTransitionRoutine != null)
        {
            return;
        }

        if (activeMusicSource == null)
        {
            ResolveMusicSources();
        }

        if (activeMusicSource != null && activeMusicSource.isPlaying)
        {
            return;
        }

        var nextIndex = GetNextTrackIndex();
        if (nextIndex < 0)
        {
            return;
        }

        PlayMusicTrack(nextIndex, true);
    }

    private int GetNextTrackIndex()
    {
        if (bgmTracks == null || bgmTracks.Length == 0)
        {
            return -1;
        }

        if (musicPlaybackMode == MusicPlaybackMode.Random)
        {
            if (bgmTracks.Length == 1)
            {
                return loopPlaylist || currentTrackIndex < 0 ? 0 : -1;
            }

            if (!loopPlaylist && currentTrackIndex >= bgmTracks.Length - 1)
            {
                return -1;
            }

            var nextIndex = currentTrackIndex;
            while (nextIndex == currentTrackIndex)
            {
                nextIndex = Random.Range(0, bgmTracks.Length);
            }

            return nextIndex;
        }

        var sequentialIndex = currentTrackIndex + 1;
        if (sequentialIndex >= bgmTracks.Length)
        {
            return loopPlaylist ? 0 : -1;
        }

        return sequentialIndex;
    }

    private void StopAndResetSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        SetSourceBlend(source, 0f);
        source.Stop();
    }

    private void ResolveDefaultTracks()
    {
        if (bgmTracks != null && bgmTracks.Length > 0)
        {
            return;
        }

        if (musicTracks != null && musicTracks.Count > 0)
        {
            bgmTracks = new BgmTrack[musicTracks.Count];
            for (var i = 0; i < musicTracks.Count; i++)
            {
                bgmTracks[i] = new BgmTrack
                {
                    id = $"phase{i + 1}",
                    clip = musicTracks[i],
                    volume = 1f
                };
            }

            return;
        }

        if (!loadDefaultPhaseTracksIfEmpty)
        {
            return;
        }

        var phase1 = Resources.Load<AudioClip>("Audio/Bgm/BGM_Phase1");
        var phase2 = Resources.Load<AudioClip>("Audio/Bgm/BGM_Phase2");
        var phase3 = Resources.Load<AudioClip>("Audio/Bgm/BGM_Phase3");

        bgmTracks = new[]
        {
            new BgmTrack { id = "phase1", clip = phase1, volume = 0.85f },
            new BgmTrack { id = "phase2", clip = phase2, volume = 0.85f },
            new BgmTrack { id = "phase3", clip = phase3, volume = 0.9f }
        };
    }

    private void ApplyMusicSourceVolumes()
    {
        if (musicSource != null)
        {
            musicSource.volume = Mathf.Clamp01(primaryTrackBlend * musicSourceBaseVolume * MusicVolume);
        }

        if (secondaryMusicSource != null)
        {
            secondaryMusicSource.volume = Mathf.Clamp01(secondaryTrackBlend * musicSourceBaseVolume * MusicVolume);
        }
    }

    private float GetSourceBlend(AudioSource source)
    {
        if (source == null)
        {
            return 0f;
        }

        if (source == musicSource)
        {
            return primaryTrackBlend;
        }

        if (source == secondaryMusicSource)
        {
            return secondaryTrackBlend;
        }

        return 0f;
    }

    private void SetSourceBlend(AudioSource source, float blend)
    {
        if (source == null)
        {
            return;
        }

        if (source == musicSource)
        {
            primaryTrackBlend = Mathf.Clamp01(blend);
        }
        else if (source == secondaryMusicSource)
        {
            secondaryTrackBlend = Mathf.Clamp01(blend);
        }

        ApplyMusicSourceVolumes();
    }
}
