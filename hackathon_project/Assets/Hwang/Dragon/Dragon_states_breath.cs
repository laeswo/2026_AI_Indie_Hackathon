using UnityEngine;

// 브레스 (2페이즈): 예고 → 착지점 앞까지 내려와 앉음 → 입 벌리고(breath 클립) → 바닥 불이 용사 쪽으로 천천히 퍼짐 → 유지 → 사라짐 → 제자리로
// 입에서 바닥으로 가는 줄기 그림은 없다. 드래곤 자체의 브레스 애니메이션이 입에서 뿜는 걸 보여주고, 바닥 불은 입 바로 앞에서 시작한다.
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

// 앞으로 내려와 앉아서 뿜는다. 바닥 불은 네모 프리팹(breath_two)에 firefloor 플립북을 붙여 늘려 그린다.
// 바닥 불은 높이가 일정한 띠라 탭 점프로는 못 넘고 길게 눌러야 넘는다. 판정은 보이는 네모와 똑같다.
public class State_breath : Dragon_state
{

    enum Phase
    {
        Approach,   // 부유 위치에서 착지점 앞(바닥)으로 내려온다
        Shoot,      // 입 벌림. 바닥 불이 붙기 전 잠깐
        Grow,       // 바닥 불이 용사 쪽으로 퍼진다
        Hold,
        Fade,
        Return      // 부유 위치로 돌아간다
    }

    Vector2 from;               // 내려오기 시작한 자리
    Vector2 stand;              // 뿜는 동안 서 있는 자리. 입이 착지점 위에 온다

    // 불빛. 입에서 점 하나, 바닥 불은 띠와 같은 사각형 라이트(원이면 모서리가 어둡다). 띠가 길어지면 라이트도 같이 늘어난다.
    // 자식으로 두면 scale 을 타므로 따로 두고 위치만 맞춘다.
    Glow_light mouth_glow;
    Glow_light floor_glow;

    // 바닥 불 플립북 클립 이름 (Resources/Dragon_Breath/fire/firefloor.anim → clips.txt)
    const string floor_clip_name = "firefloor";
    static readonly Color fire_glow_color = new Color(1f, 0.55f, 0.2f);
    const float mouth_glow_intensity = 1.0f;
    const float mouth_glow_radius = 2.2f;
    const float floor_glow_intensity = 1.1f;
    const float floor_glow_falloff = 1.2f;      // 띠 가장자리 바깥으로 부드럽게 번지는 폭
    static readonly Color placeholder_floor_color = new Color(1f, 0.45f, 0.1f);

    Vector2 impact;             // 바닥 불이 시작되는 점. 입 바로 앞
    float sign;                 // 퍼지는 방향 (+1 오른쪽, -1 왼쪽)
    float full_length;          // 다 퍼졌을 때 길이

    GameObject floor_instance;  // 바닥 불 (네모)
    SpriteRenderer floor_renderer;
    Color floor_base_color;

    Phase phase;
    float phase_timer;
    bool hit_done;              // 브레스 한 번에 한 번만 맞는다

    public State_breath(Dragon dragon) : base(dragon) { }

