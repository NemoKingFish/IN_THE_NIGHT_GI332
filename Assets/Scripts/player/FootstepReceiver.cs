using UnityEngine;

public class FootstepReceiver : MonoBehaviour
{
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float volume = 0.5f;

    public void OnFootstep(AnimationEvent evt)
    {
        if (evt.animatorClipInfo.weight < 0.5f)
            return;

        if (footstepClips == null || footstepClips.Length == 0)
            return;

        var clip = footstepClips[Random.Range(0, footstepClips.Length)];
        AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }
}