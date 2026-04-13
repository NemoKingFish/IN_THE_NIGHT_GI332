using System.Collections.Generic;
using UnityEngine;

public class PhotonScenePlayerAvatar : MonoBehaviour
{
    private static readonly HashSet<string> DisabledBehaviourNames = new HashSet<string>
    {
        "NetworkHeadBob",
        "PlayerCameraBootstrap",
        "NetworkPlayerInput",
        "NetworkPlayerMotor",
        "NetworkPlayerAnimation",
        "PlayerTrainInteractor",
        "NetworkPlayerSeatState"
    };

    private PhotonScenePlayerSpawnManager spawnManager;
    private CharacterController characterController;
    private Animator animator;
    private Transform cameraTarget;
    private Camera localCamera;
    private float verticalVelocity;
    private float cameraPitch = 12f;
    private float nextSendTime;
    private Vector3 remoteTargetPosition;
    private Quaternion remoteTargetRotation;
    private float remoteTargetPitch = 12f;
    private bool isLocalAvatar;
    private bool wasGrounded;
    private int actorNumber;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    private const float FirstPersonForwardOffset = 0.05f;
    private const float FirstPersonHeightOffset = 0.02f;

    public void Initialize(
        PhotonScenePlayerSpawnManager manager,
        int ownerActorNumber,
        string displayName,
        bool isLocal,
        Transform desiredCameraTarget)
    {
        spawnManager = manager;
        actorNumber = ownerActorNumber;
        isLocalAvatar = isLocal;
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        cameraTarget = desiredCameraTarget != null ? desiredCameraTarget : transform;
        remoteTargetPosition = transform.position;
        remoteTargetRotation = transform.rotation;
        remoteTargetPitch = cameraPitch;
        SnapToGroundImmediate();
        wasGrounded = IsGrounded();

        gameObject.name = $"{displayName} ({ownerActorNumber})";

        DisableConflictingBehaviours();
        DisableNestedCameras();
        ResetAnimatorToStandingPose();

        if (isLocalAvatar)
        {
            HideLocalCharacterMesh();
            CreateLocalCamera();
        }
    }

    private void Update()
    {
        if (spawnManager == null)
        {
            return;
        }

        if (isLocalAvatar)
        {
            UpdateLocalInput();
        }
        else
        {
            UpdateRemoteInterpolation();
        }
    }

    private void LateUpdate()
    {
        if (!isLocalAvatar || localCamera == null)
        {
            return;
        }

        UpdateCameraTransform();
    }

    private void OnDestroy()
    {
        if (localCamera != null)
        {
            Destroy(localCamera.gameObject);
        }
    }

    public void ApplyRemoteState(Vector3 position, Quaternion rotation, float pitch)
    {
        remoteTargetPosition = position;
        remoteTargetRotation = rotation;
        remoteTargetPitch = pitch;
    }

    private void DisableConflictingBehaviours()
    {
        foreach (var behaviour in GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            var type = behaviour.GetType();
            var fullName = type.FullName ?? string.Empty;
            var typeName = type.Name;

            if (fullName.Contains("Unity.Netcode") || DisabledBehaviourNames.Contains(typeName))
            {
                behaviour.enabled = false;
            }
        }
    }

    private void DisableNestedCameras()
    {
        foreach (var camera in GetComponentsInChildren<Camera>(true))
        {
            if (camera != null)
            {
                camera.enabled = false;
            }
        }

        foreach (var listener in GetComponentsInChildren<AudioListener>(true))
        {
            if (listener != null)
            {
                listener.enabled = false;
            }
        }
    }

    private void CreateLocalCamera()
    {
        var cameraObject = new GameObject($"Photon Player Camera {actorNumber}");
        localCamera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        cameraObject.tag = "MainCamera";

        localCamera.nearClipPlane = 0.03f;
        localCamera.fieldOfView = 60f;

        spawnManager.DisableSceneMainCameras(localCamera);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        UpdateCameraTransform();
    }

    private void UpdateLocalInput()
    {
        var canLookAround = Cursor.lockState == CursorLockMode.Locked;
        var mouseX = 0f;
        var mouseY = 0f;

        if (canLookAround)
        {
            mouseX = Input.GetAxis("Mouse X") * spawnManager.LookSensitivity;
            mouseY = Input.GetAxis("Mouse Y") * spawnManager.LookSensitivity;
        }

        transform.Rotate(0f, mouseX, 0f);
        cameraPitch = Mathf.Clamp(cameraPitch - mouseY, spawnManager.CameraPitchMin, spawnManager.CameraPitchMax);

        var moveInput = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        var moveDirection = (transform.forward * moveInput.z) + (transform.right * moveInput.x);
        moveDirection *= spawnManager.MoveSpeed;
        var requestedMoveAmount = new Vector3(moveDirection.x, 0f, moveDirection.z).magnitude;
        var jumpTriggered = false;

        if (characterController != null)
        {
            var isGrounded = IsGrounded();

            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -0.5f;
            }

            if (isGrounded && Input.GetButtonDown("Jump"))
            {
                verticalVelocity = Mathf.Sqrt(spawnManager.JumpHeight * -2f * spawnManager.Gravity);
                jumpTriggered = true;
            }

            verticalVelocity += spawnManager.Gravity * Time.deltaTime;
            moveDirection.y = verticalVelocity;
            characterController.Move(moveDirection * Time.deltaTime);

            var groundedAfterMove = IsGrounded();
            if (!wasGrounded && groundedAfterMove && verticalVelocity < 0f)
            {
                SnapToGroundImmediate();
                verticalVelocity = -0.5f;
            }

            wasGrounded = groundedAfterMove;
            UpdateAnimatorState(requestedMoveAmount, groundedAfterMove, jumpTriggered);
        }
        else
        {
            transform.position += moveDirection * Time.deltaTime;
            UpdateAnimatorState(requestedMoveAmount, true, jumpTriggered);
        }

