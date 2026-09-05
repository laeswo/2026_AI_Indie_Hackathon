using UnityEngine;

// 게임이 끝났는지 한 곳에서 관리한다. 끝나면 세계도 같이 멈춘다.
public static class Game_flow
{

    public static bool is_over { get; private set; }
    public static string result { get; private set; }

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        is_over = false;
        result = null;
    }

    public static void End(string message)
    {
        if (is_over) {
            return;
        }

        is_over = true;
        result = message;
        World_scroll.Get().is_running = false;

        Debug.Log(message);
    }
}
