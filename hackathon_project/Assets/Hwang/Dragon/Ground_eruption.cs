using System.Collections.Generic;
using UnityEngine;

// 내려찍기 분출 튜닝값 묶음. Dragon 이 채워서 Ground_eruption.Spawn 에 넘긴다.
public struct Ground_eruption_config
{
    public float segment_spacing;   // 세그먼트 간격 = 가로 폭. 같아야 빈틈 없이 이어진다
    public float segment_interval;  // 다음 세그먼트가 솟기까지의 시간
    public float rise_time;         // 바닥 아래에서 솟는 시간
    public float hold_time;         // 솟은 채 유지
    public float sink_time;         // 다시 가라앉는 시간
    public float height;            // 기준 높이. 실제는 0.75~1.0 배 랜덤
    public int damage;
    public bool art_faces_left;     // 땅 조각 그림이 왼쪽을 향해 그려졌으면 true. 분출 방향에 맞춰 뒤집는다
}

// 내려찍기 충격파. 착지점에서 용사 쪽으로 땅 조각이 한 칸씩 연달아 솟았다가 가라앉는다.
// 분출 하나가 이 컴포넌트 하나다(빈 GameObject). 세그먼트(ground 프리팹 인스턴스)를 순서대로 낳고 수명을 돌린다.
// 세그먼트는 반드시 바닥 아래에서 위로 솟는다. 위에서 떨어지는 것처럼 보이면 안 된다.
// 용사에게 콜라이더가 없으므로 박스로 직접 판정한다. 낮아서(height) 탭 점프로 넘는다.
public class Ground_eruption : MonoBehaviour
{

    // 땅 조각 하나. 나이(age)로 지금 어디쯤 솟아 있는지 정한다.
    class Segment
    {
        public Transform transform;
        public SpriteRenderer renderer;   // 그림. 밑변을 맞추는 기준. 없으면 중심 피벗 1×1 로 친다
        public float center_x;
        public float height;              // 실제 렌더 높이. 판정도 이 값
        public float bottom_y;            // 지금 밑변 y. 바닥 아래에서 올라와 floor_y 에 닿는다
        public float age;
    }

    Ground_eruption_config config;
    GameObject prefab;
    float floor_y;
    float direction_sign;
    float end_x;

    float next_x;          // 다음 세그먼트가 솟을 x
    float spawn_timer;
    bool spawning_done;
    bool hit_done;         // 분출 하나당 한 번만 맞힌다

    readonly List<Segment> segments = new List<Segment>();

    Transform player;
    Player_health health;
    Player_jump jump;
    Player player_component;    // 발 위치는 용사 그림에서 잰 값을 쓴다

    // 착지점(바닥 y)에서 direction_sign 쪽으로 end_x 까지 분출한다.
    public static Ground_eruption Spawn(GameObject prefab, Vector2 impact_floor_point, float direction_sign, float end_x, Ground_eruption_config config)
    {
        GameObject holder = new GameObject("Ground_eruption");
        holder.transform.position = impact_floor_point;

        Ground_eruption eruption = holder.AddComponent<Ground_eruption>();
        eruption.prefab = prefab;
        eruption.config = config;
        eruption.floor_y = impact_floor_point.y;
        eruption.direction_sign = direction_sign >= 0f ? 1f : -1f;
        eruption.end_x = end_x;
        eruption.next_x = impact_floor_point.x;
        eruption.spawn_timer = 0f;   // 첫 조각은 바로 솟는다

        return eruption;
    }

    void Awake()
    {
        GameObject player_object = GameObject.FindWithTag("Player");
        if (player_object != null) {
            player = player_object.transform;
            health = player_object.GetComponent<Player_health>();
            jump = player_object.GetComponent<Player_jump>();
            player_component = player_object.GetComponent<Player>();
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (!spawning_done) {
            TickSpawn(dt);
        }

        TickSegments(dt);
        TryHit();

        // 다 낳았고 다 가라앉았으면 분출 끝.
        if (spawning_done && segments.Count == 0) {
            Destroy(gameObject);
        }
    }

    // ---------- 세그먼트 낳기 ----------

    void TickSpawn(float dt)
    {
        // 게임이 끝나면 더 만들지 않는다. 있는 것만 가라앉는다.
        if (Game_flow.is_over) {
            spawning_done = true;
            return;
        }

        spawn_timer -= dt;

        // 프레임이 길어도 간격을 지키게 while 로 밀린 만큼 낳는다.
        float interval = Mathf.Max(0.001f, config.segment_interval);
        while (spawn_timer <= 0f && !spawning_done) {
            SpawnSegment(next_x);

            next_x += direction_sign * config.segment_spacing;
            spawn_timer += interval;

            // 용사를 지나 end_x 에 닿으면 멈춘다.
            bool passed = direction_sign > 0f ? next_x > end_x : next_x < end_x;
            if (passed) {
                spawning_done = true;
            }
        }
    }

    void SpawnSegment(float center_x)
    {
        // 울퉁불퉁하게. 판정도 이 높이를 쓴다.
        float height = config.height * Random.Range(0.75f, 1f);

        GameObject instance = Instantiate(prefab, new Vector3(center_x, floor_y, 0f), Quaternion.identity);
        instance.name = "Ground_segment";
        // 임시 원본이 꺼져 있을 수 있어서 켜 준다. 프리팹이면 이미 켜져 있다.
        instance.SetActive(true);

        SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null) {
            // 용사보다 뒤, 배경보다 앞.
            renderer.sortingOrder = 5;

            // 그림 bounds 로 폭·높이를 맞춘다. 1×1 이라는 가정은 없다. 판정 높이는 실제 렌더 높이.
            Sprite_fit.FitSize(renderer, config.segment_spacing, height);
            Sprite_fit.Face(renderer, direction_sign, config.art_faces_left);
            height = Sprite_fit.WorldSize(renderer).y;
        }
        else {
            instance.transform.localScale = new Vector3(config.segment_spacing, height, 1f);
        }

        Segment segment = new Segment();
        segment.transform = instance.transform;
        segment.renderer = renderer;
        segment.center_x = center_x;
        segment.height = height;
        segment.age = 0f;

        // 같은 프레임에 바닥 아래로 내려 놓는다. 그래야 한 프레임도 엉뚱한 곳에 안 보인다.
        PlaceSegment(segment);
        segments.Add(segment);
    }

