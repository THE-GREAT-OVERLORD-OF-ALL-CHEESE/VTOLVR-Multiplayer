﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class PlaneNetworker_Sender : MonoBehaviour
{
    public ulong networkUID;

    //Classes we use to find the information out
    private WheelsController wheelsController;
    private AeroController aeroController;
    private VRThrottle vRThrottle;

    private Message_PlaneUpdate lastMessage;

    private void Awake()
    {
        
        lastMessage = new Message_PlaneUpdate(false, 0, 0, 0, 0, 0, 0, false, false, networkUID);

        wheelsController = GetComponent<WheelsController>();
        aeroController = GetComponent<AeroController>();
        vRThrottle = gameObject.GetComponentInChildren<VRThrottle>();
        if (vRThrottle == null)
            Debug.Log("Cound't find throttle");
        else
            vRThrottle.OnSetThrottle.AddListener(SetThrottle);

        Debug.Log("Done Plane Sender");
    }

    private void LateUpdate()
    {
        lastMessage.flaps = aeroController.flaps;
        lastMessage.pitch = Mathf.Round(aeroController.input.x * 100000f) / 100000f;
        lastMessage.yaw = Mathf.Round(aeroController.input.y * 100000f) / 100000f;
        lastMessage.roll = Mathf.Round(aeroController.input.z * 100000f) / 100000f;
        lastMessage.breaks = aeroController.brake;
        lastMessage.landingGear = LandingGearState();
        lastMessage.networkUID = networkUID;

        if (Networker.isHost)
            Networker.SendGlobalP2P(lastMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
        else
            Networker.SendP2P(Networker.hostID, lastMessage, Steamworks.EP2PSend.k_EP2PSendUnreliable);
    }

    private bool LandingGearState()
    {
        return wheelsController.gearAnimator.GetCurrentState() == GearAnimator.GearStates.Extended;
    }

    public void SetThrottle(float t)
    {
        lastMessage.throttle = t;
    }
}