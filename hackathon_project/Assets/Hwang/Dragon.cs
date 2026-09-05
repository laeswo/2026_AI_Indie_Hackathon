using UnityEngine;

// 화면 오른쪽에 떠 있는 드래곤.
// 던져진 작물에 맞으면 HP 가 깎이고, 일정 간격으로 예고 후 화염구를 쏜다.
[RequireComponent(typeof(Rigidbody2D))]
public class Dragon : MonoBehaviour
{

    public int max_hp = 130;

    // 부유. 시작 위치를 중심으로 위아래/좌우로 흔들린다.
    public float hover_amplitude_y = 1.2f;
    public float hover_amplitude_x = 0.6f;
    public float hover_speed_y = 0.9f;
    public float hover_speed_x = 0.5f;

    // 공격. 예고(telegraph) 동안 색이 바뀌고, 끝나면 화염구가 나간다.
    public float attack_interval_min = 2.0f;
    public float attack_interval_max = 2.9f;
    public float telegraph_time = 0.55f;

    // 화염구. 프리팹을 비워두면 임시 원으로 코드에서 만든다.
    public Fireball fireball_prefab;
    public float fireball_speed = 6.4f;
    public int fireball_damage = 1;
    public float fireball_offset_x = -0.8f;

    // 피격. Crop_data 가 없는 작물은 default_damage 를 준다.
    public int default_damage = 10;
    public float hit_radius = 1f;
    public float hurt_flash_time = 0.25f;

    public Color normal_color = new Color(0.45f, 0.68f, 0.35f);
    public Color hurt_color = new Color(1f, 0.61f, 0.55f);
    public Color telegraph_color = new Color(1f, 0.82f, 0.29f);

    public int hp { get; private set; }

    Rigidbody2D body;
    SpriteRenderer sprite_renderer;
    Transform player;

    Vector2 base_position;
    float phase;
    float attack_timer;
    float telegraph_timer;
    float hurt_timer;

    // 씬에 드래곤을 안 놓아도 게임이 돌아가게, 씬이 뜬 직후 없으면 하나 만든다.
    // 위치는 화면 가로 77% 지점(목업 기준), 용사보다 4.5 유닛 위. 씬에 직접 놓으면 그쪽을 쓴다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void SpawnIfMissing()
    {
        if (FindFirstObjectByType<Dragon>() != null) {
            return;
        }

        // 용사가 없는 씬(메뉴 등)에는 만들지 않는다.
        GameObject player_object = GameObject.FindWithTag("Player");
        if (player_object == null) {
            return;
        }

        float x = World_scroll.LeftX() + World_scroll.ViewHalfWidth() * 2f * 0.77f;
        float y = player_object.transform.position.y + 4.5f;

        GameObject holder = new GameObject("Dragon");
        holder.transform.position = new Vector3(x, y, 0f);
        holder.AddComponent<Dragon>();
    }