        if (Time.time >= nextSendTime)
        {
            nextSendTime = Time.time + (1f / Mathf.Max(1f, spawnManager.NetworkSendRate));
            spawnManager.SendLocalAvatarState(transform.position, transform.rotation, cameraPitch);
        }
    }

    private void UpdateRemoteInterpolation()
    {
        var lastPosition = transform.position;
        transform.position = Vector3.Lerp(transform.position, remoteTargetPosition, Time.deltaTime * spawnManager.RemoteLerpSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, remoteTargetRotation, Time.deltaTime * spawnManager.RemoteLerpSpeed);
        cameraPitch = Mathf.Lerp(cameraPitch, remoteTargetPitch, Time.deltaTime * spawnManager.RemoteLerpSpeed);

        var frameVelocity = Time.deltaTime > 0f ? (transform.position - lastPosition) / Time.deltaTime : Vector3.zero;
        var horizontalSpeed = new Vector3(frameVelocity.x, 0f, frameVelocity.z).magnitude;
        var grounded = IsGrounded();
        var movingUp = frameVelocity.y > 0.15f;
        UpdateAnimatorState(horizontalSpeed, grounded, movingUp);
    }

    private void UpdateCameraTransform()
    {
        if (localCamera == null)
        {
            return;
        }

        var lookRotation = Quaternion.Euler(cameraPitch, transform.eulerAngles.y, 0f);
        var focusPoint = cameraTarget != null ? cameraTarget.position : transform.position + Vector3.up * 1.5f;
        var cameraPosition = focusPoint + Vector3.up * FirstPersonHeightOffset + (lookRotation * Vector3.forward * FirstPersonForwardOffset);

        localCamera.transform.SetPositionAndRotation(cameraPosition, lookRotation);
    }

    private bool IsGrounded()
    {
        if (characterController == null)
        {
            return false;
        }

        var sphereRadius = Mathf.Max(0.05f, characterController.radius * 0.9f);
        var origin = transform.position + characterController.center;
        var castDistance = (characterController.height * 0.5f) - characterController.radius + spawnManager.GroundedProbeDistance;

        return Physics.SphereCast(
            origin,
            sphereRadius,
            Vector3.down,
            out _,
            castDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
    }

    private void SnapToGroundImmediate()
    {
        if (spawnManager == null)
        {
            return;
        }

        var resolvedPosition = spawnManager.ResolveSpawnPosition(transform.position, characterController);
        transform.position = resolvedPosition;
        remoteTargetPosition = resolvedPosition;
    }

    private void ResetAnimatorToStandingPose()
    {
        if (animator == null)
        {
            return;
        }

        animator.Rebind();
        animator.Update(0f);
        animator.SetBool(GroundedHash, true);
        animator.SetBool(FreeFallHash, false);
        animator.SetBool(JumpHash, false);
        animator.SetFloat(SpeedHash, 0f);
        animator.SetFloat(MotionSpeedHash, 0f);
    }

    private void UpdateAnimatorState(float horizontalSpeed, bool grounded, bool jumpTriggered)
    {
        if (animator == null)
        {
            return;
        }

        var normalizedMotion = Mathf.Clamp01(horizontalSpeed / Mathf.Max(0.01f, spawnManager.MoveSpeed));
        var freeFall = !grounded && verticalVelocity < -1f;

        animator.SetFloat(SpeedHash, horizontalSpeed);
        animator.SetFloat(MotionSpeedHash, normalizedMotion);
        animator.SetBool(GroundedHash, grounded);
        animator.SetBool(FreeFallHash, freeFall);
        animator.SetBool(JumpHash, jumpTriggered);
    }

    public void OnLand()
    {
        if (verticalVelocity < 0f)
        {
            verticalVelocity = -0.5f;
        }

        if (animator == null)
        {
            return;
        }

        animator.SetBool(GroundedHash, true);
        animator.SetBool(FreeFallHash, false);
        animator.SetBool(JumpHash, false);
    }

    private void HideLocalCharacterMesh()
    {
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;
        }
    }
}
