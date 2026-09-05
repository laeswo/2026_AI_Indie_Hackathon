using UnityEngine;

// 내려찍기 (1페이즈): 예고 → 그 자리에서 바닥으로 뚝 떨어짐 → 착지하며 충격파 → 잠깐 눌러앉음 → 부유 위치로 복귀
// 충격파(Ground_eruption)는 착지점부터 용사 쪽으로 땅 조각이 한 칸씩 연달아 솟았다 가라앉는다. 낮아서 탭 점프로 넘는다.
// 떨어지는 동안과 눌러앉아 있는 동안 맞힐 수 있다. 눌러앉은 slam_stun_time 이 이 패턴의 대가다.
// 예고만 is_hovering 이고 나머지 동안은 먹지 않는다(can_eat 기본값 false).

public class State_slam_telegraph : Dragon_state
{

    public State_slam_telegraph(Dragon dragon) : base(dragon) { }

    public override bool is_hovering
    {
        get { return true; }
    }

    public override void Enter()
    {
        timer = dragon.slam_telegraph_time / dragon.SpeedScale();

        Debug.Log("내려찍기 예고");
    }

    public override void FixedTick(float dt)
    {
        dragon.MoveTo(dragon.HoverPosition());

        if (CountDown(dt)) {
            dragon.ChangeState(new State_slam_drop(dragon));
        }
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.slam_color, pulse);
    }
}

// 현재 x 그대로, 살짝 떠올랐다가 바닥까지 떨어진다.
// 떠오를 때는 EaseOut 으로 끝에서 멈칫하고, 떨어질 때는 t² 로 가속한다. 그래야 "쿵" 이 읽힌다.
public class State_slam_drop : Dragon_state
{

    enum Step { Lift, Fall }

    Step step;
    Vector2 from;
    Vector2 lift_point;    // 떠올라서 멈칫하는 자리
    Vector2 floor_point;   // 착지 지점(바닥 y). 충격파가 여기서 출발한다
    Vector2 land_point;    // 몸 중심이 멈추는 자리. 바닥 위에 몸이 얹힌다

    public State_slam_drop(Dragon dragon) : base(dragon) { }

    public override void Enter()
    {
        from = dragon.position;
        lift_point = from + Vector2.up * dragon.slam_lift_height;

        float floor_y = dragon.FloorY();
        floor_point = new Vector2(from.x, floor_y);
        land_point = new Vector2(from.x, floor_y + dragon.BodyRadius());

        step = Step.Lift;
        timer = dragon.slam_lift_time;
    }

    public override void FixedTick(float dt)
    {
        if (step == Step.Lift) {
            TickLift(dt);
        }
        else {
            TickFall(dt);
        }
    }

    void TickLift(float dt)
    {
        bool done = CountDown(dt);
        MoveEased(from, lift_point, dragon.slam_lift_time);

        if (done) {
            step = Step.Fall;
            from = lift_point;
            timer = dragon.slam_drop_time;
        }
    }

    void TickFall(float dt)
    {
        bool done = CountDown(dt);

        // MoveEased(EaseOut)는 도착이 느려져서 "떨어진다" 는 느낌이 안 난다. 반대로 t² 를 쓴다.
        float total = Mathf.Max(0.01f, dragon.slam_drop_time);
        float t = Mathf.Clamp01(1f - timer / total);
        dragon.MoveTo(Vector2.Lerp(from, land_point, t * t));

        if (done) {
            dragon.ChangeState(new State_slam_impact(dragon, floor_point, land_point));
        }
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.slam_color, 1f);
    }
}

// 착지. 충격파를 내고 잠깐 눌러앉아 있는다. 이때 맞힐 수 있다.
public class State_slam_impact : Dragon_state
{

    readonly Vector2 floor_point;
    readonly Vector2 land_point;

    public State_slam_impact(Dragon dragon, Vector2 floor_point, Vector2 land_point) : base(dragon)
    {
        this.floor_point = floor_point;
        this.land_point = land_point;
    }

    public override void Enter()
    {
        dragon.MoveTo(land_point);
        dragon.SpawnGroundEruption(floor_point);
        Camera_director.Shake(dragon.slam_shake_amplitude, dragon.slam_shake_time);
        Camera_director.ZoomPunch(dragon.slam_zoom_amount, dragon.slam_zoom_time);

        timer = dragon.slam_stun_time;

        Debug.Log("내려찍기 착지 - 충격파");
    }

    public override void FixedTick(float dt)
    {
        dragon.MoveTo(land_point);

        if (CountDown(dt)) {
            dragon.ChangeState(new State_slam_rise(dragon));
        }
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.slam_color, 1f);
    }
}

// 부유 위치로 돌아간다.
public class State_slam_rise : Dragon_state
{

    Vector2 from;

    // 올라간 뒤 attack_timer 에 넣을 값. 음수면 GoIdle(true) 로 간격을 처음부터 다시 센다.
    readonly float attack_timer_after;

    public State_slam_rise(Dragon dragon) : this(dragon, -1f) { }

    // 그로기 복귀처럼 "올라간 뒤 잠깐만 쉬고 다음 패턴" 이 필요할 때.
    public State_slam_rise(Dragon dragon, float attack_timer_after) : base(dragon)
    {
        this.attack_timer_after = attack_timer_after;
    }

    public override void Enter()
    {
        from = dragon.position;
        timer = dragon.slam_rise_time;
    }

    public override void FixedTick(float dt)
    {
        bool done = CountDown(dt);
        MoveEased(from, dragon.HoverPosition(), dragon.slam_rise_time);

        if (done) {
            if (attack_timer_after >= 0f) {
                dragon.attack_timer = attack_timer_after;
                dragon.ChangeState(new State_idle(dragon));
            }
            else {
                GoIdle(true);
            }
        }
    }
}
