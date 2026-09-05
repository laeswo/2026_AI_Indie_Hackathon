using UnityEngine;
using UnityEngine.UI;

// 게임 화면 HUD. UI 오브젝트는 에디터에서 Canvas 에 직접 놓고, 여기 슬롯에 드래그해서 연결한다.
// 이 스크립트는 값만 써넣는다. 배치·크기·색·그림은 전부 에디터 몫.
//
//   용사   player_label(선택) + hearts[] (왼쪽부터 max_hp 개)
//   드래곤 dragon_hp_fill (Image Type = Filled, Fill Method = Horizontal) + dragon_hp_ghost(선택) + dragon_hp_text + eat_stack_text
//   결과   result_panel (평소 꺼둠) + result_text + restart_button / start_button (OnClick 은 비워두면 코드가 연결)
//   플래시 flash_image (전체화면 Image, Raycast Target 끄기). 비우면 코드가 최상위 Canvas 에 하나 만든다
//
// 슬롯이 비어 있으면 그 항목만 건너뛴다. 하나씩 붙여가며 확인해도 된다.
// 씬에 Hud 가 없으면 Player_health.Awake 의 Ensure() 가 빈 것을 하나 만들어 플래시만 살린다(경고 출력).
// 값은 Update 에서 폴링하고, 바뀔 때만 텍스트를 다시 쓴다.
public class Hud : MonoBehaviour
{

    [Header("용사")]
    public Text player_label;               // 선택. "용사" 글자
    public Image[] hearts;                  // 왼쪽부터. 개수가 max_hp 보다 적으면 있는 만큼만
    public Sprite heart_full_sprite;        // 비우면 색으로만 구분
    public Sprite heart_empty_sprite;

    [Header("드래곤")]
    public Image dragon_hp_fill;            // Image Type = Filled, Fill Method = Horizontal
    public Image dragon_hp_ghost;           // 선택. 채움 뒤에서 천천히 따라오는 잔상 (같은 세팅)
    public Text dragon_hp_text;
    public Text eat_stack_text;

    [Header("결과")]
    public GameObject result_panel;         // 평소엔 꺼둔다. 게임이 끝나면 켠다
    public Text result_text;
    public Button restart_button;           // OnClick 은 비워둬라. 코드가 Game_flow.Restart 를 연결한다
    public Button start_button;             // 코드가 Game_flow.GoToStart 를 연결한다

    [Header("플래시")]
    public Image flash_image;               // 전체화면 Image. 비우면 코드로 만든다

    [Header("색")]
    public Color heart_color = new Color(0.92f, 0.32f, 0.28f);
    public Color heart_empty_color = new Color(0.23f, 0.25f, 0.28f);
    public Color bar_color = new Color(0.5f, 0.85f, 0.45f);
    public Color bar_low_color = new Color(0.95f, 0.4f, 0.3f);
    public Color stack_full_color = new Color(0.8f, 0.6f, 1f);
    public Color text_color = Color.white;

    // 연출
    const float bar_follow_speed = 0.6f;        // HP 바가 실제 값을 따라가는 속도. 초당 max 의 60%
    const float ghost_follow_speed = 0.3f;      // 잔상 바. 채움보다 느리게 따라온다
    const float heart_punch_time = 0.15f;       // 하트가 깎일 때 커졌다 작아지는 시간
    const float heart_punch_scale = 1.35f;
    const float low_hp_ratio = 0.3f;            // 이 아래로 떨어지면 바가 빨개진다
    const float find_retry_interval = 0.5f;     // 드래곤·용사를 못 찾았을 때 다시 찾는 간격

    static readonly Color phase2_bar_tint = new Color(1f, 0.55f, 0.45f);

