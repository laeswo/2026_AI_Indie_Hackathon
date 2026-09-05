using System.Collections.Generic;
using UnityEngine;

// 바람 이펙트. 드래곤 날갯짓(2페이즈) 동안 하얀 바람 줄기가 화면 오른쪽에서 왼쪽으로 빠르게 지나간다.
// 씬에 놓을 필요 없다. Begin() 이 처음 불릴 때 하나 생기고, End() 뒤에는 남은 줄기가 화면 밖으로 나가며 저절로 정리된다.
// 줄기는 임시 원 스프라이트를 길게 늘린 것(끝이 둥근 선). 조명을 안 받는 Unlit 흰색이라 어두운 화면에서도 보인다.
// 오브젝트는 풀로 돌려 써서 프레임마다 새로 만들지 않는다. 세계가 흐르는 방향(왼쪽)으로만 분다.
public class Wind_effect : MonoBehaviour
{

    // 줄기 모양·움직임. 세기(strength)가 크면 더 많이, 더 빨리.
    internal float streaks_per_second = 16f;
    internal float speed_min = 14f;
    internal float speed_max = 22f;
    internal float length_min = 1.6f;
    internal float length_max = 3.6f;
    internal float thickness_min = 0.05f;
    internal float thickness_max = 0.11f;
    internal float alpha_min = 0.22f;
    internal float alpha_max = 0.5f;
    internal float drift_y = 0.6f;          // 위아래로 살짝 흐르는 속도 최대치
    internal float fade_in_time = 0.12f;
    internal float edge_fade = 1.5f;        // 화면 왼쪽 끝 이만큼 안에서 옅어지며 사라진다
    internal float spawn_margin = 1f;       // 화면 오른쪽 끝에서 이만큼 밖에서 시작
    internal float top_margin = 0.5f;       // 화면 위 끝 여유
    internal Color wind_color = new Color(1f, 1f, 1f, 1f);

    // 정렬. 앞쪽 줄기는 용사·아이템보다 앞(30), 뒤쪽 줄기는 배경보다 앞·용사보다 뒤(-5). 깊이감.
    internal int front_order = 30;
    internal int back_order = -5;
    internal float front_ratio = 0.6f;

    class Streak
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector2 velocity;
        public float alpha;
        public float age;
        public bool active;
    }

    static Wind_effect instance;

    readonly List<Streak> pool = new List<Streak>();
    float strength;
    bool blowing;
    float spawn_accumulator;
    float floor_y;

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        instance = null;
    }

    // ---------- 밖에서 부르는 것 ----------

    // 바람 시작. strength 는 세계가 빨라지는 배율(1 이면 평소). 줄기 수와 속도에 곱해진다.
    public static void Begin(float strength)
    {
        Wind_effect wind = Ensure();
        wind.strength = Mathf.Max(1f, strength);
        wind.blowing = true;
        wind.floor_y = FindFloorY();
    }

    // 바람 끝. 더 만들지 않고, 날아가던 줄기는 화면 밖으로 나가며 사라진다.
    public static void End()
    {
        if (instance != null) {
            instance.blowing = false;
        }
    }

    static Wind_effect Ensure()
    {
        if (instance != null) {
            return instance;
        }

        instance = FindFirstObjectByType<Wind_effect>();
        if (instance == null) {
            GameObject holder = new GameObject("Wind_effect");
            instance = holder.AddComponent<Wind_effect>();
        }
        return instance;
    }

    // 용사가 서 있는 바닥. 줄기는 바닥 조금 위에서 화면 위까지 분다.
    static float FindFloorY()
    {
        Player player = FindFirstObjectByType<Player>();
        if (player != null) {
            return player.FootY();
        }

        return Camera_director.RestPosition().y - Camera_director.RestSize();
    }

    void Awake()
    {
        if (instance == null) {
            instance = this;
        }
    }

    // ---------- 진행 ----------

    void Update()
    {
        float dt = Time.deltaTime;

        if (blowing && !Game_flow.is_over) {
            spawn_accumulator += dt * streaks_per_second * strength;
            while (spawn_accumulator >= 1f) {
                spawn_accumulator -= 1f;
                Spawn();
            }
        }

        float left_x = World_scroll.LeftX();

        foreach (Streak streak in pool) {
            if (!streak.active) {
                continue;
            }

            streak.age += dt;
            Vector3 position = streak.transform.position;
            position += (Vector3)(streak.velocity * dt);
            streak.transform.position = position;

            // 나타날 때 스르륵, 왼쪽 끝 근처에서 옅어지며 사라진다.
            float fade_in = fade_in_time > 0f ? Mathf.Clamp01(streak.age / fade_in_time) : 1f;
            float fade_out = edge_fade > 0f ? Mathf.Clamp01((position.x - left_x) / edge_fade) : 1f;

            Color color = wind_color;
            color.a = streak.alpha * fade_in * fade_out;
            streak.renderer.color = color;

            if (position.x < left_x - 0.5f) {
                streak.active = false;
                streak.renderer.enabled = false;
            }
        }
    }

    void Spawn()
    {
        Streak streak = Take();

        float top_y = World_scroll.TopY() - top_margin;
        float bottom_y = floor_y + 0.2f;
        if (top_y <= bottom_y) {
            top_y = bottom_y + 1f;
        }

        float length = Random.Range(length_min, length_max);
        float thickness = Random.Range(thickness_min, thickness_max);
        float speed = Random.Range(speed_min, speed_max) * Mathf.Lerp(1f, strength, 0.5f);

        streak.transform.position = new Vector3(World_scroll.RightX() + spawn_margin + length * 0.5f, Random.Range(bottom_y, top_y), 0f);
        streak.transform.rotation = Quaternion.identity;
        streak.velocity = new Vector2(-speed, Random.Range(-drift_y, drift_y));
        streak.alpha = Random.Range(alpha_min, alpha_max);
        streak.age = 0f;
        streak.active = true;

        // 임시 원을 길게 늘리면 끝이 둥근 선이 된다. 빠른 줄기는 더 길고 얇게 보이도록.
        Sprite_fit.FitSize(streak.renderer, length, thickness);

        bool front = Random.value < front_ratio;
        streak.renderer.sortingOrder = front ? front_order : back_order;

        Color color = wind_color;
        color.a = 0f;
        streak.renderer.color = color;
        streak.renderer.enabled = true;
    }

    // 쉬는 줄기를 하나 꺼내고, 없으면 만든다.
    Streak Take()
    {
        foreach (Streak streak in pool) {
            if (!streak.active) {
                return streak;
            }
        }

        GameObject holder = new GameObject("Wind_streak");
        holder.transform.SetParent(transform, false);

        SpriteRenderer renderer = holder.AddComponent<SpriteRenderer>();
        renderer.sprite = Placeholder_sprite.Circle();
        renderer.color = wind_color;
        Scene_lighting.ApplyUnlitMaterial(renderer);

        Streak made = new Streak();
        made.transform = holder.transform;
        made.renderer = renderer;
        pool.Add(made);
        return made;
    }
}
