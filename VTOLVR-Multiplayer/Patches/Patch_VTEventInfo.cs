﻿using Harmony;
using System;
using System.Collections.Generic;
using UnityEngine;



    [HarmonyPatch(typeof(CameraFogSettings), "OnPreRender")]
class PatchFog
{
    static void Postfix(CameraFogSettings __instance)
    {
        if(Multiplayer._instance.fog  !=0.0f)
            RenderSettings.fogDensity = PlayerManager.DefaultFog + (Multiplayer._instance.fog * 0.000045f);

    }
}


// patch to grab all the events being loaded on creation this replaces original method
[HarmonyPatch(typeof(Bullet), "KillBullet")]
class PatchBullet
{
    static bool Prefix(Bullet __instance)
    {

        Vector3 pos = Traverse.Create(__instance).Field("hitPoint").GetValue<Vector3>();
        Vector3 vel = Traverse.Create(__instance).Field("velocity").GetValue<Vector3>();
        Vector3 a = pos;
        a += -vel * Time.deltaTime;
        float damage = Traverse.Create(__instance).Field("damage").GetValue<float>();
        Actor sourceActor = Traverse.Create(__instance).Field("sourceActor").GetValue<Actor>();
        Hitbox hitbox = null;



        Collider[] ColliderHits;
        ColliderHits = Physics.OverlapSphere(pos, 5.0f, 1025);
        for (int i = 0; i < ColliderHits.Length; i++)
        {

            Collider coll = ColliderHits[i];
            hitbox = coll.GetComponent<Hitbox>();
            if (hitbox && hitbox.actor)
            {
                PlayerManager.lastBulletHit = hitbox;
                DebugCustom.Log("hit box bullet hit");
                ulong lastID; ulong sourceID=0;
                if (VTOLVR_Multiplayer.AIDictionaries.reverseAllActors.TryGetValue(hitbox.actor, out lastID))
                {

                    if(sourceActor!=null)
                    if (VTOLVR_Multiplayer.AIDictionaries.reverseAllActors.TryGetValue(sourceActor, out sourceID))
                    {

                    }

                    DebugCustom.Log("hit player sending bullet packet");
                    Message_BulletHit hitmsg = new Message_BulletHit(PlayerManager.localUID, lastID, sourceID, VTMapManager.WorldToGlobalPoint(pos), new Vector3D(vel), damage);
                    if (Networker.isHost)
                        NetworkSenderThread.Instance.SendPacketAsHostToAllClients(hitmsg, Steamworks.EP2PSend.k_EP2PSendReliable);
                    else
                        NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, hitmsg, Steamworks.EP2PSend.k_EP2PSendReliable);
                }

            }
        }

        return true;
    }
}


[HarmonyPatch(typeof(GPSTargetSystem), "AddTarget")]
class PatchGPS
{
    static bool Prefix(GPSTargetSystem __instance, Vector3 worldPosition, string prefix)
    {

        Vector3 pos = worldPosition;
        string msgp = prefix;
        DebugCustom.Log("sending GPS");


        Message_GPSData gpsm = new Message_GPSData(PlayerManager.localUID, VTMapManager.WorldToGlobalPoint(pos), "MP", PlayerManager.teamLeftie, __instance.currentGroup.groupName);

        if (PlayerManager.sendGPS)
        {
            if (Networker.isHost)
            {
                NetworkSenderThread.Instance.SendPacketAsHostToAllClients(gpsm, Steamworks.EP2PSend.k_EP2PSendReliable);
            }
            else
            {
                NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, gpsm, Steamworks.EP2PSend.k_EP2PSendReliable);
            }
        }
        return true;
    }
}


[HarmonyPatch(typeof(ShipSurviveObjective), "OnDeath")]
class deathmenu
{
    static bool Prefix()
    {
        return false;
    }
}

