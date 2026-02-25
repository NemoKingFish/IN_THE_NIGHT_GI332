using UnityEngine;

public class LoopSFX : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
  
    public AudioSource source;

    void Start()
    {
        source.PlayScheduled(AudioSettings.dspTime);
        source.SetScheduledEndTime(AudioSettings.dspTime + source.clip.length);
    }
// Update is called once per frame
void Update()
    {
        
    }
}
