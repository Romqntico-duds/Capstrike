using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CapStrikePngUiSetup
{
    const string UiRoot = "Assets/UI/LocalPNG";
    const string GameUiPath = "Assets/UI/UI Prefabs/GameUI.prefab";
    const string HudPath = "Assets/UI/UI Prefabs/GameplayHud.prefab";
    const string WhitePath = "Assets/UI/UIWhite.png";

    [MenuItem("CapStrike/Legacy Setup/Apply Local PNG UI Layout")]
    public static void Apply()
    {
        AssetDatabase.Refresh();
        ConfigureSprites();
        ApplyGameUi();
        ApplyHud();
        ApplyJoystick();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CapStrike PNG UI assets and layout are ready.");
    }

    static void ConfigureSprites()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { UiRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single && importer.spritePixelsPerUnit == 100 && importer.alphaIsTransparency) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    static void ApplyGameUi()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameUiPath);
        if (root == null) return;
        Sprite blank = Load("Host_Game(button)-1.png");
        Sprite orangeBlank = Load("Join(button)-1.png");

        SetImage(root.transform, "Main Menu Screen", AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath), new Color(0.012f, 0.02f, 0.035f, 1f));
        SetImage(root.transform, "Lobby Arena Background", AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath), new Color(0.012f, 0.025f, 0.045f, 1f));
        SetImage(root.transform, "Join Panel Frame", AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath), new Color(0.035f, 0.055f, 0.085f, 0.98f));
        SetRect(root.transform, "Join Panel Frame", Center, Center, Vector2.zero, new Vector2(560, 405));
        SetImage(root.transform, "Blue Team Card", AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath), new Color(0.02f, 0.09f, 0.17f, 0.96f));
        SetImage(root.transform, "Orange Team Card", AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath), new Color(0.15f, 0.055f, 0.015f, 0.96f));
        SetImage(root.transform, "Team Lobby Panel", null, new Color(0f, 0f, 0f, 0f));
        SetRect(root.transform, "Team Lobby Panel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        SetImage(root.transform, "Settings Screen", AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath), new Color(0.012f, 0.02f, 0.035f, 1f));
        SetImage(root.transform, "Settings Frame", AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath), new Color(0.035f, 0.055f, 0.085f, 0.98f));
        SetRect(root.transform, "Settings Frame", Center, Center, Vector2.zero, new Vector2(520, 300));

        SetImage(root.transform, "CapStrike Logo", Load("CapStrikeLogo.png"), Color.white, true);
        SetRect(root.transform, "CapStrike Logo", new Vector2(0.25f, 0.5f), new Vector2(0.25f, 0.5f), new Vector2(0, 166), new Vector2(420, 72));
        HideChild(root.transform, "CapStrike Logo", "Text Overlay");
        SetMenuButton(root.transform, "PLAY", blank, "PLAY", "Maglaro.png", new Vector2(0.25f, 0.5f), new Vector2(0, 62), new Vector2(320, 70), new Vector2(165, 48));
        SetMenuButton(root.transform, "SETTINGS", blank, "SETTINGS", "Maglaro-2.png", new Vector2(0.25f, 0.5f), new Vector2(0, -27), new Vector2(320, 70), new Vector2(170, 34));
        SetMenuButton(root.transform, "QUIT", orangeBlank, "QUIT", "Maglaro-1.png", new Vector2(0.25f, 0.5f), new Vector2(0, -116), new Vector2(320, 70), new Vector2(98, 40));
        SetChildImage(root.transform, "SETTINGS", "Settings Gear", Load("Vector-1.png"), new Vector2(30, 32), new Vector2(-105, 0));
        SetChildImage(root.transform, "QUIT", "Quit Door Icon", Load("Vector.png"), new Vector2(30, 30), new Vector2(-90, 0));

        SetImage(root.transform, "Connection Screen Logo", Load("CapStrikeLogo.png"), Color.white, true);
        SetRect(root.transform, "Connection Screen Logo", Center, Center, new Vector2(0, 158), new Vector2(420, 64));
        HideChild(root.transform, "Connection Screen Logo", "Text Overlay");
        SetText(root.transform, "Title", "MULTIPLAYER", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(root.transform, "Title", Center, Center, new Vector2(0, 116), new Vector2(500, 38));
        SetText(root.transform, "Multiplayer Subtitle", "Enter your nickname and server address.", 15, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(root.transform, "Multiplayer Subtitle", Center, Center, new Vector2(0, 91), new Vector2(480, 24));
        SetRect(root.transform, "Player Name Label", Center, Center, new Vector2(-190, 68), new Vector2(340, 24));
        SetRect(root.transform, "Player Name Field", Center, Center, new Vector2(0, 34), new Vector2(440, 46));
        SetRect(root.transform, "Host Address Label", Center, Center, new Vector2(-190, -28), new Vector2(340, 24));
        SetRect(root.transform, "Host IP Field", Center, Center, new Vector2(0, -64), new Vector2(440, 46));
        SetRect(root.transform, "Status Message", Center, Center, new Vector2(0, -102), new Vector2(440, 22));
        SetImage(root.transform, "Player Name Field", Load("Enter_nickname-1.png"), Color.white, true);
        SetImage(root.transform, "Host IP Field", Load("Enter_IP-1.png"), Color.white, true);
        SetButton(root.transform, "Host Button", Load("Host_Game(button)-1.png"), "HOST GAME", Center, Center, new Vector2(-122, -145), new Vector2(210, 54), 17, false);
        SetButton(root.transform, "Client Button", Load("Join(button)-1.png"), "JOIN GAME", Center, Center, new Vector2(122, -145), new Vector2(210, 54), 17, false);
        SetButton(root.transform, "CONNECTION BACK", blank, "BACK", new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -34), new Vector2(88, 34), 15, false);

        SetText(root.transform, "Multiplayer Lobby Title", "MULTIPLAYER LOBBY", 27, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(root.transform, "Multiplayer Lobby Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(500, 42));
        SetText(root.transform, "Lobby Subtitle", "CHOOSE YOUR TEAM", 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(root.transform, "Lobby Subtitle", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -65), new Vector2(360, 24));
        SetImageOnTextObject(root.transform, "Connection Status", Load("Connected.png"), new Vector2(86, 20));
        SetRect(root.transform, "Connection Status", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-176, -28), new Vector2(90, 22));
        Hide(root.transform, "Disconnect Inset");
        SetButton(root.transform, "Disconnect Button", null, "DISCONNECT", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-68, -28), new Vector2(118, 34), 13, false);

        SetRect(root.transform, "Blue Team Card", Center, Center, new Vector2(-200, 0), new Vector2(365, 286));
        SetRect(root.transform, "Orange Team Card", Center, Center, new Vector2(200, 0), new Vector2(365, 286));
        SetRect(root.transform, "Blue Team Header", Center, Center, new Vector2(-202, 105), new Vector2(260, 38));
        SetRect(root.transform, "Orange Team Header", Center, Center, new Vector2(202, 105), new Vector2(260, 38));
        SetText(root.transform, "Blue Team Header", "<b>BLUE TEAM</b>", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetText(root.transform, "Orange Team Header", "<b>ORANGE TEAM</b>", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
        Hide(root.transform, "Blue Team Emblem");
        Hide(root.transform, "Orange Team Emblem");
        SetCountBadge(root.transform, "Blue Team Header", "Blue Team Count", Load("Slot(blue).png"));
        SetCountBadge(root.transform, "Orange Team Header", "Orange Team Count", Load("Slot(orange).png"));

        SetSlotRow(root.transform, "Blue Slot Row 1", new Vector2(-200, 48));
        SetSlotRow(root.transform, "Blue Slot Row 2", new Vector2(-200, -8));
        SetSlotRow(root.transform, "Orange Slot Row 1", new Vector2(200, 48));
        SetSlotRow(root.transform, "Orange Slot Row 2", new Vector2(200, -8));
        SetText(root.transform, "Blue Team Roster", "Open slot", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
        SetText(root.transform, "Blue Team Second Slot", "Open slot", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
        SetText(root.transform, "Orange Team Roster", "Open slot", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
        SetText(root.transform, "Orange Team Second Slot", "Open slot", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
        SetRect(root.transform, "Blue Team Roster", Center, Center, new Vector2(-200, 48), new Vector2(260, 32));
        SetRect(root.transform, "Blue Team Second Slot", Center, Center, new Vector2(-200, -8), new Vector2(260, 32));
        SetRect(root.transform, "Orange Team Roster", Center, Center, new Vector2(200, 48), new Vector2(260, 32));
        SetRect(root.transform, "Orange Team Second Slot", Center, Center, new Vector2(200, -8), new Vector2(260, 32));
        Hide(root.transform, "Player Slot Icon 8340");
        Hide(root.transform, "Player Slot Icon 8350");
        Hide(root.transform, "Player Slot Icon 8360");
        Hide(root.transform, "Player Slot Icon 8370");
        Hide(root.transform, "Player Slot Divider 8380");
        Hide(root.transform, "Player Slot Divider 8390");
        Hide(root.transform, "Player Slot Divider 8400");
        Hide(root.transform, "Player Slot Divider 8410");

        SetButton(root.transform, "Blue Team Button", Load("Join_Blue(button).png"), "", Center, Center, new Vector2(-200, -76), new Vector2(260, 42), 16, true);
        SetButton(root.transform, "Orange Team Button", Load("Join_Orange(button).png"), "", Center, Center, new Vector2(200, -76), new Vector2(260, 42), 16, true);
        SetButton(root.transform, "Fill Players Button", blank, "FILL PLAYERS", Center, Center, new Vector2(-174, -174), new Vector2(160, 40), 14, false);
        SetButton(root.transform, "Remove AI Button", orangeBlank, "REMOVE AI", Center, Center, new Vector2(0, -174), new Vector2(160, 40), 14, false);
        SetButton(root.transform, "Start Game Button", blank, "START GAME", Center, Center, new Vector2(174, -174), new Vector2(160, 40), 14, false);
        SetButton(root.transform, "Ready Button", Load("Ready(button).png"), "", Center, Center, new Vector2(0, -230), new Vector2(198, 42), 15, true);
        GameUI gameUi = root.GetComponent<GameUI>();
        if (gameUi != null)
        {
            gameUi.blueTeamCountImage = FindChild(root.transform, "Blue Team Count").GetComponent<Image>();
            gameUi.orangeTeamCountImage = FindChild(root.transform, "Orange Team Count").GetComponent<Image>();
            gameUi.blueTeamCount0Sprite = Load("Slot(blue).png");
            gameUi.blueTeamCount1Sprite = Load("Slot(1)blue.png");
            gameUi.blueTeamCount2Sprite = Load("Slot(2)blue.png");
            gameUi.orangeTeamCount0Sprite = Load("Slot(orange).png");
            gameUi.orangeTeamCount1Sprite = Load("Slot(1)orange.png");
            gameUi.orangeTeamCount2Sprite = Load("Slot(2)orange.png");
            gameUi.unfilledPlayerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + "/Unfilled Player.png");
            gameUi.filledPlayerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + "/Filled Player.png");
            gameUi.readyButtonSprite = Load("Ready(button).png");
            gameUi.readyCancelSprite = blank;
            EditorUtility.SetDirty(gameUi);
        }
        SetButton(root.transform, "SETTINGS BACK", blank, "BACK", Center, Center, new Vector2(0, -145), new Vector2(210, 48), 17, false);
        SetText(root.transform, "Settings Title", "SETTINGS", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(root.transform, "Settings Title", Center, Center, new Vector2(0, 90), new Vector2(460, 50));
        SetText(root.transform, "Settings Description", "Settings options will be added later.", 17, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetRect(root.transform, "Settings Description", Center, Center, new Vector2(0, 8), new Vector2(480, 44));

        PrefabUtility.SaveAsPrefabAsset(root, GameUiPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void ApplyHud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        if (root == null) return;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.referenceResolution = new Vector2(960, 540);
        SetRect(root.transform, "Score Panel", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(480, 78));
        SetText(root.transform, "Score", "BLUE  0  —  0  ORANGE", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(root.transform, "Score", Center, Center, new Vector2(0, 18), new Vector2(460, 32));
        SetText(root.transform, "Clock", "03:00", 19, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(root.transform, "Clock", Center, Center, new Vector2(0, -10), new Vector2(130, 24));
        SetText(root.transform, "Phase", "MATCH IN PROGRESS", 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(root.transform, "Phase", Center, Center, new Vector2(0, -29), new Vector2(450, 20));
        SetButton(root.transform, "Shoot", null, "SHOOT", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70, 92), new Vector2(120, 64), 18, false);
        SetButton(root.transform, "Pass", null, "PASS", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-200, 92), new Vector2(120, 64), 18, false);
        SetButton(root.transform, "Tackle", null, "TACKLE", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-330, 92), new Vector2(120, 64), 18, false);
        SetButton(root.transform, "Pause", null, "Ⅱ", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-38, -32), new Vector2(48, 48), 22, false);
        PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void ApplyJoystick()
    {
        const string path = "Assets/UI/UI Prefabs/VirtualJoystick.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) return;
        SetRect(root.transform, "Background", Vector2.zero, Vector2.zero, new Vector2(106, 104), new Vector2(152, 152));
        SetRect(root.transform, "Handle", Center, Center, Vector2.zero, new Vector2(56, 56));
        Image background = FindChild(root.transform, "Background")?.GetComponent<Image>();
        if (background != null) background.color = new Color(0.025f, 0.07f, 0.12f, 0.86f);
        Image handle = FindChild(root.transform, "Handle")?.GetComponent<Image>();
        if (handle != null) handle.color = new Color(0.12f, 0.72f, 1f, 1f);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void SetMenuButton(Transform root, string name, Sprite background, string visibleText, string labelSprite, Vector2 anchor, Vector2 position, Vector2 size, Vector2 labelSize)
    {
        SetButton(root, name, background, visibleText, anchor, anchor, position, size, 18, false);
        string childName = name == "PLAY" ? "PLAY Label" : name == "SETTINGS" ? "SETTINGS Label" : "QUIT Label";
        SetChildImage(root, name, childName, Load(labelSprite), labelSize, Vector2.zero);
    }

    static void SetSlotRow(Transform root, string rowName, Vector2 position)
    {
        SetImage(root, rowName, AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + "/Unfilled Player.png"), Color.white);
        SetRect(root, rowName, Center, Center, position, new Vector2(300, 37.5f));
        HideChild(root, rowName, "Name Backplate");
    }

    static void SetCountBadge(Transform root, string parentName, string childName, Sprite initial)
    {
        Transform parent = FindChild(root, parentName);
        if (parent == null) return;
        Transform child = FindChild(parent, childName);
        if (child == null)
        {
            GameObject imageObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            child = imageObject.transform;
        }
        SetRect(child, Vector2.one, new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(63, 35));
        Image image = child.GetComponent<Image>();
        image.sprite = initial;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    static void SetChildImage(Transform root, string parentName, string childName, Sprite sprite, Vector2 size, Vector2 position)
    {
        Transform parent = FindChild(root, parentName);
        Transform child = parent != null ? FindChild(parent, childName) : null;
        if (child == null) return;
        Text text = child.GetComponent<Text>();
        if (text != null) text.enabled = false;
        Transform art = FindChild(child, "Artwork");
        if (art == null)
        {
            GameObject artObject = new GameObject("Artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            artObject.transform.SetParent(child, false);
            art = artObject.transform;
        }
        Image image = art.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        SetRect(child, Center, Center, position, size);
        SetRect(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    static void SetImageOnTextObject(Transform root, string name, Sprite sprite, Vector2 size)
    {
        Transform target = FindChild(root, name);
        if (target == null) return;
        Text text = target.GetComponent<Text>();
        if (text != null) text.enabled = false;
        Transform art = FindChild(target, "Artwork");
        if (art == null)
        {
            GameObject artObject = new GameObject("Artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            artObject.transform.SetParent(target, false);
            art = artObject.transform;
        }
        Image image = art.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        SetRect(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    static void SetImage(Transform root, string name, Sprite sprite, Color color, bool preserveAspect = false)
    {
        Transform target = FindChild(root, name);
        if (target == null) return;
        Image image = target.GetComponent<Image>();
        if (image == null) image = target.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = preserveAspect;
        image.type = Image.Type.Simple;
        image.raycastTarget = target.GetComponent<Button>() != null || target.GetComponent<InputField>() != null;
        EditorUtility.SetDirty(image);
    }

    static void SetButton(Transform root, string name, Sprite background, string label, Vector2 minAnchor, Vector2 maxAnchor, Vector2 position, Vector2 size, int fontSize, bool artworkHasText)
    {
        Transform target = FindChild(root, name);
        if (target == null) return;
        Button button = target.GetComponent<Button>();
        Image image = target.GetComponent<Image>();
        if (image == null) image = target.gameObject.AddComponent<Image>();
        image.sprite = background != null ? background : AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath);
        image.preserveAspect = false;
        image.color = background != null ? Color.white : name.Contains("Disconnect") ? new Color(0.62f, 0.12f, 0.14f) : name == "Shoot" ? new Color(0.03f, 0.38f, 0.84f) : name == "Pass" ? new Color(0.9f, 0.32f, 0.05f) : name == "Tackle" ? new Color(0.08f, 0.68f, 0.28f) : new Color(0.08f, 0.34f, 0.74f);
        image.raycastTarget = true;
        if (button != null) button.targetGraphic = image;
        SetRect(target, minAnchor, maxAnchor, position, size);
        Text text = target.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.enabled = !artworkHasText;
            if (!artworkHasText && !string.IsNullOrEmpty(label))
            {
                text.text = label;
                text.fontSize = fontSize;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;
                RectTransform textRect = text.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(4, 2);
                textRect.offsetMax = new Vector2(-4, -2);
            }
        }
    }

    static void SetText(Transform root, string name, string value, int size, FontStyle style, TextAnchor alignment)
    {
        Transform target = FindChild(root, name);
        Text text = target != null ? target.GetComponent<Text>() : null;
        if (text == null) return;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        EditorUtility.SetDirty(text);
    }

    static void SetTextOn(Transform root, string name, string value, int size, FontStyle style, TextAnchor alignment, Color color)
    {
        Transform target = FindChild(root, name);
        if (target == null) return;
        Text text = target.GetComponent<Text>();
        if (text == null)
        {
            Transform textTransform = FindChild(target, "Text Overlay");
            if (textTransform == null)
            {
                GameObject textObject = new GameObject("Text Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObject.transform.SetParent(target, false);
                textTransform = textObject.transform;
            }
            SetRect(textTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text = textTransform.GetComponent<Text>();
        }
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        EditorUtility.SetDirty(text);
    }

    static void SetRect(Transform root, string name, Vector2 minAnchor, Vector2 maxAnchor, Vector2 position, Vector2 size)
    {
        Transform target = FindChild(root, name);
        if (target != null) SetRect(target, minAnchor, maxAnchor, position, size);
    }

    static void SetRect(Transform target, Vector2 minAnchor, Vector2 maxAnchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null) return;
        rect.anchorMin = minAnchor;
        rect.anchorMax = maxAnchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        EditorUtility.SetDirty(rect);
    }

    static void Hide(Transform root, string name)
    {
        Transform target = FindChild(root, name);
        if (target != null) target.gameObject.SetActive(false);
    }

    static void HideChild(Transform root, string parentName, string childName)
    {
        Transform parent = FindChild(root, parentName);
        if (parent == null) return;
        Transform child = FindChild(parent, childName);
        if (child != null) child.gameObject.SetActive(false);
    }

    static Sprite Load(string fileName) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiRoot}/{fileName}");

    static Transform FindChild(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            Transform nested = FindChild(child, name);
            if (nested != null) return nested;
        }
        return null;
    }

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
}
