using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

public class Game : MonoBehaviour
{
    public const ushort LanGamePort = 7777;
    public NetworkManager networkManager;
    public UnityTransport transport;
    public GameUI uiPrefab;
    public GameBall ballPrefab;
    [SerializeField] GameSoundLibrary soundLibrary;
    string message = "";
    bool isConnecting;
    LanLobbyDiscovery discovery;
    GameAudio audioManager;
    float nextDiscoveryStatusUpdate;

    void OnEnable()
    {
        if (networkManager == null) return;
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnTransportFailure += OnTransportFailure;
    }

    void OnDisable()
    {
        if (networkManager == null) return;
        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        networkManager.OnTransportFailure -= OnTransportFailure;
    }

    void Start()
    {
        discovery = GetComponent<LanLobbyDiscovery>();
        if (discovery == null) discovery = gameObject.AddComponent<LanLobbyDiscovery>();
        audioManager = GetComponent<GameAudio>();
        if (audioManager == null) audioManager = gameObject.AddComponent<GameAudio>();
        audioManager.Configure(soundLibrary);
        GameUI ui = Instantiate(uiPrefab);
        ui.Initialize(this);
    }

    void Update()
    {
        if (!networkManager.IsHost || discovery == null || Time.unscaledTime < nextDiscoveryStatusUpdate) return;
        nextDiscoveryStatusUpdate = Time.unscaledTime + 0.5f;
        MatchController match = GetMatchController();
        int playerCount = networkManager.ConnectedClientsIds.Count;
        string hostName = PlayerPrefs.GetString("CapStrikePlayerName", "CapStrike Host");
        discovery.SetHostStatus(hostName, playerCount, match != null ? match.Phase.Value : MatchController.WaitingPhase);
    }

    public void SetPlayerName(string playerName)
    {
        playerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        if (playerName.Length > 16) playerName = playerName.Substring(0, 16);
        PlayerPrefs.SetString("CapStrikePlayerName", playerName);
        PlayerPrefs.Save();
    }

    public void StartHost()
    {
        if (networkManager.IsListening) return;

        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = ApproveConnection;
        isConnecting = false;
        transport.SetConnectionData("127.0.0.1", LanGamePort, "0.0.0.0");
        bool started = networkManager.StartHost();
        message = started ? "Hosting game." : "Could not start host.";
        if (started)
        {
            if (discovery == null) discovery = GetComponent<LanLobbyDiscovery>() ?? gameObject.AddComponent<LanLobbyDiscovery>();
            discovery.StartHosting();
        }
    }

    public void JoinGame(string ip)
    {
        JoinGame(ip, LanGamePort);
    }

    void JoinGame(string ip, int port)
    {
        if (networkManager.IsListening) return;

        ip = ip?.Trim();
        if (port > 0 && port <= ushort.MaxValue && IPAddress.TryParse(ip, out IPAddress address)
            && address.AddressFamily == AddressFamily.InterNetwork)
        {
            StopLobbySearch();
            // The client must serialize an approval payload when the host requires one.
            networkManager.NetworkConfig.ConnectionApproval = true;
            transport.SetConnectionData(address.ToString(), (ushort)port);
            isConnecting = networkManager.StartClient();
            message = isConnecting ? "Connecting to host..." : "Could not start connection.";
        }
        else
        {
            isConnecting = false;
            message = "Enter the host's IPv4 address.";
        }
    }

    public void FindLobbies()
    {
        if (networkManager.IsListening) return;
        if (discovery == null) discovery = GetComponent<LanLobbyDiscovery>() ?? gameObject.AddComponent<LanLobbyDiscovery>();
        discovery.StartSearch();
        message = "Searching for open lobbies on this Wi-Fi...";
    }

    public void StopLobbySearch()
    {
        if (discovery != null) discovery.StopSearch();
    }

    public bool IsSearchingForLobbies()
    {
        return discovery != null && discovery.IsSearching;
    }

    public List<LanLobbyInfo> GetDiscoveredLobbies()
    {
        return discovery != null ? discovery.GetLobbies() : new List<LanLobbyInfo>();
    }

