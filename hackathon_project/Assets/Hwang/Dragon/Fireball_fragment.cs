using UnityEngine;

// 큰 화염구가 부서질 때 튀는 파편. 날아가면서 작아지고 옅어지다가 사라진다.
// 파편 프리팹(fragment_prefab)이 있으면 SpawnPrefab 으로 그걸 쓰고, 없으면 Spawn 이 본체 그림을 작게 복제한다.
// 크기는 "유닛 지름" 으로 받아 그림 bounds 에 맞추므로 어떤 그림이든 같은 크기로 나온다.
public class Fireball_fragment : MonoBehaviour
{

    Vector2 velocity;
    float life;
    float age;
    float gravity = 12f;
    Vector3 start_scale;
    SpriteRenderer sprite_renderer;
    Color base_color = Color.white;

    // 본체 그림을 복제해서 파편으로 쓴다. size 는 유닛 지름.
    public static Fireball_fragment Spawn(Vector3 position, Sprite sprite, Color color, int sorting_order,
        float size, Vector2 velocity, float life)
    {
        GameObject holder = new GameObject("Fireball_fragment");
        holder.transform.position = position;

        SpriteRenderer renderer = holder.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sorting_order + 1;
        Scene_lighting.ApplyLitMaterial(renderer);

        Sprite_fit.FitDiameter(renderer, size);

        Fireball_fragment fragment = holder.AddComponent<Fireball_fragment>();
        fragment.Init(renderer, velocity, life);
        return fragment;
    }

    // 파편 프리팹을 쓴다. 프리팹에 이 컴포넌트가 없으면 붙인다. size 는 유닛 지름.
    public static Fireball_fragment SpawnPrefab(GameObject prefab, Vector3 position, float size, Vector2 velocity, float life)
    {
        GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
        instance.name = "Fireball_fragment";

        SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null) {
            Sprite_fit.FitDiameter(renderer, size);
        }

        Fireball_fragment fragment = instance.GetComponent<Fireball_fragment>();
        if (fragment == null) {
            fragment = instance.AddComponent<Fireball_fragment>();
        }

        fragment.Init(renderer, velocity, life);
        return fragment;
    }

    void Init(SpriteRenderer renderer, Vector2 start_velocity, float life_time)
    {
        sprite_renderer = renderer;
        base_color = renderer != null ? renderer.color : Color.white;
        velocity = start_velocity;
        life = Mathf.Max(0.05f, life_time);
        start_scale = transform.localScale;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) {
            Destroy(gameObject);
            return;
        }

        velocity.y -= gravity * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);

        // 루트를 줄이면 자식 그림도 같이 줄어든다.
        float t = age / life;
        transform.localScale = start_scale * (1f - t * 0.6f);

        if (sprite_renderer != null) {
            // 원래 색 기준으로 알파만 옅어진다.
            Color color = base_color;
            color.a *= 1f - t;
            sprite_renderer.color = color;
        }
    }
}
