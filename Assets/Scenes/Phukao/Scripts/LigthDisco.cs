using UnityEngine;

public class LigthDisco : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public float changeSpeed = 0.15f;
    public float rotateSpeed = 60f;

    Light lightComp;

    void Start()
    {
        lightComp = GetComponent<Light>();
        InvokeRepeating(nameof(ChangeColor), 0f, changeSpeed);
    }

    void Update()
    {
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }

    void ChangeColor()
    {
        lightComp.color = Random.ColorHSV();
    }
}
