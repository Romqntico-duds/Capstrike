using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;

public class GameUI : MonoBehaviour
{
    static readonly Vector2 LobbyDesignSize = new Vector2(960f, 540f);
    const float LobbyEdgePadding = 12f;
    public RectTransform lobbyBackground;
    public RectTransform lobbyPanel;
    public RectTransform connectedPanel;
    public RectTransform mainMenuPanel;
    public RectTransform settingsPanel;
    public InputField playerNameField;
    public InputField ipField;
    public Button hostButton;
    public Button clientButton;
    public Button disconnectButton;
    public Button blueTeamButton;
    public Button orangeTeamButton;
    public Button readyButton;
    public Button fillPlayersButton;
    public Button removeAIButton;
    public Button startGameButton;
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;
    public Button settingsBackButton;
    public Button connectionBackButton;
    public RectTransform lobbySearchPanel;
    public Button lobbySearchBackButton;
    public Button[] discoveredLobbyButtons;
    public Text[] discoveredLobbyLabels;
    public Text lobbySearchStatusLabel;
    public Button classicMapButton;
    public Button narrowMapButton;
    public Text mapStatusLabel;
    [SerializeField] Color classicMapSelectedColor = new Color(0.04f, 0.48f, 0.9f);
    [SerializeField] Color narrowMapSelectedColor = new Color(0.95f, 0.32f, 0.05f);
    [SerializeField] Color mapUnselectedColor = new Color(0.48f, 0.58f, 0.68f, 0.8f);
    public Slider soundVolumeSlider;
    public Text soundVolumeLabel;
    public GameplayHud gameplayHudPrefab;
    public Text ipLabel;
    public Text messageLabel;
    public Text connectionLabel;
    public Text blueTeamHeaderLabel;
    public Text orangeTeamHeaderLabel;
    public Text blueTeamRosterLabel;
    public Text orangeTeamRosterLabel;
    public Text blueTeamSecondSlotLabel;
    public Text orangeTeamSecondSlotLabel;
    public Text readyLabel;
    public Image blueSlotRow1Image;
    public Image blueSlotRow2Image;
    public Image orangeSlotRow1Image;
    public Image orangeSlotRow2Image;
    public Sprite unfilledPlayerSprite;
    public Sprite filledPlayerSprite;
    public Image blueTeamCountImage;
    public Image orangeTeamCountImage;
    public Sprite blueTeamCount0Sprite;
    public Sprite blueTeamCount1Sprite;
    public Sprite blueTeamCount2Sprite;
    public Sprite orangeTeamCount0Sprite;
    public Sprite orangeTeamCount1Sprite;
    public Sprite orangeTeamCount2Sprite;
    public Sprite readyButtonSprite;
    public Sprite readyCancelSprite;
    Game game;
    bool showConnectionScreen;
    bool showSettingsScreen;
    bool showLobbySearch;
    List<LanLobbyInfo> displayedLobbies;
    int shownMap = -1;
    GameplayHud gameplayHud;
    GameplayHud.SoundSettingsView mainMenuSoundSettings;
    int previousLobbyRoster;

