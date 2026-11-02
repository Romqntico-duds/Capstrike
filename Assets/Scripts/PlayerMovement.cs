using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    public float speed = 3f;
    public InputActionAsset inputActions;
    public VirtualJoystick joystickPrefab;
    InputAction moveAction;
    VirtualJoystick joystick;

    void Awake()
    {
        moveAction = inputActions.FindAction("Player/Move", true);
    }

    void OnEnable()
    {
        moveAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
    }

    public override void OnNetworkSpawn()
    {
        GetComponent<SpriteRenderer>().color = IsOwner ? Color.cyan : new Color(1f, 0.5f, 0f);
        if (IsOwner)
        {
            transform.position = new Vector3(((int)(OwnerClientId % 4) - 1.5f) * 2f, 0f, 0f);
            joystick = Instantiate(joystickPrefab);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (joystick != null)
            Destroy(joystick.gameObject);
    }

    void Update()
    {
        if (!IsOwner || !IsSpawned) return;

        Vector2 direction = moveAction.ReadValue<Vector2>();
        if (joystick != null)
            direction += joystick.Value;

        transform.position += (Vector3)Vector2.ClampMagnitude(direction, 1f) * speed * Time.deltaTime;
        transform.position = new Vector3(Mathf.Clamp(transform.position.x, -7.5f, 7.5f), Mathf.Clamp(transform.position.y, -3.5f, 3.5f), 0f);
    }
}
