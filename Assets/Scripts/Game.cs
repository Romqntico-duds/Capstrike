using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class Game : MonoBehaviour
{
    public NetworkManager networkManager;
    public UnityTransport transport;
    public GameUI uiPrefab;
    string message = "";

    void Start()
    {
        GameUI ui = Instantiate(uiPrefab);
        ui.Initialize(this);
    }

    public void StartHost()
    {
        transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
        message = networkManager.StartHost() ? "Hosting game." : "Could not start host.";
    }

    public void JoinGame(string ip)
    {
        if (System.Net.IPAddress.TryParse(ip, out _))
        {
            transport.SetConnectionData(ip, 7777);
            message = networkManager.StartClient() ? "Connecting..." : "Could not connect.";
        }
        else
        {
            message = "Enter a valid host IP address.";
        }
    }

    public void Disconnect()
    {
        networkManager.Shutdown();
        message = "Disconnected.";
    }

    public string GetMessage()
    {
        return message;
    }

    public string GetConnectionStatus()
    {
        return networkManager.IsHost ? "Host" : networkManager.IsConnectedClient ? "Client connected" : "Connecting...";
    }
}
