using UnityEngine;

public class RandomFloor : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [Range(0f, 1f)]
    public float redChance = 0.3f; // 0.3 = 30% เป็นสีแดง

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();

        float rand = Random.value; // 0 - 1

        if (rand < redChance)
        {
            rend.material.color = Color.red; // เปลี่ยนเป็นสีแดง
        }

        // Update is called once per frame
        void Update()
        {

        }
    }

}
