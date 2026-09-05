using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// 용사의 점프. 화염구를 피하는 유일한 수단이다.
// 용사에겐 Rigidbody 가 없으므로 물리 대신 직접 계산한다. 그래야 착지 높이가 딱 떨어진다.
//
// 누르는 길이에 따라 높이가 달라진다:
//   탭          → jump_velocity 로 튀어올라 원래 중력으로 떨어진다 (가장 낮은 점프)
//   누르고 있음 → 그동안은 중력이 hold_gravity_scale 배로 약해져서 더 올라간다
//   한계        → max_hold_time 이 지나거나, 손을 떼거나, 정점을 지나면 원래 중력으로 돌아온다
// 즉 max_hold_time 이 높이의 상한이다. 아무리 오래 눌러도 그 이상 안 올라간다.
public class Player_jump : MonoBehaviour
{

    [Header("점프")]
    public Key jump_key = Key.Space;

    public float jump_velocity = 9f;
    float gravity = 24.5f;

    float hold_gravity_scale = 0.3f;
    public float max_hold_time = 0.35f;

    public bool is_grounded { get; private set; }

    // 서 있던 높이. 착지 판정과 드래곤의 화염구 높이가 이 값을 본다.
    public float ground_y { get; private set; }

    float velocity_y;
    float hold_timer;
    bool is_boosting;

    // 있으면 jump 트리거와 grounded 를 넣는다. 없어도 된다.
    Animator animator;

    void Awake()
    {
        ground_y = transform.position.y;
        is_grounded = true;
        animator = GetComponentInChildren<Animator>();

        // 인스펙터 값을 만질 때 결과 높이를 바로 볼 수 있게 한 번 찍어둔다.
        Debug.Log("점프 높이 - 탭 " + TapHeight().ToString("0.00") + " / 최대 " + MaxHeight().ToString("0.00"));
    }

    void Update()
    {
        Step();

        // 애니메이션이 있으면 서 있는지 알려준다.
        Sprite_fit.SetBool(animator, "grounded", is_grounded);
    }

    void Step()
    {
        KeyControl key = Keyboard.current != null ? Keyboard.current[jump_key] : null;

        if (is_grounded) {
            if (key != null && key.wasPressedThisFrame && !Game_flow.is_over) {
                velocity_y = jump_velocity;
                is_grounded = false;
                is_boosting = true;
                hold_timer = 0f;

                Sprite_fit.Trigger(animator, "jump");
            }
            else {
                return;
            }
        }

        // 누르고 있는 동안만 중력을 약하게 한다. 떼거나, 한계 시간이 지나거나, 정점을 지나면 끝.
        if (is_boosting) {
            hold_timer += Time.deltaTime;

            bool released = key == null || !key.isPressed;
            if (released || hold_timer >= max_hold_time || velocity_y <= 0f) {
                is_boosting = false;
            }
        }

        float current_gravity = is_boosting ? gravity * hold_gravity_scale : gravity;
        velocity_y -= current_gravity * Time.deltaTime;

        Vector3 position = transform.position;
        position.y += velocity_y * Time.deltaTime;

        if (position.y <= ground_y) {
            position.y = ground_y;
            velocity_y = 0f;
            is_grounded = true;
            is_boosting = false;
        }

        transform.position = position;
    }

    // 아래 둘은 튜닝용. 실제 점프는 프레임마다 적분하므로 소수점 둘째 자리쯤에서 약간 다를 수 있다.

    public float TapHeight()
    {
        return jump_velocity * jump_velocity / (2f * gravity);
    }

    public float MaxHeight()
    {
        float hold_gravity = gravity * hold_gravity_scale;

        // 약한 중력 구간이 정점 전에 끝나는지, 정점에서 끝나는지 중 빠른 쪽.
        float hold_time = hold_gravity > 0f ? Mathf.Min(max_hold_time, jump_velocity / hold_gravity) : max_hold_time;

        float velocity_after = jump_velocity - hold_gravity * hold_time;
        float height_during = jump_velocity * hold_time - 0.5f * hold_gravity * hold_time * hold_time;

        return height_during + velocity_after * velocity_after / (2f * gravity);
    }
}
