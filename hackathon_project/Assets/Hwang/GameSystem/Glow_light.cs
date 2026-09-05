using UnityEngine;
using UnityEngine.Rendering.Universal;

// 라이트 하나의 동작. Scene_lighting.Attach / AttachShape 가 만들어 붙인다.
//   Set(색, 세기[, 반경])   기준값. 부른 쪽이 매 프레임 바꿔도 된다(드래곤 상태 색, 브레스 길이)
//   SetRect(폭, 높이)        사각형(Freeform) 라이트의 크기. 띠처럼 네모난 것은 원 대신 이걸 써야 모서리가 어둡지 않다
//   SetCone(안쪽각, 바깥각)  점 라이트를 원뿔(스포트)로. 가로등처럼 한 방향만 비출 때
//   Pulse(정점, 시간)        순간 정점까지 밝아졌다가 시간 동안 기준으로 돌아온다(피격·명중)
//   FadeOut(시간)            시간 동안 꺼지며 사라진다(번쩍)
//   flicker_amount           불처럼 일렁이는 정도(0~1)
// 타이머는 unscaled 라 히트스톱 중에도 번쩍임이 식는다. 부모가 scale 되어 있어도 반경은 월드 유닛 그대로 나오게 나눠 넣는다.
public class Glow_light : MonoBehaviour
{

    public float flicker_amount = 0f;
    public float flicker_speed = 9f;

    Light2D light2d;
    Color color = Color.white;
    float intensity;
    float radius = 1f;

    // 사각형 라이트의 꼭짓점. 매 프레임 새로 만들지 않고 값만 바꾼다.
    readonly Vector3[] rect_path = new Vector3[4];

    float pulse_peak;
    float pulse_duration;
    float pulse_remaining;

    bool fading;
    float fade_duration;
    float fade_remaining;

    float seed;

    internal void Init(Light2D light)
    {
        light2d = light;
        seed = Random.value * 100f;
    }

    public void Set(Color new_color, float new_intensity)
    {
        color = new_color;
        intensity = Mathf.Max(0f, new_intensity);
        ApplyShape();
    }

    public void Set(Color new_color, float new_intensity, float new_radius)
    {
        radius = Mathf.Max(0.01f, new_radius);
        Set(new_color, new_intensity);
    }

    // 사각형(Freeform) 라이트의 폭·높이. 중심이 이 오브젝트 위치다. 부모 scale 을 타지 않게 나눠 넣는다.
    public void SetRect(float width, float height)
    {
        if (light2d == null || light2d.lightType != Light2D.LightType.Freeform) {
            return;
        }

        Vector3 scale = transform.lossyScale;
        float half_w = 0.5f * Mathf.Max(0.01f, width) / Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float half_h = 0.5f * Mathf.Max(0.01f, height) / Mathf.Max(0.0001f, Mathf.Abs(scale.y));

        rect_path[0] = new Vector3(-half_w, -half_h, 0f);
        rect_path[1] = new Vector3(half_w, -half_h, 0f);
        rect_path[2] = new Vector3(half_w, half_h, 0f);
        rect_path[3] = new Vector3(-half_w, half_h, 0f);
        light2d.SetShapePath(rect_path);
    }

    // 점 라이트를 원뿔로 좁힌다. 원뿔은 라이트의 로컬 +y 쪽으로 열리므로 방향은 transform.rotation 으로 정한다.
    public void SetCone(float inner_angle, float outer_angle)
    {
        if (light2d == null || light2d.lightType != Light2D.LightType.Point) {
            return;
        }

        light2d.pointLightInnerAngle = Mathf.Clamp(inner_angle, 0f, 360f);
        light2d.pointLightOuterAngle = Mathf.Clamp(outer_angle, 0f, 360f);
    }

    // 순간 peak 까지 올라가서 duration 동안 기준 세기로 돌아온다. 더 센 펄스가 진행 중이면 그걸 둔다.
    public void Pulse(float peak, float duration)
    {
        if (pulse_remaining > 0f && pulse_peak >= peak) {
            return;
        }

        pulse_peak = peak;
        pulse_duration = Mathf.Max(0.01f, duration);
        pulse_remaining = pulse_duration;
    }

    // duration 동안 꺼지며 끝나면 오브젝트를 없앤다.
    public void FadeOut(float duration)
    {
        fading = true;
        fade_duration = Mathf.Max(0.01f, duration);
        fade_remaining = fade_duration;
    }

    void ApplyShape()
    {
        if (light2d == null) {
            return;
        }

        light2d.color = color;

        // 점 라이트만 반경이 있다. 부모 scale 을 타지 않게 나눠 넣는다. 반경은 항상 월드 유닛.
        if (light2d.lightType == Light2D.LightType.Point) {
            float parent_scale = Mathf.Abs(transform.lossyScale.x);
            light2d.pointLightOuterRadius = radius / Mathf.Max(0.0001f, parent_scale);
        }
    }

    void Update()
    {
        if (light2d == null) {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float current = intensity;

        if (pulse_remaining > 0f) {
            pulse_remaining -= dt;
            float t = Mathf.Clamp01(pulse_remaining / pulse_duration);
            current = Mathf.Lerp(intensity, pulse_peak, t * t);
        }

        if (flicker_amount > 0f) {
            float noise = Mathf.PerlinNoise(Time.unscaledTime * flicker_speed, seed);
            current *= 1f - flicker_amount * 0.5f + flicker_amount * noise;
        }

        if (fading) {
            fade_remaining -= dt;
            if (fade_remaining <= 0f) {
                Destroy(gameObject);
                return;
            }

            float t = fade_remaining / fade_duration;
            current *= t * t;
        }

        light2d.intensity = current;
    }
}
