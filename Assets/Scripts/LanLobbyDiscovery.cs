using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
public class LanLobbyInfo
{
    public string hostName;
    public string address;
    public int playerCount;
    public int playerLimit;
    public int gamePort;
    public long lastSeenTicks;
}

public class LanLobbyDiscovery : MonoBehaviour
{
    const int DiscoveryPort = 47777;
    const string Query = "CAPSTRIKE_DISCOVER_V1";
    const string Protocol = "CAPSTRIKE_LOBBY_V1";

    readonly ConcurrentDictionary<string, LanLobbyInfo> lobbies = new ConcurrentDictionary<string, LanLobbyInfo>();
    volatile bool hosting;
    volatile bool searching;
    volatile string advertisedName = "CapStrike Host";
    volatile int advertisedCount;
    volatile int advertisedPhase;
    Thread hostThread;
    Thread searchThread;
    UdpClient hostSocket;
    UdpClient searchSocket;

    public bool IsSearching => searching;

    public void SetHostStatus(string hostName, int playerCount, int phase)
    {
        advertisedName = string.IsNullOrWhiteSpace(hostName) ? "CapStrike Host" : hostName.Trim();
        advertisedCount = Math.Max(0, Math.Min(playerCount, 4));
        advertisedPhase = phase;
    }

    public void StartHosting()
    {
        StopHosting();
        hosting = true;
        hostThread = new Thread(HostLoop) { IsBackground = true, Name = "CapStrike LAN Lobby Host" };
        hostThread.Start();
    }

    public void StopHosting()
    {
        hosting = false;
        if (hostSocket != null)
        {
            hostSocket.Close();
            hostSocket = null;
        }
        JoinThread(hostThread);
        hostThread = null;
    }

    public void StartSearch()
    {
        StopSearch();
        lobbies.Clear();
        searching = true;
        searchThread = new Thread(SearchLoop) { IsBackground = true, Name = "CapStrike LAN Lobby Search" };
        searchThread.Start();
    }

    public void StopSearch()
    {
        searching = false;
        if (searchSocket != null)
        {
            searchSocket.Close();
            searchSocket = null;
        }
        JoinThread(searchThread);
        searchThread = null;
    }

    public List<LanLobbyInfo> GetLobbies()
    {
        long now = DateTime.UtcNow.Ticks;
        List<LanLobbyInfo> result = new List<LanLobbyInfo>();
        foreach (KeyValuePair<string, LanLobbyInfo> entry in lobbies)
        {
            if (now - entry.Value.lastSeenTicks > TimeSpan.FromSeconds(5).Ticks)
            {
                lobbies.TryRemove(entry.Key, out _);
                continue;
            }
            result.Add(entry.Value);
        }
        result.Sort((a, b) => string.Compare(a.hostName, b.hostName, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    void HostLoop()
    {
        try
        {
            UdpClient socket = new UdpClient(DiscoveryPort);
            hostSocket = socket;
            socket.Client.ReceiveTimeout = 150;
            while (hosting)
            {
                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] request = socket.Receive(ref remote);
                    if (Encoding.UTF8.GetString(request) != Query) continue;
                    int phase = advertisedPhase;
                    int count = advertisedCount;
                    if (phase != 0 || count >= 4) continue;

                    string safeName = Convert.ToBase64String(Encoding.UTF8.GetBytes(advertisedName));
                    string data = Protocol + "|" + safeName + "|" + count + "|4|" + Game.LanGamePort;
                    byte[] response = Encoding.UTF8.GetBytes(data);
                    socket.Send(response, response.Length, remote);
                }
                catch (SocketException exception)
                {
                    if (exception.SocketErrorCode != SocketError.TimedOut && hosting) break;
                }
            }
        }
        catch
        {
        }
        finally
        {
            hosting = false;
        }
    }

    void SearchLoop()
    {
        try
        {
            UdpClient socket = new UdpClient(0);
            searchSocket = socket;
            socket.EnableBroadcast = true;
            socket.Client.ReceiveTimeout = 250;
            byte[] request = Encoding.UTF8.GetBytes(Query);
            IPEndPoint broadcast = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
            DateTime nextQuery = DateTime.MinValue;

            while (searching)
            {
                if (DateTime.UtcNow >= nextQuery)
                {
                    socket.Send(request, request.Length, broadcast);
                    nextQuery = DateTime.UtcNow.AddSeconds(1.5);
                }

                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] response = socket.Receive(ref remote);
                    string[] fields = Encoding.UTF8.GetString(response).Split('|');
                    if (fields.Length != 5 || fields[0] != Protocol) continue;
                    string hostName;
                    int count;
                    int limit;
                    int port;
                    try
                    {
                        hostName = Encoding.UTF8.GetString(Convert.FromBase64String(fields[1]));
                        count = int.Parse(fields[2]);
                        limit = int.Parse(fields[3]);
                        port = int.Parse(fields[4]);
                    }
                    catch (FormatException)
                    {
                        continue;
                    }
                    if (count >= limit || port <= 0 || limit <= 0) continue;
                    string address = remote.Address.ToString();
                    string key = address + ":" + port;
                    lobbies[key] = new LanLobbyInfo
                    {
                        hostName = string.IsNullOrWhiteSpace(hostName) ? "CapStrike Host" : hostName,
                        address = address,
                        playerCount = Math.Max(0, Math.Min(count, limit)),
                        playerLimit = Math.Max(1, Math.Min(limit, 4)),
                        gamePort = port,
                        lastSeenTicks = DateTime.UtcNow.Ticks
                    };
                }
                catch (SocketException exception)
                {
                    if (exception.SocketErrorCode != SocketError.TimedOut && searching) break;
                }
            }
        }
        catch
        {
        }
        finally
        {
            searching = false;
        }
    }

    static void JoinThread(Thread thread)
    {
        if (thread != null && thread.IsAlive && Thread.CurrentThread != thread)
            thread.Join(300);
    }

    void OnDestroy()
    {
        StopSearch();
        StopHosting();
    }
}
