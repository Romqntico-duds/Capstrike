using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Components;

public static class CapStrikeGameplaySetup
{
    const string BallPath = "Assets/Prefabs/GameBall.prefab";
    const string GameUiPath = "Assets/UI/UI Prefabs/GameUI.prefab";
    const string HudPath = "Assets/UI/UI Prefabs/GameplayHud.prefab";
    const string DriveAssetsPath = "Assets/UI/Drive";

    [MenuItem("CapStrike/Legacy Setup/Build Gameplay Assets")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        TextureImporter ballImporter = AssetImporter.GetAtPath("Assets/UI/BallSprite.png") as TextureImporter;
        if (ballImporter != null && (ballImporter.textureType != TextureImporterType.Sprite || ballImporter.spriteImportMode != SpriteImportMode.Single))
        {
            ballImporter.textureType = TextureImporterType.Sprite;
            ballImporter.spriteImportMode = SpriteImportMode.Single;
            ballImporter.spritePixelsPerUnit = 100;
            ballImporter.SaveAndReimport();
        }
        EnsureFolder("Assets/Prefabs");
        GameBall ball = CreateBall();
        if (AssetDatabase.LoadAssetAtPath<GameplayHud>(HudPath) == null)
            CreateHud();
        UpdatePlayerPrefab(ball);
        UpdateUiPrefab();
        UpdateMainScene(ball);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CapStrike gameplay assets are ready.");
    }

    [MenuItem("CapStrike/Legacy Setup/Integrate Shared Drive Assets")]
    public static void IntegrateSharedDriveAssets()
    {
        AssetDatabase.Refresh();
        ConfigureDriveAssetImporters();
        Sprite blueFront = LoadDriveSprite("BlueFront.png");
        Sprite orangeFront = LoadDriveSprite("OrangeFront.png");
        Sprite logo = LoadDriveSprite("Logo.png");
        Sprite entryPanel = LoadDriveSprite("EntryPanel.jpg");
        Sprite entryField = LoadDriveSprite("EntryField.png");
        Sprite hostButton = LoadDriveSprite("HostButton.png");
        Sprite joinButton = LoadDriveSprite("JoinButton.png");
        Sprite blueCard = LoadDriveSprite("BlueTeamCard.jpg");
        Sprite blueTeamButton = LoadDriveSprite("BlueTeamButton.jpg");
        Sprite blueSlot = LoadDriveSprite("BlueSlotIcon.jpg");
        Sprite orangeCard = LoadDriveSprite("OrangeTeamCard.jpg");
        Sprite orangeTeamButton = LoadDriveSprite("OrangeTeamButton.jpg");
        Sprite orangeSlot = LoadDriveSprite("OrangeSlotIcon.jpg");
        Sprite placeholder = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/UIWhite.png");

        UpdatePlayerAppearance(blueFront, orangeFront);
        UpdateBallPlaceholder(placeholder);
        UpdateUiAssetAssignments(logo, entryPanel, entryField, hostButton, joinButton, blueCard, blueTeamButton, blueSlot, orangeCard, orangeTeamButton, orangeSlot);
        UpdateJoystickPlaceholder();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Shared Drive artwork is assigned. Missing artwork uses simple placeholders.");
    }

