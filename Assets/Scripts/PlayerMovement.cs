using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine.InputSystem;
using Unity.Collections;

public class PlayerMovement : NetworkBehaviour
{
    public const float BodyRadius = 0.3f;
    const float LungeSpeed = 8f;
    const float LungeVisualDuration = 0.34f;

    enum SpecialMove : byte { None, Dash, Tackle }
    enum VisualAction : byte { None, Shoot, Dash, Tackle }

    [System.Serializable]
    struct DirectionalFrames
    {
        public Sprite idle;
        public Sprite[] run;
        public Sprite[] shootPass;
        public Sprite[] dashTackle;
        public Sprite[] getDown;
        public Sprite[] getUp;

        public Sprite Sample(AnimatorStateInfo state)
        {
            Sprite[] frames;
            if (state.IsName("Base Layer.Run")) frames = run;
            else if (state.IsName("Base Layer.ShootPass")) frames = shootPass;
            else if (state.IsName("Base Layer.Dash") || state.IsName("Base Layer.Tackle")) frames = dashTackle;
            else if (state.IsName("Base Layer.GetDown")) frames = getDown;
            else if (state.IsName("Base Layer.GetUp")) frames = getUp;
            else return null;

            if (frames == null || frames.Length == 0) return null;
            float progress = state.loop ? Mathf.Repeat(state.normalizedTime, 1f) : Mathf.Clamp01(state.normalizedTime);
            return frames[Mathf.Min((int)(progress * frames.Length), frames.Length - 1)];
        }
    }

    [System.Serializable]
    struct TeamDirectionalFrames
    {
        public DirectionalFrames left;
        public DirectionalFrames right;
        public DirectionalFrames up;

        public DirectionalFrames Get(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                return direction.x < 0f ? left : right;

            return direction.y > 0f ? up : default;
        }
    }

    static readonly int MovingParameter = Animator.StringToHash("Moving");
    static readonly int FacingXParameter = Animator.StringToHash("FacingX");
    static readonly int FacingYParameter = Animator.StringToHash("FacingY");
    static readonly int TeamParameter = Animator.StringToHash("Team");
    static readonly int SpecialMoveParameter = Animator.StringToHash("SpecialMove");

    static readonly int RunningParameter = Animator.StringToHash("Running");
    static readonly int RecoveringParameter = Animator.StringToHash("Recovering");
    static readonly int ShootPassTrigger = Animator.StringToHash("ShootPass");
    static readonly int TackleTrigger = Animator.StringToHash("Tackle");
    static readonly int DashTrigger = Animator.StringToHash("Dash");

