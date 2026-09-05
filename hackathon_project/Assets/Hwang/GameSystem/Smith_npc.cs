using UnityEngine;

// 대장장이 NPC. 성검 뒤를 따라 걸어 들어와 화면 오른쪽 자리(stop_margin)에 멈춰 서서 "던지지 말라"고 한다.
// 성검은 그대로 흘러가고 대장장이는 그 자리에 계속 서 있다.
// 용사가 성검을 던지면 멈칫하며 뒤로 기울어 배신당한 대사를 하고, 대사가 끝나면 옅어지며 사라진다.
// 던지지 않으면(안 줍거나 계속 들고 있으면) 사라지지 않고 계속 서 있다.
//
// Rigidbody · 콜라이더 · Crop_flow 없음. 판정도 없고 작물로 주워지지도 않는다. 위치는 여기서 직접 옮긴다.
// 스폰은 Spawn_crop 이 한다 (성검 오른쪽 1.2 유닛, 같은 높이). 프리팹은 그림만 있으면 된다.
// 대사 타이머와 반응 연출은 unscaled 라 히트스톱 중에도 진행된다. 걷는 이동만 scaled.
public class Smith_npc : MonoBehaviour
{

    // 그림이 왼쪽을 보고 그려졌으면 true. 오른쪽을 보고 있으면 인스펙터에서 끈다.
    public bool art_faces_left = true;

    // 걷기 · 서기
    const float bounce_amplitude = 0.05f;
    const float bounce_period = 0.35f;
    const float enter_margin = 1f;          // 화면 오른쪽 끝에서 이만큼 들어오면 "등장"
    const float stop_margin = 2.8f;         // 화면 오른쪽 끝에서 이만큼 안쪽에 멈춰 선다
    const int sorting_order = -1;           // 성검(1)·일반 작물(0) 뒤에 그린다. 성검을 가리지 않게

    // 등장 대사 (smith_01). 두 줄을 순서대로.
    const float intro_line_time = 1.6f;

    // 배신 반응 (smith_02). 대사가 끝나면 vanish_time 동안 옅어지며 사라진다
    const float stun_time = 0.15f;
    const float betrayal_line_time = 2.5f;
    const float hitstop_time = 0.08f;
    const float tilt_angle = -12f;
    const float tilt_time = 0.2f;
    const float vanish_time = 0.5f;

    // 용사가 성검을 던졌다. 살아 있는 대장장이가 반응한다. 이미 화면 밖으로 사라졌으면 아무 일도 없다.
    public static void OnHolySwordThrown()
    {
        foreach (Smith_npc smith in FindObjectsByType<Smith_npc>(FindObjectsSortMode.None)) {
            smith.React();
        }
    }

    float base_y;
    float stop_x;
    float walk_time;
    bool entered;
    bool arrived;

    // 등장 대사 진행. -1 이면 아직, lines.Length 면 끝
    string[] intro_lines;
    int intro_index = -1;
    float intro_timer;

    // 배신 반응
    bool reacted;
    float stun_timer;
    float betrayal_timer;
    float tilt_target;
    float tilt_current;
    bool vanishing;
    float vanish_timer;

    SpriteRenderer sprite_renderer;
    Color base_color = Color.white;

    void Start()
    {
        base_y = transform.position.y;
        stop_x = World_scroll.RightX() - stop_margin;

        sprite_renderer = GetComponentInChildren<SpriteRenderer>();
        if (sprite_renderer != null) {
            base_color = sprite_renderer.color;
            sprite_renderer.sortingOrder = Mathf.Min(sprite_renderer.sortingOrder, sorting_order);
        }
        Sprite_fit.Face(sprite_renderer, -1f, art_faces_left);
    }

    void FixedUpdate()
    {
        if (arrived || stun_timer > 0f || Game_flow.is_over) {
            return;
        }

        // 작물과 같은 속도로 걸어 들어오다가 자리에 닿으면 선다.
        float dt = Time.fixedDeltaTime;
        walk_time += dt;

        Vector3 position = transform.position;
        position.x -= World_scroll.current_speed * dt;

        if (position.x <= stop_x) {
            position.x = stop_x;
            position.y = base_y;
            arrived = true;
        }
        else {
            position.y = base_y + Mathf.Abs(Mathf.Sin(walk_time * Mathf.PI / bounce_period)) * bounce_amplitude;
        }

        transform.position = position;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (stun_timer > 0f) {
            stun_timer -= dt;
        }

        UpdateEntrance(dt);
        UpdateBetrayal(dt);
        UpdateTilt(dt);
        UpdateVanish(dt);
    }

    // 화면 안으로 들어온 순간 등장 대사 두 줄을 차례로.
    void UpdateEntrance(float dt)
    {
        if (!entered) {
            if (transform.position.x >= World_scroll.RightX() - enter_margin) {
                return;
            }

            entered = true;
            intro_lines = Dialogue_table.Lines("smith_01");
            intro_index = -1;
            intro_timer = 0f;
        }

        if (intro_lines == null || intro_index >= intro_lines.Length) {
            return;
        }

        // 배신 대사가 시작되면 등장 대사는 거기서 끊는다. 머리 위 팝업은 대상당 하나라 어차피 덮인다.
        if (reacted) {
            intro_index = intro_lines.Length;
            return;
        }

        intro_timer -= dt;
        if (intro_timer > 0f) {
            return;
        }

        intro_index++;
        if (intro_index >= intro_lines.Length) {
            return;
        }

        intro_timer = intro_line_time;
        Popup_text.ShowAbove(transform, intro_lines[intro_index], intro_line_time);
    }

    void React()
    {
        if (reacted) {
            return;
        }
        reacted = true;

        // 멈칫 + 화면도 잠깐 멈춤. 이 게임 최대 웃음 포인트.
        stun_timer = stun_time;
        Camera_director.HitStop(hitstop_time);

        betrayal_timer = betrayal_line_time;
        tilt_target = tilt_angle;

        string line = Dialogue_table.Line("smith_02");
        if (line != null) {
            Popup_text.ShowAbove(transform, line, betrayal_line_time);
        }
    }

    void UpdateBetrayal(float dt)
    {
        if (betrayal_timer <= 0f) {
            return;
        }

        betrayal_timer -= dt;
        if (betrayal_timer <= 0f) {
            betrayal_timer = 0f;
            tilt_target = 0f;

            // 배신당한 대사가 끝났다. 옅어지며 사라진다. 던지지 않았으면 여기까지 오지 않는다.
            vanishing = true;
            vanish_timer = vanish_time;
        }
    }

    // 성검을 던진 뒤에만. 그림이 옅어지다 없어진다.
    void UpdateVanish(float dt)
    {
        if (!vanishing) {
            return;
        }

        vanish_timer -= dt;
        if (vanish_timer <= 0f) {
            Destroy(gameObject);
            return;
        }

        if (sprite_renderer != null) {
            Color color = base_color;
            color.a *= Mathf.Clamp01(vanish_timer / vanish_time);
            sprite_renderer.color = color;
        }
    }

    // 뒤로 살짝 자빠지는 느낌. tilt_time 에 걸쳐 목표 각도로, 대사가 끝나면 같은 시간에 걸쳐 되돌아온다.
    void UpdateTilt(float dt)
    {
        if (Mathf.Approximately(tilt_current, tilt_target)) {
            return;
        }

        float step = Mathf.Abs(tilt_angle) / tilt_time * dt;
        tilt_current = Mathf.MoveTowards(tilt_current, tilt_target, step);
        transform.rotation = Quaternion.Euler(0f, 0f, tilt_current);
    }
}
