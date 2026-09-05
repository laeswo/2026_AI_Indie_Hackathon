using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// 드래곤 플립북 애니메이션. Resources/dragon 의 그림들을 번호 순으로 돌려 보여준다(날갯짓).
// Animator 나 .anim 에셋 없이 코드로 돈다. Dragon.Awake 가 붙이고, 상태에 따라 SetSpeed 로 빠르기를 바꾼다.
//
// 프레임 크기가 제각각(194~459px)이라 그대로 쓰면 프레임마다 몸이 커졌다 작아진다.
// 그래서 텍스처를 새 스프라이트로 다시 만들면서 PPU 를 하나로 통일한다: 가장 큰 프레임의 높이가 target_height 유닛이 되게.
// 그러면 프레임끼리의 크기 비율(날개 펼침 등)은 그대로 살고, 전체 크기는 씬에 놓인 드래곤 크기를 따른다.
// transform 은 건드리지 않으므로 콜라이더·BodyRadius 는 영향 없다.
//
// 한 번 재생 클립(브레스·사망): 사람이 만든 .anim 을 Dragon_clip_tool 이 Resources/Dragon_Breath/clips.txt 로 구워 두면
// 여기서 읽어 같은 방식으로 PPU 를 맞춘 뒤 PlayClip 으로 돌린다. 재생 중엔 날갯짓을 멈추고, 끝나면 돌아오거나 마지막 프레임을 유지한다.
public class Dragon_animation : MonoBehaviour
{

    const string resource_folder = "dragon";
    const string clip_folder = "Dragon_Breath";
    const string clip_list = "Dragon_Breath/clips";

    public float fps = 6f;

    SpriteRenderer sprite_renderer;
    Sprite[] frames;
    float timer;
    int index;
    float speed_scale = 1f;

    // 한 번 재생 클립.
    class Clip
    {
        public string name;
        public float fps;
        public Sprite[] frames;
    }

    Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
    Clip playing;
    float clip_timer;
    int clip_index;
    bool clip_hold_last;

    public bool is_playing_clip
    {
        get { return playing != null; }
    }

    // renderer 에 프레임을 돌린다. Resources 에 날갯짓 그림도 클립도 없으면 아무것도 안 붙이고 null.
    public static Dragon_animation Attach(SpriteRenderer renderer, float target_height)
    {
        if (renderer == null) {
            return null;
        }

        Sprite[] loaded = Resources.LoadAll<Sprite>(resource_folder);
        Sprite[] frames = loaded != null && loaded.Length > 0 ? Rebuild(loaded, target_height) : new Sprite[0];

        Dictionary<string, Clip> clips = LoadClips(target_height);

        if (frames.Length == 0 && clips.Count == 0) {
            return null;
        }

        Dragon_animation animation = renderer.gameObject.GetComponent<Dragon_animation>();
        if (animation == null) {
            animation = renderer.gameObject.AddComponent<Dragon_animation>();
        }

        animation.sprite_renderer = renderer;
        animation.frames = frames;
        animation.clips = clips;
        animation.index = 0;

        if (frames.Length > 0) {
            renderer.sprite = frames[0];
        }

        Debug.Log("드래곤 애니메이션: 날갯짓 " + frames.Length + "장, 클립 " + clips.Count + "개, 높이 " + target_height.ToString("0.0"));
        return animation;
    }

    // 상태에 따라 빠르기. 1 이 기본, 예고 1.6, 포효 2.
    public void SetSpeed(float multiplier)
    {
        speed_scale = Mathf.Max(0f, multiplier);
    }

    // 클립을 처음부터 한 번 돌린다. hold_last 면 끝나고도 마지막 프레임에 머문다(StopClip 까지). 아니면 끝나면 날갯짓으로 돌아온다.
    // 이름에 맞는 클립이 없으면 false. 대소문자는 구분하지 않는다.
    public bool PlayClip(string name, bool hold_last)
    {
        Clip clip = FindClip(name);
        if (clip == null) {
            return false;
        }

        playing = clip;
        clip_hold_last = hold_last;
        clip_timer = 0f;
        clip_index = 0;

        if (sprite_renderer != null) {
            sprite_renderer.sprite = clip.frames[0];
        }
        return true;
    }

    // 클립을 끊고 날갯짓으로 돌아온다. 재생 중이 아니면 아무것도 안 한다.
    public void StopClip()
    {
        if (playing == null) {
            return;
        }

        playing = null;

        if (sprite_renderer != null && frames != null && frames.Length > 0) {
            sprite_renderer.sprite = frames[index % frames.Length];
        }
    }

    Clip FindClip(string name)
    {
        if (string.IsNullOrEmpty(name)) {
            return null;
        }

        foreach (KeyValuePair<string, Clip> pair in clips) {
            if (string.Equals(pair.Key, name, System.StringComparison.OrdinalIgnoreCase)) {
                return pair.Value;
            }
        }
        return null;
    }

    // clips.txt 의 클립 하나를 원본 스프라이트 그대로 읽은 것. 드래곤은 Normalize 를 거쳐 쓰고, 바닥 불 같은 다른 곳은 그대로 쓴다.
    class Raw_clip
    {
        public string name;
        public float fps;
        public List<Sprite> frames;
    }

    // clips.txt 를 읽어 클립마다 프레임을 PPU 통일해 만든다. 파일이나 그림이 없으면 빈 목록.
    static Dictionary<string, Clip> LoadClips(float target_height)
    {
        Dictionary<string, Clip> result = new Dictionary<string, Clip>();

        foreach (Raw_clip raw in ReadRawClips()) {
            Clip clip = new Clip();
            clip.name = raw.name;
            clip.fps = raw.fps;
            clip.frames = Normalize(raw.frames, target_height);
            result[clip.name] = clip;
        }

        return result;
    }

