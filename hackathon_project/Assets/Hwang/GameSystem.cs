using UnityEngine;

public class GameSystem : MonoBehaviour
{

    public float time_limit = 30f;

    float time_left;
    int last_logged_second;
    bool is_running;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        time_left = time_limit;
        last_logged_second = Mathf.CeilToInt(time_left);
        is_running = true;

        Debug.Log("게임 시작 - 남은 시간 " + last_logged_second + "초");
    }

    // Update is called once per frame
    void Update()
    {
        if (!is_running) {
            return;
        }

        time_left -= Time.deltaTime;

        if (time_left <= 0f) {
            time_left = 0f;
            is_running = false;
            Debug.Log("시간 종료");
            return;
        }

        // 매 프레임 찍으면 콘솔이 초당 수백 줄로 넘치므로 초가 바뀔 때만 남긴다.
        int second = Mathf.CeilToInt(time_left);
        if (second != last_logged_second) {
            last_logged_second = second;
            Debug.Log("남은 시간 " + second + "초");
        }
    }
}
