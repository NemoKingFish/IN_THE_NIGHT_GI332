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

    private Collider[] cachedColliders;
    private Collider2D[] cachedColliders2D;
    private bool isOpen;

    private void Awake()
    {
        if (doorTransform == null)
        {
            doorTransform = transform;
        }

        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedColliders2D = GetComponentsInChildren<Collider2D>(true);
        ApplyStateInstant(isOpen);
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

    private void ApplyStateInstant(bool shouldOpen)
    {
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
}
