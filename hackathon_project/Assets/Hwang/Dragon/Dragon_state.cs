using UnityEngine;

// 드래곤 상태의 공통 뼈대. 상태 하나가 매 물리 스텝 FixedTick 을 받아 몸을 옮기고, 다음 상태로 넘긴다.
// MonoBehaviour 가 아니라 평범한 클래스다. 드래곤(context)의 설정과 헬퍼를 빌려 쓴다.
public abstract class Dragon_state
{

    protected readonly Dragon dragon;

    // 대부분의 상태가 "몇 초 뒤 다음" 이라 타이머를 기본으로 갖는다.
    protected float timer;

    protected Dragon_state(Dragon dragon)
    {
        this.dragon = dragon;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public abstract void FixedTick(float dt);

    // 몸 색. pulse 는 0~1 로 빠르게 왕복하는 값이라 예고 깜빡임에 쓴다.
    public virtual Color GetColor(float pulse)
    {
        return dragon.normal_color;
    }

    // 작물이 통과하고 데미지도 안 받는다.
    public virtual bool is_invincible
    {
        get { return false; }
    }

    // 제자리에서 떠 있는 중. 예고·대기 상태들이 true.
    public virtual bool is_hovering
    {
        get { return false; }
    }

    // 흘러오는 작물을 먹을 수 있는가. 기본은 떠 있을 때만이고, 바닥을 훑는 돌진처럼 예외가 필요한 상태만 덮어쓴다.
    public virtual bool can_eat
    {
        get { return is_hovering; }
    }

    // 용사의 조준이 현재 위치 대신 제자리를 보게 한다 (머리 위를 지날 때 방향이 뒤집히지 않게).
    public virtual bool aims_at_home
    {
        get { return false; }
    }

    public virtual void DrawGizmos() { }

    // ---------- 자주 쓰는 도우미 ----------

    protected bool CountDown(float dt)
    {
        timer -= dt;
        return timer <= 0f;
    }

    // 대기로 돌아간다. reset_interval 이면 공격 간격을 처음부터 다시 센다.
    protected void GoIdle(bool reset_interval)
    {
        if (reset_interval) {
            dragon.attack_timer = dragon.NextAttackDelay();
        }

        dragon.ChangeState(new State_idle(dragon));
    }

    // from 에서 to 로 timer 가 다 될 때까지 감속하며 옮긴다. total 은 처음 timer 값.
    protected void MoveEased(Vector2 from, Vector2 to, float total)
    {
        float t = total > 0f ? 1f - timer / total : 1f;
        dragon.MoveTo(Vector2.Lerp(from, to, Dragon.EaseOut(t)));
    }
}