    // 다른 곳(브레스 바닥 불 등)에서 clips.txt 의 클립을 원본 크기 그대로 쓸 때. 이름은 대소문자 무시. 없으면 null 이고 fps 는 12.
    public static Sprite[] LoadClipFrames(string clip_name, out float fps)
    {
        fps = 12f;

        foreach (Raw_clip raw in ReadRawClips()) {
            if (string.Equals(raw.name, clip_name, System.StringComparison.OrdinalIgnoreCase)) {
                fps = raw.fps;
                return raw.frames.ToArray();
            }
        }

        return null;
    }

    // clips.txt 를 읽어 클립 이름·fps·프레임(Resources/Dragon_Breath 의 조각을 이름으로 찾음)을 그대로 모은다. 하위 폴더도 같이 읽힌다.
    static List<Raw_clip> ReadRawClips()
    {
        List<Raw_clip> result = new List<Raw_clip>();

        TextAsset list = Resources.Load<TextAsset>(clip_list);
        if (list == null) {
            return result;
        }

        Sprite[] sheet = Resources.LoadAll<Sprite>(clip_folder);
        Dictionary<string, Sprite> by_name = new Dictionary<string, Sprite>();
        foreach (Sprite sprite in sheet) {
            if (sprite != null && !by_name.ContainsKey(sprite.name)) {
                by_name[sprite.name] = sprite;
            }
        }

        foreach (string raw in list.text.Split('\n')) {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) {
                continue;
            }

            // 클립이름 fps 프레임 프레임 …
            string[] parts = line.Split(' ');
            if (parts.Length < 3) {
                continue;
            }

            float fps;
            if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fps) || fps <= 0f) {
                fps = 12f;
            }

            List<Sprite> ordered = new List<Sprite>();
            for (int i = 2; i < parts.Length; i++) {
                Sprite sprite;
                if (by_name.TryGetValue(parts[i], out sprite)) {
                    ordered.Add(sprite);
                }
                else {
                    Debug.LogWarning("드래곤 애니메이션: 클립 " + parts[0] + " 의 프레임 " + parts[i] + " 을 Resources/" + clip_folder + " 에서 찾지 못했습니다. 시트를 다시 잘랐으면 메뉴 Hwang › 드래곤 애니메이션 클립 굽기.");
                }
            }

            if (ordered.Count == 0) {
                continue;
            }

            Raw_clip entry = new Raw_clip();
            entry.name = parts[0];
            entry.fps = fps;
            entry.frames = ordered;
            result.Add(entry);
        }

        return result;
    }

    // 파일 이름의 숫자로 정렬하고(dragon_1, dragon_2, dragon3 …), PPU 를 통일한 새 스프라이트로 만든다.
    static Sprite[] Rebuild(Sprite[] loaded, float target_height)
    {
        // Multiple 모드는 한 텍스처에 조각이 여럿일 수 있다. 텍스처당 첫 조각만 쓴다.
        Dictionary<Texture2D, Sprite> per_texture = new Dictionary<Texture2D, Sprite>();
        foreach (Sprite sprite in loaded) {
            if (sprite == null || sprite.texture == null || per_texture.ContainsKey(sprite.texture)) {
                continue;
            }
            per_texture[sprite.texture] = sprite;
        }

        List<Sprite> ordered = new List<Sprite>(per_texture.Values);
        ordered.Sort((a, b) => FrameNumber(a.texture.name).CompareTo(FrameNumber(b.texture.name)));

        return Normalize(ordered, target_height);
    }

    // 순서가 정해진 프레임들을 같은 PPU 로 다시 만든다. 가장 큰 프레임이 target_height 유닛, 나머지는 비율대로 작다.
    static Sprite[] Normalize(List<Sprite> ordered, float target_height)
    {
        float tallest = 0f;
        foreach (Sprite sprite in ordered) {
            tallest = Mathf.Max(tallest, sprite.rect.height);
        }
        if (tallest <= 0f || target_height <= 0f) {
            return ordered.ToArray();
        }

        float pixels_per_unit = tallest / target_height;

        Sprite[] result = new Sprite[ordered.Count];
        for (int i = 0; i < ordered.Count; i++) {
            Sprite source = ordered[i];
            result[i] = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0.5f), pixels_per_unit);
            result[i].name = source.name;
        }

        return result;
    }

    static int FrameNumber(string name)
    {
        Match match = Regex.Match(name, @"(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
    }

    void Update()
    {
        if (sprite_renderer == null) {
            return;
        }

        if (playing != null) {
            TickClip();
            return;
        }

        if (frames == null || frames.Length < 2 || fps <= 0f) {
            return;
        }

        // 히트스톱·슬로우모션이면 같이 느려지는 게 맞으므로 deltaTime.
        timer += Time.deltaTime * fps * speed_scale;
        if (timer < 1f) {
            return;
        }

        int steps = Mathf.FloorToInt(timer);
        timer -= steps;
        index = (index + steps) % frames.Length;
        sprite_renderer.sprite = frames[index];
    }

    // 클립은 상태 빠르기(speed_scale)와 무관하게 만든 fps 그대로. 히트스톱·슬로우모션에는 같이 느려진다.
    void TickClip()
    {
        int last = playing.frames.Length - 1;

        if (clip_index >= last) {
            // 끝. 머무르거나 날갯짓으로.
            if (!clip_hold_last) {
                StopClip();
            }
            return;
        }

        clip_timer += Time.deltaTime * playing.fps;
        if (clip_timer < 1f) {
            return;
        }

        int steps = Mathf.FloorToInt(clip_timer);
        clip_timer -= steps;
        clip_index = Mathf.Min(last, clip_index + steps);
        sprite_renderer.sprite = playing.frames[clip_index];
    }
}
