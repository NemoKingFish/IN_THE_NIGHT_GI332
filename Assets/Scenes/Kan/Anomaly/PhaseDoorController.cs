using UnityEngine;

[DisallowMultipleComponent]
public class PhaseDoorController : MonoBehaviour
{
    [SerializeField] private Transform doorTransform;
    [SerializeField] private Vector3 closedLocalPosition;
    [SerializeField] private Vector3 closedLocalEulerAngles;
    [SerializeField] private Vector3 openLocalPosition;
    [SerializeField] private Vector3 openLocalEulerAngles;
    [SerializeField] private bool disableCollidersWhenOpen = true;
    [SerializeField] private bool previewOpenInEditMode;

    private Collider[] cachedColliders;
    private Collider2D[] cachedColliders2D;
    private bool isOpen;

    private void Awake()
    {
        EnsureDoorTransform();
        CacheColliders();
        ApplyStateInstant(isOpen);
    }

    private void OnValidate()
    {
        EnsureDoorTransform();
        CacheColliders();

        if (Application.isPlaying)
        {
            return;
        }

        ApplyEditorPreview();
    }

    public void SetOpen(bool shouldOpen)
    {
        if (isOpen == shouldOpen)
        {
            return;
        }

        isOpen = shouldOpen;
        ApplyStateInstant(isOpen);
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    public bool IsPreviewOpenInEditMode()
    {
        return previewOpenInEditMode;
    }

    public void SetPreviewOpenInEditMode(bool shouldPreviewOpen)
    {
        previewOpenInEditMode = shouldPreviewOpen;

        if (!Application.isPlaying)
        {
            ApplyEditorPreview();
        }
    }

    private void ApplyStateInstant(bool shouldOpen)
    {
        EnsureDoorTransform();
        CacheColliders();

        if (doorTransform == null)
        {
            return;
        }

        doorTransform.localPosition = shouldOpen ? openLocalPosition : closedLocalPosition;
        doorTransform.localRotation = Quaternion.Euler(shouldOpen ? openLocalEulerAngles : closedLocalEulerAngles);

        if (!disableCollidersWhenOpen)
        {
            return;
        }

        var collidersEnabled = !shouldOpen;
        for (var i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
            {
                cachedColliders[i].enabled = collidersEnabled;
            }
        }

        for (var i = 0; i < cachedColliders2D.Length; i++)
        {
            if (cachedColliders2D[i] != null)
            {
                cachedColliders2D[i].enabled = collidersEnabled;
            }
        }
    }

    private void ApplyEditorPreview()
    {
        ApplyStateInstant(previewOpenInEditMode);
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
