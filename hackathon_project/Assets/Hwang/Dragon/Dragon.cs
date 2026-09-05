using UnityEngine;

// 용사 뒤에서 따라오는 드래곤. 씬의 오브젝트에 직접 붙여서 쓴다.
//
// 동작은 상태머신으로 돌아간다. 현재 상태 하나가 매 물리 스텝 FixedTick 을 받고, 다음 상태로 ChangeState 한다.
//   State_enter            등장. 용사 반대쪽에서 제자리로 날아온다. 공격 없음
//   State_idle             제자리 부유. 간격이 차면 페이즈에 맞는 패턴을 고른다. 페이즈 2·큰 화염구 예약도 여기서 시작
//   State_charge_*         돌진. 예고 → 위/아래 중 랜덤 높이로 → 화면 밖까지 → 위로 호를 그리며 복귀(무적)
//                          아래는 롱점프로 넘고, 위는 뛰면 맞으니 가만히 있어야 한다
//   State_breath_*         브레스(2페이즈). 예고 → 바닥 불이 천천히 퍼짐. 상쇄 불가, 롱점프로만 피한다
//   State_wing_*           날갯짓(2페이즈). 바람이 불어 작물과 배경이 빨라진다. 데미지 없음
//   State_roar             페이즈 2 진입 포효. 무적
//   State_spit             작물 eat_count 개 먹은 뒤 큰 화염구 뱉기. 아래(앉아서 직선) / 위(제자리에서 포물선). 두 페이즈 모두.
//                          직선은 작물 2번, 포물선은 1번으로 격추. 포물선은 작아서 롱점프로도 넘는다
//   State_slam_*           내려찍기(1페이즈). 예고 → 살짝 떠올랐다가 바닥으로 쿵 → 착지점부터 용사 쪽으로 땅이 연달아 솟음 → 복귀
//                          솟는 땅은 낮아서 탭 점프로 넘는다. 착지 후 눌러앉은 동안 맞힐 수 있다
//   State_stagger          상쇄(그로기). 성검에 맞으면 하던 패턴이 끊기고 무방비. 받는 데미지 2배. 끝나면 Idle
// 밥먹기는 상태가 아니라 상시 동작이다. 어느 페이즈든 상태가 can_eat 이면(부유 중, 아래 돌진 중) 몸에 닿은 흘러오는 작물을 삼킨다.
//
//   1페이즈: 돌진(데미지 1) / 내려찍기(충격파 데미지 1) / 먹고 큰 화염구(데미지 2)
//   2페이즈: 위에 더해 브레스·날갯짓. 먹고 큰 화염구는 2페이즈에도 그대로
//   2페이즈: 돌진(데미지 2, 간격 빠름) / 브레스(데미지 3) / 날갯짓(바람)
//
// 이 파일은 설정값·공용 헬퍼·HP 만 갖는다. HUD 는 UI/Hud.cs 가 값을 읽어 그린다. 각 상태의 동작은 Dragon_states_*.cs 에 있다.
// 붙이면 Rigidbody2D 와 CircleCollider2D 가 같이 생기고, Awake 에서 Kinematic + 트리거로 맞춘다.
// 그림은 이 오브젝트나 자식에 있는 SpriteRenderer 를 쓴다. 없으면 경고하고 임시 원을 띄운다.
// 그림 크기·피벗은 가정하지 않는다. 몸 크기(BodyRadius)는 콜라이더 반지름이므로 그림을 넣은 사람이 인스펙터에서 맞춘다.
// 틴트는 전부 원래 색(base_color) 기준으로 섞고, 방향은 art_faces_left 를 반영해 flipX 로 뒤집는다.
// Animator 가 있으면 telegraph / charge / breath / wing / slam / spit / roar / hurt / eat 트리거와 int phase 를 넣는다. 없어도 된다.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Dragon : MonoBehaviour
{

    [Header("체력")]
    public int max_hp = 130;

    // 그림 방향. 그림은 오른쪽을 본다는 전제이고, 반대로 그린 그림이면 켠다. 프리팹 슬롯의 그림도 여기서 알려준다.
    [Header("그림")]
    public bool art_faces_left = false;         // 드래곤 몸 그림
    public bool breath_art_faces_left = false;  // 브레스 줄기·띠. 띠가 오른쪽→왼쪽으로 퍼지는 모양이면 켠다
    public bool ground_art_faces_left = false;  // 내려찍기 땅 조각

    // 이 속도(유닛/초) 이상으로 옆으로 움직이는 동안은 진행 방향을 보고, 그 외에는 용사 쪽을 본다. 부유 흔들림(0.3)보다 크게.
    internal float face_move_speed = 3f;

    // 부유. 씬에 놓은 자리를 위쪽 끝으로 삼아 작물 줄까지 내려갔다 올라온다. 좌우로도 조금 흔들린다.
    internal float hover_amplitude_x = 0.6f;
    internal float hover_speed_x = 0.5f;

    // 등장. 씬에 놓은 자리에서 용사 반대쪽으로 enter_distance 만큼 물러난 곳에서 날아 들어온다.
    internal bool enter_from_behind = true;
    internal float enter_distance = 6f;
    internal float enter_time = 2f;

    [Header("공격 간격 (초)")]
    public float attack_interval_min = 2.0f;
    public float attack_interval_max = 2.9f;

    internal float fireball_offset_x = 0.8f;  // 입 위치. 몸 중심에서 용사 쪽으로
    internal float fireball_lane_y = 0.3f;    // 큰 화염구 높이. 용사 몸 중심 기준

    // 돌진. 위/아래 중 하나를 랜덤으로 골라 화면 밖까지 지나간다. 1페이즈는 이것만 쓴다.
    [Header("돌진")]
    public float sweep_telegraph_time = 0.8f;
    public float sweep_speed = 26f;
    internal float sweep_dive_time = 0.4f;
    internal float sweep_exit_margin = 1.5f;  // 화면 끝에서 이만큼 더 나간 뒤 돌아오기 시작한다
    internal float sweep_return_time = 1.2f;
    internal float sweep_arc_height = 2f;     // 돌아올 때 화면 위 끝보다 이만큼 더 높이 솟는다
    internal float sweep_hit_half_width = 1.2f;
    internal float charge_low_offset = 0.2f;      // 아래 돌진 높이. 용사 몸 중심 기준
    internal float charge_low_safe_height = 2.4f; // 아래 돌진: 이보다 높이 떠 있으면 안 맞는다. 탭(1.65)보다 높게
    internal float charge_high_offset = 2.6f;     // 위 돌진 높이. 서 있으면 머리 위로 지나간다
    internal float charge_high_hit_height = 0.9f; // 위 돌진: 이보다 떠 있으면 맞는다 (뛰면 맞음)
    internal int charge_damage_p1 = 1;
    internal int charge_damage_p2 = 2;

    // 위로 돌아오는 동안 무적. 화면 밖에서 호를 그리며 오는 걸 맞히는 건 운이라 아예 막는다.
    internal bool invincible_while_returning = true;
    internal float invincible_alpha = 0.55f;

    // 브레스. breath_prefab 은 위쪽이 입에 닿는 줄기(입에서 바닥으로), breath_floor_prefab 은 바닥에서 옆으로 퍼지는 띠.
    // 둘 다 스프라이트만 있으면 되고 크기·피벗은 상관없다(bounds 로 맞춘다). 띠는 Full Rect 로 임포트하면 텍스처가 반복(Tiled)된다.
    [Header("브레스 (2페이즈)")]
    public GameObject breath_prefab;
    public GameObject breath_floor_prefab;
    public float breath_ready_time = 4f;        // 준비. 주황 깜빡임
    public float breath_spread_time = 3f;       // 바닥에서 용사 쪽으로 퍼지는 시간. 느릴수록 절망적이다
    internal float breath_shoot_time = 0.25f;   // 입에서 바닥까지 뿜는 시간. 이 동안은 안 맞는다
    internal float breath_shoot_width = 0.8f;   // 입에서 바닥으로 가는 줄기의 끝 폭
    internal float breath_impact_distance = 2f; // 드래곤 앞 이만큼 떨어진 바닥에 떨어진다
    internal float breath_hold_time = 0.5f;     // 다 퍼진 뒤 유지
    internal float breath_fade_time = 0.4f;     // 옅어지며 사라짐
    internal int breath_damage = 3;
    internal float breath_height = 3f;          // 바닥 불의 높이. 발에서 재서 탭 점프(1.65)로는 못 넘고 홀드(3.54)로는 넘게
    internal float breath_extra_length = 1.5f;  // 용사를 지나 이만큼 더 퍼진다
    internal float breath_player_radius = 0f;   // 0 이면 보이는 네모가 곧 판정
    // 바닥 높이(불이 깔리는 y)는 용사 그림에서 잰 발 위치로 구한다 → FloorY()

    // 내려찍기. 떠 있다가 그 자리에서 바닥으로 뚝 떨어지고, 착지점부터 용사 쪽으로 땅 조각이 한 칸씩 연달아 솟았다 가라앉는다.
    // ground_prefab 은 땅 조각 스프라이트만 있으면 된다. 크기·피벗은 상관없다(bounds 로 맞추고 밑변을 바닥에 붙인다). 분출 동작(Ground_eruption)은 실행 중에 붙인다.
    [Header("내려찍기 (1페이즈)")]
    public GameObject ground_prefab;
    internal float slam_telegraph_time = 0.9f;  // 예고. 갈색 깜빡임
    internal float slam_lift_height = 1.2f;     // 떨어지기 전에 이만큼 위로 살짝 떠오른다. "쿵" 을 예고하는 예비 동작
    internal float slam_lift_time = 0.25f;      // 떠오르는 시간. 끝에서 잠깐 멈칫한 느낌이 나게 EaseOut
    internal float slam_drop_time = 0.3f;       // 떠오른 자리에서 바닥까지 떨어지는 시간. 짧을수록 갑작스럽다
    internal float slam_stun_time = 0.35f;      // 착지 후 눌러앉아 있는 시간. 이때 맞힐 수 있다
    internal float slam_rise_time = 0.6f;       // 부유 위치로 돌아오는 시간
    internal float wave_height = 0.8f;          // 땅 조각 기준 높이. 발에서 재서 탭 점프(1.65)로 확실히 넘게. 실제는 0.75~1배 랜덤
    internal int wave_damage = 1;
    internal float segment_spacing = 0.9f;      // 조각 간격 = 조각 가로 폭. 앞머리 속도는 spacing / interval ≈ 12.9 유닛/초
    internal float segment_interval = 0.07f;    // 다음 조각이 솟기까지
    internal float segment_rise_time = 0.1f;    // 바닥 아래에서 솟는 시간
    internal float segment_hold_time = 0.3f;    // 솟은 채 유지. rise+hold+sink ≈ 0.55초가 용사 아래에 있는 시간
    internal float segment_sink_time = 0.15f;   // 가라앉는 시간
    internal float eruption_overshoot = 2f;     // 용사를 지나 이만큼 더 간다
    internal float phase1_slam_chance = 0.5f;   // 1페이즈에서 내려찍기 확률. 나머지는 돌진
    internal float slam_shake_amplitude = 0.35f; // 착지 화면 흔들림. 충격파 직후 한 번만. 떨어지는 동안은 안 흔든다
    internal float slam_shake_time = 0.35f;

    [Header("밥먹기 → 큰 화염구")]
    public Fireball eat_fireball_prefab;        // 다 먹으면 쏘는 큰 화염구
    public int eat_count = 3;                   // 이만큼 먹으면 쏜다
    public float hover_dip_speed = 0.9f;        // 내려갔다 올라오는 빠르기 (rad/s). 0.9 면 한 바퀴 7초쯤
    internal float sit_down_time = 0.4f;        // 뱉을 자리(바닥 또는 제자리)로 가는 시간
    internal float bigfire_ready_time = 1.2f;   // 자리에서 깜빡이는 시간
    internal float sit_up_time = 0.6f;          // 부유 위치로 돌아오는 시간
    internal float bigfire_scale = 3.2f;   // 직선(아래) 지름(유닛). 클수록 작물로 맞히기 쉽다. 판정 반경도 같이 커진다
    internal int bigfire_damage = 2;
    internal int bigfire_hits_to_break = 2;     // 직선(아래). 크고 느려서 두 번
    internal int bigfire_arc_hits_to_break = 1; // 포물선(위). 작고 빨라서 한 번
    internal float bigfire_speed_scale = 0.8f;
    internal float bigfire_arc_scale = 2.0f;    // 포물선(위) 지름(유닛). 롱점프로 넘을 수 있게 직선보다 작다
    internal float bigfire_arc_time = 1.2f;     // 포물선 비행 시간. 이 시간에 용사 발밑에 떨어지게 초속을 정한다
    internal float bigfire_arc_gravity = 12f;   // 포물선 중력

    [Header("페이즈 2")]
    public float phase2_hp_ratio = 0.5f;        // 이 비율 아래로 떨어지면 페이즈 2
    public float phase2_speed = 1.4f;           // 빨라지는 배율. 예고는 나누고 쓸기·퍼짐은 곱한다
    public float phase2_interval_scale = 2f;    // 공격 간격만 따로. 2 면 간격이 절반. 행동 주기가 짧아진다
    internal float phase2_roar_time = 2.2f;     // 포효. 무적이고 공격 안 함. 첫 1초는 슬로우모션
    internal float phase2_slowmo_scale = 0.3f;
    internal float phase2_slowmo_time = 1f;
    internal float roar_shake_amplitude = 0.25f; // 포효 시작 화면 흔들림. 내려찍기보다 약하게
    internal float roar_shake_time = 0.5f;
    internal float phase2_charge_chance = 0.4f; // 2페이즈 확률. 나머지는 날갯짓
    internal float phase2_breath_chance = 0.3f;

    // 날갯짓 (2페이즈). 바람이 불어 작물과 배경이 wind_speed_multiplier 배로 빨라진다.
    internal float wing_telegraph_time = 0.8f;
    internal float wing_duration = 4f;
    internal float wind_speed_multiplier = 2.2f;

    // 피격. Crop_data 가 없는 작물은 default_damage 를 준다.
    internal int default_damage = 10;
    internal float hurt_flash_time = 0.25f;
    internal float hit_hitstop_time = 0.05f;    // 작물 명중 때 아주 짧게 멈칫. 연타해도 큰 값 유지라 누적되지 않는다

    // 상쇄(성검). 하던 패턴이 끊기고 무방비. 이 게임에서 제일 센 한 방이라 연출도 제일 세다.
    // 등장·포효 중에는 성검도 안 통한다. 그 외엔 무적(돌진 복귀)이어도 통한다.
    internal float stagger_time = 3.5f;
    internal float stagger_sink = 0.6f;             // 그로기 동안 아래로 처지는 높이
    internal float stagger_damage_multiplier = 2f;  // 그로기 중 받는 데미지 배수
    internal float stagger_flash_time = 0.3f;       // 진입 순간 몸이 하얗게
    internal float stagger_edge_margin = 2f;        // 화면 밖에서 맞았으면 이만큼 안쪽으로 당긴다
    internal Color stagger_color = new Color(0.6f, 0.6f, 0.7f);   // 그로기 회색
    internal Color stagger_gold = new Color(1f, 0.9f, 0.5f);      // 상쇄 금빛

    // 격추 연출. Game_flow.End 직전에.
    internal float end_shake_amplitude = 0.3f;
    internal float end_shake_time = 0.5f;
    internal float end_zoom_amount = 0.12f;
    internal float end_zoom_time = 0.8f;
    internal float end_flash_time = 0.3f;
    internal Color end_flash_color = new Color(1f, 1f, 1f, 0.6f);

    // 패턴별 카메라 연출. 셰이크 값은 각 패턴 블록에 있고, 줌·플래시는 여기 모아 둔다.
    internal float slam_zoom_amount = 0.06f;    // 내려찍기 착지. 살짝 당겼다 복귀
    internal float slam_zoom_time = 0.3f;
    internal float roar_zoom_amount = 0.10f;    // 페이즈 2 포효
    internal float roar_zoom_time = 0.6f;
    internal float roar_flash_time = 0.4f;
    internal Color roar_flash_color = new Color(1f, 0.3f, 0.3f, 0.35f);
    internal float breath_land_shake_amplitude = 0.15f;  // 브레스 바닥 불이 붙는 순간
    internal float breath_land_shake_time = 0.25f;
    internal float breath_land_flash_time = 0.15f;
    internal Color breath_land_flash_color = new Color(1f, 0.55f, 0.1f, 0.35f);

    // 색은 스프라이트에 곱해진다. 원래 색(base_color)은 Awake 에서 그림의 color 를 읽어 두고, 모든 틴트는 그 색 기준으로 Lerp 한다(Tint).
    internal Color base_color = Color.white;
    internal Color normal_color { get { return base_color; } }   // Dragon_state 기본 GetColor 가 읽는 이름. base_color 와 같다
    internal Color hurt_color = new Color(1f, 0.55f, 0.5f);
    internal Color heal_color = new Color(0.55f, 1f, 0.6f);   // 생명포션에 맞아 회복될 때
    internal Color sweep_telegraph_color = new Color(1f, 0.35f, 0.25f);
    internal Color wing_color = new Color(0.55f, 0.9f, 1f);
    internal Color breath_telegraph_color = new Color(1f, 0.5f, 0.1f);
    internal Color eat_color = new Color(0.6f, 0.4f, 1f);         // 아래서 뱉기
    internal Color eat_high_color = new Color(0.78f, 0.62f, 1f);  // 위에서 뱉기. 구분되게 살짝 밝다
    internal Color slam_color = new Color(0.75f, 0.5f, 0.25f);    // 내려찍기 예고·낙하. 충격파(갈색 땅)와 같은 계열
    internal Color roar_color = new Color(1f, 0.3f, 0.3f);
    internal Color phase2_tint = new Color(1f, 0.5f, 0.45f);
    Color placeholder_color = new Color(0.45f, 0.68f, 0.35f);

    public int hp { get; private set; }
    public int phase { get; private set; } = 1;

    // 상태머신. 상태 클래스들이 아래 것들을 읽고 쓴다.
    Dragon_state state;

    internal Rigidbody2D body { get; private set; }
    internal Transform player { get; private set; }
    internal Vector2 base_position { get; private set; }

    internal float attack_timer;        // Idle 에서만 줄어든다
    internal bool phase2_pending;       // HP 는 넘었는데 아직 하던 동작이 안 끝났을 때
    internal bool bigfire_pending;      // 다 먹었는데 아직 하던 동작이 안 끝났을 때
    public int eat_stack { get; internal set; }   // HUD 가 읽는다. 쓰는 건 드래곤과 상태뿐
    bool phase2_opening_pending;        // 포효 직후 첫 패턴은 무조건 날갯짓
    string last_pattern;                // 직전에 고른 패턴 이름. 같은 것이 세 번 연속 나오지 않게
    int same_pattern_count;             // 같은 패턴이 몇 번 연속됐는지
    internal bool next_spit_high = true; // 뱉기는 위 → 아래 → 위 … 번갈아. State_spit 이 읽고 뒤집는다

    SpriteRenderer sprite_renderer;
    Animator animator;                  // 있으면 상태 트리거를 넣는다. 없어도 된다
    Player player_component;            // 발 위치(foot_offset)를 여기서 읽는다
    CircleCollider2D circle_collider;
    Spawn_crop spawner;                 // 작물이 흐르는 높이를 여기서 읽는다
    float previous_x;                   // 그림 방향을 정할 때 쓰는 직전 x
    float facing_sign = 1f;

    // 몸 주변 불빛. 평소엔 꺼져 있고, 예고로 색이 진해질수록 그 색으로 밝아진다. 맞으면 번쩍.
    Glow_light body_glow;
    internal float body_glow_intensity = 0f;      // 평소. 0 이면 예고·피격 때만 빛난다
    internal float body_glow_tint_boost = 1.0f;   // 틴트가 진할 때 더해지는 최대치
    internal float body_glow_radius = 3.2f;
    internal float hurt_glow_intensity = 1.4f;    // 맞은 순간 번쩍
    internal float hurt_glow_time = 0.2f;
    internal Color eruption_glow_color = new Color(1f, 0.7f, 0.4f);  // 내려찍기 착지. 흙먼지 속 불꽃
    internal float eruption_glow_intensity = 1.6f;
    internal float eruption_glow_radius = 4.5f;
    internal float eruption_glow_time = 0.4f;
    GameObject ground_placeholder;      // ground_prefab 이 비었을 때 땅 조각으로 복제할 임시 원본. 꺼둔 채로 둔다
    float hover_phase;
    float hurt_timer;
    float heal_timer;
    float stagger_flash_timer;

    public bool is_invincible
    {
        get { return state != null && state.is_invincible; }
    }

    // 위로 돌아오는 중. 이때 조준은 제자리를 본다 (용사 머리 위를 지날 때 던지는 방향이 뒤집히지 않게).
    public bool is_returning
    {
        get { return state != null && state.aims_at_home; }
    }

    public Vector2 aim_position
    {
        get { return is_returning ? base_position : (Vector2)transform.position; }
    }

    internal bool can_act
    {
        get { return !Game_flow.is_over && hp > 0; }
    }

    internal Vector2 position
    {
        get { return body.position; }
    }

    void Awake()
    {
        hp = max_hp;
        base_position = transform.position;

        // 움직임은 상태가 옮기므로 물리에 밀리면 안 되고, 작물이 닿는 건 트리거로만 감지한다.
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        circle_collider = GetComponent<CircleCollider2D>();
        circle_collider.isTrigger = true;

        // 그림은 이 오브젝트나 자식의 SpriteRenderer. 루트는 위치·판정, 자식은 그림·애니메이션.
        sprite_renderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        if (sprite_renderer == null) {
            sprite_renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        // 그림이 없으면 마지막 대체로 임시 원. 어느 슬롯이 비었는지 경고한다. 크기는 인스펙터 scale 로 조절한다.
        bool placeholder = Sprite_fit.EnsureSprite(sprite_renderer, placeholder_color, name, "SpriteRenderer.sprite");
        base_color = sprite_renderer.color;

        // 조명. 씬에 라이트를 안 놓아도 어둑한 분위기가 깔리고, 몸 주변에 상태 색 불빛이 따라다닌다.
        Scene_lighting.Ensure();
        body_glow = Scene_lighting.Attach(transform, base_color, body_glow_intensity, body_glow_radius);

        // 바닥 불빛에 그림자가 생기게.
        Scene_lighting.AddShadowCaster(gameObject);

        // 몸 크기(판정·앉는 높이)는 콜라이더가 정한다. 그림과 많이 어긋나면 알려만 준다. 콜라이더는 인스펙터에서 사람이 맞춘다.
        if (!placeholder) {
            Vector2 art = Sprite_fit.WorldSize(sprite_renderer);
            float art_size = Mathf.Max(art.x, art.y);
            float body_size = BodyRadius() * 2f;
            if (art_size > 0f && (body_size < art_size * 0.6f || body_size > art_size * 1.6f)) {
                Debug.LogWarning(name + " : 그림 크기(" + art_size.ToString("0.00") + ")와 콜라이더 지름(" + body_size.ToString("0.00")
                    + ")이 다릅니다. CircleCollider2D 반지름을 그림에 맞춰 주세요.");
            }
        }

        GameObject player_object = GameObject.FindWithTag("Player");
        if (player_object != null) {
            player = player_object.transform;
            player_component = player_object.GetComponent<Player>();
        }

        spawner = FindFirstObjectByType<Spawn_crop>();

        attack_timer = NextAttackDelay();

        if (breath_prefab == null) {
            Debug.LogWarning(name + " : breath_prefab 이 비어 있어서 브레스를 못 씁니다. breath 프리팹을 연결해 주세요.");
        }
        else if (breath_floor_prefab == null) {
            Debug.LogWarning(name + " : breath_floor_prefab 이 비어 있어서 바닥 불도 세모로 그립니다. breath_two 프리팹을 연결해 주세요.");
        }
        if (eat_fireball_prefab == null) {
            Debug.LogWarning(name + " : eat_fireball_prefab 이 비어 있어서 큰 화염구를 임시 원으로 그립니다. eat_fireball 프리팹을 연결해 주세요.");
        }
        if (ground_prefab == null) {
            Debug.LogWarning(name + " : ground_prefab 이 비어 있어서 땅 조각을 임시 갈색 타원으로 그립니다. ground 프리팹을 연결해 주세요.");
            ground_placeholder = MakeGroundPlaceholder();
        }

        Sprite_fit.SetInt(animator, "phase", phase);

        if (enter_from_behind) {
            ChangeState(new State_enter(this));
        }
        else {
            ChangeState(new State_idle(this));
        }

        // 등장 상태가 몸을 화면 밖으로 옮긴 뒤에 기준을 잡아야 첫 스텝에서 방향이 튀지 않는다.
        previous_x = body.position.x;
    }

    // ---------- 상태머신 ----------

    internal void ChangeState(Dragon_state next)
    {
        if (state != null) {
            state.Exit();
        }

        state = next;
        TriggerAnimationFor(next);
        state.Enter();

        // 첫 예고(깜빡임)에 점프 안내. 위기 직전에 떠야 기억된다. 한 판에 한 번만.
        if (next is State_charge_telegraph || next is State_slam_telegraph || next is State_breath_telegraph) {
            Tutorial.Fire("tuto_jump");
        }
    }

    // 상태에 맞는 애니메이션 트리거. 예고 넷은 같은 telegraph 를 쓴다. Animator 가 없으면 아무것도 안 한다.
    void TriggerAnimationFor(Dragon_state next)
    {
        string trigger = null;

        if (next is State_charge_telegraph || next is State_breath_telegraph || next is State_wing_telegraph || next is State_slam_telegraph) {
            trigger = "telegraph";
        }
        else if (next is State_charge_dive) {
            trigger = "charge";
        }
        else if (next is State_breath) {
            trigger = "breath";
        }
        else if (next is State_wing) {
            trigger = "wing";
        }
        else if (next is State_slam_drop) {
            trigger = "slam";
        }
        else if (next is State_spit) {
            trigger = "spit";
        }
        else if (next is State_roar) {
            trigger = "roar";
        }

        if (trigger != null) {
            Sprite_fit.Trigger(animator, trigger);
        }
    }

    internal void MoveTo(Vector2 target)
    {
        // 물리 스텝에서 MovePosition 으로 옮겨야 트리거 판정이 제대로 따라온다.
        body.MovePosition(target);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // 등장 중에는 부유 위상을 안 돌린다. 도착한 자리(위)에서 부유가 시작되게.
        if (!(state is State_enter)) {
            hover_phase += dt;
        }

        // 상태가 허락할 때만 먹는다. 기본은 떠서 흔들리는 동안이고, 아래 돌진은 바닥을 훑으며 먹는다. 페이즈 무관.
        if (can_act && state.can_eat) {
            TryEatCrops();
        }

        state.FixedTick(dt);
        UpdateFacing(dt);
    }

    // 그림 방향. 빠르게 옆으로 움직이는 동안(돌진·복귀·등장)은 진행 방향을, 그 외(부유·예고·앉기·낙하)에는 용사 쪽을 본다.
    void UpdateFacing(float dt)
    {
        float x = body.position.x;
        float speed_x = dt > 0f ? (x - previous_x) / dt : 0f;
        previous_x = x;

        float sign;
        if (Mathf.Abs(speed_x) >= face_move_speed) {
            sign = Mathf.Sign(speed_x);
        }
        else if (player != null) {
            sign = player.position.x >= x ? 1f : -1f;
        }
        else {
            sign = facing_sign;
        }

        facing_sign = sign;
        Sprite_fit.Face(sprite_renderer, sign, art_faces_left);
    }

    // Idle 이 간격을 다 채우면 부른다. 페이즈에 맞는 확률로 다음 상태를 고른다.
    //   1페이즈: 돌진 / 내려찍기 (밥먹기는 상시 동작)
    //   2페이즈: 돌진 / 브레스 / 날갯짓
    // 같은 패턴이 2번 연속 나왔으면 다음엔 그 패턴을 빼고 고른다. 세 번 연속은 지루하고, 운에 따라 한쪽만 나올 수 있으니까.
    internal Dragon_state ChooseAttack()
    {
        float roll = Random.value;
        bool breath_ready = breath_prefab != null && player != null;

        string banned = same_pattern_count >= 2 ? last_pattern : null;
        bool switched = false;

        string picked;
        string label;

        if (phase == 1) {
            bool slam = roll < phase1_slam_chance;

            // 1페이즈는 둘뿐이라 금지된 쪽이 뽑히면 강제로 반대쪽.
            if (banned == "내려찍기" && slam) {
                slam = false;
                switched = true;
            }
            else if (banned == "돌진" && !slam) {
                slam = true;
                switched = true;
            }

            picked = slam ? "내려찍기" : "돌진";
            label = picked;
        }
        else if (phase2_opening_pending) {
            // 화난 걸 바로 보여주려고 포효 뒤 첫 패턴은 날갯짓으로 고정한다. 연속 제한보다 우선한다.
            phase2_opening_pending = false;
            picked = "날갯짓";
            label = "날갯짓 (페이즈 2 첫 패턴 고정)";
        }
        else {
            picked = PickPhase2Pattern(roll, breath_ready, null);

            // 금지된 패턴에 걸렸으면 그걸 빼고 나머지끼리의 비율로 다시 고른다.
            if (picked == banned) {
                picked = PickPhase2Pattern(roll, breath_ready, banned);
                switched = true;
            }

            label = picked == "브레스"
                ? "브레스 (준비 " + breath_ready_time.ToString("0.0") + "초, 퍼짐 " + (breath_spread_time / SpeedScale()).ToString("0.0") + "초)"
                : picked;
        }

        if (switched) {
            label += " (연속 제한으로 전환)";
        }

        // 연속 횟수 갱신. 고정된 첫 날갯짓도 날갯짓으로 센다.
        if (picked == last_pattern) {
            same_pattern_count++;
        }
        else {
            last_pattern = picked;
            same_pattern_count = 1;
        }

        // 어떤 패턴이 왜 뽑혔는지 콘솔에서 바로 보이게 한다. 확률을 만질 때 이 줄을 보면 된다.
        Debug.Log("[페이즈 " + phase + "] 패턴 선택: " + label + "  (roll " + roll.ToString("0.00")
            + (breath_ready ? "" : " / 브레스 불가: 프리팹 또는 Player 없음") + ")");

        return MakePatternState(picked);
    }

    // 2페이즈 패턴을 확률로 고른다. banned 는 뽑기에서 빼고, 남은 것들끼리 원래 비율대로 나눈다.
    // 아무것도 안 빼면 (돌진 → 브레스 → 날갯짓) 구간이 예전 코드와 똑같다. 브레스를 못 쓰면 그 몫은 날갯짓이 가져간다.
    string PickPhase2Pattern(float roll, bool breath_ready, string banned)
    {
        float breath_share = breath_ready ? phase2_breath_chance : 0f;

        float charge = banned == "돌진" ? 0f : phase2_charge_chance;
        float breath = banned == "브레스" ? 0f : breath_share;
        float wing = banned == "날갯짓" ? 0f : Mathf.Max(0f, 1f - phase2_charge_chance - breath_share);

        float total = charge + breath + wing;
        if (total <= 0f) {
            return "날갯짓";
        }

        float r = roll * total;
        if (r < charge) {
            return "돌진";
        }
        if (r < charge + breath) {
            return "브레스";
        }
        return "날갯짓";
    }

    Dragon_state MakePatternState(string pattern)
    {
        switch (pattern) {
            case "내려찍기":
                return new State_slam_telegraph(this);
            case "돌진":
                return new State_charge_telegraph(this);
            case "브레스":
                return new State_breath_telegraph(this);
            default:
                return new State_wing_telegraph(this);
        }
    }

    internal int ChargeDamage()
    {
        return phase >= 2 ? charge_damage_p2 : charge_damage_p1;
    }

    internal void EnterPhase2()
    {
        phase2_pending = false;
        phase2_opening_pending = true;
        phase = 2;

        Sprite_fit.SetInt(animator, "phase", phase);

        Debug.Log("페이즈 2 돌입! (HP " + hp + "/" + max_hp + ")");

        // 조명이 붉고 어두워지고, 드래곤 뒤에서 붉은 빛이 비친다.
        Scene_lighting.SetPhase(2);
        Scene_lighting.EnableDragonRim(transform, true);

        ChangeState(new State_roar(this));
    }

    // ---------- 공용 헬퍼 ----------

    // 페이즈 2 배율. 간격·예고처럼 "짧아져야 하는" 값은 이걸로 나누고, 속도처럼 "커져야 하는" 값은 곱한다.
    internal float SpeedScale()
    {
        return phase >= 2 ? Mathf.Max(0.1f, phase2_speed) : 1f;
    }

    internal float NextAttackDelay()
    {
        // 간격은 예고 배율과 별개로 더 세게 줄인다. "빠른 예고" 보다 "자주 온다" 가 2페이즈 체감을 만든다.
        float scale = phase >= 2 ? Mathf.Max(0.1f, phase2_interval_scale) : 1f;
        return Random.Range(attack_interval_min, attack_interval_max) / scale;
    }

    // 용사가 오른쪽에 있으면 +1, 왼쪽이면 -1. 등장·불·쓸기 방향이 전부 이 값을 따른다.
    internal float FacingSign()
    {
        if (player == null) {
            return 1f;
        }

        return player.position.x >= base_position.x ? 1f : -1f;
    }

    // 용사가 서 있는 높이. 점프 중이어도 땅 기준이어야 착지하면 맞고 떠 있으면 피한다.
    internal float GroundY()
    {
        if (player == null) {
            return transform.position.y;
        }

        Player_jump jump = player.GetComponent<Player_jump>();
        return jump != null ? jump.ground_y : player.position.y;
    }

    // 용사 몸 중심에서 발바닥까지. 용사 그림에서 잰 값이라 그림이 바뀌면 따라온다. 없으면 예전 캡슐 기준 -1.
    internal float PlayerFootOffset()
    {
        return player_component != null ? player_component.foot_offset : -1f;
    }

    // 용사가 서 있는 바닥 높이. 불·충격파·앉기·포물선 착지가 전부 이 값을 쓴다.
    internal float FloorY()
    {
        return GroundY() + PlayerFootOffset();
    }

    // 작물이 흐르는 높이. 스포너가 정하는 값이라 거기서 읽는다.
    internal float CropLaneY()
    {
        if (spawner != null) {
            return spawner.spawn_point != null ? spawner.spawn_point.position.y : spawner.transform.position.y;
        }

        return GroundY();
    }

    // 위(씬에 놓은 자리)에서 출발해 작물 줄까지 내려갔다 올라온다. 아래에 닿을 때 작물을 먹는다.
    internal Vector2 HoverPosition()
    {
        float top = base_position.y;
        float bottom = CropLaneY();

        // 0 → 1 → 0. 위에서 시작하므로 등장 직후와 이어진다.
        float t = 0.5f - 0.5f * Mathf.Cos(hover_phase * hover_dip_speed);

        return new Vector2(
            base_position.x + Mathf.Sin(hover_phase * hover_speed_x) * hover_amplitude_x,
            Mathf.Lerp(top, bottom, t)
        );
    }

    // 몸 크기. 콜라이더 반지름에 scale 이 곱해진 월드 기준 값이라 그림과 맞는다.
    internal float BodyRadius()
    {
        if (circle_collider == null) {
            return 1f;
        }

        return circle_collider.radius * Mathf.Abs(transform.localScale.x);
    }

    // 끝으로 갈수록 느려지는 보간. 도착이 부드럽다.
    internal static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    internal Player_health PlayerHealth()
    {
        return player != null ? player.GetComponent<Player_health>() : null;
    }

    // 몸 색 틴트. 항상 원래 색 기준으로 섞는다. t = 0 이면 정확히 원래 색. 상태들의 GetColor 가 이걸로 색을 만든다.
    internal Color Tint(Color target, float t)
    {
        return Color.Lerp(base_color, target, Mathf.Clamp01(t));
    }

    // ---------- 공격 실행 ----------

    // 아래서 뱉기. 몸통 높이로 수평으로 쏜다. 커서 서 있어도, 탭 점프로도 맞는다. 길게 뛰거나 작물로 부숴야 한다.
    internal void FireBigStraight()
    {
        float sign = FacingSign();
        Vector3 spawn = new Vector3(transform.position.x + fireball_offset_x * sign, GroundY() + fireball_lane_y, 0f);

        Fireball fireball = SpawnBigFireball(spawn, bigfire_scale, bigfire_hits_to_break);
        fireball.Launch(new Vector2(sign, 0f));

        Debug.Log("큰 화염구 발사! 작물 " + bigfire_hits_to_break + "번 맞히면 부서짐");
    }

    // 위에서 뱉기. 입에서 출발해 bigfire_arc_time 뒤에 target(용사 발 위치)에 떨어지는 포물선.
    // 초속은 v0 = (target - origin - 0.5 * g * t^2) / t. 작아서 롱점프로 넘고, 바닥에 닿으면 터진다.
    internal void FireBigArc(Vector2 target)
    {
        float sign = FacingSign();
        Vector2 origin = new Vector2(transform.position.x + fireball_offset_x * sign, transform.position.y);

        float t = Mathf.Max(0.1f, bigfire_arc_time);
        Vector2 gravity = new Vector2(0f, -bigfire_arc_gravity);
        Vector2 initial_velocity = (target - origin - 0.5f * gravity * t * t) / t;

        Fireball fireball = SpawnBigFireball(origin, bigfire_arc_scale, bigfire_arc_hits_to_break);
        fireball.LaunchArc(initial_velocity, bigfire_arc_gravity, target.y);

        Debug.Log("큰 화염구 발사! (포물선, 착지 x " + target.x.ToString("0.0") + ") 작물 " + bigfire_arc_hits_to_break + "번 맞히면 부서짐");
    }

    // 큰 화염구를 만들어 지름(유닛)에 맞게 키운다. 발사는 부른 쪽이 Launch / LaunchArc 로 한다. 격파에 필요한 횟수도 부른 쪽이 정한다.
    Fireball SpawnBigFireball(Vector3 spawn, float diameter, int hits_to_break)
    {
        Fireball fireball;
        if (eat_fireball_prefab != null) {
            fireball = Instantiate(eat_fireball_prefab, spawn, Quaternion.identity);
        }
        else {
            // 프리팹을 안 연결했어도 패턴이 비지 않게 임시 보라 원으로 만든다(마지막 대체. Awake 에서 이미 경고했다).
            GameObject holder = new GameObject("Big_fireball (임시)");
            holder.transform.position = spawn;

            SpriteRenderer renderer = holder.AddComponent<SpriteRenderer>();
            renderer.sprite = Placeholder_sprite.Circle();
            Scene_lighting.ApplyLitMaterial(renderer);
            renderer.color = eat_color;
            renderer.sortingOrder = 60;

            fireball = holder.AddComponent<Fireball>();
        }

        fireball.MakeBig(diameter, bigfire_damage, hits_to_break, bigfire_speed_scale);
        return fireball;
    }

    // 내려찍기 착지 지점(바닥 y)에서 땅을 분출시킨다. State_slam_impact 가 부른다.
    // 용사를 지나 eruption_overshoot 만큼 더 간다. 단 화면 끝 안쪽 1 유닛까지만.
    internal void SpawnGroundEruption(Vector2 floor_point)
    {
        float sign = FacingSign();

        float target_x = player != null ? player.position.x : floor_point.x + sign * breath_impact_distance;
        float end_x = target_x + sign * eruption_overshoot;
        end_x = Mathf.Clamp(end_x, World_scroll.LeftX() + 1f, World_scroll.RightX() - 1f);

        Ground_eruption_config config;
        config.segment_spacing = segment_spacing;
        config.segment_interval = segment_interval;
        config.rise_time = segment_rise_time;
        config.hold_time = segment_hold_time;
        config.sink_time = segment_sink_time;
        config.height = wave_height;
        config.damage = wave_damage;
        config.art_faces_left = ground_art_faces_left;

        GameObject prefab = ground_prefab != null ? ground_prefab : ground_placeholder;
        Ground_eruption.Spawn(prefab, floor_point, sign, end_x, config);

        // 착지점에서 불빛이 크게 번쩍하고 가라앉는다.
        Scene_lighting.Flash(floor_point, eruption_glow_color, eruption_glow_intensity, eruption_glow_radius, eruption_glow_time);
    }

    // 프리팹을 안 연결했어도 패턴이 비지 않게 임시 갈색 원을 만들어 둔다(마지막 대체. Awake 에서 이미 경고했다). 늘리면 타원이 된다.
    // 씬에 보이면 안 되니 꺼 두고, Ground_eruption 이 조각마다 복제한 뒤 켠다. sortingOrder 는 조각을 만들 때 다시 정한다.
    GameObject MakeGroundPlaceholder()
    {
        GameObject holder = new GameObject("Ground_segment (임시 원본)");

        SpriteRenderer renderer = holder.AddComponent<SpriteRenderer>();
        renderer.sprite = Placeholder_sprite.Circle();
        renderer.color = slam_color;
        Scene_lighting.ApplyLitMaterial(renderer);

        holder.SetActive(false);
        return holder;
    }

    // 몸에 닿은 흘러오는 작물을 삼킨다. 흐르는 작물끼리는 물리 접촉이 안 생기므로 거리로 본다.
    void TryEatCrops()
    {
        float radius = BodyRadius();
        Crop_flow[] crops = FindObjectsByType<Crop_flow>(FindObjectsSortMode.None);

        foreach (Crop_flow crop in crops) {
            if (!crop.is_flowing) {
                continue;
            }

            // 성검은 먹지 않는다. 용사가 주워서 던질 이벤트 아이템이라 드래곤 앞을 지나가도 그대로 흘려보낸다.
            if (crop.GetComponent<Holy_sword>() != null) {
                continue;
            }

            if (Vector2.Distance(crop.transform.position, body.position) > radius) {
                continue;
            }

            Destroy(crop.gameObject);
            eat_stack++;

            Sprite_fit.Trigger(animator, "eat");

            Debug.Log("냠 (" + eat_stack + "/" + eat_count + ")");

            if (eat_stack >= eat_count && !bigfire_pending) {
                bigfire_pending = true;
                Debug.Log("배부름 - 제자리로 오면 큰 화염구 뱉기");
            }
        }
    }

    // ---------- 피격 ----------

    void OnTriggerEnter2D(Collider2D other)
    {
        Crop_flow flow = other.GetComponentInParent<Crop_flow>();

        // 흘러가는 중이거나 손에 든 작물은 무시한다. 던져진 것만 맞는다.
        if (flow == null || !flow.is_thrown) {
            return;
        }

        Crop_data data = other.GetComponentInParent<Crop_data>();
        bool holy = IsHolySword(data);

        // 무적일 때는 작물을 없애지도 않고 그냥 지나가게 둔다. 성검만은 뚫는다.
        // 단 등장·포효는 성검도 못 뚫는다. 자리 잡기 전이나 판이 바뀌는 순간에 끊기는 건 싱겁다.
        if (is_invincible && (!holy || state is State_enter || state is State_roar)) {
            return;
        }

        string label = data != null ? data.display_name : flow.name;

        Vector2 hit_point = other.transform.position;

        if (data != null && data.heal_dragon > 0) {
            // 함정 아이템. 데미지 대신 회복.
            Heal(data.heal_dragon, label);
            Popup_text.ShowDamage(hit_point, data.heal_dragon, true);
        }
        else if (data != null && data.RollExplode()) {
            // 폭발. 연출과 반경 피해는 Crop_flow 가 하고, 직접 맞은 나는 반경과 무관하게 맞는다. 팝업도 그쪽에서 띄운다.
            flow.Explode(this);
        }
        else {
            int damage = data != null ? data.RollDamage() : default_damage;

            // 그로기 중이면 배가 된 값이 돌아온다. 팝업도 실제로 깎인 만큼.
            int dealt = TakeDamage(damage, label);
            Popup_text.ShowDamage(hit_point, dealt > 0 ? dealt : damage);
        }

        if (data != null) {
            Audio_util.PlayAt(data.hit_sound, other.transform.position);
        }

        Destroy(flow.gameObject);

        // 맞은 손맛. 아주 짧게 멈칫한다. 게임이 끝났으면 HitStop 이 스스로 무시한다.
        Camera_director.HitStop(hit_hitstop_time);

        // 성검은 데미지와 별개로 하던 패턴을 끊는다. 이 한 방에 죽었으면 그로기는 없다.
        if (holy && can_act) {
            EnterStagger(label);
        }
    }

    // 던진 작물이 성검인가. Holy_sword.Setup 이 스폰 때 dialogue_id 를 채운다.
    internal bool IsHolySword(Crop_data data)
    {
        return data != null && data.dialogue_id == Holy_sword.dialogue_id;
    }

    // 상쇄. 지금 상태가 뭐든 그 자리에서 끊고 그로기로. 이전 상태의 Exit 가 불·바람·뱉기 이펙트를 치운다.
    // 화면에 남은 큰 화염구와 진행 중인 충격파는 여기서 치운다. bigfire_pending / phase2_pending 은 그대로 두고 Idle 이 처리한다.
    internal void EnterStagger(string source)
    {
        if (!can_act) {
            return;
        }

        foreach (Fireball fireball in FindObjectsByType<Fireball>(FindObjectsSortMode.None)) {
            fireball.ForceBreak();
        }

        foreach (Ground_eruption eruption in FindObjectsByType<Ground_eruption>(FindObjectsSortMode.None)) {
            eruption.SinkNow();
        }

        ChangeState(new State_stagger(this, source));
    }

    // 몸을 잠깐 하얗게. 상쇄 진입 연출.
    internal void FlashWhite(float seconds)
    {
        stagger_flash_timer = seconds;
    }

    // 생명포션 같은 함정 아이템에 맞았을 때. max_hp 를 넘지 않는다. 페이즈 2 예약은 되돌리지 않는다.
    public void Heal(int amount, string source)
    {
        if (hp <= 0 || Game_flow.is_over) {
            return;
        }

        int before = hp;
        hp = Mathf.Min(max_hp, hp + amount);
        heal_timer = hurt_flash_time;

        Debug.Log("회복! " + source + " +" + (hp - before) + " (드래곤 HP " + hp + "/" + max_hp + ")");
    }

    // 실제로 깎은 양을 돌려준다. 그로기 중이면 배가 된 값. 이미 죽었거나 게임이 끝났으면 0.
    public int TakeDamage(int damage, string source)
    {
        if (hp <= 0 || Game_flow.is_over) {
            return 0;
        }

        // 그로기 중에는 무방비. 받는 데미지가 배가 된다.
        if (state is State_stagger) {
            damage = Mathf.RoundToInt(damage * stagger_damage_multiplier);
        }

        hp -= damage;
        hurt_timer = hurt_flash_time;

        Sprite_fit.Trigger(animator, "hurt");

        // 맞은 순간 몸 불빛이 번쩍.
        if (body_glow != null) {
            body_glow.Pulse(hurt_glow_intensity, hurt_glow_time);
        }

        Debug.Log("명중! " + source + " -" + damage + " (드래곤 HP " + Mathf.Max(hp, 0) + "/" + max_hp + ")");

        if (hp <= 0) {
            hp = 0;

            // 격추 연출. 흰 플래시 + 줌 + 긴 흔들림 뒤에 종료 배너가 뜬다.
            Camera_director.Shake(end_shake_amplitude, end_shake_time);
            Camera_director.ZoomPunch(end_zoom_amount, end_zoom_time);
            Camera_director.Flash(end_flash_color, end_flash_time);

            Game_flow.End("드래곤 격추");
            return damage;
        }

        // 하던 동작이 끝나고 제자리로 오면 전환한다. 그로기 중이면 그로기 시간을 다 채운 뒤에.
        if (phase == 1 && !phase2_pending && hp <= max_hp * phase2_hp_ratio) {
            phase2_pending = true;
        }

        return damage;
    }

    // ---------- 표시 ----------

    void Update()
    {
        hurt_timer -= Time.deltaTime;
        heal_timer -= Time.deltaTime;
        stagger_flash_timer -= Time.deltaTime;
        UpdateColor();
    }

    void UpdateColor()
    {
        // 예고 중에는 깜빡여서 곧 온다는 걸 읽게 한다. 어떤 색인지는 상태가 정한다. 전부 원래 색(base_color) 기준 틴트다.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 30f);
        Color color = stagger_flash_timer > 0f ? Tint(Color.white, 1f)
            : heal_timer > 0f ? Tint(heal_color, 1f)
            : hurt_timer > 0f ? Tint(hurt_color, 1f)
            : state.GetColor(pulse);

        // 페이즈 2 부터는 항상 붉은 기가 돈다.
        if (phase >= 2) {
            color *= phase2_tint;
        }

        // 무적이면 반투명하게 해서 지금은 못 맞힌다는 걸 보여준다.
        if (is_invincible) {
            color.a *= invincible_alpha;
        }

        sprite_renderer.color = color;
        UpdateGlow(color);
    }

    // 몸 불빛은 지금 몸 색을 따라간다. 원래 색에서 멀어질수록(예고가 진해질수록) 밝아져서 색 깜빡임이 빛으로도 읽힌다.
    void UpdateGlow(Color body_color)
    {
        if (body_glow == null) {
            return;
        }

        float difference = Mathf.Max(
            Mathf.Abs(body_color.r - base_color.r),
            Mathf.Abs(body_color.g - base_color.g),
            Mathf.Abs(body_color.b - base_color.b));

        Color glow_color = body_color;
        glow_color.a = 1f;

        body_glow.Set(glow_color, body_glow_intensity + body_glow_tint_boost * Mathf.Clamp01(difference));
    }

    void OnDrawGizmos()
    {
        if (state != null) {
            state.DrawGizmos();
        }
    }
}
