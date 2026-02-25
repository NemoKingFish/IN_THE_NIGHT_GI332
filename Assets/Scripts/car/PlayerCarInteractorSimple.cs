using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerCarInteractorSimple : NetworkBehaviour
{
    [Header("Keys")]
    public KeyCode enterKey = KeyCode.E;
    public KeyCode exitKey = KeyCode.F;
    public KeyCode brakeKey = KeyCode.Space;

    [Header("Disable these while seated (player movement, camera controller, etc.)")]
    public MonoBehaviour[] disableWhenSeated;

    // State
    private bool seated;
    private CarSeatManager currentCar;
    private int currentSeatIndex = -1;

    // Nearby car (from trigger)
    private CarSeatManager nearbyCar;

    // Optional: cached local player object (owner)
    private Transform self;

    void Awake()
    {
        self = transform;
    }

    /// <summary>
    /// Called by CarSeatManager ClientRpc when you get seated/unseated.
    /// </summary>
    public void SetSeated(bool value, CarSeatManager car, int seatIndex)
    {
        seated = value;
        currentCar = car;
        currentSeatIndex = seatIndex;

        // Toggle movement / look scripts
        if (disableWhenSeated != null)
        {
            foreach (var m in disableWhenSeated)
                if (m != null) m.enabled = !seated;
        }

        // If unseated, clear seat index
        if (!seated)
        {
            currentCar = null;
            currentSeatIndex = -1;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        // ----- Not seated: press E to enter nearby car -----
        if (!seated)
        {
            if (Input.GetKeyDown(enterKey) && nearbyCar != null)
            {
                // ขอ driver ก่อน ถ้าไม่ได้ค่อย passenger (แบบง่ายและชัวร์)
                nearbyCar.RequestEnterServerRpc(true);
                nearbyCar.RequestEnterServerRpc(false);
            }
            return;
            Debug.Log($"[Enter] try enter car={nearbyCar.name}, isSpawned={nearbyCar.NetworkObject.IsSpawned}, isServer={NetworkManager.Singleton.IsServer}");
        }

        // ----- Seated: press F to exit -----
        if (Input.GetKeyDown(exitKey) && currentCar != null)
        {
            currentCar.RequestExitServerRpc();
            return;
        }

        // ----- If seated AND driver: send driving input -----
        if (currentCar != null && currentCar.car != null && IsDriverSeat(currentCar, currentSeatIndex))
        {
            float throttle = 0f;
            if (Input.GetKey(KeyCode.W)) throttle += 1f;
            if (Input.GetKey(KeyCode.S)) throttle -= 1f;

            float steer = 0f;
            if (Input.GetKey(KeyCode.D)) steer += 1f;
            if (Input.GetKey(KeyCode.A)) steer -= 1f;

            bool brake = Input.GetKey(brakeKey);

            currentCar.car.SubmitInputServerRpc(throttle, steer, brake);
        }
    }

    private bool IsDriverSeat(CarSeatManager car, int seatIndex)
    {
        if (car == null || car.seats == null) return false;
        if (seatIndex < 0 || seatIndex >= car.seats.Length) return false;
        return car.seats[seatIndex].isDriver;
    }

    // ----------------------------
    // Trigger detection (no raycast)
    // ----------------------------

    void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        // Preferred: dedicated trigger component
        var enterTrigger = other.GetComponent<CarEnterTrigger>();
        if (enterTrigger != null && enterTrigger.car != null)
        {
            nearbyCar = enterTrigger.car;
            return;
        }

        // Fallback: any collider under a car root that has CarSeatManager
        var car = other.GetComponentInParent<CarSeatManager>();
        if (car != null)
        {
            nearbyCar = car;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;

        // Preferred: dedicated trigger component
        var enterTrigger = other.GetComponent<CarEnterTrigger>();
        if (enterTrigger != null && enterTrigger.car == nearbyCar)
        {
            nearbyCar = null;
            return;
        }

        // Fallback: clear if exiting same car
        var car = other.GetComponentInParent<CarSeatManager>();
        if (car != null && car == nearbyCar)
        {
            nearbyCar = null;
        }

        var trig = other.GetComponent<CarEnterTrigger>();
        if (trig != null) Debug.Log("[Trigger] CarEnterTrigger found!");
    }


}