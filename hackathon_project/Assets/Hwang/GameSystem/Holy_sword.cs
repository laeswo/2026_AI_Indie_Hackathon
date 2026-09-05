using UnityEngine;
using UnityEngine.Rendering.Universal;

// 성검. 등장하면 하늘에서 성검 쪽으로 원뿔 빛줄기가 내려왔다가 사라진다(성검 자체는 빛나지 않는다).
// 던져지면 약한 후광이 꼬리처럼 따라붙고, 맞거나 사라질 때 꺼진다.
// Crop_flow 는 건드리지 않는다. 스폰 때 Setup() 이 필요한 컴포넌트(Crop_data · Rigidbody2D · Collider2D · Crop_flow)를 붙이고
// 이 컴포넌트를 얹는다. 프리팹은 그림만 있으면 되고, 직접 붙여 둔 컴포넌트가 있으면 그 값을 우선한다.
// 그림이 자식 오브젝트에 있으면 흐르는 동안 위아래로 살짝 떠 있는다. 루트에 그림이 직접 있으면 부유는 건너뛴다(Crop_flow 가 위치를 잡는다).
public class Holy_sword : MonoBehaviour
{

    // 대사표 id. Player 가 던질 때 이걸로 성검인지 알아본다.
    public const string dialogue_id = "pickup_holysword";

    // Crop_data 기본값 (프리팹에 없을 때)
    const int damage = 70;
    const float gravity_scale = 0.35f;
    const float homing_turn_rate = 240f;

    // 하늘에서 내려오는 원뿔 빛줄기. 성검 위 화면 밖에서 성검을 향해 좁게 비춘다. 성검이 흐르는 동안 따라다닌다.
    // Additive 라이트라 세기 1 을 넘으면 Bloom 이 번진다. 작게. 빛줄기 자체가 보이는 건 볼륨(volume) 값이다.
    static readonly Color beam_color = new Color(1f, 0.92f, 0.6f);
    const float beam_intensity = 0.45f;     // 바닥·성검을 비추는 세기
    const float beam_volume = 0.25f;        // 공중에 보이는 빛줄기의 진하기 (0 이면 줄기가 안 보인다)
    const float beam_inner_angle = 8f;      // 원뿔 안쪽(꽉 찬) 각도
    const float beam_outer_angle = 22f;     // 원뿔 바깥(옅어지는) 각도. 전체 각이라 작을수록 가늘다
    const float beam_top_margin = 1f;       // 화면 위 끝보다 이만큼 더 위에서 시작한다
    const float beam_reach_below = 1.5f;    // 성검을 지나 이만큼 더 내려가 바닥까지 닿게
    const float beam_fade_in_time = 0.6f;
    const float beam_hold_time = 3.5f;      // 다 밝아진 뒤 유지. 그 전에 주우면 바로 꺼지기 시작한다
    const float beam_fade_out_time = 1.2f;

    // 던진 뒤 꼬리처럼 따라붙는 약한 후광. 흐르는 동안은 없다.
    static readonly Color glow_color = new Color(1f, 0.9f, 0.55f);
    const float thrown_intensity = 0.3f;
    const float thrown_radius_scale = 1.0f; // 던진 뒤 반경 = 그림 긴 변 × 이 값

    // 대장장이(-1)·일반 작물(0)보다 앞에 그린다. 뒤따르는 대장장이에게 가려지지 않게
    const int sorting_order = 1;
    const float thrown_flicker = 0.3f;
    const float fade_out_time = 0.3f;

    // 부유 (그림이 자식일 때만)
    const float bob_amplitude = 0.15f;
    const float bob_period = 1.2f;

    // 그림. 프리팹의 스프라이트가 비었거나 잃어버린(Missing) 참조면 Resources/Ob_holy_sword 의 첫 그림을 쓴다.
    // 그것도 없으면 금빛 임시 칼날(길쭉한 타원)로라도 보이게 한다. 다른 아이템처럼 긴 변 art_size 유닛.
    const string art_folder = "Ob_holy_sword";
    const float art_size = 1.5f;            // 다른 아이템과 같은 크기 (Item_art_tool.item_size)
    const float placeholder_width = 0.35f;
    const float placeholder_height = 1.2f;
    static readonly Color placeholder_color = new Color(1f, 0.9f, 0.5f);

    Crop_flow flow;
    Glow_light glow;            // 던진 뒤 후광. 그 전엔 null
    Glow_light beam;            // 하늘에서 내려오는 빛줄기. 다 꺼지면 null
    float beam_time;
    bool beam_closing;          // 페이드 아웃에 들어갔다
    float beam_close_time;      // 페이드 아웃 시작 시각
    float beam_close_from;      // 페이드 아웃 시작 때 세기
    float art_longest = 1f;     // 그림 긴 변(월드). 후광 반경의 기준
    Transform art;              // 흔들 그림. 루트면 null
    Vector3 art_rest;
    float bob_time;
    bool thrown_applied;

