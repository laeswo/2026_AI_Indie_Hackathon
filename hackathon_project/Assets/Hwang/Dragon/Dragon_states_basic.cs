using UnityEngine;

// 기본 상태들: 등장 / 대기 / 포효(페이즈 2) / 상쇄(그로기) / 격추

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

        // 마지막 패턴. HP 가 1 에 묶인 뒤 하던 패턴을 끝내고 제자리로 왔을 때 시작한다. 페이즈 2 전환보다는 뒤.
        if (dragon.final_pending) {
            dragon.StartFinalPattern();
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
        Sound_bank.Play("phase_roar_sound", dragon.transform.position);

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

// 상쇄(그로기). 성검에 맞으면 하던 패턴이 그 자리에서 끊기고 여기로 온다. Dragon.EnterStagger 가 부른다.
// 무방비: 무적 아님, 안 먹음, 받는 데미지 2배(Dragon.TakeDamage). 제자리에서 천천히 아래로 처지며 회색으로 깜빡인다.
// 이전 상태의 Exit 가 불·바람·뱉기 이펙트를 치우고, 큰 화염구·충격파는 EnterStagger 가 치운다.
// 뱉기 도중 끊긴 건 그냥 취소다 (State_spit.Enter 가 bigfire_pending 을 이미 내렸고 되살리지 않는다. eat_stack 은 남는다).
// phase2_pending 은 그대로 두고, 끝나서 Idle 로 가면 거기서 처리한다.
public class State_stagger : Dragon_state
{

    readonly string source;
    readonly float duration;
    readonly bool counter;     // 성검 상쇄(연출 전부) 인가, 뱉고 지친 것(연출 없음) 인가
    Vector2 from;
    Vector2 rest;

    // 성검 상쇄. stagger_time 동안, 연출 전부.
    public State_stagger(Dragon dragon, string source) : this(dragon, source, dragon.stagger_time, true) { }

    // 시간과 연출 여부를 정해서. 뱉고 나서 지친 그로기는 counter = false.
    public State_stagger(Dragon dragon, string source, float duration, bool counter) : base(dragon)
    {
        this.source = source;
        this.duration = Mathf.Max(0.1f, duration);
        this.counter = counter;
    }

    // 전부 기본값(false)이지만 "무방비" 가 이 상태의 정의라 눈에 보이게 적어 둔다.
    public override bool is_invincible
    {
        get { return false; }
    }

    public override bool can_eat
    {
        get { return false; }
    }

    public override void Enter()
    {
        // 돌진 복귀(화면 밖) 중에 맞았을 수 있다. 보이는 곳으로 당겨서 거기서 처진다.
        from = dragon.position;
        from.x = Mathf.Clamp(from.x, World_scroll.LeftX() + dragon.stagger_edge_margin, World_scroll.RightX() - dragon.stagger_edge_margin);
        rest = from + Vector2.down * dragon.stagger_sink;

        // 바닥에 앉아 뱉은 뒤라면 더 처질 데가 없다. 땅속으로 파고들지 않게.
        rest.y = Mathf.Max(rest.y, dragon.FloorY() + dragon.BodyRadius());
        dragon.MoveTo(from);

        timer = duration;

        if (counter) {
            PlayEffects();
            Debug.Log("상쇄! " + source + " - " + duration + "초 무방비");
        }
        else {
            Debug.Log("지침! " + source + " - " + duration + "초 무방비");
        }
    }

    // 이 게임에서 제일 센 한 방. 멈칫 → 느려짐 → 흔들림 → 당김 → 금빛 번쩍 → 라이트 → 글자 → 몸 하얗게.
    void PlayEffects()
    {
        Vector3 position = dragon.transform.position;
        Color gold = dragon.stagger_gold;
        Color gold_flash = gold;
        gold_flash.a = 0.6f;

        Camera_director.HitStop(0.12f);
        Camera_director.SlowMo(0.25f, 0.8f);
        Camera_director.Shake(0.4f, 0.5f);
        Camera_director.ZoomPunch(0.12f, 0.6f);
        Camera_director.Flash(gold_flash, 0.35f);
        Scene_lighting.Flash(position, gold, 1.2f, 6f, 0.6f);
        Popup_text.ShowText((Vector2)position + Vector2.up * 1.5f, "상쇄!", gold);

        dragon.FlashWhite(dragon.stagger_flash_time);
    }

    public override void FixedTick(float dt)
    {
        bool done = CountDown(dt);

        // 천천히 아래로 처진다. 끝으로 갈수록 느리게.
        MoveEased(from, rest, duration);

        if (done) {
            Debug.Log(counter ? "상쇄 해제" : "그로기 해제");

            // 그 자리에서 부유 위치로 부드럽게 올라간다. 바로 Idle 로 가면 HoverPosition 으로 순간이동한다.
            // 올라간 뒤 바로 다음 패턴이 나오지 않게 짧은 여유만 준다.
            dragon.ChangeState(new State_slam_rise(dragon, 0.8f));
        }
    }

    // 회색에 살짝 깜빡임.
    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.stagger_color, 0.8f + 0.2f * pulse);
    }
}

// 격추. Dragon.TakeDamage 가 HP 0 에서 넣는다. 그 자리에서 가속하며 바닥으로 떨어져 붙어 있고, 그 뒤로는 아무것도 안 한다.
// Dead 클립(구르다 쓰러짐)은 TakeDamage 가 먼저 돌려 두고, 프레임마다 그림 높이가 달라도 밑변이 바닥에 붙도록 매 틱 다시 맞춘다.
// 무적이라 작물이 그냥 지나간다. 먹지도 않는다.
public class State_dead : Dragon_state
{

    Vector2 from;

    public State_dead(Dragon dragon) : base(dragon) { }

    public override bool is_invincible
    {
        get { return true; }
    }

    public override bool can_eat
    {
        get { return false; }
    }

    public override void Enter()
    {
        from = dragon.position;
        timer = dragon.dead_fall_time;
    }

    public override void FixedTick(float dt)
    {
        // 밑변이 바닥에 닿는 몸 중심 높이. 그림이 바뀌면 같이 바뀐다.
        float rest_y = dragon.FloorY() + dragon.SpriteBottomOffset();

        float y;
        if (timer > 0f) {
            CountDown(dt);
            float total = Mathf.Max(0.01f, dragon.dead_fall_time);
            float t = Mathf.Clamp01(1f - timer / total);
            y = Mathf.Lerp(from.y, rest_y, t * t);   // 점점 빨라지며 떨어진다
        }
        else {
            y = rest_y;
        }

        dragon.MoveTo(new Vector2(from.x, y));
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.dead_color, 0.6f);
    }
}
