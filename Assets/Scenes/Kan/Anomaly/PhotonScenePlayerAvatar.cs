using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class PhotonScenePlayerAvatar : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float sprintSpeed = 8.05f;
    [SerializeField] private float gravity = -18f;
    [SerializeField] private float remoteSmoothing = 12f;

    [Header("Footstep Audio")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip[] walkFootstepClips;
    [SerializeField] private AudioClip[] sprintFootstepClips;
    [SerializeField] private float walkStepInterval = 0.46f;
    [SerializeField] private float sprintStepInterval = 0.31f;
    [SerializeField] private float footstepMinSpeed = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float footstepVolume = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float sprintFootstepVolumeMultiplier = 0.75f;
    [Range(0f, 0.2f)] [SerializeField] private float footstepReplayPadding = 0.03f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2.2f;
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0.02f, 0.03f);

    [Header("Presentation")]
    [SerializeField] private Animator animator;
    [SerializeField] private TextMeshPro nameLabel;
    [SerializeField] private bool hideLocalBody = false;
    [SerializeField] private Transform localVisualRoot;
    [SerializeField] private Transform remoteVisualRoot;

    private CharacterController characterController;
    private Camera playerCamera;
    private Renderer[] renderers;
    private Renderer[] localRenderers;
    private Renderer[] remoteRenderers;
    private Animator[] childAnimators;
    private int ownerActorNumber;
    private bool isLocalPlayer;
    private float verticalVelocity;
    private float lookPitch;
    private Vector3 remoteTargetPosition;
    private Quaternion remoteTargetRotation;
    private float remoteTargetPitch;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private Vector3 lastAnimationSamplePosition;
    private float sampledHorizontalSpeed;
    private float footstepTimer;
    private float nextFootstepAllowedTime;

    public int OwnerActorNumber => ownerActorNumber;
    public bool IsLocalPlayer => isLocalPlayer;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        renderers = GetComponentsInChildren<Renderer>(true);
        childAnimators = GetComponentsInChildren<Animator>(true);
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.enabled = true;
        }

        EnsureFootstepAudioSource();
        ResolveDefaultFootstepClips();

        DisableExtraAnimators();

        if (cameraTarget == null)
        {
            var existingCameraTarget = FindChildRecursive(transform, "CameraTarget");
            if (existingCameraTarget != null)
            {
                cameraTarget = existingCameraTarget;
            }
        }

        if (nameLabel == null)
        {
            nameLabel = GetComponentInChildren<TextMeshPro>(true);
        }

        if (localVisualRoot == null)
        {
            localVisualRoot = FindChildRecursive(transform, "Player");
        }

        if (remoteVisualRoot == null)
        {
            remoteVisualRoot = FindChildRecursive(transform, "Dummy");
        }

        localRenderers = GetExclusiveRenderers(localVisualRoot, remoteVisualRoot);
        remoteRenderers = GetExclusiveRenderers(remoteVisualRoot, localVisualRoot);

        remoteTargetPosition = transform.position;
        remoteTargetRotation = transform.rotation;
        lastAnimationSamplePosition = transform.position;
    }

    private void Update()
    {
        if (isLocalPlayer)
        {
            UpdateLocalMovement();
            UpdateLocalLook();
            UpdateCameraTransform();
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, remoteTargetPosition, Time.deltaTime * remoteSmoothing);
            transform.rotation = Quaternion.Slerp(transform.rotation, remoteTargetRotation, Time.deltaTime * remoteSmoothing);
            lookPitch = Mathf.Lerp(lookPitch, remoteTargetPitch, Time.deltaTime * remoteSmoothing);
            UpdateCameraTransform();
        }

        UpdateAnimator();
        UpdateFootstepAudio();
    }

    private void LateUpdate()
    {
        UpdateNameLabelFacing();
    }

    public void Initialize(int actorNumber, bool isLocal, string displayName)
    {
        ownerActorNumber = actorNumber;
        isLocalPlayer = isLocal;
        gameObject.name = displayName;
        DisableExtraAnimators();
        SnapToGround();
        lastAnimationSamplePosition = transform.position;
        sampledHorizontalSpeed = 0f;
        footstepTimer = Random.Range(0f, 0.12f);
        nextFootstepAllowedTime = 0f;

        if (nameLabel != null)
        {
            nameLabel.text = displayName;
            nameLabel.gameObject.SetActive(!isLocal);
        }

        if (isLocalPlayer)
        {
            EnsureCamera();
            DisableNonPlayerCameras();
            ApplyVisualPresentation(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(false);
            }

            ApplyVisualPresentation(false);
        }
    }

    public void ApplyRemoteState(Vector3 position, Quaternion rotation, float pitch, string displayName)
    {
        remoteTargetPosition = position;
        remoteTargetRotation = rotation;
        remoteTargetPitch = pitch;

        if (nameLabel != null && !string.IsNullOrWhiteSpace(displayName))
        {
            nameLabel.text = displayName;
        }
    }

    public PhotonScenePlayerSpawnManager.PlayerStateData BuildState(string displayName)
    {
        return new PhotonScenePlayerSpawnManager.PlayerStateData
        {
            actorNumber = ownerActorNumber,
            position = transform.position,
            rotation = transform.rotation,
            pitch = lookPitch,
            displayName = displayName
        };
    }

    public void OnLand()
    {
    }

    public void ResetMotionState()
    {
        verticalVelocity = -2f;
        remoteTargetPosition = transform.position;
        remoteTargetRotation = transform.rotation;
        remoteTargetPitch = lookPitch;
        lastAnimationSamplePosition = transform.position;
        sampledHorizontalSpeed = 0f;
        footstepTimer = Random.Range(0f, 0.12f);
        nextFootstepAllowedTime = 0f;
    }

    private void UpdateLocalMovement()
    {
        if (GameplayPauseMenu.IsLocalPauseMenuOpen)
        {
            return;
        }

        var inputX = Input.GetAxisRaw("Horizontal");
        var inputZ = Input.GetAxisRaw("Vertical");
        var isSprinting = Input.GetKey(KeyCode.LeftShift);
        var input = new Vector3(inputX, 0f, inputZ);
        input = Vector3.ClampMagnitude(input, 1f);

        var moveWorld = (transform.forward * input.z) + (transform.right * input.x);
        var currentMoveSpeed = isSprinting ? sprintSpeed : moveSpeed;
        var horizontalVelocity = moveWorld * currentMoveSpeed;

        var grounded = IsGrounded();
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        var finalVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
        if (characterController != null)
        {
            characterController.Move(finalVelocity * Time.deltaTime);
        }
        else
        {
            transform.position += finalVelocity * Time.deltaTime;
        }
    }

    private void UpdateLocalLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        var mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        var mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mouseX, 0f, Space.Self);
        lookPitch = Mathf.Clamp(lookPitch - mouseY, minPitch, maxPitch);
    }

    private void UpdateCameraTransform()
    {
        if (playerCamera == null && !isLocalPlayer)
        {
            return;
        }

        var target = cameraTarget != null ? cameraTarget : transform;
        var targetPosition = target.position + (target.rotation * cameraOffset);

        if (playerCamera != null)
        {
            playerCamera.transform.position = targetPosition;
            playerCamera.transform.rotation = Quaternion.Euler(lookPitch, transform.eulerAngles.y, 0f);
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        var currentPosition = transform.position;
        var delta = currentPosition - lastAnimationSamplePosition;
        lastAnimationSamplePosition = currentPosition;

        var derivedHorizontalVelocity = new Vector3(delta.x, 0f, delta.z) / Mathf.Max(Time.deltaTime, 0.0001f);
        var controllerHorizontalVelocity = characterController != null
            ? new Vector3(characterController.velocity.x, 0f, characterController.velocity.z)
            : Vector3.zero;
        var horizontalVelocity = isLocalPlayer && characterController != null
            ? controllerHorizontalVelocity
            : derivedHorizontalVelocity;

        sampledHorizontalSpeed = Mathf.Lerp(sampledHorizontalSpeed, horizontalVelocity.magnitude, Time.deltaTime * 12f);
        var speed = sampledHorizontalSpeed;
        var grounded = IsGrounded();
        var referenceSpeed = Mathf.Max(0.01f, sprintSpeed);
        var normalizedSpeed = Mathf.Clamp01(speed / referenceSpeed);

        SetAnimatorFloatIfPresent("Speed", speed);
        SetAnimatorFloatIfPresent("MotionSpeed", normalizedSpeed);
        SetAnimatorFloatIfPresent("InputMagnitude", normalizedSpeed);
        SetAnimatorFloatIfPresent("MoveSpeed", normalizedSpeed);
        SetAnimatorFloatIfPresent("VerticalVelocity", verticalVelocity);
        SetAnimatorBoolIfPresent("Grounded", grounded);
        SetAnimatorBoolIfPresent("FreeFall", !grounded && verticalVelocity < -0.1f);
        SetAnimatorBoolIfPresent("Jump", false);
    }

    private void UpdateFootstepAudio()
    {
        if (footstepAudioSource == null)
        {
            return;
        }

        var grounded = IsGrounded();
        if (!grounded)
        {
            footstepTimer = 0f;
            return;
        }

        var speed = sampledHorizontalSpeed;
        if (speed < footstepMinSpeed)
        {
            footstepTimer = 0f;
            return;
        }

        var isSprinting = speed > (moveSpeed + sprintSpeed) * 0.5f;
        var clips = isSprinting && sprintFootstepClips != null && sprintFootstepClips.Length > 0
            ? sprintFootstepClips
            : walkFootstepClips;

        if (clips == null || clips.Length == 0)
        {
            return;
        }

        if (Time.time < nextFootstepAllowedTime)
        {
            return;
        }

        if (footstepAudioSource.isPlaying)
        {
            return;
        }

        footstepTimer += Time.deltaTime;
        var targetInterval = Mathf.Max(0.05f, isSprinting ? sprintStepInterval : walkStepInterval);
        if (footstepTimer < targetInterval)
        {
            return;
        }

        footstepTimer = 0f;
        var clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (clip == null)
        {
            return;
        }

        footstepAudioSource.pitch = isSprinting ? 1.08f : 1f;
        var stepVolume = footstepVolume * (isSprinting ? sprintFootstepVolumeMultiplier : 1f);
        footstepAudioSource.PlayOneShot(clip, Mathf.Clamp01(stepVolume));
        nextFootstepAllowedTime = Time.time + GetFootstepReplayDelay(clip, footstepAudioSource.pitch, targetInterval);
    }

    private bool IsGrounded()
    {
        if (characterController != null)
        {
            if (characterController.isGrounded)
            {
                return true;
            }

            var origin = transform.position + Vector3.up * 0.1f;
            var distance = GetGroundCheckDistance();
            var hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, distance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hitCount; i++)
            {
                var hitTransform = groundHits[i].collider != null ? groundHits[i].collider.transform : null;
                if (hitTransform == null || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                return true;
            }
        }

        var fallbackHitCount = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.1f, Vector3.down, groundHits, 0.4f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (var i = 0; i < fallbackHitCount; i++)
        {
            var hitTransform = groundHits[i].collider != null ? groundHits[i].collider.transform : null;
            if (hitTransform == null || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void EnsureCamera()
    {
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            playerCamera.enabled = true;
            return;
        }

        var cameraObject = new GameObject("LocalPlayerCamera");
        playerCamera = cameraObject.AddComponent<Camera>();
        playerCamera.tag = "MainCamera";
        playerCamera.nearClipPlane = 0.01f;
        playerCamera.fieldOfView = 75f;
        playerCamera.depth = 100f;
        cameraObject.AddComponent<AudioListener>();
        UpdateCameraTransform();
    }

    private void DisableNonPlayerCameras()
    {
        var allCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < allCameras.Length; i++)
        {
            var camera = allCameras[i];
            if (camera == null || camera == playerCamera)
            {
                continue;
            }

            camera.enabled = false;
            camera.gameObject.SetActive(false);
        }

        var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < listeners.Length; i++)
        {
            var listener = listeners[i];
            if (listener == null || playerCamera == null)
            {
                continue;
            }

            if (listener.gameObject == playerCamera.gameObject)
            {
                listener.enabled = true;
                continue;
            }

            listener.enabled = false;
        }
    }

    private void ApplyVisualPresentation(bool isLocalAvatar)
    {
        if (localRenderers.Length == 0 && remoteRenderers.Length == 0)
        {
            SetRendererGroupVisible(renderers, isLocalAvatar ? !hideLocalBody : true);
            return;
        }

        SetRendererGroupVisible(localRenderers, isLocalAvatar && !hideLocalBody);
        SetRendererGroupVisible(remoteRenderers, !isLocalAvatar);
    }

    private static void SetRendererGroupVisible(Renderer[] rendererGroup, bool visible)
    {
        if (rendererGroup == null)
        {
            return;
        }

        for (var i = 0; i < rendererGroup.Length; i++)
        {
            if (rendererGroup[i] != null)
            {
                rendererGroup[i].enabled = visible;
            }
        }
    }

    private void EnsureFootstepAudioSource()
    {
        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
        }

        if (footstepAudioSource == null)
        {
            footstepAudioSource = gameObject.AddComponent<AudioSource>();
        }

        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.loop = false;
        footstepAudioSource.spatialBlend = 1f;
        footstepAudioSource.rolloffMode = AudioRolloffMode.Linear;
        footstepAudioSource.minDistance = 1.2f;
        footstepAudioSource.maxDistance = 14f;
    }

    private void ResolveDefaultFootstepClips()
    {
        if (walkFootstepClips == null || walkFootstepClips.Length == 0)
        {
            var walkClip = Resources.Load<AudioClip>("Audio/Player/WalkDefault");
            if (walkClip != null)
            {
                walkFootstepClips = new[] { walkClip };
            }
        }

        if (sprintFootstepClips == null || sprintFootstepClips.Length == 0)
        {
            var sprintClip = Resources.Load<AudioClip>("Audio/Player/RunDefault");
            if (sprintClip != null)
            {
                sprintFootstepClips = new[] { sprintClip };
            }
        }
    }

    private float GetFootstepReplayDelay(AudioClip clip, float pitch, float fallbackInterval)
    {
        if (clip == null)
        {
            return Mathf.Max(0.05f, fallbackInterval);
        }

        var adjustedPitch = Mathf.Max(0.01f, pitch);
        var clipDuration = clip.length / adjustedPitch;
        return Mathf.Max(fallbackInterval, clipDuration + Mathf.Max(0f, footstepReplayPadding));
    }

    private void SnapToGround()
    {
        if (characterController == null)
        {
            return;
        }

        var origin = transform.position + Vector3.up * 2f;
        var hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 5f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (var i = 0; i < hitCount; i++)
        {
            var hitTransform = groundHits[i].collider != null ? groundHits[i].collider.transform : null;
            if (hitTransform == null || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            var targetY = groundHits[i].point.y - GetControllerBottomOffset() + characterController.skinWidth + 0.005f;
            transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
            verticalVelocity = -2f;
            return;
        }
    }

    private void UpdateNameLabelFacing()
    {
        if (nameLabel == null || !nameLabel.gameObject.activeInHierarchy)
        {
            return;
        }

        var targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        var directionToCamera = targetCamera.transform.position - nameLabel.transform.position;
        if (directionToCamera.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        nameLabel.transform.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
    }

    private float GetGroundCheckDistance()
    {
        if (characterController == null)
        {
            return 0.3f;
        }

        return Mathf.Abs(GetControllerBottomOffset()) + characterController.skinWidth + 0.15f;
    }

    private float GetControllerBottomOffset()
    {
        if (characterController == null)
        {
            return 0f;
        }

        return characterController.center.y - (characterController.height * 0.5f);
    }

    private void SetAnimatorFloatIfPresent(string parameterName, float value)
    {
        if (childAnimators == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        for (var i = 0; i < childAnimators.Length; i++)
        {
            var targetAnimator = childAnimators[i];
            if (HasAnimatorParameter(targetAnimator, parameterName, AnimatorControllerParameterType.Float))
            {
                targetAnimator.SetFloat(parameterName, value);
            }
        }
    }

    private void SetAnimatorBoolIfPresent(string parameterName, bool value)
    {
        if (childAnimators == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        for (var i = 0; i < childAnimators.Length; i++)
        {
            var targetAnimator = childAnimators[i];
            if (HasAnimatorParameter(targetAnimator, parameterName, AnimatorControllerParameterType.Bool))
            {
                targetAnimator.SetBool(parameterName, value);
            }
        }
    }

    private static bool HasAnimatorParameter(Animator targetAnimator, string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        var parameters = targetAnimator.parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == parameterType && parameters[i].name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private void DisableExtraAnimators()
    {
        if (childAnimators == null || childAnimators.Length == 0)
        {
            return;
        }

        for (var i = 0; i < childAnimators.Length; i++)
        {
            var candidate = childAnimators[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate == animator)
            {
                candidate.enabled = true;
                candidate.applyRootMotion = false;
                continue;
            }

            candidate.enabled = true;
            candidate.applyRootMotion = false;
        }
    }

    private static Renderer[] GetExclusiveRenderers(Transform includeRoot, Transform excludeRoot)
    {
        if (includeRoot == null)
        {
            return System.Array.Empty<Renderer>();
        }

        var group = includeRoot.GetComponentsInChildren<Renderer>(true);
        if (group == null || group.Length == 0 || excludeRoot == null)
        {
            return group ?? System.Array.Empty<Renderer>();
        }

        var filtered = new System.Collections.Generic.List<Renderer>(group.Length);
        for (var i = 0; i < group.Length; i++)
        {
            var renderer = group[i];
            if (renderer == null)
            {
                continue;
            }

            var rendererTransform = renderer.transform;
            if (rendererTransform == excludeRoot || rendererTransform.IsChildOf(excludeRoot))
            {
                continue;
            }

            filtered.Add(renderer);
        }

        return filtered.ToArray();
    }

    private static Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == targetName)
        {
            return parent;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var child = FindChildRecursive(parent.GetChild(i), targetName);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }
}
