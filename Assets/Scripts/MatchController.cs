using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System.Text;

public class MatchController : NetworkBehaviour
{
    const float ShotSpeed = 11f;
    const float PassSpeed = 6f;

    public const int WaitingPhase = 0;
    public const int PlayingPhase = 1;
    public const int FinishedPhase = 2;
    public const int CountdownPhase = 3;
    const int KickoffSeconds = 3;
    public GameBall ballPrefab;
    public float matchLength = 180f;
    public NetworkVariable<int> BlueScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> OrangeScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> Phase = new NetworkVariable<int>(WaitingPhase);
    public NetworkVariable<float> SecondsRemaining = new NetworkVariable<float>(180f);
    public NetworkVariable<bool> RosterFull = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> EveryoneReady = new NetworkVariable<bool>(false);
    public NetworkVariable<int> MapChoice = new NetworkVariable<int>(0);
    public NetworkVariable<FixedString512Bytes> Result = new NetworkVariable<FixedString512Bytes>(default);
    GameBall ball;
    double matchEndsAt;
    double kickoffAt;

    public static int CurrentPhase
    {
        get
        {
            MatchController match = FindMatch();
            return match == null ? WaitingPhase : match.Phase.Value;
        }
    }

    public static MatchController FindMatch()
    {
        MatchController[] matches = FindObjectsByType<MatchController>(FindObjectsSortMode.None);
        foreach (MatchController match in matches)
        {
            PlayerMovement player = match.GetComponent<PlayerMovement>();
            if (match.IsSpawned && match.OwnerClientId == NetworkManager.ServerClientId && player != null && !player.IsBot.Value)
                return match;
        }
        return null;
    }

    public GameBall GetBall()
    {
        if (ball != null && ball.IsSpawned) return ball;
        GameBall[] balls = FindObjectsByType<GameBall>(FindObjectsSortMode.None);
        return balls.Length > 0 ? balls[0] : null;
    }

    public override void OnNetworkSpawn()
    {
        PlayerMovement player = GetComponent<PlayerMovement>();
        if (OwnerClientId != NetworkManager.ServerClientId || player == null || player.IsBot.Value) return;
        MapChoice.OnValueChanged += OnMapChoiceChanged;
        ApplyMapChoice(MapChoice.Value);
        if (IsServer)
        {
            if (ballPrefab != null)
            {
                CourtReferences court = CourtReferences.FindActive();
                Vector3 spawnPosition = court != null ? court.GetBallSpawnPosition() : Vector3.zero;
                spawnPosition.z = -0.1f;
                ball = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
                ball.NetworkObject.Spawn();
            }
            else
            {
                Debug.LogError("MatchController needs a networked ball prefab.");
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        MapChoice.OnValueChanged -= OnMapChoiceChanged;
    }

    public bool SetMapChoice(int mapIndex)
    {
        if (!IsServer || Phase.Value != WaitingPhase || (mapIndex != 0 && mapIndex != 1)) return false;
        MapChoice.Value = mapIndex;
        return true;
    }

    void OnMapChoiceChanged(int previous, int current)
    {
        ApplyMapChoice(current);
        GameAudio.Instance?.Play(GameSound.MapSelect);
    }

    void ApplyMapChoice(int mapIndex)
    {
        CourtReferences court = CourtReferences.FindActive();
        if (court != null) court.ApplyMapStyle(mapIndex);
    }

    void Update()
    {
        PlayerMovement player = GetComponent<PlayerMovement>();
        if (!IsSpawned || !IsServer || OwnerClientId != NetworkManager.ServerClientId || player == null || player.IsBot.Value) return;

        if (Phase.Value == CountdownPhase)
        {
            int remaining = Mathf.CeilToInt((float)(kickoffAt - NetworkManager.ServerTime.Time));
            if (remaining <= 0)
            {
                SecondsRemaining.Value = matchLength;
                matchEndsAt = NetworkManager.ServerTime.Time + matchLength;
                Phase.Value = PlayingPhase;
                PlayMatchSoundClientRpc(GameSound.CountdownGo);
            }
            else if (remaining != SecondsRemaining.Value)
            {
                SecondsRemaining.Value = remaining;
                PlayMatchSoundClientRpc(GameSound.CountdownTick);
            }
            return;
        }

        if (Phase.Value == PlayingPhase)
        {
            float remaining = Mathf.Max(0f, (float)(matchEndsAt - NetworkManager.ServerTime.Time));
            int second = Mathf.CeilToInt(remaining);
            if (second != SecondsRemaining.Value)
            {
                bool reachedWarning = SecondsRemaining.Value > 10 && second <= 10 && second > 0;
                SecondsRemaining.Value = second;
                if (reachedWarning) PlayMatchSoundClientRpc(GameSound.TimerWarning);
            }
            if (remaining <= 0f)
                FinishMatch();
            return;
        }

        UpdateLobbyState();
    }

    void UpdateLobbyState()
    {
        int blue = 0;
        int orange = 0;
        int total = 0;
        bool everyoneReady = true;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (!player.IsSpawned) continue;
            if (!player.Ready.Value) everyoneReady = false;
            total++;
            if (player.Team.Value == 1) blue++;
            if (player.Team.Value == 2) orange++;
        }
        bool rosterFull = total == 4 && blue == 2 && orange == 2;
        if (RosterFull.Value != rosterFull) RosterFull.Value = rosterFull;
        bool readyToStart = rosterFull && everyoneReady;
        if (EveryoneReady.Value != readyToStart) EveryoneReady.Value = readyToStart;
    }

    public int GetRosterCount()
    {
        int count = 0;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (player.IsSpawned) count++;
        }
        return count;
    }

