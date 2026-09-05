using System.Collections.Generic;
using UnityEngine;

// 드래곤이 쏘는 화염구. 지금은 작물을 먹고 뱉는 큰 화염구(eat_fireball 프리팹)에만 쓴다.
// 속도·데미지는 여기(프리팹)에서 정하고, Dragon 은 방향과 크기(유닛 지름)만 넣어 준다.
// 용사에게 콜라이더가 없으므로 거리로 판정한다.
//
// 큰 화염구: 드래곤이 작물을 먹고 쏘는 강화판. MakeBig() 으로 키운다.
// 크고 아프지만 던진 작물에 hits_to_break 번 맞으면 부서진다. 그때만 트리거 콜라이더가 붙는다.
//
// 그림: 이 오브젝트나 자식의 SpriteRenderer. 크기는 그림의 bounds 를 재서 원하는 지름에 맞추고,
// 판정 반경은 실제 렌더 지름에서 유도한다. 그림이 바뀌어도 코드를 고칠 필요가 없다.
public class Fireball : MonoBehaviour
{

    [Header("화염구")]
    public float speed = 6.4f;
    public int damage = 1;

    [Header("그림")]
    public bool art_faces_left = false;        // 그림이 왼쪽을 보고 그려졌으면 켠다. 진행 방향에 맞춰 flipX 를 건다
    public bool rotate_to_velocity = false;    // 포물선일 때 속도 방향으로 회전한다 (불꽃 꼬리가 뒤로 가게)
    public GameObject fragment_prefab;         // 격파 파편. 비면 본체 그림 조각을 뿌린다

    float despawn_margin = 2f;

    // 판정 반경 = 렌더 지름의 절반 × 이 비율. 트리거 콜라이더도 같은 값.
    // 1.4 면 임시 원(지름 1) 기준으로 예전 판정(0.7 × 배율)과 같다. 그림에 딱 맞추고 싶으면 0.85 쯤으로 낮춘다.
    internal float hit_radius_ratio = 1.4f;
    float hit_radius = 0.7f;   // 작은 화염구 기본값. MakeBig 이 그림 크기에서 다시 정한다

    // 큰 화염구 그림. Resources/fireball 의 fireball 클립(clips.txt)이 있으면 그걸로 돈다: 불씨에서 커지는 프레임을 한 번 돌고
    // 마지막(꼬리 달린 화염구)으로 난다. 머리가 오른쪽 끝에 있는 그림이라 피벗을 머리 중심에 둔다. 없으면 프리팹 그림 그대로.
    const string art_folder = "fireball";
    const string art_clip = "fireball";
    bool clip_art;

    // 부서질 때 파편 연출. 파편 크기는 본체 지름 기준 비율.
    int fragment_count = 8;
    float fragment_speed = 6f;
    float fragment_life = 0.6f;
    float fragment_size_ratio = 0.3f;
    float hit_flash_time = 0.12f;

    // Dragon 이 Launch() 로 넣어 준다.
    Vector2 direction = Vector2.left;

    // 포물선(LaunchArc). 중력을 받아 속도가 매 프레임 바뀌고, 바닥(arc_floor_y)에 닿으면 터진다.
    // 포물선인지. 용사 조준이 읽는다 (포물선은 앞을 예측하지 않고 현재 위치를 겨냥한다).
    public bool is_arc { get; private set; }
    Vector2 arc_velocity;
    float arc_gravity;
    float arc_floor_y;

    public bool is_big { get; private set; }

    // 지금 판정 반경. 폭발 아이템이 반경 안에 있는지 볼 때 쓴다.
    internal float HitRadius()
    {
        return hit_radius;
    }

    // 용사가 조준할 때 비행 시간만큼 앞을 예측하는 데 쓴다. 포물선이면 지금 이 순간의 속도.
    public Vector2 velocity
    {
        get { return is_arc ? arc_velocity : direction.normalized * speed; }
    }

    int hits_left;
    float flash_timer;
    bool broken;

    // 이미 맞힌 작물. 관통 아이템이 같은 화염구에 두 번 맞지 않게.
    readonly HashSet<Crop_flow> hit_crops = new HashSet<Crop_flow>();

    // 원래 색. 피격 플래시는 이 색과 흰색 사이를 오가고, 끝나면 정확히 이 색으로 돌아온다.
    internal Color base_color = Color.white;
    Color placeholder_color = new Color(0.9f, 0.3f, 1f);

    Transform player;
    Player_health health;
    SpriteRenderer sprite_renderer;
    Animator animator;              // 있으면 hit / break 트리거. 없어도 된다