    public override void Enter()
    {
        from = dragon.position;
        sign = dragon.FacingSign();

        // 착지점은 부유 자리 앞 breath_impact_distance. 드래곤은 입이 그 위에 오게 바닥에 내려와 앉는다.
        float floor_y = dragon.FloorY();
        impact = new Vector2(from.x + sign * dragon.breath_impact_distance, floor_y);
        stand = new Vector2(impact.x - sign * dragon.fireball_offset_x, floor_y + dragon.BodyRadius());

        // 착지점에서 용사를 지나 조금 더 퍼진다. 뿜는 동안 길이는 고정이다.
        float player_x = dragon.player != null ? dragon.player.position.x : impact.x + sign * 8f;
        float to_player = (player_x - impact.x) * sign;
        full_length = Mathf.Max(0.5f, to_player + dragon.breath_extra_length);

        floor_instance = MakeFloor();
        floor_renderer = floor_instance.GetComponentInChildren<SpriteRenderer>();
        floor_base_color = floor_renderer != null ? floor_renderer.color : Color.white;

        // 바닥 불 애니메이션. Resources/Dragon_Breath/fire 의 firefloor 클립(clips.txt 에 구워진 것)을 무한 반복한다.
        // 클립이 없으면 프리팹 그림 그대로. 진짜 그림이면 프리팹의 빨강 틴트는 쓰지 않는다.
        float floor_fps;
        Sprite[] floor_frames = Dragon_animation.LoadClipFrames(floor_clip_name, out floor_fps);
        if (floor_renderer != null && floor_frames != null && floor_frames.Length > 0) {
            floor_renderer.color = Color.white;
            floor_base_color = Color.white;
            Sprite_flipbook.Attach(floor_renderer, floor_frames, floor_fps);
        }

        // 그림은 왼쪽→오른쪽으로 퍼지는 모양이라는 전제. 반대로 그렸으면 breath_art_faces_left 로 뒤집는다.
        Sprite_fit.Face(floor_renderer, sign, dragon.breath_art_faces_left);

        // 불꽃 그림 한 장이 곧 띠 하나다. Tiled 로 반복하면 같은 불이 옆으로 여러 번 찍히므로 Simple 로 고정하고,
        // LayoutFloor 가 scale 로 띠 길이만큼 늘린다. 프리팹이 Tiled 로 저장돼 있어도 여기서 되돌린다.
        if (floor_renderer != null) {
            floor_renderer.drawMode = SpriteDrawMode.Simple;
            Sprite_fit.FitSize(floor_renderer, 0.01f, dragon.breath_height);
        }

        mouth_glow = Scene_lighting.Attach(null, fire_glow_color, 0f, mouth_glow_radius);
        floor_glow = Scene_lighting.AttachShape(null, fire_glow_color, 0f, 0.01f, dragon.breath_height, floor_glow_falloff);

        // 바닥 불은 그림자를 드리운다. 용사·드래곤에 ShadowCaster2D 가 붙어 있다.
        UnityEngine.Rendering.Universal.Light2D floor_light = floor_glow.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
        if (floor_light != null) {
            floor_light.shadowsEnabled = true;
            floor_light.shadowIntensity = 0.5f;
        }

        phase = Phase.Approach;
        phase_timer = 0f;
        hit_done = false;
        LayoutFloor(0f, 0f);
        LayoutMouthGlow(0f);

        Debug.Log("브레스 - 앞으로 내려옴");
    }

    // 바닥 불 오브젝트. 프리팹이 비어 있으면 임시 주황 네모(Awake 에서 이미 경고했다).
    GameObject MakeFloor()
    {
        GameObject instance;
        if (dragon.breath_floor_prefab != null) {
            instance = Object.Instantiate(dragon.breath_floor_prefab);
        }
        else {
            instance = new GameObject();
            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = Placeholder_sprite.Circle();
            renderer.color = placeholder_floor_color;
            Scene_lighting.ApplyLitMaterial(renderer);
        }

        instance.name = "Breath_floor";
        return instance;
    }

