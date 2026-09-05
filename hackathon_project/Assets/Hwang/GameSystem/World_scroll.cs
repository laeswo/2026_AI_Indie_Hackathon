using UnityEngine;

// 세계가 왼쪽으로 흐르는 속도를 한 곳에서 관리한다.
// 배경과 작물이 같은 값을 봐야 서로 미끄러지지 않는다.
// 씬에 없으면 처음 쓰일 때 기본값으로 하나 만들어지므로 배치를 잊어도 동작한다.
public class World_scroll : MonoBehaviour
{

    [Header("스크롤")]
    public float speed = 3f;        // 초당 유닛. 양수면 세계가 왼쪽으로 흐른다.

    // 게임이 끝나면 Game_flow 가 끈다. 인스펙터에서 만질 값이 아니다.
    [System.NonSerialized]
    public bool is_running = true;

    // 드래곤 날갯짓 같은 일시적 바람. 작물과 배경이 같이 빨라진다.
    [System.NonSerialized]
    public float speed_multiplier = 1f;

    // 페이즈 2 같은 "판이 바뀐" 뒤의 영구 배율. 바람(speed_multiplier)과 곱해진다.
    [System.NonSerialized]
    public float phase_multiplier = 1f;

    static World_scroll instance;

    public static World_scroll Get()
    {
        if (instance != null) {
            return instance;
        }

        instance = FindFirstObjectByType<World_scroll>();
        if (instance != null) {
            return instance;
        }

        GameObject holder = new GameObject("World_scroll");
        instance = holder.AddComponent<World_scroll>();
        return instance;
    }

    public static float current_speed
    {
        get {
            World_scroll scroll = Get();
            return scroll.is_running ? scroll.speed * scroll.speed_multiplier * scroll.phase_multiplier : 0f;
        }
    }

    void Awake()
    {
        // 씬에 두 개가 놓이면 먼저 깨어난 쪽을 기준으로 삼는다.
        if (instance == null) {
            instance = this;
        }
    }

    // 아래는 화면 경계. 스폰 위치와 소멸 판정이 전부 이 값을 본다.

    public static float ViewHalfWidth()
    {
        Camera cam = Camera.main;
        if (cam == null) {
            return 9f;
        }

        if (cam.orthographic) {
            return Camera_director.RestSize() * cam.aspect;
        }

        // 원근 카메라면 z=0 평면 기준으로 잡는다.
        float distance = Mathf.Abs(Camera_director.RestPosition().z);
        return Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance * cam.aspect;
    }

    public static float LeftX()
    {
        Camera cam = Camera.main;
        float center = cam != null ? Camera_director.RestPosition().x : 0f;
        return center - ViewHalfWidth();
    }

    public static float RightX()
    {
        Camera cam = Camera.main;
        float center = cam != null ? Camera_director.RestPosition().x : 0f;
        return center + ViewHalfWidth();
    }

    public static float TopY()
    {
        Camera cam = Camera.main;
        if (cam == null) {
            return 5f;
        }

        return Camera_director.RestPosition().y + Camera_director.RestSize();
    }
}
