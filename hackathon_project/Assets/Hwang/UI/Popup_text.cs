using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 위에 잠깐 뜨는 짧은 글. 줍기 대사, 데미지 숫자, "격파!" 같은 것.
// Resources/Popup_text.prefab (루트 + World Space Canvas + Text) 을 한 번 불러와 풀에서 돌려 쓴다. 매번 Instantiate 하지 않는다.
//
//   ShowAbove(target, line)              대상 머리 위에 띄우고 1.2초 동안 따라다닌다. 같은 대상의 이전 팝업은 즉시 사라진다
//   ShowDamage(position, amount, heal)   -35 / +60. 0.8초, 펀치, 위로 떠오름. 크기에 따라 흰/노랑/주황, 회복은 초록
//   ShowText(position, text, color)      "-50 폭발!", "격파!" 같은 자유 문구
//
// 콜라이더 없음, 레이캐스트 대상 아님. 판정·물리에 영향 없다.
// 타이머는 unscaledDeltaTime 이라 히트스톱(timeScale 0) 중에도 진행된다. 루트 z 는 항상 0.
public class Popup_text : MonoBehaviour
{

    public Text text;                       // 프리팹에서 연결. 이 슬롯 하나만 있다

    // 줍기 대사
    const float above_offset_y = 1.4f;      // 대상 몸 중심에서 머리 위까지
    const float above_duration = 1.2f;
    const float above_rise = 0.4f;
    const float above_fade_time = 0.3f;

    // 데미지 숫자
    const float damage_duration = 0.8f;
    const float damage_rise = 0.6f;
    const float damage_fade_time = 0.25f;
    const float damage_punch_time = 0.1f;
    const float damage_punch_scale = 1.4f;
    const float damage_spread_x = 0.3f;     // 좌우 랜덤 오프셋. 연타해도 겹치지 않게

    static readonly Color damage_small_color = Color.white;                   // 15 이하
    static readonly Color damage_medium_color = new Color(1f, 0.85f, 0.3f);   // 16 ~ 49
    static readonly Color damage_big_color = new Color(1f, 0.5f, 0.2f);       // 50 이상
    static readonly Color heal_color = new Color(0.5f, 0.9f, 0.5f);

    const int pool_max = 16;
    const string prefab_path = "Popup_text";

