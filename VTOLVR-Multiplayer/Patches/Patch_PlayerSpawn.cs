﻿using Harmony;
using System.Collections.Generic;
using UnityEngine;

[HarmonyPatch(typeof(PlayerSpawn), "OnPreSpawnUnit")]
class Patch_OnPreSpawnUnit
{
    public static bool Prefix(PlayerSpawn __instance)
    {
       
        if (PlayerManager.selectedVehicle == "")
        {
            DebugCustom.LogError("selected vehicle is empty");
        }
        if (PlayerManager.selectedVehicle == "FA-26B")
            PlayerManager.selectedVehicle = "F/A-26B";
        VTScenario.current.vehicle = VTResources.GetPlayerVehicle(PlayerManager.selectedVehicle);
        PilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle(PlayerManager.selectedVehicle);
        VTCampaignInfo[] list = VTResources.GetBuiltInCampaigns().ToArray();
        string campID = " ";
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
        Campaign campref = VTResources.GetBuiltInCampaign(campID).ToIngameCampaign();
        PilotSaveManager.currentCampaign = campref;
        Multiplayer._instance.buttonMade = false;
        return true;
    }
}
[HarmonyPatch(typeof(LoadoutConfigurator), "EquipCompatibilityMask")]
public static class Patch_DrawButton
{
    public static bool Prefix(HPEquippable equip)
    {
        if (VTOLAPI.currentScene == VTOLScenes.VehicleConfiguration)
            if (!Multiplayer._instance.buttonMade)
            {
                Multiplayer.CreateVehicleButton();
               //Multiplayer.CreateCustomPlaneButton();
            }
        return true;
    }
}
[HarmonyPatch(typeof(VehicleConfigSceneSetup), "LaunchMission")]
public static class Patch_LaunchMIssion
{
    public static bool Prefix()
    {
        PilotSaveManager.currentCampaign = Networker._instance.pilotSaveManagerControllerCampaign;
        return true;
    }
}
 

