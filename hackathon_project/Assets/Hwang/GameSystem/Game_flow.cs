using UnityEngine;
using UnityEngine.SceneManagement;

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

    // 씬을 다시 불러올 때 부른다. 도메인 리로드가 없어도 새 판이 깨끗하게 시작되게.
    public static void ResetForNewGame()
    {
        is_over = false;
        result = null;
        Time.timeScale = 1f;
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

    // 결과 화면에서 "다시". 같은 씬을 다시 불러온다. R 키와 HUD 버튼이 둘 다 이걸 부른다.
    public static void Restart()
    {
        ResetForNewGame();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // 결과 화면에서 "시작 화면". Esc 키와 HUD 버튼이 둘 다 이걸 부른다.
    public static void GoToStart()
    {
        ResetForNewGame();

        // 시작 화면에 갔다 오면 인트로와 튜토리얼 안내가 다시 나온다. R 재시작(Restart)에서는 유지.
        Tutorial.ResetAll();

        SceneManager.LoadScene("StartScene");
    }
}
