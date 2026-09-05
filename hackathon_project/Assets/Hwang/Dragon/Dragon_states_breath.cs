using UnityEngine;

// 브레스 (2페이즈): 예고 → 입에서 바닥으로 짧게 뿜고 → 바닥 불이 용사 쪽으로 천천히 퍼짐 → 유지 → 사라짐
// 상쇄 수단은 없다. 바닥 불이 닿기 전에 롱점프로 넘는 수밖에 없고, 맞으면 아프다.

public class State_breath_telegraph : Dragon_state
{

    public State_breath_telegraph(Dragon dragon) : base(dragon) { }

    public override bool is_hovering
    {
        get { return true; }
    }

    public override void Enter()
    {
        // 준비 시간은 읽을 여유를 주는 값이라 페이즈 배율을 안 건다.
        timer = dragon.breath_ready_time;
    }

    public override void FixedTick(float dt)
    {
        dragon.MoveTo(dragon.HoverPosition());

        if (CountDown(dt)) {
            dragon.ChangeState(new State_breath(dragon));
        }
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.breath_telegraph_color, pulse);
    }
}

// 뿜는 동안 제자리에 멈춘다. 줄기는 세모 프리팹, 바닥 불은 네모 프리팹을 늘려 그린다.
// 바닥 불은 높이가 일정한 띠라 탭 점프로는 못 넘고 길게 눌러야 넘는다. 판정은 보이는 네모와 똑같다.
public class State_breath : Dragon_state
{

    enum Phase
    {
        Shoot,
        Grow,
        Hold,
        Fade
    }

    Vector2 hold_position;
    Vector2 mouth;              // 뿜기 시작점

    // 불빛. 입에서 점 하나, 바닥 불은 띠와 같은 사각형 라이트(원이면 모서리가 어둡다). 띠가 길어지면 라이트도 같이 늘어난다.
    // 자식으로 두면 scale 을 타므로 따로 두고 위치만 맞춘다.
    Glow_light mouth_glow;
    Glow_light floor_glow;
    static readonly Color fire_glow_color = new Color(1f, 0.55f, 0.2f);
    const float mouth_glow_intensity = 1.0f;
    const float mouth_glow_radius = 2.2f;
    const float floor_glow_intensity = 1.1f;
    const float floor_glow_falloff = 1.2f;      // 띠 가장자리 바깥으로 부드럽게 번지는 폭
    Vector2 impact;             // 바닥에 떨어지는 점. 여기서부터 퍼진다
    float sign;                 // 퍼지는 방향 (+1 오른쪽, -1 왼쪽)
    float full_length;          // 다 퍼졌을 때 길이

    GameObject shoot_instance;  // 입에서 바닥으로 가는 줄기 (세모)
    SpriteRenderer shoot_renderer;
    Color shoot_base_color;
    GameObject floor_instance;  // 바닥 불 (네모)
    SpriteRenderer floor_renderer;
    Color floor_base_color;

    Phase phase;
    float phase_timer;
    bool hit_done;              // 브레스 한 번에 한 번만 맞는다

    public State_breath(Dragon dragon) : base(dragon) { }

    public override void Enter()
    {
        hold_position = dragon.position;
        sign = dragon.FacingSign();

        mouth = hold_position + new Vector2(dragon.fireball_offset_x * sign, 0f);

        // 드래곤 앞 바닥에 떨어진 뒤, 거기서부터 용사를 지나 조금 더 퍼진다. 뿜는 동안 길이는 고정이다.
        float floor_y = dragon.FloorY();
        impact = new Vector2(hold_position.x + sign * dragon.breath_impact_distance, floor_y);

        float player_x = dragon.player != null ? dragon.player.position.x : impact.x + sign * 8f;
        float to_player = (player_x - impact.x) * sign;
        full_length = Mathf.Max(0.5f, to_player + dragon.breath_extra_length);

        shoot_instance = Object.Instantiate(dragon.breath_prefab);
        shoot_instance.name = "Breath_shoot";
        shoot_renderer = shoot_instance.GetComponentInChildren<SpriteRenderer>();
        shoot_base_color = shoot_renderer != null ? shoot_renderer.color : Color.white;

        // 네모 프리팹이 없으면 세모로라도 그린다. 모양만 다르고 판정은 같다.
        GameObject floor_prefab = dragon.breath_floor_prefab != null ? dragon.breath_floor_prefab : dragon.breath_prefab;
        floor_instance = Object.Instantiate(floor_prefab);
        floor_instance.name = "Breath_floor";
        floor_renderer = floor_instance.GetComponentInChildren<SpriteRenderer>();
        floor_base_color = floor_renderer != null ? floor_renderer.color : Color.white;

        // 그림은 왼쪽→오른쪽으로 퍼지는 모양이라는 전제. 반대로 그렸으면 breath_art_faces_left 로 뒤집는다.
        Sprite_fit.Face(shoot_renderer, sign, dragon.breath_art_faces_left);
        Sprite_fit.Face(floor_renderer, sign, dragon.breath_art_faces_left);

        // 바닥 불은 텍스처가 반복되게 Tiled 를 먼저 시도한다. 안 되는 그림이면 그대로 두고 LayoutFloor 가 scale 로 늘린다.
        Sprite_fit.TryTile(floor_renderer, 0.01f, dragon.breath_height);

        mouth_glow = Scene_lighting.Attach(null, fire_glow_color, 0f, mouth_glow_radius);
        floor_glow = Scene_lighting.AttachShape(null, fire_glow_color, 0f, 0.01f, dragon.breath_height, floor_glow_falloff);

        // 바닥 불은 그림자를 드리운다. 용사·드래곤에 ShadowCaster2D 가 붙어 있다.
        UnityEngine.Rendering.Universal.Light2D floor_light = floor_glow.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
        if (floor_light != null) {
            floor_light.shadowsEnabled = true;
            floor_light.shadowIntensity = 0.5f;
        }

        phase = Phase.Shoot;
        phase_timer = 0f;
        hit_done = false;
        LayoutShoot(0f, 1f);
        LayoutFloor(0f, 0f);

        Debug.Log("브레스 시작");
    }

