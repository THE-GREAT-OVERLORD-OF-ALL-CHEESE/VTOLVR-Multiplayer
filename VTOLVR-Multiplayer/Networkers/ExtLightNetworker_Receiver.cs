﻿using UnityEngine;

class ExtLight_Receiver : MonoBehaviour
{
    public ulong networkUID;
    private Message_ExtLight lastMessage;
    public ExteriorLightsController lightsController;

    private void Awake()
    {
        lastMessage = new Message_ExtLight(false, false, false, networkUID);
        Networker.ExtLight += ChangeLights;
    }

    public void ChangeLights(Packet packet)
    {
        lastMessage = (Message_ExtLight)((PacketSingle)packet).message;
        if (lastMessage.UID != networkUID)
            return;

        DebugCustom.Log("The lights on " + networkUID + " have changed.");
        if (lastMessage.nav)
        {
            lightsController.SetNavLights(1);
        }
        else
        {
            lightsController.SetNavLights(0);
        }
        if (lastMessage.strobe)
        {
            lightsController.SetStrobeLights(1);
        }
        else
        {
            lightsController.SetStrobeLights(0);
        }
        if (lastMessage.land)
        {
            lightsController.SetLandingLights(1);
        }
        else
        {
            lightsController.SetLandingLights(0);
        }
    }

    public void OnDestroy()
    {
        Networker.ExtLight -= ChangeLights;
        DebugCustom.Log("Destroyed ExtLight");
        DebugCustom.Log(gameObject.name);
    }
}