    static void ConfigureDriveAssetImporters()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { DriveAssetsPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);
            importer.spritePixelsPerUnit = path.Contains("Front.png") || path.Contains("Facing") ? 2000f : 100f;
            importer.SaveAndReimport();
        }
    }

    static Sprite LoadDriveSprite(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{DriveAssetsPath}/{fileName}");
    }

    static void UpdatePlayerAppearance(Sprite blueFront, Sprite orangeFront)
    {
        if (!System.IO.File.Exists("Assets/Player.prefab")) return;
        GameObject player = PrefabUtility.LoadPrefabContents("Assets/Player.prefab");
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.blueFrontSprite = blueFront;
            movement.orangeFrontSprite = orangeFront;
            EditorUtility.SetDirty(movement);
            PrefabUtility.SaveAsPrefabAsset(player, "Assets/Player.prefab");
        }
        PrefabUtility.UnloadPrefabContents(player);
    }

    static void UpdateBallPlaceholder(Sprite placeholder)
    {
        if (placeholder == null || !System.IO.File.Exists(BallPath)) return;
        GameObject ball = PrefabUtility.LoadPrefabContents(BallPath);
        SpriteRenderer renderer = ball.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            AssignBallArtwork(ball.GetComponent<GameBall>(), renderer, placeholder);
            EditorUtility.SetDirty(renderer);
            PrefabUtility.SaveAsPrefabAsset(ball, BallPath);
        }
        PrefabUtility.UnloadPrefabContents(ball);
    }

    static void UpdateUiAssetAssignments(Sprite logo, Sprite entryPanel, Sprite entryField, Sprite hostButton, Sprite joinButton, Sprite blueCard, Sprite blueTeamButton, Sprite blueSlot, Sprite orangeCard, Sprite orangeTeamButton, Sprite orangeSlot)
    {
        if (!System.IO.File.Exists(GameUiPath)) return;
        GameObject ui = PrefabUtility.LoadPrefabContents(GameUiPath);
        GameUI gameUi = ui.GetComponent<GameUI>();
        if (gameUi != null)
        {
            Sprite slotBackground = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/LocalPNG/Unfilled Player.png");
            Color slotColor = Color.white;
            gameUi.blueSlotRow1Image = SetUiImage(ui.transform, "Blue Slot Row 1", slotBackground, slotColor);
            gameUi.blueSlotRow2Image = SetUiImage(ui.transform, "Blue Slot Row 2", slotBackground, slotColor);
            gameUi.orangeSlotRow1Image = SetUiImage(ui.transform, "Orange Slot Row 1", slotBackground, slotColor);
            gameUi.orangeSlotRow2Image = SetUiImage(ui.transform, "Orange Slot Row 2", slotBackground, slotColor);
            gameUi.unfilledPlayerSprite = slotBackground;
            gameUi.filledPlayerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/LocalPNG/Filled Player.png");
        }

        SetUiImage(ui.transform, "Lobby Arena Background", null, new Color(0.018f, 0.045f, 0.085f, 1f));
        SetUiImage(ui.transform, "Lobby Panel", null, new Color(0f, 0f, 0f, 0f));
        SetUiImage(ui.transform, "Team Lobby Panel", null, new Color(0f, 0f, 0f, 0.32f));
        SetUiImage(ui.transform, "Main Menu Screen", null, new Color(0.015f, 0.035f, 0.07f, 0.94f));
        SetUiImage(ui.transform, "Settings Screen", null, new Color(0.015f, 0.035f, 0.07f, 0.96f));
        SetUiImage(ui.transform, "Join Panel Frame", entryPanel, Color.white);
        SetUiImage(ui.transform, "CapStrike Logo", logo, Color.white, true);
        SetUiImage(ui.transform, "Connection Screen Logo", logo, Color.white, true);
        SetUiImage(ui.transform, "Player Name Field", entryField, new Color(0.12f, 0.17f, 0.24f, 1f));
        SetUiImage(ui.transform, "Host IP Field", entryField, new Color(0.12f, 0.17f, 0.24f, 1f));
        SetUiImage(ui.transform, "Host Button", hostButton, Color.white);
        SetUiImage(ui.transform, "Client Button", joinButton, Color.white);
        SetUiImage(ui.transform, "Blue Team Card", blueCard, new Color(0.08f, 0.15f, 0.25f, 1f));
        SetUiImage(ui.transform, "Orange Team Card", orangeCard, new Color(0.16f, 0.1f, 0.07f, 1f));
        SetUiImage(ui.transform, "Blue Team Button", blueTeamButton, Color.white);
        SetUiImage(ui.transform, "Orange Team Button", orangeTeamButton, Color.white);
        SetUiImage(ui.transform, "Blue Team Emblem", AssetDatabase.LoadAssetAtPath<Sprite>($"{DriveAssetsPath}/BlueFront.png"), Color.white, true);
        SetUiImage(ui.transform, "Orange Team Emblem", AssetDatabase.LoadAssetAtPath<Sprite>($"{DriveAssetsPath}/OrangeFront.png"), Color.white, true);
        SetUiImage(ui.transform, "Player Slot Icon 8340", blueSlot, Color.white, true);
        SetUiImage(ui.transform, "Player Slot Icon 8350", blueSlot, Color.white, true);
        SetUiImage(ui.transform, "Player Slot Icon 8360", orangeSlot, Color.white, true);
        SetUiImage(ui.transform, "Player Slot Icon 8370", orangeSlot, Color.white, true);
        SetUiImage(ui.transform, "Fill Players Button", blueTeamButton, Color.white);
        SetUiImage(ui.transform, "Remove AI Button", orangeTeamButton, Color.white);
        SetUiImage(ui.transform, "Start Game Button", hostButton, Color.white);
        SetUiImage(ui.transform, "Ready Button", hostButton, Color.white);
        SetUiImage(ui.transform, "Disconnect Button", null, new Color(0.52f, 0.12f, 0.12f, 1f));
        SetUiImage(ui.transform, "Disconnect Inset", null, new Color(0.18f, 0.07f, 0.08f, 1f));
        SetUiImage(ui.transform, "PLAY", hostButton, Color.white);
        SetUiImage(ui.transform, "SETTINGS", blueTeamButton, Color.white);
        SetUiImage(ui.transform, "QUIT", orangeTeamButton, Color.white);
        SetUiImage(ui.transform, "SETTINGS BACK", hostButton, Color.white);
        SetUiImage(ui.transform, "CONNECTION BACK", hostButton, Color.white);
        SetUiImage(ui.transform, "Settings Gear", null, new Color(0f, 0f, 0f, 0f));
        SetUiImage(ui.transform, "Quit Door Icon", null, new Color(0f, 0f, 0f, 0f));
        if (gameUi != null) EditorUtility.SetDirty(gameUi);
        PrefabUtility.SaveAsPrefabAsset(ui, GameUiPath);
        PrefabUtility.UnloadPrefabContents(ui);
    }

    static Image SetUiImage(Transform root, string objectName, Sprite sprite, Color color, bool preserveAspect = false)
    {
        Transform target = FindChildByName(root, objectName);
        if (target == null) return null;
        Image image = target.GetComponent<Image>();
        if (image == null) image = target.GetComponentInChildren<Image>(true);
        if (image == null) return null;
        if (sprite == null && color.a > 0f)
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/UIWhite.png");
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = preserveAspect;
        image.type = Image.Type.Simple;
        EditorUtility.SetDirty(image);
        return image;
    }

    static Transform FindChildByName(Transform root, string objectName)
    {
        foreach (Transform child in root)
        {
            if (child.name == objectName) return child;
            Transform nested = FindChildByName(child, objectName);
            if (nested != null) return nested;
        }
        return null;
    }

    static void UpdateJoystickPlaceholder()
    {
        const string path = "Assets/UI/UI Prefabs/VirtualJoystick.prefab";
        Sprite placeholder = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/JoystickCircle.png");
        if (placeholder == null || !System.IO.File.Exists(path)) return;
        GameObject joystick = PrefabUtility.LoadPrefabContents(path);
        foreach (string imageName in new[] { "Background", "Handle" })
        {
            Transform target = FindChildByName(joystick.transform, imageName);
            Image image = target != null ? target.GetComponent<Image>() : null;
            if (image == null) continue;
            image.sprite = placeholder;
            EditorUtility.SetDirty(image);
        }
        PrefabUtility.SaveAsPrefabAsset(joystick, path);
        PrefabUtility.UnloadPrefabContents(joystick);
    }

    static GameBall CreateBall()
    {
        GameObject ballObject = new GameObject("Game Ball");
        ballObject.transform.localScale = Vector3.one * 0.28f;
        SpriteRenderer renderer = ballObject.AddComponent<SpriteRenderer>();
        Sprite placeholder = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/UIWhite.png");
        if (placeholder == null)
            throw new System.InvalidOperationException("Assets/UI/UIWhite.png is required as the ball placeholder.");
        renderer.sortingOrder = 3;
        ballObject.AddComponent<NetworkObject>();
        ballObject.AddComponent<NetworkTransform>();
        GameBall ball = ballObject.AddComponent<GameBall>();
        AssignBallArtwork(ball, renderer, placeholder);
        PrefabUtility.SaveAsPrefabAsset(ballObject, BallPath);
        Object.DestroyImmediate(ballObject);
        return AssetDatabase.LoadAssetAtPath<GameBall>(BallPath);
    }

    static void AssignBallArtwork(GameBall ball, SpriteRenderer renderer, Sprite placeholder)
    {
        var serializedBall = new SerializedObject(ball);
        SerializedProperty frames = serializedBall.FindProperty("rollFrames");
        frames.arraySize = 3;
        for (int index = 0; index < frames.arraySize; index++)
            frames.GetArrayElementAtIndex(index).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/UI/LocalPNG/Ball/{index + 1}.png");
        Sprite firstFrame = frames.GetArrayElementAtIndex(0).objectReferenceValue as Sprite;
        serializedBall.ApplyModifiedPropertiesWithoutUndo();
        renderer.sprite = firstFrame != null ? firstFrame : placeholder;
        renderer.color = firstFrame != null ? Color.white : new Color(1f, 0.86f, 0.2f, 1f);
    }

    static void UpdatePlayerPrefab(GameBall ball)
    {
        GameObject player = PrefabUtility.LoadPrefabContents("Assets/Player.prefab");
        MatchController controller = player.GetComponent<MatchController>();
        if (controller == null) controller = player.AddComponent<MatchController>();
        controller.ballPrefab = ball;
        PrefabUtility.SaveAsPrefabAsset(player, "Assets/Player.prefab");
        PrefabUtility.UnloadPrefabContents(player);
    }

    static void UpdateUiPrefab()
    {
        GameObject ui = PrefabUtility.LoadPrefabContents(GameUiPath);
        GameUI gameUi = ui.GetComponent<GameUI>();
        gameUi.lobbyBackground = ui.transform.Find("Lobby Arena Background").GetComponent<RectTransform>();
        gameUi.gameplayHudPrefab = AssetDatabase.LoadAssetAtPath<GameplayHud>(HudPath);
        Transform fillButtonTransform = ui.transform.Find("Connected Panel/Fill Players Button");
        if (fillButtonTransform == null)
            fillButtonTransform = MakeButton("Fill Players Button", gameUi.connectedPanel, "FILL PLAYERS", new Vector2(0.5f, 0.5f), new Vector2(-194, -165), new Vector2(176, 48), Color.white).transform;
        gameUi.fillPlayersButton = fillButtonTransform.GetComponent<Button>();
        ConfigureLobbyButton(gameUi.fillPlayersButton, new Vector2(-194, -165), new Vector2(176, 48), "Assets/UI/Drive/BlueTeamButton.jpg");

        Transform removeButtonTransform = ui.transform.Find("Connected Panel/Remove AI Button");
        if (removeButtonTransform == null)
            removeButtonTransform = MakeButton("Remove AI Button", gameUi.connectedPanel, "REMOVE AI", new Vector2(0.5f, 0.5f), new Vector2(0, -165), new Vector2(176, 48), Color.white).transform;
        gameUi.removeAIButton = removeButtonTransform.GetComponent<Button>();
        ConfigureLobbyButton(gameUi.removeAIButton, new Vector2(0, -165), new Vector2(176, 48), "Assets/UI/Drive/OrangeTeamButton.jpg");

        Transform startButtonTransform = ui.transform.Find("Connected Panel/Start Game Button");
        if (startButtonTransform == null)
            startButtonTransform = MakeButton("Start Game Button", gameUi.connectedPanel, "START GAME", new Vector2(0.5f, 0.5f), new Vector2(194, -165), new Vector2(176, 48), Color.white).transform;
        gameUi.startGameButton = startButtonTransform.GetComponent<Button>();
        ConfigureLobbyButton(gameUi.startGameButton, new Vector2(194, -165), new Vector2(176, 48), "Assets/UI/Drive/HostButton.png");
        RectTransform readyRect = gameUi.readyButton.GetComponent<RectTransform>();
        readyRect.anchoredPosition = new Vector2(0, -226);
        readyRect.sizeDelta = new Vector2(248, 52);
        RectTransform blueCard = ui.transform.Find("Connected Panel/Blue Team Card").GetComponent<RectTransform>();
        RectTransform orangeCard = ui.transform.Find("Connected Panel/Orange Team Card").GetComponent<RectTransform>();
        blueCard.anchoredPosition = new Vector2(-185, 15);
        orangeCard.anchoredPosition = new Vector2(185, 15);
        blueCard.sizeDelta = new Vector2(360, 270);
        orangeCard.sizeDelta = new Vector2(360, 270);
        RectTransform blueJoinRect = gameUi.blueTeamButton.GetComponent<RectTransform>();
        RectTransform orangeJoinRect = gameUi.orangeTeamButton.GetComponent<RectTransform>();
        blueJoinRect.anchoredPosition = new Vector2(-185, -90);
        orangeJoinRect.anchoredPosition = new Vector2(185, -90);
        blueJoinRect.sizeDelta = new Vector2(300, 56);
        orangeJoinRect.sizeDelta = new Vector2(300, 56);
        PrefabUtility.SaveAsPrefabAsset(ui, GameUiPath);
        PrefabUtility.UnloadPrefabContents(ui);
    }

    static void ConfigureLobbyButton(Button button, Vector2 position, Vector2 size, string spritePath)
    {
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = button.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        image.color = Color.white;
        image.preserveAspect = true;
    }

    static void UpdateMainScene(GameBall ball)
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        Game game = Object.FindFirstObjectByType<Game>();
        if (game != null) game.ballPrefab = ball;
        Transform court = GameObject.Find("Court")?.transform;
        if (court != null)
        {
            Transform courtArtwork = FindOrCreatePoint(court, "Court Artwork", new Vector3(0, 0, -1));
            SpriteRenderer courtRenderer = courtArtwork.GetComponent<SpriteRenderer>();
            if (courtRenderer == null) courtRenderer = courtArtwork.gameObject.AddComponent<SpriteRenderer>();
            if (courtRenderer.sprite == null)
                courtRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Court.png");
            Transform ballSpawn = FindOrCreatePoint(court, "Ball Spawnpoint", new Vector3(0, 0, -1));
            Transform bluePlayer1Spawn = FindOrCreatePoint(court, "1BlueSpawnPoint", new Vector3(-0.06f, 0.09f, -1));
            Transform bluePlayer2Spawn = FindOrCreatePoint(court, "2BlueSpawnPoint", new Vector3(-0.16f, -0.12f, -1));
            Transform orangePlayer1Spawn = FindOrCreatePoint(court, "1OrangeSpawnPoint", new Vector3(0.08f, -0.1f, -1));
            Transform orangePlayer2Spawn = FindOrCreatePoint(court, "2OrangeSpawnPoint", new Vector3(0.18f, 0.12f, -1));
            CourtReferences references = court.GetComponent<CourtReferences>();
            if (references == null) references = court.gameObject.AddComponent<CourtReferences>();
            references.courtArtwork = courtRenderer;
            references.ballSpawnPoint = ballSpawn;
            references.bluePlayer1SpawnPoint = bluePlayer1Spawn;
            references.bluePlayer2SpawnPoint = bluePlayer2Spawn;
            references.orangePlayer1SpawnPoint = orangePlayer1Spawn;
            references.orangePlayer2SpawnPoint = orangePlayer2Spawn;
            references.blueGoal = court.Find("Blue Goal");
            references.orangeGoal = court.Find("Orange Goal");
            EditorUtility.SetDirty(references);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static Transform FindOrCreatePoint(Transform parent, string name, Vector3 localPosition)
    {
        Transform point = parent.Find(name);
        if (point != null) return point;
        GameObject pointObject = new GameObject(name);
        pointObject.transform.SetParent(parent, false);
        pointObject.transform.localPosition = localPosition;
        return pointObject.transform;
    }

    static void CreateHud()
    {
        GameObject root = new GameObject("Gameplay HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.localScale = Vector3.one;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 600);
        scaler.matchWidthOrHeight = 0.5f;

        GameplayHud hud = root.AddComponent<GameplayHud>();
        GameObject scorePanel = MakePanel("Score Panel", root.transform, new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(520, 92), new Color(0.035f, 0.07f, 0.12f, 0.94f));
        hud.scoreboardArtwork = scorePanel;
        hud.scoreLabel = MakeText("Score", scorePanel.transform, "BLUE  0  —  0  ORANGE", 27, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0, 19), new Vector2(500, 40));
        hud.clockLabel = MakeText("Clock", scorePanel.transform, "03:00", 22, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0, -12), new Vector2(140, 30));
        hud.phaseLabel = MakeText("Phase", scorePanel.transform, "CHOOSE TEAMS AND READY UP", 14, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0, -34), new Vector2(480, 24));

        hud.shootButton = MakeButton("Shoot", root.transform, "SHOOT\nSPACE", new Vector2(1, 0), new Vector2(-74, 96), new Vector2(132, 72), new Color(0.02f, 0.45f, 0.95f, 1));
        hud.passButton = MakeButton("Pass", root.transform, "PASS\nE", new Vector2(1, 0), new Vector2(-220, 96), new Vector2(132, 72), new Color(0.95f, 0.36f, 0.04f, 1));
        hud.tackleButton = MakeButton("Tackle", root.transform, "TACKLE\nQ", new Vector2(1, 0), new Vector2(-366, 96), new Vector2(132, 72), new Color(0.12f, 0.78f, 0.33f, 1));
        hud.pauseButton = MakeButton("Pause", root.transform, "Ⅱ", new Vector2(1, 1), new Vector2(-38, -32), new Vector2(48, 48), new Color(0.035f, 0.10f, 0.18f, 1f));
        GameObject result = MakePanel("Match Result Panel", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 350), new Color(0.025f, 0.045f, 0.08f, 0.97f));
        hud.resultPanel = result.GetComponent<RectTransform>();
        hud.resultLabel = MakeText("Match Result", result.transform, "BLUE TEAM WINS\nMVP: Player\nGoals 1  Assists 0  Tackles 2", 18, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0, 30), new Vector2(560, 268));
        hud.returnToLobbyButton = MakeButton("Back to Lobby", result.transform, "BACK TO LOBBY", new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(224, 48), new Color(0.07f, 0.32f, 0.52f, 1));

        PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        Object.DestroyImmediate(root);
    }

    static GameObject MakePanel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = color;
        panel.GetComponent<Image>().raycastTarget = false;
        return panel;
    }

    static Text MakeText(string name, Transform parent, string value, int size, FontStyle style, TextAnchor alignment, Vector2 position, Vector2 rectSize)
    {
        GameObject label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        label.transform.SetParent(parent, false);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = rectSize;
        Text text = label.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static Button MakeButton(string name, Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        MakeText("Label", buttonObject.transform, label, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, size - new Vector2(12, 8));
        return button;
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder("Assets", path.Substring("Assets/".Length));
    }
}
