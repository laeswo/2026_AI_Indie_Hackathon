using UnityEngine;
using UnityEngine.SceneManagement;

// BGM. 씬이 바뀌면 알아서 고른다: StartScene 이면 main_bgm, 그 외(전투)는 battle_bgm. 반복 재생.
// 씬을 넘나들어도 하나만 살아 있고(DontDestroyOnLoad), 같은 곡이면 다시 시작하지 않는다.
// 게임이 끝나면(Game_flow.End) 멈춘다. 결과 징글은 Sound_bank 가 따로 튼다.
// 시작 메뉴가 게임 씬 안 오버레이로 뜨는 경우는 Start_menu 가 PlayMain / PlayBattle 을 직접 부른다.
public class Music_player : MonoBehaviour
{

    const string start_scene = "StartScene";
    public const float music_volume = 0.45f;

    static Music_player instance;
    static bool hooked;

    AudioSource source;
    string current_id;

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        instance = null;
        hooked = false;
    }

    // 첫 씬이 뜬 직후 한 번. 이후 씬 전환은 sceneLoaded 로 따라간다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Ensure();

        if (!hooked) {
            SceneManager.sceneLoaded += OnSceneLoaded;
            hooked = true;
        }

        PlayForScene(SceneManager.GetActiveScene().name);
    }

    static Music_player Ensure()
    {
        if (instance != null) {
            return instance;
        }

        GameObject holder = new GameObject("Music_player");
        Object.DontDestroyOnLoad(holder);

        instance = holder.AddComponent<Music_player>();
        instance.source = holder.AddComponent<AudioSource>();
        instance.source.loop = true;
        instance.source.playOnAwake = false;
        instance.source.spatialBlend = 0f;
        instance.source.volume = music_volume;

        return instance;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayForScene(scene.name);
    }

    public static void PlayForScene(string scene_name)
    {
        Play(scene_name == start_scene ? "main_bgm" : "battle_bgm");
    }

    public static void PlayMain()
    {
        Play("main_bgm");
    }

    public static void PlayBattle()
    {
        Play("battle_bgm");
    }

    // Sound_bank 의 id 로 튼다. 이미 그 곡이 돌고 있으면 그대로 둔다.
    public static void Play(string id)
    {
        Music_player player = Ensure();

        if (player.current_id == id && player.source.isPlaying) {
            return;
        }

        AudioClip clip = Sound_bank.Get(id);
        if (clip == null) {
            player.source.Stop();
            player.current_id = null;
            return;
        }

        player.source.clip = clip;
        player.source.volume = music_volume;
        player.source.Play();
        player.current_id = id;
    }

    public static void Stop()
    {
        if (instance == null) {
            return;
        }

        instance.source.Stop();
        instance.current_id = null;
    }
}
