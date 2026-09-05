using UnityEngine;

// 날갯짓 (2페이즈): 예고 → 바람. 바람이 부는 동안 작물과 배경이 wind_speed_multiplier 배로 빨라진다.
// 데미지는 없다. 작물이 휙휙 지나가서 줍기 어려워지는 게 전부다.
// World_scroll.speed_multiplier 를 올렸다가 끝나면(어떤 이유로 끊겨도) Exit 에서 되돌린다.

public class State_wing_telegraph : Dragon_state
{

    public State_wing_telegraph(Dragon dragon) : base(dragon) { }

    public override bool is_hovering
    {
        get { return true; }
    }

    public override void Enter()
    {
        timer = dragon.wing_telegraph_time / dragon.SpeedScale();
    }

    public override void FixedTick(float dt)
    {
        dragon.MoveTo(dragon.HoverPosition());

        if (CountDown(dt)) {
            dragon.ChangeState(new State_wing(dragon));
        }
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.wing_color, pulse);
    }
}

public class State_wing : Dragon_state
{

    public State_wing(Dragon dragon) : base(dragon) { }

    public override bool is_hovering
    {
        get { return true; }
    }

    public override void Enter()
    {
        timer = dragon.wing_duration;
        World_scroll.Get().speed_multiplier = dragon.wind_speed_multiplier;

        // 하얀 바람 줄기가 화면을 가로질러 왼쪽으로 지나간다. 세계가 빨라진 만큼 더 많이, 더 빨리.
        Wind_effect.Begin(dragon.wind_speed_multiplier);

        Debug.Log("날갯짓 - 바람 " + dragon.wind_speed_multiplier + "배, " + dragon.wing_duration + "초");
    }

    public override void Exit()
    {
        World_scroll.Get().speed_multiplier = 1f;
        Wind_effect.End();
    }

    public override void FixedTick(float dt)
    {
        dragon.MoveTo(dragon.HoverPosition());

        if (CountDown(dt)) {
            GoIdle(true);
        }
    }

    public override Color GetColor(float pulse)
    {
        // 바람 부는 동안 빠르게 깜빡여서 날갯짓하는 느낌을 준다.
        float flap = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
        return dragon.Tint(dragon.wing_color, flap * 0.6f);
    }
}
