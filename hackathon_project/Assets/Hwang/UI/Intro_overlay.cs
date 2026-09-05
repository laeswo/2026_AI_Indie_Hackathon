using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 게임 시작 때 검은 화면에 대사를 한 줄씩 보여주는 인트로.
// Resources/Intro_overlay.prefab (루트 + Screen Space Canvas 200 + 검정 배경 + 본문 Text + 안내 Text) 을 불러와 글자만 써넣는다.
//
//   Play(lines, on_done)   줄을 한 줄씩 추가하며 보여준다. 이전 줄은 남고 새 줄은 0.4초 페이드 인.
//                          스페이스 / 엔터 / 클릭으로 다음, 마지막 줄 뒤 한 번 더 누르면 끝. 입력이 없으면 줄당 2.5초 뒤 자동.
//                          진행 중 timeScale 0, 끝나면 1 로 복구하고 on_done 을 부른 뒤 자기를 없앤다.
//
// 언제 나오나 (TryPlayIntro):
//   게임 씬이 로드되고 용사(태그 Player)가 있으면. 단 시작 메뉴 오버레이가 떠 있으면 그게 걷힌 뒤(Start_menu.StartGame 이 부른다).
//   R 재시작(Tutorial.seen_intro 유지)에서는 건너뛰고, Esc 로 시작 화면에 갔다 오면 다시 나온다.
// 인트로 UI 자체는 unscaledDeltaTime 으로 움직인다.
public class Intro_overlay : MonoBehaviour
{

    public Text body_text;                  // 줄이 쌓이는 본문
    public Text hint_text;                  // 하단 "스페이스 / 클릭 - 넘기기"

    const float line_fade_time = 0.4f;
    const float line_auto_time = 4f;        // 입력이 없을 때 다음 줄까지. 읽을 시간을 넉넉히
    const float last_hold_time = 7f;        // 마지막 줄까지 다 뜬 뒤 자동으로 끝나기까지. 입력하면 바로 끝난다
    const float input_guard_time = 0.15f;   // 시작 직후 이 동안은 입력을 무시한다. 메뉴를 넘긴 스페이스가 첫 줄까지 넘기지 않게
    const string prefab_path = "Intro_overlay";
    const string intro_id = "intro_01";

    static Intro_overlay instance;

    public static bool is_playing
    {
        get { return instance != null && instance.playing; }
    }

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다. 씬 로드 훅도 여기서 건다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 씬이 로드될 때. 시작 메뉴 오버레이가 있는 씬이면 여기서는 안 하고, 메뉴가 걷힐 때 Start_menu 가 부른다.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<Start_menu>() != null || FindFirstObjectByType<Start_menu_view>() != null) {
            return;
        }

        TryPlayIntro();
    }

    // 조건이 맞으면 인트로를 시작한다. 용사가 없는 씬(시작 화면)이나 이미 본 판에서는 아무것도 안 한다.
    public static void TryPlayIntro()
    {
        if (Tutorial.seen_intro || is_playing) {
            return;
        }

        if (GameObject.FindWithTag("Player") == null) {
            return;
        }

        string[] lines = Dialogue_table.Lines(intro_id);
        if (lines == null || lines.Length == 0) {
            return;
        }

        Tutorial.seen_intro = true;
        Play(lines, null);
    }

    public static void Play(string[] lines, System.Action on_done)
    {
        if (lines == null || lines.Length == 0) {
            if (on_done != null) {
                on_done();
            }
            return;
        }

        Intro_overlay overlay = Ensure();
        if (overlay == null) {
            if (on_done != null) {
                on_done();
            }
            return;
        }

        overlay.Begin(lines, on_done);
    }

    static Intro_overlay Ensure()
    {
        if (instance != null) {
            return instance;
        }

        instance = FindFirstObjectByType<Intro_overlay>();
        if (instance != null) {
            return instance;
        }

        GameObject prefab = Resources.Load<GameObject>(prefab_path);
        if (prefab == null) {
            Debug.LogWarning("Intro_overlay : Resources/" + prefab_path + ".prefab 이 없어서 인트로를 건너뜁니다.");
            return null;
        }

        GameObject spawned = Instantiate(prefab);
        spawned.name = prefab.name;
        instance = spawned.GetComponent<Intro_overlay>();
        return instance;
    }

    // ---------- 진행 ----------

    string[] lines;
    System.Action on_done;
    bool playing;
    int shown_count;            // 지금까지 띄운 줄 수. 마지막 줄까지 띄운 뒤 한 번 더 넘기면 끝
    float line_timer;           // 현재 줄이 뜬 뒤 흐른 시간
    float total_timer;

    void Awake()
    {
        if (instance == null) {
            instance = this;
        }

        Ui_util.ApplyFont(gameObject);
    }

    void Begin(string[] new_lines, System.Action done)
    {
        lines = new_lines;
        on_done = done;
        playing = true;
        shown_count = 0;
        line_timer = 0f;
        total_timer = 0f;

        Time.timeScale = 0f;

        if (hint_text != null) {
            hint_text.text = "스페이스 / 클릭 - 넘기기";
        }

        ShowNextLine();
    }

    void Update()
    {
        if (!playing) {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        line_timer += dt;
        total_timer += dt;

        RefreshBody();

        bool advance = total_timer >= input_guard_time && WasAdvancePressed();

        // 줄이 남았으면 line_auto_time, 다 떴으면 last_hold_time 뒤에 저절로 넘어간다.
        float auto_time = shown_count < lines.Length ? line_auto_time : last_hold_time;
        if (!advance && line_timer >= auto_time) {
            advance = true;
        }

        if (!advance) {
            return;
        }

        if (shown_count < lines.Length) {
            ShowNextLine();
        }
        else {
            Finish();
        }
    }

    static bool WasAdvancePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) {
            return true;
        }

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    void ShowNextLine()
    {
        shown_count = Mathf.Min(shown_count + 1, lines.Length);
        line_timer = 0f;
        RefreshBody();
    }

    // 이전 줄은 그대로, 마지막 줄만 알파를 올리며 보여준다. 리치 텍스트 색 태그로 줄마다 알파를 준다.
    void RefreshBody()
    {
        if (body_text == null) {
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int i = 0; i < shown_count; i++) {
            if (i > 0) {
                builder.Append('\n');
            }

            bool is_last = i == shown_count - 1;
            float alpha = is_last && line_fade_time > 0f ? Mathf.Clamp01(line_timer / line_fade_time) : 1f;
            int alpha_byte = Mathf.RoundToInt(alpha * 255f);

            builder.Append("<color=#FFFFFF").Append(alpha_byte.ToString("X2")).Append('>');
            builder.Append(lines[i]);
            builder.Append("</color>");
        }

        body_text.text = builder.ToString();
    }

    void Finish()
    {
        playing = false;
        Time.timeScale = 1f;

        System.Action done = on_done;
        on_done = null;

        if (done != null) {
            done();
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // 인트로 도중에 씬이 바뀌어 없어져도 게임이 멈춘 채 남지 않게.
        if (playing) {
            playing = false;
            Time.timeScale = 1f;
        }

        if (instance == this) {
            instance = null;
        }
    }
}
