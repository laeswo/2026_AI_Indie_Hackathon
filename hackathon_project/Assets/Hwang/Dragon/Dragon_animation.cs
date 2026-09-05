using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// 드래곤 플립북 애니메이션. Resources/dragon 의 그림들을 번호 순으로 돌려 보여준다.
// Animator 나 .anim 에셋 없이 코드로 돈다. Dragon.Awake 가 붙이고, 상태에 따라 SetSpeed 로 빠르기를 바꾼다.
//
// 프레임 크기가 제각각(194~459px)이라 그대로 쓰면 프레임마다 몸이 커졌다 작아진다.
// 그래서 텍스처를 새 스프라이트로 다시 만들면서 PPU 를 하나로 통일한다: 가장 큰 프레임의 높이가 target_height 유닛이 되게.
// 그러면 프레임끼리의 크기 비율(날개 펼침 등)은 그대로 살고, 전체 크기는 씬에 놓인 드래곤 크기를 따른다.
// transform 은 건드리지 않으므로 콜라이더·BodyRadius 는 영향 없다.
public class Dragon_animation : MonoBehaviour
{

    const string resource_folder = "dragon";

    public float fps = 6f;

    SpriteRenderer sprite_renderer;
    Sprite[] frames;
    float timer;
    int index;
    float speed_scale = 1f;

    // renderer 에 프레임을 돌린다. Resources 에 그림이 없으면 아무것도 안 붙이고 null.
    public static Dragon_animation Attach(SpriteRenderer renderer, float target_height)
    {
        if (renderer == null) {
            return null;
        }

        Sprite[] loaded = Resources.LoadAll<Sprite>(resource_folder);
        if (loaded == null || loaded.Length == 0) {
            return null;
        }

        Sprite[] frames = Rebuild(loaded, target_height);
        if (frames.Length == 0) {
            return null;
        }

        Dragon_animation animation = renderer.gameObject.GetComponent<Dragon_animation>();
        if (animation == null) {
            animation = renderer.gameObject.AddComponent<Dragon_animation>();
        }

        animation.sprite_renderer = renderer;
        animation.frames = frames;
        animation.index = 0;
        renderer.sprite = frames[0];

        Debug.Log("드래곤 애니메이션: 프레임 " + frames.Length + "장, 높이 " + target_height.ToString("0.0"));
        return animation;
    }

    // 상태에 따라 빠르기. 1 이 기본, 예고 1.6, 포효 2.
    public void SetSpeed(float multiplier)
    {
        speed_scale = Mathf.Max(0f, multiplier);
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

        float tallest = 0f;
        foreach (Sprite sprite in ordered) {
            tallest = Mathf.Max(tallest, sprite.rect.height);
        }
        if (tallest <= 0f || target_height <= 0f) {
            return ordered.ToArray();
        }

        // 가장 큰 프레임이 target_height 유닛. 나머지는 같은 PPU 라 비율대로 작다.
        float pixels_per_unit = tallest / target_height;

        Sprite[] result = new Sprite[ordered.Count];
        for (int i = 0; i < ordered.Count; i++) {
            Sprite source = ordered[i];
            result[i] = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0.5f), pixels_per_unit);
            result[i].name = source.texture.name;
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
        if (frames == null || frames.Length < 2 || sprite_renderer == null || fps <= 0f) {
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
}
