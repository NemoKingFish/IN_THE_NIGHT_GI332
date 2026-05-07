using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LoopBgmSfx : MonoBehaviour
{
    [System.Serializable]
    public class BgmTrack
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private BgmTrack[] tracks;
    [SerializeField] private bool playDefaultOnStart = true;
    [SerializeField] private int defaultTrackIndex;
    [SerializeField] private float crossfadeDuration = 1.5f;
    [SerializeField] private bool loadDefaultResourceTracksIfEmpty = true;

    private AudioSource primarySource;
    private AudioSource secondarySource;
    private AudioSource activeSource;
    private Coroutine transitionRoutine;
    private int currentTrackIndex = -1;

    private void Awake()
    {
        EnsureSources();
        ResolveDefaultTracks();
    }

    private void Start()
    {
        if (!playDefaultOnStart || tracks == null || tracks.Length == 0)
        {
            return;
        }

        PlayTrack(defaultTrackIndex, true);
    }

    public void PlayTrack(int trackIndex)
    {
        PlayTrack(trackIndex, false);
    }

    public void PlayTrackById(string trackId)
    {
        if (tracks == null || string.IsNullOrWhiteSpace(trackId))
        {
            return;
        }

        for (var i = 0; i < tracks.Length; i++)
        {
            if (tracks[i] != null && tracks[i].id == trackId)
            {
                PlayTrack(i, false);
                return;
            }
        }
    }

    public void StopBgm(float fadeDuration = 0.8f)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(FadeOutRoutine(Mathf.Max(0f, fadeDuration)));
    }

    private void PlayTrack(int trackIndex, bool instant)
    {
        if (tracks == null || trackIndex < 0 || trackIndex >= tracks.Length)
        {
            return;
        }

        var track = tracks[trackIndex];
        if (track == null || track.clip == null)
        {
            return;
        }

        EnsureSources();

        if (currentTrackIndex == trackIndex && activeSource != null && activeSource.isPlaying)
        {
            return;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        var nextSource = activeSource == primarySource ? secondarySource : primarySource;
        if (nextSource == null)
        {
            return;
        }

        nextSource.clip = track.clip;
        nextSource.volume = instant ? track.volume : 0f;
        nextSource.loop = true;
        nextSource.Play();

        if (instant || activeSource == null || !activeSource.isPlaying)
        {
            if (activeSource != null && activeSource != nextSource)
            {
                activeSource.Stop();
                activeSource.volume = 0f;
            }

            activeSource = nextSource;
            currentTrackIndex = trackIndex;
            return;
        }

        transitionRoutine = StartCoroutine(CrossfadeRoutine(activeSource, nextSource, Mathf.Max(0.05f, crossfadeDuration), track.volume));
        activeSource = nextSource;
        currentTrackIndex = trackIndex;
    }

    private IEnumerator CrossfadeRoutine(AudioSource fromSource, AudioSource toSource, float duration, float targetVolume)
    {
        var startVolume = fromSource != null ? fromSource.volume : 0f;
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);

            if (fromSource != null)
            {
                fromSource.volume = Mathf.Lerp(startVolume, 0f, t);
            }

            if (toSource != null)
            {
                toSource.volume = Mathf.Lerp(0f, targetVolume, t);
            }

            yield return null;
        }

        if (fromSource != null)
        {
            fromSource.Stop();
            fromSource.volume = 0f;
        }

        if (toSource != null)
        {
            toSource.volume = targetVolume;
        }

        transitionRoutine = null;
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        if (activeSource == null || !activeSource.isPlaying)
        {
            yield break;
        }

        var source = activeSource;
        var startVolume = source.volume;
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        source.Stop();
        source.volume = 0f;
        currentTrackIndex = -1;
        transitionRoutine = null;
    }

    private void EnsureSources()
    {
        if (primarySource == null)
        {
            primarySource = GetComponent<AudioSource>();
        }

        if (primarySource == null)
        {
            primarySource = gameObject.AddComponent<AudioSource>();
        }

        if (secondarySource == null)
        {
            secondarySource = GetSecondarySource();
        }

        ConfigureSource(primarySource);
        ConfigureSource(secondarySource);

        if (activeSource == null)
        {
            activeSource = primarySource;
        }
    }

    private AudioSource GetSecondarySource()
    {
        var sources = GetComponents<AudioSource>();
        for (var i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null && sources[i] != primarySource)
            {
                return sources[i];
            }
        }

        return gameObject.AddComponent<AudioSource>();
    }

    private static void ConfigureSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
    }

    private void ResolveDefaultTracks()
    {
        if (!loadDefaultResourceTracksIfEmpty || tracks != null && tracks.Length > 0)
        {
            return;
        }

        var phase1 = Resources.Load<AudioClip>("Audio/Bgm/BGM_Phase1");
        var phase2 = Resources.Load<AudioClip>("Audio/Bgm/BGM_Phase2");
        var phase3 = Resources.Load<AudioClip>("Audio/Bgm/BGM_Phase3");

        tracks = new[]
        {
            new BgmTrack { id = "phase1", clip = phase1, volume = 0.85f },
            new BgmTrack { id = "phase2", clip = phase2, volume = 0.85f },
            new BgmTrack { id = "phase3", clip = phase3, volume = 0.9f }
        };
    }
}
