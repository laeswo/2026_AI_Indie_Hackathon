using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴 Hwang › 드래곤 프리팹 슬롯 채우기. 스크립트가 다시 로드될 때도 한 번 자동으로 돈다(바꿀 게 없으면 조용히 끝난다).
// 열려 있는 씬의 Dragon 에서 비어 있는 프리팹 슬롯을 Assets/Hwang/Dragon_prefab 의 프리팹으로 채우고 씬을 dirty 로 표시한다.
// 이미 들어 있는 슬롯은 건드리지 않는다. 씬 파일을 손으로 고치지 않고 Unity 를 통해 바꾸므로 저장(Ctrl+S)하면 남는다.
public static class Dragon_prefab_tool
{

    const string prefab_folder = "Assets/Hwang/Dragon_prefab/";

    struct Slot
    {
        public string field;        // Dragon 의 public 필드 이름
        public string prefab;       // Dragon_prefab 안의 프리팹 이름
    }

    static readonly Slot[] slots = {
        new Slot { field = "final_beam_prefab", prefab = "breath_two_last" },   // 마지막 레이저 몸통(띠)
        new Slot { field = "final_core_prefab", prefab = "breath" },            // 마지막 레이저 심(세모)
        new Slot { field = "breath_floor_prefab", prefab = "breath_two" },      // 브레스 바닥 불
        new Slot { field = "ground_prefab", prefab = "ground" },                // 내려찍기 땅 조각
        new Slot { field = "eat_fireball_prefab", prefab = "eat_fireball" },    // 큰 화염구
    };

    [InitializeOnLoadMethod]
    static void AutoApply()
    {
        EditorApplication.delayCall += () => {
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                return;
            }
            Apply(false);
        };
    }

    [MenuItem("Hwang/드래곤 프리팹 슬롯 채우기")]
    public static void ApplyFromMenu()
    {
        Apply(true);
    }

    static void Apply(bool verbose)
    {
        Dragon dragon = Object.FindFirstObjectByType<Dragon>();
        if (dragon == null) {
            if (verbose) {
                Debug.Log("드래곤 프리팹 슬롯: 열린 씬에 Dragon 이 없습니다.");
            }
            return;
        }

        SerializedObject serialized = new SerializedObject(dragon);
        int changed = 0;

        foreach (Slot slot in slots) {
            SerializedProperty property = serialized.FindProperty(slot.field);
            if (property == null || property.objectReferenceValue != null) {
                continue;
            }

            string path = prefab_folder + slot.prefab + ".prefab";
            Object prefab = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (prefab == null) {
                if (verbose) {
                    Debug.LogWarning("드래곤 프리팹 슬롯: " + path + " 이 없어서 " + slot.field + " 을 비워 둡니다.");
                }
                continue;
            }

            // 필드 타입에 맞게. Fireball 슬롯은 컴포넌트, 나머지는 GameObject.
            if (slot.field == "eat_fireball_prefab") {
                Fireball fireball = (prefab as GameObject) != null ? (prefab as GameObject).GetComponent<Fireball>() : null;
                if (fireball == null) {
                    continue;
                }
                property.objectReferenceValue = fireball;
            }
            else {
                property.objectReferenceValue = prefab;
            }

            changed++;
            Debug.Log("드래곤 프리팹 슬롯: " + slot.field + " ← " + slot.prefab);
        }

        if (changed == 0) {
            if (verbose) {
                Debug.Log("드래곤 프리팹 슬롯: 비어 있는 슬롯이 없습니다.");
            }
            return;
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(dragon);
        EditorSceneManager.MarkSceneDirty(dragon.gameObject.scene);
        Debug.Log("드래곤 프리팹 슬롯: " + changed + "개 채움. 씬을 저장(Ctrl+S)하면 남습니다.");
    }
}
