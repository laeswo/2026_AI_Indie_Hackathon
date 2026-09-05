using UnityEngine;

// 카메라 연출 네 가지를 한 곳에서 한다. 어디서든 정적 메서드 한 줄로 부른다.
//   Shake(진폭, 시간)        화면 흔들림. 진폭은 남은 시간 비율로 선형 감쇠
//   HitStop(시간)            Time.timeScale 을 0 으로 잠깐 멈춤. 최대 0.12초. 게임이 끝났으면 무시
//   ZoomPunch(비율, 시간)    orthographicSize 를 순간 당겼다가 EaseOut 으로 복귀
//   Flash(색, 시간)          전체화면을 색으로 덮고 알파 1→0 선형 감쇠. 그리는 건 Hud (HUD 위에 덮인다)
//   SlowMo(배율, 시간)       Time.timeScale 을 배율로 낮췄다가 시간이 지나면 1 로. 히트스톱과 겹치면 히트스톱이 끝날 때 배율로 돌아온다
//
// 씬에 놓을 필요 없다. 처음 부를 때 Camera.main 에 없으면 붙인다.
// Awake 에서 원래 자리(rest_position)와 원래 크기(rest_size)를 기억하고, 모든 연출은 그 둘을 기준으로 계산한다.
// 오프셋이 쌓이지 않고, 끝나면 정확히 원래 값으로 돌아간다. z 는 건드리지 않는다(카메라 z = -10 유지).
// 타이머는 전부 unscaledDeltaTime 이라 히트스톱(timeScale 0) 중에도 흔들림이 진행된다.
// 겹침 규칙: 셰이크·줌·히트스톱은 큰 값 유지, 플래시는 나중 것이 덮는다.
//
// World_scroll 은 화면 경계를 Camera.main 대신 RestPosition() / RestSize() 로 읽는다. 줌·셰이크 중에도 스폰·소멸이 안 튄다.
public class Camera_director : MonoBehaviour
{

    // 히트스톱 상한. 이보다 길면 "멈칫" 이 아니라 "멈춤" 으로 읽힌다.
    const float hitstop_max = 0.12f;

    static Camera_director instance;

    // 히트스톱은 timeScale 이라는 전역을 만지므로, 컴포넌트가 사라져도 복구할 수 있게 정적으로 둔다.
    static bool hitstop_active;
    static float base_time_scale = 1f;

    Camera cam;
    Vector3 rest_position;      // 흔들기 전 카메라 자리
    float rest_size;            // 줌 전 orthographicSize

    // 셰이크
    float shake_amplitude;
    float shake_duration;
    float shake_remaining;
    bool shake_was_active;

    // 히트스톱
    float hitstop_remaining;

    // 슬로우모션. 히트스톱처럼 전역이라 정적으로 둔다.
    static bool slowmo_active;
    static float slowmo_scale = 1f;
    float slowmo_remaining;

    // 줌 펀치
    float zoom_amount;
    float zoom_duration;
    float zoom_remaining;
    bool zoom_was_active;

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        instance = null;

