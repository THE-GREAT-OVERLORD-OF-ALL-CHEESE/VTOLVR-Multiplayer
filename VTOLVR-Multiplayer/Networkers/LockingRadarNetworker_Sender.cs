﻿using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

class LockingRadarNetworker_Sender : MonoBehaviour
{
    public ulong networkUID;
    private Message_RadarUpdate lastRadarMessage;
    private Message_LockingRadarUpdate lastLockingMessage;
    private Radar radar;
    private LockingRadar lr;
    private bool lastOn = false;
    float lastFov;
    RadarLockData lastRadarLockData = new RadarLockData();
    private bool stateChanged;
    private void Awake()
    {
        Debug.Log("Radar sender awoken for object " + gameObject.name);
        lr = gameObject.GetComponentInChildren<LockingRadar>();
        if (lr == null)
        {
            Debug.LogError($"LockingRadar on networkUID {networkUID} is null");
            return;
        }
        lr.radar = gameObject.GetComponentInChildren<Radar>();
        if (lr.radar == null)
        {
            Debug.LogError($"Radar null on netUID {networkUID}");
        }
        else
        {
            Debug.Log($"Radar sender successfully attached to object {gameObject.name}.");
        }
        lastRadarMessage = new Message_RadarUpdate(true, 0, networkUID);
        lastLockingMessage = new Message_LockingRadarUpdate(0, false, networkUID);
    }
    private void Update()
    {
        if (lr == null)
        {
            Debug.LogError($"LockingRadar is null for object {gameObject.name} with an uid of {networkUID}.");
            lr = gameObject.GetComponentInChildren<LockingRadar>();
        }
        if (lr.radar == null)
        {
            Debug.LogError("This radar.radar shouldn't be null. If this error pops up a second time then be worried.");
            lr.radar = gameObject.GetComponentInChildren<Radar>();
        }
        if (lr.radar.radarEnabled != lastOn || lr.radar.sweepFov != lastFov)
        {
            Debug.Log("radar.radar is not equal to last on");
            lastRadarMessage.UID = networkUID;
            Debug.Log("last uid");
            lastRadarMessage.on = lr.radar.radarEnabled;
            Debug.Log("on enabled");
            lastRadarMessage.fov = lr.radar.sweepFov;
            Debug.Log("sweep fov and SENDING!");

            if (Networker.isHost)
                NetworkSenderThread.Instance.SendPacketAsHostToAllClients(lastRadarMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
            else
                NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastRadarMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
            Debug.Log("last 2");
            lastOn = lr.radar.radarEnabled;
            Debug.Log("last one");
            lastFov = lr.radar.sweepFov;
        }
        // if (lr.currentLock != lastRadarLockData) { stateChanged = true; }
        if (lr.currentLock != null)
        {
            if (lr.IsLocked() != lastLockingMessage.isLocked) { stateChanged = true; }
            if (lr.currentLock.actor != lastRadarLockData.actor)
            {
                lastRadarLockData.actor = lr.currentLock.actor;
                stateChanged = true;
            }
            if (lr.currentLock.locked != lastRadarLockData.locked)
            {
                lastRadarLockData.locked = lr.currentLock.locked;
                stateChanged = true;
            }
        }
        // if (lr.IsLocked() != lastLockingMessage.isLocked || lr.currentLock != null && (lr.currentLock != lastRadarLockData || lr.currentLock.actor != lastRadarLockData.actor))
        if (stateChanged)
        {
            Debug.Log("is lock not equal to last message is locked");
            if (lr.currentLock == null)
            {
                Debug.Log("lr lock data null");
                lastLockingMessage.actorUID = 0;
                lastLockingMessage.isLocked = false;

                lastLockingMessage.senderUID = networkUID;
                Debug.Log($"Sending a locking radar message to uID {networkUID}");
                if (Networker.isHost)
                    NetworkSenderThread.Instance.SendPacketAsHostToAllClients(lastLockingMessage, EP2PSend.k_EP2PSendReliable);
                else
                    NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastLockingMessage, EP2PSend.k_EP2PSendReliable);
            }
            else
            {
                Debug.Log("else going into foreach");
                foreach (var AI in AIManager.AIVehicles)
                {
                    if (AI.actor == lastRadarLockData.actor)
                    {
                        Debug.Log(lastRadarLockData.actor.name + " radar data found its lock " + AI.actor.name + " at id " + AI.vehicleUID + " with its own uID being " + networkUID);
                        lastLockingMessage.actorUID = AI.vehicleUID;
                        lastLockingMessage.isLocked = true;
                        lastLockingMessage.senderUID = networkUID;
                        if (Networker.isHost)
                            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(lastLockingMessage, EP2PSend.k_EP2PSendUnreliable);
                        else
                            NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastLockingMessage, EP2PSend.k_EP2PSendUnreliable);
                        break;
                    }
                }
            }
            stateChanged = false;
        }
    }
}
