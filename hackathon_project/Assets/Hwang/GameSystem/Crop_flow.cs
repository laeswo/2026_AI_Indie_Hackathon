using UnityEngine;

// 오른쪽에서 흘러와 용사 앞을 지나가는 아이템(작물).
// 주워지는 순간 흐름을 멈추고 다시 평범한 물리 오브젝트가 된다.
// 프리팹에 Crop_data 가 붙어 있으면 중력 배율·유도·폭발을 여기서 적용한다. 없으면 예전 작물과 똑같이 동작한다.
[RequireComponent(typeof(Rigidbody2D))]
public class Crop_flow : MonoBehaviour
{

    // 눈에 잘 띄게. 조명에 어두워지지 않고(Unlit), 그림 크기를 이만큼 키운다. 콜라이더도 같이 커진다.
    const float art_scale = 1.3f;

    float despawn_margin = 3f;   // 화면 왼쪽 끝에서 이만큼 더 나가면 지운다
    float fall_limit = -20f;     // 아래로 떨어져 사라진 것도 정리한다

    // 폭발 연출. 크기·시간은 큰 화염구 격파보다 조금 크게.
    float explode_shake_amplitude = 0.3f;
    float explode_shake_time = 0.3f;
    int explode_fragment_count = 10;
    float explode_fragment_speed = 7f;
    float explode_fragment_life = 0.5f;
    float explode_fragment_size = 0.25f;

    public bool is_flowing { get; private set; }

    // 던져진 뒤에만 참. 드래곤은 이 값이 참인 작물에만 맞는다.
    public bool is_thrown { get; private set; }

    // 아이템 수치. 없을 수 있다.
    public Crop_data data { get; private set; }

    Rigidbody2D body;
    Collider2D[] colliders;
    SpriteRenderer sprite_renderer;     // face_velocity 회전에 쓴다

    // 주워진 뒤 적용할 중력 배율. Crop_data.gravity_scale 이 단일 진실이고, 없을 때만 프리팹 Rigidbody2D 값을 쓴다.
    float thrown_gravity_scale;

    bool exploded;

    Dragon dragon;
    bool dragon_searched;

    // Awake 뒤에 그림·크기를 정하는 것들(Item_variants, Holy_sword.Setup)이 끝난 다음 프레임에 손본다.
    bool looks_applied;

    void Start()
    {
        ApplyLooks();
    }

    void ApplyLooks()
    {
        if (looks_applied) {
            return;
        }
        looks_applied = true;

        // 주울 수 있는 건 조명과 상관없이 선명하게.
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>()) {
            Scene_lighting.ApplyUnlitMaterial(renderer);
        }

        transform.localScale *= art_scale;
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        colliders = GetComponentsInChildren<Collider2D>();
        data = GetComponentInChildren<Crop_data>();
        sprite_renderer = GetComponentInChildren<SpriteRenderer>();

        // 흐르는 동안 중력을 끄므로, 주워질 때 넣을 값을 여기서 정해 둔다.
        // 예측선(Throw_trajectory)이 body.gravityScale 을 읽으므로 이 값 하나만 맞으면 궤적도 맞는다.
        thrown_gravity_scale = data != null ? data.gravity_scale : body.gravityScale;