    // 불빛. 몸에 붙어 따라다니고 일렁인다. 반경은 지름에 비례. 맞으면 번쩍, 터지면 자리에 큰 빛이 남았다 사라진다.
    Glow_light glow;
    internal Color glow_color = new Color(1f, 0.6f, 0.25f);
    internal float glow_intensity = 1.0f;
    internal float glow_radius_ratio = 1.4f;      // 반경 = 렌더 지름 × 이 비율
    internal float glow_min_radius = 1.5f;
    internal float hit_glow_intensity = 2.0f;
    internal float hit_glow_time = 0.15f;
    internal float shatter_glow_intensity = 2.4f;
    internal float shatter_glow_time = 0.4f;

    void Awake()
    {
        sprite_renderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();

        // 그림이 없으면 마지막 대체로 임시 원. 어느 슬롯이 비었는지 경고한다.
        if (sprite_renderer == null) {
            sprite_renderer = gameObject.AddComponent<SpriteRenderer>();
        }
        Sprite_fit.EnsureSprite(sprite_renderer, placeholder_color, name, "SpriteRenderer.sprite");
        base_color = sprite_renderer.color;

        glow = Scene_lighting.Attach(transform, glow_color, glow_intensity, GlowRadius());
        glow.flicker_amount = 0.25f;

        GameObject player_object = GameObject.FindWithTag("Player");
        if (player_object != null) {
            player = player_object.transform;
            health = player_object.GetComponent<Player_health>();
        }

    }

    public void Launch(Vector2 launch_direction)
    {
        direction = launch_direction;

        // 진행 방향을 보게 뒤집는다. 그림 방향은 art_faces_left 가 말해 준다.
        Sprite_fit.Face(sprite_renderer, direction.x, art_faces_left);
    }

    // 포물선 발사. initial_velocity 로 출발해 매 프레임 gravity 만큼 아래로 가속한다.
    // floor_y 아래로 내려가면 파편을 튀기며 사라진다. 그때는 용사에게 데미지를 주지 않는다.
    public void LaunchArc(Vector2 initial_velocity, float gravity, float floor_y)
    {
        is_arc = true;
        arc_velocity = initial_velocity;
        arc_gravity = gravity;
        arc_floor_y = floor_y;

        // 좌우는 초기 속도의 부호로 정하고, 옵션이 켜져 있으면 속도 방향으로 돌린다.
        Sprite_fit.Face(sprite_renderer, initial_velocity.x, art_faces_left);
        if (rotate_to_velocity) {
            Sprite_fit.RotateToward(sprite_renderer.transform, sprite_renderer, arc_velocity, art_faces_left);
        }
    }

    // 지름(유닛)을 맞추고 데미지를 키우고, 작물에 맞을 수 있게 트리거 콜라이더와 Kinematic 바디를 붙인다.
    // 판정 반경은 실제 렌더 지름에서 유도한다.
    public void MakeBig(float diameter, int big_damage, int hits_to_break, float speed_scale)
    {
        is_big = true;

        hits_left = Mathf.Max(1, hits_to_break);

        // 클립 그림이면 공 머리 높이가 지름이고 꼬리는 그 뒤로 뻗는다. 없으면 프리팹 그림을 긴 변 = 지름으로.
        if (!ApplyClipArt(diameter)) {
            Sprite_fit.FitDiameter(sprite_renderer, diameter);
        }

        hit_radius = BallDiameter() * 0.5f * hit_radius_ratio;

        damage = big_damage;
        speed *= speed_scale;

        // 커진 만큼 빛도 넓게.
        if (glow != null) {
            glow.Set(glow_color, glow_intensity, GlowRadius());
        }

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null) {
            body = gameObject.AddComponent<Rigidbody2D>();
        }
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        // 콜라이더는 루트에 둔다. 반지름은 scale 에 곱해지므로 나눠서 넣는다.
        Collider2D existing = GetComponent<Collider2D>();
        CircleCollider2D circle = existing as CircleCollider2D;
        if (existing == null) {
            circle = gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
        }
        if (circle != null) {
            circle.radius = hit_radius / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (is_arc) {
            // 중력을 속도에 쌓고 그 속도로 움직인다.
            arc_velocity.y -= arc_gravity * dt;
            transform.position += (Vector3)(arc_velocity * dt);

            if (rotate_to_velocity) {
                Sprite_fit.RotateToward(sprite_renderer.transform, sprite_renderer, arc_velocity, art_faces_left);
            }

            // 바닥에 닿으면 터진다. 착지 자체로는 용사를 때리지 않는다.
            if (arc_velocity.y < 0f && transform.position.y <= arc_floor_y) {
                Debug.Log("큰 화염구 착지 - 터짐");
                Shatter();
                return;
            }
        }
        else {
            transform.position += (Vector3)(direction.normalized * (speed * dt));
        }

        UpdateFlash();

        // 어느 쪽으로 쏘든 화면 밖으로 나가면 지운다.
        float x = transform.position.x;
        if (x < World_scroll.LeftX() - despawn_margin || x > World_scroll.RightX() + despawn_margin) {
            Destroy(gameObject);
            return;
        }

        if (player == null) {
            return;
        }

        float distance = Vector2.Distance(player.position, transform.position);
        if (distance > hit_radius) {
            return;
        }

        // 무적 중이면 맞지 않고 그냥 지나간다.
        if (health != null && health.is_invincible) {
            return;
        }

        if (health != null) {
            health.TakeHit(damage);
        }

        Destroy(gameObject);
    }

