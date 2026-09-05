using System.Collections.Generic;
using UnityEngine;

// Resources/<폴더>/clips.txt 를 읽어 클립(이름 · fps · 프레임 스프라이트 목록)으로 돌려준다.
// clips.txt 는 에디터 도구(Dragon_clip_tool)가 같은 폴더의 .anim 에서 굽는다. 실행 중에는 .anim 의 스프라이트 키를 못 읽어서다.
// 형식: 한 줄에 "클립이름 fps 프레임이름 프레임이름 …". # 으로 시작하면 주석.
// 프레임은 같은 폴더(하위 폴더 포함)의 스프라이트를 이름으로 찾는다. 원본 그대로 돌려주므로 크기·피벗은 부른 쪽이 맞춘다.
public static class Clip_library
{

    public class Clip
    {
        public string name;
        public float fps;
        public List<Sprite> frames;
    }

    static readonly HashSet<string> warned = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        warned.Clear();
    }

    // 폴더의 클립 전부. 파일이나 그림이 없으면 빈 목록.
    public static List<Clip> Read(string folder)
    {
        List<Clip> result = new List<Clip>();

        TextAsset list = Resources.Load<TextAsset>(folder + "/clips");
        if (list == null) {
            return result;
        }

        Sprite[] sheet = Resources.LoadAll<Sprite>(folder);
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

            string[] parts = line.Split(' ');
            if (parts.Length < 3) {
                continue;
            }

            float fps;
            if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fps) || fps <= 0f) {
                fps = 12f;
            }

            List<Sprite> frames = new List<Sprite>();
            for (int i = 2; i < parts.Length; i++) {
                Sprite sprite;
                if (by_name.TryGetValue(parts[i], out sprite)) {
                    frames.Add(sprite);
                }
                else {
                    Warn("클립 " + parts[0] + " 의 프레임 " + parts[i] + " 을 Resources/" + folder + " 에서 찾지 못했습니다. 시트를 다시 잘랐으면 메뉴 Hwang › 애니메이션 클립 굽기.");
                }
            }

            if (frames.Count == 0) {
                continue;
            }

            Clip clip = new Clip();
            clip.name = parts[0];
            clip.fps = fps;
            clip.frames = frames;
            result.Add(clip);
        }

        return result;
    }

    // 이름으로 하나. 대소문자 무시. 없으면 null.
    public static Clip Find(List<Clip> clips, string name)
    {
        foreach (Clip clip in clips) {
            if (string.Equals(clip.name, name, System.StringComparison.OrdinalIgnoreCase)) {
                return clip;
            }
        }
        return null;
    }

    static void Warn(string message)
    {
        if (warned.Add(message)) {
            Debug.LogWarning("Clip_library : " + message);
        }
    }
}
