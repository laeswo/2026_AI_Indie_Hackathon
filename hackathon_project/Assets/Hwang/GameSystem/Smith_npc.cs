using UnityEngine;

// 대장장이 NPC. 성검 뒤를 따라 작물과 같은 속도로 걸어오며 "던지지 말라"고 한다.
// 용사가 성검을 주우면 용사 옆(오른쪽)에 붙어 서서 계속 지켜본다. 던지면 놀라서 커지며 배신 대사, 그 뒤 옅어지며 사라진다.
// 성검을 안 주우면 성검과 나란히 흘러가 화면 왼쪽 밖에서 사라진다. 그림 정렬(뒤쪽)은 그대로 둔다 — 크기만 커진다.
// 용사가 성검을 던지면 멈칫하며 뒤로 기울어 배신당한 대사를 하고, 대사가 끝나면 옅어지며 사라진다.
// 던지지 않으면(안 줍거나 계속 들고 있으면) 사라지지 않고 계속 서 있다.
//
// Rigidbody · 콜라이더 · Crop_flow 없음. 판정도 없고 작물로 주워지지도 않는다. 위치는 여기서 직접 옮긴다.
// 스폰은 Spawn_crop 이 한다 (성검 오른쪽 1.2 유닛, 같은 높이). 프리팹은 크기 잡을 임시 그림만 있으면 된다.
// 그림은 Resources/Smith 에서 읽는다: 서 있을 땐 smith_idle, 성검을 던지면 smith_suprise. 프리팹 그림이 차지하던 높이에 맞춘다.
// 시트가 Multiple 모드라 텍스처당 가장 큰 조각을 쓰고, 피벗은 가운데로 다시 만든다(반전·정렬이 흔들리지 않게).
// 대사 타이머와 반응 연출은 unscaled 라 히트스톱 중에도 진행된다. 걷는 이동만 scaled.
public class Smith_npc : MonoBehaviour
{

    // 그림이 왼쪽(용사 쪽)을 보고 그려졌으면 true → 안 뒤집는다. smith_idle / smith_suprise 는 이미 왼쪽을 보게 그려져 있다.
    // 오른쪽을 보는 그림으로 바꾸면 인스펙터에서 끈다(그러면 flipX 로 뒤집는다).
    public bool art_faces_left = true;

    // 그림. Resources/Smith 의 텍스처 이름.
    const string art_folder = "Smith";
    const string idle_art = "smith_idle";
    const string surprise_art = "smith_suprise";
    const float default_height = 2f;        // 프리팹에 그림이 없어서 높이를 못 재면 이 높이

    Sprite idle_sprite;
    Sprite surprise_sprite;
    float art_height;                       // 프리팹 그림이 차지하던 높이. 새 그림도 이 높이로 맞춘다

    // 걷기 · 서기
    const float bounce_amplitude = 0.05f;
    const float bounce_period = 0.35f;
    const float enter_margin = 1f;          // 화면 오른쪽 끝에서 이만큼 들어오면 "등장"
    const float despawn_margin = 3f;        // 화면 왼쪽 끝에서 이만큼 더 나가면 사라진다
    const int sorting_order = -1;           // 성검(1)·일반 작물(0) 뒤에 그린다. 성검을 가리지 않게

    // 등장 대사 (smith_01). 두 줄을 순서대로.
    const float intro_line_time = 1.6f;

    // 배신 반응 (smith_02). 대사가 끝나면 vanish_time 동안 옅어지며 사라진다
    const float stun_time = 0.15f;
    const float betrayal_line_time = 2.5f;
    const float surprise_scale = 1.6f;      // 놀라면 이만큼 커진다. 정렬은 안 바꾼다
    const float scale_time = 0.25f;

    // 용사가 성검을 들고 있을 때 옆에 서는 자리. 용사 오른쪽 이만큼
    const float follow_offset_x = 1.7f;
    const float follow_speed = 6f;          // 자리로 다가가는 속도 (유닛/초)
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

    // 성검이 없어졌다(드래곤에 맞음 · 화면 밖으로 흘러감 · 땅에 닿음). 대장장이도 같이 사라진다.
    // 던져서 배신 대사가 진행 중이면 그 대사가 끝난 뒤 사라지는 기존 흐름을 그대로 둔다(끊지 않는다).
    public static void OnHolySwordGone()
    {
        foreach (Smith_npc smith in FindObjectsByType<Smith_npc>(FindObjectsSortMode.None)) {
            smith.Leave();
        }
    }

    // 조용히 옅어지며 사라진다. 이미 사라지는 중이거나 배신 대사 중이면 아무것도 안 한다.
    void Leave()
    {
        if (vanishing || reacted) {
            return;
        }

        // 등장 대사가 남았어도 끊는다.
        if (intro_lines != null) {
            intro_index = intro_lines.Length;
        }

        vanishing = true;
        vanish_timer = vanish_time;
    }

    float base_y;
    float walk_time;
    bool entered;

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

    // 같이 스폰된 성검과 용사. 성검을 들고 있는지 보고 옆에 붙는다.
    GameObject sword;
    Transform player;
    Vector3 base_scale = Vector3.one;
    float scale_current = 1f;
    float scale_target = 1f;

    // Spawn_crop 이 성검을 만든 직후 부른다.
    public void Bind(GameObject sword_object)
    {
        sword = sword_object;
    }

    // 용사가 성검을 손에 들고 있는가 (흐르지도, 던져지지도 않은 상태).
    bool SwordHeld()
    {
        if (sword == null) {
            return false;
        }

        Crop_flow flow = sword.GetComponent<Crop_flow>();
        return flow != null && !flow.is_flowing && !flow.is_thrown;
    }