    public void JoinDiscoveredLobby(LanLobbyInfo lobby)
    {
        JoinGame(lobby.address, lobby.gamePort);
    }

    public void SelectMap(int mapIndex)
    {
        if (!networkManager.IsHost) return;
        MatchController match = GetMatchController();
        if (match != null && match.SetMapChoice(mapIndex))
            message = $"{GetMapName(mapIndex)} selected.";
    }

    public string GetMapName(int mapIndex)
    {
        CourtReferences court = CourtReferences.FindActive();
        return court != null ? court.GetMapName(mapIndex) : mapIndex == 1 ? "Narrow" : "Normal";
    }

    public int GetSelectedMap()
    {
        MatchController match = GetMatchController();
        return match != null ? match.MapChoice.Value : 0;
    }

    public float GetSoundVolume()
    {
        return audioManager != null ? audioManager.Volume : PlayerPrefs.GetFloat("CapStrikeSoundVolume", 0.75f);
    }

    public void SetSoundVolume(float volume)
    {
        if (audioManager == null) audioManager = GetComponent<GameAudio>() ?? gameObject.AddComponent<GameAudio>();
        audioManager.SetVolume(volume);
    }

    public void Disconnect()
    {
        isConnecting = false;
        StopLobbySearch();
        if (discovery != null) discovery.StopHosting();
        if (networkManager.IsListening)
            networkManager.Shutdown();
        message = "Disconnected.";
    }

    void OnClientConnected(ulong clientId)
    {
        if (clientId == networkManager.LocalClientId)
        {
            isConnecting = false;
            message = networkManager.IsHost ? "Host is ready. Waiting for players." : "Connected to host.";
        }
        else
        {
            MatchController match = GetMatchController();
            if (networkManager.IsServer && match != null && match.GetRosterCount() > 4)
                ReplaceBotWithPlayer();
            message = "A player joined the lobby.";
        }
    }

    void ReplaceBotWithPlayer()
    {
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (!player.IsSpawned || !player.IsBot.Value) continue;
            player.NetworkObject.Despawn(true);
            return;
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (!networkManager.IsServer || clientId == networkManager.LocalClientId)
        {
            bool connectionFailed = isConnecting;
            isConnecting = false;
            string reason = networkManager.DisconnectReason;
            bool technicalReason = string.IsNullOrWhiteSpace(reason) || reason.StartsWith("[") ||
                reason.IndexOf("Transport", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                reason.IndexOf("disconnected by server", System.StringComparison.OrdinalIgnoreCase) >= 0;
            message = technicalReason ? connectionFailed
                ? "Could not join. Check the host address, Wi-Fi, and that both devices run the same build."
                : "Disconnected from host." : reason;
        }
        else
        {
            message = "A player left the lobby.";
        }
    }

    void OnTransportFailure()
    {
        isConnecting = false;
        message = "Network connection failed.";
    }

    void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (request.ClientNetworkId == NetworkManager.ServerClientId)
        {
            response.Approved = true;
            response.CreatePlayerObject = networkManager.NetworkConfig.PlayerPrefab != null;
            return;
        }

        MatchController match = GetMatchController();
        response.Reason = match == null ? "The host is still opening the lobby. Try again." :
            match.Phase.Value != MatchController.WaitingPhase ? "The match has already started." :
            networkManager.ConnectedClientsIds.Count >= 4 ? "This lobby is full." : "";
        response.Approved = response.Reason.Length == 0;
        response.CreatePlayerObject = response.Approved;
    }

    public void SelectTeam(int team)
    {
        if (networkManager.LocalClient == null || networkManager.LocalClient.PlayerObject == null)
        {
            message = "Your player is still joining the lobby.";
            return;
        }

        PlayerMovement player = networkManager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        if (player != null)
            player.SelectTeamServerRpc(team);
    }

    public void ToggleReady()
    {
        if (networkManager.LocalClient?.PlayerObject == null) return;
        PlayerMovement player = networkManager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        if (player != null)
            player.SetReadyServerRpc(!player.Ready.Value);
    }

    public bool CanChooseTeam()
    {
        return networkManager.LocalClient != null && networkManager.LocalClient.PlayerObject != null;
    }