        // 지난 플레이가 히트스톱·슬로우모션 도중 끝났다면 timeScale 이 남아 있을 수 있다.
        if (hitstop_active || slowmo_active) {
            Time.timeScale = 1f;
            hitstop_active = false;
            slowmo_active = false;
            base_time_scale = 1f;
        }
    }

    // ---------- 밖에서 부르는 것 ----------

    public static void Shake(float amplitude, float duration)
    {
        if (amplitude <= 0f || duration <= 0f) {
            return;
        }

        Camera_director director = Get();
        if (director != null) {
            director.BeginShake(amplitude, duration);
        }
    }

    public static void HitStop(float duration)
    {
        if (duration <= 0f || Game_flow.is_over) {
            return;
        }

        Camera_director director = Get();
        if (director != null) {
            director.BeginHitStop(Mathf.Min(duration, hitstop_max));
        }
    }

    public static void ZoomPunch(float amount, float duration)
    {
        if (amount <= 0f || duration <= 0f) {
            return;
        }

        Camera_director director = Get();
        if (director != null) {
            director.BeginZoom(Mathf.Clamp01(amount), duration);
        }
    }

    public static void SlowMo(float scale, float duration)
    {
        if (duration <= 0f || Game_flow.is_over) {
            return;
        }

        Camera_director director = Get();
        if (director != null) {
            director.BeginSlowMo(Mathf.Clamp(scale, 0.05f, 1f), duration);
        }
    }

    // 플래시는 카메라가 아니라 HUD Canvas 가 그린다. 여기서는 넘겨주기만.
    public static void Flash(Color color, float duration)
    {
        if (duration <= 0f || color.a <= 0f) {
            return;
        }

        Hud.Flash(color, duration);
    }

    // 연출과 무관한 "원래" 자리와 크기. 컴포넌트가 아직 없으면 카메라의 현재 값이 곧 원래 값이다.
    public static Vector3 RestPosition()
    {
        if (instance != null) {
            return instance.rest_position;
        }

        Camera main = Camera.main;
        return main != null ? main.transform.position : Vector3.zero;
    }

    public static float RestSize()
    {
        if (instance != null) {
            return instance.rest_size;
        }

        Camera main = Camera.main;
        return main != null ? main.orthographicSize : 5f;
    }

    // ---------- 준비 ----------

    static Camera_director Get()
    {
        if (instance != null) {
            return instance;
        }

        Camera main = Camera.main;
        if (main == null) {
            Debug.LogWarning("Camera_director : MainCamera 태그가 붙은 카메라가 없어서 연출을 할 수 없습니다.");
            return null;
        }

        instance = main.GetComponent<Camera_director>();
        if (instance == null) {
            instance = main.gameObject.AddComponent<Camera_director>();
        }

        return instance;
    }

    void Awake()
    {
        if (instance == null) {
            instance = this;
        }

        cam = GetComponent<Camera>();
        rest_position = transform.position;
        rest_size = cam != null ? cam.orthographicSize : 5f;
    }

    void BeginShake(float amplitude, float duration)
    {
        // 이미 흔드는 중이면 큰 쪽을 남긴다. 작은 흔들림이 큰 흔들림을 끊지 않게.
        if (shake_remaining > 0f) {
            shake_amplitude = Mathf.Max(shake_amplitude, amplitude);
            shake_duration = Mathf.Max(shake_duration, duration);
            shake_remaining = Mathf.Max(shake_remaining, duration);
            return;
        }

        shake_amplitude = amplitude;
        shake_duration = duration;
        shake_remaining = duration;
    }

    void BeginHitStop(float duration)
    {
        // 연타로 들어와도 누적하지 않는다. 남은 시간 중 큰 값만.
        if (hitstop_active) {
            hitstop_remaining = Mathf.Max(hitstop_remaining, duration);
            return;
        }

        hitstop_active = true;
        base_time_scale = Time.timeScale > 0f ? Time.timeScale : 1f;
        hitstop_remaining = duration;
        Time.timeScale = 0f;
    }

    void BeginSlowMo(float scale, float duration)
    {
        slowmo_active = true;
        slowmo_scale = scale;
        slowmo_remaining = Mathf.Max(slowmo_remaining, duration);

        // 히트스톱 중이면 지금 건드리지 않고, 히트스톱이 끝날 때 이 배율로 돌아오게만 한다.
        if (hitstop_active) {
            base_time_scale = scale;
            return;
        }

        Time.timeScale = scale;
    }

    void EndSlowMo()
    {
        slowmo_remaining = 0f;
        slowmo_active = false;
        slowmo_scale = 1f;

        if (hitstop_active) {
            base_time_scale = 1f;
            return;
        }

        Time.timeScale = 1f;
    }

    void BeginZoom(float amount, float duration)
    {
        if (zoom_remaining > 0f) {
            zoom_amount = Mathf.Max(zoom_amount, amount);
            zoom_duration = Mathf.Max(zoom_duration, duration);
            zoom_remaining = Mathf.Max(zoom_remaining, duration);
            return;
        }

        zoom_amount = amount;
        zoom_duration = duration;
        zoom_remaining = duration;
    }

    // ---------- 진행 ----------

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;

        TickHitStop(dt);
        TickSlowMo(dt);
        TickShake(dt);
        TickZoom(dt);
    }

    void TickSlowMo(float dt)
    {
        if (!slowmo_active) {
            return;
        }

        slowmo_remaining -= dt;
        if (slowmo_remaining <= 0f) {
            EndSlowMo();
        }
    }

    void TickHitStop(float dt)
    {
        if (!hitstop_active) {
            return;
        }

        hitstop_remaining -= dt;
        if (hitstop_remaining <= 0f) {
            EndHitStop();
        }
    }

    void EndHitStop()
    {
        hitstop_remaining = 0f;
        hitstop_active = false;
        Time.timeScale = base_time_scale;
    }

    void TickShake(float dt)
    {
        if (shake_remaining <= 0f) {
            if (shake_was_active) {
                // 끝. 1픽셀도 어긋나지 않게 원래 자리로 딱 돌려놓는다.
                shake_was_active = false;
                transform.position = rest_position;
            }
            return;
        }

        shake_was_active = true;
        shake_remaining -= dt;

        if (shake_remaining <= 0f) {
            shake_remaining = 0f;
            shake_was_active = false;
            transform.position = rest_position;
            return;
        }

        // 남은 시간 비율로 선형 감쇠. 오프셋은 매 프레임 새로 뽑고 원래 자리에 더한다.
        float current = shake_amplitude * (shake_duration > 0f ? shake_remaining / shake_duration : 0f);
        Vector2 offset = Random.insideUnitCircle * current;

        transform.position = new Vector3(rest_position.x + offset.x, rest_position.y + offset.y, rest_position.z);
    }

    void TickZoom(float dt)
    {
        if (cam == null) {
            zoom_remaining = 0f;
            return;
        }

        if (zoom_remaining <= 0f) {
            if (zoom_was_active) {
                zoom_was_active = false;
                cam.orthographicSize = rest_size;
            }
            return;
        }

        zoom_was_active = true;
        zoom_remaining -= dt;

        if (zoom_remaining <= 0f) {
            zoom_remaining = 0f;
            zoom_was_active = false;
            cam.orthographicSize = rest_size;
            return;
        }

        // 시작 순간 rest_size * (1 - amount) 까지 당겨져 있고, EaseOut 으로 원래 크기로 돌아온다.
        float t = zoom_duration > 0f ? 1f - zoom_remaining / zoom_duration : 1f;
        float pull = zoom_amount * (1f - Dragon.EaseOut(t));

        cam.orthographicSize = rest_size * (1f - pull);
    }

    // 플레이 종료나 파괴 때 전역(timeScale)과 카메라를 남기지 않는다.
    void OnDestroy()
    {
        if (hitstop_active) {
            EndHitStop();
        }
        if (slowmo_active) {
            EndSlowMo();
        }

        if (cam != null) {
            transform.position = rest_position;
            cam.orthographicSize = rest_size;
        }

        if (instance == this) {
            instance = null;
        }
    }
}
