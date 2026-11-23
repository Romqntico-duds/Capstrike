using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CapStrikeDecisionIntegration
{
    const string GameUiPath = "Assets/UI/UI Prefabs/GameUI.prefab";
    const string PlayerPath = "Assets/Player.prefab";
    const string MainScenePath = "Assets/Scenes/Main.unity";
    const string WhitePath = "Assets/UI/UIWhite.png";
    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    [MenuItem("CapStrike/Legacy Setup/Integrate Game Decisions")]
    public static void Apply()
    {
        ConfigureIdentity();
        ConfigurePlayerPrefab();
        ConfigureMainScene();
        ConfigureGameUi();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CapStrike integration applied.");
    }

    static void ConfigureIdentity()
    {
        PlayerSettings.productName = "CapStrike";
        PlayerSettings.companyName = "CapStrike";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.capstrike.game");
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.capstrike.game");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
    }

    static void ConfigurePlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        if (root == null) return;
        PlayerMovement movement = root.GetComponent<PlayerMovement>();
        SpriteRenderer original = root.GetComponentInChildren<SpriteRenderer>(true);
        if (movement != null && original != null)
        {
            Transform visual = root.transform.Find("Visual");
            if (visual == null)
            {
                GameObject visualObject = new GameObject("Visual", typeof(Transform));
                visualObject.transform.SetParent(root.transform, false);
                visual = visualObject.transform;
                original.transform.SetParent(visual, false);
                original.transform.localPosition = Vector3.zero;
                original.transform.localRotation = Quaternion.identity;
                original.transform.localScale = Vector3.one;
            }
            movement.blueFrontSprite = LoadSprite("Assets/UI/Drive/BlueFront.png");
            movement.blueFacingLeftSprite = LoadSprite("Assets/UI/Drive/BlueFacingLeft.png");
            movement.blueFacingRightSprite = LoadSprite("Assets/UI/Drive/BlueFacingRight.png");
            movement.orangeFrontSprite = LoadSprite("Assets/UI/Drive/OrangeFront.png");
            movement.orangeFacingLeftSprite = LoadSprite("Assets/UI/Drive/OrangeFacingLeft.png");
            movement.orangeFacingRightSprite = LoadSprite("Assets/UI/Drive/OrangeFacingRight.png");
            movement.movementAnimationSpeed = 12f;
            movement.movementAnimationHeight = 0.035f;
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true)) animator.enabled = true;
            foreach (Animation animation in root.GetComponentsInChildren<Animation>(true)) animation.enabled = true;
            EditorUtility.SetDirty(movement);
        }
        PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void ConfigureMainScene()
    {
        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        bool openedForIntegration = !scene.IsValid() || !scene.isLoaded;
        if (openedForIntegration)
            scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        GameObject court = GameObject.Find("Court");
        if (court != null)
        {
            CourtReferences references = court.GetComponent<CourtReferences>();
            if (references == null) references = court.AddComponent<CourtReferences>();
            Transform artwork = court.transform.Find("Court Artwork");
            if (artwork != null) references.courtArtwork = artwork.GetComponent<SpriteRenderer>();
            EditorUtility.SetDirty(references);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedForIntegration)
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }
    }

    static void ConfigureGameUi()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPath);
        if (root == null) return;
        GameUI controller = root.GetComponent<GameUI>();
        Transform connectionPanel = Find(root.transform, "Connection Panel");
        if (connectionPanel == null) connectionPanel = Find(root.transform, "Join Panel Frame");
        if (connectionPanel == null) connectionPanel = Find(root.transform, "Lobby Panel");
        Transform connected = Find(root.transform, "Team Lobby Panel");
        Transform settings = Find(root.transform, "Settings Frame");
        if (controller != null)
        {
            SetButtonLabel(root.transform, "Host Button", "CREATE LOBBY");
            SetButtonLabel(root.transform, "Client Button", "FIND LOBBY");
            if (connectionPanel != null)
            {
                GameObject search = CreatePanel(connectionPanel, "Lobby Search Panel", new Vector2(0, -28), new Vector2(500, 330), new Color(0.025f, 0.05f, 0.085f, 0.98f));
                Text title = CreateText(search.transform, "Search Title", "OPEN LOBBIES", 25, FontStyle.Bold, new Vector2(0, 112), new Vector2(420, 42));
                Text status = CreateText(search.transform, "Search Status", "SEARCHING FOR OPEN LOBBIES...", 14, FontStyle.Normal, new Vector2(0, 76), new Vector2(420, 26));
                Button[] rows = new Button[4];
                Text[] labels = new Text[4];
                for (int i = 0; i < rows.Length; i++)
                {
                    GameObject row = CreateButton(search.transform, "Lobby Result " + (i + 1), "Open CapStrike lobby", new Vector2(0, 40 - i * 44), new Vector2(410, 36), new Color(0.04f, 0.1f, 0.16f, 1f), 15);
                    rows[i] = row.GetComponent<Button>();
                    labels[i] = row.GetComponentInChildren<Text>();
                    row.SetActive(false);
                }
                GameObject back = CreateButton(search.transform, "Lobby Search Back", "BACK", new Vector2(0, -140), new Vector2(150, 40), new Color(0.05f, 0.35f, 0.7f), 16);
                controller.lobbySearchPanel = search.GetComponent<RectTransform>();
                controller.lobbySearchBackButton = back.GetComponent<Button>();
                controller.discoveredLobbyButtons = rows;
                controller.discoveredLobbyLabels = labels;
                controller.lobbySearchStatusLabel = status;
                SetInputLabel(root.transform, "Host Address Label", "LOCAL WI-FI LOBBIES");
                title.raycastTarget = false;
                controller.lobbySearchPanel.gameObject.SetActive(false);
            }
            if (connected != null)
            {
                GameObject mapPanel = CreatePanel(connected, "Map Selection", new Vector2(0, 160), new Vector2(410, 70), new Color(0.025f, 0.05f, 0.085f, 0.94f));
                CreateText(mapPanel.transform, "Map Label", "CHOOSE MAP", 14, FontStyle.Bold, new Vector2(-142, 12), new Vector2(115, 24));
                GameObject classic = CreateButton(mapPanel.transform, "Classic Map Button", "Normal", new Vector2(-38, 8), new Vector2(115, 34), new Color(0.03f, 0.35f, 0.72f), 13);
                GameObject narrow = CreateButton(mapPanel.transform, "Narrow Map Button", "Narrow", new Vector2(85, 8), new Vector2(100, 34), new Color(0.95f, 0.32f, 0.05f), 13);
                Text mapStatus = CreateText(mapPanel.transform, "Map Status", "NORMAL SELECTED", 11, FontStyle.Normal, new Vector2(0, -20), new Vector2(260, 18));
                controller.classicMapButton = classic.GetComponent<Button>();
                controller.narrowMapButton = narrow.GetComponent<Button>();
                controller.mapStatusLabel = mapStatus;
            }
            if (settings != null)
            {
                Text description = Find(settings, "Settings Description")?.GetComponent<Text>();
                if (description != null) description.text = "SOUND";
                Slider slider = CreateSlider(settings, "Sound Volume", new Vector2(0, -30), new Vector2(360, 24));
                Text volume = CreateText(settings, "Sound Volume Label", "SOUND  75%", 16, FontStyle.Bold, new Vector2(0, 12), new Vector2(300, 30));
                controller.soundVolumeSlider = slider;
                controller.soundVolumeLabel = volume;
            }
            EditorUtility.SetDirty(controller);
        }
        PrefabUtility.SaveAsPrefabAsset(root, GameUiPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void SetInputLabel(Transform root, string name, string value)
    {
        Transform target = Find(root, name);
        if (target == null) return;
        Text text = target.GetComponent<Text>();
        if (text != null) text.text = value;
    }

    static void SetButtonLabel(Transform root, string name, string value)
    {
        Transform target = Find(root, name);
        Text text = target != null ? target.GetComponentInChildren<Text>(true) : null;
        if (text != null) text.text = value;
    }

    static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject panel = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null) panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Center;
        rect.anchorMax = Center;
        rect.pivot = Center;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = panel.GetComponent<Image>();
        image.sprite = LoadSprite(WhitePath);
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    static GameObject CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color, int fontSize)
    {
        Transform existing = parent.Find(name);
        GameObject buttonObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        if (existing == null) buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = Center;
        rect.anchorMax = Center;
        rect.pivot = Center;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.sprite = LoadSprite(WhitePath);
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        Text text = buttonObject.GetComponentInChildren<Text>();
        if (text == null)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            text = labelObject.GetComponent<Text>();
        }
        RectTransform labelRect = text.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6, 2);
        labelRect.offsetMax = new Vector2(-6, -2);
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return buttonObject;
    }

    static Text CreateText(Transform parent, string name, string value, int fontSize, FontStyle style, Vector2 position, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        if (existing == null) textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Center;
        rect.anchorMax = Center;
        rect.pivot = Center;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static Slider CreateSlider(Transform parent, string name, Vector2 position, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject root = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Slider));
        if (existing == null) root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Center;
        rect.anchorMax = Center;
        rect.pivot = Center;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Slider slider = root.GetComponent<Slider>();
        GameObject track = CreatePanel(root.transform, "Track", Vector2.zero, size, new Color(0.08f, 0.13f, 0.19f));
        Transform existingFillArea = root.transform.Find("Fill Area");
        GameObject fillArea = existingFillArea != null ? existingFillArea.gameObject : new GameObject("Fill Area", typeof(RectTransform));
        if (existingFillArea == null) fillArea.transform.SetParent(root.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1, 0.75f);
        fillAreaRect.offsetMin = new Vector2(8, 0);
        fillAreaRect.offsetMax = new Vector2(-8, 0);
        GameObject fill = CreatePanel(fillArea.transform, "Fill", Vector2.zero, Vector2.zero, new Color(0.05f, 0.62f, 1f));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Transform existingHandleArea = root.transform.Find("Handle Slide Area");
        GameObject handleArea = existingHandleArea != null ? existingHandleArea.gameObject : new GameObject("Handle Slide Area", typeof(RectTransform));
        if (existingHandleArea == null) handleArea.transform.SetParent(root.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(8, 0);
        handleAreaRect.offsetMax = new Vector2(-8, 0);
        GameObject handle = CreatePanel(handleArea.transform, "Handle", Vector2.zero, new Vector2(20, 30), Color.white);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0, 0.5f);
        handleRect.anchorMax = new Vector2(0, 0.5f);
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 0.75f;
        slider.direction = Slider.Direction.LeftToRight;
        EditorUtility.SetDirty(track);
        return slider;
    }

    static Transform Find(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
