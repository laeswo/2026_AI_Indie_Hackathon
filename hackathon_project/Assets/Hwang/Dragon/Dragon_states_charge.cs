using UnityEngine;

// 돌진: 예고 → 위/아래 중 랜덤 높이로 이동 → 용사를 지나 화면 밖까지 → 위로 호를 그리며 복귀
//   아래(Low)  : 바닥 높이. 용사가 charge_low_safe_height 이상 떠 있어야 피한다 → 롱점프
//   위(High)   : 머리 위. 용사가 charge_high_hit_height 이상 떠 있으면 맞는다 → 가만히 서 있기
// 돌진하는 동안은 맞힐 수 있고 조준도 따라간다. 돌아올 때만 무적이고 조준은 제자리를 본다.
// 아래 돌진(run)은 작물이 흐르는 높이를 지나가므로 그동안 닿는 작물을 먹는다. 위 돌진·다이브·복귀는 안 먹는다.
// 데미지는 페이즈에 따라 다르다 (Dragon.ChargeDamage).

public enum Charge_lane
{
    Low,
    High
}

public class State_charge_telegraph : Dragon_state
{

    public State_charge_telegraph(Dragon dragon) : base(dragon) { }

    public override bool is_hovering
    {
        get { return true; }
    }

    public override void Enter()
    {
        timer = dragon.sweep_telegraph_time / dragon.SpeedScale();
    }

    public override void FixedTick(float dt)
    {
        dragon.MoveTo(dragon.HoverPosition());

        if (CountDown(dt)) {
            Charge_lane lane = Random.value < 0.5f ? Charge_lane.Low : Charge_lane.High;
            dragon.ChangeState(new State_charge_dive(dragon, lane));
        }
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.sweep_telegraph_color, pulse);
    }
}

// 제자리(용사 뒤)에서 돌진 높이로 이동한다.
public class State_charge_dive : Dragon_state
{

    readonly Charge_lane lane;
    Vector2 from;
    Vector2 lane_point;
    float sign;

    public State_charge_dive(Dragon dragon, Charge_lane lane) : base(dragon)
    {
        this.lane = lane;
    }

    public override void Enter()
    {
        sign = dragon.FacingSign();
        from = dragon.position;

        float offset = lane == Charge_lane.Low ? dragon.charge_low_offset : dragon.charge_high_offset;
        lane_point = new Vector2(dragon.base_position.x, dragon.GroundY() + offset);

        timer = dragon.sweep_dive_time;

        Debug.Log("돌진 시작 (" + (lane == Charge_lane.Low ? "아래 - 롱점프로 넘기" : "위 - 뛰지 말기") + ")");
    }

    public override void FixedTick(float dt)
    {
        bool done = CountDown(dt);
        MoveEased(from, lane_point, dragon.sweep_dive_time);

        if (done) {
            dragon.ChangeState(new State_charge_run(dragon, lane, sign));
        }
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.sweep_telegraph_color, 1f);
    }
}

// 돌진 높이로 용사를 지나 화면 밖까지 간다. 용사 옆을 지나는 순간 한 번만 판정한다.
public class State_charge_run : Dragon_state
{

    readonly Charge_lane lane;
    readonly float sign;
    float end_x;
    bool hit_done;

    public State_charge_run(Dragon dragon, Charge_lane lane, float sign) : base(dragon)
    {
        this.lane = lane;
        this.sign = sign;
    }

    // 바닥을 훑는 동안은 작물 높이를 지나가니 먹는다. 위 돌진은 작물 위를 지나므로 그대로 안 먹는다.
    public override bool can_eat
    {
        get { return lane == Charge_lane.Low; }
    }

    public override void Enter()
    {
        // 화면 밖으로 완전히 나간 뒤에 돌아오기 시작한다.
        end_x = sign > 0f
            ? World_scroll.RightX() + dragon.sweep_exit_margin
            : World_scroll.LeftX() - dragon.sweep_exit_margin;

        hit_done = false;
    }

    public override void FixedTick(float dt)
    {
        Vector2 position = dragon.position;
        position.x += sign * dragon.sweep_speed * dragon.SpeedScale() * dt;
        dragon.MoveTo(position);

        CheckHit(position);

        bool passed = sign > 0f ? position.x >= end_x : position.x <= end_x;
        if (passed) {
            dragon.ChangeState(new State_charge_return(dragon, position));
        }
    }

    void CheckHit(Vector2 dragon_position)
    {
        if (hit_done || dragon.player == null) {
            return;
        }

        float dx = Mathf.Abs(dragon_position.x - dragon.player.position.x);
        if (dx > dragon.sweep_hit_half_width) {
            return;
        }

        // 여기까지 왔으면 드래곤이 용사 바로 위/옆을 지나는 중. 한 번만 본다.
        hit_done = true;

        float height = dragon.player.position.y - dragon.GroundY();
        bool dodged;

        if (lane == Charge_lane.Low) {
            dodged = height >= dragon.charge_low_safe_height;
        }
        else {
            dodged = height < dragon.charge_high_hit_height;
        }

        if (dodged) {
            Debug.Log("돌진 회피 (높이 " + height.ToString("0.00") + ")");
            return;
        }

        Player_health health = dragon.PlayerHealth();
        if (health != null) {
            health.TakeHit(dragon.ChargeDamage());
        }
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.sweep_telegraph_color, 1f);
    }
}

// 화면 밖에서 위로 솟았다가 제자리로 내려오는 호. 무적이고 조준은 제자리를 본다.
public class State_charge_return : Dragon_state
{

    readonly Vector2 from;

    public State_charge_return(Dragon dragon, Vector2 from) : base(dragon)
    {
        this.from = from;
    }

    public override bool is_invincible
    {
        get { return dragon.invincible_while_returning; }
    }

    public override bool aims_at_home
    {
        get { return true; }
    }

    public override void Enter()
    {
        timer = dragon.sweep_return_time;
    }

    public override void FixedTick(float dt)
    {
        bool done = CountDown(dt);

        Vector2 hover = dragon.HoverPosition();
        float t = Mathf.SmoothStep(0f, 1f, 1f - timer / dragon.sweep_return_time);

        // 2차 베지어. 가운데 점을 화면 위 밖에 둔다.
        Vector2 arc_top = new Vector2(
            (from.x + hover.x) * 0.5f,
            World_scroll.TopY() + dragon.sweep_arc_height
        );
        float u = 1f - t;
        dragon.MoveTo(u * u * from + 2f * u * t * arc_top + t * t * hover);

        if (done) {
            // 돌아오자마자 쏘지 않도록 간격을 처음부터 다시 센다.
            GoIdle(true);
        }
    }
}