    // 불빛 반경. 그림이 바뀌어도 렌더 지름 기준이라 따라온다.
    float GlowRadius()
    {
        return Mathf.Max(glow_min_radius, BallDiameter() * glow_radius_ratio);
    }

    // 판정·불빛·파편의 기준 지름. 클립 그림은 꼬리가 길어서 높이(공 머리)를 보고, 그 외엔 긴 변을 본다.
    float BallDiameter()
    {
        Vector2 rendered = Sprite_fit.WorldSize(sprite_renderer);
        return clip_art ? rendered.y : Mathf.Max(rendered.x, rendered.y);
    }

    // Resources/fireball 클립을 붙인다. 성공하면 true. 가장 큰 프레임의 높이가 diameter 가 되게 맞추고 한 번 돌린다.
    bool ApplyClipArt(float diameter)
    {
        float fps;
        Sprite[] frames = LoadClipFrames(out fps);
        if (frames == null || sprite_renderer == null) {
            return false;
        }

        clip_art = true;

        // 진짜 그림이면 프리팹의 임시 틴트(보라)는 쓰지 않는다.
        sprite_renderer.color = Color.white;
        base_color = Color.white;

        // 가장 큰 프레임 기준으로 크기를 잡는다. 같은 PPU 라 나머지는 비율대로 작다.
        Sprite largest = frames[0];
        foreach (Sprite frame in frames) {
            if (frame.rect.height > largest.rect.height) {
                largest = frame;
            }
        }
        sprite_renderer.sprite = largest;

        // 균등하게 키운다. FitHeight 는 높이만 맞추고 가로는 두기 때문에 꼬리 그림이 찌그러지고 콜라이더 반경도 어긋난다.
        Vector2 size = Sprite_fit.WorldSize(sprite_renderer);
        float k = size.y > 0.0001f ? diameter / size.y : 1f;
        Transform art = sprite_renderer.transform;
        art.localScale = new Vector3(art.localScale.x * k, art.localScale.y * k, art.localScale.z);

        Sprite_flipbook.Attach(sprite_renderer, frames, fps, false);
        return true;
    }

    // clips.txt 의 fireball 클립. 공 머리 중심에 피벗을 둔 스프라이트로 다시 만든다 (머리는 오른쪽 끝, 반지름 = 높이/2).
    // 그래야 transform(판정 중심·불빛·회전축)이 머리에 오고 꼬리가 뒤로 뻗는다. 뒤집어도 머리는 제자리.
    static Sprite[] LoadClipFrames(out float fps)
    {
        fps = 12f;

        Clip_library.Clip clip = Clip_library.Find(Clip_library.Read(art_folder), art_clip);
        if (clip == null || clip.frames.Count == 0) {
            return null;
        }

        fps = clip.fps;

        Sprite[] result = new Sprite[clip.frames.Count];
        for (int i = 0; i < result.Length; i++) {
            Sprite source = clip.frames[i];
            Rect rect = source.rect;

            float head_radius = rect.height * 0.5f;
            float pivot_x = rect.width > 0f ? 1f - head_radius / rect.width : 0.5f;

            Sprite rebuilt = Sprite.Create(source.texture, rect, new Vector2(Mathf.Clamp01(pivot_x), 0.5f), source.pixelsPerUnit);
            rebuilt.name = source.name;
            result[i] = rebuilt;
        }

        return result;
    }

