using UnityEngine;

// 던지기 전에 날아갈 경로를 미리 그려준다.
// Unity 의 Rigidbody2D 가 실제로 쓰는 적분 방식을 그대로 흉내내므로
// 그려진 선과 실제 궤적이 거의 어긋나지 않는다.
[RequireComponent(typeof(LineRenderer))]
public class Throw_trajectory : MonoBehaviour
{

    public int point_count = 90;
    public float line_width = 0.08f;

    // 힘이 약할 때와 셀 때의 색. 게이지 대신 선 색으로도 세기를 읽을 수 있다.
    public Color low_power_color = new Color(1f, 1f, 1f, 0.7f);
    public Color high_power_color = new Color(1f, 0.35f, 0.2f, 0.9f);

    // 벽이나 바닥에 닿는 지점에서 선을 끊을지 여부.
    public bool stop_on_hit = true;
    public LayerMask blocking_layers = ~0;

    LineRenderer line;
    Vector3[] points;
    Gradient gradient = new Gradient();

    // 레이캐스트 결과를 매 프레임 새로 할당하지 않도록 버퍼를 재사용한다.
    RaycastHit2D[] hit_buffer = new RaycastHit2D[8];
    ContactFilter2D hit_filter;

    void Awake()
    {
        line = GetComponent<LineRenderer>();

        // 코드에서 AddComponent 로 붙인 경우를 대비한다. RequireComponent 가 못 챙기는 경우가 있다.
        if (line == null) {
            line = gameObject.AddComponent<LineRenderer>();
        }

        if (point_count < 2) {
            point_count = 2;
        }

        points = new Vector3[point_count];

        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 2;
        line.startWidth = line_width;
        line.endWidth = line_width * 0.4f;
        line.sortingOrder = 100;

        // 머티리얼이 비어 있으면 라인이 분홍색으로 나오므로 기본값을 하나 만들어 준다.
        if (line.sharedMaterial == null) {
            line.material = CreateDefaultMaterial();
        }

        hit_filter = new ContactFilter2D();
        hit_filter.useTriggers = false;
        hit_filter.SetLayerMask(blocking_layers);

        Hide();
    }

    static Material CreateDefaultMaterial()
    {
        // Sprites/Default 는 정점 색을 그대로 반영해서 LineRenderer 색이 그대로 나온다.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null) {
            shader = Shader.Find("Unlit/Color");
        }

        return new Material(shader);
    }

    public void Hide()
    {
        if (line != null) {
            line.enabled = false;
        }
    }

    // origin 에서 velocity 로 출발했을 때의 경로를 그린다.
    // body 는 중력 배율과 감쇠를 읽고, 자기 자신과의 충돌을 무시하는 데 쓴다.
    // power_ratio 는 0~1 사이의 힘 비율이며 선 색에만 영향을 준다.
    public void Show(Vector2 origin, Vector2 velocity, Rigidbody2D body, float power_ratio)
    {
        if (line == null) {
            return;
        }

        if (velocity.sqrMagnitude <= 0.0001f) {
            Hide();
            return;
        }

        float gravity_scale = body != null ? body.gravityScale : 1f;
        float damping = body != null ? body.linearDamping : 0f;

        // 물리 스텝과 같은 간격으로 적분해야 실제 궤적과 맞는다.
        float dt = Time.fixedDeltaTime;
        Vector2 gravity = Physics2D.gravity * gravity_scale;

        Vector2 position = origin;
        Vector2 current_velocity = velocity;

        points[0] = position;
        int used = 1;

        for (int i = 1; i < point_count; i++) {
            // Rigidbody2D 와 동일한 순서: 중력을 더하고, 감쇠를 곱하고, 위치를 옮긴다.
            current_velocity += gravity * dt;
            current_velocity /= 1f + damping * dt;

            Vector2 next = position + current_velocity * dt;

            if (stop_on_hit) {
                Vector2 segment = next - position;
                float distance = segment.magnitude;

                if (distance > 0f && HasBlockingHit(position, segment / distance, distance, body, out Vector2 hit_point)) {
                    points[used] = hit_point;
                    used++;
                    break;
                }
            }

            position = next;
            points[used] = position;
            used++;
        }

        // 버퍼 길이와 실제 점 개수가 다를 수 있으므로 하나씩 넣는다.
        line.positionCount = used;
        for (int i = 0; i < used; i++) {
            line.SetPosition(i, points[i]);
        }
        line.startWidth = line_width;
        line.endWidth = line_width * 0.4f;

        ApplyColor(power_ratio);
        line.enabled = true;
    }

    bool HasBlockingHit(Vector2 origin, Vector2 direction, float distance, Rigidbody2D ignore_body, out Vector2 hit_point)
    {
        hit_point = origin;

        hit_filter.SetLayerMask(blocking_layers);
        int count = Physics2D.Raycast(origin, direction, hit_filter, hit_buffer, distance);

        for (int i = 0; i < count; i++) {
            RaycastHit2D hit = hit_buffer[i];
            if (hit.collider == null) {
                continue;
            }

            // 지금 손에 들고 있는 작물 자신은 장애물이 아니다.
            if (ignore_body != null && hit.collider.attachedRigidbody == ignore_body) {
                continue;
            }

            hit_point = hit.point;
            return true;
        }

        return false;
    }

    void ApplyColor(float power_ratio)
    {
        Color color = Color.Lerp(low_power_color, high_power_color, Mathf.Clamp01(power_ratio));

        // 끝으로 갈수록 옅어지게 해서 예측선이라는 느낌을 준다.
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(color.a * 0.15f, 1f)
            }
        );

        line.colorGradient = gradient;
    }
}
