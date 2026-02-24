using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public Rigidbody rb;
    public float force = 10f;

    void Start()
    {
        rb.AddForce(Vector3.forward * force);
    }


// Update is called once per frame
void Update()
    {
        
    }
}