    public override void Exit()
    {
        if (floor_instance != null) {
            Object.Destroy(floor_instance);
        }
        if (shoot_instance != null) {
            Object.Destroy(shoot_instance);
        }
        if (mouth_glow != null) {
            Object.Destroy(mouth_glow.gameObject);
        }
        if (floor_glow != null) {
            Object.Destroy(floor_glow.gameObject);
        }
    }

    public override void FixedTick(float dt)
    {
        // 뿜는 동안은 제자리에 멈춘다. 맞히라고 주는 시간이다.
        dragon.MoveTo(hold_position);

        if (Advance(dt)) {
            GoIdle(true);
        }
    }

    float SpreadTime()
    {
        return dragon.breath_spread_time / dragon.SpeedScale();
    }

    // 한 번 뿜는 과정을 진행한다. 다 끝나면 true.
    bool Advance(float dt)
    {
        phase_timer += dt;

        switch (phase) {
            case Phase.Shoot: {
                float t = dragon.breath_shoot_time > 0f ? Mathf.Clamp01(phase_timer / dragon.breath_shoot_time) : 1f;
                LayoutShoot(t, 1f);

                if (t >= 1f) {
                    phase = Phase.Grow;
                    phase_timer = 0f;

                    // 바닥 불이 붙는 순간. 살짝 흔들고 주황으로 번쩍여서 이제 퍼진다는 걸 알린다.
                    Camera_director.Shake(dragon.breath_land_shake_amplitude, dragon.breath_land_shake_time);
                    Camera_director.Flash(dragon.breath_land_flash_color, dragon.breath_land_flash_time);
                }
                break;
            }

            case Phase.Grow: {
                float spread_time = SpreadTime();
                float t = spread_time > 0f ? Mathf.Clamp01(phase_timer / spread_time) : 1f;

                // 처음엔 빠르게 퍼지고 끝으로 갈수록 느려진다. 도달 직전을 읽을 시간을 준다.
                float fraction = Dragon.EaseOut(t);
                LayoutShoot(1f, 1f);
                LayoutFloor(fraction, 1f);
                TryHit(fraction);

                if (t >= 1f) {
                    phase = Phase.Hold;
                    phase_timer = 0f;
                }
                break;
            }

            case Phase.Hold:
                LayoutShoot(1f, 1f);
                LayoutFloor(1f, 1f);
                TryHit(1f);

                if (phase_timer >= dragon.breath_hold_time) {
                    phase = Phase.Fade;
                    phase_timer = 0f;
                }
                break;

            case Phase.Fade: {
                float t = dragon.breath_fade_time > 0f ? Mathf.Clamp01(phase_timer / dragon.breath_fade_time) : 1f;
                LayoutShoot(1f, 1f - t);
                LayoutFloor(1f, 1f - t);

                if (t >= 1f) {
                    return true;
                }
                break;
            }
        }

        return false;
    }

    static void ApplyAlpha(SpriteRenderer renderer, Color base_color, float alpha)
    {
        if (renderer == null) {
            return;
        }

        Color color = base_color;
        color.a *= alpha;
        renderer.color = color;
        renderer.enabled = alpha > 0f;
    }