    [SerializeField] NetworkAnimator actionAnimator;
    [SerializeField] SpriteRenderer visualRenderer;
    [SerializeField] Animator visualAnimator;
    [SerializeField] AnimatorOverrideController blueActionAnimations;
    [SerializeField] AnimatorOverrideController orangeActionAnimations;
    [SerializeField] TeamDirectionalFrames blueDirectionalFrames;
    [SerializeField] TeamDirectionalFrames orangeDirectionalFrames;
    [Tooltip("Enable after adding Sprite frames to both team Idle clips.")]
    [SerializeField] bool animateIdleSprite;
    [Tooltip("Enable after adding Sprite frames to both team Run clips.")]
    [SerializeField] bool animateRunSprite;
    [SerializeField, Range(0.5f, 1f)] float animationSpriteScale = 0.82f;
    RuntimeAnimatorController defaultActionAnimations;
    public float speed = 3f;
    [SerializeField] Vector2 courtEdgeClearance = new Vector2(0.70f, 0.68f);
    public InputActionAsset inputActions;
    public VirtualJoystick joystickPrefab;
    public Sprite blueFrontSprite;
    public Sprite orangeFrontSprite;
    public Sprite blueFacingLeftSprite;
    public Sprite blueFacingRightSprite;
    public Sprite orangeFacingLeftSprite;
    public Sprite orangeFacingRightSprite;
    public float movementAnimationSpeed = 12f;
    public float movementAnimationHeight = 0.035f;
    public NetworkVariable<int> Team = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> Ready = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsBot = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> GoalCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> AssistCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> TackleCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<double> RecoverUntil = new NetworkVariable<double>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<double> SpecialMoveReadyAt = new NetworkVariable<double>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<byte> activeSpecialMove = new NetworkVariable<byte>();
    InputAction moveAction;
    InputAction shootAction;
    InputAction passAction;
    InputAction tackleAction;
    VirtualJoystick joystick;
    bool spawnAsBot;
    int botTeam;
    Vector2 sentMove;
    Vector2 serverMove;
    Vector2 facing;
    float tackleDistance;
    float dashDistance;
    SpriteRenderer spriteRenderer;
    Transform visualTransform;
    Vector3 visualBasePosition;
    Vector3 visualBaseScale;
    Quaternion visualBaseRotation;
    Vector2 lastVisualPosition;
    Vector2 visualFacing;
    float lastMovementSampleAt;
    float animationPhase;
    float strideWeight;
    VisualAction visualAction;
    Vector2 actionFacing;
    float actionStartedAt;
    float nextMovementSend;
    public Vector2 Facing => facing;
    public bool IsTackling => tackleDistance > 0f;

    public void PrepareAsBot(int team)
    {
        spawnAsBot = true;
        botTeam = team;
    }

    void Awake()
    {
        if (inputActions == null) return;
        inputActions = Instantiate(inputActions);
        moveAction = inputActions.FindAction("Player/Move", false);
        shootAction = inputActions.FindAction("Player/Shoot", false);
        passAction = inputActions.FindAction("Player/Pass", false);
        tackleAction = inputActions.FindAction("Player/Tackle", false);
    }

    void OnEnable()
    {
        moveAction?.Enable();
        shootAction?.Enable();
        passAction?.Enable();
        tackleAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.Disable();
        shootAction?.Disable();
        passAction?.Disable();
        tackleAction?.Disable();
    }

    public override void OnNetworkSpawn()
    {
        CacheVisual();
        defaultActionAnimations = actionAnimator != null ? actionAnimator.Animator?.runtimeAnimatorController : null;
        sentMove = serverMove = Vector2.zero;
        Team.OnValueChanged += OnTeamChanged;
        Ready.OnValueChanged += OnReadyChanged;
        if (IsServer && spawnAsBot)
        {
            IsBot.Value = true;
            Team.Value = botTeam;
            Ready.Value = true;
            PlayerName.Value = new FixedString64Bytes("AI Player");
        }
        else if (!IsBot.Value && IsOwner)
        {
            string playerName = PlayerPrefs.GetString("CapStrikePlayerName", $"Player {OwnerClientId + 1}");
            if (IsServer)
                PlayerName.Value = new FixedString64Bytes(playerName);
            else
                SetPlayerNameServerRpc(playerName);
        }
        facing = Team.Value == 2 ? Vector2.left : Vector2.right;
        visualFacing = facing;
        lastVisualPosition = transform.position;
        UpdateTeamAnimations(Team.Value);
        UpdateTeamColor(Team.Value);
        if (IsOwner && !IsBot.Value)
        {
            joystick = Instantiate(joystickPrefab);
        }
    }

    public override void OnNetworkDespawn()
    {
        Team.OnValueChanged -= OnTeamChanged;
        Ready.OnValueChanged -= OnReadyChanged;
        UpdateTeamAnimations(0);
        if (joystick != null)
            Destroy(joystick.gameObject);
    }

    [ServerRpc]
    public void SetPlayerNameServerRpc(string playerName)
    {
        playerName = string.IsNullOrWhiteSpace(playerName) ? $"Player {OwnerClientId + 1}" : playerName.Trim();
        if (playerName.Length > 16) playerName = playerName.Substring(0, 16);
        PlayerName.Value = new FixedString64Bytes(playerName);
    }

