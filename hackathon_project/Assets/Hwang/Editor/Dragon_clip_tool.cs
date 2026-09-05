using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 메뉴 Hwang › 애니메이션 클립 굽기. 스크립트가 다시 로드될 때도 한 번 자동으로 돈다(바뀐 게 없으면 조용히 끝난다).
//
// 드래곤·용사는 Animator 없이 코드 플립북으로 돈다. 그런데 브레스·점프 같은 동작은 사람이 Unity 에서 .anim 으로 만드는 게 편하다.
// 문제는 .anim 의 스프라이트 키를 실행 중에는 못 읽는다는 것(AnimationUtility 는 에디터 전용).
// 그래서 여기서 미리 읽어 텍스트로 구워 두고, 실행 중엔 Clip_library 가 그걸 읽는다.
//
// 폴더마다: Resources/<폴더>/*.anim (SpriteRenderer.m_Sprite 키만 본다) → Resources/<폴더>/clips.txt
// 한 줄에 "클립이름 fps 프레임이름 프레임이름 …". 클립 이름은 .anim 파일 이름, 프레임 이름은 시트 조각 이름.
public static class Dragon_clip_tool
{

    const string resources_folder = "Assets/Hwang/Resources/";

    // 클립을 굽는 폴더들. 새 캐릭터가 생기면 여기 추가.
    static readonly string[] clip_folders = { "Dragon_Breath", "Player", "fireball" };

    [InitializeOnLoadMethod]
    static void AutoBake()
    {
        EditorApplication.delayCall += () => {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                return;
            }
            BakeAll(false);
        };
    }

    [MenuItem("Hwang/애니메이션 클립 굽기")]
    public static void BakeFromMenu()
    {
        BakeAll(true);
    }

    static void BakeAll(bool verbose)
    {
        foreach (string folder in clip_folders) {
            Bake(resources_folder + folder, verbose);
        }
    }

    static void Bake(string clip_folder, bool verbose)
    {
        if (!Directory.Exists(clip_folder)) {
            if (verbose) {
                Debug.LogWarning("애니메이션 클립: " + clip_folder + " 폴더가 없습니다.");
            }
            return;
        }

        string output_path = clip_folder + "/clips.txt";
        string folder_name = Path.GetFileName(clip_folder);

        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { clip_folder });
        List<string> lines = new List<string>();
        List<string> names = new List<string>();

        foreach (string guid in guids) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) {
                continue;
            }

            List<string> frames = ReadSpriteFrames(clip);
            if (frames.Count == 0) {
                Debug.LogWarning("애니메이션 클립: " + path + " 에 스프라이트 키가 없어서 건너뜁니다. SpriteRenderer.Sprite 를 키로 넣은 클립이어야 합니다.");
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            lines.Add(name + " " + clip.frameRate.ToString("0.###") + " " + string.Join(" ", frames));
            names.Add(name + "(" + frames.Count + ")");
        }

        if (lines.Count == 0) {
            if (verbose) {
                Debug.Log("애니메이션 클립: " + folder_name + " 에 .anim 이 없습니다.");
            }
            return;
        }

        // 순서가 흔들려 매번 파일이 바뀌지 않게 이름순.
        lines.Sort(System.StringComparer.Ordinal);

        StringBuilder builder = new StringBuilder();
        builder.Append("# 자동 생성. Dragon_clip_tool 이 " + folder_name + "/*.anim 에서 만든다. 손으로 고치지 말고 .anim 을 고친 뒤 메뉴 Hwang › 애니메이션 클립 굽기.\n");
        builder.Append("# 형식: 클립이름 fps 프레임이름 프레임이름 …\n");
        foreach (string line in lines) {
            builder.Append(line).Append('\n');
        }
        string content = builder.ToString();

        string existing = File.Exists(output_path) ? File.ReadAllText(output_path, Encoding.UTF8) : null;
        if (existing == content) {
            if (verbose) {
                Debug.Log("애니메이션 클립: " + folder_name + " 바뀐 게 없습니다. (" + string.Join(", ", names) + ")");
            }
            return;
        }

        File.WriteAllText(output_path, content, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(output_path);
        Debug.Log("애니메이션 클립: " + folder_name + "/clips.txt 갱신 — " + string.Join(", ", names));
    }

    // 클립에서 SpriteRenderer.m_Sprite 키를 시간순으로 읽어 스프라이트 이름 목록으로.
    static List<string> ReadSpriteFrames(AnimationClip clip)
    {
        List<string> frames = new List<string>();

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") {
                continue;
            }

            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            System.Array.Sort(keys, (a, b) => a.time.CompareTo(b.time));

            foreach (ObjectReferenceKeyframe key in keys) {
                Sprite sprite = key.value as Sprite;
                if (sprite != null) {
                    frames.Add(sprite.name);
                }
            }

            // 바인딩이 여럿(자식 경로 등)이어도 첫 번째만 쓴다. 렌더러가 하나다.
            break;
        }

        return frames;
    }
}
