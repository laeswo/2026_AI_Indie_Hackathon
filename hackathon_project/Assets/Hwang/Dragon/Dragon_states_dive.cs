using UnityEngine;

// 찍기 (1페이즈): 예고 → 왼쪽 위(씬에 놓은 자리)에서 용사 발밑으로 대각선으로 내리꽂힘 → 바닥에 박혀 잠깐 멈춤 → 부유 위치로 복귀
// 목표는 예고가 끝나는 순간의 용사 위치로 고정된다. 용사는 옆으로 못 움직이니 타이밍 맞춰 뛰어야 한다.
// 판정은 박히는 순간 한 번: 용사 x 가 dive_hit_half_width 안이고 발이 바닥에서 dive_hit_height 보다 낮으면 맞는다.
// 탭 점프 정점 근처면 넘고, 홀드 점프면 여유. 돌진(빨강)과 구분되게 분홍(dive_color).
// 박혀 있는 dive_stuck_time 동안 맞힐 수 있다. 예고만 is_hovering, 나머지는 먹지 않는다.

public class State_dive_telegraph : Dragon_state
{

    Vector2 from;
    float total;

    public State_dive_telegraph(Dragon dragon) : base(dragon) { }

    public override bool is_hovering
    {
        get { return true; }
    }

    public override void Enter()
    {
        // 왼쪽 위(씬에 놓은 자리)로 올라가며 깜빡인다. 거기서 내리꽂는다.
        from = dragon.position;
        total = dragon.dive_telegraph_time / dragon.SpeedScale();
        timer = total;

        Debug.Log("찍기 예고");
    }

    public override void FixedTick(float dt)
    {
        bool done = CountDown(dt);
        MoveEased(from, dragon.base_position, total);

        if (done) {
            dragon.ChangeState(new State_dive_strike(dragon));
        }
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.dive_color, pulse);
    }
}

// 용사 발밑으로 내리꽂힌다. 도착 순간 판정하고, 잠깐 박혀 있다가 올라간다.
public class State_dive_strike : Dragon_state
{

    Vector2 from;
    Vector2 target;     // 몸 중심이 멈추는 자리. 용사 x, 바닥 위에 몸이 얹힌 높이
    float total;
    bool struck;

    public State_dive_strike(Dragon dragon) : base(dragon) { }

    public override void Enter()
    {
        from = dragon.position;

        // 지금 이 순간의 용사 자리. 이후로는 안 따라간다.
        float target_x = dragon.player != null
            ? dragon.player.position.x
            : from.x + dragon.FacingSign() * dragon.breath_impact_distance;
        target = new Vector2(target_x, dragon.FloorY() + dragon.BodyRadius());

        total = Mathf.Max(0.01f, dragon.dive_time);
        timer = total;
        struck = false;

        Debug.Log("찍기 - 용사 발밑으로 (x " + target_x.ToString("0.0") + ")");
    }

    public override void FixedTick(float dt)
    {
        if (!struck) {
            bool done = CountDown(dt);

            // 점점 빨라지며 내리꽂힌다. t² 라 처음엔 느리고 끝에 확 박힌다.
            float t = Mathf.Clamp01(1f - timer / total);
            dragon.MoveTo(Vector2.Lerp(from, target, t * t));

            if (done) {
                Impact();
            }
            return;
        }

        // 박혀 있다. 맞힐 수 있는 시간.
        dragon.MoveTo(target);

        if (CountDown(dt)) {
            dragon.ChangeState(new State_slam_rise(dragon));
        }
    }

    void Impact()
    {
        struck = true;
        timer = dragon.dive_stuck_time;

        dragon.MoveTo(target);
        Camera_director.Shake(dragon.dive_shake_amplitude, dragon.dive_shake_time);
        Scene_lighting.Flash(target, dragon.dive_color, dragon.dive_flash_intensity, dragon.dive_flash_radius, dragon.dive_flash_time);

        CheckHit();

        Debug.Log("찍기 착지");
    }

    // 박히는 순간 한 번. 용사가 가로 범위 안이고 발이 낮으면 맞는다. 무적이면 TakeHit 이 알아서 무시한다.
    void CheckHit()
    {
        if (dragon.player == null) {
            return;
        }

        float dx = Mathf.Abs(dragon.player.position.x - target.x);
        if (dx > dragon.dive_hit_half_width) {
            return;
        }

        float foot_height = dragon.PlayerFootY() - dragon.FloorY();
        if (foot_height >= dragon.dive_hit_height) {
            Debug.Log("찍기 회피 (발 높이 " + foot_height.ToString("0.00") + ")");
            return;
        }

        Player_health health = dragon.PlayerHealth();
        if (health != null) {
            health.TakeHit(dragon.dive_damage);
        }
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.dive_color, 1f);
    }

    // 씬 뷰에 판정 박스. 박히는 자리의 바닥에서 dive_hit_height 높이.
    public override void DrawGizmos()
    {
        float floor = dragon.FloorY();
        Gizmos.color = dragon.dive_color;
        Gizmos.DrawWireCube(
            new Vector3(target.x, floor + dragon.dive_hit_height * 0.5f, 0f),
            new Vector3(dragon.dive_hit_half_width * 2f, dragon.dive_hit_height, 0f)
        );
    }
}