    // ---------- 세그먼트 수명 ----------

    void TickSegments(float dt)
    {
        float hold_end = config.rise_time + config.hold_time;
        float life = hold_end + config.sink_time;

        for (int i = segments.Count - 1; i >= 0; i--) {
            Segment segment = segments[i];
            segment.age += dt;

            // 게임이 끝났으면 유지를 건너뛰고 바로 가라앉기 시작한다.
            if (Game_flow.is_over && segment.age < hold_end) {
                segment.age = hold_end;
            }

            if (segment.age >= life || segment.transform == null) {
                if (segment.transform != null) {
                    Destroy(segment.transform.gameObject);
                }
                segments.RemoveAt(i);
                continue;
            }

            PlaceSegment(segment);
        }
    }

    // 나이에 맞는 밑변 높이로 놓는다. 피벗이 어디든 그림의 밑변이 bottom_y 에 오게 맞춘다.
    void PlaceSegment(Segment segment)
    {
        segment.bottom_y = BottomY(segment);

        if (segment.renderer != null) {
            Sprite_fit.AlignBottomTo(segment.transform, segment.renderer, segment.bottom_y);
            return;
        }

        Vector3 position = segment.transform.position;
        position.y = segment.bottom_y + segment.height * 0.5f;
        segment.transform.position = position;
    }

    // 완전히 숨은 자리의 밑변. 윗변이 바닥보다 height/2 아래에 있다.
    float HiddenBottomY(float height)
    {
        return floor_y - height * 1.5f;
    }

    // 나이로 지금 밑변 y 를 정한다. 아래에서 솟아(rise) → 유지(hold) → 가라앉음(sink). 다 솟으면 밑변이 바닥에 닿는다.
    float BottomY(Segment segment)
    {
        float down = HiddenBottomY(segment.height);
        float up = floor_y;

        float rise = Mathf.Max(0.001f, config.rise_time);
        if (segment.age < rise) {
            // 솟을 때는 빠르게 튀어나와 멈춘다.
            return Mathf.Lerp(down, up, Dragon.EaseOut(segment.age / rise));
        }

        float hold_end = rise + config.hold_time;
        if (segment.age < hold_end) {
            return up;
        }

        // 가라앉을 때는 점점 빨라진다.
        float sink = Mathf.Max(0.001f, config.sink_time);
        float t = Mathf.Clamp01((segment.age - hold_end) / sink);
        return Mathf.Lerp(up, down, t * t);
    }

    // 지금 바닥 위로 나와 있는 윗변 높이. 바닥 아래면 0.
    float TopAboveFloor(Segment segment)
    {
        if (segment.transform == null) {
            return 0f;
        }

        float top = segment.bottom_y + segment.height;
        return Mathf.Max(0f, top - floor_y);
    }

    // ---------- 판정 ----------

    void TryHit()
    {
        if (hit_done || player == null || Game_flow.is_over) {
            return;
        }

        foreach (Segment segment in segments) {
            if (!IsPlayerOn(segment)) {
                continue;
            }

            // 분출 하나는 한 번만 본다. 무적이면 TakeHit 이 알아서 무시한다.
            hit_done = true;

            if (health != null) {
                health.TakeHit(config.damage);
            }
            return;
        }
    }

    // 지금 이 순간 용사 발 높이. 서 있으면 바닥과 같고, 뛰면 그만큼 올라간다.
    // 몸 중심에서 발까지는 용사 그림에서 잰 값(Player.foot_offset)을 쓴다. 없으면 "서 있을 때 몸 중심 - 바닥" 으로 구한다.
    float PlayerFootY()
    {
        if (player_component != null) {
            return player_component.FootY();
        }

        float body_to_floor = jump != null ? jump.ground_y - floor_y : 1f;
        return player.position.y - body_to_floor;
    }

    // 용사 x 가 세그먼트 가로 범위 안이고, 발이 지금 나와 있는 윗변보다 낮으면 맞는다.
    // OnDrawGizmos 가 그리는 박스와 같은 식이다. 그림과 판정이 같아야 한다.
    bool IsPlayerOn(Segment segment)
    {
        if (player == null) {
            return false;
        }

        float top = TopAboveFloor(segment);
        if (top <= 0f) {
            return false;
        }

        float dx = Mathf.Abs(player.position.x - segment.center_x);
        if (dx > config.segment_spacing * 0.5f) {
            return false;
        }

        return PlayerFootY() < floor_y + top;
    }

    void OnDrawGizmos()
    {
        foreach (Segment segment in segments) {
            float top = TopAboveFloor(segment);
            if (top <= 0f) {
                continue;
            }

            Gizmos.color = IsPlayerOn(segment) ? Color.red : Color.yellow;
            Gizmos.DrawWireCube(
                new Vector3(segment.center_x, floor_y + top * 0.5f, 0f),
                new Vector3(config.segment_spacing, top, 0f)
            );
        }
    }
}
