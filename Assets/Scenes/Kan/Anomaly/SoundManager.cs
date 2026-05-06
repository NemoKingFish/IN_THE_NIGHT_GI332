using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-400)]
public class SoundManager : MonoBehaviour
{
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
    [SerializeField] private List<AudioClip> musicTracks = new List<AudioClip>();
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

        ResolveMusicSource();
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
        if (musicSource == null || musicTracks.Count == 0 || !hasStartedMusicPlayback)
        {
            return;
        }

        if (musicSource.isPlaying)
        {
            return;
        }

        PlayNextTrack();
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
        ResolveMusicSource();
        if (musicSource == null || musicTracks.Count == 0)
        {
            return;
        }

        hasStartedMusicPlayback = true;
        if (currentTrackIndex < 0 || currentTrackIndex >= musicTracks.Count || musicSource.clip == null)
        {
            if (musicPlaybackMode == MusicPlaybackMode.Random)
            {
                PlayRandomTrack();
                return;
            }

            currentTrackIndex = 0;
        }

        PlayTrackAtIndex(currentTrackIndex);
    }

    public void StopMusic()
    {
        hasStartedMusicPlayback = false;
        if (musicSource != null)
        {
            musicSource.Stop();
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
        ResolveMusicSource();

        if (playMusicOnStart && hasStartedMusicPlayback && musicSource != null && !musicSource.isPlaying && musicTracks.Count > 0)
        {
            PlayTrackAtIndex(Mathf.Clamp(currentTrackIndex, 0, musicTracks.Count - 1));
        }
    }

    private void ResolveMusicSource()
    {
        if (musicSource != null)
        {
            ConfigureMusicSource(musicSource);
            return;
        }

        var childTransform = transform.Find("__SoundManagerMusic");
        if (childTransform == null)
        {
            var musicObject = new GameObject("__SoundManagerMusic", typeof(AudioSource));
            childTransform = musicObject.transform;
            childTransform.SetParent(transform, false);
        }

        musicSource = childTransform.GetComponent<AudioSource>();
        ConfigureMusicSource(musicSource);
        var emitter = SoundCategoryEmitter.Ensure(musicSource, SoundCategory.Music);
        if (emitter != null)
        {
            emitter.SetBaseVolume(musicSourceBaseVolume);
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
        source.volume = Mathf.Clamp01(musicSourceBaseVolume);
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

        if (musicSource != null)
        {
            musicSource.volume = Mathf.Clamp01(musicSourceBaseVolume * MusicVolume);
        }

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

    private void PlayNextTrack()
    {
        if (musicTracks.Count == 0)
        {
            return;
        }

        if (musicPlaybackMode == MusicPlaybackMode.Random)
        {
            PlayRandomTrack();
            return;
        }

        var nextIndex = currentTrackIndex + 1;
        if (nextIndex >= musicTracks.Count)
        {
            if (!loopPlaylist)
            {
                hasStartedMusicPlayback = false;
                return;
            }

            nextIndex = 0;
        }

        PlayTrackAtIndex(nextIndex);
    }

    private void PlayRandomTrack()
    {
        if (musicTracks.Count == 0)
        {
            return;
        }

        var nextIndex = musicTracks.Count == 1 ? 0 : Random.Range(0, musicTracks.Count);
        if (musicTracks.Count > 1 && nextIndex == currentTrackIndex)
        {
            nextIndex = (nextIndex + 1) % musicTracks.Count;
        }

        PlayTrackAtIndex(nextIndex);
    }

    private void PlayTrackAtIndex(int index)
    {
        if (musicSource == null || musicTracks.Count == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, musicTracks.Count - 1);
        var nextTrack = musicTracks[index];
        if (nextTrack == null)
        {
            currentTrackIndex = index;
            PlayNextTrack();
            return;
        }

        currentTrackIndex = index;
        musicSource.clip = nextTrack;
        musicSource.volume = Mathf.Clamp01(musicSourceBaseVolume * MusicVolume);
        musicSource.Play();
    }
}
