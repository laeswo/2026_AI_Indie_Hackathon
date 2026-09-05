using System.Collections.Generic;
using UnityEngine;

// 용사 플립북. Resources/Player 의 clips.txt(Run · jump · Dead)를 읽어 코드로 돌린다. Animator 없이.
//   Run   : 기본. 배경이 흐르니 서 있어도 달리는 그림이 맞다. 반복
//   jump  : 공중에 뜬 동안. 한 번 돌고 마지막 프레임에 머물다 착지하면 Run 으로
//   Dead  : 죽으면. 한 번 돌고 마지막 프레임에 머문다
//   승리로 끝나면 Run 첫 프레임에 멈춰 선다
//
// 크기: Run 의 가장 큰 프레임 높이가 target_height(씬에서 잰 지금 그림 높이). 나머지 프레임은 같은 PPU 라 비율대로.
// 발: 모든 프레임의 밑변이 transform.y + foot_offset 에 오도록 피벗을 잡는다. 그래서 프레임 높이가 달라도 발이 땅에 붙어 있고,
//     Player.FootY() 로 하는 피격 판정과 그림이 어긋나지 않는다. transform·콜라이더는 건드리지 않는다.
// 상태는 Player_jump.is_grounded 와 Player_health.hp 를 매 프레임 보고 정한다. 다른 스크립트를 고칠 필요가 없다.
public class Player_animation : MonoBehaviour
{

    const string folder = "Player";
    const string run_clip = "Run";
    const string jump_clip = "jump";
    const string dead_clip = "Dead";

    class Clip
    {
        public string name;
        public float fps;
        public Sprite[] frames;
    }

    Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
    SpriteRenderer sprite_renderer;
    Player_jump jump;
    Player_health health;

    Clip current;
    float timer;
    int index;
    bool loop;
    bool frozen;    // 승리로 끝났을 때. 첫 프레임에 멈춘다

    // root 에 붙인다. 클립이 하나도 없으면 안 붙이고 null.
    public static Player_animation Attach(GameObject root, SpriteRenderer renderer, float target_height, float foot_offset)
    {
        if (root == null || renderer == null) {
            return null;
        }

        List<Clip_library.Clip> raws = Clip_library.Read(folder);
        if (raws.Count == 0) {
            return null;
        }

        // Run 의 가장 큰 프레임이 target_height. Run 이 없으면 전체에서 가장 큰 프레임.
        float tallest = 0f;
        Clip_library.Clip run = Clip_library.Find(raws, run_clip);
        List<Clip_library.Clip> reference = run != null ? new List<Clip_library.Clip> { run } : raws;
        foreach (Clip_library.Clip raw in reference) {
            foreach (Sprite sprite in raw.frames) {
                tallest = Mathf.Max(tallest, sprite.rect.height);
            }
        }
        if (tallest <= 0f || target_height <= 0f) {
            return null;
        }
        float pixels_per_unit = tallest / target_height;

        Player_animation animation = root.GetComponent<Player_animation>();
        if (animation == null) {
            animation = root.AddComponent<Player_animation>();
        }

        animation.sprite_renderer = renderer;
        animation.jump = root.GetComponent<Player_jump>();
        animation.health = root.GetComponent<Player_health>();
        animation.clips.Clear();

        foreach (Clip_library.Clip raw in raws) {
            Clip clip = new Clip();
            clip.name = raw.name;
            clip.fps = raw.fps;
            clip.frames = new Sprite[raw.frames.Count];

            for (int i = 0; i < raw.frames.Count; i++) {
                Sprite source = raw.frames[i];

                // 밑변이 foot_offset 에 오게. pivot.y 는 "밑변에서 transform 까지" 를 프레임 높이로 나눈 값.
                float frame_height = source.rect.height / pixels_per_unit;
                float pivot_y = frame_height > 0f ? -foot_offset / frame_height : 0.5f;

                Sprite rebuilt = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, pivot_y), pixels_per_unit);
                rebuilt.name = source.name;
                clip.frames[i] = rebuilt;
            }

            animation.clips[clip.name] = clip;
        }

        animation.Play(run_clip, true);

        Debug.Log("용사 애니메이션: 클립 " + animation.clips.Count + "개, 높이 " + target_height.ToString("0.0"));
        return animation;
    }

    Clip FindClip(string name)
    {
        foreach (KeyValuePair<string, Clip> pair in clips) {
            if (string.Equals(pair.Key, name, System.StringComparison.OrdinalIgnoreCase)) {
                return pair.Value;
            }
        }
        return null;
    }

    // 클립을 처음부터. 이미 그 클립이면 그대로 둔다. 없는 클립이면 아무것도 안 한다.
    void Play(string name, bool loop_it)
    {
        Clip clip = FindClip(name);
        if (clip == null || clip == current) {
            return;
        }

        current = clip;
        loop = loop_it;
        timer = 0f;
        index = 0;
        sprite_renderer.sprite = clip.frames[0];
    }

    void Update()
    {
        if (sprite_renderer == null) {
            return;
        }

        ChooseClip();

        if (current == null || frozen) {
            return;
        }

        int last = current.frames.Length - 1;
        if (!loop && index >= last) {
            return;   // 한 번짜리는 마지막 프레임에 머문다
        }

        // 히트스톱·슬로우모션이면 같이 느려지는 게 맞으므로 deltaTime.
        timer += Time.deltaTime * current.fps;
        if (timer < 1f) {
            return;
        }

        int steps = Mathf.FloorToInt(timer);
        timer -= steps;

        if (loop) {
            index = (index + steps) % current.frames.Length;
        }
        else {
            index = Mathf.Min(last, index + steps);
        }
        sprite_renderer.sprite = current.frames[index];
    }

    // 지금 상황에 맞는 클립. 죽음 > 게임 끝(승리) > 공중 > 달리기.
    void ChooseClip()
    {
        bool dead = Game_flow.is_over && health != null && health.hp <= 0;
        if (dead) {
            frozen = false;
            Play(dead_clip, false);
            return;
        }

        if (Game_flow.is_over) {
            // 이겼다. 달리기 첫 프레임에 서서 멈춘다.
            Play(run_clip, true);
            if (!frozen) {
                frozen = true;
                index = 0;
                sprite_renderer.sprite = current.frames[0];
            }
            return;
        }

        frozen = false;

        if (jump != null && !jump.is_grounded) {
            Play(jump_clip, false);
            return;
        }

        Play(run_clip, true);
    }
}
