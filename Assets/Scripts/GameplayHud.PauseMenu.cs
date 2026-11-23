using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class GameplayHud
{
    enum PauseScreen { Closed, Menu, Sound }

    [Header("Pause Menu Artwork")]
    [SerializeField] Sprite pauseIconSprite;
    [SerializeField] Sprite gameMenuPanelSprite;
    [SerializeField] Sprite resumeButtonSprite;
    [SerializeField] Sprite soundSettingsButtonSprite;
    [SerializeField] Sprite soundSettingsButtonLabelSprite;
    [SerializeField] Sprite disconnectMenuButtonSprite;
    [SerializeField] Sprite disconnectMenuButtonLabelSprite;
    [SerializeField] Sprite soundSettingsBackgroundSprite;
    [SerializeField] Sprite soundSettingsLogoSprite;
    [SerializeField] Sprite soundSettingsPanelSprite;
    [SerializeField] Sprite soundControlsPanelSprite;
    [SerializeField] Sprite soundVolumeLabelSprite;
    [SerializeField] Sprite soundTrackSprite;
    [SerializeField] Sprite soundFillSprite;
    [SerializeField] Sprite soundHandleSprite;
    [SerializeField] Sprite soundLowIconSprite;
    [SerializeField] Sprite soundHighIconSprite;
    [SerializeField] Sprite soundBackButtonSprite;
    [SerializeField] Sprite soundBackButtonLabelSprite;
    [SerializeField] bool resumeArtIncludesLabel;

    static readonly Color MenuSurface = new Color32(10, 22, 38, 250);
    static readonly Color MenuAccent = new Color32(0, 192, 226, 255);
    static readonly Color MenuPrimary = new Color32(0, 89, 198, 255);
    static readonly Color MenuDanger = new Color32(139, 35, 45, 255);
    PauseScreen pauseScreen;
    GameObject pauseOverlay;
    Image gameMenuPanel;
    Button resumeMenuButton;
    SoundSettingsView pauseSoundSettings;
    Font menuFont;

    internal sealed class SoundSettingsView
    {
        public GameObject Root;
        public Slider Slider;
        public Text VolumeValue;

        public void Refresh(float volume)
        {
            Slider.SetValueWithoutNotify(volume);
            ShowVolume(volume);
        }

        public void ShowVolume(float volume)
        {
            VolumeValue.text = $"{Mathf.RoundToInt(volume * 100f)}%";
        }
    }

    void InitializePauseMenu()
    {
        menuFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pauseButton.image.sprite = pauseIconSprite;
        pauseButton.image.color = pauseIconSprite != null ? Color.white : MenuSurface;
        pauseButton.image.preserveAspect = pauseIconSprite != null;
        foreach (Text label in pauseButton.GetComponentsInChildren<Text>(true))
            label.enabled = false;
        if (pauseIconSprite == null)
        {
            CreateMenuImage("Left Bar", pauseButton.transform, new Vector2(-5, 0), new Vector2(4, 16), Color.white);
            CreateMenuImage("Right Bar", pauseButton.transform, new Vector2(5, 0), new Vector2(4, 16), Color.white);
        }
        pauseButton.onClick.AddListener(() => SetPauseScreen(PauseScreen.Menu));

        Image overlay = CreateMenuImage("Pause Overlay", transform, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.58f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        pauseOverlay = overlay.gameObject;
        Vector2 menuSize = new Vector2(426, 344);
        gameMenuPanel = CreateMenuImage("Game Menu", overlay.transform, Vector2.zero, menuSize, MenuSurface, gameMenuPanelSprite);
        AddMenuOutline(gameMenuPanel, MenuAccent);
        CreateMenuLabel("GAME MENU", gameMenuPanel.transform, new Vector2(0, 121), new Vector2(396, 39), 34);
        var subtitle = CreateMenuLabel("Online match continues", gameMenuPanel.transform, new Vector2(0, 90), new Vector2(396, 25), 17);
        subtitle.fontStyle = FontStyle.Normal;
        subtitle.color = new Color32(196, 209, 227, 255);
        resumeMenuButton = CreateMenuButton("RESUME", gameMenuPanel.transform, 33, MenuPrimary, MenuAccent,
            resumeButtonSprite, () => SetPauseScreen(PauseScreen.Closed),
            embeddedLabel: resumeArtIncludesLabel);
        Button sound = CreateMenuButton("SOUND SETTINGS", gameMenuPanel.transform, -37, MenuSurface, MenuAccent,
            soundSettingsButtonSprite, () => SetPauseScreen(PauseScreen.Sound), soundSettingsButtonLabelSprite);
        Button disconnect = CreateMenuButton("DISCONNECT", gameMenuPanel.transform, -107, MenuDanger,
            new Color32(245, 99, 110, 255), disconnectMenuButtonSprite, DisconnectFromMenu, disconnectMenuButtonLabelSprite);
        SetMenuNavigation(resumeMenuButton, null, sound);
        SetMenuNavigation(sound, resumeMenuButton, disconnect);
        SetMenuNavigation(disconnect, sound, null);

        pauseSoundSettings = CreateSoundSettings(overlay.transform, () => SetPauseScreen(PauseScreen.Menu));
        SetPauseScreen(PauseScreen.Closed);
    }

    internal SoundSettingsView CreateSoundSettings(Transform parent, UnityEngine.Events.UnityAction onBack)
    {
        Image backdrop = CreateMenuImage("Sound Settings Background", parent, Vector2.zero, Vector2.zero,
            new Color32(8, 17, 32, 255), soundSettingsBackgroundSprite);
        Stretch(backdrop.rectTransform);
        Image soundMenuPanel = CreateMenuImage("Sound Settings", backdrop.transform, new Vector2(0, -16), new Vector2(540, 294),
            MenuSurface, soundSettingsPanelSprite);
        AddMenuOutline(soundMenuPanel, MenuAccent);
        if (soundSettingsLogoSprite != null)
            CreateMenuImage("CapStrike Logo", soundMenuPanel.transform, new Vector2(0, 180), new Vector2(235, 45),
                Color.white, soundSettingsLogoSprite).preserveAspect = true;
        CreateMenuLabel("SOUND SETTINGS", soundMenuPanel.transform, new Vector2(0, 112), new Vector2(490, 44), 31);
        if (soundControlsPanelSprite != null)
            CreateMenuImage("Sound Controls Background", soundMenuPanel.transform, new Vector2(0, -29),
                new Vector2(506, 185), Color.white, soundControlsPanelSprite);
        if (soundVolumeLabelSprite != null)
            CreateMenuImage("Sound Volume Label", soundMenuPanel.transform, new Vector2(-137, 39),
                new Vector2(184, 25), Color.white, soundVolumeLabelSprite).preserveAspect = true;
        else
            CreateMenuLabel("Sound volume", soundMenuPanel.transform, new Vector2(-118, 39), new Vector2(210, 30), 23)
                .alignment = TextAnchor.MiddleLeft;
        Text volumeValue = CreateMenuLabel("", soundMenuPanel.transform, new Vector2(174, 39), new Vector2(96, 30), 23);
        volumeValue.alignment = TextAnchor.MiddleRight;
        Slider slider = CreateVolumeSlider(soundMenuPanel.transform);
        Button back = CreateMenuButton("BACK", soundMenuPanel.transform, -94, MenuPrimary, MenuAccent,
            soundBackButtonSprite, onBack, soundBackButtonLabelSprite, width: 245);
        SetMenuNavigation(slider, null, back);
        SetMenuNavigation(back, slider, null);
        var view = new SoundSettingsView { Root = backdrop.gameObject, Slider = slider, VolumeValue = volumeValue };
        view.Refresh(game.GetSoundVolume());
        slider.onValueChanged.AddListener(value =>
        {
            game.SetSoundVolume(value);
            view.ShowVolume(value);
            GameAudio.Instance?.PlayVolumePreview();
        });
        return view;
    }

    void UpdatePauseMenu(bool playing)
    {
        if (!playing)
            SetPauseScreen(PauseScreen.Closed);
        else if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            SetPauseScreen(pauseScreen switch
            {
                PauseScreen.Sound => PauseScreen.Menu,
                PauseScreen.Menu => PauseScreen.Closed,
                _ => PauseScreen.Menu
            });
        pauseButton.gameObject.SetActive(playing && pauseScreen == PauseScreen.Closed);
    }

    void SetPauseScreen(PauseScreen screen)
    {
        if (pauseOverlay == null) return;
        bool wasOpen = pauseScreen != PauseScreen.Closed;
        bool isOpen = screen != PauseScreen.Closed;
        bool changed = screen != pauseScreen;
        pauseScreen = screen;
        if (wasOpen != isOpen)
            game.GetLocalPlayer()?.SetMenuInputBlocked(isOpen);

        pauseOverlay.SetActive(isOpen);
        gameMenuPanel.gameObject.SetActive(screen == PauseScreen.Menu);
        pauseSoundSettings.Root.SetActive(screen == PauseScreen.Sound);
        if (!changed) return;

        if (screen == PauseScreen.Sound)
            pauseSoundSettings.Refresh(game.GetSoundVolume());
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(screen switch
            {
                PauseScreen.Menu => resumeMenuButton.gameObject,
                PauseScreen.Sound => pauseSoundSettings.Slider.gameObject,
                _ => null
            });
    }

    void DisconnectFromMenu()
    {
        SetPauseScreen(PauseScreen.Closed);
        game.Disconnect();
    }

    Slider CreateVolumeSlider(Transform parent)
    {
        Image track = CreateMenuImage("Volume Slider", parent, new Vector2(0, -13),
            new Vector2(440, 44), Color.clear);
        track.raycastTarget = true;
        Image rail = CreateMenuImage("Track", track.transform, Vector2.zero, new Vector2(412, 14),
            new Color32(48, 73, 102, 255), soundTrackSprite);
        if (soundLowIconSprite != null)
            CreateMenuImage("Low Volume Icon", track.transform, new Vector2(-232, 0), new Vector2(24, 24),
                Color.white, soundLowIconSprite).preserveAspect = true;
        if (soundHighIconSprite != null)
            CreateMenuImage("High Volume Icon", track.transform, new Vector2(232, 0), new Vector2(30, 30),
                Color.white, soundHighIconSprite).preserveAspect = true;
        Image fillArea = CreateMenuImage("Fill Area", rail.transform, Vector2.zero, Vector2.zero, Color.clear);
        Stretch(fillArea.rectTransform);
        Image fill = CreateMenuImage("Fill", fillArea.transform, Vector2.zero, Vector2.zero, MenuAccent, soundFillSprite);
        Stretch(fill.rectTransform);
        Image handleArea = CreateMenuImage("Handle Area", track.transform, Vector2.zero, new Vector2(412, 36), Color.clear);
        Image handle = CreateMenuImage("Handle", handleArea.transform, Vector2.zero, new Vector2(32, 32),
            Color.white, soundHandleSprite);
        AddMenuOutline(handle, MenuAccent);
        Slider slider = track.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        return slider;
    }

    Button CreateMenuButton(string text, Transform parent, float y, Color color, Color accent, Sprite sprite,
        UnityEngine.Events.UnityAction onClick, Sprite labelSprite = null, bool embeddedLabel = false, float width = 338)
    {
        Image image = CreateMenuImage(text, parent, new Vector2(0, y), new Vector2(width, 54), color, sprite);
        image.raycastTarget = true;
        AddMenuOutline(image, accent);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(0.82f, 0.92f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.64f, 0.75f, 0.9f);
        button.colors = colors;
        button.onClick.AddListener(onClick);
        if (labelSprite != null)
            CreateMenuImage("Label Artwork", image.transform, Vector2.zero, new Vector2(width * 0.72f, 32),
                Color.white, labelSprite).preserveAspect = true;
        else if (sprite == null || !embeddedLabel)
            CreateMenuLabel(text, image.transform, Vector2.zero, new Vector2(width - 8, 54), 23);
        return button;
    }

    static void SetMenuNavigation(Selectable control, Selectable up, Selectable down)
    {
        control.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
    }

    Text CreateMenuLabel(string text, Transform parent, Vector2 position, Vector2 size, int fontSize)
    {
        var label = CreateMenuRect(text, parent, position, size).gameObject.AddComponent<Text>();
        label.font = menuFont;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = text;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    static Image CreateMenuImage(string name, Transform parent, Vector2 position, Vector2 size, Color color, Sprite sprite = null)
    {
        var image = CreateMenuRect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : color;
        image.raycastTarget = false;
        return image;
    }

    static RectTransform CreateMenuRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void AddMenuOutline(Image image, Color color)
    {
        if (image.sprite != null) return;
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }
}
