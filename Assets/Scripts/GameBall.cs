using UnityEngine;
using Unity.Netcode;

public class GameBall : NetworkBehaviour
{
    const ulong NoDribbler = ulong.MaxValue;
    public NetworkVariable<ulong> DribblerClientId = new NetworkVariable<ulong>(NoDribbler);
    public NetworkVariable<ulong> DribblerNetworkObjectId = new NetworkVariable<ulong>(NoDribbler);
    public NetworkVariable<int> LastTouchTeam = new NetworkVariable<int>(0);
    Vector2 velocity;
    PlayerMovement dribbler;
    ulong lastTouchClientId = NoDribbler;
    ulong assistCandidateClientId = NoDribbler;
    float assistExpiry;
    bool receivingPass;
    float dribbleBlockedUntil;
    PlayerMovement dribbleBlockedPlayer;
    float lastKickBlockedUntil;
    bool scoringLocked;
    [SerializeField, Min(0.01f)] float ballRadius = 0.14f;
    [SerializeField] Sprite[] rollFrames;
    SpriteRenderer ballRenderer;
    Vector2 previousVisualPosition;
    float rollProgress;
    float dribbleTravel;

    public override void OnNetworkSpawn()
    {
        ballRenderer = GetComponent<SpriteRenderer>();
        previousVisualPosition = transform.position;
        rollProgress = 0f;
        dribbleTravel = 0f;
        if (ballRenderer != null && rollFrames != null && rollFrames.Length > 0 && rollFrames[0] != null)
            ballRenderer.sprite = rollFrames[0];
    }

    void LateUpdate()
    {
        if (!IsSpawned) return;

        Vector2 position = transform.position;
        float travel = Vector2.Distance(previousVisualPosition, position);
        previousVisualPosition = position;
        if (IsFree || travel > 1f) dribbleTravel = 0f;
        if (travel < 0.0001f || travel > 1f) return;

        if (!IsFree)
        {
            dribbleTravel += travel;
            if (dribbleTravel >= 0.8f)
            {
                dribbleTravel %= 0.8f;
                GameAudio.Instance?.Play(GameSound.BallDribble);
            }
        }

        if (ballRenderer == null || rollFrames == null || rollFrames.Length < 2) return;

        // Observers animate from the replicated position, with no extra network traffic.
        rollProgress = Mathf.Repeat(rollProgress + travel / (2f * Mathf.PI * ballRadius), 1f);
        Sprite frame = rollFrames[Mathf.Min((int)(rollProgress * rollFrames.Length), rollFrames.Length - 1)];
        if (frame != null && frame != ballRenderer.sprite)
            ballRenderer.sprite = frame;
    }


    public bool IsDribbledBy(PlayerMovement player)
    {
        if (player == null || !player.IsSpawned) return false;
        if (IsServer) return dribbler == player;
        return DribblerNetworkObjectId.Value == player.NetworkObjectId;
    }

    public bool IsFree => DribblerClientId.Value == NoDribbler;
    public float PlayerContactDistance => PlayerMovement.BodyRadius + ballRadius;

    public void Release(Vector2 newVelocity, ulong clientId, int team, bool pass)
    {
        if (!IsServer) return;
        dribbleBlockedPlayer = dribbler;
        lastKickBlockedUntil = Time.time + 0.25f;
        if (dribbler != null)
        {
            Vector2 direction = dribbler.Facing.sqrMagnitude > 0.001f ? dribbler.Facing.normalized : Vector2.right;
            Vector2 carrierPosition = dribbler.transform.position;
            Vector2 start = carrierPosition + direction * 0.4f;
            FindFirstPlayerContact(carrierPosition, ref start, dribbler);
            transform.position = new Vector3(start.x, start.y, -0.1f);
        }
        dribbler = null;
        DribblerClientId.Value = NoDribbler;
        DribblerNetworkObjectId.Value = NoDribbler;
        LastTouchTeam.Value = team;
        lastTouchClientId = clientId;
        if (pass)
        {
            assistCandidateClientId = clientId;
            assistExpiry = Time.time + 8f;
        }
        else if (Time.time > assistExpiry)
        {
            assistCandidateClientId = NoDribbler;
        }
        receivingPass = pass;
        velocity = newVelocity;
        Vector2 releasePosition = transform.position;
        ApplyCourtWalls(ref releasePosition, ref velocity);
        transform.position = new Vector3(releasePosition.x, releasePosition.y, -0.1f);
        dribbleBlockedUntil = 0f;
        scoringLocked = false;
    }