        IgnoreOtherCrops();
        StartFlowing();
        WarnIfArtMismatch();
    }

    // 그림과 콜라이더 크기가 크게 어긋나면 알려만 준다. 자동으로 맞추지는 않는다. 프리팹은 사람이 고친다.
    // 원·네모 콜라이더만 본다. 다각형은 모양이 제각각이라 건너뛴다.
    void WarnIfArtMismatch()
    {
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null || colliders == null) {
            return;
        }

        Vector2 art = Sprite_fit.WorldSize(renderer);
        float art_size = Mathf.Max(art.x, art.y);
        if (art_size <= 0f) {
            return;
        }

        foreach (Collider2D collider in colliders) {
            float hit_size = 0f;
            Vector3 scale = collider.transform.lossyScale;

            CircleCollider2D circle = collider as CircleCollider2D;
            BoxCollider2D box = collider as BoxCollider2D;
            if (circle != null) {
                hit_size = circle.radius * 2f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }
            else if (box != null) {
                hit_size = Mathf.Max(box.size.x * Mathf.Abs(scale.x), box.size.y * Mathf.Abs(scale.y));
            }

            if (hit_size <= 0f) {
                continue;
            }

            float ratio = hit_size / art_size;
            if (ratio < 0.75f || ratio > 1.25f) {
                Debug.LogWarning(name + " : 그림 크기(" + art_size.ToString("0.00") + ")와 콜라이더 크기(" + hit_size.ToString("0.00")
                    + ")가 다릅니다. 프리팹의 콜라이더를 그림에 맞춰 주세요.");
            }
        }
    }

    // 작물끼리는 부딪히지 않는다. 던진 게 뒤따라오는 작물을 튕겨내면 흐름이 엉킨다.
    // 살아 있는 작물이 몇 개 안 되므로 생성 시점에 전부 훑어도 부담이 없다.
    void IgnoreOtherCrops()
    {
        Crop_flow[] others = FindObjectsByType<Crop_flow>(FindObjectsSortMode.None);

        foreach (Crop_flow other in others) {
            if (other == this || other.colliders == null) {
                continue;
            }

            foreach (Collider2D mine in colliders) {
                foreach (Collider2D theirs in other.colliders) {
                    Physics2D.IgnoreCollision(mine, theirs, true);
                }
            }
        }
    }

    void StartFlowing()
    {
        is_flowing = true;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        // 흘러올 때 기본 자세. 창처럼 눕혀 두고 싶은 건 Crop_data.rest_angle 로.
        if (data != null) {
            body.rotation = data.rest_angle;
        }
    }

    // 용사가 주웠을 때 호출한다. 이후 위치는 Player 가 직접 옮긴다.
    // 여기서 넣는 중력 배율이 던진 뒤 궤적을 정한다.
    public void StopFlowing()
    {
        is_flowing = false;
        body.gravityScale = thrown_gravity_scale;
    }

    // Player 가 손에서 놓는 순간 호출한다.
    public void MarkThrown()
    {
        is_thrown = true;
    }

    // 포효 바람에 날려 보낸다. 흘러오던 것만. 주울 수도 맞힐 수도 없게 되고 잠시 뒤 사라진다.
    public void BlowAway()
    {
        if (!is_flowing) {
            return;
        }

        is_flowing = false;

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0.15f;
        body.linearVelocity = new Vector2(Random.Range(-15f, -9f), Random.Range(4f, 8f));
        body.angularVelocity = Random.Range(-540f, 540f);

        Destroy(gameObject, 1.5f);
    }

    // 화면의 흘러오는 작물을 전부 날려 보낸다. 페이즈 2 포효가 부른다.
    public static void BlowAwayAll()
    {
        foreach (Crop_flow crop in FindObjectsByType<Crop_flow>(FindObjectsSortMode.None)) {
            crop.BlowAway();
        }
    }

    void FixedUpdate()
    {
        if (is_flowing) {
            // 배경과 같은 속도를 매 스텝 다시 읽는다. 중간에 속도가 바뀌어도 같이 따라간다.
            body.linearVelocity = new Vector2(-World_scroll.current_speed, 0f);
            return;
        }

        if (is_thrown && data != null && data.homing_turn_rate > 0f) {
            Home(Time.fixedDeltaTime);
        }

        // 창처럼 앞뒤가 있는 것은 날아가는 방향을 본다. 유도로 방향이 바뀌어도 따라간다.
        // Dynamic 바디라 transform 을 돌리면 물리가 되돌리므로 body.rotation 으로 돌린다.
        if (is_thrown && data != null && data.face_velocity && body.linearVelocity.sqrMagnitude > 0.0001f) {
            body.angularVelocity = 0f;
            body.rotation = Sprite_fit.AngleToward(sprite_renderer, body.linearVelocity, data.art_faces_left, data.art_angle);
        }
    }

    Dragon FindDragon()
    {
        if (!dragon_searched) {
            dragon = FindFirstObjectByType<Dragon>();
            dragon_searched = true;
        }

        return dragon;
    }

    // 유도. 속도 방향을 드래곤 쪽으로 turn_rate 만큼 돌린다. 속력은 그대로.
    // 중력은 계속 받지만 매 스텝 방향을 다시 잡으므로 turn_rate 가 크면 유도가 이긴다.
    void Home(float dt)
    {
        if (Game_flow.is_over) {
            return;
        }

        Dragon target = FindDragon();
        if (target == null) {
            return;
        }

        Vector2 velocity = body.linearVelocity;
        float speed = velocity.magnitude;
        if (speed < 0.01f) {
            return;
        }

        Vector2 to_target = (Vector2)target.transform.position - body.position;
        if (to_target.sqrMagnitude < 0.0001f) {
            return;
        }

        float max_radians = data.homing_turn_rate * Mathf.Deg2Rad * dt;
        Vector2 direction = Vector3.RotateTowards(velocity / speed, to_target.normalized, max_radians, 0f);
        body.linearVelocity = direction.normalized * speed;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 던진 작물은 땅이든 뭐든 처음 닿는 순간 사라진다. 바닥에 남아 굴러다니지 않게.
        // 드래곤은 트리거라 여기로 안 들어오고, Dragon 쪽에서 직접 지운다.
        if (!is_thrown) {
            return;
        }

        // 폭발 아이템은 땅에 닿아도 터질 수 있다. 반경 안에 드래곤이 있으면 맞는다.
        if (data != null && data.RollExplode()) {
            Explode(null);
        }

        Destroy(gameObject);
    }

    // 폭발. 연출 + 반경 안의 드래곤과 큰 화염구에 피해.
    // direct_hit 은 이 아이템이 직접 닿은 드래곤. 반경과 무관하게 맞는다. 없으면 null.
    // 없애는 건 부른 쪽이 한다. 여러 경로에서 불려도 한 번만 터진다.
    public void Explode(Dragon direct_hit)
    {
        if (exploded || data == null) {
            return;
        }
        exploded = true;

        Vector2 center = body.position;

        Debug.Log(data.display_name + " 폭발! (" + data.explode_damage + ", 반경 " + data.explode_radius + ")");

        Camera_director.Shake(explode_shake_amplitude, explode_shake_time);
        Scene_lighting.Flash(center, new Color(1f, 0.65f, 0.25f), 2.5f, data.explode_radius + 2f, 0.5f);
        SpawnFragments(center);
        Popup_text.ShowText(center, "-" + data.explode_damage + " 폭발!", new Color(1f, 0.5f, 0.2f));

        // 드래곤. 직접 맞았으면 무조건, 아니면 반경 안일 때. 무적이면 안 맞는다.
        Dragon target = direct_hit != null ? direct_hit : FindDragon();
        if (target != null && !target.is_invincible) {
            bool in_range = direct_hit != null
                || Vector2.Distance(center, target.transform.position) <= data.explode_radius + target.BodyRadius();

            if (in_range) {
                target.TakeDamage(data.explode_damage, data.display_name + " 폭발");
            }
        }

        // 큰 화염구. 반경 안이면 즉시 격파.
        foreach (Fireball fireball in FindObjectsByType<Fireball>(FindObjectsSortMode.None)) {
            if (!fireball.is_big) {
                continue;
            }

            float reach = data.explode_radius + fireball.HitRadius();
            if (Vector2.Distance(center, fireball.transform.position) <= reach) {
                fireball.ForceBreak();
            }
        }
    }

    // 자기 그림을 잘게 복제해 사방으로 튀긴다. 큰 화염구 파편과 같은 컴포넌트를 쓴다.
    void SpawnFragments(Vector2 center)
    {
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) {
            return;
        }

        for (int i = 0; i < explode_fragment_count; i++) {
            float angle = (360f / explode_fragment_count) * i + Random.Range(-15f, 15f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad))
                * explode_fragment_speed * Random.Range(0.6f, 1.2f);

            Fireball_fragment.Spawn(center, renderer.sprite, renderer.color, renderer.sortingOrder,
                explode_fragment_size, velocity, explode_fragment_life);
        }
    }

    void Update()
    {
        if (transform.position.y < fall_limit) {
            Destroy(gameObject);
            return;
        }

        if (transform.position.x < World_scroll.LeftX() - despawn_margin) {
            Destroy(gameObject);
            return;
        }

        // 던진 것이 오른쪽·위로 화면을 벗어나면 정리한다. 중력 0 인 창은 떨어지지 않아 여기서만 사라진다.
        // 흘러오는 것은 오른쪽 밖에서 태어나므로 던진 것만 본다.
        if (is_thrown && (transform.position.x > World_scroll.RightX() + despawn_margin
            || transform.position.y > World_scroll.TopY() + despawn_margin)) {
            Destroy(gameObject);
        }
    }
}
