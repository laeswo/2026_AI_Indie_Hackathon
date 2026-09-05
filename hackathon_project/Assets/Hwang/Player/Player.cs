using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{

    [Header("연결")]
    public Transform hand_transform;

    Throw_trajectory throw_trajectory;

    // 누르고 있으면 충전, 떼면 던진다. 점프 키는 Player_jump 에 있다.
    [Header("조작")]
    public Key hold_key = Key.Y;

    // 손 주변 이 반경 안으로 흘러 들어온 작물을 자동으로 줍는다.
    [Header("줍기")]
    public float pickup_radius = 0.9f;

    // 던진 뒤 이만큼은 못 줍는다. 그동안 지나가는 작물은 놓친다. 연사를 막는 장치다.
    public float pickup_cooldown = 1.2f;

    // 던지는 방향. 드래곤이 뒤(왼쪽)에 있으므로 기본은 왼쪽 위.
    // aim_toward_dragon 이 켜져 있으면 x 부호는 드래곤이 있는 쪽으로 알아서 뒤집힌다.
    // 드래곤이 위로 돌아오는 동안은 Dragon 쪽에서 제자리를 돌려주므로 방향이 안 흔들린다.
    Vector2 throw_direction = new Vector2(-1f, 1f);
    bool aim_toward_dragon = true;

    // 큰 화염구가 이 거리 안으로 오면 드래곤 대신 화염구를 조준한다. 부술 기회를 주려고.
    // 0 이하면 거리 제한 없음. 화면 어디에 있든 다가오는 큰 화염구를 조준한다.
    public float fireball_target_range = 0f;

    // 직선 화염구를 조준할 때 앞을 내다보는 시간의 상한(초). 멀리서 조준하면 비행 시간이 길어져
    // 예측점이 용사를 지나쳐 뒤로 넘어가고, 화염구가 다가올수록 조준선이 한 바퀴 돌아 버린다. 그걸 막는다.
    float fireball_lead_time_max = 0.3f;

    // 여기서의 power 는 임펄스가 아니라 reference_mass 인 작물이 날아가는 속도다.
    [Header("던지기")]
    public float min_power = 4f;
    public float max_power = 12f;
    public float charge_speed = 10f;

    // 질량 보정. reference_mass 인 작물이 딱 min_power ~ max_power 속도로 날아가고,
    // 그보다 무거우면 느리게, 가벼우면 빠르게 날아간다.
    float reference_mass = 1f;

    // 0 이면 질량을 완전히 무시하고, 1 이면 임펄스와 같은 정직한 반비례가 된다.
    // 아이템 프리팹은 Mass 1 로 통일하고 무게감은 Crop_data.gravity_scale 로 내므로(ITEM_PREFAB_CHECKLIST.md),
    // reference_mass 도 1 이어야 min_power ~ max_power 가 그대로 던지는 속도가 된다. 옛 작물(질량 2)은 조금 느리게 날아간다.
    float mass_influence = 0.2f;

    Dragon dragon;

    // 그림. 이 오브젝트나 자식의 SpriteRenderer 를 쓴다. 루트는 위치·판정, 자식은 그림·애니메이션.
    internal SpriteRenderer sprite_renderer;
    internal Animator animator;                  // 있으면 jump / throw / hurt / dead 트리거와 grounded 를 넣는다. 없어도 된다
    internal Color base_color = Color.white;     // 원래 색. 피격 깜빡임은 이 색 기준으로만 알파를 바꾸고 정확히 여기로 돌아온다

    // 몸 중심(transform)에서 발바닥까지. 그림의 아래 끝에서 재므로 그림이 바뀌면 자동으로 맞는다.
    // 드래곤·화염구·충격파·브레스가 "바닥 y" 를 구할 때 전부 이 값을 쓴다. 그림이 없으면 예전 캡슐 기준 -1.
    internal float foot_offset = -1f;
    Color placeholder_color = new Color(0.95f, 0.85f, 0.6f);

    GameObject held_object;
    Rigidbody2D held_body;
    bool is_holding;
    bool is_charging;
    float charge_time;
    float current_power;

    // 각도 조준 (창처럼 face_velocity 인 아이템). Y 를 누르고 있는 동안 세기 대신 각도가 위아래로 왕복한다.
    // 세기는 max_power 고정. 낮게 던지면 직선으로 관통, 높게 던지면 포물선.
    const float aim_angle_min = 8f;
    const float aim_angle_max = 65f;
    const float aim_sweep_speed = 70f;      // 도/초
    bool aim_by_angle;
    float aim_angle;
    float aim_time;
    float pickup_cooldown_timer;

    // 줍기 판정 결과를 매 프레임 새로 할당하지 않도록 버퍼를 재사용한다.
    Collider2D[] pickup_buffer = new Collider2D[8];
    ContactFilter2D pickup_filter;

    void Awake()
    {
        pickup_filter = new ContactFilter2D();
        pickup_filter.useTriggers = true;

        // 씬에 예측선 오브젝트를 안 만들어 뒀거나 연결을 잊어도 궤적이 나오게 한다.
        // 색이나 굵기를 손보고 싶으면 씬에 직접 하나 만들어 연결하면 그쪽을 쓴다.
        if (throw_trajectory == null) {
            throw_trajectory = FindFirstObjectByType<Throw_trajectory>();
        }

        if (throw_trajectory == null) {
            GameObject holder = new GameObject("Throw_trajectory");
            throw_trajectory = holder.AddComponent<Throw_trajectory>();
        }

        // 화염구에 맞으려면 체력이, 피하려면 점프가 있어야 한다. 안 붙여뒀으면 여기서 붙인다.
        if (GetComponent<Player_health>() == null) {
            gameObject.AddComponent<Player_health>();
        }

        if (GetComponent<Player_jump>() == null) {
            gameObject.AddComponent<Player_jump>();
        }

        dragon = FindFirstObjectByType<Dragon>();

        MeasureArt();

        // Resources/Player 에 클립이 있으면 플립북으로 돌린다. 크기는 지금 그림 높이를 따르고 발은 foot_offset 에 고정된다.
        // Animator 를 직접 붙였으면 그쪽을 존중하고 안 붙인다.
        if (animator == null && sprite_renderer != null) {
            float current_height = Sprite_fit.WorldSize(sprite_renderer).y;
            Player_animation.Attach(gameObject, sprite_renderer, current_height > 0.1f ? current_height : 2f, foot_offset);
        }

        // 바닥 불빛에 그림자가 생기게.
        Scene_lighting.AddShadowCaster(gameObject);
    }

    // 그림에서 원래 색과 발 위치를 잰다. 그림이 없으면 경고하고 임시 원 + 예전 값(-1)을 쓴다.
    void MeasureArt()
    {
        sprite_renderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();

        if (sprite_renderer == null) {
            Debug.LogWarning(name + " : SpriteRenderer 가 없어서 발 위치를 " + foot_offset + " 로 둡니다. 그림을 넣어 주세요.");
            return;
        }

        Sprite_fit.EnsureSprite(sprite_renderer, placeholder_color, name, "SpriteRenderer.sprite");
        base_color = sprite_renderer.color;

        foot_offset = Sprite_fit.WorldBounds(sprite_renderer).min.y - transform.position.y;

        // 조명. 씬에 라이트를 안 놓아도 어둑한 분위기가 깔린다. 용사는 스스로 빛나지 않는다.
        Scene_lighting.Ensure();
    }

    // 지금 발바닥 높이. 점프 중이면 그만큼 올라간다.
    internal float FootY()
    {
        return transform.position.y + foot_offset;
    }

    // 가까이 날아오는 큰 화염구. 없으면 null.
    Fireball FindBigFireballNearby()
    {
        Vector2 origin = hand_transform != null ? (Vector2)hand_transform.position : (Vector2)transform.position;

        Fireball nearest = null;
        float nearest_distance = fireball_target_range > 0f ? fireball_target_range : float.PositiveInfinity;

        foreach (Fireball fireball in FindObjectsByType<Fireball>(FindObjectsSortMode.None)) {
            if (!fireball.is_big) {
                continue;
            }

            // 이미 지나간 건 조준하지 않는다. 다가오는 것만.
            Vector2 to_fireball = (Vector2)fireball.transform.position - origin;
            if (Vector2.Dot(to_fireball, fireball.velocity) >= 0f) {
                continue;
            }

            float distance = to_fireball.magnitude;
            if (distance < nearest_distance) {
                nearest_distance = distance;
                nearest = fireball;
            }
        }

        return nearest;
    }

    // 던지는 방향. 큰 화염구가 가까우면 그쪽, 아니면 인스펙터 각도에서 좌우만 드래곤 쪽으로 맞춘 방향.
    Vector2 GetThrowDirection()
    {
        Fireball target = FindBigFireballNearby();
        if (target != null) {
            Vector2 origin = hand_transform != null ? (Vector2)hand_transform.position : (Vector2)transform.position;
            Vector2 predicted = target.transform.position;

            // 직선 화염구는 작물이 날아가는 시간만큼 앞을 겨냥한다. 단, 너무 멀리 내다보면 예측점이 용사를 지나치므로 상한을 둔다.
            // 포물선(위에서 뱉은 것)은 중력 때문에 직선 예측이 틀리고, 어차피 용사 발밑으로 떨어지므로 현재 위치를 그대로 겨냥한다.
            if (!target.is_arc) {
                float speed = Mathf.Max(1f, current_power);
                float flight_time = Vector2.Distance(origin, target.transform.position) / speed;
                flight_time = Mathf.Min(flight_time, fireball_lead_time_max);
                predicted += target.velocity * flight_time;
            }

            Vector2 to_target = predicted - origin;
            if (to_target.sqrMagnitude > 0.0001f) {
                return to_target.normalized;
            }
        }

        Vector2 direction = throw_direction;

        // 각도 조준 중이면 그 각도로. 좌우는 아래에서 드래곤 쪽으로 맞춘다.
        if (is_charging && aim_by_angle) {
            float radians = aim_angle * Mathf.Deg2Rad;
            direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        if (aim_toward_dragon && dragon != null) {
            float sign = dragon.aim_position.x >= transform.position.x ? 1f : -1f;
            direction.x = Mathf.Abs(direction.x) * sign;
        }

        return direction.normalized;
    }

    // 손에 든 게 각도로 조준하는 종류인지 (Crop_data.face_velocity — 창).
    bool HeldAimsByAngle()
    {
        if (held_object == null) {
            return false;
        }

        Crop_data data = held_object.GetComponentInChildren<Crop_data>();
        return data != null && data.face_velocity;
    }

    // Update is called once per frame
    void Update()
    {
        // 끝난 뒤에는 줍지도 던지지도 않는다. 들고 있던 건 그대로 손에 남는다.
        if (Game_flow.is_over) {
            return;
        }

        // 들고 있던 게 파괴되면 먼저 손을 비운다.
        if (is_holding && held_object == null) {
            ClearHold();
        }

        pickup_cooldown_timer -= Time.deltaTime;

        // 손이 비어 있고 쿨타임이 끝났으면 흘러오는 작물을 알아서 줍는다. 줍는 데는 입력이 필요 없다.
        if (!is_holding && pickup_cooldown_timer <= 0f) {
            TryPickup();
        }

        if (is_holding && hand_transform != null) {
            held_object.transform.position = hand_transform.position;
            FaceHeldToward(GetThrowDirection());
        }

        if (Keyboard.current == null) {
            return;
        }

        var hold = Keyboard.current[hold_key];

        // 이미 누르고 있는 상태에서 주운 것이 곧바로 던져지지 않도록,
        // 충전은 손에 뭔가 있을 때 새로 누른 순간에만 시작한다.
        if (hold.wasPressedThisFrame && is_holding) {
            is_charging = true;
            charge_time = 0f;
            current_power = min_power;

            // 창 같은 건 각도로 조준한다. 아래에서 출발해 위로 올라간다.
            aim_by_angle = HeldAimsByAngle();
            aim_time = 0f;
            aim_angle = aim_angle_min;
            if (aim_by_angle) {
                current_power = max_power;
            }
        }

        if (is_charging && is_holding) {
            if (aim_by_angle) {
                // 각도가 왕복한다. 세기는 고정.
                aim_time += Time.deltaTime * aim_sweep_speed;
                aim_angle = aim_angle_min + Mathf.PingPong(aim_time, aim_angle_max - aim_angle_min);
                current_power = max_power;

                if (aim_time >= aim_angle_max - aim_angle_min) {
                    Tutorial.Fire("tuto_throw");
                }
            }
            else {
                float before = charge_time;
                charge_time += Time.deltaTime * charge_speed;
                current_power = min_power + Mathf.PingPong(charge_time, max_power - min_power);

                // 게이지가 처음으로 끝까지 찬 프레임. 던지기 전에 "떼서 던지기" 안내가 뜨게.
                float full = max_power - min_power;
                if (before < full && charge_time >= full) {
                    Tutorial.Fire("tuto_throw");
                }
            }

            UpdateTrajectory();
        }

        if (hold.wasReleasedThisFrame && is_charging) {
            ThrowCrop();
        }
    }

    void TryPickup()
    {
        Vector2 origin = hand_transform != null ? (Vector2)hand_transform.position : (Vector2)transform.position;

        int count = Physics2D.OverlapCircle(origin, pickup_radius, pickup_filter, pickup_buffer);

        Crop_flow nearest = null;
        float nearest_distance = float.MaxValue;

        for (int i = 0; i < count; i++) {
            Collider2D hit = pickup_buffer[i];
            if (hit == null) {
                continue;
            }

            Crop_flow flow = hit.GetComponentInParent<Crop_flow>();

            // 흘러오는 중인 것만 주울 수 있다. 던져 놓은 것은 다시 잡히지 않는다.
            if (flow == null || !flow.is_flowing) {
                continue;
            }

            // 제곱 거리로 비교한다. 순서만 필요하므로 제곱근을 구할 이유가 없다.
            float distance = ((Vector2)flow.transform.position - origin).sqrMagnitude;
            if (distance < nearest_distance) {
                nearest_distance = distance;
                nearest = flow;
            }
        }

        if (nearest != null) {
            Grab(nearest);
        }
    }

    void Grab(Crop_flow flow)
    {
        // 흐름을 먼저 멈춰야 Crop_data.gravity_scale 이 몸에 들어간다. 예측선이 그 값을 읽는다.
        flow.StopFlowing();

        Sound_bank.Play("pickup_sound", flow.transform.position);

        held_object = flow.gameObject;
        held_body = flow.GetComponent<Rigidbody2D>();
        is_holding = true;

        if (held_body != null) {
            held_body.linearVelocity = Vector2.zero;
            held_body.angularVelocity = 0f;
            held_body.bodyType = RigidbodyType2D.Kinematic;
        }

        charge_time = 0f;
        current_power = min_power;

        // 주운 물건의 대사를 머리 위에 띄운다. 대사가 없는 물건은 조용히 넘어간다.
        Crop_data data = flow.GetComponent<Crop_data>();
        string line = Dialogue_table.PickupLine(data);
        if (line != null) {
            Popup_text.ShowAbove(transform, line);
        }

        // 첫 물건을 주웠을 때 조준 안내. 한 판에 한 번만 (Tutorial 이 기억한다).
        Tutorial.Fire("tuto_aim");
    }

    // 손에 든 것이 face_velocity 면 던질 방향을 보게 돌린다. 창이 조준선과 같은 쪽을 향한다.
    void FaceHeldToward(Vector2 direction)
    {
        if (held_object == null) {
            return;
        }

        Crop_data data = held_object.GetComponentInChildren<Crop_data>();
        if (data == null || !data.face_velocity) {
            return;
        }

        SpriteRenderer renderer = held_object.GetComponentInChildren<SpriteRenderer>();
        float angle = Sprite_fit.AngleToward(renderer, direction, data.art_faces_left, data.art_angle);

        // 손에 든 건 Kinematic 바디라 body.rotation 으로 돌려야 물리 동기화가 되돌리지 않는다.
        if (held_body != null) {
            held_body.rotation = angle;
        }
        else {
            held_object.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // 던졌을 때 실제로 생기는 속도. 던지기와 예측선이 같은 함수를 쓰므로 둘이 어긋날 일이 없다.
    Vector2 GetThrowVelocity()
    {
        float reference = reference_mass > 0f ? reference_mass : 1f;

        float mass = held_body != null ? held_body.mass : reference;
        if (mass <= 0f) {
            mass = reference;
        }

        // mass_influence 가 1 일 때 (reference / mass) 배가 되어 임펄스와 정확히 같아진다.
        float mass_scale = Mathf.Pow(reference / mass, mass_influence);

        return GetThrowDirection() * (current_power * mass_scale);
    }

    void UpdateTrajectory()
    {
        if (throw_trajectory == null || held_object == null) {
            return;
        }

        Vector2 origin = hand_transform != null
            ? (Vector2)hand_transform.position
            : (Vector2)held_object.transform.position;

        float power_range = max_power - min_power;
        float power_ratio = power_range > 0f ? (current_power - min_power) / power_range : 1f;

        throw_trajectory.Show(origin, GetThrowVelocity(), held_body, power_ratio);
    }

    // 던질 때 회전 속도(도/초). 창처럼 날아가는 방향을 보는 것(face_velocity)과 성검은 안 돈다.
    // 프리팹에 spin 이 있으면 그 값, 없으면(0) 기본 회전. 그래서 새 아이템을 넣어도 알아서 돈다.
    internal float default_throw_spin = 360f;

    float ThrowSpin(Crop_data data)
    {
        if (data != null && data.face_velocity) {
            return 0f;
        }

        if (held_object != null && held_object.GetComponentInChildren<Holy_sword>() != null) {
            return 0f;
        }

        if (data != null && data.spin > 0f) {
            return data.spin;
        }

        return default_throw_spin;
    }

    void ThrowCrop()
    {
        if (held_body != null) {
            held_body.bodyType = RigidbodyType2D.Dynamic;

            // 속도를 직접 넣는다. AddForce 로는 질량이 한 번 더 나뉘어서
            // GetThrowVelocity() 의 보정과 이중으로 걸린다.
            Vector2 velocity = GetThrowVelocity();
            held_body.linearVelocity = velocity;

            Crop_data data = held_body.GetComponentInParent<Crop_data>();

            // 날아가는 쪽으로 구르듯 돈다. 오른쪽이면 시계 방향(음수), 왼쪽이면 반대.
            // 프리팹 Rigidbody2D 의 Freeze Rotation Z 가 켜져 있으면 안 돈다.
            float spin = ThrowSpin(data);
            if (spin > 0f) {
                held_body.angularVelocity = -spin * Mathf.Sign(velocity.x);
            }

            // 던지는 소리. 프리팹에 넣어둔 게 있으면 그걸, 없으면 공용 소리.
            Sound_bank.PlayThrow(data, held_body.position);
        }

        pickup_cooldown_timer = pickup_cooldown;

        Sprite_fit.Trigger(animator, "throw");

        // 던져진 걸로 표시해야 다시 안 잡히고 드래곤이 알아본다.
        // ClearHold() 가 참조를 비우기 전에 처리해야 한다.
        if (held_object != null) {
            Crop_flow flow = held_object.GetComponent<Crop_flow>();
            if (flow != null) {
                flow.MarkThrown();

                // 성검을 던졌다. 대장장이가 보고 있다면 배신당한다.
                if (flow.data != null && flow.data.dialogue_id == Holy_sword.dialogue_id) {
                    Smith_npc.OnHolySwordThrown();
                }
            }
        }

        ClearHold();
    }

    void ClearHold()
    {
        // 손을 비우는 모든 경로가 여기를 지나므로 예측선도 여기서 한 번만 끈다.
        if (throw_trajectory != null) {
            throw_trajectory.Hide();
        }

        held_object = null;
        held_body = null;
        is_holding = false;
        is_charging = false;
        charge_time = 0f;
    }

    void OnDrawGizmosSelected()
    {
        // 줍기 반경을 씬 뷰에서 눈으로 맞출 수 있게 그려둔다.
        Vector3 origin = hand_transform != null ? hand_transform.position : transform.position;

        Gizmos.color = new Color(0.6f, 0.9f, 0.5f, 0.9f);
        Gizmos.DrawWireSphere(origin, pickup_radius);
    }
}