    public bool IsLocalPlayerReady()
    {
        if (networkManager.LocalClient?.PlayerObject == null) return false;
        PlayerMovement player = networkManager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        return player != null && player.Ready.Value;
    }

    public bool IsHost()
    {
        return networkManager.IsHost;
    }

    public void FillPlayers()
    {
        if (!networkManager.IsHost) return;
        MatchController match = GetLocalPlayer()?.GetComponent<MatchController>();
        if (match == null) match = GetMatchController();
        if (match == null || !match.IsServer)
        {
            message = "The match controller is not ready yet.";
            return;
        }
        int added = match.FillOpenSlots();
        message = added == 0 ? "There are no open player slots." : $"Added {added} stationary AI player{(added == 1 ? "" : "s")}.";
    }

    public void RemoveAI()
    {
        if (!networkManager.IsHost) return;
        MatchController match = GetLocalPlayer()?.GetComponent<MatchController>();
        if (match == null) match = GetMatchController();
        if (match == null || !match.IsServer)
        {
            message = "The match controller is not ready yet.";
            return;
        }
        int removed = match.RemoveBots();
        message = removed == 0 ? "There are no AI players to remove." : $"Removed {removed} AI player{(removed == 1 ? "" : "s")}.";
    }

    public void StartGame()
    {
        if (!networkManager.IsHost) return;
        MatchController match = GetLocalPlayer()?.GetComponent<MatchController>();
        if (match == null) match = GetMatchController();
        message = match != null && match.TryStartMatch() ? "Match started." : "All four players must be ready before starting.";
    }

    public void ReturnToLobby()
    {
        PlayerMovement player = GetLocalPlayer();
        MatchController match = player != null ? player.GetComponent<MatchController>() : null;
        if (match != null)
            match.ReturnToLobbyServerRpc();
    }

    public PlayerMovement GetLocalPlayer()
    {
        if (networkManager.LocalClient?.PlayerObject == null) return null;
        return networkManager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
    }

    public MatchController GetMatchController()
    {
        return MatchController.FindMatch();
    }

    public void RequestShoot()
    {
        PlayerMovement player = GetLocalPlayer();
        if (player != null) player.RequestShoot();
    }

    public void RequestPass()
    {
        PlayerMovement player = GetLocalPlayer();
        if (player != null) player.RequestPass();
    }

    public void RequestSpecialMove()
    {
        PlayerMovement player = GetLocalPlayer();
        if (player != null) player.SpecialMoveServerRpc();
    }

    public string GetTeamHeader(int team)
    {
        string teamName = team == 1 ? "BLUE TEAM" : "ORANGE TEAM";
        return $"<b>{teamName}</b>   <b>{GetTeamCount(team)}/2</b>";
    }

    public int GetTeamCount(int team)
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        int teamCount = 0;
        foreach (PlayerMovement player in players)
        {
            if (player.IsSpawned && player.Team.Value == team) teamCount++;
        }
        return teamCount;
    }

    public string GetTeamSlot(int team, int slotIndex)
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) =>
        {
            int ownerOrder = a.OwnerClientId.CompareTo(b.OwnerClientId);
            if (ownerOrder != 0) return ownerOrder;
            return a.IsBot.Value.CompareTo(b.IsBot.Value);
        });
        int count = 0;
        foreach (PlayerMovement player in players)
        {
            if (!player.IsSpawned || player.Team.Value != team) continue;
            if (count == slotIndex)
            {
                string playerName = player.PlayerName.Value.ToString();
                if (string.IsNullOrWhiteSpace(playerName))
                    playerName = $"Player {player.OwnerClientId + 1}";
                if (player.Ready.Value)
                    playerName += "  <color=#35E875>✓</color>";
                return playerName;
            }
            count++;
        }
        return "Open slot";
    }

    public string GetMessage()
    {
        return message;
    }

    public string GetConnectionStatus()
    {
        if (networkManager.IsHost)
            return "<color=#35E875>●</color> Connected";
        if (networkManager.IsConnectedClient)
            return "<color=#35E875>●</color> Connected";
        return isConnecting ? "Connecting..." : "Not connected";
    }
}