    public void ResetToSpawn()
    {
        if (!IsServer) return;
        dribbler = null;
        DribblerClientId.Value = NoDribbler;
        DribblerNetworkObjectId.Value = NoDribbler;
        LastTouchTeam.Value = 0;
        lastTouchClientId = NoDribbler;
        assistCandidateClientId = NoDribbler;
        assistExpiry = 0;
        receivingPass = false;
        velocity = Vector2.zero;
        CourtReferences court = CourtReferences.FindActive();
        Vector3 spawnPosition = court != null ? court.GetBallSpawnPosition() : Vector3.zero;
        transform.position = new Vector3(spawnPosition.x, spawnPosition.y, -0.1f);
        Vector2 resetPosition = transform.position;
        ApplyCourtWalls(ref resetPosition, ref velocity);
        transform.position = new Vector3(resetPosition.x, resetPosition.y, -0.1f);
        scoringLocked = false;
        dribbleBlockedUntil = Time.time + 0.35f;
        dribbleBlockedPlayer = null;
    }

    void FixedUpdate()
    {
        if (!IsServer || !IsSpawned) return;
        MatchController match = MatchController.FindMatch();
        if (match == null || match.Phase.Value != MatchController.PlayingPhase) return;

        if (dribbler != null && dribbler.IsSpawned)
        {
            Vector2 direction = dribbler.Facing.sqrMagnitude > 0.001f ? dribbler.Facing.normalized : (dribbler.Team.Value == 2 ? Vector2.left : Vector2.right);
            Vector2 dribbleTarget = (Vector2)dribbler.transform.position + direction * 0.42f;
            Vector2 dribblePosition = Vector2.MoveTowards(transform.position, dribbleTarget, Mathf.Max(6f, dribbler.speed * 2.5f) * Time.fixedDeltaTime);
            FindFirstPlayerContact(transform.position, ref dribblePosition, dribbler);
            Vector2 heldBallVelocity = Vector2.zero;
            ApplyCourtWalls(ref dribblePosition, ref heldBallVelocity);
            transform.position = new Vector3(dribblePosition.x, dribblePosition.y, -0.1f);
            if (CheckGoal(match, dribblePosition)) return;
            return;
        }
        if (DribblerClientId.Value != NoDribbler)
        {
            dribbler = null;
            DribblerClientId.Value = NoDribbler;
            DribblerNetworkObjectId.Value = NoDribbler;
        }

        // Fast kicks can hit players, but must slow down before being controlled.
        const float maxControlSpeed = 7.5f;
        bool canBeControlled = velocity.sqrMagnitude <= maxControlSpeed * maxControlSpeed;
        Vector2 previousPosition = transform.position;
        Vector2 position = previousPosition + velocity * Time.fixedDeltaTime;
        PlayerMovement blocker = FindFirstPlayerContact(previousPosition, ref position);
        if (blocker != null)
        {
            if (canBeControlled && CanTakePossession(blocker))
            {
                transform.position = new Vector3(position.x, position.y, -0.1f);
                TakePossession(blocker);
                return;
            }

            Vector2 normal = (position - (Vector2)blocker.transform.position).normalized;
            if (normal == Vector2.zero) normal = -velocity.normalized;
            if (Vector2.Dot(velocity, normal) < 0f)
            {
                velocity = Vector2.Reflect(velocity, normal) * 0.35f;
                PlayBallSoundClientRpc(GameSound.BallIntercept);
            }
        }
        Vector2 velocityBeforeWalls = velocity;
        ApplyCourtWalls(ref position, ref velocity);
        if ((velocity - velocityBeforeWalls).sqrMagnitude > 0.04f)
            PlayBallSoundClientRpc(GameSound.WallBounce);
        velocity = Vector2.MoveTowards(velocity, Vector2.zero, 2.5f * Time.fixedDeltaTime);
        transform.position = new Vector3(position.x, position.y, -0.1f);

        if (CheckGoal(match, position)) return;
        if (canBeControlled) TryDribble(position);
    }

