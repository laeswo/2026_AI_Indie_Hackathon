using System.Collections.Generic;

// 튜토리얼 진행 상태. 어떤 안내를 이미 보여줬는지 한 곳에서 기억한다.
//   seen_intro   인트로(전체화면 대사)를 봤는지. R 재시작에서는 유지, Esc 로 시작 화면에 갔다 오면 다시 나온다
//   shown        토스트로 보여준 id 들. 마찬가지로 시작 화면에 갔다 올 때만 초기화
//
// Fire(id) 는 아직 안 보여준 id 면 Dialogue_table 의 대사를 토스트로 띄운다. 훅은 Player / Dragon 에 있다.
public static class Tutorial
{

    public static bool seen_intro;

    static readonly HashSet<string> shown = new HashSet<string>();

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        ResetAll();
    }

    // 시작 화면으로 돌아갈 때. 다음 판에서는 인트로와 안내가 전부 다시 나온다.
    public static void ResetAll()
    {
        seen_intro = false;
        shown.Clear();
    }

    // 한 판에 한 번만. 이미 보여준 id 면 아무것도 안 한다.
    public static void Fire(string id)
    {
        if (string.IsNullOrEmpty(id) || shown.Contains(id)) {
            return;
        }

        string line = Dialogue_table.Line(id);
        if (line == null) {
            return;
        }

        shown.Add(id);
        Toast.Show(line);
    }
}