    // 스폰 직후 부른다. 없는 컴포넌트를 채우고 성검 동작을 붙인다.
    public static Holy_sword Setup(GameObject sword)
    {
        if (sword == null) {
            return null;
        }

        // 순서가 중요하다. Crop_flow.Awake 가 Crop_data 와 콜라이더를 읽으므로 그 둘을 먼저.
        Crop_data data = sword.GetComponentInChildren<Crop_data>();
        if (data == null) {
            data = sword.AddComponent<Crop_data>();
            data.display_name = "성검";
            data.damage_min = damage;
            data.damage_max = damage;
            data.gravity_scale = gravity_scale;
            data.spin = 0f;
            data.spawn_weight = 0f;
            data.homing_turn_rate = homing_turn_rate;
        }
        if (string.IsNullOrEmpty(data.dialogue_id)) {
            data.dialogue_id = dialogue_id;
        }

        Rigidbody2D body = sword.GetComponent<Rigidbody2D>();
        if (body == null) {
            body = sword.AddComponent<Rigidbody2D>();
            body.mass = 1f;
            body.gravityScale = gravity_scale;
            body.constraints = RigidbodyConstraints2D.None;
        }

        // 그림을 콜라이더보다 먼저 정한다. 콜라이더 크기가 그림에서 나온다.
        // 대장장이보다 앞에, 그리고 빛을 받는 재질로.
        SpriteRenderer art_renderer = sword.GetComponentInChildren<SpriteRenderer>();
        if (art_renderer == null) {
            art_renderer = sword.AddComponent<SpriteRenderer>();
        }
        EnsureArt(sword, art_renderer);
        art_renderer.sortingOrder = Mathf.Max(art_renderer.sortingOrder, sorting_order);
        Scene_lighting.ApplyLitMaterial(art_renderer);

        if (sword.GetComponentInChildren<Collider2D>() == null) {
            AddArtCollider(sword);
        }

        if (sword.GetComponent<Crop_flow>() == null) {
            sword.AddComponent<Crop_flow>();
        }

        Holy_sword holy = sword.GetComponent<Holy_sword>();
        if (holy == null) {
            holy = sword.AddComponent<Holy_sword>();
        }
        return holy;
    }

    // 프리팹에 그림이 들어 있으면 그림도 scale 도 건드리지 않는다 (Item_art_tool 이 넣고 크기를 맞춰 둔다).
    // 프리팹 그림이 없으면(None 이거나 에셋이 사라진 Missing 참조. 둘 다 sprite 가 null 로 읽힌다) 대체 그림을 넣는다.
    //   1) Resources/Ob_holy_sword 에 png 가 있으면 가장 큰 조각을 긴 변 art_size 유닛으로
    //   2) 없으면 금빛 임시 칼날. 경고로 어디에 그림을 넣어야 하는지 알린다
    static void EnsureArt(GameObject sword, SpriteRenderer renderer)
    {
        if (renderer.sprite != null) {
            return;
        }

        Sprite found = LargestResourceSprite(art_folder);
        if (found != null) {
            renderer.sprite = found;
            renderer.color = Color.white;
            Sprite_fit.FitDiameter(renderer, art_size);
            return;
        }

        Debug.LogWarning(sword.name + " : 프리팹 ob_holy_sword 의 SpriteRenderer 에 그림이 없습니다(비었거나 Missing). "
            + "프리팹에 성검 그림을 넣거나 Resources/" + art_folder + " 폴더에 png 를 두세요. 그동안 임시 칼날로 그립니다.");

        renderer.sprite = Placeholder_sprite.Circle();
        renderer.color = placeholder_color;
        Sprite_fit.FitSize(renderer, placeholder_width, placeholder_height);
    }

    // 폴더의 스프라이트 중 가장 큰 조각. 자동 슬라이스 부스러기를 피한다. 없으면 null.
    static Sprite LargestResourceSprite(string folder)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(folder);
        Sprite largest = null;
        float largest_area = -1f;

        foreach (Sprite sprite in sprites) {
            if (sprite == null) {
                continue;
            }
            float area = sprite.rect.width * sprite.rect.height;
            if (area > largest_area) {
                largest_area = area;
                largest = sprite;
            }
        }

