using UnityEngine;
using UnityEngine.InputSystem;

// 용사의 점프. 화염구를 피하는 유일한 수단이다.
// 용사에겐 Rigidbody 가 없으므로 물리 대신 직접 계산한다. 그래야 착지 높이가 딱 떨어진다.
public class Player_jump : MonoBehaviour
{

    public Key jump_key = Key.Space;

    // 목업의 10.4px/frame, 0.34px/frame² 을 유닛으로 환산한 값.
    // 물리 중력(9.81)보다 세게 잡아야 점프가 붕 뜨지 않고 가볍게 떨어진다.
    public float jump_velocity = 12.5f;
    public float gravity = 24.5f;

    public bool is_grounded { get; private set; }

    // 서 있던 높이. 착지 판정과 드래곤의 화염구 높이가 이 값을 본다.
    public float ground_y { get; private set; }

    float velocity_y;

    void Awake()
    {
        ground_y = transform.position.y;
        is_grounded = true;
    }

    void Update()
    {
        if (is_grounded && CanJump() && Keyboard.current[jump_key].wasPressedThisFrame) {
            velocity_y = jump_velocity;
            is_grounded = false;
        }

        if (is_grounded) {
            return;
        }

        velocity_y -= gravity * Time.deltaTime;

        Vector3 position = transform.position;
        position.y += velocity_y * Time.deltaTime;

        if (position.y <= ground_y) {
            position.y = ground_y;
            velocity_y = 0f;
            is_grounded = true;
        }

        transform.position = position;
    }

    bool CanJump()
    {
        return Keyboard.current != null && !Game_flow.is_over;
    }
}