    static Hud instance;

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        instance = null;
    }

    // 씬에 있는 걸 돌려준다. 없으면 Resources/Hud.prefab 을 불러온다.
    // 배치를 바꾸고 싶으면 그 프리팹을 열어 에디터에서 만지면 된다. 씬에 직접 놓아도 되고, 그러면 그쪽을 쓴다.
    public static Hud Ensure()
    {
        if (instance != null) {
            return instance;
        }

        instance = FindFirstObjectByType<Hud>();
        if (instance != null) {
            return instance;
        }

        GameObject prefab = Resources.Load<GameObject>("Hud");
        if (prefab != null) {
            GameObject spawned = Instantiate(prefab);
            spawned.name = "Hud";
            instance = spawned.GetComponent<Hud>();
            if (instance != null) {
                return instance;
            }
        }

        Debug.LogWarning("Hud 를 찾지 못했습니다. Assets/Hwang/Resources/Hud.prefab 이 있는지 확인하세요. 지금은 플래시만 동작합니다.");

        GameObject holder = new GameObject("Hud (빈 슬롯)");
        instance = holder.AddComponent<Hud>();
        return instance;
    }

    // 전체화면을 색으로 덮고 알파 1→0 으로 감쇠. 나중 것이 덮는다. Camera_director.Flash 가 부른다.
    public static void Flash(Color color, float duration)
    {
        if (duration <= 0f || color.a <= 0f) {
            return;
        }

        Hud hud = Ensure();
        hud.EnsureFlashImage();

        hud.flash_color = color;
        hud.flash_duration = duration;
        hud.flash_remaining = duration;
        hud.flash_image.enabled = true;
    }

    // 화면 가운데 큰 글자를 seconds 동안 띄운다. 페이즈 2 진입 같은 순간용. 나중 것이 덮는다.
    public static void Banner(string text, float seconds)
    {
        if (string.IsNullOrEmpty(text) || seconds <= 0f) {
            return;
        }

        Hud hud = Ensure();
        hud.EnsureBanner();

        hud.banner_text.text = text;
        hud.banner_duration = seconds;
        hud.banner_remaining = seconds;
        hud.banner_text.enabled = true;
    }

    // ---------- 참조 ----------

    Dragon dragon;
    Player_health health;
    float find_retry_timer;

    // 용사
    RectTransform[] heart_rects = new RectTransform[0];
    float[] heart_punch_timers = new float[0];
    int shown_player_hp = -1;
    bool warned_heart_count;

    // 드래곤
    string shown_dragon_label;
    string shown_stack_label;
    float bar_shown_ratio = 1f;
    float ghost_shown_ratio = 1f;
    bool bar_initialized;

    // 결과
    string shown_result;

    // 플래시
    Color flash_color;
    float flash_duration;
    float flash_remaining;

    // 배너 (화면 가운데 큰 글자. 페이즈 2 진입 등)
    Text banner_text;
    float banner_duration;
    float banner_remaining;
    const float banner_fade_in = 0.25f;
    const float banner_fade_out = 0.45f;

    void Awake()
    {
        if (instance == null) {
            instance = this;
        }

        // 결과 패널 버튼이 클릭을 받으려면 EventSystem 이 있어야 한다.
        Ui_util.EnsureEventSystem();

        SetupHearts();
        SetupBar(dragon_hp_fill);
        SetupBar(dragon_hp_ghost);
        SetupButtons();

        if (result_panel != null) {
            result_panel.SetActive(false);
        }

        if (flash_image != null) {
            flash_image.raycastTarget = false;
            flash_image.enabled = false;
        }
    }

    void OnDestroy()
    {
        if (instance == this) {
            instance = null;
        }
    }

    // ---------- 슬롯 준비 ----------

    void SetupHearts()
    {
        if (hearts == null) {
            hearts = new Image[0];
        }

        heart_rects = new RectTransform[hearts.Length];
        heart_punch_timers = new float[hearts.Length];

        for (int i = 0; i < hearts.Length; i++) {
            heart_rects[i] = hearts[i] != null ? hearts[i].rectTransform : null;
        }
    }

    // 흰 네모 스프라이트. Image 에 스프라이트가 없으면 Filled 타입이어도 fillAmount 를 무시하고 통째로 그리기 때문에 필요하다.
    static Sprite white_sprite;

    static Sprite WhiteSprite()
    {
        if (white_sprite == null) {
            Texture2D texture = Texture2D.whiteTexture;
            white_sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }
        return white_sprite;
    }

    // 에디터에서 Filled 로 안 맞춰 놨어도 동작하게 강제한다.
    static void SetupBar(Image image)
    {
        if (image == null) {
            return;
        }

        // 스프라이트가 비어 있으면 채움이 안 줄어든다. 흰 네모라도 넣는다.
        if (image.sprite == null) {
            image.sprite = WhiteSprite();
        }

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 1f;
    }

    // 에디터 OnClick 에 이미 뭔가 연결돼 있으면 두 번 불리지 않게 코드 쪽은 건너뛴다.
    void SetupButtons()
    {
        if (restart_button != null && restart_button.onClick.GetPersistentEventCount() == 0) {
            restart_button.onClick.AddListener(() => {
                Sound_bank.Play("click_sound");
                Game_flow.Restart();
            });
        }

        if (start_button != null && start_button.onClick.GetPersistentEventCount() == 0) {
            start_button.onClick.AddListener(() => {
                Sound_bank.Play("click_sound");
                Game_flow.GoToStart();
            });
        }
    }

    // 플래시 슬롯이 비었으면 최상위 Canvas 에 하나 만든다. 클릭을 가로채면 안 되므로 Raycaster 없이.
    void EnsureFlashImage()
    {
        if (flash_image != null) {
            return;
        }

        Ui_util.EnsureEventSystem();

        Canvas flash_canvas = Ui_util.MakeCanvas("Flash_canvas", 100, false);
        flash_canvas.transform.SetParent(transform, false);

        flash_image = Ui_util.MakeImage(flash_canvas.GetComponent<RectTransform>(), "Flash", Color.clear);
        flash_image.raycastTarget = false;
        Ui_util.Stretch(flash_image.rectTransform);
        flash_image.enabled = false;
    }

    // 배너용 Canvas 와 Text 를 처음 쓸 때 만든다. 플래시(100)보다 아래, HUD(10)보다 위.
    void EnsureBanner()
    {
        if (banner_text != null) {
            return;
        }

        Canvas banner_canvas = Ui_util.MakeCanvas("Banner_canvas", 90, false);
        banner_canvas.transform.SetParent(transform, false);

        RectTransform root = banner_canvas.GetComponent<RectTransform>();
        banner_text = Ui_util.MakeText(root, "Banner", "", 72, new Color(1f, 0.45f, 0.35f), FontStyle.Bold);
        Ui_util.AddShadow(banner_text);
        Ui_util.Place(banner_text.rectTransform, new Vector2(0f, 120f), new Vector2(1700f, 120f));
        banner_text.raycastTarget = false;
        banner_text.enabled = false;
    }

    void UpdateBanner(float dt)
    {
        if (banner_remaining <= 0f || banner_text == null) {
            return;
        }

        banner_remaining = Mathf.Max(0f, banner_remaining - dt);

        if (banner_remaining <= 0f) {
            banner_text.enabled = false;
            return;
        }

        // 들어올 때 빠르게, 나갈 때 천천히. 그 사이는 그대로.
        float elapsed = banner_duration - banner_remaining;
        float alpha = 1f;
        if (elapsed < banner_fade_in) {
            alpha = elapsed / banner_fade_in;
        }
        else if (banner_remaining < banner_fade_out) {
            alpha = banner_remaining / banner_fade_out;
        }

        Color color = banner_text.color;
        color.a = alpha;
        banner_text.color = color;

        // 들어올 때 살짝 커지며 자리 잡는다.
        float scale = elapsed < banner_fade_in ? Mathf.Lerp(1.25f, 1f, elapsed / banner_fade_in) : 1f;
        banner_text.rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    // ---------- 갱신 ----------

    void Update()
    {
        // 히트스톱(timeScale 0) 중에도 연출이 진행되게 unscaled 를 쓴다.
        float dt = Time.unscaledDeltaTime;

        FindTargets(dt);
        UpdatePlayer(dt);
        UpdateDragon(dt);
        UpdateResult();
        UpdateFlash(dt);
        UpdateBanner(dt);
    }

    void FindTargets(float dt)
    {
        if (dragon != null && health != null) {
            return;
        }

        find_retry_timer -= dt;
        if (find_retry_timer > 0f) {
            return;
        }
        find_retry_timer = find_retry_interval;

        if (dragon == null) {
            dragon = FindFirstObjectByType<Dragon>();
        }

        if (health == null) {
            GameObject player_object = GameObject.FindWithTag("Player");
            if (player_object != null) {
                health = player_object.GetComponent<Player_health>();
            }
        }
    }

    void UpdatePlayer(float dt)
    {
        if (health == null || hearts.Length == 0) {
            return;
        }

        if (!warned_heart_count && health.max_hp > hearts.Length) {
            warned_heart_count = true;
            Debug.LogWarning("Hud: 하트 Image 가 " + hearts.Length + "개인데 용사 max_hp 는 " + health.max_hp + "입니다. 하트를 더 놓고 슬롯에 추가하세요.");
        }

        int hp = Mathf.Clamp(health.hp, 0, hearts.Length);

        if (hp != shown_player_hp) {
            // 깎인 하트만 펀치. 처음 보여줄 때(-1)는 연출 없이 그냥 칠한다.
            if (shown_player_hp >= 0 && hp < shown_player_hp) {
                for (int i = hp; i < shown_player_hp; i++) {
                    heart_punch_timers[i] = heart_punch_time;
                }
            }

            for (int i = 0; i < hearts.Length; i++) {
                PaintHeart(hearts[i], i < hp);
            }

            shown_player_hp = hp;
        }

        for (int i = 0; i < hearts.Length; i++) {
            if (heart_punch_timers[i] <= 0f || heart_rects[i] == null) {
                continue;
            }

            heart_punch_timers[i] -= dt;

            // 커졌다가 원래 크기로. 남은 시간 비율이 곧 커진 정도다.
            float t = Mathf.Clamp01(heart_punch_timers[i] / heart_punch_time);
            float scale = Mathf.Lerp(1f, heart_punch_scale, t);
            heart_rects[i].localScale = new Vector3(scale, scale, 1f);

            if (heart_punch_timers[i] <= 0f) {
                heart_rects[i].localScale = Vector3.one;
            }
        }
    }

    // 스프라이트를 줬으면 스프라이트를 바꾸고, 아니면 색으로 구분한다.
    void PaintHeart(Image heart, bool full)
    {
        if (heart == null) {
            return;
        }

        if (heart_full_sprite != null) {
            heart.sprite = full ? heart_full_sprite : (heart_empty_sprite != null ? heart_empty_sprite : heart_full_sprite);
            heart.color = full || heart_empty_sprite != null ? Color.white : heart_empty_color;
            return;
        }

        heart.color = full ? heart_color : heart_empty_color;
    }

    void UpdateDragon(float dt)
    {
        if (dragon == null) {
            return;
        }

        float ratio = dragon.max_hp > 0 ? Mathf.Clamp01((float)dragon.hp / dragon.max_hp) : 0f;

        // 처음엔 바로 맞추고, 그 다음부턴 부드럽게 따라간다.
        if (!bar_initialized) {
            bar_initialized = true;
            bar_shown_ratio = ratio;
            ghost_shown_ratio = ratio;
        }
        else {
            bar_shown_ratio = Mathf.MoveTowards(bar_shown_ratio, ratio, bar_follow_speed * dt);
            ghost_shown_ratio = Mathf.MoveTowards(ghost_shown_ratio, bar_shown_ratio, ghost_follow_speed * dt);
        }

        if (dragon_hp_fill != null) {
            dragon_hp_fill.fillAmount = bar_shown_ratio;

            // 30% 아래로 떨어지면 붉게. 페이즈 2 면 평소 색도 살짝 붉게.
            Color color = ratio > low_hp_ratio ? bar_color : bar_low_color;
            if (dragon.phase >= 2) {
                color = Color.Lerp(color, phase2_bar_tint, 0.35f);
            }
            if (dragon_hp_fill.color != color) {
                dragon_hp_fill.color = color;
            }
        }

        if (dragon_hp_ghost != null) {
            dragon_hp_ghost.fillAmount = ghost_shown_ratio;
        }

        if (dragon_hp_text != null) {
            string label = (dragon.phase >= 2 ? "드래곤 [2페이즈]  " : "드래곤  ") + dragon.hp + " / " + dragon.max_hp;
            if (label != shown_dragon_label) {
                shown_dragon_label = label;
                dragon_hp_text.text = label;
            }
        }

        if (eat_stack_text != null) {
            // 먹은 개수. 다 차면 보라색으로 경고.
            string stack = "밥  " + dragon.eat_stack + " / " + dragon.eat_count;
            if (stack != shown_stack_label) {
                shown_stack_label = stack;
                eat_stack_text.text = stack;
                eat_stack_text.color = dragon.eat_stack >= dragon.eat_count ? stack_full_color : text_color;
            }
        }
    }

    void UpdateResult()
    {
        bool over = Game_flow.is_over;

        if (result_panel != null && result_panel.activeSelf != over) {
            result_panel.SetActive(over);
        }

        if (!over || result_text == null) {
            return;
        }

        string result = Game_flow.result ?? "";
        if (result != shown_result) {
            shown_result = result;
            result_text.text = result;
        }
    }

    void UpdateFlash(float dt)
    {
        if (flash_remaining <= 0f || flash_image == null) {
            return;
        }

        flash_remaining = Mathf.Max(0f, flash_remaining - dt);

        if (flash_remaining <= 0f) {
            flash_image.enabled = false;
            return;
        }

        Color color = flash_color;
        color.a *= flash_duration > 0f ? flash_remaining / flash_duration : 0f;
        flash_image.color = color;
    }
}
