using UnityEngine;

// 기본 상태들: 등장 / 대기 / 포효(페이즈 2)

// 씬에 놓은 자리에서 용사 반대쪽으로 물러난 곳에서 날아 들어온다. 그동안 공격하지 않는다.
public class State_enter : Dragon_state
{

    Vector2 start;

    public State_enter(Dragon dragon) : base(dragon) { }

    // 날아 들어오는 동안은 무적. 자리 잡기 전에 맞는 건 싱겁다.
    public override bool is_invincible
    {
        get { return true; }
    }

    public override void Enter()
    {
        start = dragon.base_position;
        start.x -= dragon.FacingSign() * dragon.enter_distance;

        dragon.body.position = start;
        timer = dragon.enter_time;
    }

    public override void FixedTick(float dt)
    {
        bool done = CountDown(dt);
        MoveEased(start, dragon.base_position, dragon.enter_time);

        if (done) {
            dragon.ChangeState(new State_idle(dragon));
        }
    }
}

// 제자리 부유. 간격이 다 차면 패턴을 고른다.
// 페이즈 2 전환과 큰 화염구는 여기서만 시작한다. 쓸기 도중에 멈추면 공중에 붕 뜨니까.
public class State_idle : Dragon_state
{

    public State_idle(Dragon dragon) : base(dragon) { }

    public override bool is_hovering
    {
        get { return true; }
    }

    public override void FixedTick(float dt)
    {
        dragon.MoveTo(dragon.HoverPosition());

        if (!dragon.can_act) {
            return;
        }

        if (dragon.phase2_pending) {
            dragon.EnterPhase2();
            return;
        }

        if (dragon.bigfire_pending) {
            dragon.ChangeState(new State_spit(dragon));
            return;
        }

        dragon.attack_timer -= dt;
        if (dragon.attack_timer <= 0f) {
            dragon.attack_timer = dragon.NextAttackDelay();
            dragon.ChangeState(dragon.ChooseAttack());
        }
    }
}

// 페이즈 2 진입 포효. 무적이고 공격하지 않는다. 끝나면 바로 몰아친다.
public class State_roar : Dragon_state
{

    public State_roar(Dragon dragon) : base(dragon) { }

    public override bool is_invincible
    {
        get { return true; }
    }

    public override void Enter()
    {
        timer = dragon.phase2_roar_time;

        // 화면을 흔들고 붉게 번쩍이며 살짝 당기고, 잠깐 느려진다. 화난 걸 몸으로 느끼게.
        Camera_director.Shake(dragon.roar_shake_amplitude, dragon.roar_shake_time);
        Camera_director.ZoomPunch(dragon.roar_zoom_amount, dragon.roar_zoom_time);
        Camera_director.Flash(dragon.roar_flash_color, dragon.roar_flash_time);
        Camera_director.SlowMo(dragon.phase2_slowmo_scale, dragon.phase2_slowmo_time);

        // 판이 바뀐다: 화면의 작물이 바람에 날아가고, 하늘이 붉어지고 불씨가 뜬다. 스크롤도 영구히 빨라진다.
        Crop_flow.BlowAwayAll();
        Background.EnterPhase2();
    }

    public override void FixedTick(float dt)
    {
        dragon.MoveTo(dragon.HoverPosition());

        if (CountDown(dt)) {
            dragon.attack_timer = 0.5f;
            dragon.ChangeState(new State_idle(dragon));
        }
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.roar_color, pulse);
    }
}