    bool CheckGoal(MatchController match, Vector2 position)
    {
        CourtReferences currentCourt = CourtReferences.FindActive();
        Transform blueGoal = currentCourt != null ? currentCourt.blueGoal : null;
        Transform orangeGoal = currentCourt != null ? currentCourt.orangeGoal : null;
        float blueGoalX = blueGoal != null ? blueGoal.position.x : -7.55f;
        float orangeGoalX = orangeGoal != null ? orangeGoal.position.x : 7.55f;
        if (scoringLocked) return false;

        int scoringTeam;
        if (position.x >= orangeGoalX && IsInsideGoalMouth(orangeGoal, position.y, currentCourt != null ? currentCourt.orangeGoalHalfHeight : 0f))
            scoringTeam = 1;
        else if (position.x <= blueGoalX && IsInsideGoalMouth(blueGoal, position.y, currentCourt != null ? currentCourt.blueGoalHalfHeight : 0f))
            scoringTeam = 2;
        else
            return false;

        scoringLocked = true;
        match.ScoreGoal(scoringTeam, lastTouchClientId, CurrentAssist());
        return true;
    }

    bool IsInsideGoalMouth(Transform goal, float y, float halfHeight)
    {
        if (goal == null) return true;
        return Mathf.Abs(y - goal.position.y) <= Mathf.Max(0f, halfHeight - ballRadius);
    }

    void ApplyCourtWalls(ref Vector2 position, ref Vector2 movementVelocity)
    {
        CourtReferences currentCourt = CourtReferences.FindActive();
        float bottomSideline = -3.5f;
        float topSideline = 3.5f;
        float slope = 0f;
        if (currentCourt != null) currentCourt.GetSidelines(position.x, out bottomSideline, out topSideline, out slope);
        float bounce = currentCourt != null ? currentCourt.wallBounce : 0.85f;
        float clearance = ballRadius * Mathf.Sqrt(1f + slope * slope);
        float bottomLimit = bottomSideline + clearance;
        float topLimit = topSideline - clearance;

        if (position.y < bottomLimit)
        {
            position.y = bottomLimit;
            Vector2 inward = new Vector2(slope, 1f).normalized;
            if (Vector2.Dot(movementVelocity, inward) < 0f)
                movementVelocity -= (1f + bounce) * Vector2.Dot(movementVelocity, inward) * inward;
        }
        else if (position.y > topLimit)
        {
            position.y = topLimit;
            Vector2 inward = new Vector2(slope, -1f).normalized;
            if (Vector2.Dot(movementVelocity, inward) < 0f)
                movementVelocity -= (1f + bounce) * Vector2.Dot(movementVelocity, inward) * inward;
        }

        if (currentCourt == null) return;

        if (currentCourt.blueGoal != null && position.x < currentCourt.blueGoal.position.x + ballRadius && !IsInsideGoalMouth(currentCourt.blueGoal, position.y, currentCourt.blueGoalHalfHeight))
        {
            position.x = currentCourt.blueGoal.position.x + ballRadius;
            if (movementVelocity.x < 0f) movementVelocity.x = -movementVelocity.x * bounce;
        }
        else if (currentCourt.orangeGoal != null && position.x > currentCourt.orangeGoal.position.x - ballRadius && !IsInsideGoalMouth(currentCourt.orangeGoal, position.y, currentCourt.orangeGoalHalfHeight))
        {
            position.x = currentCourt.orangeGoal.position.x - ballRadius;
            if (movementVelocity.x > 0f) movementVelocity.x = -movementVelocity.x * bounce;
        }

        foreach (Collider2D obstacle in currentCourt.GetMapObstacles())
        {
            if (obstacle == null || !obstacle.isActiveAndEnabled) continue;
            Bounds bounds = obstacle.bounds;
            bounds.Expand(ballRadius * 2f);
            if (!bounds.Contains(position)) continue;

            float left = Mathf.Abs(position.x - bounds.min.x);
            float right = Mathf.Abs(bounds.max.x - position.x);
            float bottom = Mathf.Abs(position.y - bounds.min.y);
            float top = Mathf.Abs(bounds.max.y - position.y);
            float closest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
            if (closest == left)
            {
                position.x = bounds.min.x;
                if (movementVelocity.x > 0f) movementVelocity.x = -movementVelocity.x * bounce;
            }
            else if (closest == right)
            {
                position.x = bounds.max.x;
                if (movementVelocity.x < 0f) movementVelocity.x = -movementVelocity.x * bounce;
            }
            else if (closest == bottom)
            {
                position.y = bounds.min.y;
                if (movementVelocity.y > 0f) movementVelocity.y = -movementVelocity.y * bounce;
            }
            else
            {
                position.y = bounds.max.y;
                if (movementVelocity.y < 0f) movementVelocity.y = -movementVelocity.y * bounce;
            }
        }
    }

