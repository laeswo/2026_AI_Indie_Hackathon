using System.Collections.Generic;
using UnityEngine;

// 한 프리팹이 여러 물건이 되는 아이템 (ob_normal: 돌류, ob_sword: 검·종). 스폰될 때마다 Resources 폴더의 그림 중 하나를 골라
// 그림·이름·줍기 대사를 그 물건으로 바꾼다. 데미지·확률 같은 다른 Crop_data 값은 건드리지 않는다.
//
// 폴더의 png 는 Sprite Mode = Multiple 이라 파일당 조각이 여러 개 나올 수 있다(자동 슬라이스 부스러기 포함).
// 파일(texture)당 가장 큰 조각 하나만 쓴다. 조각 피벗은 Item_art_tool 이 가운데로 고쳐 둔다.
// 파일 이름 → 물건 이름·대사 id 는 아래 표. 표에 없는 파일은 파일 이름을 그대로 이름으로 쓰고 대사는 없다.
// 그래서 Ob_normal/stone.png 를 넣기만 하면 코드 수정 없이 "돌" 이 섞여 나온다.
//
// Crop_flow.Awake 가 그림과 콜라이더 크기를 비교하므로 그보다 먼저 돌아야 한다 (DefaultExecutionOrder -10).
// 같은 물건이 3번 연속 나오지 않게 직전 것을 정적으로 기억한다.
[DefaultExecutionOrder(-10)]
public class Item_variants : MonoBehaviour
{

    public string resource_folder = "Ob_normal";

    const float item_size = 1.5f;           // 모든 아이템은 긴 변이 1.5 유닛. Item_art_tool 과 같은 값

    struct Variant
    {
        public string file_name;
        public string display_name;
        public string dialogue_id;
        public Sprite sprite;
    }

    // 파일 이름 → (표시 이름, 대사 id). 표에 없으면 파일 이름 그대로, 대사 없음.
    static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>
    {
        { "brick",       new[] { "벽돌", "pickup_brick" } },
        { "chair",       new[] { "의자", "pickup_chair" } },
        { "chicken",     new[] { "닭", "pickup_chicken" } },
        { "pot",         new[] { "냄비", "pickup_pot" } },
        { "waterbucket", new[] { "물통", "pickup_water" } },
        { "stone",       new[] { "돌", "pickup_stone" } },

        // Ob_Sword (ob_sword). 검·종
        { "sword",       new[] { "검", "pickup_sword" } },
        { "bell",        new[] { "종", "pickup_bell" } },
    };

    // 폴더별로 한 번만 읽는다. 씬이 바뀌어도 그대로 써도 된다 (Resources 에셋은 남는다).
    static readonly Dictionary<string, List<Variant>> cache = new Dictionary<string, List<Variant>>();
    static readonly HashSet<string> warned_folders = new HashSet<string>();

    // 직전에 고른 물건. 같은 것이 몇 번 연속인지도 같이 세서 3연속을 막는다.
    static string last_name;
    static int same_count;

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        cache.Clear();
        warned_folders.Clear();
        last_name = null;
        same_count = 0;
    }

    void Awake()
    {
        List<Variant> variants = Load(resource_folder);
        if (variants.Count == 0) {
            if (warned_folders.Add(resource_folder)) {
                Debug.LogWarning(name + " : Resources/" + resource_folder + " 에 그림이 없어서 프리팹의 임시 그림을 그대로 씁니다.");
            }
            return;
        }

        Variant chosen = Pick(variants);
        Apply(chosen);
    }

    // 폴더의 스프라이트를 파일당 가장 큰 조각만 모은다. 결과는 파일 이름순.
    static List<Variant> Load(string folder)
    {
        List<Variant> cached;
        if (cache.TryGetValue(folder, out cached)) {
            return cached;
        }

        List<Variant> result = new List<Variant>();
        Dictionary<string, Sprite> first_by_file = new Dictionary<string, Sprite>();

        Sprite[] sprites = Resources.LoadAll<Sprite>(folder);
        foreach (Sprite sprite in sprites) {
            if (sprite == null || sprite.texture == null) {
                continue;
            }

            string file = sprite.texture.name;

            // 같은 파일의 조각이 여러 개면 가장 큰 것. 자동 슬라이스가 남긴 몇 픽셀짜리 부스러기를 피한다.
            Sprite existing;
            if (!first_by_file.TryGetValue(file, out existing) || Area(sprite) > Area(existing)) {
                first_by_file[file] = sprite;
            }
        }

        if (result.Count == 0 && sprites.Length == 0) {
            Debug.LogWarning("Item_variants : Resources.LoadAll<Sprite>(\"" + folder + "\") 결과가 0개입니다. 폴더 이름과 png 의 Texture Type(Sprite) 을 확인해 주세요.");
        }

        List<string> files = new List<string>(first_by_file.Keys);
        files.Sort(string.CompareOrdinal);

        foreach (string file in files) {
            Variant variant = new Variant();
            variant.file_name = file;
            variant.sprite = first_by_file[file];

            string[] entry;
            if (table.TryGetValue(file, out entry)) {
                variant.display_name = entry[0];
                variant.dialogue_id = entry[1];
            }
            else {
                variant.display_name = file;
                variant.dialogue_id = "";
            }

            result.Add(variant);
        }

        cache[folder] = result;
        Debug.Log("Item_variants : " + folder + " 에서 그림 " + result.Count + "종 (" + string.Join(", ", files) + ")");
        return result;
    }

    static float Area(Sprite sprite)
    {
        return sprite.rect.width * sprite.rect.height;
    }

    // 랜덤. 직전 것이 2번 연속이면 이번엔 뺀다. 하나뿐이면 그냥 그것.
    static Variant Pick(List<Variant> variants)
    {
        int index = Random.Range(0, variants.Count);

        if (variants.Count > 1 && same_count >= 2 && variants[index].file_name == last_name) {
            index = (index + Random.Range(1, variants.Count)) % variants.Count;
        }

        Variant chosen = variants[index];

        if (chosen.file_name == last_name) {
            same_count++;
        }
        else {
            last_name = chosen.file_name;
            same_count = 1;
        }

        return chosen;
    }

    // 그림·이름·대사를 덮어쓰고 크기를 item_size 유닛에 맞춘 뒤 콜라이더를 그림에 맞춘다.
    void Apply(Variant variant)
    {
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer != null && variant.sprite != null) {
            renderer.sprite = variant.sprite;
            Sprite_fit.FitDiameter(renderer, item_size);
            FitCollider(renderer);
        }

        Crop_data data = GetComponentInChildren<Crop_data>();
        if (data != null) {
            data.display_name = variant.display_name;
            data.dialogue_id = variant.dialogue_id;
        }

        // 인스턴스 이름은 프리팹 이름 그대로 둔다. Dialogue_table 이 "ob_normal" 로도 찾을 수 있게.
    }

    // 원 콜라이더 반지름을 그림 긴 변의 절반(월드)으로. 로컬 값이라 scale 로 나눈다.
    static void FitCollider(SpriteRenderer renderer)
    {
        CircleCollider2D circle = renderer.GetComponentInParent<CircleCollider2D>();
        if (circle == null) {
            return;
        }

        Vector2 world = Sprite_fit.WorldSize(renderer);
        float longest = Mathf.Max(world.x, world.y);
        float scale = Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y));
        circle.radius = longest * 0.5f / Mathf.Max(0.0001f, scale);
    }
}
