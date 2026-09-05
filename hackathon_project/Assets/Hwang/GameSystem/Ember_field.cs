using UnityEngine;

// 2페이즈 분위기용 불씨. 작은 주황 점들이 화면을 오른쪽에서 왼쪽으로 흘러가며 깜빡인다.
// Background.EnterPhase2 가 붙인다. 판정·물리 없음. 배경 앞, 게임 오브젝트 뒤에 그린다.
public class Ember_field : MonoBehaviour
{

    const int count = 28;
    const int sorting_order = -50;
    static readonly Color ember_color = new Color(1f, 0.55f, 0.15f, 0.85f);

    SpriteRenderer[] renderers;
    float[] drift_x;        // 바람과 별개로 제 속도로 흐르는 만큼
    float[] drift_y;        // 위로 살짝 떠오르는 속도
    float[] flicker_phase;
    float[] flicker_speed;

    void Start()
    {
        renderers = new SpriteRenderer[count];
        drift_x = new float[count];
        drift_y = new float[count];
        flicker_phase = new float[count];
        flicker_speed = new float[count];

        for (int i = 0; i < count; i++) {
            GameObject ember = new GameObject("Ember_" + i);
            ember.transform.SetParent(transform, false);

            SpriteRenderer renderer = ember.AddComponent<SpriteRenderer>();
            renderer.sprite = Placeholder_sprite.Circle();
            renderer.color = ember_color;
            renderer.sortingOrder = sorting_order;

            float size = Random.Range(0.08f, 0.18f);
            ember.transform.localScale = new Vector3(size, size, 1f);
            ember.transform.position = RandomPoint(false);

            // 여섯 개 중 하나만 빛난다. 전부 켜면 라이트가 너무 많다.
            if (i % 6 == 0) {
                Glow_light glow = Scene_lighting.Attach(ember.transform, new Color(1f, 0.55f, 0.2f), 0.8f, 1.1f);
                glow.flicker_amount = 0.5f;
            }

            renderers[i] = renderer;
            drift_x[i] = Random.Range(0.6f, 1.8f);
            drift_y[i] = Random.Range(0.25f, 0.8f);
            flicker_phase[i] = Random.Range(0f, Mathf.PI * 2f);
            flicker_speed[i] = Random.Range(4f, 9f);
        }
    }

    // 화면 안 아무 데나. from_right 면 오른쪽 밖에서 새로 들어온다.
    static Vector3 RandomPoint(bool from_right)
    {
        float top = World_scroll.TopY();
        float bottom = Camera_director.RestPosition().y - Camera_director.RestSize();
        float x = from_right ? World_scroll.RightX() + Random.Range(0.5f, 3f) : Random.Range(World_scroll.LeftX(), World_scroll.RightX());
        return new Vector3(x, Random.Range(bottom, top), 0f);
    }

    void Update()
    {
        if (renderers == null) {
            return;
        }

        float dt = Time.deltaTime;
        float wind = World_scroll.current_speed * 0.6f;
        float left = World_scroll.LeftX() - 1f;
        float top = World_scroll.TopY() + 1f;

        for (int i = 0; i < count; i++) {
            Transform t = renderers[i].transform;
            Vector3 position = t.position;
            position.x -= (wind + drift_x[i]) * dt;
            position.y += drift_y[i] * dt;

            // 왼쪽이나 위로 나가면 오른쪽 밖에서 다시 들어온다.
            if (position.x < left || position.y > top) {
                position = RandomPoint(true);
            }

            t.position = position;

            // 깜빡임. 알파만 흔든다.
            flicker_phase[i] += flicker_speed[i] * dt;
            Color color = ember_color;
            color.a *= 0.55f + 0.45f * Mathf.Sin(flicker_phase[i]);
            renderers[i].color = color;
        }
    }
}
