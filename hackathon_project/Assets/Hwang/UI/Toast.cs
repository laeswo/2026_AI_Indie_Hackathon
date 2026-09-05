using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 화면 상단 가운데에 잠깐 뜨는 한 줄 안내. 튜토리얼 조작 안내에 쓴다.
// Resources/Toast.prefab (루트 + Screen Space Canvas + 패널 + Text) 을 불러와 슬롯에 글자만 써넣는다.
//
//   Show(line, seconds)   0.2초 페이드 인 → seconds 유지 → 0.3초 페이드 아웃
//
// 표시 중에 새 요청이 오면 큐에 넣고 순서대로 보여준다. 인트로가 떠 있는 동안은 쌓아 두었다가 끝나면 보여준다.
// 타이머는 unscaledDeltaTime 이라 히트스톱·인트로(timeScale 0) 중에도 진행된다.
public class Toast : MonoBehaviour
{

    public CanvasGroup group;               // 패널의 CanvasGroup. 알파로 페이드한다. 비우면 패널에 하나 붙인다
    public Text text;

    const float fade_in_time = 0.2f;
    const float fade_out_time = 0.3f;
    const float gap_time = 0.15f;           // 연달아 나올 때 사이 간격
    const string prefab_path = "Toast";

    struct Request
    {
        public string line;
        public float seconds;
    }

    static Toast instance;
    static readonly Queue<Request> queue = new Queue<Request>();

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        instance = null;
        queue.Clear();
    }

    public static void Show(string line, float seconds = 2.5f)
    {
        if (string.IsNullOrEmpty(line)) {
            return;
        }

        Toast toast = Ensure();
        if (toast == null) {
            return;
        }

        queue.Enqueue(new Request { line = line, seconds = Mathf.Max(0.1f, seconds) });
    }

    static Toast Ensure()
    {
        if (instance != null) {
            return instance;
        }

        instance = FindFirstObjectByType<Toast>();
        if (instance != null) {
            return instance;
        }

        GameObject prefab = Resources.Load<GameObject>(prefab_path);
        if (prefab == null) {
            Debug.LogWarning("Toast : Resources/" + prefab_path + ".prefab 이 없어서 안내를 띄울 수 없습니다.");
            return null;
        }

        GameObject spawned = Instantiate(prefab);
        spawned.name = prefab.name;
        instance = spawned.GetComponent<Toast>();
        return instance;
    }

    // ---------- 진행 ----------

    enum Phase { idle, fade_in, hold, fade_out, gap }

    Phase phase = Phase.idle;
    float timer;
    float hold_seconds;

    void Awake()
    {
        if (instance == null) {
            instance = this;
        }

        if (group == null && text != null) {
            group = text.GetComponentInParent<CanvasGroup>();
        }
        if (group == null && text != null) {
            group = text.transform.parent.gameObject.AddComponent<CanvasGroup>();
        }

        if (group != null) {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        switch (phase) {
            case Phase.idle:
                // 인트로가 떠 있는 동안은 기다린다. 끝나면 쌓인 것부터 차례로.
                if (queue.Count == 0 || Intro_overlay.is_playing) {
                    return;
                }
                Begin(queue.Dequeue());
                break;

            case Phase.fade_in:
                timer += dt;
                SetAlpha(timer / fade_in_time);
                if (timer >= fade_in_time) {
                    phase = Phase.hold;
                    timer = 0f;
                }
                break;

            case Phase.hold:
                timer += dt;
                if (timer >= hold_seconds) {
                    phase = Phase.fade_out;
                    timer = 0f;
                }
                break;

            case Phase.fade_out:
                timer += dt;
                SetAlpha(1f - timer / fade_out_time);
                if (timer >= fade_out_time) {
                    phase = Phase.gap;
                    timer = 0f;
                }
                break;

            case Phase.gap:
                timer += dt;
                if (timer >= gap_time) {
                    phase = Phase.idle;
                }
                break;
        }
    }

    void Begin(Request request)
    {
        if (text != null) {
            text.text = request.line;
        }

        hold_seconds = request.seconds;
        phase = Phase.fade_in;
        timer = 0f;
        SetAlpha(0f);
    }

    void SetAlpha(float alpha)
    {
        if (group != null) {
            group.alpha = Mathf.Clamp01(alpha);
        }
    }

    void OnDestroy()
    {
        if (instance == this) {
            instance = null;

            // 씬이 바뀌면 지난 판에서 못 보여준 안내는 버린다. 새 판에서 엉뚱하게 뜨지 않게.
            queue.Clear();
        }
    }
}
