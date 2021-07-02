﻿using Harmony;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;

public struct manObjectSorter
{
    public GameObject man;
    public double dist;
    public bool farMan;
    public static int CompareByDist(manObjectSorter a, manObjectSorter b)
    {
        return a.dist.CompareTo(b.dist);
    }

}
public class PlaneNetworker_Receiver : MonoBehaviour
{
    private ulong _networkUID;
    
    private Message_PlaneUpdate lastMessage;
    private bool firstMessageReceived;
    public static bool dontPrefixNextJettison = false;
    //Classes we use to set the information
    private AIPilot aiPilot;
    private AutoPilot autoPilot;
    private WeaponManager weaponManager;
    private CountermeasureManager cmManager;
    private FuelTank fuelTank;
    private Traverse traverse;
    private HPEquipMissileLauncher lastml;
    private int idx;
    private bool noAmmo;
    // private RadarLockData radarLockData;
    private ulong mostCurrentUpdateNumber;
    private Actor ownerActor;
    private List<int> collidersStore;


    GameObject manPuppet;

    public Transform puppetRhand;
    public Transform puppetLhand;
    public Transform puppetHead;
    public Transform puppetHeadLook;
    public Transform puppethip;
    bool manSetup = false;
    public VTOLVehicles vehicleType = VTOLVehicles.None;
    public static List<manObjectSorter> manObjects = new List<manObjectSorter>();
    manObjectSorter mos;
    public static Dictionary<ulong, List<PlaneNetworker_Receiver>> recieverDict = new Dictionary<ulong, List<PlaneNetworker_Receiver>>();
    private List<ModuleEngine> engines = new List<ModuleEngine>();
    public ulong networkUID
    {
        get
        {
            return _networkUID;
        }
        set
        {
            mostCurrentUpdateNumber = 0;
            if (recieverDict.ContainsKey(networkUID))
            {
                recieverDict[networkUID].Remove(this);
            }
            if (!recieverDict.ContainsKey(value))
            {
                List<PlaneNetworker_Receiver> newList = new List<PlaneNetworker_Receiver>();
                recieverDict.Add(value, newList);
                newList.Add(this);
            }
            else
            {
                recieverDict[value].Add(this);
            }

            this._networkUID = value;
        }
    }
    private void Awake()
    {
        firstMessageReceived = false;
        aiPilot = GetComponent<AIPilot>();

        ownerActor = GetComponentInParent<Actor>();
        autoPilot = aiPilot.autoPilot;
        
        aiPilot.enabled = false;
        aiPilot.enabled = false;
       
        Networker.WeaponSet_Result += WeaponSet_Result;
        Networker.Disconnecting += OnDisconnect;
        Networker.WeaponFiring += WeaponFiring;
        Networker.JettisonUpdate += JettisonUpdate;

        // Networker.WeaponStoppedFiring += WeaponStoppedFiring;
        Networker.FireCountermeasure += FireCountermeasure;
        if (!ownerActor.gameObject.name.Contains("verlord") && !ownerActor.gameObject.name.Contains("kc") && !ownerActor.gameObject.name.Contains("KC"))
            weaponManager = GetComponent<WeaponManager>();
        mostCurrentUpdateNumber = 0;
        if (weaponManager == null)
            Debug.LogError("Weapon Manager was null on " + gameObject.name);
        else
            traverse = Traverse.Create(weaponManager);

        if (weaponManager != null)
            foreach (var iwb in weaponManager.internalWeaponBays)
        {
            iwb.openOnAnyWeaponMatch = true;
        }
        cmManager = GetComponentInChildren<CountermeasureManager>();
        if (cmManager == null)
            Debug.LogError("CountermeasureManager was null on " + gameObject.name);
        fuelTank = GetComponent<FuelTank>();
        if (fuelTank == null)
            Debug.LogError("FuelTank was null on " + gameObject.name);

        collidersStore = new List<int>();
        //?fix gun sight jitter
        if (ownerActor != null)
        {
            ownerActor.flightInfo.PauseGCalculations();
            //FlightSceneManager.instance.playerActor.flightInfo.OverrideRecordedAcceleration(Vector3.zero);

            foreach (Rigidbody rb in ownerActor.gameObject.GetComponentsInChildren<Rigidbody>())
            {
                rb.detectCollisions = false;
            }
            foreach (Collider collider in ownerActor.gameObject.GetComponentsInChildren<Collider>())
            {
                if (collider)
                {
                    Hitbox hitbox = collider.GetComponent<Hitbox>();

                    if (hitbox != null)
                    {
                        hitbox.health.invincible = true;
                        collidersStore.Add(collider.gameObject.layer);
                        collider.gameObject.layer = 9;
                    }
                    else
                    {
                        collider.gameObject.layer = 9;
                    }
                }
            }
        }


       // if (vehicleType == VTOLVehicles.F45A)
       // {
            ModuleEngine[] engines = ownerActor.gameObject.GetComponentsInChildren<ModuleEngine>();
            foreach (ModuleEngine eng in engines)
            {
                //eng.thrustHeatMult *= 14.0f;
                engines.Add(eng);
            }
       // }
          mos = new manObjectSorter();
      
    }