    void UpdateFlash()
    {
        flash_timer -= Time.deltaTime;

        // 원래 색 기준으로 흰색을 섞는다. 끝나면 정확히 원래 색.
        Sprite_fit.Tint(sprite_renderer, base_color, Color.white, flash_timer > 0f ? 1f : 0f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!is_big) {
            return;
        }

        Crop_flow flow = other.GetComponentInParent<Crop_flow>();

        // 던져진 작물만 맞는다. 흘러가는 작물이나 손에 든 건 통과.
        if (flow == null || !flow.is_thrown) {
            return;
        }

        // 관통 아이템은 지나가면서 콜라이더에 다시 걸릴 수 있다. 한 화염구에 한 번만.
        if (!hit_crops.Add(flow)) {
            return;
        }

        Crop_data data = flow.data;

        // 폭발 아이템이 터지면 화염구는 즉시 격파. 반경 피해와 폭발음은 Crop_flow 가 준다.
        if (data != null && data.RollExplode()) {
            flow.Explode(null);
            Destroy(flow.gameObject);
            Break();
            return;
        }

        // 맞는 소리. 아이템 종류마다 다르다.
        Sound_bank.PlayHit(data, other.transform.position);

        // 성검은 몇 히트가 남았든 한 방에 격파하고, 안 사라지고 계속 드래곤으로 간다.
        if (data != null && data.dialogue_id == Holy_sword.dialogue_id) {
            Debug.Log("큰 화염구 명중 (성검) - 즉시 격파");
            Break();
            return;
        }

        int hits = data != null ? Mathf.Max(1, data.counter_hits) : 1;
        hits_left -= hits;
        flash_timer = hit_flash_time;

        Sprite_fit.Trigger(animator, "hit");
        if (glow != null) {
            glow.Pulse(hit_glow_intensity, hit_glow_time);
        }

        Debug.Log("큰 화염구 명중" + (hits > 1 ? " (" + hits + "히트)" : "") + ", 남은 " + hits_left);

        // 관통 아이템은 안 없어지고 계속 날아간다.
        if (data == null || !data.pierce) {
            Destroy(flow.gameObject);
        }

        if (hits_left <= 0) {
            Break();
        }
    }

    // 폭발 반경에 들어갔을 때 밖에서 부순다.
    internal void ForceBreak()
    {
        if (is_big) {
            Break();
        }
    }

    // 격파 연출. Fireball 은 Dragon 을 모르므로 여기 상수로 둔다.
    const float break_shake_amplitude = 0.15f;
    const float break_shake_time = 0.2f;
    const float break_hitstop_time = 0.06f;
    const float break_zoom_amount = 0.08f;
    const float break_zoom_time = 0.35f;
    const float break_flash_time = 0.12f;
    static readonly Color break_flash_color = new Color(1f, 1f, 1f, 0.5f);

    // 작물에 다 맞아 격파됐다. 흰 플래시 + 멈칫 + 줌 + 흔들림, 그리고 파편을 튀기고 사라진다.
    void Break()
    {
        // 폭발 반경과 직접 명중이 같은 프레임에 겹쳐도 한 번만.
        if (broken) {
            return;
        }
        broken = true;

        Debug.Log("큰 화염구 격파!");
        Popup_text.ShowText(transform.position, "격파!", Color.white);

        Camera_director.Shake(break_shake_amplitude, break_shake_time);
        Camera_director.HitStop(break_hitstop_time);
        Camera_director.ZoomPunch(break_zoom_amount, break_zoom_time);
        Camera_director.Flash(break_flash_color, break_flash_time);

        Sprite_fit.Trigger(animator, "break");

        Shatter();
    }

    // 파편을 사방으로 튀기고 사라진다. 격파와 포물선 착지가 같이 쓴다.
    // fragment_prefab 이 있으면 그걸 뿌리고, 없으면 본체 그림을 작게 복제한다. 파편 크기는 본체 지름 기준.
    void Shatter()
    {
        Vector2 rendered = Sprite_fit.WorldSize(sprite_renderer);
        float fragment_size = BallDiameter() * fragment_size_ratio;

        // 터진 자리에 큰 빛이 남았다가 사라진다. 몸에 붙은 불빛은 몸과 함께 사라진다.
        Scene_lighting.Flash(transform.position, glow_color, shatter_glow_intensity, GlowRadius() * 1.3f, shatter_glow_time);

        bool can_spawn = fragment_prefab != null || (sprite_renderer != null && sprite_renderer.sprite != null);

        if (can_spawn) {
            for (int i = 0; i < fragment_count; i++) {
                float angle = (360f / fragment_count) * i + Random.Range(-15f, 15f);
                Vector2 fragment_velocity = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad))
                    * fragment_speed * Random.Range(0.6f, 1.2f);

                if (fragment_prefab != null) {
                    Fireball_fragment.SpawnPrefab(fragment_prefab, transform.position, fragment_size, fragment_velocity, fragment_life);
                }
                else {
                    Fireball_fragment.Spawn(transform.position, sprite_renderer.sprite, base_color,
                        sprite_renderer.sortingOrder, fragment_size, fragment_velocity, fragment_life);
                }
            }
        }

        Destroy(gameObject);
    }
}
