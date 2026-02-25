using Unity.Netcode;
using UnityEngine;

public class CarSeatManager : NetworkBehaviour
{
    [System.Serializable]
    public class Seat
    {
        public string name;
        public Transform seatPoint; // จุดยืน/นั่ง
        public bool isDriver;
        [HideInInspector]
        public NetworkVariable<ulong> occupantId =
            new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    }

    public Seat[] seats;
    public NetworkCarSimple car;

    void Awake()
    {
        if (car == null) car = GetComponent<NetworkCarSimple>();
    }

    public int FindFreeSeatIndex(bool wantDriver)
    {
        for (int i = 0; i < seats.Length; i++)
        {
            if (seats[i].isDriver != wantDriver) continue;
            if (seats[i].occupantId.Value == 0) return i;
        }
        return -1;
    }

    public int FindSeatOf(ulong playerId)
    {
        for (int i = 0; i < seats.Length; i++)
            if (seats[i].occupantId.Value == playerId) return i;
        return -1;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEnterServerRpc(bool wantDriver, ServerRpcParams rpc = default)
    {
        ulong pid = rpc.Receive.SenderClientId;
        if (FindSeatOf(pid) != -1) return; // นั่งอยู่แล้ว

        int idx = FindFreeSeatIndex(wantDriver);
        if (idx == -1) return;

        seats[idx].occupantId.Value = pid;

        // ถ้าเป็น driver -> set driver
        if (seats[idx].isDriver && car != null)
            car.SetDriverServerRpc(pid);

        // สั่งให้ client คนนั้นไป “ผูกตัวกับที่นั่ง”
        TeleportAndLockClientRpc(idx, pid);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestExitServerRpc(ServerRpcParams rpc = default)
    {
        ulong pid = rpc.Receive.SenderClientId;
        int idx = FindSeatOf(pid);
        if (idx == -1) return;

        seats[idx].occupantId.Value = 0;

        // ถ้าออกจาก driver -> clear driver
        if (seats[idx].isDriver && car != null && car.DriverId.Value == pid)
            car.SetDriverServerRpc(0);

        UnlockClientRpc(pid);
    }

    [ClientRpc]
    void TeleportAndLockClientRpc(int seatIndex, ulong playerId)
    {
        if (NetworkManager.Singleton.LocalClientId != playerId) return;

        var localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        if (localPlayer == null) return;

        // ย้ายไปที่นั่ง
        var t = localPlayer.transform;
        t.position = seats[seatIndex].seatPoint.position;
        t.rotation = seats[seatIndex].seatPoint.rotation;

        // ล็อกการเดิน/ฟิสิกส์ฝั่ง player (ทำใน interactor)
        var interactor = localPlayer.GetComponent<PlayerCarInteractorSimple>();
        if (interactor != null) interactor.SetSeated(true, this, seatIndex);
    }

    [ClientRpc]
    void UnlockClientRpc(ulong playerId)
    {
        if (NetworkManager.Singleton.LocalClientId != playerId) return;

        var localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        if (localPlayer == null) return;

        var interactor = localPlayer.GetComponent<PlayerCarInteractorSimple>();
        if (interactor != null) interactor.SetSeated(false, null, -1);
    }
}