    // 입에서 바닥 착지점으로 가는 줄기. 그림의 위쪽(꼭짓점) = 입, 아래쪽 = 착지점에 놓는다.
    // 로컬 +y(그림의 위)가 -direction 을 보도록 돌리고, 그림 bounds 로 폭·길이를 맞춘 뒤 윗변 가운데를 입에 붙인다.
    // 피벗이 어디든 결과가 같다. t 가 0 → 1 로 가며 늘어난다.
    void LayoutShoot(float t, float alpha)
    {
        if (shoot_instance == null) {
            return;
        }

        Vector2 axis = impact - mouth;
        float axis_length = axis.magnitude;
        Vector2 direction = axis_length > 0f ? axis / axis_length : Vector2.down;

        float length = Mathf.Max(0.01f, axis_length * t);
        float width = Mathf.Max(0.01f, dragon.breath_shoot_width * t);
        float angle = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;

        shoot_instance.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (shoot_renderer != null) {
            Sprite_fit.FitSize(shoot_renderer, width, length);
            Sprite_fit.AlignPoint(shoot_instance.transform, shoot_renderer, new Vector2(0.5f, 1f), mouth);
        }
        else {
            shoot_instance.transform.position = mouth + direction * (length * 0.5f);
            shoot_instance.transform.localScale = new Vector3(width, length, 1f);
        }

        ApplyAlpha(shoot_renderer, shoot_base_color, alpha);

        // 입 불빛. 뿜는 만큼 밝아지고, 옅어질 때 같이 꺼진다.
        if (mouth_glow != null) {
            mouth_glow.transform.position = mouth;
            mouth_glow.Set(fire_glow_color, mouth_glow_intensity * t * alpha);
        }
    }

    // 바닥에 깔린 띠. 착지점에서 용사 쪽으로 fraction 만큼 퍼져 있고, 높이는 일정하다.
    // 그림의 착지점 쪽 끝을 impact 에, 밑변을 바닥에 맞춘다. Tiled 가 되는 그림이면 size 로, 아니면 scale 로 늘어난다.
    // 판정(IsPlayerInFloorFire)은 impact·length·height 로 같은 사각형을 본다.
    void LayoutFloor(float fraction, float alpha)
    {
        if (floor_instance == null) {
            return;
        }

        float length = Mathf.Max(0.01f, full_length * fraction);
        float height = Mathf.Max(0.01f, dragon.breath_height);

        floor_instance.transform.rotation = Quaternion.identity;

        if (floor_renderer != null) {
            Sprite_fit.FitSize(floor_renderer, length, height);
            Sprite_fit.AlignPoint(floor_instance.transform, floor_renderer, new Vector2(sign > 0f ? 0f : 1f, 0f), impact);
        }
        else {
            floor_instance.transform.position = impact + new Vector2(sign * length * 0.5f, height * 0.5f);
            floor_instance.transform.localScale = new Vector3(length, height, 1f);
        }

        ApplyAlpha(floor_renderer, floor_base_color, alpha);

        // 바닥 불빛. 띠와 같은 사각형을 같은 자리에. 아직 안 퍼졌으면(fraction 0) 꺼져 있다.
        if (floor_glow != null) {
            Vector2 center = impact + new Vector2(sign * length * 0.5f, height * 0.5f);
            floor_glow.transform.position = center;
            floor_glow.SetRect(length, height);
            floor_glow.Set(fire_glow_color, fraction > 0f ? floor_glow_intensity * alpha : 0f);
        }
    }

    void TryHit(float fraction)
    {
        if (hit_done || !IsPlayerInFloorFire(fraction)) {
            return;
        }

        hit_done = true;

        Player_health health = dragon.PlayerHealth();
        if (health != null) {
            health.TakeHit(dragon.breath_damage);
        }
    }

    // 용사 몸 중심이 지금 퍼진 만큼의 띠 안에 있는지. LayoutFloor 가 그리는 네모와 같은 식이다.
    bool IsPlayerInFloorFire(float fraction)
    {
        if (dragon.player == null) {
            return false;
        }

        float length = full_length * fraction;
        if (length <= 0f) {
            return false;
        }

        Vector2 p = dragon.player.position;
        float pad = dragon.breath_player_radius;

        float along = (p.x - impact.x) * sign;
        if (along < -pad || along > length + pad) {
            return false;
        }

        return p.y >= impact.y - pad && p.y <= impact.y + dragon.breath_height + pad;
    }

    float CurrentFraction()
    {
        if (phase == Phase.Grow) {
            float spread_time = SpreadTime();
            return spread_time > 0f ? Dragon.EaseOut(Mathf.Clamp01(phase_timer / spread_time)) : 1f;
        }

        return 1f;
    }

    // 씬 뷰에서 판정 영역을 확인할 수 있게 그린다. 네모 그림과 겹쳐 보여야 정상이다.
    public override void DrawGizmos()
    {
        if (phase == Phase.Shoot) {
            return;
        }

        float fraction = CurrentFraction();
        float length = full_length * fraction;
        Vector3 center = impact + new Vector2(sign * length * 0.5f, dragon.breath_height * 0.5f);

        Gizmos.color = IsPlayerInFloorFire(fraction) ? Color.red : Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(length, dragon.breath_height, 0f));
    }

    public override Color GetColor(float pulse)
    {
        return dragon.Tint(dragon.breath_telegraph_color, 1f);
    }
}
