﻿using Harmony;
using System.Collections.Generic;
using UnityEngine;

using VTOLVR_Multiplayer;
public class MissileNetworker_Receiver : MonoBehaviour
{
    public ulong networkUID;
    public ulong ownerNetworkUID;
    public Missile thisMissile;
    public MissileLauncher thisML;
    public int idx;
    private Message_MissileUpdate lastMessage;
    private Traverse traverseML;
    private Traverse traverseMSL;
    private RadarLockData lockData;
    // private Rigidbody rigidbody; see missileSender for why i not using rigidbody
    private bool hasFired = false;
    private bool exploded = false;
    private List<int> colliderLayers = new List<int>();
    bool started = false;
    public static List<Actor> radarMissiles = new List<Actor>();
    private void Awake()
    {
        if (thisMissile == null)
        {
            thisMissile = GetComponent<Missile>();
        }
        started = true;
        // rigidbody = GetComponent<Rigidbody>();
        Networker.MissileUpdate += MissileUpdate;
        thisMissile.OnDetonate.AddListener(new UnityEngine.Events.UnityAction(() => { Debug.Log("Missile detonated: " + thisMissile.name); }));
        //if (thisMissile.guidanceMode == Missile.GuidanceModes. || thisMissile.guidanceMode == Missile.GuidanceModes.Optical || thisMissile.guidanceMode == Missile.GuidanceModes.GPS)
        {
            foreach (var collider in thisMissile.GetComponentsInChildren<Collider>())
            {
                colliderLayers.Add(collider.gameObject.layer);
                collider.gameObject.layer = 9;
            }
        }

        thisMissile.explodeRadius *= 2.48f;
        traverseML = Traverse.Create(thisML);
        traverseMSL = Traverse.Create(thisMissile);
        traverseMSL.Field("detonated").SetValue(true);
    }

