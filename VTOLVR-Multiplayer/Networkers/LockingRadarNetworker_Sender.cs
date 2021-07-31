﻿using Steamworks;
using System;
using UnityEngine;

class LockingRadarNetworker_Sender : MonoBehaviour
{
    public ulong networkUID;
    private Message_RadarUpdate lastRadarMessage;
    private Message_LockingRadarUpdate lastLockingMessage;
    private LockingRadar lr;
    private bool lastOn = false;
    float lastFov;
    RadarLockData lastRadarLockData = new RadarLockData();
    private bool stateChanged;
    private bool lastWasNull = true;
    ulong lastID;
    private float tick = 0.0f;
    public float tickRate = 0.5f;
    TacticalSituationController controller;
    private void Awake()
    {
        DebugCustom.Log("Radar sender awoken for object " + gameObject.name);
        lr = gameObject.GetComponentInChildren<LockingRadar>();
        if (lr == null)
        {
            DebugCustom.LogError($"LockingRadar on networkUID {networkUID} is null");
            return;
        }
        lr.radar = gameObject.GetComponentInChildren<Radar>();
        if (lr.radar == null)
        {
            DebugCustom.LogError($"Radar null on netUID {networkUID}");
        }
        else
        {
            lr.radar.OnDetectedActor += RadarDetectedActor;
            // Debug.Log($"Radar sender successfully attached to object {gameObject.name}.");
        }
        controller = gameObject.GetComponentInChildren<TacticalSituationController>();
        if (controller != null)
        {
            DebugCustom.Log($"{networkUID} is a player F45.");
            controller.OnAutoRadarLocked += F45LockedUpdate;
            controller.OnAutoRadarUnlocked += F45UnlockedUpdate;
        }
        lastRadarMessage = new Message_RadarUpdate(true, 0, networkUID);
        lastLockingMessage = new Message_LockingRadarUpdate(0, false, networkUID);

        tick += UnityEngine.Random.Range(0.0f, tickRate);
    }
    private void FixedUpdate()
    {
        tick += Time.fixedDeltaTime;

        if(tick<tickRate)
        {
            return;
        }
        tick = 0.0f;
        if (lr == null)
        {
            DebugCustom.LogError($"LockingRadar is null for object {gameObject.name} with an uid of {networkUID}.");
            lr = gameObject.GetComponentInChildren<LockingRadar>();
        }
        if (lr == null)
            return;
        if (lr.radar == null)
        {
            //Debug.LogError("This radar.radar shouldn't be null. If this error pops up a second time then be worried. Null on " + gameObject.name);
            lr.radar = gameObject.GetComponentInChildren<Radar>();
        }
        if (lr.radar != null)
        {
            
            {
                // Debug.Log("radar.radar is not equal to last on");
                lastRadarMessage.UID = networkUID;
                // Debug.Log("last uid");
                lastRadarMessage.on = lr.radar.radarEnabled;
                // Debug.Log("on enabled");
                lastRadarMessage.fov = lr.radar.sweepFov;
                //Debug.Log("Sending sweepFOV");

                if (Networker.isHost)
                    Networker.addToUnreliableSendBuffer(lastRadarMessage);     
                else
                    NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastRadarMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
                //Debug.Log("last 2");
                //lastOn = lr.radar.radarEnabled;
                //Debug.Log("last one");
                //lastFov = lr.radar.sweepFov;
            }
        }
        if (controller == null)
        {
                if (lr.currentLock == null)
                {
                    
                    lastLockingMessage.actorUID = 0;
                    lastLockingMessage.isLocked = false;
                    lastLockingMessage.senderUID = networkUID;
                    //Debug.Log($"Sending a locking radar message from uID {networkUID}");
                    if (Networker.isHost)
                        Networker.addToUnreliableSendBuffer(lastLockingMessage);
                    else
                        NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastLockingMessage, EP2PSend.k_EP2PSendUnreliable);
                }
                else
                {
                   
                    try
                    {
                        // ulong key = (from p in VTOLVR_Multiplayer.AIDictionaries.allActors where p.Value == lr.currentLock.actor select p.Key).FirstOrDefault();
                        if (VTOLVR_Multiplayer.AIDictionaries.reverseAllActors.TryGetValue(lr.currentLock.actor, out lastID))
                        { 
                            //Debug.Log(lastRadarLockData.actor.name + " radar data found its lock " + lr.currentLock.actor.name + " at id " + lastID + " with its own uID being " + networkUID);
                            lastLockingMessage.actorUID = lastID;
                            lastLockingMessage.isLocked = true;
                            lastLockingMessage.senderUID = networkUID;
                            if (Networker.isHost)
                            {
                                Networker.addToUnreliableSendBuffer(lastLockingMessage);
                            }
                            else
                            {
                              NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastLockingMessage, EP2PSend.k_EP2PSendUnreliable);
                            }
                        }
                        else
                        {
                            //Debug.LogError("Could not resolve lock at actor " + lastRadarLockData.actor.name);
                        }
                    }
                    catch (Exception ex)
                    {
                        //Debug.LogError("Couldn't lock target " + lr.currentLock.actor + $" exception {ex} thrown.");
                    } 
            }
        }
    }
    private void F45LockedUpdate(RadarLockData radarLockData)
    {
        try
        {
            // ulong key = (from p in VTOLVR_Multiplayer.AIDictionaries.allActors where p.Value == lr.currentLock.actor select p.Key).FirstOrDefault();
            if (VTOLVR_Multiplayer.AIDictionaries.reverseAllActors.TryGetValue(radarLockData.actor, out lastID))
            {
                //Debug.Log(gameObject.name + " F45 radar data found its lock " + radarLockData.actor.name + " at id " + lastID + " with its own uID being " + networkUID);
                lastLockingMessage.actorUID = lastID;
                lastLockingMessage.isLocked = radarLockData.locked;
                lastLockingMessage.senderUID = networkUID;
                if (Networker.isHost)
                    NetworkSenderThread.Instance.SendPacketAsHostToAllClients(lastLockingMessage, EP2PSend.k_EP2PSendReliable);
                else
                    NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastLockingMessage, EP2PSend.k_EP2PSendReliable);
            }
            else
            {
                //Debug.LogError("Could not resolve lock at actor " + radarLockData.actor.name);
            }
        }
        catch (Exception ex)
        {
            //Debug.LogError("Couldn't lock target " + radarLockData.actor.name + $" exception {ex} thrown.");
        }
    }
    private void F45UnlockedUpdate()
    {
        lastLockingMessage.actorUID = 0;
        lastLockingMessage.isLocked = false;
        lastLockingMessage.senderUID = networkUID;
        //Debug.Log($"Sending an F45 unlock radar message to uID {networkUID}");
        if (Networker.isHost)
            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(lastLockingMessage, EP2PSend.k_EP2PSendReliable);
        else
            NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastLockingMessage, EP2PSend.k_EP2PSendReliable);
    }
    private void RadarDetectedActor(Actor a)
    {
        

            if (VTOLVR_Multiplayer.AIDictionaries.reverseAllActors.TryGetValue(a, out ulong uID))
            {
                if (Networker.isHost)
                    NetworkSenderThread.Instance.SendPacketAsHostToAllClients(new Message_RadarDetectedActor(uID, networkUID), EP2PSend.k_EP2PSendReliable);
                else
                    NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, new Message_RadarDetectedActor(uID, networkUID), EP2PSend.k_EP2PSendReliable);
            }
        
    }
}