    void Start()
    {
        base_y = transform.position.y;
        base_scale = transform.localScale;

        GameObject player_object = GameObject.FindWithTag("Player");
        if (player_object != null) {
            player = player_object.transform;
        }

        sprite_renderer = GetComponentInChildren<SpriteRenderer>();
        if (sprite_renderer != null) {
            base_color = sprite_renderer.color;
            sprite_renderer.sortingOrder = Mathf.Min(sprite_renderer.sortingOrder, sorting_order);
        }

        LoadArt();
        ShowArt(idle_sprite);
    }

    // Resources/Smith 에서 두 그림을 읽는다. 프리팹 그림이 차지하던 높이를 먼저 재 둔다.
    void LoadArt()
    {
        if (sprite_renderer == null) {
            return;
        }

        Vector2 current = sprite_renderer.sprite != null ? Sprite_fit.WorldSize(sprite_renderer) : Vector2.zero;
        art_height = current.y > 0.1f ? current.y : default_height;

        Sprite[] sheet = Resources.LoadAll<Sprite>(art_folder);
        idle_sprite = LargestOf(sheet, idle_art);
        surprise_sprite = LargestOf(sheet, surprise_art);

        if (idle_sprite == null) {
            Debug.LogWarning(name + " : Resources/" + art_folder + "/" + idle_art + ".png 을 찾지 못해 프리팹 그림을 그대로 씁니다.");
        }
        if (surprise_sprite == null) {
            Debug.LogWarning(name + " : Resources/" + art_folder + "/" + surprise_art + ".png 을 찾지 못해 성검을 던져도 그림이 안 바뀝니다.");
        }
    }

    // 그림을 바꾸고 높이를 art_height 로 맞춘 뒤 용사 쪽(왼쪽)을 보게 뒤집는다. 색은 지금 알파(사라지는 중)를 유지한다.
    void ShowArt(Sprite sprite)
    {
        if (sprite_renderer == null) {
            return;
        }

        if (sprite != null) {
            sprite_renderer.sprite = sprite;
            Sprite_fit.FitHeight(sprite_renderer, art_height);
        }

        Sprite_fit.Face(sprite_renderer, -1f, art_faces_left);
    }

    // 시트에서 텍스처 이름이 맞는 조각 중 가장 큰 것. 자동 슬라이스 부스러기를 피한다.
    // 피벗을 가운데로 둔 새 스프라이트로 만들어 돌려준다. 원본 피벗이 어디든 반전·정렬이 같게.
    static Sprite LargestOf(Sprite[] sheet, string texture_name)
    {
        Sprite largest = null;
        float largest_area = -1f;

        foreach (Sprite sprite in sheet) {
            if (sprite == null || sprite.texture == null || sprite.texture.name != texture_name) {
                continue;
            }

            float area = sprite.rect.width * sprite.rect.height;
            if (area > largest_area) {
                largest_area = area;
                largest = sprite;
            }
        }

        if (largest == null) {
            return null;
        }

        Sprite centered = Sprite.Create(largest.texture, largest.rect, new Vector2(0.5f, 0.5f), largest.pixelsPerUnit);
        centered.name = largest.name;
        return centered;
    }

    void FixedUpdate()
    {
        if (stun_timer > 0f || Game_flow.is_over) {
            return;
        }

        float dt = Time.fixedDeltaTime;
        Vector3 position = transform.position;

        if (SwordHeld() && player != null) {
            // 용사가 성검을 들고 있다. 용사 옆자리로 다가가서 선다. 서 있을 땐 바운스도 멈춘다.
            float target_x = player.position.x + follow_offset_x;
            float before = position.x;
            position.x = Mathf.MoveTowards(position.x, target_x, follow_speed * dt);

            bool moving = Mathf.Abs(position.x - before) > 0.0001f;
            if (moving) {
                walk_time += dt;
            }
            position.y = base_y + (moving ? Mathf.Abs(Mathf.Sin(walk_time * Mathf.PI / bounce_period)) * bounce_amplitude : 0f);
            transform.position = position;
            return;
        }

        // 작물(성검)과 같은 속도로 계속 걷는다. 멈추지 않으므로 성검 오른쪽 1.2 유닛을 그대로 유지한다.
        walk_time += dt;
        position.x -= World_scroll.current_speed * dt;
        position.y = base_y + Mathf.Abs(Mathf.Sin(walk_time * Mathf.PI / bounce_period)) * bounce_amplitude;
        transform.position = position;

        // 화면 왼쪽 밖으로 나가면 사라진다. 배신 대사 중이면 대사가 끝난 뒤 vanish 가 처리하니 여기선 안 지운다.
        if (!reacted && !vanishing && position.x < World_scroll.LeftX() - despawn_margin) {
            Destroy(gameObject);
        }
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
        UpdateScale(dt);
        UpdateVanish(dt);
    }

    // 놀라면 커진다. 사라질 때까지 큰 채로 있다. 그림 정렬은 건드리지 않는다.
    void UpdateScale(float dt)
    {
        if (Mathf.Approximately(scale_current, scale_target)) {
            return;
        }

        scale_current = Mathf.MoveTowards(scale_current, scale_target, dt * (surprise_scale - 1f) / scale_time);
        transform.localScale = base_scale * scale_current;
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

        // 멈칫 + 화면도 잠깐 멈춤. 이 게임 최대 웃음 포인트. 그림도 놀란 얼굴로.
        stun_timer = stun_time;
        Camera_director.HitStop(hitstop_time);
        ShowArt(surprise_sprite);

        betrayal_timer = betrayal_line_time;
        tilt_target = tilt_angle;
        scale_target = surprise_scale;

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
