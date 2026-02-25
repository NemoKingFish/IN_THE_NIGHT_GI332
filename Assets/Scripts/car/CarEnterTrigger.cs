using UnityEngine;

public class CarEnterTrigger : MonoBehaviour
{
    public CarSeatManager car; // ลาก CarSeatManager ใส่

    void Reset()
    {
        // auto หาให้ ถ้าวางในรถ
        if (car == null) car = GetComponentInParent<CarSeatManager>();
    }
}