    [ServerRpc]
    public void SetReadyServerRpc(bool ready)
    {
        MatchController match = MatchController.FindMatch();
        if (match == null || match.Phase.Value != MatchController.WaitingPhase || ready && Team.Value == 0) return;
        Ready.Value = ready;
    }

    [ServerRpc]
    public void SelectTeamServerRpc(int team)
    {
        MatchController match = MatchController.FindMatch();
        if (match == null || match.Phase.Value != MatchController.WaitingPhase) return;
        if (team != 1 && team != 2) return;

        int teammates = 0;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (player.IsSpawned && player != this && player.Team.Value == team)
                teammates++;
        }

        if (teammates < 2 && Team.Value != team)
        {
            Ready.Value = false;
            Team.Value = team;
        }
    }

    [ServerRpc]
    public void ShootServerRpc()
    {
        MatchController match = MatchController.FindMatch();
        if (match != null && match.IsServer)
            match.Shoot(this);
    }

    [ServerRpc]
    public void PassServerRpc()
    {
        MatchController match = MatchController.FindMatch();
        if (match != null && match.IsServer)
            match.Pass(this);
    }

    [ServerRpc]
    public void SpecialMoveServerRpc()
    {
        MatchController match = MatchController.FindMatch();
        if (match != null && match.IsServer)
            match.UseSpecialMove(this);
    }

    void OnReadyChanged(bool previous, bool current)
    {
        if (current && !IsBot.Value && MatchController.CurrentPhase == MatchController.WaitingPhase)
            GameAudio.Instance?.Play(GameSound.Ready);
    }

    void OnTeamChanged(int previousTeam, int newTeam)
    {
        facing = newTeam == 2 ? Vector2.left : Vector2.right;
        visualFacing = facing;
        UpdateTeamAnimations(newTeam);
        UpdateTeamColor(newTeam);
        if (!IsServer) return;
        RepositionTeam(previousTeam);
        RepositionTeam(newTeam);
    }

    void RepositionTeam(int team)
    {
        if (team != 1 && team != 2) return;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (player.IsSpawned && player.Team.Value == team)
                player.SetTeamStartPosition(team);
        }
    }

    void SetTeamStartPosition(int team)
    {
        if (team != 1 && team != 2) return;
        int slot = 0;
        foreach (PlayerMovement teammate in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (teammate == this || !teammate.IsSpawned || teammate.Team.Value != team) continue;
            if (teammate.OwnerClientId < OwnerClientId || teammate.OwnerClientId == OwnerClientId && teammate.NetworkObjectId < NetworkObjectId)
                slot++;
        }
        CourtReferences court = CourtReferences.FindActive();
        if (court != null)
            transform.position = court.GetPlayerSpawnPosition(team, Mathf.Min(slot, 1));
        else
            transform.position = new Vector3(team == 1 ? -4f : 4f, slot == 0 ? -0.8f : 0.8f, 0f);
    }

    public void ResetToSpawnPoint(bool preserveMovement)
    {
        if (!IsServer) return;
        tackleDistance = 0f;
        dashDistance = 0f;
        activeSpecialMove.Value = (byte)SpecialMove.None;
        if (!preserveMovement) serverMove = Vector2.zero;
        RecoverUntil.Value = 0;
        SetTeamStartPosition(Team.Value);
    }

    void UpdateTeamAnimations(int team)
    {
        Animator animator = actionAnimator != null ? actionAnimator.Animator : null;
        if (animator == null) return;

        AnimatorOverrideController teamAnimations = team switch
        {
            1 => blueActionAnimations,
            2 => orangeActionAnimations,
            _ => null
        };
        RuntimeAnimatorController controller = teamAnimations != null ? teamAnimations : defaultActionAnimations;
        if (animator.runtimeAnimatorController != controller)
            animator.runtimeAnimatorController = controller;
    }

    bool UsesSpriteFrames(AnimatorStateInfo state, bool sideFacing)
    {
        if (sideFacing && (state.IsName("Base Layer.Run") ||
            state.IsName("Base Layer.ShootPass") ||
            state.IsName("Base Layer.Dash") ||
            state.IsName("Base Layer.Tackle")))
            return false;

        return state.IsTag("Frames")
            || (animateIdleSprite && state.IsName("Base Layer.Idle"))
            || (animateRunSprite && state.IsName("Base Layer.Run"));
    }

    void UpdateTeamColor(int team)
    {
        if (visualAnimator != null && visualAnimator.runtimeAnimatorController != null) return;
        SpriteRenderer sprite = GetVisualRenderer();
        if (sprite == null) return;
        Sprite teamSprite = GetSpriteForFacing(team, facing);
        if (teamSprite != null)
        {
            sprite.sprite = teamSprite;
            sprite.color = Color.white;
        }
        else if (team == 1)
        {
            sprite.color = new Color(0.1f, 0.65f, 1f);
        }
        else if (team == 2)
        {
            sprite.color = new Color(1f, 0.42f, 0.08f);
        }
        else
        {
            sprite.color = Color.white;
        }
    }

    void CacheVisual()
    {
        spriteRenderer = visualRenderer != null ? visualRenderer : GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer == null) return;
        visualTransform = spriteRenderer.transform;
        visualBasePosition = visualTransform.localPosition;
        visualBaseScale = visualTransform.localScale;
        visualBaseRotation = visualTransform.localRotation;
    }

    SpriteRenderer GetVisualRenderer()
    {
        if (spriteRenderer == null) CacheVisual();
        return spriteRenderer;
    }

    Sprite GetSpriteForFacing(int team, Vector2 direction)
    {
        Sprite rearSprite = GetDirectionalFrames(team, direction).idle;
        if (rearSprite != null) return rearSprite;
        if (team == 1)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y) && direction.x < 0f && blueFacingLeftSprite != null) return blueFacingLeftSprite;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y) && direction.x > 0f && blueFacingRightSprite != null) return blueFacingRightSprite;
            return blueFrontSprite;
        }
        if (team == 2)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y) && direction.x < 0f && orangeFacingLeftSprite != null) return orangeFacingLeftSprite;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y) && direction.x > 0f && orangeFacingRightSprite != null) return orangeFacingRightSprite;
            return orangeFrontSprite;
        }
        return null;
    }

    DirectionalFrames GetDirectionalFrames(int team, Vector2 direction)
    {
        return team switch
        {
            1 => blueDirectionalFrames.Get(direction),
            2 => orangeDirectionalFrames.Get(direction),
            _ => default
        };
    }

    void LateUpdate()
    {
        if (!IsSpawned) return;

        Vector2 currentPosition = transform.position;
        Vector2 delta = currentPosition - lastVisualPosition;
        lastVisualPosition = currentPosition;
        float now = Time.time;
        bool playing = MatchController.CurrentPhase == MatchController.PlayingPhase && !IsBot.Value;

        if (!playing || delta.sqrMagnitude > 0.65f * 0.65f)
        {
            strideWeight = 0f;
            lastMovementSampleAt = float.NegativeInfinity;
            visualAction = VisualAction.None;
            visualFacing = facing;
        }
        else if (delta.sqrMagnitude > 0.000004f)
        {
            lastMovementSampleAt = now;
            visualFacing = IsServer ? facing : delta.normalized;
        }

        bool moving = playing && now - lastMovementSampleAt < 0.085f;
        float elapsed = now - actionStartedAt;
        float duration = visualAction switch
        {
            VisualAction.Shoot => 0.30f,
            VisualAction.Dash or VisualAction.Tackle => LungeVisualDuration,
            _ => 0f
        };
        if (elapsed >= duration)
            visualAction = VisualAction.None;

        Vector2 poseDirection = visualAction == VisualAction.None ? visualFacing : actionFacing;
        if (visualAnimator != null && visualAnimator.runtimeAnimatorController != null)
        {
            visualAnimator.SetBool(MovingParameter, moving);
            visualAnimator.SetFloat(FacingXParameter, poseDirection.x);
            visualAnimator.SetFloat(FacingYParameter, poseDirection.y);
            visualAnimator.SetInteger(TeamParameter, Team.Value);
            visualAnimator.SetInteger(SpecialMoveParameter, activeSpecialMove.Value);
            return;
        }

        SpriteRenderer sprite = GetVisualRenderer();
        if (sprite == null) return;

        Sprite facingSprite = GetSpriteForFacing(Team.Value, poseDirection);
        Animator animator = actionAnimator != null ? actionAnimator.Animator : null;
        bool sideFacing = facingSprite != null && Mathf.Abs(poseDirection.x) > Mathf.Abs(poseDirection.y);
        bool usesSpriteFrames = false;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            AnimatorStateInfo state = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);
            Sprite directionalFrame = GetDirectionalFrames(Team.Value, poseDirection).Sample(state);
            usesSpriteFrames = directionalFrame != null || UsesSpriteFrames(state, sideFacing);
            if (directionalFrame != null)
                sprite.sprite = directionalFrame;
        }
        if (!usesSpriteFrames && facingSprite != null)
            sprite.sprite = facingSprite;
        if (visualTransform == transform) return;
        if (animator != null)
        {
            // The Animator scales Visual. Adjust only its Sprite child for the larger frame art.
            if (visualTransform.parent == animator.transform)
                visualTransform.localScale = usesSpriteFrames ? visualBaseScale * animationSpriteScale : visualBaseScale;
            return;
        }

        strideWeight = Mathf.MoveTowards(strideWeight, moving ? 1f : 0f, Time.deltaTime * 10f);
        animationPhase = Mathf.Repeat(animationPhase + Time.deltaTime * movementAnimationSpeed, 2f * Mathf.PI);
        float stride = Mathf.Sin(animationPhase);
        float lift = (1f - Mathf.Cos(animationPhase * 2f)) * 0.5f;
        float horizontal = poseDirection.x;
        Vector2 offset = Vector2.up * (lift * movementAnimationHeight * strideWeight);
        float tilt = (stride * 5f - horizontal * 4f) * strideWeight;
        float stretch = Mathf.Cos(animationPhase * 2f) * 0.045f * strideWeight;

        if (visualAction != VisualAction.None)
        {
            float progress = elapsed / duration;
            EvaluateActionPose(visualAction, progress, poseDirection, out Vector2 actionOffset,
                out float actionTilt, out float actionStretch);
            float blendIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.10f, progress));
            float blendOut = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.70f, 1f, progress));
            float actionWeight = blendIn * blendOut;
            offset = Vector2.Lerp(offset, actionOffset, actionWeight);
            tilt = Mathf.Lerp(tilt, actionTilt, actionWeight);
            stretch = Mathf.Lerp(stretch, actionStretch, actionWeight);
        }

        visualTransform.localPosition = visualBasePosition + (Vector3)offset;
        visualTransform.localRotation = visualBaseRotation * Quaternion.Euler(0f, 0f, tilt);
        visualTransform.localScale = new Vector3(
            visualBaseScale.x * (1f - stretch),
            visualBaseScale.y * (1f + stretch),
            visualBaseScale.z);
    }

    static void EvaluateActionPose(VisualAction action, float progress, Vector2 direction,
        out Vector2 offset, out float tilt, out float stretch)
    {
        switch (action)
        {
            case VisualAction.Shoot:
                float windup = PosePulse(progress, 0f, 0.16f, 0.36f);
                float kick = PosePulse(progress, 0.16f, 0.36f, 1f);
                offset = direction * (kick * 0.14f - windup * 0.06f) + Vector2.up * (kick * 0.06f);
                tilt = direction.x * (windup * 13f - kick * 18f);
                stretch = kick * 0.13f - windup * 0.07f;
                break;
            case VisualAction.Dash:
                float burst = PosePulse(progress, 0f, 0.23f, 1f);
                offset = direction * (burst * 0.15f) + Vector2.up * (burst * 0.055f);
                tilt = -direction.x * burst * 23f;
                stretch = -burst * 0.17f;
                break;
            default:
                float lunge = PosePulse(progress, 0f, 0.31f, 1f);
                offset = direction * (lunge * 0.18f) - Vector2.up * (lunge * 0.075f);
                tilt = -direction.x * lunge * 40f;
                stretch = -lunge * 0.30f;
                break;
        }
    }

    static float PosePulse(float progress, float start, float peak, float end)
    {
        float weight = progress <= peak
            ? Mathf.InverseLerp(start, peak, progress)
            : 1f - Mathf.InverseLerp(peak, end, progress);
        return Mathf.SmoothStep(0f, 1f, weight);
    }

    public void PlayShootAnimation(Vector2 direction)
    {
        if (actionAnimator != null) actionAnimator.SetTrigger(ShootPassTrigger);
        PlayActionClientRpc((byte)VisualAction.Shoot, direction);
    }

    [ClientRpc]
    void PlayActionClientRpc(byte action, Vector2 direction)
    {
        visualAction = (VisualAction)action;
        actionFacing = direction;
        actionStartedAt = Time.time;
    }

    public bool IsRecovering()
    {
        return IsSpawned && NetworkManager != null && NetworkManager.ServerTime.Time < RecoverUntil.Value;
    }

    bool menuInputBlocked;

    public void SetMenuInputBlocked(bool blocked)
    {
        menuInputBlocked = blocked;
        if (!blocked || !IsOwner || !IsSpawned) return;

        sentMove = Vector2.zero;
        nextMovementSend = 0f;
        SetMovementServerRpc(Vector2.zero);
        if (joystick != null)
            joystick.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!IsOwner || IsBot.Value || !IsSpawned) return;
        bool playing = MatchController.CurrentPhase == MatchController.PlayingPhase;
        bool showJoystick = playing && !menuInputBlocked;
        if (joystick != null && joystick.gameObject.activeSelf != showJoystick)
            joystick.gameObject.SetActive(showJoystick);
        if (!playing)
        {
            sentMove = Vector2.zero;
            nextMovementSend = 0f;
            return;
        }
        if (menuInputBlocked) return;
        Vector2 direction = ReadMovement();
        bool released = direction.sqrMagnitude < 0.0001f && sentMove.sqrMagnitude >= 0.0001f;
        bool changedEnough = (direction - sentMove).sqrMagnitude >= 0.0025f;
        if (released || changedEnough && Time.unscaledTime >= nextMovementSend)
        {
            sentMove = direction;
            SetMovementServerRpc(direction);
            nextMovementSend = Time.unscaledTime + 1f / 30f;
        }

        if (IsRecovering()) return;
        if (shootAction != null && shootAction.WasPressedThisFrame()) RequestShoot();
        if (passAction != null && passAction.WasPressedThisFrame()) RequestPass();
        if (tackleAction != null && tackleAction.WasPressedThisFrame()) SpecialMoveServerRpc();
    }

    Vector2 ReadMovement()
    {
        Vector2 direction = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        if (joystick != null)
            direction += joystick.Value;
        return Vector2.ClampMagnitude(direction, 1f);
    }

    public void RequestShoot()
    {
        SendKickDirection();
        ShootServerRpc();
    }

    public void RequestPass()
    {
        SendKickDirection();
        PassServerRpc();
    }

    void SendKickDirection()
    {
        Vector2 direction = ReadMovement();
        if (direction.sqrMagnitude > 0.001f && direction != sentMove)
        {
            sentMove = direction;
            SetMovementServerRpc(direction);
        }
    }

    [ServerRpc]
    void SetMovementServerRpc(Vector2 direction)
    {
        MatchController match = MatchController.FindMatch();
        if (match == null || match.Phase.Value != MatchController.PlayingPhase || Team.Value == 0) return;
        if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y)) return;
        serverMove = Vector2.ClampMagnitude(direction, 1f);
        if (tackleDistance <= 0f && serverMove.sqrMagnitude > 0.001f)
            facing = serverMove.normalized;
    }

    public void BeginTackle()
    {
        if (facing == Vector2.zero)
            facing = Team.Value == 2 ? Vector2.left : Vector2.right;
        tackleDistance = 1.8f;
        activeSpecialMove.Value = (byte)SpecialMove.Tackle;
        if (actionAnimator != null) actionAnimator.SetTrigger(TackleTrigger);
        PlayActionClientRpc((byte)VisualAction.Tackle, facing);
    }

    public void BeginDash()
    {
        if (facing == Vector2.zero)
            facing = Team.Value == 2 ? Vector2.left : Vector2.right;
        dashDistance = 1.8f;
        activeSpecialMove.Value = (byte)SpecialMove.Dash;
        if (actionAnimator != null) actionAnimator.SetTrigger(DashTrigger);
        PlayActionClientRpc((byte)VisualAction.Dash, facing);
    }

    void FixedUpdate()
    {
        if (!IsServer || !IsSpawned) return;
        MatchController match = MatchController.FindMatch();
        bool playing = match != null && match.Phase.Value == MatchController.PlayingPhase;
        bool recovering = playing && IsRecovering();
        Animator animator = actionAnimator != null ? actionAnimator.Animator : null;
        if (animator != null)
        {
            animator.SetBool(RecoveringParameter, recovering);
            animator.SetBool(RunningParameter, false);
        }
        if (IsBot.Value) return;

        if (!playing)
        {
            tackleDistance = 0f;
            dashDistance = 0f;
            activeSpecialMove.Value = (byte)SpecialMove.None;
            return;
        }

        if (recovering)
        {
            tackleDistance = 0f;
            dashDistance = 0f;
            activeSpecialMove.Value = (byte)SpecialMove.None;
            return;
        }

        if (tackleDistance > 0f)
        {
            float step = Mathf.Min(tackleDistance, LungeSpeed * Time.fixedDeltaTime);
            PlayerMovement contact = MoveUntilContact(facing * step, out bool hitBoundary);
            tackleDistance -= step;
            if (contact != null)
                match.ResolveTackleHit(this, contact);
            if (contact != null || hitBoundary || tackleDistance <= 0f)
            {
                tackleDistance = 0f;
                activeSpecialMove.Value = (byte)SpecialMove.None;
                RecoverUntil.Value = NetworkManager.ServerTime.Time + 1.2;
            }
            return;
        }

        if (dashDistance > 0f)
        {
            float step = Mathf.Min(dashDistance, LungeSpeed * Time.fixedDeltaTime);
            PlayerMovement contact = MoveUntilContact(facing * step, out bool hitBoundary);
            dashDistance -= step;
            if (contact != null || hitBoundary || dashDistance <= 0f)
            {
                dashDistance = 0f;
                activeSpecialMove.Value = (byte)SpecialMove.None;
            }
            return;
        }

        if (serverMove.sqrMagnitude <= 0f) return;
        Vector3 previousPosition = transform.position;
        Vector2 stepMove = serverMove * speed * Time.fixedDeltaTime;
        MoveUntilContact(new Vector2(stepMove.x, 0f), out _);
        MoveUntilContact(new Vector2(0f, stepMove.y), out _);
        if (animator != null)
            animator.SetBool(RunningParameter, (transform.position - previousPosition).sqrMagnitude > 0.000001f);
    }

    Vector2 ClampToCourt(Vector2 position, CourtReferences court)
    {
        float left = court != null ? court.leftPlayerBoundary : -7.3f;
        float right = court != null ? court.rightPlayerBoundary : 7.3f;
        if (court != null && court.blueGoal != null)
            left = Mathf.Max(left, court.blueGoal.position.x + courtEdgeClearance.x);
        if (court != null && court.orangeGoal != null)
            right = Mathf.Min(right, court.orangeGoal.position.x - courtEdgeClearance.x);

        float x = Mathf.Clamp(position.x, left, right);
        float bottom = -3.5f;
        float top = 3.5f;
        if (court != null)
        {
            // Keep both sides of the player inside the diagonal edges of Narrow.
            court.GetSidelines(x - courtEdgeClearance.x, out float leftBottom, out float leftTop, out _);
            court.GetSidelines(x + courtEdgeClearance.x, out float rightBottom, out float rightTop, out _);
            bottom = Mathf.Max(leftBottom, rightBottom);
            top = Mathf.Min(leftTop, rightTop);
        }
        return new Vector2(x, Mathf.Clamp(position.y, bottom + courtEdgeClearance.y, top - courtEdgeClearance.y));
    }

    PlayerMovement MoveUntilContact(Vector2 movement, out bool hitBoundary)
    {
        const float bodyDiameter = BodyRadius * 2f;
        Vector2 start = transform.position;
        Vector2 requested = start + movement;
        CourtReferences court = CourtReferences.FindActive();
        Vector2 end = ClampToCourt(requested, court);
        hitBoundary = requested != end;
        foreach (Collider2D obstacle in court != null ? court.GetMapObstacles() : System.Array.Empty<Collider2D>())
        {
            if (obstacle == null || !obstacle.isActiveAndEnabled) continue;
            Bounds bounds = obstacle.bounds;
            bounds.Expand(bodyDiameter);
            if (!bounds.Contains(end) || bounds.Contains(start)) continue;
            Vector2 delta = end - start;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                end.x = delta.x >= 0f ? bounds.min.x : bounds.max.x;
            else
                end.y = delta.y >= 0f ? bounds.min.y : bounds.max.y;
            hitBoundary = true;
        }
        Vector2 step = end - start;
        float lengthSquared = step.sqrMagnitude;
        if (lengthSquared <= 0f) return null;

        float fraction = 1f;
        PlayerMovement contact = null;
        foreach (PlayerMovement other in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (other == this || !other.IsSpawned || other.Team.Value == 0) continue;
            Vector2 offset = start - (Vector2)other.transform.position;
            float projection = Vector2.Dot(offset, step);
            if (projection >= 0f) continue;
            float gap = offset.sqrMagnitude - bodyDiameter * bodyDiameter;
            float discriminant = projection * projection - lengthSquared * gap;
            if (discriminant < 0f) continue;
            float entry = gap <= 0f ? 0f : (-projection - Mathf.Sqrt(discriminant)) / lengthSquared;
            if (entry > fraction) continue;
            fraction = entry;
            contact = other;
        }

        GameBall gameBall = MatchController.FindMatch()?.GetBall();
        if (gameBall != null && gameBall.IsFree)
        {
            float playerBallDistance = gameBall.PlayerContactDistance;
            Vector2 offset = start - (Vector2)gameBall.transform.position;
            float projection = Vector2.Dot(offset, step);
            if (projection < 0f)
            {
                float gap = offset.sqrMagnitude - playerBallDistance * playerBallDistance;
                float discriminant = projection * projection - lengthSquared * gap;
                if (discriminant >= 0f)
                {
                    float entry = gap <= 0f ? 0f : (-projection - Mathf.Sqrt(discriminant)) / lengthSquared;
                    if (entry >= 0f && entry < fraction)
                    {
                        fraction = entry;
                        contact = null;
                    }
                }
            }
        }

        transform.position = ClampToCourt(start + step * fraction, court);
        return contact;
    }
}