    public int FillOpenSlots()
    {
        if (!IsServer || Phase.Value != WaitingPhase) return 0;
        int openSlots = 4 - GetRosterCount();
        int added = 0;
        for (int team = 1; team <= 2; team++)
        {
            int teamCount = 0;
            foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            {
                if (player.IsSpawned && player.Team.Value == team) teamCount++;
            }

            while (teamCount < 2 && added < openSlots)
            {
                if (SpawnBot(team, teamCount))
                {
                    teamCount++;
                    added++;
                }
                else
                {
                    return added;
                }
            }
        }
        return added;
    }

    public int RemoveBots()
    {
        if (!IsServer || Phase.Value != WaitingPhase) return 0;
        int removed = 0;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (!player.IsSpawned || !player.IsBot.Value) continue;
            player.NetworkObject.Despawn(true);
            removed++;
        }
        return removed;
    }

    bool SpawnBot(int team, int teamSlot)
    {
        GameObject playerPrefab = NetworkManager.NetworkConfig.PlayerPrefab;
        if (playerPrefab == null) return false;
        CourtReferences court = CourtReferences.FindActive();
        Vector3 position = court != null ? court.GetPlayerSpawnPosition(team, teamSlot) : new Vector3(team == 1 ? -4f : 4f, teamSlot == 0 ? -0.8f : 0.8f, 0f);
        GameObject botObject = Instantiate(playerPrefab, position, Quaternion.identity);
        PlayerMovement bot = botObject.GetComponent<PlayerMovement>();
        NetworkObject networkObject = botObject.GetComponent<NetworkObject>();
        if (bot == null || networkObject == null)
        {
            Destroy(botObject);
            return false;
        }
        bot.PrepareAsBot(team);
        networkObject.SpawnWithOwnership(NetworkManager.ServerClientId);
        return true;
    }

    public bool TryStartMatch()
    {
        if (!IsServer || Phase.Value != WaitingPhase) return false;
        UpdateLobbyState();
        if (!RosterFull.Value || !EveryoneReady.Value) return false;
        BlueScore.Value = 0;
        OrangeScore.Value = 0;
        SecondsRemaining.Value = KickoffSeconds;
        kickoffAt = NetworkManager.ServerTime.Time + KickoffSeconds;
        Result.Value = default;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (!player.IsSpawned) continue;
            player.RecoverUntil.Value = 0;
            player.SpecialMoveReadyAt.Value = 0;
            player.GoalCount.Value = 0;
            player.AssistCount.Value = 0;
            player.TackleCount.Value = 0;
        }
        ResetPlayersAndBall();
        Phase.Value = CountdownPhase;
        PlayMatchSoundClientRpc(GameSound.CountdownTick);
        return true;
    }

    public void Shoot(PlayerMovement shooter)
    {
        if (!IsServer || Phase.Value != PlayingPhase) return;
        if (shooter.IsBot.Value || shooter.IsRecovering() || shooter.IsTackling) return;
        GameBall activeBall = GetBall();
        if (activeBall == null || !activeBall.IsDribbledBy(shooter)) return;
        Vector2 shotDirection = shooter.Facing;
        activeBall.Release(shotDirection * ShotSpeed, shooter.OwnerClientId, shooter.Team.Value, false);
        shooter.PlayShootAnimation(shotDirection);
        PlayMatchSoundClientRpc(GameSound.BallShoot);
    }

    public void Pass(PlayerMovement passer)
    {
        if (!IsServer || Phase.Value != PlayingPhase) return;
        if (passer.IsBot.Value || passer.IsRecovering() || passer.IsTackling) return;
        GameBall activeBall = GetBall();
        if (activeBall == null || !activeBall.IsDribbledBy(passer)) return;

        activeBall.Release(passer.Facing * PassSpeed, passer.OwnerClientId, passer.Team.Value, true);
        passer.PlayShootAnimation(passer.Facing);
        PlayMatchSoundClientRpc(GameSound.BallPass);
    }

    public void UseSpecialMove(PlayerMovement attacker)
    {
        if (!IsServer || Phase.Value != PlayingPhase || attacker.IsBot.Value || attacker.IsRecovering() || attacker.IsTackling) return;
        double now = NetworkManager.ServerTime.Time;
        if (now < attacker.SpecialMoveReadyAt.Value)
        {
            PlayMatchSoundClientRpc(GameSound.TackleCooldown, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { attacker.OwnerClientId } }
            });
            return;
        }

        attacker.SpecialMoveReadyAt.Value = now + 4.2;
        GameBall activeBall = GetBall();
        if (activeBall != null && activeBall.IsDribbledBy(attacker))
        {
            attacker.BeginDash();
            PlayMatchSoundClientRpc(GameSound.TackleAttempt);
            return;
        }
        attacker.BeginTackle();
        PlayMatchSoundClientRpc(GameSound.TackleAttempt);
    }

    public void ResolveTackleHit(PlayerMovement attacker, PlayerMovement target)
    {
        if (target.Team.Value == attacker.Team.Value || target.Team.Value == 0 || target.IsRecovering()) return;

        attacker.TackleCount.Value++;
        target.RecoverUntil.Value = NetworkManager.ServerTime.Time + 1.2;
        PlayMatchSoundClientRpc(GameSound.TackleSuccess);
        GameBall activeBall = GetBall();
        if (activeBall != null && (activeBall.IsDribbledBy(target) || activeBall.IsDribbledBy(attacker)))
            activeBall.Release(attacker.Facing * 7f, attacker.OwnerClientId, attacker.Team.Value, false);
    }

    public void ScoreGoal(int scoringTeam, ulong scorerId, ulong assisterId)
    {
        if (!IsServer || Phase.Value != PlayingPhase) return;
        if (scoringTeam == 1) BlueScore.Value++;
        else OrangeScore.Value++;
        PlayMatchSoundClientRpc(GameSound.GoalScored);

        PlayerMovement scorer = FindPlayer(scorerId);
        if (scorer != null && scorer.Team.Value == scoringTeam)
            scorer.GoalCount.Value++;
        else
            scorer = null;
        PlayerMovement assister = FindPlayer(assisterId);
        if (assister != null && scorer != null && assister != scorer && assister.Team.Value == scoringTeam)
            assister.AssistCount.Value++;
        ResetPlayersAndBall(preserveMovement: true);
        PlayMatchSoundClientRpc(GameSound.GoalReset);
    }

    [ClientRpc]
    void PlayMatchSoundClientRpc(GameSound sound, ClientRpcParams targets = default)
    {
        if (sound == GameSound.GoalReset)
            GameAudio.Instance?.QueueGoalReset();
        else
            GameAudio.Instance?.Play(sound);
    }

    void ResetPlayersAndBall(bool preserveMovement = false)
    {
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (player.IsSpawned && player.Team.Value != 0)
                player.ResetToSpawnPoint(preserveMovement);
        }
        GameBall activeBall = GetBall();
        if (activeBall != null) activeBall.ResetToSpawn();
    }

    void FinishMatch()
    {
        PlayerMovement mvp = null;
        int highestScore = -1;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (!player.IsSpawned || player.IsBot.Value) continue;
            int points = player.GoalCount.Value * 4 + player.AssistCount.Value * 2 + player.TackleCount.Value;
            bool tieBreakWins = mvp != null && points == highestScore &&
                (player.GoalCount.Value > mvp.GoalCount.Value ||
                player.GoalCount.Value == mvp.GoalCount.Value && player.AssistCount.Value > mvp.AssistCount.Value ||
                player.GoalCount.Value == mvp.GoalCount.Value && player.AssistCount.Value == mvp.AssistCount.Value && player.TackleCount.Value > mvp.TackleCount.Value ||
                player.GoalCount.Value == mvp.GoalCount.Value && player.AssistCount.Value == mvp.AssistCount.Value && player.TackleCount.Value == mvp.TackleCount.Value && player.OwnerClientId < mvp.OwnerClientId);
            if (points > highestScore || tieBreakWins)
            {
                highestScore = points;
                mvp = player;
            }
        }
        string mvpName = mvp == null ? "No MVP" : SafeResultName(mvp.PlayerName.Value.ToString());
        int mvpPoints = mvp == null ? 0 : mvp.GoalCount.Value * 4 + mvp.AssistCount.Value * 2 + mvp.TackleCount.Value;
        StringBuilder summary = new StringBuilder();
        summary.Append(BlueScore.Value).Append('|').Append(OrangeScore.Value)
            .Append('|').Append(mvpName).Append('|').Append(mvpPoints);
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) =>
        {
            int teamOrder = a.Team.Value.CompareTo(b.Team.Value);
            if (teamOrder != 0) return teamOrder;
            int ownerOrder = a.OwnerClientId.CompareTo(b.OwnerClientId);
            return ownerOrder != 0 ? ownerOrder : a.NetworkObjectId.CompareTo(b.NetworkObjectId);
        });
        foreach (PlayerMovement player in players)
        {
            if (!player.IsSpawned) continue;
            string playerName = SafeResultName(player.PlayerName.Value.ToString());
            summary.Append('\n').Append(player.Team.Value)
                .Append('|').Append(player.NetworkObjectId)
                .Append('|').Append(playerName)
                .Append('|').Append(player.GoalCount.Value)
                .Append('|').Append(player.AssistCount.Value)
                .Append('|').Append(player.TackleCount.Value);
        }
        Result.Value = new FixedString512Bytes(summary.ToString());
        Phase.Value = FinishedPhase;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (!player.IsSpawned) continue;
            player.Ready.Value = player.IsBot.Value;
        }
    }

    [ServerRpc]
    public void ReturnToLobbyServerRpc()
    {
        MatchController match = FindMatch();
        if (match == null || !match.IsServer || match.Phase.Value != FinishedPhase) return;

        match.ResetPlayersAndBall();
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (player.IsSpawned)
                player.Ready.Value = player.IsBot.Value;
        }
        match.BlueScore.Value = 0;
        match.OrangeScore.Value = 0;
        match.SecondsRemaining.Value = match.matchLength;
        match.Result.Value = default;
        match.Phase.Value = WaitingPhase;
        match.UpdateLobbyState();
    }

    PlayerMovement FindPlayer(ulong clientId)
    {
        if (NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) && client.PlayerObject != null)
            return client.PlayerObject.GetComponent<PlayerMovement>();
        return null;
    }

    string SafeResultName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return "Player";
        playerName = playerName.Replace("|", "").Replace("<", "").Replace(">", "").Replace("\n", "").Replace("\r", "");
        int length = playerName.Length;
        while (length > 0 && Encoding.UTF8.GetByteCount(playerName.Substring(0, length)) > 12)
            length--;
        if (length > 0 && char.IsHighSurrogate(playerName[length - 1]))
            length--;
        return length > 0 ? playerName.Substring(0, length) : "Player";
    }
}
