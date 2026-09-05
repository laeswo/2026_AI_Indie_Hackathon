using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{

    public Transform crop_transform;
    public Transform hand_transform;

    public Throw_trajectory throw_trajectory;

    // 누르고 있으면 충전, 떼면 던진다. 점프 키는 Player_jump 에 있다.
    public Key hold_key = Key.Y;

    // 손 주변 이 반경 안으로 흘러 들어온 작물을 자동으로 줍는다.
    public float pickup_radius = 0.9f;

    public Vector2 throw_direction = new Vector2(1f, 1f);

    // 여기서의 power 는 임펄스가 아니라 reference_mass 인 작물이 날아가는 속도다.
    public float min_power = 4f;
    public float max_power = 12f;
    public float charge_speed = 10f;

    // 질량 보정. reference_mass 인 작물이 딱 min_power ~ max_power 속도로 날아가고,
    // 그보다 무거우면 느리게, 가벼우면 빠르게 날아간다.
    public float reference_mass = 20f;

    // 0 이면 질량을 완전히 무시하고, 1 이면 임펄스와 같은 정직한 반비례가 된다.
    // 프리팹 질량이 1 ~ 40 으로 크게 벌어져 있어서 1 을 쓰면 가벼운 것만 날아가므로 낮게 잡았다.
    [Range(0f, 1f)]
    public float mass_influence = 0.2f;

    GameObject held_object;
    Rigidbody2D held_body;
    bool is_holding;
    bool is_charging;
    float charge_time;
    float current_power;

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

        // 손이 비어 있으면 흘러오는 작물을 알아서 줍는다. 줍는 데는 입력이 필요 없다.
        if (!is_holding) {
            TryPickup();
        }

        if (is_holding && hand_transform != null) {
            held_object.transform.position = hand_transform.position;
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
        }

        if (is_charging && is_holding) {
            charge_time += Time.deltaTime * charge_speed;
            current_power = min_power + Mathf.PingPong(charge_time, max_power - min_power);

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
        // 흐름을 먼저 멈춰야 원래 중력 배율이 되돌아온다. 예측선이 그 값을 읽는다.
        flow.StopFlowing();

        held_object = flow.gameObject;
        held_body = flow.GetComponent<Rigidbody2D>();
        crop_transform = flow.transform;
        is_holding = true;

        if (held_body != null) {
            held_body.linearVelocity = Vector2.zero;
            held_body.angularVelocity = 0f;
            held_body.bodyType = RigidbodyType2D.Kinematic;
        }

        charge_time = 0f;
        current_power = min_power;
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

        return throw_direction.normalized * (current_power * mass_scale);
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

    void ThrowCrop()
    {
        if (held_body != null) {
            held_body.bodyType = RigidbodyType2D.Dynamic;

            // 속도를 직접 넣는다. AddForce 로는 질량이 한 번 더 나뉘어서
            // GetThrowVelocity() 의 보정과 이중으로 걸린다.
            held_body.linearVelocity = GetThrowVelocity();
        }

        // 발사된 오브젝트는 다시 잡히면 안 되므로 태그를 바꾸고, 드래곤이 알아보도록 표시한다.
        // ClearHold() 가 참조를 비우기 전에 처리해야 한다.
        if (held_object != null) {
            held_object.tag = "Crop_trash";

            Crop_flow flow = held_object.GetComponent<Crop_flow>();
            if (flow != null) {
                flow.MarkThrown();
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
        crop_transform = null;
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
