using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

// 시작 화면의 동작. StartScene 에 이 컴포넌트 하나만 놓으면 된다.
// 화면은 Resources/Start_menu_ui.prefab(루트에 Start_menu_view) 을 불러와 쓴다. 씬에 프리팹을 직접 놓아도 되고, 그러면 그쪽을 쓴다.
// 글자·그림은 여기 인스펙터 값으로 덮어쓴다. 비워두면 프리팹에 적힌 그대로 나온다.
// 시작: 버튼 클릭 / 스페이스 / 엔터.  종료: 버튼 클릭 / Esc.
public class Start_menu : MonoBehaviour
{

    [Header("글자 (비우면 프리팹 값)")]
    public string title = "던지지 마!";
    public string subtitle = "손에 잡히는 건 전부 던진다";
    public string hint = "Y 홀드 - 던지기   /   스페이스 - 점프 (길게 누르면 높이)";

    [Header("그림 (비우면 단색·글자)")]
    public Sprite background_sprite;
    public Sprite title_sprite;

    [Header("씬")]
    public string game_scene_name = "SampleScene";

    const string prefab_name = "Start_menu_ui";

    Start_menu_view view;
    bool started;

    // 게임 씬 안에 놓인 경우: 씬을 바꾸는 대신 메뉴를 덮어 보여주고, 시작하면 걷어낸다.
    bool is_overlay
    {
        get { return SceneManager.GetActiveScene().name == game_scene_name; }
    }

    void Start()
    {
        Ui_util.EnsureEventSystem();

        view = FindFirstObjectByType<Start_menu_view>();
        if (view == null) {
            view = SpawnView();
        }

        if (view == null) {
            Debug.LogWarning("시작 화면 프리팹(Resources/" + prefab_name + ")을 찾지 못했습니다. 키 입력만 동작합니다.");
            return;
        }

        ApplyTexts();
        ApplySprites();
        WireButtons();

        // 게임 씬에 메뉴가 떠 있는 동안은 게임을 멈춰 둔다. 시작 누르면 풀린다.
        if (is_overlay) {
            Time.timeScale = 0f;
        }
    }

    Start_menu_view SpawnView()
    {
        GameObject prefab = Resources.Load<GameObject>(prefab_name);
        if (prefab == null) {
            return null;
        }

        GameObject spawned = Instantiate(prefab);
        spawned.name = "Start_menu_ui";
        return spawned.GetComponent<Start_menu_view>();
    }

    void ApplyTexts()
    {
        if (view.title_text != null && !string.IsNullOrEmpty(title)) {
            view.title_text.text = title;
        }
        if (view.subtitle_text != null && !string.IsNullOrEmpty(subtitle)) {
            view.subtitle_text.text = subtitle;
        }
        if (view.hint_text != null && !string.IsNullOrEmpty(hint)) {
            view.hint_text.text = hint;
        }
    }

    void ApplySprites()
    {
        if (background_sprite != null && view.background_image != null) {
            view.background_image.sprite = background_sprite;
            view.background_image.color = Color.white;
        }

        // 제목 그림이 있으면 글자 대신 그림을 보여준다.
        if (title_sprite != null && view.title_image != null) {
            view.title_image.sprite = title_sprite;
            view.title_image.preserveAspect = true;
            view.title_image.gameObject.SetActive(true);

            if (view.title_text != null) {
                view.title_text.gameObject.SetActive(false);
            }
        }
    }

    // 에디터 OnClick 에 이미 뭔가 연결돼 있으면 두 번 불리지 않게 코드 쪽은 건너뛴다.
    void WireButtons()
    {
        // 메뉴가 떠 있는 동안은 메인 BGM. 게임 씬 안에 오버레이로 뜬 경우도 여기서 바꿔 준다.
        Music_player.PlayMain();

        if (view.start_button != null && view.start_button.onClick.GetPersistentEventCount() == 0) {
            view.start_button.onClick.AddListener(StartGame);
        }
        if (view.quit_button != null && view.quit_button.onClick.GetPersistentEventCount() == 0) {
            view.quit_button.onClick.AddListener(QuitGame);
        }
    }

    void Update()
    {
        if (Keyboard.current == null) {
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) {
            StartGame();
        }
        else if (Keyboard.current.escapeKey.wasPressedThisFrame) {
            QuitGame();
        }
    }

    public void StartGame()
    {
        // 버튼 연타로 두 번 불리지 않게.
        if (started) {
            return;
        }
        started = true;

        Sound_bank.Play("click_sound");
        Game_flow.ResetForNewGame();

        // 전투 BGM. 씬을 새로 불러오면 Music_player 가 알아서 고르지만, 오버레이 메뉴는 씬이 안 바뀌므로 여기서.
        Music_player.PlayBattle();

        // 이미 게임 씬이면 씬을 다시 불러오지 않고(무한 반복이 된다) 메뉴만 걷어낸다.
        if (is_overlay) {
            Time.timeScale = 1f;
            if (view != null) {
                view.gameObject.SetActive(false);
            }
            enabled = false;

            // 메뉴가 걷힌 뒤에 인트로. 씬 로드 때는 메뉴가 있어서 건너뛰었다.
            Intro_overlay.TryPlayIntro();
            return;
        }

        SceneManager.LoadScene(game_scene_name);
    }

    public void QuitGame()
    {
        Sound_bank.Play("click_sound");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
