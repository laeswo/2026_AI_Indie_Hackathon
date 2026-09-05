using UnityEngine;

// 밥먹기의 뒷부분: 작물을 eat_count 개 먹어 bigfire_pending 이 서면 대기에서 여기로 온다.
// 상태 하나가 두 가지 뱉기 방식을 번갈아 쓴다 (Dragon.next_spit_high). 첫 뱉기는 위, 다음은 아래, 그 다음은 위 …
//   Low  (아래 - 직선)  : 바닥에 앉음 → 보라색 깜빡임 → 수평으로 큰 화염구 → 일어나서 제자리로
//   High (위 - 포물선)  : 씬에 놓은 자리로 올라감 → 밝은 보라 깜빡임 → 중력을 받는 큰 화염구를 용사 발밑으로 → 제자리로
// 깜빡이는 동안은 맞힐 수 있다. 어느 모드든 발사 직후 eat_stack 을 비운다.
// 먹는 것 자체는 상태가 아니라 Dragon.TryEatCrops 가 부유 중에 상시로 한다. 이 상태 동안은 먹지 않는다(can_eat 기본값 false).

public class State_spit : Dragon_state
{

    public enum Spit_mode { High, Low }

    // 내려가기(올라가기) → 깜빡임 → 발사 → 복귀
    enum Step { Move_in, Telegraph, Move_out }

    Spit_mode mode;
    Step step;

    Vector2 from;
    Vector2 spit_point;

    public State_spit(Dragon dragon) : base(dragon) { }

    public override void Enter()
    {
        dragon.bigfire_pending = false;
        from = dragon.position;

        // 랜덤이면 한쪽만 연달아 나올 수 있어서 번갈아 쓴다. 읽고 나서 다음 차례를 위해 뒤집는다.
        mode = dragon.next_spit_high ? Spit_mode.High : Spit_mode.Low;
        dragon.next_spit_high = !dragon.next_spit_high;

        if (mode == Spit_mode.Low) {
            // 바닥에 몸이 닿게 앉는다.
            float floor_y = dragon.FloorY();
            spit_point = new Vector2(dragon.base_position.x, floor_y + dragon.BodyRadius());
        }
        else {
            // 씬에 놓은 제자리(부유의 위쪽 끝)에서 뱉는다.
            spit_point = dragon.base_position;
        }

        step = Step.Move_in;
        timer = dragon.sit_down_time;

        Debug.Log(mode == Spit_mode.High ? "뱉기 (위 - 포물선)" : "뱉기 (아래 - 직선)");
    }

    public override void FixedTick(float dt)
    {
        switch (step) {
            case Step.Move_in:
                TickMoveIn(dt);
                break;
            case Step.Telegraph:
                TickTelegraph(dt);
                break;
            case Step.Move_out:
                TickMoveOut(dt);
                break;
        }
    }

    void TickMoveIn(float dt)
    {
        bool done = CountDown(dt);
        MoveEased(from, spit_point, dragon.sit_down_time);

        if (done) {
            step = Step.Telegraph;
            timer = dragon.bigfire_ready_time / dragon.SpeedScale();
        }
    }

    void TickTelegraph(float dt)
    {
        // 자리를 지키며 깜빡인다.
        dragon.MoveTo(spit_point);

        if (CountDown(dt)) {
            Fire();
            dragon.eat_stack = 0;

            step = Step.Move_out;
            from = spit_point;
            timer = dragon.sit_up_time;
        }
    }

    void TickMoveOut(float dt)
    {
        bool done = CountDown(dt);
        MoveEased(from, dragon.HoverPosition(), dragon.sit_up_time);

        if (done) {
            GoIdle(true);
        }
    }

    void Fire()
    {
        if (mode == Spit_mode.Low) {
            dragon.FireBigStraight();
            return;
        }

        // 발사 순간 용사가 서 있는 발 위치를 노린다. 점프 중이어도 땅 기준.
        float target_x = dragon.player != null
            ? dragon.player.position.x
            : spit_point.x + dragon.FacingSign() * dragon.breath_impact_distance;
        Vector2 target = new Vector2(target_x, dragon.FloorY());

        dragon.FireBigArc(target);
    }

    Color ModeColor()
    {
        return mode == Spit_mode.High ? dragon.eat_high_color : dragon.eat_color;
    }

    public override Color GetColor(float pulse)
    {
        if (step == Step.Telegraph) {
            return dragon.Tint(ModeColor(), pulse);
        }

        return dragon.Tint(ModeColor(), 1f);
    }
}
