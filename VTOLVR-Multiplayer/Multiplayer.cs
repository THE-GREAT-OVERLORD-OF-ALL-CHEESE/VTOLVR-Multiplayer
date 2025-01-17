﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Discord;
using Harmony;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Multiplayer : VTOLMOD
{
    public struct VTOLLobby
    {
        public CSteamID hostID;
        public String hostName;
        public String campID;
        public String scenID;
        public String plane;
            public String scnName;
    };
    public static bool SoloTesting = true;
    public static Multiplayer _instance = null;
    public bool UpToDate = true;
    public bool buttonMade = false;
    private bool checkedToDate = false;
    private static string TesterURL = "http://marsh.vtolvr-mods.com/?id=";
    public static GameObject canvasButtonPrefab = null;
    private struct FriendItem
    {
        public CSteamID steamID;
        public Transform transform;

        public FriendItem(CSteamID steamID, Transform transform)
        {
            this.steamID = steamID;
            this.transform = transform;
        }
    }
    //Friends
    private GameObject friendsTemplate, content, lableVTOL, JoinButton;
    private ScrollRect scrollRect;
    private ScrollRect scrollRectJoinLog;
    private GameObject lableVTOLJoinLog, lableVTOLMPHeader, lableVTOLMPIntro;
    public Text contentJoinLog;
    private float buttonHeight;
    private List<FriendItem> steamFriends = new List<FriendItem>();

    private List<GameObject> friendListItems = new List<GameObject>();

    private List<GameObject> lobbyListItems = new List<GameObject>();

    protected Callback<LobbyCreated_t> Callback_lobbyCreated;
    protected Callback<LobbyMatchList_t> Callback_lobbyList;
    protected Callback<LobbyEnter_t> Callback_lobbyEnter;
    protected Callback<LobbyDataUpdate_t> Callback_lobbyInfo;


    private CSteamID selectedFriend;
    private Transform selectionTF;

    private Coroutine waitingForJoin;
    private Text joinButtonText;
      public Text lobbyInfoText;

    // Fixing singleplayer functionality with MP mod
    public bool playingMP;

    //Create a host setting for these instead of a variable!
    public Settings settings;
    public bool FriendMode = false;
    public bool hidePlayerNameTags = false;
    public UnityAction<bool> hidePlayerNameTags_changed;
    public UnityAction<bool> hidePlayerRoundels_changed;

    public float thrust = 1.0f;
    public bool alpha = false;
    public float fog = 0.0f;
    public UnityAction<float> thrust_changed;

    public UnityAction<float> fog_changed;
    public UnityAction<bool> alpha_changed;

    public bool spawnRemainingPlayersAtAirBase = false;
    public UnityAction<bool> spawnRemainingPlayersAtAirBase_changed;

    public bool replaceWingmenWithClients = true;
    private UnityAction<bool> replaceWingmenWithClients_changed;

    public bool restrictToHostMods = false;
    private UnityAction<bool> restrictToHostMods_changed;

    public bool debugLogs = false;
    private UnityAction<bool> debugLogs_changed;

    public bool forceWinds = false; // not implemented
    private UnityAction<bool> forceWinds_changed;

    public bool FreeForAllMode = false; // not implemented
    private UnityAction<bool> FreeForAllMode_changed;

    public bool displayPing = false;
    private UnityAction<bool> DisplayPing_changed;

    public bool displayClouds = false;

    private UnityAction<bool> displayClouds_changed;
    public bool ptt = false;

    private UnityAction<bool> ptt_Changed;
    public static Discord.Discord discord;

    List<CSteamID> lobbyIDS = new List<CSteamID>();
    List<VTOLLobby> VtolLobbies = new List<VTOLLobby>();
   
    private void Start()
    {
        if (_instance == null)
        {
            HarmonyInstance harmony = HarmonyInstance.Create("marsh.vtolvr.multiplayer.temperzFork");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        _instance = this;
        Networker.SetMultiplayerInstance(this);

    }
    public static void callback()
    {
        UserManager uman = discord.GetUserManager();
        User meUser = uman.GetCurrentUser();
        long id = meUser.Id;
        VoiceManager vman = discord.GetVoiceManager();
        vman.SetLocalMute(id, true);
    }
    public override void ModLoaded()
    {
        Log($"VTOL VR Multiplayer v{ ModVersionString.ModVersionNumber } - branch: { ModVersionString.ReleaseBranch }");
        Start();

        GameSettings.SetGameSettingValue("USE_OVERCLOUD", false, true);
#if DEBUG
        Log("Running in Debug Mode");
#else
        SoloTesting = false;
        Log("Running in Release Mode");
        System.Net.WebClient wc = new System.Net.WebClient();
        /*string webData = wc.DownloadString(TesterURL + SteamUser.GetSteamID().m_SteamID);
        if (webData != "Y") Relying on the honor system for this
            return;*/
#endif
        SoloTesting = false;
        Log("Valid User " + SteamUser.GetSteamID().m_SteamID);

        DiscordRadioManager.start();
        VTOLAPI.SceneLoaded += SceneLoaded;
        CreateSettingsPage();
        base.ModLoaded();
        CreateUI();
        gameObject.AddComponent<Networker>();
        lobbyIDS = new List<CSteamID>();
        Callback_lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        Callback_lobbyList = Callback<LobbyMatchList_t>.Create(OnGetLobbiesList);
       
        Callback_lobbyInfo = Callback<LobbyDataUpdate_t>.Create(OnGetLobbyInfo);
        debugLog_Settings(debugLogs);
        UnityEngine.Debug.logger.logEnabled = false;
        UnityEngine.Debug.unityLogger.logEnabled = false;

        Networker.setupHooks();
    }

    public void CheckUpToDate()
    {
        UpToDate = true;
        return;

        /*
        if (checkedToDate)
            return;
        checkedToDate = false;
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://vtolvr-mods.com/api/mods/7jdyy3go/?format=json");
     
        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

        HttpWebResponse response = (HttpWebResponse)request.GetResponse();

        if (response.StatusCode == HttpStatusCode.NotFound)
        {

            UpToDate = true;
            return;
        }
        Stream stream = response.GetResponseStream();
        StreamReader reader = new StreamReader(stream);
        string json = reader.ReadToEnd();
        string result = "";
        if (json.Contains("version"))
        {
            int idx = json.IndexOf("version");
            idx = idx + 10;
            Console.WriteLine(json[idx]);
            while (int.TryParse(json[idx].ToString(), out _) || json[idx].ToString() == ".")
            {
                result += json[idx].ToString();
                idx++;
            }
        }
        Debug.Log(result + " , " + ModVersionString.ModVersionNumber);
        if (result != ModVersionString.ModVersionNumber && ModVersionString.ReleaseBranch == "Release")
        {
            Debug.Log("Not up to date.");
            UpToDate = false;
        }*/
    }

    private void CreateSettingsPage()
    {
        settings = new Settings(this);
        settings.CreateCustomLabel("General Settings");
        hidePlayerNameTags_changed += hidePlayerNameTags_Settings;
        hidePlayerRoundels_changed += (set) => { AvatarManager.hideAvatars = set; };
        settings.CreateCustomLabel("Hide player name tags.");
        settings.CreateBoolSetting("Default = False", hidePlayerNameTags_changed, hidePlayerNameTags);
        settings.CreateCustomLabel("Hide players roundels.");
        settings.CreateBoolSetting("Default = True", hidePlayerRoundels_changed, AvatarManager.hideAvatars);

        settings.CreateCustomLabel("Host Settings");

        settings.CreateCustomLabel("Fog strength");
        fog_changed += fog_Settings;
        settings.CreateFloatSetting("Default = 0.0f", fog_changed, 0.0f, 0.0f, 1.0f);

        /*spawnRemainingPlayersAtAirBase_changed += spawnRemainingPlayersAtAirBase_Setting;
        settings.CreateCustomLabel("Spawn players at airbase if there are no wingmen available.");
        settings.CreateBoolSetting("Default = False", spawnRemainingPlayersAtAirBase_changed, spawnRemainingPlayersAtAirBase);*/
        thrust_changed += thrust_Settings;
        alpha_changed += alpha_Settings;
        settings.CreateCustomLabel("Thrust Multiplier");
        settings.CreateFloatSetting("Default = 1.0", thrust_changed, 1.0f, 1.0f, 5.0f, 0.2f);
        settings.CreateCustomLabel("High Alpha Mode");
        settings.CreateBoolSetting("Default = False", alpha_changed, alpha);

        /*replaceWingmenWithClients_changed += replaceWingmenWithClients_Setting;
       settings.CreateCustomLabel("Replace AI wingmen (with the same flight designation) with clients.");
       settings.CreateBoolSetting("Default = True", replaceWingmenWithClients_changed, replaceWingmenWithClients);*/

        restrictToHostMods_changed += restrictToHostMods_Settings;
        settings.CreateCustomLabel("Require clients to use the same mods as host.");
        settings.CreateBoolSetting("Default = False", restrictToHostMods_changed, restrictToHostMods);

        /*forceWinds_changed += forceWinds_Settings;
        settings.CreateCustomLabel("Force winds on for clients (Not functional).");
        settings.CreateBoolSetting("Default = True", forceWinds_changed, forceWinds);*/

        debugLogs_changed += debugLog_Settings;
        settings.CreateCustomLabel("Activate Debug Logs.");
        settings.CreateBoolSetting("Default = False", debugLogs_changed, debugLogs);

        /*FreeForAllMode_changed += FreeForAllMode_Settings;
        settings.CreateCustomLabel("Free For All Mode! Sets all clients to enemies (Not functional).");
        settings.CreateBoolSetting("Default = True", FreeForAllMode_changed, FreeForAllMode);*/

        DisplayPing_changed += DisplayPing_Settings;
        settings.CreateCustomLabel("Show ping on the screen. (Not vr)");
        settings.CreateBoolSetting("Default = False", DisplayPing_changed, displayPing);

        displayClouds_changed += DisplayCloud_Settings;
        settings.CreateCustomLabel("Show Clouds");
        settings.CreateBoolSetting("Default = False", displayClouds_changed, displayClouds);
        ptt_Changed += ptt_Settings;
        settings.CreateCustomLabel("Radio PTT");
        settings.CreateBoolSetting("Default = False", ptt_Changed, ptt);
        VTOLAPI.CreateSettingsMenu(settings);
    }

    public void hidePlayerNameTags_Settings(bool newval)
    {
        hidePlayerNameTags = newval;
    }

    public void spawnRemainingPlayersAtAirBase_Setting(bool newval)
    {
        spawnRemainingPlayersAtAirBase = newval;
    }

    public void replaceWingmenWithClients_Setting(bool newval)
    {
        spawnRemainingPlayersAtAirBase = newval;
    }

    public void restrictToHostMods_Settings(bool newval)
    {
        restrictToHostMods = newval;
    }

    public void debugLog_Settings(bool newval)
    {
        debugLogs = newval;
        if (ModVersionString.ReleaseBranch == "Release")
            DebugCustom.ShowDebugMessages = newval;
        else
        {
            if (DebugCustom.ShowDebugMessages != true)
                DebugCustom.ShowDebugMessages = true;
            UnityEngine.Debug.logger.logEnabled = true;
            UnityEngine.Debug.unityLogger.logEnabled = true;
        }
    }

    public void forceWinds_Settings(bool newval)
    {
        forceWinds = newval;
    }

    public void FreeForAllMode_Settings(bool newval)
    {
        FreeForAllMode = newval;
    }

    public void DisplayPing_Settings(bool newval)
    {
        displayPing = newval;
    }

    public void thrust_Settings(float newval)
    {
        thrust = newval;
    }
    public void alpha_Settings(bool newval)
    {
        alpha = newval;
    }
    public void fog_Settings(float newval)
    {
        fog = newval;
    }
    public void DisplayCloud_Settings(bool newval)
    {
        displayClouds = newval;
        GameSettings.SetGameSettingValue("USE_OVERCLOUD", displayClouds, true);
    }
    public void ptt_Settings(bool newval)
    {
        ptt = newval;
    }

    void OnGUI()//the 2d ping display, feel free to move elsewhere
    {
        if (displayPing)
        {
            string temp = "";
            temp += "Compression Ratio " + Networker.compressionRatio + "\n";
            temp += "Compression Buffer " + Networker.compressionBufferSize + "\n";
            temp += "NormalPacketCompressed " + Networker.overflowedPacket + "\n";
            temp += "NormalPacketUNCompressed " + Networker.overflowedPacketUNC + "\n";
            temp += "compressedtotal " + Networker.totalCompressed + "\n";
            temp += "sucess " + Networker.compressionSucess + "\n";
            temp += "fail " + Networker.compressionFailure + "\n";
            temp += "Compression failure Rate " + Networker.compressionFailTotal / (float)(Networker.compressionSucessTotal + Networker.compressionFailTotal) * 100.0f + "\n";
            foreach (PlayerManager.Player player in PlayerManager.players)
            {
                temp += player.cSteamID + ": " + Mathf.Round(player.ping * 1000f) + "\n";
            }
            if (NetworkSenderThread.Instance != null)
            {
                int enumCount = MessageType.GetNames(typeof(MessageType)).Length;
                for (int i = 0; i < enumCount; i++)
                {
                    MessageType enumHandle = (MessageType)i;
                    if (NetworkSenderThread.Instance.messageCounterTypes.ContainsKey(enumHandle))
                    {
                        float percent = NetworkSenderThread.Instance.messageCounterTypes[enumHandle] / (float)NetworkSenderThread.Instance.messageCounter * 100.0f;
                        temp += MessageType.GetNames(typeof(MessageType))[i] + " " + Mathf.Round(percent) + "\n";
                    }


                }
            }

            GUI.TextArea(new Rect(100, 100, 800, 1800), temp);
        }
    }

    private void SceneLoaded(VTOLScenes scene)
    {
        UnityEngine.CrashReportHandler.CrashReportHandler.enableCaptureExceptions = false;
        DebugCustom.Log($"Scene Switch! { scene.ToString() }");
        Multiplayer._instance.buttonMade = false;
        switch (scene)
        {
            case VTOLScenes.ReadyRoom:
                CreateUI();
                Networker.gameState = Networker.GameState.Menu;
                break;
            case VTOLScenes.Akutan:
                Log("Map Loaded from vtol scenes akutan");
                waitingForJoin = null;
                DestroyLoadingSceneObjects();
                StartCoroutine(PlayerManager.MapLoaded());
                break;
            case VTOLScenes.CustomMapBase:
                Log("Map Loaded from vtol scenes custom map base");
                waitingForJoin = null;
                DestroyLoadingSceneObjects();
                StartCoroutine(PlayerManager.MapLoaded());
                break;
            case VTOLScenes.CustomMapBase_OverCloud:
                Log("Map Loaded from vtol scenes custom map base");
                waitingForJoin = null;
                DestroyLoadingSceneObjects();
                StartCoroutine(PlayerManager.MapLoaded());
                break;
            case VTOLScenes.LoadingScene:

                if (playingMP)
                {
                    Log("Create Loading Scene");
                    CreateLoadingSceneObjects();
                    break;
                }
                break;

            case VTOLScenes.VehicleConfiguration:
                Log("loading akutan disable OC " + PilotSaveManager.currentScenario.mapSceneName);
                if (PilotSaveManager.currentScenario.mapSceneName.Contains("kutan"))
                {
                    Log("loading akutan disable OC");
                    displayClouds = false;
                    GameSettings.SetGameSettingValue("USE_OVERCLOUD", false, true);
                }
                CreateVehicleButton();
                break;
        }
    }

    public void displayError(string errorText)
    {
        try
        {
            contentJoinLog.text = errorText;
            contentJoinLog.color = new Color32(255, 0, 0, 255);
        }
        catch (Exception err)
        {
            DebugCustom.Log("Got an error trying to the contentJoinLog");
            DebugCustom.Log(err.ToString());
        }
    }

    public void displayInfo(string infoText)
    {
        try
        {
            contentJoinLog.text = infoText;
            contentJoinLog.color = new Color32(255, 255, 255, 255);
        }
        catch (Exception err)
        {
            DebugCustom.Log("Got an error trying to update the contentJoinLog");
            DebugCustom.Log(err.ToString());
        }
    }

    public void clearJoinLog()
    {
        try
        {
            contentJoinLog.text = "";
            contentJoinLog.color = new Color32(255, 255, 255, 255);
        }
        catch (Exception err)
        {
            DebugCustom.Log("Got an error trying to clear the contentJoinLog");
            DebugCustom.Log(err.ToString());
        }


    }
    public Transform ScenarioDisplayStore;

    private void CreateUI()
    {

        while (!SceneManager.GetActiveScene().isLoaded)
        {
            DebugCustom.Log("Waiting for scene to be loaded");
        }
        var refrence = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name.Contains("RecenterCanvas"));
        canvasButtonPrefab = Instantiate(refrence);
        canvasButtonPrefab.SetActive(false);

        Log("Creating Multiplayer UI");
        CheckUpToDate();

        Transform ScenarioDisplay = null;
        bool foundDisplay = false;
        bool foundCampaginDisplay = false;
        int? campaignDisplayCount = null;

        DebugCustom.Log("Looping through canvases to find the Scenario Display");


        // Get the interactable canvas
        for (int i = 0; i < GameObject.Find("InteractableCanvas").transform.GetChild(0).childCount; i++)
        {
            // Loop through each child to find the Campaign Select Canavas
            ScenarioDisplay = GameObject.Find("InteractableCanvas").transform.GetChild(0).GetChild(i);
            if (ScenarioDisplay.name == "CampaignSelector")
            {
                foundCampaginDisplay = true;
                campaignDisplayCount = i;
                // Get the next page in the campaign selector (The scenario display)
                ScenarioDisplay = ScenarioDisplay.GetChild(0).GetChild(1);

                // If the name is ScenarioDisplay, we found it! Breaking out of the for loop to continue on...
                if (ScenarioDisplay.name == "ScenarioDisplay")
                {
                    foundDisplay = true;

                    ScenarioDisplayStore = ScenarioDisplay;
                    break;
                }
            }
        }
        DebugCustom.Log($"Found Campaign Display? { foundCampaginDisplay.ToString() }");

        if (campaignDisplayCount != null)
        {
            DebugCustom.Log($"Found Campaign Display { campaignDisplayCount.ToString() } canvases down.");
        }

        DebugCustom.Log($"Found Scenario Display? { foundDisplay.ToString() }");

        //Creating the MP button
        Transform mpButton = Instantiate(ScenarioDisplay.GetChild(10).gameObject, ScenarioDisplay).transform;
        Transform mpButtonF = Instantiate(ScenarioDisplay.GetChild(10).gameObject, ScenarioDisplay).transform;
       
       
        Log("Multiplayer Button" + mpButton.name);
        mpButton.gameObject.SetActive(true);
        mpButton.name = "MPButton";
        mpButton.GetComponent<RectTransform>().localPosition = new Vector3(601, -325);
        mpButton.GetComponent<RectTransform>().sizeDelta = new Vector2(70, 206.7f);
        mpButton.GetComponentInChildren<Text>().text = "Lobby";
        mpButton.GetComponent<Image>().color = Color.cyan;
        mpButton.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();

        mpButtonF.gameObject.SetActive(true);
        mpButtonF.name = "MPButtonf";
        mpButtonF.GetComponent<RectTransform>().localPosition = new Vector3(0, -325);
        mpButtonF.GetComponent<RectTransform>().sizeDelta = new Vector2(70, 206.7f);
        mpButtonF.GetComponentInChildren<Text>().text = "Friend";
        mpButtonF.GetComponent<Image>().color = Color.cyan;
        mpButtonF.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
       
        VRInteractable mpInteractable = mpButton.GetComponent<VRInteractable>();
        if (UpToDate)
        {
            mpButton.GetComponent<Image>().color = Color.cyan;
            mpInteractable.interactableName = "Multiplayer Lobby";
        }
        else
        {
            mpButton.GetComponent<Image>().color = Color.red;
            mpInteractable.interactableName = "Outdated";
        }
        mpInteractable.OnInteract = new UnityEngine.Events.UnityEvent();
        VRInteractable mpInteractablef = mpButtonF.GetComponent<VRInteractable>();
        
        mpInteractablef.OnInteract = new UnityEngine.Events.UnityEvent();
        mpInteractablef.interactableName = "Multiplayer Friend";
        Log("Creating Mp Menu");//Creating Mp Menu
        GameObject MPMenu = Instantiate(ScenarioDisplay.gameObject, ScenarioDisplay.parent);
        GameObject ScrollView = null;
        for (int i = 0; i < MPMenu.transform.childCount; i++)
        {
            if (MPMenu.transform.GetChild(i).name != "Scroll View")
                Destroy(MPMenu.transform.GetChild(i).gameObject);
            else
            {
                ScrollView = MPMenu.transform.GetChild(i).gameObject;
                scrollRect = ScrollView.GetComponent<ScrollRect>();
            }
        }
        content = ScrollView.transform.GetChild(0).GetChild(0).gameObject;
        selectionTF = content.transform.GetChild(0);
        selectionTF.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        Log("Copying the List from select Campaign for friends"); //Copying the List from select Campaign for friends
        friendsTemplate = content.transform.GetChild(1).gameObject;
        buttonHeight = ((RectTransform)friendsTemplate.transform).rect.height;

        Log("Getting the headers from the campaign display for the join log"); //Getting the headers from the campaign display
        GameObject lableTemplateHeader = ScenarioDisplay.Find("Title").gameObject;
        lableVTOLMPHeader = Instantiate(lableTemplateHeader, MPMenu.transform);

        lableVTOLMPHeader.GetComponent<RectTransform>().localPosition = new Vector3(-200, 320);
        lableVTOLMPHeader.GetComponentInChildren<Text>().text = "Welcome to VTOL VR Multiplayer!";
        lableVTOLMPHeader.GetComponentInChildren<Text>().resizeTextForBestFit = true;
        lableVTOLMPHeader.GetComponentInChildren<Text>().color = new Color32(252, 183, 34, 255);
        lableVTOLMPHeader.GetComponentInChildren<Text>().fontSize = 55;
        lableVTOLMPHeader.SetActive(true);


        Log("Getting the headers from the campaign display for the join log"); //Getting the headers from the campaign display
        GameObject lableTemplateIntro = ScenarioDisplay.Find("Title").gameObject;
        lableVTOLMPIntro = Instantiate(lableTemplateIntro, MPMenu.transform);

        lableVTOLMPIntro.GetComponent<RectTransform>().localPosition = new Vector3(-200, 200);
        lableVTOLMPIntro.GetComponent<RectTransform>().sizeDelta = new Vector2(850, 500.3f);
        if (UpToDate)
            lableVTOLMPIntro.GetComponentInChildren<Text>().text = $"Hello and welcome to multiplayer version {ModVersionString.ModVersionNumber}!\n\nThis is an alpha release and very much so a work in progress. Expect bugs!\n\nPlease report any issues at https://vtolvr-mods.com or on the modding discord here: https://discord.gg/pW4rkYf";
        else
            lableVTOLMPIntro.GetComponentInChildren<Text>().text = $"Hello and welcome to multiplayer version {ModVersionString.ModVersionNumber}!\n\nThis is an outdated version, please update the mod to be able to play with other players who have higher versions, and while you're at it, expect bugs!\n\nPlease report any issues at https://vtolvr-mods.com or on the modding discord here: https://discord.gg/pW4rkYf";
        //lableVTOLJoinLog.GetComponentInChildren<Text>().resizeTextForBestFit = true;
        lableVTOLMPIntro.GetComponentInChildren<Text>().color = new Color32(255, 255, 255, 255);
        lableVTOLMPIntro.GetComponentInChildren<Text>().fontSize = 20;
        lableVTOLMPIntro.SetActive(true);

        Log("Getting the headers from the campaign display for the join log"); //Getting the headers from the campaign display
        GameObject lableTemplateLog = ScenarioDisplay.Find("Title").gameObject;
        lableVTOLJoinLog = Instantiate(lableTemplateLog, MPMenu.transform);

        lableVTOLJoinLog.GetComponent<RectTransform>().localPosition = new Vector3(-200, 00);
        lableVTOLJoinLog.GetComponent<RectTransform>().sizeDelta = new Vector2(850, 300f);
        lableVTOLJoinLog.GetComponentInChildren<Text>().text = "";
        //lableVTOLJoinLog.GetComponentInChildren<Text>().resizeTextForBestFit = true;
        lableVTOLJoinLog.GetComponentInChildren<Text>().color = new Color32(255, 0, 0, 255);
        lableVTOLJoinLog.GetComponentInChildren<Text>().fontSize = 20;
        lableVTOLJoinLog.SetActive(true);

        contentJoinLog = lableVTOLJoinLog.GetComponentInChildren<Text>();

        Log("Getting the headers from the campaign display"); //Getting the headers from the campaign display
        GameObject lableTemplate = ScenarioDisplay.parent.GetChild(0).GetChild(5).GetChild(0).GetChild(0).GetChild(2).gameObject;
        lableVTOL = Instantiate(lableTemplate, content.transform);
        lableVTOL.GetComponentInChildren<Text>().text = "In VTOL VR";
        lableVTOL.SetActive(true);

        Log("Back Button");//Back Button
        GameObject BackButton = Instantiate(mpButton.gameObject, MPMenu.transform);
        BackButton.GetComponent<RectTransform>().localPosition = new Vector3(-508, -325);
        BackButton.GetComponent<RectTransform>().sizeDelta = new Vector2(70, 256.3f);
        BackButton.GetComponentInChildren<Text>().text = "Back";
        BackButton.GetComponent<Image>().color = Color.red;
        VRInteractable BackInteractable = BackButton.GetComponent<VRInteractable>();
        BackInteractable.interactableName = "Back";
        BackInteractable.OnInteract = new UnityEngine.Events.UnityEvent();
        BackInteractable.OnInteract.AddListener(delegate { Log("Before Back"); MPMenu.SetActive(false); ScenarioDisplay.gameObject.SetActive(true); });
        Log("Host");//Host
        GameObject HostButton = Instantiate(mpButton.gameObject, MPMenu.transform);
        HostButton.GetComponent<RectTransform>().localPosition = new Vector3(0, -325);
        HostButton.GetComponent<RectTransform>().sizeDelta = new Vector2(70, 256.3f);
        HostButton.GetComponentInChildren<Text>().text = "Host";
        HostButton.GetComponent<Image>().color = Color.green;
        VRInteractable HostInteractable = HostButton.GetComponent<VRInteractable>();
        HostInteractable.interactableName = "Host Game";
        HostInteractable.OnInteract = new UnityEngine.Events.UnityEvent();
        HostInteractable.OnInteract.AddListener(delegate { Log("Before Host"); Host(); });
        Log("Join");//Join
        JoinButton = Instantiate(mpButton.gameObject, MPMenu.transform);
        JoinButton.GetComponent<RectTransform>().localPosition = new Vector3(489, -325);
        JoinButton.GetComponent<RectTransform>().sizeDelta = new Vector2(70, 256.3f);
        joinButtonText = JoinButton.GetComponentInChildren<Text>();
        joinButtonText.text = "Join";
        joinButtonText.resizeTextForBestFit = true;
        JoinButton.GetComponent<Image>().color = Color.blue;
        VRInteractable JoinInteractable = JoinButton.GetComponent<VRInteractable>();
        JoinInteractable.interactableName = "Join Game";
        JoinInteractable.OnInteract = new UnityEngine.Events.UnityEvent();
        JoinInteractable.OnInteract.AddListener(delegate { Log("Before Join"); Join(); });
        JoinButton.SetActive(false);
        Log("Lobby Info Text");
        GameObject lobbyInfoGO = Instantiate(mpButton.transform.GetChild(0).gameObject, MPMenu.transform);
        lobbyInfoGO.GetComponent<RectTransform>().localPosition = new Vector3(-168.3f, -30.9f);
        lobbyInfoGO.GetComponent<RectTransform>().sizeDelta = new Vector2(942.9f, 469.8f);
        lobbyInfoText = lobbyInfoGO.GetComponent<Text>();
        lobbyInfoText.text = "Select a friend or host a lobby.";
        lobbyInfoText.alignment = TextAnchor.UpperLeft;
        lobbyInfoText.transform.localRotation = Quaternion.Euler(lobbyInfoText.transform.localRotation.eulerAngles.x + 90,
            lobbyInfoText.transform.localRotation.y,
            lobbyInfoText.transform.localRotation.z);
        Log("Last one");
        mpInteractable.OnInteract.AddListener(delegate { Log("Before Opening MP"); FriendMode = false; StartCoroutine(Refershlobbies()); MPMenu.SetActive(true); ScenarioDisplay.gameObject.SetActive(false); OpenMP(); });
        mpInteractablef.OnInteract.AddListener(delegate { Log("Before Opening MP"); FriendMode = true;  RefershFriends(); MPMenu.SetActive(true); ScenarioDisplay.gameObject.SetActive(false); OpenMP(); });
        GameObject.Find("InteractableCanvas").GetComponent<VRPointInteractableCanvas>().RefreshInteractables();
        Log("Finished");

        Log("Refresh");//Host
        GameObject RefreshButton = Instantiate(mpButton.gameObject, MPMenu.transform);
        RefreshButton.GetComponent<RectTransform>().localPosition = new Vector3(510, 322);
        RefreshButton.GetComponent<RectTransform>().sizeDelta = new Vector2(50, 250f);
        RefreshButton.GetComponentInChildren<Text>().text = "Refresh";
        RefreshButton.GetComponent<Image>().color = Color.red;
        VRInteractable RefreshInteractable = RefreshButton.GetComponent<VRInteractable>();
        RefreshInteractable.interactableName = "Refresh";
        RefreshInteractable.OnInteract = new UnityEngine.Events.UnityEvent();
        RefreshInteractable.OnInteract.AddListener(delegate { Log("Before Host"); if(!FriendMode)StartCoroutine(Refershlobbies()); else RefershFriends();  });


    }
    private IEnumerator Refershlobbies()
    {
        lableVTOL.SetActive(false);
        foreach (GameObject uiItem in lobbyListItems)
        {
             
            Destroy(uiItem);
        }
        lobbyListItems.Clear();

        foreach (GameObject uiItem in friendListItems)
        {
            if (uiItem != null)
            {
                DebugCustom.Log($"Destroying {uiItem.name}");
            }
            else
            {
                DebugCustom.Log("UI Item is null");
            }

            Destroy(uiItem);
        }

        friendListItems.Clear();
        friendsTemplate.SetActive(true);
        SteamMatchmaking.RequestLobbyList();
        yield return new WaitForSeconds(2);

        GameObject lastlobbyGO;
        VRUIListItemTemplate uiListItem;
        int totalLobby = 0;
     
        for (int i = 0; i < VtolLobbies.Count; i++)
        {
            totalLobby++;
            lastlobbyGO = Instantiate(friendsTemplate, content.transform);
            lastlobbyGO.name = VtolLobbies[i].hostName;

            lastlobbyGO.transform.localPosition = new Vector3(0f, -totalLobby * buttonHeight);
            uiListItem = lastlobbyGO.GetComponent<VRUIListItemTemplate>();
            uiListItem.Setup(VtolLobbies[i].hostName, totalLobby - 1, SelectLobby);
            uiListItem.labelText.color = Color.green;
         
            lobbyListItems.Add(lastlobbyGO);
        }

        Log("Updating Scroll Rect");
        scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, (2f + VtolLobbies.Count) * buttonHeight);
        scrollRect.ClampVertical();

        JoinButton.SetActive(false);
        friendsTemplate.SetActive(false);
        
        GameObject.Find("InteractableCanvas").GetComponent<VRPointInteractableCanvas>().RefreshInteractables();
     
    }
        public void RefershFriends()
    {
        foreach (GameObject uiItem in lobbyListItems)
        {

            Destroy(uiItem);
        }
        lobbyListItems.Clear();
        Log("Refreshing Friends");
        steamFriends.Clear();
        int totalFriends = 0;
        DebugCustom.Log($"UI List Item Count: {friendListItems.Count}");

        foreach (GameObject uiItem in friendListItems)
        {
            if (uiItem != null)
            {
                DebugCustom.Log($"Destroying {uiItem.name}");
            }
            else
            {
                DebugCustom.Log("UI Item is null");
            }

            Destroy(uiItem);
        }

        friendListItems.Clear();

        int friendsCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
        if (friendsCount == -1)
        {
            LogError("Friendcount == -1, user isn't logged into steam");
            return;
        }

        /*
         * First we are going to sort the list into four sections
         * Friends playing vtol vr
         * Friends in game
         * Friends online
         * Friends offline
         */
        CSteamID lastFriendID;
        List<CSteamID> vtolvrFriends = new List<CSteamID>();
        Log("Getting all friends");
        for (int i = 0; i < friendsCount; i++)
        {
            lastFriendID = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            if (SteamFriends.GetFriendGamePlayed(lastFriendID, out FriendGameInfo_t gameInfo))
            {
                if (gameInfo.m_gameID.AppID().m_AppId == 667970)
                {
                    //User is in VTOLVR
                    vtolvrFriends.Add(lastFriendID);
                    continue;
                }
            }
        }
        //m_CallResultLobbyMatchList.Set(hSteamAPICall, this, &CLobbyListManager::OnLobbyMatchList);
        Log("Adding friends to list");
        //Now we want to create the ingame list
         friendsTemplate.SetActive(true);
        GameObject lastFriendGO;
        VRUIListItemTemplate uiListItem;
        totalFriends = 0;
        lableVTOL.transform.localPosition = new Vector3(0, -totalFriends * buttonHeight);
        for (int i = 0; i < vtolvrFriends.Count; i++)
        {
            totalFriends++;
            lastFriendGO = Instantiate(friendsTemplate, content.transform);
            lastFriendGO.name = SteamFriends.GetFriendPersonaName(vtolvrFriends[i]);
            steamFriends.Add(new FriendItem(vtolvrFriends[i], lastFriendGO.transform));
            lastFriendGO.transform.localPosition = new Vector3(0f, -totalFriends * buttonHeight);
            uiListItem = lastFriendGO.GetComponent<VRUIListItemTemplate>();
            uiListItem.Setup(SteamFriends.GetFriendPersonaName(vtolvrFriends[i]), totalFriends - 1, SelectFriend);
            uiListItem.labelText.color = Color.green;
            friendListItems.Add(lastFriendGO);
        }

        Log("Updating Scroll Rect");
        scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, (2f + steamFriends.Count) * buttonHeight);
        scrollRect.ClampVertical(); 

        JoinButton.SetActive(false);
        friendsTemplate.SetActive(false);
        Log("Refreahing Interactables");
        GameObject.Find("InteractableCanvas").GetComponent<VRPointInteractableCanvas>().RefreshInteractables();
        Log($"Total Friends:{totalFriends} In VTOLVR:{vtolvrFriends.Count}");
        Networker.ResetNetworkUID();
    }


    void OnGetLobbiesList(LobbyMatchList_t result)
    {
        lobbyIDS.Clear();
        VtolLobbies.Clear();
        for (int i = 0; i < result.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
            lobbyIDS.Add(lobbyID);
            SteamMatchmaking.RequestLobbyData(lobbyID);
        }
    }
    void OnLobbyCreated(LobbyCreated_t result)
    {
        if (result.m_eResult == EResult.k_EResultOK)
            DebugCustom.Log("Lobby created -- SUCCESS!");
        else
            DebugCustom.Log("Lobby created -- failure ...");

        string personalName = SteamFriends.GetPersonaName();
        SteamMatchmaking.SetLobbyData((CSteamID)result.m_ulSteamIDLobby, "name", personalName);

        SteamMatchmaking.SetLobbyData((CSteamID)result.m_ulSteamIDLobby, "hostID",  SteamUser.GetSteamID().m_SteamID.ToString());
        if(PilotSaveManager.currentScenario!=null)
        {
            SteamMatchmaking.SetLobbyData((CSteamID)result.m_ulSteamIDLobby, "ScenarioID", PilotSaveManager.currentScenario.scenarioID);
            SteamMatchmaking.SetLobbyData((CSteamID)result.m_ulSteamIDLobby, "Scenarioname", PilotSaveManager.currentScenario.scenarioName);
        }
        else
            DebugCustom.Log("bad scenrio");
        if (PilotSaveManager.currentCampaign != null)
            SteamMatchmaking.SetLobbyData((CSteamID)result.m_ulSteamIDLobby, "CampID", PilotSaveManager.currentCampaign.campaignID);
          else
            DebugCustom.Log("bad camp");
        if (PilotSaveManager.currentVehicle != null)
            SteamMatchmaking.SetLobbyData((CSteamID)result.m_ulSteamIDLobby, "plane", PilotSaveManager.currentVehicle.name);
        else DebugCustom.Log("bad vech");

        Networker.lobbyID = (CSteamID)result.m_ulSteamIDLobby;
    }

    void OnGetLobbyInfo(LobbyDataUpdate_t result)
    {
        for (int i = 0; i < lobbyIDS.Count; i++)
        {
            if (lobbyIDS[i].m_SteamID == result.m_ulSteamIDLobby)
            {
                if (SteamMatchmaking.GetLobbyData((CSteamID)lobbyIDS[i].m_SteamID, "plane") != PilotSaveManager.currentVehicle.name)
                    return;
                VTOLLobby newLob = new VTOLLobby();
                newLob.hostName=SteamMatchmaking.GetLobbyData((CSteamID)lobbyIDS[i].m_SteamID, "name");
                newLob.hostID  = new CSteamID(Convert.ToUInt64(SteamMatchmaking.GetLobbyData((CSteamID)lobbyIDS[i].m_SteamID, "hostID")));
                newLob.scenID = SteamMatchmaking.GetLobbyData((CSteamID)lobbyIDS[i].m_SteamID, "ScenarioID");
                newLob.campID = SteamMatchmaking.GetLobbyData((CSteamID)lobbyIDS[i].m_SteamID, "CampID");
                newLob.plane = SteamMatchmaking.GetLobbyData((CSteamID)lobbyIDS[i].m_SteamID, "plane");
                newLob.scnName = SteamMatchmaking.GetLobbyData((CSteamID)lobbyIDS[i].m_SteamID, "Scenarioname");
                VtolLobbies.Add(newLob);

                DebugCustom.Log("Lobby added" + newLob.hostName);
                DebugCustom.Log("Lobbycampid" + newLob.campID);
                DebugCustom.Log("Lobby plan" + newLob.plane);

                return;
            }
        }

    }
    public void SelectFriend(int index)
    {
        JoinButton.SetActive(true);
        joinButtonText.text = $"Join {SteamFriends.GetFriendPersonaName(steamFriends[index].steamID)}";
        selectedFriend = steamFriends[index].steamID;
        Log("User has selected " + SteamFriends.GetFriendPersonaName(steamFriends[index].steamID));
        NetworkSenderThread.Instance.SendPacketToSpecificPlayer(steamFriends[index].steamID, new Message_LobbyInfoRequest(), EP2PSend.k_EP2PSendReliable); //Getting lobby info.
        selectionTF.position = steamFriends[index].transform.position;
        selectionTF.GetComponent<Image>().color = new Color(0.3529411764705882f, 0.196078431372549f, 0);
    }

    public void SelectLobby(int index)
    {
        JoinButton.SetActive(true);
        joinButtonText.text = $"Join {VtolLobbies[index].hostName}";
        selectedFriend = VtolLobbies[index].hostID;
        //PilotSaveManager.currentVehicle = PilotSaveManager.GetVehicle(VtolLobbies[index].plane);

        CampaignSelectorUI selectorUI = FindObjectOfType<CampaignSelectorUI>();

        List<Campaign> campaignss = (List<Campaign>)Traverse.Create(selectorUI).Field("campaigns").GetValue();
        int iddx = 0;
        for (int i= 0; i<campaignss.Count(); i++)
        {
            if(campaignss[i].campaignID == VtolLobbies[index].campID)
            {
                  Traverse.Create(selectorUI).Field("campaignIdx").SetValue(i);
               
                iddx = i;
            }

        }
        bool found = false;
        PilotSaveManager.currentScenario = null;
        //selectorUI.SelectCampaign();
        PilotSaveManager.currentCampaign = campaignss[iddx];
        for  (int s= 0; s < campaignss[iddx].missions.Count(); s++ )
        {
            if (campaignss[iddx].missions[s].scenarioID == VtolLobbies[index].scenID)
            {
                PilotSaveManager.currentScenario = campaignss[iddx].missions[s];
                found = true;
            }
            
        }
        Networker._instance.pilotSaveManagerControllerCampaign = PilotSaveManager.currentCampaign;


        Networker._instance.pilotSaveManagerControllerCampaignScenario = PilotSaveManager.currentScenario;
        // scn = VTResources.GetCustomScenario(VtolLobbies[index].scenID, camp.campaignID);
        ScenarioDisplayStore.gameObject.SetActive(false);

        contentJoinLog.text = VtolLobbies[index].hostName + " is playing " + VtolLobbies[index].scnName;
        if (found ==false)
        { 
            contentJoinLog.text += ",Please Select Steamworkshop Or You May Not Have It Downloaded";
        }
        NetworkSenderThread.Instance.SendPacketToSpecificPlayer(VtolLobbies[index].hostID, new Message_LobbyInfoRequest(), EP2PSend.k_EP2PSendReliable); //Getting lobby info.

        //selectionTF.position = steamFriends[index].transform.position;
        //selectionTF.GetComponent<Image>().color = new Color(0.3529411764705882f, 0.196078431372549f, 0);
    }

    public void OpenMP()
    {
       
        CampaignSelectorUI selectorUI = FindObjectOfType<CampaignSelectorUI>();
        int missionIdx = (int)Traverse.Create(selectorUI).Field("missionIdx").GetValue();
        PilotSaveManager.currentScenario = PilotSaveManager.currentCampaign.missions[missionIdx];
        Log("Pressed Open Multiplayer with\n" +
            PilotSaveManager.currentScenario + "\n" +
            PilotSaveManager.currentCampaign + "\n" +
            PilotSaveManager.currentVehicle);


    }

    public void Host()
    {
        if(PilotSaveManager.currentScenario ==null)
        {
            if (contentJoinLog != null)
                contentJoinLog.text = "Select Scenario";
                return;
        }
        CampaignSelectorUI selectorUI = FindObjectOfType<CampaignSelectorUI>();
        PlayerVehicle pv = PilotSaveManager.currentVehicle;
        Campaign cc = PilotSaveManager.currentCampaign;
        string name = PlayerManager.selectedVehicle;

        PlayerManager.selectedVehicle = "F/A-26B";
        PilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle(PlayerManager.selectedVehicle);
        string campID;
        campID = "fa26bFreeFlight";
        PilotSaveManager.current.lastVehicleUsed = PilotSaveManager.currentVehicle.name;
        Campaign campref = VTResources.GetBuiltInCampaign(campID).ToIngameCampaign();
        PilotSaveManager.currentCampaign = campref;
        selectorUI.SetupCampaignScenarios(campref, false);

        PlayerManager.selectedVehicle = "AV-42C";
        PilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle(PlayerManager.selectedVehicle);
        campID = "av42cQuickFlight";
        PilotSaveManager.current.lastVehicleUsed = PilotSaveManager.currentVehicle.name;
        campref = VTResources.GetBuiltInCampaign(campID).ToIngameCampaign();
        PilotSaveManager.currentCampaign = campref;
        selectorUI.SetupCampaignScenarios(campref, false);

        PlayerManager.selectedVehicle = "F-45A";
        PilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle(PlayerManager.selectedVehicle);
        campID = "f45-quickFlight";
        PilotSaveManager.current.lastVehicleUsed = PilotSaveManager.currentVehicle.name;
        campref = VTResources.GetBuiltInCampaign(campID).ToIngameCampaign();
        PilotSaveManager.currentCampaign = campref;

        selectorUI.SetupCampaignScenarios(campref, false);

        PilotSaveManager.currentVehicle = pv;
        PilotSaveManager.currentCampaign = cc;
        PlayerManager.selectedVehicle = name;

        Networker._instance.DisconnectionTasks();
        DebugCustom.Log("Dictionaries cleared just in case.");
        playingMP = true;
        MapAndScenarioVersionChecker.CreateHashes();
        Networker._instance.HostGame();
    }

    public void Join()
    {

        playingMP = true;
        if (Networker.hostID == new Steamworks.CSteamID(0) && waitingForJoin == null)
        {
            Networker.JoinGame(selectedFriend);
            DebugCustom.Log($"Joining friend {selectedFriend.m_SteamID}");
            waitingForJoin = StartCoroutine(WaitingForJoiningRequestResult());
        }
        else
            LogWarning("Already in a game with " + Networker.hostID.m_SteamID);
    }
    private IEnumerator WaitingForJoiningRequestResult()
    {
        for (int i = 5; i > 0; i--)
        {
         joinButtonText.text = $"Joining [{i}]";
        yield return new WaitForSeconds(1);
        }
        joinButtonText.text = "Join";
        waitingForJoin = null;
    }

    private void CreateLoadingSceneObjects()
    {
        DebugCustom.Log("Creating Loading Screen Object.");

        //Transform cube = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        //cube.position = new Vector3(-0.485f, 1.153f, 0.394f);
        //cube.rotation = Quaternion.Euler(0,-53.038f,0);
        //cube.localScale = new Vector3(0.5f,0.5f,0.01f);
        //cube.name = "Multiplayer Player List";

        DebugCustom.Log("Looking for Canvas and Info Transforms");
        GameObject canvas_go = GameObject.Find("Canvas").gameObject;

        Transform infoCanvas = canvas_go.transform.Find("Info").transform;

        //Transform canvas = canvas_go.transform;

        //Text missionText = canvas.Find("MissionText").GetComponent<Text>();

        DebugCustom.Log("Removing old text");
        infoCanvas.Find("Ready").GetComponent<Text>().text = "";
        infoCanvas.Find("MissionText").GetComponent<Text>().text = "";
        infoCanvas.Find("MissionText").gameObject.SetActive(false);
        infoCanvas.Find("progressGameObject").gameObject.SetActive(false);
        infoCanvas.Find("progressGameObject").transform.Find("LoadingText").gameObject.SetActive(false);
        infoCanvas.Find("progressGameObject").transform.Find("LoadingText (1)").gameObject.SetActive(false);

        //Destroy(missionText);

        //missionText.text = "HELLO!";

        //for (int i = 0; i < GameObject.Find("Canvas").transform.GetChild(0).childCount; i++)
        //{
        //    // Loop through each child to find the Campaign Select Canavas
        //    Transform cube = GameObject.Find("InteractableCanvas").transform.GetChild(0).GetChild(i);
        //    if (cube.name == "CampaignSelector")
        //    {
        //        // Get the next page in the campaign selector (The scenario display)
        //        cube = cube.GetChild(0).GetChild(1);

        //        // If the name is ScenarioDisplay, we found it! Breaking out of the for loop to continue on...
        //        if (cube.name == "ScenarioDisplay")
        //        {
        //            break;
        //        }
        //    }
        //}Adding loading text

        //Can't add component 'RectTransform' to Text because such a component is already added to the game object!
        //Loaded scene: LoadingScene

        DebugCustom.Log("Adding loading text");

        Networker.loadingText = canvas_go.AddComponent<TextMeshPro>();

        //GameObject Text = new GameObject("Text", typeof(TextMeshPro));
        //Text.transform.SetParent(canvas, false);
        RectTransform rect = Networker.loadingText.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(676, 350);
        //rect.position = new Vector3(0, -99, 0);
        //Networker.loadingText = Text.GetComponent<TextMeshPro>();
        Networker.loadingText.enableAutoSizing = true;
        Networker.loadingText.fontSizeMin = 100;
        Networker.loadingText.fontSizeMax = 450;
        Networker.loadingText.color = Color.white;
        if (!Networker.isHost)
        {
            NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, new Message_LoadingTextRequest(), EP2PSend.k_EP2PSendReliable); // Getting Loading Text

        }
        Networker.UpdateLoadingText();
    }

    public void CleanUpOnDisconnect()
    {
        selectedFriend = new CSteamID(0);
        steamFriends?.Clear();
        playingMP = false;
    }

    private void DestroyLoadingSceneObjects()
    {
        GameObject cube = GameObject.Find("Multiplayer Player List");
        if (cube != null)
        {
            DebugCustom.Log("Destroying MP list cube");
            Destroy(cube);
        }
        else
        {
            DebugCustom.Log("Could not find MP list cube");
        }
    }

    public static GameObject CreateVehicleButton()
    {

        if (canvasButtonPrefab == null)
        {
            var refrence = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name.Contains("RecenterCanvas"));
            canvasButtonPrefab = Instantiate(refrence);
            canvasButtonPrefab.SetActive(false);
            DontDestroyOnLoad(canvasButtonPrefab);
        }
        foreach (var controller in GameObject.FindObjectsOfType<VRHandController>())
        {
            GameObject button;
            if (canvasButtonPrefab == null)
            {
                button = GameObject.Instantiate(GameObject.Find("RecenterCanvas"));
            }
            else
            {
                button = GameObject.Instantiate(canvasButtonPrefab);
                button.SetActive(true);
            }
            if (!controller.isLeft)
            {
                DebugCustom.Log("Current vehicle name is " + PilotSaveManager.currentVehicle.name);
                button.transform.SetParent(controller.transform);
                button.transform.localPosition = new Vector3(0.101411f, 0.02100047f, -0.128024f);
                button.transform.localRotation = Quaternion.Euler(-5.834f, 283.583f, 328.957f);
                button.transform.localScale = new Vector3(button.transform.localScale.x * -1, button.transform.localScale.y * -1, button.transform.localScale.z);
                VRInteractable bInteractable = button.GetComponentInChildren<VRInteractable>();
                Text text = button.GetComponentInChildren<Text>();
                text.transform.localScale = text.transform.localScale * 0.75f;
                text.text = PilotSaveManager.currentVehicle.name;
                bInteractable.interactableName = "Switch Vehicles.";
                bInteractable.OnInteract = new UnityEvent();
                PlayerManager.selectedVehicle = PilotSaveManager.currentVehicle.name;

                foreach (var vehicle in VTResources.GetPlayerVehicles())
                {
                    DebugCustom.Log(vehicle.name);
                    DebugCustom.Log(vehicle.vehicleName);
                }
                bInteractable.OnInteract.AddListener(delegate
                {
                    if (PilotSaveManager.currentVehicle.name == "AV-42C")
                    {
                        PilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle("F/A-26B");
                        PilotSaveManager.current.lastVehicleUsed = PilotSaveManager.currentVehicle.name;

                    }
                    else if (PilotSaveManager.currentVehicle.name == "FA-26B" || PilotSaveManager.currentVehicle.name == "F/A-26B")
                    {
                        PilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle("F-45A");
                        PilotSaveManager.current.lastVehicleUsed = PilotSaveManager.currentVehicle.name;
                    }
                    else
                    {
                        PilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle("AV-42C");
                        PilotSaveManager.current.lastVehicleUsed = PilotSaveManager.currentVehicle.name;
                    }
                    text.text = PilotSaveManager.currentVehicle.name;
                    PlayerManager.selectedVehicle = text.text;
                    if (VTOLAPI.currentScene == VTOLScenes.VehicleConfiguration)
                    {
                        // BPilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle(PlayerManager.selectedVehicle);
                        string campID;
                        if (PlayerManager.selectedVehicle == "AV-42C")
                        {
                            campID = "av42cQuickFlight";
                        }
                        else if (PilotSaveManager.currentVehicle.name == "FA-26B" || PilotSaveManager.currentVehicle.name == "F/A-26B")
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
                        SceneManager.LoadScene("VehicleConfiguration");
                    }
                });
            }
            if (canvasButtonPrefab == null)
            {
                canvasButtonPrefab = Instantiate(GameObject.Find("RecenterCanvas"));
                canvasButtonPrefab.SetActive(false);
                DontDestroyOnLoad(canvasButtonPrefab);
            }
            Multiplayer._instance.buttonMade = true;
            return button;
        }
        return null;
    }


    public static GameObject CreateFreqButton()
    {
        foreach (var controller in GameObject.FindObjectsOfType<VRHandController>())
        {
            GameObject button;
            if (canvasButtonPrefab == null)
            {
                return null;
            }
            else
            {
                button = GameObject.Instantiate(canvasButtonPrefab);
                button.SetActive(true);
            }
            if (!controller.isLeft)
            {
                DebugCustom.Log("Freq");
                button.transform.SetParent(controller.transform);
                button.transform.localPosition = new Vector3(0.101411f, 0.02100047f, -0.128024f);
                button.transform.localRotation = Quaternion.Euler(-5.834f, 283.583f, 328.957f);
                button.transform.localScale = new Vector3(button.transform.localScale.x * -1, button.transform.localScale.y * -1, button.transform.localScale.z);
                VRInteractable bInteractable = button.GetComponentInChildren<VRInteractable>();
                Text text = button.GetComponentInChildren<Text>();
                text.transform.localScale = text.transform.localScale * 0.75f;
                text.text = "Freq: " + CUSTOM_API.currentFreq;
                DiscordRadioManager.radioFreq = CUSTOM_API.currentFreq.GetHashCode();
                bInteractable.interactableName = "Freq.";
                bInteractable.OnInteract = new UnityEvent();

                string textS = "";
                bInteractable.OnInteract.AddListener(delegate
                {
                    textS = DiscordRadioManager.getNextFrequency();
                    DiscordRadioManager.radioFreq = textS.GetHashCode();
                    CUSTOM_API.forceSetFreq(textS);
                    text.text = textS;
                    DebugCustom.Log("discord freq " + DiscordRadioManager.radioFreq);
                });
            }

            return button;
        }
        return null;
    }

    public static GameObject CreateCustomPlaneButton()
    {
        if (PlayerManager.CustomPlaneNames.Count == 0)
            return null;
        foreach (var controller in GameObject.FindObjectsOfType<VRHandController>())
        {
            GameObject button;
            if (canvasButtonPrefab == null)
            {
                return null;
            }
            else
            {
                button = GameObject.Instantiate(canvasButtonPrefab);
                button.SetActive(true);
            }
            if (!controller.isLeft)
            {
                 
                button.transform.SetParent(controller.transform);
                button.transform.localPosition = new Vector3(0.101411f, 0.02100047f, -0.328024f);
                button.transform.localRotation = Quaternion.Euler(-5.834f, 283.583f, 328.957f);
                button.transform.localScale = new Vector3(button.transform.localScale.x * -1, button.transform.localScale.y * -1, button.transform.localScale.z);
                VRInteractable bInteractable = button.GetComponentInChildren<VRInteractable>();
                Text text = button.GetComponentInChildren<Text>();
                text.transform.localScale = text.transform.localScale * 0.75f;
                text.text = "Custom Plane: " + PlayerManager.CustomPlaneNames[PlayerManager.CustomPlaneIndex];
                
                bInteractable.interactableName = "plane";
                bInteractable.OnInteract = new UnityEvent();

                string textS = "";
                bInteractable.OnInteract.AddListener(delegate
                {
                    if(PlayerManager.CustomPlaneIndex+1< PlayerManager.CustomPlaneNames.Count)
                    PlayerManager.CustomPlaneIndex += 1;
                    else
                        PlayerManager.CustomPlaneIndex = 0;
                    textS = "Custom Plane: " + PlayerManager.CustomPlaneNames[PlayerManager.CustomPlaneIndex];
                  
                    PlayerManager.LoadedCustomPlaneString = PlayerManager.CustomPlaneNames[PlayerManager.CustomPlaneIndex];
                   
                    if(PlayerManager.CustomPlaneNames[PlayerManager.CustomPlaneIndex]!="none")
                    {

                        PlayerManager.PlayerIsCustomPlane = true;
                        if (PlayerManager.CustomPlaneNamesBasePlane[PlayerManager.CustomPlaneIndex] == "AV-42C")
                        {
                         
                            PilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle("AV-42C");
                            PilotSaveManager.current.lastVehicleUsed = PilotSaveManager.currentVehicle.name;

                        }
                        else if (PlayerManager.CustomPlaneNamesBasePlane[PlayerManager.CustomPlaneIndex] ==   "FA-26B" || PlayerManager.CustomPlaneNamesBasePlane[PlayerManager.CustomPlaneIndex] == "F/A-26B")
                        {
                            PilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle("F/A-26B");
                            PilotSaveManager.current.lastVehicleUsed = PilotSaveManager.currentVehicle.name;
                           
                        }
                        else
                        {
                            PilotSaveManager.currentVehicle = VTResources.GetPlayerVehicle("F-45A");
                            PilotSaveManager.current.lastVehicleUsed = PilotSaveManager.currentVehicle.name;
                        }
                       
                        PlayerManager.selectedVehicle = PilotSaveManager.currentVehicle.name;
                        
                    }
                    else
                    {
                        PlayerManager.PlayerIsCustomPlane = false;
                    }
                    //DiscordRadioManager.radioFreq = textS.GetHashCode();
                    //CUSTOM_API.forceSetFreq(textS);
                    text.text = textS;
                   // Debug.Log("discord freq " + DiscordRadioManager.radioFreq);
                });
            }

            return button;
        }
        return null;
    }
    public void OnDestroy()
    {
        VTOLAPI.SceneLoaded -= SceneLoaded;
        Networker.OnMultiplayerDestroy();
    }
}