    static GameObject prefab;
    static readonly Stack<Popup_text> pool = new Stack<Popup_text>();
    static readonly Dictionary<Transform, Popup_text> following = new Dictionary<Transform, Popup_text>();

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        prefab = null;
        pool.Clear();
        following.Clear();
    }

    // ---------- 밖에서 부르는 것 ----------

    // 대상 머리 위에 대사. 대상을 따라다니며 위로 떠오르고 마지막에 옅어진다.
    public static void ShowAbove(Transform target, string line)
    {
        if (target == null || string.IsNullOrEmpty(line)) {
            return;
        }

        // 같은 대상에 이미 떠 있는 게 있으면 즉시 치운다. 연달아 주우면 새 문구만 남는다.
        Popup_text previous;
        if (following.TryGetValue(target, out previous) && previous != null) {
            previous.Release();
        }

        Popup_text popup = Take();
        if (popup == null) {
            return;
        }

        popup.follow_target = target;
        popup.base_position = target.position;
        popup.offset = new Vector2(0f, above_offset_y);
        popup.Begin(line, Color.white, above_duration, above_rise, above_fade_time, 0f, 1f);

        following[target] = popup;
    }

    // 데미지 숫자. heal 이면 +N 초록.
    public static void ShowDamage(Vector2 world_position, int amount, bool heal = false)
    {
        int size = Mathf.Abs(amount);
        string label = (heal ? "+" : "-") + size;

        Color color;
        if (heal) {
            color = heal_color;
        }
        else if (size <= 15) {
            color = damage_small_color;
        }
        else if (size <= 49) {
            color = damage_medium_color;
        }
        else {
            color = damage_big_color;
        }

        ShowText(world_position, label, color);
    }

    // 자유 문구. "-50 폭발!", "격파!" 등. 데미지 숫자와 같은 연출.
    public static void ShowText(Vector2 world_position, string content, Color color)
    {
        if (string.IsNullOrEmpty(content)) {
            return;
        }

        Popup_text popup = Take();
        if (popup == null) {
            return;
        }

        popup.follow_target = null;
        popup.base_position = world_position;
        popup.offset = new Vector2(Random.Range(-damage_spread_x, damage_spread_x), 0f);
        popup.Begin(content, color, damage_duration, damage_rise, damage_fade_time, damage_punch_time, damage_punch_scale);
    }

    // ---------- 풀 ----------

    static Popup_text Take()
    {
        // 씬이 바뀌면서 파괴된 게 스택에 남아 있을 수 있다. 살아 있는 걸 찾을 때까지 꺼낸다.
        while (pool.Count > 0) {
            Popup_text pooled = pool.Pop();
            if (pooled != null) {
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        if (prefab == null) {
            prefab = Resources.Load<GameObject>(prefab_path);
            if (prefab == null) {
                Debug.LogWarning("Popup_text : Resources/" + prefab_path + ".prefab 이 없어서 팝업을 띄울 수 없습니다.");
                return null;
            }
        }

        GameObject holder = Instantiate(prefab);
        holder.name = prefab.name;

        Popup_text popup = holder.GetComponent<Popup_text>();
        if (popup == null) {
            popup = holder.AddComponent<Popup_text>();
        }
        if (popup.text == null) {
            popup.text = holder.GetComponentInChildren<Text>();
        }

        return popup;
    }

    // 끝났다. 꺼서 풀에 돌려놓는다. 풀이 가득 차면 없앤다.
    void Release()
    {
        if (follow_target != null) {
            Popup_text current;
            if (following.TryGetValue(follow_target, out current) && current == this) {
                following.Remove(follow_target);
            }
        }
        follow_target = null;

        if (pool.Count >= pool_max) {
            Destroy(gameObject);
            return;
        }

        gameObject.SetActive(false);
        transform.localScale = Vector3.one;
        pool.Push(this);
    }

    // ---------- 진행 ----------

    Transform follow_target;        // 따라다닐 대상. 없으면 base_position 에 고정
    Vector2 base_position;
    Vector2 offset;                 // 대상(또는 base_position) 기준 오프셋. 떠오르는 양은 따로 더한다

    Color base_color;
    float duration;
    float elapsed;
    float rise;
    float fade_time;
    float punch_time;
    float punch_scale;

    void Begin(string content, Color color, float total, float rise_amount, float fade, float punch, float punch_amount)
    {
        base_color = color;
        duration = total;
        elapsed = 0f;
        rise = rise_amount;
        fade_time = fade;
        punch_time = punch;
        punch_scale = punch_amount;

        if (text != null) {
            text.text = content;
            text.color = color;
        }

        transform.localScale = punch_time > 0f ? Vector3.one * punch_scale : Vector3.one;
        Tick(0f);
    }

    void Update()
    {
        Tick(Time.unscaledDeltaTime);
    }

    void Tick(float dt)
    {
        elapsed += dt;

        if (elapsed >= duration) {
            Release();
            return;
        }

        // 대상이 사라졌으면 마지막 자리에 남는다.
        if (follow_target != null) {
            base_position = follow_target.position;
        }

        float t = duration > 0f ? elapsed / duration : 1f;

        // 위치. 시간에 비례해 위로 떠오른다. z 는 0 고정.
        Vector2 position = base_position + offset + new Vector2(0f, rise * t);
        transform.position = new Vector3(position.x, position.y, 0f);

        // 펀치. 처음 punch_time 동안 punch_scale 에서 1 로.
        if (punch_time > 0f && elapsed < punch_time) {
            float scale = Mathf.Lerp(punch_scale, 1f, elapsed / punch_time);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
        else if (transform.localScale.x != 1f) {
            transform.localScale = Vector3.one;
        }

        // 마지막 fade_time 동안 알파 아웃. Shadow 는 글자 알파를 따라간다 (useGraphicAlpha).
        if (text != null) {
            float remaining = duration - elapsed;
            Color color = base_color;
            if (fade_time > 0f && remaining < fade_time) {
                color.a *= Mathf.Clamp01(remaining / fade_time);
            }
            text.color = color;
        }
    }

    void OnDestroy()
    {
        if (follow_target != null) {
            Popup_text current;
            if (following.TryGetValue(follow_target, out current) && current == this) {
                following.Remove(follow_target);
            }
        }
    }
}