    public void MissileUpdate(Packet packet)
    {
        lastMessage = ((PacketSingle)packet).message as Message_MissileUpdate;
       
        if (lastMessage.networkUID != networkUID)
        {
            return;
        }
        if (!thisMissile.gameObject.activeSelf)
        {
            Debug.LogError(thisMissile.gameObject.name + " isn't active in hiearchy, changing it to active.");
            thisMissile.gameObject.SetActive(true);
        }
        if (started == false)
        {
            return;
        }
        if (traverseML == null)
        {
            traverseML = Traverse.Create(thisML);
        } 
        
        if (!thisMissile.fired)
        {
            Debug.Log(thisMissile.gameObject.name + " missile fired on one end but not another, firing here.");
            if (thisML == null)
            {
                Debug.LogError($"Missile launcher is null on missile {thisMissile.actor.name}, someone forgot to assign it.");
            }
            if (lastMessage.guidanceMode == Missile.GuidanceModes.Radar)
            {
                // thisMissile.debugMissile = true;
                RadarMissileLauncher radarLauncher = thisML as RadarMissileLauncher;
                if (!VTOLVR_Multiplayer.AIDictionaries.allActors.TryGetValue(lastMessage.radarLock, out Actor actor))
                {
                    Debug.LogWarning($"Could not resolve missile launcher radar lock from uID {lastMessage.radarLock}.");
                }
                else
                {
                    if (radarLauncher.lockingRadar != null)
                        radarLauncher.lockingRadar.ForceLock(actor, out lockData);
                    else
                        Debug.LogWarning("Locking Radar null on object " + thisMissile.name);
                }
                if (radarLauncher != null)
                {
                    radarMissiles.Add(thisMissile.actor);
                    Debug.Log("Guidance mode radar, firing it as a radar missile.");
                    traverseML.Field("missileIdx").SetValue(idx);
                    if (!radarLauncher.TryFireMissile())
                    {
                        Debug.LogError($"Could not fire radar missile, lock data is as follows: Locked: {lockData.locked}, Actor: {lockData.actor}");
                    }
                    else
                    {
                        RigidbodyNetworker_Receiver rbReceiver = gameObject.AddComponent<RigidbodyNetworker_Receiver>();
                        rbReceiver.networkUID = networkUID;
                        foreach(PlayerManager.Player p in PlayerManager.players)
                        {
                            if(p.cSteamID.m_SteamID == ownerNetworkUID)
                            {
                                rbReceiver.playerWeRepresent = p;
                            }
                        }
                       
                        rbReceiver.smoothingTime =0.5f;
                    }
                }
            }
            else
            {
                if (lastMessage.guidanceMode == Missile.GuidanceModes.Heat)
                {
                    Debug.Log("Guidance mode Heat.");
                    thisMissile.heatSeeker.transform.rotation = lastMessage.seekerRotation;
                    thisMissile.heatSeeker.SetHardLock();
                }

                if (lastMessage.guidanceMode == Missile.GuidanceModes.Optical)
                {
                    Debug.Log("Guidance mode Optical.");

                    GameObject emptyGO = new GameObject();
                    Transform newTransform = emptyGO.transform;

                    newTransform.position = VTMapManager.GlobalToWorldPoint(lastMessage.targetPosition);
                    thisMissile.SetOpticalTarget(newTransform);
                    //thisMissile.heatSeeker.SetHardLock();

                    if (thisMissile.opticalLOAL)
                    {
                        thisMissile.SetLOALInitialTarget(VTMapManager.GlobalToWorldPoint(lastMessage.targetPosition));

                    }
                }
                // Debug.Log("Try fire missile clientside");
                traverseML.Field("missileIdx").SetValue(idx);
                thisML.FireMissile();
                RigidbodyNetworker_Receiver rbReceiver = gameObject.AddComponent<RigidbodyNetworker_Receiver>();
                rbReceiver.networkUID = networkUID;
                rbReceiver.smoothingTime = 0.5f;
            }
            if (hasFired != thisMissile.fired)
            {
                Debug.Log("Missile fired " + thisMissile.name);
                hasFired = true;

                AIDictionaries.allActors.Add(networkUID, thisMissile.actor);
                AIDictionaries.reverseAllActors.Add(thisMissile.actor, networkUID);
                if (colliderLayers.Count > 0)
                    StartCoroutine(colliderTimer());
            }
        }

        //explode missle after it has done its RB physics fixed timestep
        if (!exploded)
            if (lastMessage.hasExploded)
        {

            Debug.Log("Missile exploded.");
            if (thisMissile != null)
            {
                traverseMSL.Field("detonated").SetValue(false);

                ///thisMissile.rb.velocity = thisMissile.transform.forward * 10.0f;
                thisMissile.Detonate();
            }


        }
    }
    private void LateUpdate()
    {
        if(hasFired)
        if (thisMissile != null)
        {
            if (!exploded)
            {
                traverseMSL.Field("detonated").SetValue(true);
                traverseMSL.Field("radarLostTime").SetValue(0.0f);
                traverseMSL.Field("finalTorque").SetValue(new Vector3(0.0f, 0.0f, 0.0f));
            }
        }

    }


    private System.Collections.IEnumerator colliderTimer()
    {
        yield return new WaitForSeconds(0.75f);
        int count = 0;
        foreach (var collider in thisMissile.GetComponentsInChildren<Collider>())
        {
            collider.gameObject.layer = colliderLayers[count];
            count++;
        }

    }

    public void OnDestroy()
    {

        radarMissiles.Remove(thisMissile.actor);
        Networker.MissileUpdate -= MissileUpdate;
    }
}

/* Possiable Issue
 * 
 * A missile on a client may explode early because of the random chance it loses locking bceause of 
 * counter measures. 
 * 
 * Missile.cs Line 633
 * This bool function could return false on a clients game and true on the owners game.
 * No error should occour on either game however the missile would now be out of sync, causing bigger issues.
 * 
 * The reason I have't tried fixing this issue is because it's right in the middle of UpdateTargetData() so
 * it would require a lot of rewriting game code just to add a network check for it.
 * 
 * As of writing this note CMS are not networked so it wouldn't effect it, but later on it will.
 * . Marsh.Mello . 21/02/2020 
 * Temperz87 says you suck.
 */
