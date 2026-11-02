using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

public class GameUI : MonoBehaviour
{
    public RectTransform lobbyPanel;
    public RectTransform connectedPanel;
    public InputField ipField;
    public Button hostButton;
    public Button clientButton;
    public Button disconnectButton;
    public Text ipLabel;
    public Text messageLabel;
    public Text connectionLabel;
    Game game;
    string hostIp = "127.0.0.1";

    public void Initialize(Game owner)
    {
        game = owner;
        InputSystemUIInputModule inputModule = GetComponent<InputSystemUIInputModule>();
        if (inputModule != null)
            inputModule.AssignDefaultActions();
        ipField.text = hostIp;
        ipField.onValueChanged.AddListener(value => hostIp = value);
        hostButton.onClick.AddListener(game.StartHost);
        clientButton.onClick.AddListener(() => game.JoinGame(hostIp));
        disconnectButton.onClick.AddListener(game.Disconnect);
    }

    void Update()
    {
        if (game == null) return;

        bool connected = game.networkManager.IsListening;
        lobbyPanel.gameObject.SetActive(!connected);
        connectedPanel.gameObject.SetActive(connected);
        messageLabel.text = game.GetMessage();
        connectionLabel.text = game.GetConnectionStatus();
        ipLabel.text = hostIp;
    }
}
