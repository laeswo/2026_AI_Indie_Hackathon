using UnityEngine;
using UnityEngine.SceneManagement;

// 무한 횡스크롤 배경. 게임 씬이 뜨면 Resources 의 배경 그림을 불러와 Scroll_layer 로 깔아 준다.
// 씬에 손으로 놓을 게 없다. 그림은 화면 높이에 맞춰 키우고, 가로로 이어 붙여 왼쪽으로 흘려보낸다.
// 그림 양 끝이 안 맞아 이음새가 보이는 걸 막으려고 한 장 걸러 좌우를 뒤집어 붙인다(거울 반복).
//
// 그림을 바꾸려면 background_resource 이름만 바꾸면 된다. 씬에 직접 Background 를 놓고 sprite 를 넣으면 그쪽을 쓴다.
// 페이즈 2 에 들어가면 EnterPhase2() 가 하늘을 붉게 물들이고 불씨를 띄우고 스크롤을 영구히 빠르게 한다.
public class Background : MonoBehaviour
{

    // Resources 안의 파일 이름(확장자 없이). 스프라이트 모드가 Multiple 이어도 첫 조각을 쓴다.
    const string background_resource = "ChatGPT_Image_2026_9_5_11_31_54";

    [Header("배경")]
    public Sprite sprite;                   // 비우면 background_resource 를 불러온다
    [Range(0f, 1f)]
    public float parallax = 0.4f;           // 0 이면 멈춰 있고, 1 이면 작물과 같은 속도. 멀리 있는 느낌은 0.3~0.5
    public bool mirror_alternate = true;    // 한 장 걸러 좌우 반전. 이음새가 안 보인다
    public int sorting_order = -100;        // 전부의 뒤

    // 페이즈 2 분위기
    static readonly Color phase2_tint = new Color(0.8f, 0.4f, 0.35f);
    const float phase2_tint_time = 1.5f;
    const float phase2_scroll_multiplier = 1.3f;

    Color tint_from = Color.white;
    Color tint_to = Color.white;
    float tint_timer;
    float tint_duration;
    bool tinting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook()
    {
        // 게임 씬을 다시 불러올 때마다 새로 깔리게 한다. 시작 화면처럼 용사가 없는 씬에는 안 깐다.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (GameObject.FindWithTag("Player") == null) {
            return;
        }

        if (FindFirstObjectByType<Background>() != null) {
            return;
        }

        GameObject holder = new GameObject("Background");
        holder.AddComponent<Background>();
    }

    // 페이즈 2 진입. 드래곤(State_roar)이 부른다. 배경이 없으면 조용히 넘어간다.
    public static void EnterPhase2()
    {
        World_scroll.Get().phase_multiplier = phase2_scroll_multiplier;

        Background background = FindFirstObjectByType<Background>();
        if (background == null) {
            return;
        }

        background.SetTint(phase2_tint, phase2_tint_time);

        if (background.GetComponentInChildren<Ember_field>() == null) {
            GameObject embers = new GameObject("Embers");
            embers.transform.SetParent(background.transform, false);
            embers.AddComponent<Ember_field>();
        }
    }

    // 배경 그림 색을 seconds 에 걸쳐 tint 로 바꾼다. 타일 복제본까지 전부.
    public void SetTint(Color tint, float seconds)
    {
        tint_from = CurrentTint();
        tint_to = tint;
        tint_duration = Mathf.Max(0.01f, seconds);
        tint_timer = 0f;
        tinting = true;
    }

    Color CurrentTint()
    {
        SpriteRenderer any = GetComponentInChildren<SpriteRenderer>();
        return any != null ? any.color : Color.white;
    }

    void Update()
    {
        if (!tinting) {
            return;
        }

        // 슬로우모션·히트스톱 중에도 물들게 unscaled.
        tint_timer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(tint_timer / tint_duration);
        Color color = Color.Lerp(tint_from, tint_to, t);

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>()) {
            // 불씨는 제 색을 쓴다.
            if (renderer.GetComponentInParent<Ember_field>() != null) {
                continue;
            }
            renderer.color = color;
        }

        if (t >= 1f) {
            tinting = false;
        }
    }

    void Start()
    {
        if (sprite == null) {
            sprite = LoadSprite();
        }

        if (sprite == null) {
            Debug.LogWarning("배경 그림을 못 찾았습니다. Resources/" + background_resource + " 를 확인하세요.");
            return;
        }

        Build();
    }

    static Sprite LoadSprite()
    {
        Sprite single = Resources.Load<Sprite>(background_resource);
        if (single != null) {
            return single;
        }

        // 스프라이트 모드가 Multiple 이면 조각 배열로 나온다.
        Sprite[] pieces = Resources.LoadAll<Sprite>(background_resource);
        return pieces != null && pieces.Length > 0 ? pieces[0] : null;
    }

    void Build()
    {
        Camera cam = Camera.main;
        Vector3 center = cam != null ? cam.transform.position : Vector3.zero;
        float view_height = cam != null ? cam.orthographicSize * 2f : 10f;

        // 층: Scroll_layer 가 붙은 부모 + 타일 하나. 타일은 Scroll_layer 가 화면을 채울 만큼 복제한다.
        GameObject layer_object = new GameObject("Far_layer");
        layer_object.transform.SetParent(transform, false);

        GameObject tile = new GameObject("Tile");
        tile.transform.SetParent(layer_object.transform, false);

        SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sorting_order;

        // 화면 높이에 딱 맞게 키운다. 가로는 비율대로 따라오고, 부족하면 Scroll_layer 가 이어 붙인다.
        float sprite_height = Mathf.Max(0.01f, sprite.bounds.size.y);
        float scale = view_height / sprite_height;
        tile.transform.localScale = new Vector3(scale, scale, 1f);
        tile.transform.position = new Vector3(center.x, center.y, 0f);

        Scroll_layer layer = layer_object.AddComponent<Scroll_layer>();
        layer.tile = tile.transform;
        layer.parallax = parallax;
        layer.mirror_alternate = mirror_alternate;
    }
}
