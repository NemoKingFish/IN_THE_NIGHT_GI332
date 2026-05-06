using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class SoundCategoryEmitter : MonoBehaviour
{
    [SerializeField] private SoundCategory category = SoundCategory.Sfx;
    [SerializeField] private bool syncBaseVolumeFromAudioSourceOnEnable = true;
    [SerializeField] [Range(0f, 1f)] private float baseVolume = 1f;

    private AudioSource cachedAudioSource;

    public SoundCategory Category => category;
    public AudioSource TargetSource => ResolveAudioSource();
    public float BaseVolume => Mathf.Clamp01(baseVolume);

    private void Awake()
    {
        ResolveAudioSource();
        if (syncBaseVolumeFromAudioSourceOnEnable && cachedAudioSource != null)
        {
            baseVolume = Mathf.Clamp01(cachedAudioSource.volume);
        }
    }

    private void OnEnable()
    {
        ResolveAudioSource();
        if (syncBaseVolumeFromAudioSourceOnEnable && cachedAudioSource != null)
        {
            baseVolume = Mathf.Clamp01(cachedAudioSource.volume);
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.RegisterEmitter(this);
        }
    }

    private void Start()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.RegisterEmitter(this);
        }
    }

    private void OnDisable()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.UnregisterEmitter(this);
        }
    }

    private void OnDestroy()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.UnregisterEmitter(this);
        }
    }

    private void OnValidate()
    {
        baseVolume = Mathf.Clamp01(baseVolume);
        if (!Application.isPlaying && syncBaseVolumeFromAudioSourceOnEnable)
        {
            ResolveAudioSource();
            if (cachedAudioSource != null)
            {
                baseVolume = Mathf.Clamp01(cachedAudioSource.volume);
            }
        }
    }

    public void SetCategory(SoundCategory nextCategory)
    {
        if (category == nextCategory)
        {
            return;
        }

        category = nextCategory;
        NotifyManagerOfChange();
    }

    public void SetBaseVolume(float volume)
    {
        baseVolume = Mathf.Clamp01(volume);
        NotifyManagerOfChange();
    }

    public void CaptureCurrentVolumeAsBase()
    {
        ResolveAudioSource();
        if (cachedAudioSource == null)
        {
            return;
        }

        baseVolume = Mathf.Clamp01(cachedAudioSource.volume);
        NotifyManagerOfChange();
    }

    public void ApplyCategoryVolume(float categoryVolume)
    {
        ResolveAudioSource();
        if (cachedAudioSource == null)
        {
            return;
        }

        cachedAudioSource.volume = Mathf.Clamp01(baseVolume * Mathf.Clamp01(categoryVolume));
    }

    public static SoundCategoryEmitter Ensure(AudioSource source, SoundCategory category)
    {
        if (source == null)
        {
            return null;
        }

        var emitter = source.GetComponent<SoundCategoryEmitter>();
        if (emitter == null)
        {
            emitter = source.gameObject.AddComponent<SoundCategoryEmitter>();
        }

        emitter.category = category;
        emitter.cachedAudioSource = source;
        return emitter;
    }

    private AudioSource ResolveAudioSource()
    {
        if (cachedAudioSource == null)
        {
            cachedAudioSource = GetComponent<AudioSource>();
        }

        return cachedAudioSource;
    }

    private void NotifyManagerOfChange()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.RefreshEmitter(this);
        }
    }
}