        return largest;
    }

    // 그림 크기에 맞는 BoxCollider2D. 그림이 루트에 있으면 스프라이트 로컬 bounds 그대로, 자식이면 월드 크기를 루트 scale 로 나눠 넣는다.
    static void AddArtCollider(GameObject sword)
    {
        BoxCollider2D box = sword.AddComponent<BoxCollider2D>();

        SpriteRenderer renderer = sword.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) {
            box.size = Vector2.one;
            return;
        }

        if (renderer.transform == sword.transform) {
            Bounds local = renderer.sprite.bounds;
            box.size = local.size;
            box.offset = local.center;
            return;
        }

        Bounds world = renderer.bounds;
        Vector3 scale = sword.transform.lossyScale;
        box.size = new Vector2(world.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)), world.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
        box.offset = sword.transform.InverseTransformPoint(world.center);
    }

    void Start()
    {
        flow = GetComponent<Crop_flow>();

        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        art_longest = ArtLongestSide(renderer);

        CreateBeam();

        if (renderer != null && renderer.transform != transform) {
            art = renderer.transform;
            art_rest = art.localPosition;
        }
    }

    void Update()
    {
        UpdateBob();
        UpdateBeam();
        UpdateThrownGlow();
    }

    // ---------- 하늘의 빛줄기 ----------

    // 성검 위 화면 밖에 원뿔 라이트를 만들어 아래(성검)를 향하게 한다. 원뿔은 로컬 +y 로 열리므로 180도 돌린다.
    void CreateBeam()
    {
        float top_y = World_scroll.TopY() + beam_top_margin;
        float length = Mathf.Max(1f, top_y - transform.position.y + beam_reach_below);

        beam = Scene_lighting.Attach(null, beam_color, 0f, length);
        if (beam == null) {
            return;
        }

        beam.name = "Holy_beam";
        beam.transform.position = new Vector3(transform.position.x, top_y, 0f);
        beam.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
        beam.SetCone(beam_inner_angle, beam_outer_angle);

        // 공중에 빛줄기가 보이게. 라이트가 비추는 것과 별개로 원뿔 모양이 옅게 그려진다.
        Light2D light = beam.GetComponent<Light2D>();
        if (light != null) {
            light.volumetricEnabled = true;
            light.volumeIntensity = 0f;
        }

        beam_time = 0f;
    }

    // 페이드 인 → 유지 → 페이드 아웃. 흐르는 동안 성검을 따라다니고, 주워지거나 유지 시간이 끝나면 꺼지기 시작한다.
    void UpdateBeam()
    {
        if (beam == null) {
            return;
        }

        beam_time += Time.deltaTime;

        bool flowing = flow != null && flow.is_flowing;
        if (flowing) {
            Vector3 position = beam.transform.position;
            position.x = transform.position.x;
            beam.transform.position = position;
        }

        float level;
        if (!beam_closing) {
            level = Mathf.Clamp01(beam_time / beam_fade_in_time);

            bool held_long_enough = beam_time >= beam_fade_in_time + beam_hold_time;
            if (!flowing || held_long_enough) {
                beam_closing = true;
                beam_close_time = beam_time;
                beam_close_from = level;
            }
        }
        else {
            float t = Mathf.Clamp01((beam_time - beam_close_time) / beam_fade_out_time);
            level = beam_close_from * (1f - t);

            if (t >= 1f) {
                Destroy(beam.gameObject);
                beam = null;
                return;
            }
        }

        ApplyBeamLevel(level);
    }

    void ApplyBeamLevel(float level)
    {
        beam.Set(beam_color, beam_intensity * level);

        Light2D light = beam.GetComponent<Light2D>();
        if (light != null) {
            light.volumeIntensity = beam_volume * level;
        }
    }

    // 흐르는 동안만 위아래로. 주워지면 제자리로.
    void UpdateBob()
    {
        if (art == null) {
            return;
        }

        bool flowing = flow != null && flow.is_flowing;
        if (!flowing) {
            if (art.localPosition != art_rest) {
                art.localPosition = art_rest;
            }
            return;
        }

        bob_time += Time.deltaTime;
        float offset = Mathf.Sin(bob_time * Mathf.PI * 2f / bob_period) * bob_amplitude;
        art.localPosition = art_rest + new Vector3(0f, offset, 0f);
    }

    // 던져진 뒤에만 약한 후광이 꼬리처럼 따라붙는다. 한 번만 만든다.
    void UpdateThrownGlow()
    {
        if (thrown_applied || flow == null || !flow.is_thrown) {
            return;
        }

        thrown_applied = true;
        glow = Scene_lighting.Attach(transform, glow_color, thrown_intensity, art_longest * thrown_radius_scale);
        if (glow != null) {
            glow.flicker_amount = thrown_flicker;
        }
    }

    // 그림 긴 변(월드 유닛). 그림이 없으면 1 로 본다.
    static float ArtLongestSide(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) {
            return 1f;
        }

        Vector2 world = Sprite_fit.WorldSize(renderer);
        return Mathf.Max(0.1f, Mathf.Max(world.x, world.y));
    }

    // 드래곤에 맞거나 화면 밖으로 나가 없어질 때. 후광은 떼어 내서 제자리에서 꺼지게 한다. 같이 사라지면 뚝 끊긴다.
    void OnDestroy()
    {
        // 씬이 통째로 내려가는 중이면 라이트도 같이 사라지니 손대지 않는다.
        if (!gameObject.scene.isLoaded) {
            return;
        }

        if (beam != null) {
            beam.FadeOut(fade_out_time);
            beam = null;
        }

        if (glow != null) {
            glow.transform.SetParent(null, true);
            glow.FadeOut(fade_out_time);
            glow = null;
        }
    }
}