    private void Start()
    {
        if (gameObject.name.Contains("Client"))
        {           
            setupManReciever();
            //Networker.IKUpdate += IKUpdate;
            //if (gameObject.name.Contains("Client"))
            Transform[] children = gameObject.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == "lowPolyInterior" || child.name.Contains("lowPolyInterior" + "(clone"))
                {
                    child.gameObject.SetActive(true);
                }else
                if (child.name == "sevtf_lowPolyInterior" || child.name.Contains("sevtf_lowPolyInterior" + "(clone"))
                    {
                        child.gameObject.SetActive(true);
                    }
              }
            foreach (var rend in GetComponentsInChildren<Renderer>())
            {
                if (rend.material.name.Contains("Glass") || rend.material.name.Contains("glass"))
                {
                    Color meshColor = rend.sharedMaterial.color;

                    meshColor *= new Color(0.8f, 0.8f, 1.0f, 1.0f);
                    meshColor.a = 0.64f;
                    Shader newShader = Shader.Find("Transparent/Diffuse");

                    rend.material.color = meshColor;
                    rend.material.shader = newShader;
                }




            }
        }
        StartCoroutine(colliderTimer());
    }

    FastIKLook ikheadlook;
    FastIKFabric iklh; FastIKFabric ikrh; FastIKFabric ikh;
    private void setupManReciever()
    {
        manPuppet = GameObject.Instantiate(CUSTOM_API.manprefab, gameObject.GetComponent<Rigidbody>().transform);
      
        mos.man = manPuppet;
        mos.farMan = false;
        manObjects.Add(mos);
        foreach (Collider col in manPuppet.GetComponentsInChildren<Collider>())
        { col.enabled = false; }


        manPuppet.transform.localScale = new Vector3(0.074f, 0.074f, 0.074f);

        manPuppet.transform.localEulerAngles = new Vector3(0.0f, 180.0f, 0.0f);


        if (vehicleType == VTOLVehicles.FA26B)
        {
            manPuppet.transform.localPosition = new Vector3(0.03f, 1.04f, 5.31f);
          
        }
        if (vehicleType == VTOLVehicles.F45A)
            manPuppet.transform.localPosition = new Vector3(-0.06f, 0.77f, 5.7f);
      

        if (vehicleType == VTOLVehicles.AV42C)
            manPuppet.transform.localPosition = new Vector3(-0.07f, 0.69f, -0.1f) - PlayerManager.av42Offset;


        Debug.Log("righthandControl");
        puppetRhand = CUSTOM_API.GetChildWithName(manPuppet, "righthandControl").transform;
        puppetRhand.GetComponent<Renderer>().enabled = false;
        Debug.Log("lefthandControl");
        puppetLhand = CUSTOM_API.GetChildWithName(manPuppet, "lefthandControl").transform;
        puppetLhand.GetComponent<Renderer>().enabled = false;
        Debug.Log("headControl");
        puppetHead = CUSTOM_API.GetChildWithName(manPuppet, "headControl").transform;
        puppetHead.GetComponent<Renderer>().enabled = false;
        Debug.Log("headLook");
        puppetHeadLook = CUSTOM_API.GetChildWithName(manPuppet, "lookControl").transform;
        puppetHeadLook.transform.position = puppetHeadLook.transform.position - new Vector3(0.0f, 0.15f, 0.0f);
        puppetHeadLook.GetComponent<Renderer>().enabled = false;
        Debug.Log("Bone.008");
        puppethip = CUSTOM_API.GetChildWithName(manPuppet, "Bone.008").transform;

        Debug.Log("headik_end");
        ikh = CUSTOM_API.GetChildWithName(manPuppet, "Bone.007").AddComponent<FastIKFabric>();
        ikh.Target = puppetHead;
        ikh.ChainLength = 4;

        Debug.Log("righthandik_end");
        ikrh = CUSTOM_API.GetChildWithName(manPuppet, "righthandik_end").AddComponent<FastIKFabric>();
        ikrh.Target = puppetRhand;
        ikrh.ChainLength = 3;

        Debug.Log("lefthandik_end");
        iklh = CUSTOM_API.GetChildWithName(manPuppet, "lefthandik_end").AddComponent<FastIKFabric>();
        iklh.Target = puppetLhand;
        iklh.ChainLength = 3;
        Debug.Log("SetupNewDisplay");


        Debug.Log("headik");
        ikheadlook = CUSTOM_API.GetChildWithName(manPuppet, "headik").AddComponent<FastIKLook>();
        ikheadlook.Target = puppetHeadLook;
        manSetup = true;

    }

    private void FixedUpdate()
    {
       
        if (manSetup)
        {
            Vector3 v = manPuppet.transform.position - FlightSceneManager.instance.playerActor.gameObject.transform.position;
            mos.dist = v.magnitude;
            if (mos.dist > 200)
            {
                manPuppet.SetActive(false);
                mos.farMan = true;
            }else
            {
                mos.farMan = false;
            }
             
        }
    }
    public static void IKUpdate(Packet packet)
    {
        
        Message_IKPuppet newMessage = (Message_IKPuppet)((PacketSingle)packet).message;
        List<PlaneNetworker_Receiver> plnl = null;
        if (!recieverDict.TryGetValue(newMessage.networkUID, out plnl))
            return;
        foreach (var pln in plnl)
        {
            if (pln.manSetup != true)
                return;
            if (newMessage.networkUID != pln.networkUID)
                return;
            pln.puppetRhand.position = pln.puppethip.transform.position + newMessage.puppetRhand.toVector3;
            pln.puppetLhand.position = pln.puppethip.transform.position + newMessage.puppetLhand.toVector3;
            pln.puppetHead.position = pln.puppethip.transform.position + newMessage.puppetHead.toVector3;
            pln.puppetHeadLook.position = pln.puppethip.transform.position + newMessage.puppetHeadLook.toVector3;
        }
    }
    public static void PlaneUpdate(Packet packet)
    {
        Message_PlaneUpdate newMessage = (Message_PlaneUpdate)((PacketSingle)packet).message;

       
       
        List<PlaneNetworker_Receiver> plnl = null;
        if (!recieverDict.TryGetValue(newMessage.networkUID, out plnl))
            return;
        foreach (var pln in plnl)
        {
            if (pln == null)
                return;
            if (newMessage.networkUID != pln.networkUID)
                return;
            pln.mostCurrentUpdateNumber = newMessage.sequenceNumber;


            if (!pln.firstMessageReceived)
            {
                pln.firstMessageReceived = true;
                pln.SetLandingGear(newMessage.landingGear);
                pln.SetTailHook(newMessage.tailHook);
                pln.SetLaunchBar(newMessage.launchBar);
                pln.SetFuelPort(newMessage.fuelPort);
                pln.SetOrientation(newMessage.pitch, newMessage.yaw, newMessage.roll);
                pln.SetFlaps(newMessage.flaps);
                pln.SetBrakes(newMessage.brakes);
                pln.SetThrottle(newMessage.throttle);
            }
            else
            {
                if (pln.lastMessage.landingGear != newMessage.landingGear)
                {
                    pln.SetLandingGear(newMessage.landingGear);
                }
                if (pln.lastMessage.tailHook != newMessage.tailHook)
                {
                    pln.SetTailHook(newMessage.tailHook);
                }
                if (pln.lastMessage.launchBar != newMessage.launchBar)
                {
                    pln.SetLaunchBar(newMessage.launchBar);
                }
                if (pln.lastMessage.fuelPort != newMessage.fuelPort)
                {
                    pln.SetFuelPort(newMessage.fuelPort);
                }
                if (pln.lastMessage.pitch != newMessage.pitch || pln.lastMessage.yaw != newMessage.yaw || pln.lastMessage.roll != newMessage.roll)
                {
                    pln.SetOrientation(newMessage.pitch, newMessage.yaw, newMessage.roll);
                }
                if (pln.lastMessage.flaps != newMessage.flaps)
                {
                    pln.SetFlaps(newMessage.flaps);
                }
                if (pln.lastMessage.brakes != newMessage.brakes)
                {
                    pln.SetBrakes(newMessage.brakes);
                }
                if (pln.lastMessage.throttle != newMessage.throttle)
                {
                    pln.SetThrottle(newMessage.throttle);
                }

            }
            pln.lastMessage = newMessage;

            if (pln.ownerActor != null)
            {
                pln.ownerActor.flightInfo.PauseGCalculations();
                pln.ownerActor.flightInfo.OverrideRecordedAcceleration(Vector3.zero);
            }
        }
    }
    private void SetLandingGear(bool state)
    {
        if (aiPilot.gearAnimator == null)
            return;
        if (state)
            aiPilot.gearAnimator.Extend();
        else
            aiPilot.gearAnimator.Retract();
    }
    private void SetTailHook(bool state)
    {
        if (aiPilot.tailHook != null)
        {
            if (state)
                aiPilot.tailHook.ExtendHook();
            else
                aiPilot.tailHook.RetractHook();
        }
    }
    private void SetLaunchBar(bool state)
    {
        if (aiPilot.catHook != null)
        {
            if (state)
                aiPilot.catHook.SetState(1);
            else
                aiPilot.catHook.SetState(0);
        }
    }
    private void SetFuelPort(bool state)
    {
        if (aiPilot.refuelPort != null)
        {
            if (state)
                aiPilot.refuelPort.Open();
            else
                aiPilot.refuelPort.Close();
        }
    }
    private void SetOrientation(float pitch, float yaw, float roll)
    {
        for (int i = 0; i < autoPilot.outputs.Length; i++)
        {
            autoPilot.outputs[i].SetPitchYawRoll(new Vector3(pitch, yaw, roll));
            autoPilot.outputs[i].SetWheelSteer(yaw);
        }
    }
    private void SetFlaps(float flaps)
    {
        for (int i = 0; i < autoPilot.outputs.Length; i++)
        {
            autoPilot.outputs[i].SetFlaps(flaps);
        }
    }
    private void SetBrakes(float brakes)
    {
        for (int i = 0; i < autoPilot.outputs.Length; i++)
        {
            autoPilot.outputs[i].SetBrakes(brakes);
        }
    }
    private void SetThrottle(float throttle)
    {
        for (int i = 0; i <  engines.Count; i++)
        {
             engines[i].autoAB=true;

             engines[i].afterburner = (throttle > autoPilot.engines[i].autoABThreshold); engines[i].SetThrottle(throttle);
            engines[i].SetFinalThrottle(throttle);
        }
    }
    public void WeaponSet_Result(Packet packet)
    {
        Message_WeaponSet_Result message = (Message_WeaponSet_Result)((PacketSingle)packet).message;
        if (message.UID != networkUID)
            return;

        if (Networker.isHost && packet.networkUID != networkUID)
        {
            //Debug.Log("Generating UIDS for any missiles the new vehicle has");
            for (int i = 0; i < message.hpLoadout.Length; i++)
            {
                for (int j = 0; j < message.hpLoadout[i].missileUIDS.Length; j++)
                {
                    if (message.hpLoadout[i].missileUIDS[j] != 0)
                    {
                        //Storing the old one
                        ulong clientsUID = message.hpLoadout[i].missileUIDS[j];
                        //Generating a new global UID for that missile
                        message.hpLoadout[i].missileUIDS[j] = Networker.GenerateNetworkUID();
                        //Sending it back to that client
                        NetworkSenderThread.Instance.SendPacketToSpecificPlayer(PlayerManager.GetPlayerCSteamID(message.UID),
                            new Message_RequestNetworkUID(clientsUID, message.hpLoadout[i].missileUIDS[j]),
                            EP2PSend.k_EP2PSendReliable);
                    }
                }
            }
        }

        PlaneEquippableManager.SetLoadout(gameObject, networkUID, message.normalizedFuel, message.hpLoadout, message.cmLoadout);
        noAmmo = false;
        if (Networker.isHost)
        {
            NetworkSenderThread.Instance.SendPacketAsHostToAllButOneSpecificClient(PlayerManager.GetPlayerCSteamID(message.UID),
                message,
                Steamworks.EP2PSend.k_EP2PSendReliable);
        }
    }

    private void JettisonUpdate(Packet packet)
    {
        Message_JettisonUpdate message = ((PacketSingle)packet).message as Message_JettisonUpdate;
        if (message.networkUID != networkUID)
            return;
        if (message.toJettison == null)
        {
            Debug.LogError("Why did we get a jettison message that want's to jettison nothing?");
            return;
        }
        foreach (var idx in message.toJettison)
        {
            HPEquippable equip = weaponManager.GetEquip(idx);
            if (equip != null)
                equip.markedForJettison = true;
        }
        dontPrefixNextJettison = true;
        weaponManager.JettisonMarkedItems();
    }
    public void WeaponFiring(Packet packet)
    {
        Message_WeaponFiring message = ((PacketSingle)packet).message as Message_WeaponFiring;
        if (message.UID != networkUID)
            return;

        if (weaponManager.isMasterArmed == false)
        {
            weaponManager.ToggleMasterArmed();
        }
        idx = (int)traverse.Field("weaponIdx").GetValue();
        /*while (message.weaponIdx != idx && i < 60)
        {
            if (weaponManager.isMasterArmed == false)
            {
                weaponManager.ToggleMasterArmed();
            }
            // Debug.Log(idx + " " + message.weaponIdx);
            i++;
        }
        if (i > 59)
        {
            Debug.Log("couldn't change weapon idx to right weapon for aircraft " + gameObject.name);
        }*/
        weaponManager.SetWeapon(message.weaponIdx);
        idx = (int)traverse.Field("weaponIdx").GetValue();
        if (idx != message.weaponIdx)
        {
            Debug.LogWarning("Couldn't change weapon idx to the right weapon for aircraft " + gameObject.name);
        }

        if (weaponManager.currentEquip is RocketLauncher)
        {
            RocketLauncher rl = (RocketLauncher)weaponManager.currentEquip;
           rl.SetRippleRateIdx(message.ripple);
        }

       
        if (message.isFiring != weaponManager.isFiring)
        {
            if (message.isFiring)
            {

                /*if (weaponManager.currentEquip is HPEquipMissileLauncher)
                {
                    //lastml = weaponManager.currentEquip as HPEquipMissileLauncher;
                    //Traverse.Create(lastml.ml).Field("missileIdx").SetValue(message.missileIdx);
                    //Debug.Log("Single firing this missile " + weaponManager.currentEquip.shortName);
                    //weaponManager.SingleFire();
                }
                else*/
                if (weaponManager.currentEquip is RocketLauncher)
                {
                    weaponManager.StartFire();
                }
                else
                {
                    //Debug.Log("try start fire for vehicle" + gameObject.name + " on current equip " + weaponManager.currentEquip);
                    if (message.noAmmo)
                    {
                        if (weaponManager.currentEquip is HPEquipGun)
                        {
                            ((HPEquipGun)weaponManager.currentEquip).gun.currentAmmo = 0;
                            noAmmo = true;
                        }
                        else if (weaponManager.currentEquip is HPEquipGunTurret)
                        {
                            ((HPEquipGunTurret)weaponManager.currentEquip).gun.currentAmmo = 0;
                            noAmmo = true;
                        }
                    }
                    else
                    {
                        if (weaponManager.currentEquip is HPEquipGun)
                        {
                            ((HPEquipGun)weaponManager.currentEquip).gun.currentAmmo = ((HPEquipGun)weaponManager.currentEquip).gun.maxAmmo;
                            noAmmo = false;
                        }
                        else if (weaponManager.currentEquip is HPEquipGunTurret)
                        {
                            ((HPEquipGun)weaponManager.currentEquip).gun.currentAmmo = ((HPEquipGun)weaponManager.currentEquip).gun.maxAmmo;
                            noAmmo = false;
                        }
                    }
                    if (!noAmmo && !(weaponManager.currentEquip is HPEquipMissileLauncher))
                        weaponManager.StartFire();
                }
            }
            else
            {
                if (!(weaponManager.currentEquip is HPEquipMissileLauncher || weaponManager.currentEquip is RocketLauncher))
                    weaponManager.EndFire();
            }
        }
    }
    public void FireCountermeasure(Packet packet) // chez
    {
        Message_FireCountermeasure message = ((PacketSingle)packet).message as Message_FireCountermeasure;
        if (message.UID != networkUID)
            return;
        aiPilot.aiSpawn.CountermeasureProgram(message.flares, message.chaff, 2, 0.1f);
    }

    public HPInfo[] GenerateHPInfo()
    {
        if (!Networker.isHost)
        {
            Debug.LogError("Generate HPInfo was ran from a player which isn't the host.");
            return null;
        }

        return PlaneEquippableManager.generateHpInfoListFromWeaponManager(weaponManager,
            PlaneEquippableManager.HPInfoListGenerateNetworkType.receiver).ToArray();
    }
    public int[] GetCMS()
    {
        //There is only ever 2 counter measures, thats why it's hard coded.
        return PlaneEquippableManager.generateCounterMeasuresFromCmManager(cmManager).ToArray();
    }
    public float GetFuel()
    {
        return fuelTank.fuel;
    }
    public void OnDisconnect(Packet packet)
    {
        Message_Disconnecting message = ((PacketSingle)packet).message as Message_Disconnecting;
        if (message.UID != networkUID)
            return;

        firstMessageReceived = false;
        Destroy(gameObject);
    }
    public void OnDestroy()
    {
        firstMessageReceived = false;
        if (recieverDict.ContainsKey(_networkUID))
        {
            recieverDict[networkUID].Remove(this);
        }
        Networker.Disconnecting -= OnDisconnect;
        Networker.WeaponSet_Result -= WeaponSet_Result;
        Networker.WeaponFiring -= WeaponFiring;
        
        if (manSetup == true)
        {
            manObjects.Remove(mos);
          
        }
            
        // Networker.WeaponStoppedFiring -= WeaponStoppedFiring;
        Networker.FireCountermeasure -= FireCountermeasure;
        Debug.Log("Destroyed Plane Update");
        Debug.Log(gameObject.name);
    }


    private System.Collections.IEnumerator colliderTimer()
    {
        yield return new WaitForSeconds(5.5f);

        if (ownerActor != null)
        {
            foreach (Rigidbody rb in ownerActor.gameObject.GetComponentsInChildren<Rigidbody>())
            {
                rb.detectCollisions = true;
            }
            int i = 0;
            foreach (Collider collider in ownerActor.gameObject.GetComponentsInChildren<Collider>())
            {
                if (collider)
                {
                    Hitbox hitbox = collider.GetComponent<Hitbox>();

                    if (hitbox != null)
                    {
                        hitbox.health.invincible = true;
                        collider.gameObject.layer = collidersStore[i];
                        i += 1;
                    }
                    else
                    {
                        collider.gameObject.layer = 9;
                    }
                }
            }
        }

    }

}

[HarmonyPatch(typeof(AutoPilot))]
[HarmonyPatch("UpdateAutopilot")]
public class Patch0
{
    public static bool Prefix(AutoPilot __instance, float deltaTime)
    {
        bool result = !__instance.gameObject.name.Contains("Client [");
        return result;
    }
}
