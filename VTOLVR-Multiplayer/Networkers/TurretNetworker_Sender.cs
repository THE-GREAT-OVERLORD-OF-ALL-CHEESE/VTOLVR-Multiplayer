﻿using UnityEngine;

class TurretNetworker_Sender : MonoBehaviour
{
    public ulong networkUID;
    public ulong turretID;
    private Message_TurretUpdate lastMessage;
    public ModuleTurret turret;

    private void Awake()
    {
        lastMessage = new Message_TurretUpdate(new Vector3D(), networkUID, turretID);
        if (turret == null)
        {
            turret = base.GetComponentInChildren<ModuleTurret>();
            if (turret == null)
            {
                Debug.LogError($"Turret was null on ID {networkUID}");
            }
        }
    }

    private void FixedUpdate()
    {
        Vector3D dir = new Vector3D(turret.pitchTransform.forward);
        lastMessage.direction = dir;

        lastMessage.UID = networkUID;
        lastMessage.turretID = turretID;
        if (Networker.isHost)
            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(lastMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        else
            NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
    }
}
