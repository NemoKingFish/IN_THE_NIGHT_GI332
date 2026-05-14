using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LoopingDoorAnomalyController : MonoBehaviour, IAnomalyBehaviour
{
    [Header("References")]
    [SerializeField] private Transform doorTransform;

    [Header("Door States")]
    [SerializeField] private Vector3 closedLocalPosition;
    [SerializeField] private Vector3 closedLocalEulerAngles;
    [SerializeField] private Vector3 openLocalPosition;
    [SerializeField] private Vector3 openLocalEulerAngles;

    [Header("Loop Motion")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool startOpen;
    [SerializeField] private float moveDuration = 0.8f;
    [SerializeField] private float holdOpenDuration = 0.5f;
    [SerializeField] private float holdClosedDuration = 0.5f;
    [SerializeField] private bool useUnscaledTime;

    [Header("Collider Handling")]
    [SerializeField] private bool disableCollidersWhenOpen = true;

    [Header("Editor Preview")]
    [SerializeField] private bool previewOpenInEditMode;

    private Coroutine loopRoutine;
    private Collider[] cachedColliders;
    private Collider2D[] cachedColliders2D;
    private bool isOpen;
    private bool isLooping;

    private void Awake()
    {
        EnsureDoorTransform();
        CacheColliders();
        ApplyStateInstant(startOpen);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (playOnEnable)
        {
            StartLooping();
        }
        else
        {
            ApplyStateInstant(startOpen);
        }
    }

    private void OnDisable()
    {
        StopLooping(false);
    }

    private void OnValidate()
    {
        EnsureDoorTransform();
        CacheColliders();
        moveDuration = Mathf.Max(0.01f, moveDuration);
        holdOpenDuration = Mathf.Max(0f, holdOpenDuration);
        holdClosedDuration = Mathf.Max(0f, holdClosedDuration);

        if (Application.isPlaying)
        {
            return;
        }

        ApplyStateInstant(previewOpenInEditMode);
    }

    public void StartLooping()
    {
        EnsureDoorTransform();
        CacheColliders();

        if (doorTransform == null)
        {
            return;
        }

        StopLooping(false);
        isLooping = true;
        loopRoutine = StartCoroutine(LoopDoorRoutine());
    }

    public void StopLooping(bool snapClosed = true)
    {
        isLooping = false;

        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        if (snapClosed)
        {
            ApplyStateInstant(false);
        }
    }

    public void SetLooping(bool shouldLoop)
    {
        if (shouldLoop)
        {
            StartLooping();
            return;
        }

        StopLooping(true);
    }

    public void SetOpenInstant(bool shouldOpen)
    {
        StopLooping(false);
        ApplyStateInstant(shouldOpen);
    }

    public bool IsLooping()
    {
        return isLooping;
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    public void OnAnomalyActivated(AnomalySpawnPoint spawnPoint, AnomalyType anomalyType)
    {
        StartLooping();
    }

    public void OnAnomalyDeactivated(AnomalySpawnPoint spawnPoint)
    {
        StopLooping(true);
    }

    private IEnumerator LoopDoorRoutine()
    {
        ApplyStateInstant(startOpen);

        while (isLooping)
        {
            yield return WaitForDuration(isOpen ? holdOpenDuration : holdClosedDuration);
            yield return AnimateState(!isOpen);
        }
    }

    private IEnumerator AnimateState(bool shouldOpen)
    {
        EnsureDoorTransform();
        CacheColliders();

        if (doorTransform == null)
        {
            yield break;
        }

        var fromPosition = doorTransform.localPosition;
        var fromRotation = doorTransform.localRotation;
        var toPosition = shouldOpen ? openLocalPosition : closedLocalPosition;
        var toRotation = Quaternion.Euler(shouldOpen ? openLocalEulerAngles : closedLocalEulerAngles);
        var elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / moveDuration);
            doorTransform.localPosition = Vector3.LerpUnclamped(fromPosition, toPosition, t);
            doorTransform.localRotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, t);
            yield return null;
        }

        ApplyStateInstant(shouldOpen);
    }

    private object WaitForDuration(float duration)
    {
        if (useUnscaledTime)
        {
            return new WaitForSecondsRealtime(duration);
        }

        return new WaitForSeconds(duration);
    }

    private void ApplyStateInstant(bool shouldOpen)
    {
        EnsureDoorTransform();
        CacheColliders();

        if (doorTransform == null)
        {
            return;
        }

        isOpen = shouldOpen;
        doorTransform.localPosition = shouldOpen ? openLocalPosition : closedLocalPosition;
        doorTransform.localRotation = Quaternion.Euler(shouldOpen ? openLocalEulerAngles : closedLocalEulerAngles);
        ApplyColliderState(shouldOpen);
    }

    private void ApplyColliderState(bool shouldOpen)
    {
        if (!disableCollidersWhenOpen)
        {
            return;
        }

        var shouldEnableColliders = !shouldOpen;

        for (var index = 0; index < cachedColliders.Length; index++)
        {
            if (cachedColliders[index] != null)
            {
                cachedColliders[index].enabled = shouldEnableColliders;
            }
        }

        for (var index = 0; index < cachedColliders2D.Length; index++)
        {
            if (cachedColliders2D[index] != null)
            {
                cachedColliders2D[index].enabled = shouldEnableColliders;
            }
        }
    }

    private void EnsureDoorTransform()
    {
        if (doorTransform == null)
        {
            doorTransform = transform;
        }
    }

    private void CacheColliders()
    {
        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        if (cachedColliders2D == null || cachedColliders2D.Length == 0)
        {
            cachedColliders2D = GetComponentsInChildren<Collider2D>(true);
        }
    }
}