    void Awake()
    {
        hp = max_hp;
        base_position = transform.position;

        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        // 임시 스프라이트가 scale 을 바꿀 수 있으므로 콜라이더보다 먼저 씌운다.
        sprite_renderer = Placeholder_sprite.Ensure(gameObject, normal_color, hit_radius * 2f, 10);

        // 작물이 닿는 걸 알아채려면 트리거 콜라이더가 있어야 한다. 없으면 만들어 준다.
        // 콜라이더 반지름은 scale 에 곱해지므로 나눠서 넣어야 월드 기준 hit_radius 가 된다.
        if (GetComponent<Collider2D>() == null) {
            float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.localScale.x));

            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = hit_radius / scale;
        }

        GameObject player_object = GameObject.FindWithTag("Player");
        if (player_object != null) {
            player = player_object.transform;
        }

        attack_timer = Random.Range(attack_interval_min, attack_interval_max);
    }

    void FixedUpdate()
    {
        // 물리 스텝에서 MovePosition 으로 옮겨야 트리거 판정이 제대로 따라온다.
        phase += Time.fixedDeltaTime;

        Vector2 offset = new Vector2(
            Mathf.Sin(phase * hover_speed_x) * hover_amplitude_x,
            Mathf.Sin(phase * hover_speed_y) * hover_amplitude_y
        );

        body.MovePosition(base_position + offset);
    }

    void Update()
    {
        hurt_timer -= Time.deltaTime;

        if (!Game_flow.is_over && hp > 0) {
            UpdateAttack();
        }

        UpdateColor();
    }

    void UpdateAttack()
    {
        if (telegraph_timer > 0f) {
            telegraph_timer -= Time.deltaTime;
            if (telegraph_timer <= 0f) {
                Fire();
            }
            return;
        }

        attack_timer -= Time.deltaTime;
        if (attack_timer <= 0f) {
            attack_timer = Random.Range(attack_interval_min, attack_interval_max);
            telegraph_timer = telegraph_time;
        }
    }

    void Fire()
    {
        // 용사가 서 있는 높이로 쏜다. 점프 중이어도 땅 높이로 쏴야 착지하면 맞고 떠 있으면 피한다.
        // Player_jump 는 Player 가 Awake 에서 붙이므로 여기서 매번 찾는다. 2~3초에 한 번이라 부담 없다.
        float y = transform.position.y;
        if (player != null) {
            Player_jump jump = player.GetComponent<Player_jump>();
            y = jump != null ? jump.ground_y : player.position.y;
        }
        Vector3 position = new Vector3(transform.position.x + fireball_offset_x, y, 0f);

        Fireball fireball;
        if (fireball_prefab != null) {
            fireball = Instantiate(fireball_prefab, position, Quaternion.identity);
        }
        else {
            GameObject holder = new GameObject("Fireball");
            holder.transform.position = position;
            fireball = holder.AddComponent<Fireball>();
        }

        fireball.speed = fireball_speed;
        fireball.damage = fireball_damage;
    }

    void UpdateColor()
    {
        if (sprite_renderer == null) {
            return;
        }

        if (hurt_timer > 0f) {
            sprite_renderer.color = hurt_color;
        }
        else if (telegraph_timer > 0f) {
            // 예고 중에는 깜빡여서 곧 쏜다는 걸 읽게 한다.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 30f);
            sprite_renderer.color = Color.Lerp(normal_color, telegraph_color, pulse);
        }
        else {
            sprite_renderer.color = normal_color;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Crop_flow flow = other.GetComponentInParent<Crop_flow>();

        // 흘러가는 중이거나 손에 든 작물은 무시한다. 던져진 것만 맞는다.
        if (flow == null || !flow.is_thrown) {
            return;
        }

        Crop_data data = other.GetComponentInParent<Crop_data>();
        int damage = data != null ? data.damage : default_damage;
        string label = data != null ? data.display_name : flow.name;

        TakeDamage(damage, label);
        Destroy(flow.gameObject);
    }

    public void TakeDamage(int damage, string source)
    {
        if (hp <= 0 || Game_flow.is_over) {
            return;
        }

        hp -= damage;
        hurt_timer = hurt_flash_time;

        Debug.Log("명중! " + source + " -" + damage + " (드래곤 HP " + Mathf.Max(hp, 0) + "/" + max_hp + ")");

        if (hp <= 0) {
            hp = 0;
            Game_flow.End("드래곤 격추");
        }
    }

    void OnGUI()
    {
        float width = 220f;
        float height = 16f;
        float x = Screen.width - width - 24f;
        float y = 20f;

        GUI.color = Color.white;
        GUI.Label(new Rect(x - 70f, y - 3f, 70f, 22f), "드래곤");

        GUI.color = new Color(0.16f, 0.18f, 0.22f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

        GUI.color = new Color(0.5f, 0.77f, 0.48f);
        GUI.DrawTexture(new Rect(x, y, width * hp / max_hp, height), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }
}