    public void Initialize(Game owner)
    {
        game = owner;
        connectedPanel.anchorMin = connectedPanel.anchorMax = connectedPanel.pivot = new Vector2(0.5f, 0.5f);
        connectedPanel.sizeDelta = LobbyDesignSize;
        if (gameplayHudPrefab != null)
        {
            gameplayHud = Instantiate(gameplayHudPrefab, transform);
            gameplayHud.gameObject.SetActive(false);
            gameplayHud.Initialize(game);
            foreach (Transform child in settingsPanel)
                child.gameObject.SetActive(false);
            mainMenuSoundSettings = gameplayHud.CreateSoundSettings(settingsPanel, ReturnToMainMenu);
        }
        InputSystemUIInputModule inputModule = GetComponent<InputSystemUIInputModule>();
        if (inputModule != null)
            inputModule.AssignDefaultActions();
        playerNameField.text = PlayerPrefs.GetString("CapStrikePlayerName", "");
        hostButton.onClick.AddListener(HostClicked);
        clientButton.onClick.AddListener(JoinClicked);
        disconnectButton.onClick.AddListener(game.Disconnect);
        blueTeamButton.onClick.AddListener(() => game.SelectTeam(1));
        orangeTeamButton.onClick.AddListener(() => game.SelectTeam(2));
        readyButton.onClick.AddListener(game.ToggleReady);
        fillPlayersButton.onClick.AddListener(game.FillPlayers);
        removeAIButton.onClick.AddListener(game.RemoveAI);
        startGameButton.onClick.AddListener(game.StartGame);
        playButton.onClick.AddListener(ShowConnectionScreen);
        settingsButton.onClick.AddListener(ShowSettingsScreen);
        quitButton.onClick.AddListener(QuitGame);
        settingsBackButton.onClick.AddListener(ReturnToMainMenu);
        connectionBackButton.onClick.AddListener(ReturnToMainMenu);
        if (lobbySearchPanel == null) CreateLobbySearchPanel();
        ConfigureDirectJoin();
        lobbySearchPanel.gameObject.SetActive(false);
        if (lobbySearchBackButton != null) lobbySearchBackButton.onClick.AddListener(CloseLobbySearch);
        if (discoveredLobbyButtons != null)
        {
            for (int i = 0; i < discoveredLobbyButtons.Length; i++)
            {
                int lobbyIndex = i;
                if (discoveredLobbyButtons[i] != null)
                    discoveredLobbyButtons[i].onClick.AddListener(() => JoinDisplayedLobby(lobbyIndex));
            }
        }
        BindMapButton(classicMapButton, 0);
        BindMapButton(narrowMapButton, 1);
        if (mainMenuSoundSettings == null && soundVolumeSlider != null)
        {
            soundVolumeSlider.SetValueWithoutNotify(game.GetSoundVolume());
            soundVolumeSlider.onValueChanged.AddListener(game.SetSoundVolume);
        }
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            GameSound cue = button == connectionBackButton || button == settingsBackButton ||
                button == lobbySearchBackButton || button.name == "BACK" ? GameSound.Back :
                button == hostButton || button == clientButton || button == startGameButton ? GameSound.Confirm :
                GameSound.Click;
            button.onClick.AddListener(() => GameAudio.Instance?.Play(cue));
            button.gameObject.AddComponent<GameAudioHover>();
        }
    }

    void LateUpdate()
    {
        if (!connectedPanel.gameObject.activeSelf || Screen.width <= 0 || Screen.height <= 0) return;

        var canvasRect = (RectTransform)connectedPanel.parent;
        Vector2 canvasSize = canvasRect.rect.size;
        Rect safeArea = Screen.safeArea;
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        Vector2 safeSize = Vector2.Scale(canvasSize, safeArea.size / screenSize);
        Vector2 safeCenter = Vector2.Scale(canvasSize, safeArea.center / screenSize - new Vector2(0.5f, 0.5f));
        float scale = Mathf.Min(1f, Mathf.Min(
            (safeSize.x - 2f * LobbyEdgePadding) / LobbyDesignSize.x,
            (safeSize.y - 2f * LobbyEdgePadding) / LobbyDesignSize.y));
        Vector3 panelScale = Vector3.one * Mathf.Max(0.01f, scale);

        if (connectedPanel.anchoredPosition != safeCenter)
            connectedPanel.anchoredPosition = safeCenter;
        if (connectedPanel.localScale != panelScale)
            connectedPanel.localScale = panelScale;
    }

    void BindMapButton(Button button, int index)
    {
        if (button == null) return;
        button.onClick.AddListener(() => game.SelectMap(index));
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null) label.text = game.GetMapName(index);
    }

    void CreateLobbySearchPanel()
    {
        Image background = new GameObject("Lobby Search Panel", typeof(Image)).GetComponent<Image>();
        lobbySearchPanel = background.rectTransform;
        lobbySearchPanel.SetParent(lobbyPanel.parent, false);
        lobbySearchPanel.anchorMin = lobbySearchPanel.anchorMax = new Vector2(0.5f, 0.5f);
        lobbySearchPanel.sizeDelta = new Vector2(500f, 330f);
        background.color = new Color(0.025f, 0.05f, 0.085f);

        Text title = CreateLobbySearchLabel(lobbySearchPanel, "Title", 0.87f);
        title.text = "OPEN LOBBIES";
        title.fontStyle = FontStyle.Bold;
        lobbySearchStatusLabel = CreateLobbySearchLabel(lobbySearchPanel, "Search Status", 0.75f);
        discoveredLobbyButtons = new Button[4];
        discoveredLobbyLabels = new Text[4];
        for (int i = 0; i < discoveredLobbyButtons.Length; i++)
        {
            discoveredLobbyButtons[i] = CreateLobbySearchButton($"Lobby Result {i + 1}", 0.61f - i * 0.13f, out discoveredLobbyLabels[i]);
            discoveredLobbyButtons[i].gameObject.SetActive(false);
        }

        lobbySearchBackButton = CreateLobbySearchButton("Back", 0.12f, out Text backLabel);
        backLabel.text = "BACK";
    }

    void ConfigureDirectJoin()
    {
        if (ipField == null) return;

        lobbySearchPanel.sizeDelta = new Vector2(lobbySearchPanel.sizeDelta.x, 410f);
        int resultCount = discoveredLobbyButtons?.Length ?? 0;
        for (int i = 0; i < resultCount; i++)
        {
            if (discoveredLobbyButtons[i] != null)
                SetSearchControlRect((RectTransform)discoveredLobbyButtons[i].transform,
                    new Vector2(0, 40 - i * 44), new Vector2(410, 36));
        }
        if (lobbySearchBackButton != null)
            SetSearchControlRect((RectTransform)lobbySearchBackButton.transform,
                new Vector2(0, -180), new Vector2(180, 34));
        var address = (RectTransform)ipField.transform;
        address.SetParent(lobbySearchPanel, false);
        SetSearchControlRect(address, new Vector2(-72, -133), new Vector2(266, 36));
        ipField.contentType = InputField.ContentType.Standard;
        ipField.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
        ipField.SetTextWithoutNotify("");
        if (ipField.placeholder is Text placeholder)
            placeholder.text = "Host IPv4 address";
        ipField.gameObject.SetActive(true);

        Button direct = CreateLobbySearchButton("Join by IP", 0.5f, out Text label);
        label.text = "JOIN BY IP";
        SetSearchControlRect((RectTransform)direct.transform, new Vector2(138, -133), new Vector2(134, 36));
        direct.onClick.AddListener(() =>
        {
            game.StopLobbySearch();
            showLobbySearch = false;
            game.JoinGame(ipField.text);
        });
    }

    static void SetSearchControlRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    Button CreateLobbySearchButton(string objectName, float verticalAnchor, out Text label)
    {
        Image image = new GameObject(objectName, typeof(Image), typeof(Button)).GetComponent<Image>();
        RectTransform rect = image.rectTransform;
        rect.SetParent(lobbySearchPanel, false);
        rect.anchorMin = new Vector2(0.25f, verticalAnchor - 0.055f);
        rect.anchorMax = new Vector2(0.75f, verticalAnchor + 0.055f);
        rect.sizeDelta = Vector2.zero;
        image.color = new Color(0.12f, 0.32f, 0.55f);
        Button button = image.GetComponent<Button>();
        button.targetGraphic = image;
        label = CreateLobbySearchLabel(rect, "Label", 0.5f);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.sizeDelta = Vector2.zero;
        return button;
    }

    Text CreateLobbySearchLabel(RectTransform parent, string objectName, float verticalAnchor)
    {
        Text label = new GameObject(objectName, typeof(Text)).GetComponent<Text>();
        RectTransform rect = label.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.05f, verticalAnchor - 0.06f);
        rect.anchorMax = new Vector2(0.95f, verticalAnchor + 0.06f);
        rect.sizeDelta = Vector2.zero;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 12;
        label.resizeTextMaxSize = 22;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.supportRichText = false;
        label.raycastTarget = false;
        return label;
    }

    void JoinDisplayedLobby(int index)
    {
        if (displayedLobbies == null || index >= displayedLobbies.Count) return;
        showLobbySearch = false;
        game.JoinDiscoveredLobby(displayedLobbies[index]);
    }

    void ShowConnectionScreen()
    {
        showConnectionScreen = true;
        showSettingsScreen = false;
        showLobbySearch = false;
    }

    void ShowSettingsScreen()
    {
        showConnectionScreen = false;
        showSettingsScreen = true;
        showLobbySearch = false;
    }

    void ReturnToMainMenu()
    {
        showConnectionScreen = false;
        showSettingsScreen = false;
        showLobbySearch = false;
        game?.StopLobbySearch();
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void HostClicked()
    {
        game.SetPlayerName(playerNameField.text);
        game.StartHost();
    }

    void JoinClicked()
    {
        game.SetPlayerName(playerNameField.text);
        showLobbySearch = true;
        game.FindLobbies();
    }

    void CloseLobbySearch()
    {
        showLobbySearch = false;
        game?.StopLobbySearch();
    }

    void Update()
    {
        if (game == null) return;

        bool connected = game.networkManager.IsHost || game.networkManager.IsConnectedClient;
        if (connected)
        {
            showConnectionScreen = false;
            showSettingsScreen = false;
            if (showLobbySearch) game.StopLobbySearch();
            showLobbySearch = false;
        }

        mainMenuPanel.gameObject.SetActive(!connected && !showConnectionScreen && !showSettingsScreen);
        bool choosingConnection = !connected && showConnectionScreen;
        lobbyPanel.gameObject.SetActive(choosingConnection && !showLobbySearch);
        bool settingsVisible = !connected && showSettingsScreen;
        if (settingsVisible && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            ReturnToMainMenu();
            settingsVisible = false;
        }
        if (settingsPanel.gameObject.activeSelf != settingsVisible)
        {
            settingsPanel.gameObject.SetActive(settingsVisible);
            if (settingsVisible && mainMenuSoundSettings != null)
            {
                mainMenuSoundSettings.Refresh(game.GetSoundVolume());
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(mainMenuSoundSettings.Slider.gameObject);
            }
        }
        if (lobbySearchPanel != null)
            lobbySearchPanel.gameObject.SetActive(choosingConnection && showLobbySearch);
        MatchController match = game.GetMatchController();
        int phase = connected && match != null ? match.Phase.Value : MatchController.WaitingPhase;
        bool playing = phase == MatchController.PlayingPhase || phase == MatchController.CountdownPhase;
        bool finished = connected && match != null && match.Phase.Value == MatchController.FinishedPhase;
        bool lobby = connected && match != null && match.Phase.Value == MatchController.WaitingPhase;
        if (GameAudio.Instance != null)
        {
            if (lobby)
            {
                int roster = match.GetRosterCount();
                if (roster != previousLobbyRoster)
                    GameAudio.Instance.Play(roster > previousLobbyRoster ? GameSound.LobbyJoin : GameSound.LobbyLeave);
                previousLobbyRoster = roster;
            }
            else if (!connected)
                previousLobbyRoster = 0;
            GameAudio.Instance.UpdateMatchAudio(phase, game.GetLocalPlayer()?.Team.Value ?? 0,
                match?.BlueScore.Value ?? 0, match?.OrangeScore.Value ?? 0);
        }
        lobbyBackground.gameObject.SetActive(!playing && !finished);
        connectedPanel.gameObject.SetActive(connected && !playing && !finished);
        fillPlayersButton.gameObject.SetActive(lobby && game.IsHost());
        removeAIButton.gameObject.SetActive(lobby && game.IsHost());
        startGameButton.gameObject.SetActive(lobby && game.IsHost() && match.RosterFull.Value && match.EveryoneReady.Value);
        UpdateMapButtons(lobby, match);
        if (gameplayHud != null)
            gameplayHud.SetVisible(playing || finished);
        messageLabel.text = string.IsNullOrEmpty(game.GetMessage()) ? "Required to join a game." : game.GetMessage();
        connectionLabel.text = game.GetConnectionStatus();
        if (ipLabel != null && (ipField == null || ipLabel != ipField.textComponent))
            ipLabel.text = "Find open games on the same Wi-Fi";
        blueTeamHeaderLabel.text = "<b>BLUE TEAM</b>";
        orangeTeamHeaderLabel.text = "<b>ORANGE TEAM</b>";
        int blueCount = game.GetTeamCount(1);
        int orangeCount = game.GetTeamCount(2);
        UpdateTeamCountImage(blueTeamCountImage, blueCount, blueTeamCount0Sprite, blueTeamCount1Sprite, blueTeamCount2Sprite);
        UpdateTeamCountImage(orangeTeamCountImage, orangeCount, orangeTeamCount0Sprite, orangeTeamCount1Sprite, orangeTeamCount2Sprite);
        blueTeamRosterLabel.text = game.GetTeamSlot(1, 0);
        orangeTeamRosterLabel.text = game.GetTeamSlot(2, 0);
        blueTeamSecondSlotLabel.text = game.GetTeamSlot(1, 1);
        orangeTeamSecondSlotLabel.text = game.GetTeamSlot(2, 1);
        UpdateSlotImage(blueSlotRow1Image, blueCount >= 1);
        UpdateSlotImage(blueSlotRow2Image, blueCount >= 2);
        UpdateSlotImage(orangeSlotRow1Image, orangeCount >= 1);
        UpdateSlotImage(orangeSlotRow2Image, orangeCount >= 2);
        blueTeamButton.interactable = game.CanChooseTeam();
        orangeTeamButton.interactable = game.CanChooseTeam();
        readyButton.interactable = game.CanChooseTeam();
        bool isReady = game.IsLocalPlayerReady();
        Image readyImage = readyButton.GetComponent<Image>();
        if (readyImage != null)
            readyImage.sprite = isReady ? readyCancelSprite : readyButtonSprite;
        readyLabel.enabled = isReady;
        readyLabel.text = "CANCEL READY";
        UpdateLobbySearch();
        if (mainMenuSoundSettings == null && soundVolumeLabel != null)
            soundVolumeLabel.text = $"SOUND  {Mathf.RoundToInt(game.GetSoundVolume() * 100f)}%";
    }

    void UpdateLobbySearch()
    {
        if (lobbySearchPanel == null || !showLobbySearch) return;
        displayedLobbies = game.GetDiscoveredLobbies();
        int count = discoveredLobbyButtons != null ? discoveredLobbyButtons.Length : 0;
        for (int i = 0; i < count; i++)
        {
            bool found = i < displayedLobbies.Count;
            if (discoveredLobbyButtons[i] != null)
            {
                discoveredLobbyButtons[i].gameObject.SetActive(found);
                discoveredLobbyButtons[i].interactable = found;
            }
            if (discoveredLobbyLabels != null && i < discoveredLobbyLabels.Length && discoveredLobbyLabels[i] != null && found)
                discoveredLobbyLabels[i].text = $"{displayedLobbies[i].hostName}     {displayedLobbies[i].playerCount}/{displayedLobbies[i].playerLimit} PLAYERS";
        }
        if (lobbySearchStatusLabel != null)
            lobbySearchStatusLabel.text = displayedLobbies.Count > 0 ? "OPEN LOBBIES ON THIS WI-FI" : game.IsSearchingForLobbies() ? "SEARCHING FOR OPEN LOBBIES..." : "NO OPEN LOBBIES FOUND";
    }

    void UpdateMapButtons(bool lobby, MatchController match)
    {
        if (match == null) return;
        int selected = match.MapChoice.Value;
        bool hostCanChoose = lobby && game.IsHost();
        if (classicMapButton != null)
        {
            classicMapButton.interactable = hostCanChoose;
            Image image = classicMapButton.GetComponent<Image>();
            if (image != null) image.color = selected == 0 ? classicMapSelectedColor : mapUnselectedColor;
        }
        if (narrowMapButton != null)
        {
            narrowMapButton.interactable = hostCanChoose;
            Image image = narrowMapButton.GetComponent<Image>();
            if (image != null) image.color = selected == 1 ? narrowMapSelectedColor : mapUnselectedColor;
        }
        if (mapStatusLabel != null && shownMap != selected)
        {
            shownMap = selected;
            mapStatusLabel.text = $"{game.GetMapName(selected).ToUpperInvariant()} SELECTED";
        }
    }

    void UpdateSlotImage(Image image, bool occupied)
    {
        if (image == null) return;
        Sprite sprite = occupied ? filledPlayerSprite : unfilledPlayerSprite;
        if (sprite != null && image.sprite != sprite) image.sprite = sprite;
    }

    void UpdateTeamCountImage(Image image, int count, Sprite zero, Sprite one, Sprite two)
    {
        if (image == null) return;
        image.sprite = count >= 2 ? two : count == 1 ? one : zero;
        image.color = Color.white;
    }
}