    PlayerMovement FindFirstPlayerContact(Vector2 start, ref Vector2 end, PlayerMovement ignoredPlayer = null)
    {
        Vector2 movement = end - start;
        float lengthSquared = movement.sqrMagnitude;
        if (lengthSquared <= 0f) return null;

        float radius = PlayerContactDistance;
        float firstEntry = 1f;
        PlayerMovement blocker = null;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (!player.IsSpawned || player.Team.Value == 0 || player == ignoredPlayer) continue;
            Vector2 offset = start - (Vector2)player.transform.position;
            float projection = Vector2.Dot(offset, movement);
            if (projection >= 0f) continue;
            float gap = offset.sqrMagnitude - radius * radius;
            float discriminant = projection * projection - lengthSquared * gap;
            if (discriminant < 0f) continue;
            float entry = gap <= 0f ? 0f : (-projection - Mathf.Sqrt(discriminant)) / lengthSquared;
            if (entry > firstEntry) continue;
            firstEntry = entry;
            blocker = player;
        }

        if (blocker != null)
            end = start + movement * firstEntry;
        return blocker;
    }

    bool CanTakePossession(PlayerMovement player)
    {
        return Time.time >= dribbleBlockedUntil
            && player.IsSpawned && player.Team.Value != 0 && !player.IsBot.Value
            && !player.IsRecovering() && !player.IsTackling
            && (player != dribbleBlockedPlayer || Time.time >= lastKickBlockedUntil);
    }

    void TryDribble(Vector2 ballPosition)
    {
        if (Time.time < dribbleBlockedUntil) return;
        PlayerMovement closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (!CanTakePossession(player)) continue;
            float distance = Vector2.Distance(ballPosition, player.transform.position);
            float pickupRadius = receivingPass && player.Team.Value == LastTouchTeam.Value ? 0.88f : 0.72f;
            if (distance < pickupRadius && distance < closestDistance)
            {
                closest = player;
                closestDistance = distance;
            }
        }
        if (closest != null) TakePossession(closest);
    }

    void TakePossession(PlayerMovement player)
    {
        int previousTouchTeam = LastTouchTeam.Value;
        bool intercepted = previousTouchTeam != 0 && previousTouchTeam != player.Team.Value;
        dribbler = player;
        receivingPass = false;
        DribblerClientId.Value = player.OwnerClientId;
        DribblerNetworkObjectId.Value = player.NetworkObjectId;
        LastTouchTeam.Value = player.Team.Value;
        lastTouchClientId = player.OwnerClientId;
        velocity = Vector2.zero;
        PlayBallSoundClientRpc(intercepted ? GameSound.BallIntercept : GameSound.BallPickup);
        if (Time.time > assistExpiry || previousTouchTeam != player.Team.Value)
            assistCandidateClientId = NoDribbler;
    }

    [ClientRpc]
    void PlayBallSoundClientRpc(GameSound sound) => GameAudio.Instance?.Play(sound);

    ulong CurrentAssist()
    {
        return Time.time <= assistExpiry ? assistCandidateClientId : NoDribbler;
    }
}
