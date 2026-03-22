using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NetworkPlayerMotor))]
public class NetworkPlayerAnimation : NetworkBehaviour
{
    private Animator animator;
    private NetworkPlayerMotor motor;

    // hash เพื่อ performance
    private int speedHash;
    private int jumpHash;
    private int groundedHash;
    private int freeFallHash;
    private int motionSpeedHash;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        motor = GetComponent<NetworkPlayerMotor>();

        speedHash = Animator.StringToHash("Speed");
        jumpHash = Animator.StringToHash("Jump");
        groundedHash = Animator.StringToHash("Grounded");
        freeFallHash = Animator.StringToHash("FreeFall");
        motionSpeedHash = Animator.StringToHash("MotionSpeed");
    }

    private void Update()
    {
        if (!IsServer)
            return;

        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (motor == null)
            return;

        Vector3 velocity = motor.HorizontalVelocity;
        velocity.y = 0f;

        float speed = velocity.magnitude;

        bool grounded = motor.IsGrounded;

        // ===== SET PARAM =====
        animator.SetFloat(speedHash, speed);
        animator.SetBool(groundedHash, grounded);

        // MotionSpeed เอาไว้ blend run/walk
        animator.SetFloat(motionSpeedHash, speed > 0.1f ? 1f : 0f);

        // ===== JUMP / FALL =====
        if (!grounded)
        {
            if (motor.VerticalVelocity > 0.1f)
            {
                animator.SetBool(jumpHash, true);
                animator.SetBool(freeFallHash, false);
            }
            else
            {
                animator.SetBool(jumpHash, false);
                animator.SetBool(freeFallHash, true);
            }
        }
        else
        {
            animator.SetBool(jumpHash, false);
            animator.SetBool(freeFallHash, false);
        }
    }
}