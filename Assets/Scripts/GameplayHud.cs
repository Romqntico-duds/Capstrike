using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public partial class GameplayHud : MonoBehaviour
{
    public RectTransform resultPanel;
    public Text scoreLabel;
    public Text clockLabel;
    public Text phaseLabel;
    public GameObject scoreboardArtwork;
    public Text resultLabel;
    [SerializeField] GameObject resultBackdrop;
    [SerializeField] Text resultWinner;
    [SerializeField] Text resultBlueScore;
    [SerializeField] Text resultOrangeScore;
    [SerializeField] Text resultMvp;
    [SerializeField] ResultRow[] resultRows;
    [SerializeField] Button resultMainMenuButton;

    [System.Serializable]
    class ResultRow
    {
        public GameObject root;
        public Image teamMarker;
        public Image highlight;
        public Text playerName;
        public Text you;
        public Text goals;
        public Text assists;
        public Text tackles;
    }

    string displayedResult;
    public Button shootButton;
    public Button passButton;
    public Button tackleButton;
    [FormerlySerializedAs("disconnectButton")] public Button pauseButton;
    public Button returnToLobbyButton;
    [SerializeField] Sprite dashButtonSprite;
    [SerializeField] Sprite tackleButtonSprite;
    [SerializeField] string dashButtonText = "DASH\nQ";
    [SerializeField] string tackleButtonText = "TACKLE\nQ";
    Game game;
    Image specialMoveImage;
    Text specialMoveLabel;
    Text kickoffLabel;

    public void Initialize(Game owner)
    {
        game = owner;
        shootButton.onClick.AddListener(game.RequestShoot);
        passButton.onClick.AddListener(game.RequestPass);
        tackleButton.onClick.AddListener(game.RequestSpecialMove);
        InitializePauseMenu();
        kickoffLabel = CreateMenuLabel("Kickoff Countdown", transform, Vector2.zero, new Vector2(180, 110), 72);
        AddCountdownOutline();
        kickoffLabel.transform.SetSiblingIndex(pauseOverlay.transform.GetSiblingIndex());
        kickoffLabel.gameObject.SetActive(false);
        returnToLobbyButton.onClick.AddListener(game.ReturnToLobby);
        if (resultMainMenuButton != null) resultMainMenuButton.onClick.AddListener(game.Disconnect);
        specialMoveImage = tackleButton.image;
        specialMoveLabel = tackleButton.GetComponentInChildren<Text>();
    }

    public void SetVisible(bool visible)
    {
        if (!visible) displayedResult = null;
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    void OnDisable()
    {
        SetPauseScreen(PauseScreen.Closed);
    }

    void Update()
    {
        if (game == null) return;
        MatchController match = game.GetMatchController();
        bool hasMatch = match != null;
        bool playing = hasMatch && match.Phase.Value == MatchController.PlayingPhase;
        bool countdown = hasMatch && match.Phase.Value == MatchController.CountdownPhase;
        bool finished = hasMatch && match.Phase.Value == MatchController.FinishedPhase;

        scoreLabel.text = hasMatch ? $"BLUE  {match.BlueScore.Value}  —  {match.OrangeScore.Value}  ORANGE" : "WAITING FOR PLAYERS";
        clockLabel.text = hasMatch ? FormatTime(match.SecondsRemaining.Value) : "03:00";
        if (phaseLabel != null)
            phaseLabel.text = playing ? "MATCH IN PROGRESS" : finished ? "MATCH COMPLETE" : "CHOOSE TEAMS AND READY UP";
        bool showScoreboard = playing || countdown;
        if (scoreboardArtwork != null && scoreboardArtwork != gameObject)
            scoreboardArtwork.SetActive(showScoreboard);
        scoreLabel.gameObject.SetActive(showScoreboard);
        clockLabel.gameObject.SetActive(showScoreboard);
        kickoffLabel.gameObject.SetActive(countdown);
        if (countdown) kickoffLabel.text = Mathf.Max(1, Mathf.CeilToInt(match.SecondsRemaining.Value)).ToString();
        bool showResults = finished && UpdateResults(match);
        resultPanel.gameObject.SetActive(showResults);
        if (resultBackdrop != null) resultBackdrop.SetActive(showResults);
        if (!finished)
            displayedResult = null;

        UpdatePauseMenu(playing || countdown);
        bool controlsVisible = playing && pauseScreen == PauseScreen.Closed;
        shootButton.gameObject.SetActive(controlsVisible);
        passButton.gameObject.SetActive(controlsVisible);
        tackleButton.gameObject.SetActive(controlsVisible);
        if (playing)
        {
            PlayerMovement localPlayer = game.GetLocalPlayer();
            GameBall ball = match.GetBall();
            bool hasBall = ball != null && ball.IsDribbledBy(localPlayer);
            shootButton.interactable = hasBall;
            passButton.interactable = hasBall;
            tackleButton.interactable = localPlayer != null && !localPlayer.IsRecovering() && match.NetworkManager.ServerTime.Time >= localPlayer.SpecialMoveReadyAt.Value;
            Sprite moveSprite = hasBall ? dashButtonSprite : tackleButtonSprite;
            if (specialMoveImage != null)
                specialMoveImage.overrideSprite = moveSprite;
            if (specialMoveLabel != null)
            {
                specialMoveLabel.enabled = moveSprite == null;
                if (specialMoveLabel.enabled)
                    specialMoveLabel.text = hasBall ? dashButtonText : tackleButtonText;
            }
        }
        else
        {
            shootButton.interactable = false;
            passButton.interactable = false;
            tackleButton.interactable = false;
        }
    }

    void AddCountdownOutline()
    {
        Outline outline = kickoffLabel.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
    }

    bool UpdateResults(MatchController match)
    {
        string snapshot = match.Result.Value.ToString();
        if (string.IsNullOrEmpty(snapshot)) return false;
        if (snapshot == displayedResult) return true;
        string[] lines = snapshot.Split('\n');
        string[] summary = lines[0].Split('|');
        if (summary.Length != 4 || !int.TryParse(summary[0], out int blue)
            || !int.TryParse(summary[1], out int orange)) return false;
        displayedResult = snapshot;

        string winner = blue == orange ? "DRAW" : blue > orange ? "BLUE TEAM WINS" : "ORANGE TEAM WINS";
        string mvpText = $"MVP  {summary[2]}  •  {summary[3]} PTS";
        if (resultWinner == null)
        {
            resultLabel.text = $"{winner}\nBLUE  {blue}  —  {orange}  ORANGE\n{mvpText}";
            for (int i = 1; i < lines.Length; i++)
            {
                string[] row = lines[i].Split('|');
                if (row.Length == 6)
                    resultLabel.text += $"\n{row[2]}   G {row[3]}   A {row[4]}   T {row[5]}";
            }
            return true;
        }

        resultWinner.text = winner;
        resultBlueScore.text = blue.ToString();
        resultOrangeScore.text = orange.ToString();
        resultMvp.text = mvpText;
        PlayerMovement localPlayer = game.GetLocalPlayer();
        for (int i = 0; i < resultRows.Length; i++)
        {
            ResultRow row = resultRows[i];
            string[] fields = i + 1 < lines.Length ? lines[i + 1].Split('|') : null;
            bool hasPlayer = fields != null && fields.Length == 6;
            row.root.SetActive(hasPlayer);
            if (!hasPlayer) continue;

            bool isLocal = localPlayer != null && ulong.TryParse(fields[1], out ulong playerId)
                && playerId == localPlayer.NetworkObjectId;
            row.teamMarker.color = fields[0] == "1" ? new Color(0.02f, 0.72f, 0.94f)
                : new Color(1f, 0.54f, 0.04f);
            row.highlight.enabled = isLocal;
            row.you.gameObject.SetActive(isLocal);
            row.playerName.text = fields[2];
            row.goals.text = fields[3];
            row.assists.text = fields[4];
            row.tackles.text = fields[5];
        }
        return true;
    }

    string FormatTime(float seconds)
    {
        int value = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{value / 60:00}:{value % 60:00}";
    }
}