[HarmonyPatch(typeof(ShipSurviveObjective), "OnDeathDelayed")]
class deathmenu2
{
    static bool Prefix()
    {
        return false;
    }
}
[HarmonyPatch(typeof(ProtectObjective), "Update")]
class PatchPROTECC
{
    static bool Prefix()
    {


        if (Networker.isHost)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}


[HarmonyPatch(typeof(MFDCommsPage), "RequestRearming")]

class Patchmissions
{
    static bool Prefix()
    {

        if (PlayerManager.selectedVehicle == "FA-26B")
            PlayerManager.selectedVehicle = "F/A-26B";
        PilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle(PlayerManager.selectedVehicle);
        string campID;
        if (PlayerManager.selectedVehicle == "AV-42C")
        {
            campID = "av42cQuickFlight";
        }
        else if (PlayerManager.selectedVehicle == "F/A-26B")
        {
            campID = "fa26bFreeFlight";
        }
        else
        {
            campID = "f45-quickFlight";
        }
        PilotSaveManager.current.lastVehicleUsed = PilotSaveManager.currentVehicle.name;
        Campaign campref = VTResources.GetBuiltInCampaign(campID).ToIngameCampaign();
        PilotSaveManager.currentCampaign = campref;
         
        return true;
    }
}
[HarmonyPatch(typeof(EndMission), "CompleteMission")]

class Patchmission
{
    static bool Prefix()
    {

        PilotSaveManager.currentCampaign = Networker._instance.pilotSaveManagerControllerCampaign;
        PilotSaveManager.currentScenario = Networker._instance.pilotSaveManagerControllerCampaignScenario;
        return true;
    }
}

[HarmonyPatch(typeof(VTEventTarget), "Invoke")]
class Patch22
{
    static bool Prefix(VTEventTarget __instance)
    {


        if (Networker.isHost)
        {
            return true;
        }
        else
        {

           
            {
                bool shouldComplete = ObjectiveNetworker_Reciever.completeNextEvent;
                DebugCustom.Log($"Should complete is {shouldComplete}.");
                ObjectiveNetworker_Reciever.completeNextEvent = false;
                return shouldComplete;// clients should not send kill obj packets or have them complete

            }

        }
        return false;
    }
}
[HarmonyPatch(typeof(VTEventTarget), "Invoke")]
class Patch2
{
    static void Postfix(VTEventTarget __instance)
    {
        
        if (!__instance.TargetExists())
        {
            DebugCustom.Log("Target doesn't exist in invoke");
        }

   
            if (Networker.isHost)
        {
            //FlightLogger.Log("Host sent Event action" + __instance.eventName + " of type " + __instance.methodName + " for target " + __instance.targetID);
            if (ObjectiveNetworker_Reciever.reverseScenarioActionsList.ContainsKey(__instance))
            {
                int hash = 0;

                hash = ObjectiveNetworker_Reciever.reverseScenarioActionsList[__instance];
                Message_ScenarioAction ScanarioActionOutMessage = new Message_ScenarioAction(PlayerManager.localUID, hash);
                NetworkSenderThread.Instance.SendPacketAsHostToAllClients(ScanarioActionOutMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
            }

        }
        else
        {
            DebugCustom.Log("Client sent Event action" + __instance.eventName + " of type " + __instance.methodName + " for target " + __instance.targetID);
            // NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, ScanarioActionOutMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        }
    }
}



// patch to grab all the events being loaded on creation this replaces original method
[HarmonyPatch(typeof(Missile), "CheckMiss")]
class Patch33
{
    static bool Prefix(Missile __instance)
    {
        if(MissileNetworker_Receiver.radarMissiles!=null)
        if (MissileNetworker_Receiver.radarMissiles.Contains(__instance.actor))
            return false;
        else
            return true;
        else
            return true;
    }
}
        // patch to grab all the events being loaded on creation this replaces original method
        [HarmonyPatch(typeof(VTEventInfo), "LoadFromInfoNode")]
class Patch3
{
    static bool Prefix(VTEventInfo __instance, ConfigNode infoNode)
    {

        DebugCustom.Log("bahacode scenario dictionary");
        __instance.eventName = infoNode.GetValue("eventName");
        __instance.actions = new List<VTEventTarget>();
        foreach (ConfigNode node in infoNode.GetNodes("EventTarget"))
        {
            VTEventTarget vTEventTarget = new VTEventTarget();
            vTEventTarget.LoadFromNode(node);
            __instance.actions.Add(vTEventTarget);
            DebugCustom.Log("Compiling scenario dictonary my codd2");
            String actionIdentifier = __instance.eventName + vTEventTarget.eventName + vTEventTarget.methodName + vTEventTarget.targetID + vTEventTarget.targetType.ToString()+ ObjectiveNetworker_Reciever.actionCounter.ToString();

            ObjectiveNetworker_Reciever.actionCounter += 1;
            DebugCustom.Log(actionIdentifier);
            int hash = actionIdentifier.GetHashCode();
            DebugCustom.Log("Compiling scenario dictonary adding to my dictionary");

            if (!ObjectiveNetworker_Reciever.scenarioActionsList.ContainsKey(hash))
                ObjectiveNetworker_Reciever.scenarioActionsList.Add(hash, vTEventTarget);

            if (!ObjectiveNetworker_Reciever.reverseScenarioActionsList.ContainsKey(vTEventTarget))
                ObjectiveNetworker_Reciever.reverseScenarioActionsList.Add(vTEventTarget, hash);


        }
        return false;//dont run bahas code
    }
}


        //patch to grab all the events being loaded on creation this replaces original method
        [HarmonyPatch(typeof(MissionObjective), "CompleteObjective")]
class Patch4
{
    static bool Prefix(MissionObjective __instance)
    {

        DebugCustom.Log("A mission got completed we need to send it");

        int hashCode = ObjectiveNetworker_Reciever.getMissionHash(__instance);

        Message_ObjectiveSync objOutMessage = new Message_ObjectiveSync(PlayerManager.localUID, hashCode, ObjSyncType.EMissionCompleted);
        if (Networker.isHost)
        {
            DebugCustom.Log("Host sent objective complete " + __instance.objectiveID);
            ObjectiveNetworker_Reciever.completeNext = false;
            ObjectiveNetworker_Reciever.ObjectiveHistory.Add(objOutMessage);
            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(objOutMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
        }
        else
        {
            bool shouldComplete = ObjectiveNetworker_Reciever.completeNext;
            if (VTScenario.current.objectives.GetObjective(__instance.objectiveID).objectiveType == VTObjective.ObjectiveTypes.Fly_To ||
                VTScenario.current.objectives.GetObjective(__instance.objectiveID).objectiveType == VTObjective.ObjectiveTypes.Refuel ||
                VTScenario.current.objectives.GetObjective(__instance.objectiveID).objectiveType == VTObjective.ObjectiveTypes.Join ||
                VTScenario.current.objectives.GetObjective(__instance.objectiveID).objectiveType == VTObjective.ObjectiveTypes.Land)
            {
                if (shouldComplete == false)
                {
                    //we havent been told to do this by host send it.
                    NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, objOutMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
                }
                return true;
            }
            else
            {
                //    VTScenario.current.objectives.GetObjective(__instance.objectiveID).objectiveType == VTObjective.ObjectiveTypes.Conditional)


                DebugCustom.Log($"Should complete is {shouldComplete}.");
                ObjectiveNetworker_Reciever.completeNext = false;
                return shouldComplete;// clients should not send kill obj packets or have them complete
            }
            //NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, objOutMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        }
        return true;
    }
}
/*
[HarmonyPatch(typeof(Actor), "DetectActor")]
class Patchact
{
    static bool Prefix(Actor __instance, Teams detectedBy)
    {
        if (detectedBy == Teams.Enemy)
            return true;
        if (PlayerManager.networkedDetection)
            return true;
        if (VTOLVR_Multiplayer.AIDictionaries.reverseAllActors.TryGetValue(__instance, out ulong uID))
        {
            if (Networker.isHost)
                NetworkSenderThread.Instance.SendPacketAsHostToAllClients(new Message_DiscoveredActor(uID, PlayerManager.localUID, PlayerManager.teamLeftie), Steamworks.EP2PSend.k_EP2PSendReliable);
            else
                NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, new Message_DiscoveredActor(uID, PlayerManager.localUID, PlayerManager.teamLeftie), Steamworks.EP2PSend.k_EP2PSendReliable);
        }
        return true;
    }
}*/
//patch to grab all the events being loaded on creation this replaces original method
[HarmonyPatch(typeof(MissionObjective), "FailObjective")]
class Patch5
{
    static bool Prefix(MissionObjective __instance)
    {
        DebugCustom.Log("A mission got failed we need to send it");

        int hashCode = ObjectiveNetworker_Reciever.getMissionHash(__instance);

        Message_ObjectiveSync objOutMessage = new Message_ObjectiveSync(PlayerManager.localUID, hashCode, ObjSyncType.EMissionFailed);
        if (Networker.isHost)
        {
            DebugCustom.Log("Host sent objective fail " + __instance.objectiveID);
            ObjectiveNetworker_Reciever.ObjectiveHistory.Add(objOutMessage);
            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(objOutMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
        }
        else
        {

            bool shouldComplete = ObjectiveNetworker_Reciever.completeNextFailed;
            DebugCustom.Log($"Should complete is {shouldComplete}.");
            ObjectiveNetworker_Reciever.completeNextFailed = false;
            return shouldComplete;// clients should not send kill obj packets or have them complete
            //NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, objOutMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        }
        return true;
    }
}

//patch to grab all the events being loaded on creation this replaces original method
[HarmonyPatch(typeof(VTObjective), "BeginObjective")]
class Patch6
{
    static void Postfix(VTObjective __instance)
    {
        DebugCustom.Log("A VTObjective got begin we need to send it");

        Message_ObjectiveSync objOutMessage = new Message_ObjectiveSync(PlayerManager.localUID, __instance.objectiveID, ObjSyncType.EVTBegin);
        if (Networker.isHost)
        {
            DebugCustom.Log("Host sent VTObjective begin " + __instance.objectiveID);
            ObjectiveNetworker_Reciever.ObjectiveHistory.Add(objOutMessage);
            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(objOutMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
        }
        else
        {

            bool shouldComplete = ObjectiveNetworker_Reciever.completeNextBegin;
            DebugCustom.Log($"Should complete is {shouldComplete}.");
            ObjectiveNetworker_Reciever.completeNextBegin = false;
            //return shouldComplete;// clients should not send kill obj packets or have them complete
            //NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, objOutMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        }
        // return true;
    }
}


//patch to grab all the events being loaded on creation this replaces original method
[HarmonyPatch(typeof(MissionObjective), "CancelObjective")]
class Patch7
{
    static bool Prefix(MissionObjective __instance)
    {

        DebugCustom.Log("A mission got CancelObjective we need to send it");

        int hashCode = ObjectiveNetworker_Reciever.getMissionHash(__instance);

        Message_ObjectiveSync objOutMessage = new Message_ObjectiveSync(PlayerManager.localUID, hashCode, ObjSyncType.EMissionCanceled);
        if (Networker.isHost && objOutMessage.objID != -1)
        {

            DebugCustom.Log("Host sent objective CancelObjective " + __instance.objectiveID);
            ObjectiveNetworker_Reciever.ObjectiveHistory.Add(objOutMessage);
            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(objOutMessage, Steamworks.EP2PSend.k_EP2PSendReliable);
        }
        else
        {
            bool shouldComplete = ObjectiveNetworker_Reciever.completeNextCancel;
            DebugCustom.Log($"Should complete is {shouldComplete}.");
            ObjectiveNetworker_Reciever.completeNextCancel = false;
            return shouldComplete;// clients should not send kill obj packets or have them complete
            //Debug.Log("Client sent objective CancelObjective " + __instance.objectiveID);
            // NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, objOutMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        }
        return true;
    }
}