    public override void Exit()
    {
        dragon.StopAnimationClip();

        if (floor_instance != null) {
            Object.Destroy(floor_instance);
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
        if (Advance(dt)) {
            GoIdle(true);
        }
    }

    float SpreadTime()
    {
        return dragon.breath_spread_time / dragon.SpeedScale();
    }

    // 지금 입 위치. 몸 중심에서 용사 쪽으로 fireball_offset_x.
    Vector2 Mouth()
    {
        return dragon.position + new Vector2(dragon.fireball_offset_x * sign, dragon.breath_mouth_offset_y);
    }

    // 한 번 뿜는 과정을 진행한다. 다 끝나면 true.
    bool Advance(float dt)
    {
        phase_timer += dt;

        switch (phase) {
            case Phase.Approach: {
                // 부유 자리에서 착지점 앞 바닥으로. 도착이 부드럽게.
                float total = Mathf.Max(0.01f, dragon.breath_approach_time);
                float t = Mathf.Clamp01(phase_timer / total);
                dragon.MoveTo(Vector2.Lerp(from, stand, Dragon.EaseOut(t)));

                if (t >= 1f) {
                    phase = Phase.Shoot;
                    phase_timer = 0f;

                    // 입 벌리고 뿜는 그림. 불이 다 사라질 때까지(Exit) 마지막 프레임에 머문다.
                    dragon.PlayAnimationClip("breath", true);
                    Sound_bank.Play("flame_sound", dragon.transform.position);
                    Debug.Log("브레스 시작");
                }
                break;
            }

            case Phase.Shoot: {
                dragon.MoveTo(stand);

                float t = dragon.breath_shoot_time > 0f ? Mathf.Clamp01(phase_timer / dragon.breath_shoot_time) : 1f;
                LayoutMouthGlow(t);

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
                dragon.MoveTo(stand);

                float spread_time = SpreadTime();
                float t = spread_time > 0f ? Mathf.Clamp01(phase_timer / spread_time) : 1f;

                // 처음엔 빠르게 퍼지고 끝으로 갈수록 느려진다. 도달 직전을 읽을 시간을 준다.
                float fraction = Dragon.EaseOut(t);
                LayoutMouthGlow(1f);
                LayoutFloor(fraction, 1f);
                TryHit(fraction);

                if (t >= 1f) {
                    phase = Phase.Hold;
                    phase_timer = 0f;
                }
                break;
            }

            case Phase.Hold:
                dragon.MoveTo(stand);
                LayoutMouthGlow(1f);
                LayoutFloor(1f, 1f);
                TryHit(1f);

                if (phase_timer >= dragon.breath_hold_time) {
                    phase = Phase.Fade;
                    phase_timer = 0f;
                }
                break;

            case Phase.Fade: {
                dragon.MoveTo(stand);

                float t = dragon.breath_fade_time > 0f ? Mathf.Clamp01(phase_timer / dragon.breath_fade_time) : 1f;
                LayoutMouthGlow(1f - t);
                LayoutFloor(1f, 1f - t);

                if (t >= 1f) {
                    phase = Phase.Return;
                    phase_timer = 0f;
                    dragon.StopAnimationClip();
                }
                break;
            }

            case Phase.Return: {
                // 부유 위치로. 목표가 계속 움직이므로 매 스텝 다시 본다.
                float total = Mathf.Max(0.01f, dragon.breath_return_time);
                float t = Mathf.Clamp01(phase_timer / total);
                dragon.MoveTo(Vector2.Lerp(stand, dragon.HoverPosition(), Dragon.EaseOut(t)));

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

    // 입 불빛. 입을 따라다니고, 뿜는 만큼 밝아지고 옅어질 때 같이 꺼진다.
    void LayoutMouthGlow(float strength)
    {
        if (mouth_glow == null) {
            return;
        }

        mouth_glow.transform.position = Mouth();
        mouth_glow.Set(fire_glow_color, mouth_glow_intensity * Mathf.Clamp01(strength));
    }

    // 바닥에 깔린 띠. 착지점에서 용사 쪽으로 fraction 만큼 퍼져 있고, 높이는 일정하다.
    // 그림의 착지점 쪽 끝을 impact 에, 밑변을 바닥에 맞춘다. 불꽃 한 장을 scale 로 띠 길이만큼 늘린다(반복 없음).
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

    // 판정은 "불의 앞머리가 용사 자리에 닿는 순간" 한 번만. 그때 떠 있으면 피한 것이고, 그 뒤 불이 밑에 깔려 있어도 다시 맞지 않는다.
    // 계속 판정하면 착지하는 순간 맞아서 스페이스 한 번으로는 못 피한다. 타이밍 게임으로 만든다.
    void TryHit(float fraction)
    {
        if (hit_done || dragon.player == null) {
            return;
        }

        float length = full_length * fraction;
        float along = (dragon.player.position.x - impact.x) * sign;

        // 아직 앞머리가 용사 자리까지 못 왔다.
        if (along > length) {
            return;
        }

        // 닿는 순간. 맞든 피하든 이번 브레스 판정은 여기서 끝.
        hit_done = true;

        if (!IsPlayerInFloorFire(fraction)) {
            Debug.Log("브레스 회피 (높이 " + (dragon.player.position.y - impact.y).ToString("0.00") + ")");
            return;
        }

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
        if (phase == Phase.Approach || phase == Phase.Shoot) {
            return 0f;
        }
        if (phase == Phase.Grow) {
            float spread_time = SpreadTime();
            return spread_time > 0f ? Dragon.EaseOut(Mathf.Clamp01(phase_timer / spread_time)) : 1f;
        }

        return 1f;
    }

    // 씬 뷰에서 판정 영역을 확인할 수 있게 그린다. 네모 그림과 겹쳐 보여야 정상이다.
    public override void DrawGizmos()
    {
        float fraction = CurrentFraction();
        if (fraction <= 0f) {
            return;
        }

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
