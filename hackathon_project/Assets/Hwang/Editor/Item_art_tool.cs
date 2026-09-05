using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// 메뉴 Hwang › 아이템 그림 프리팹에 넣기. 스크립트가 다시 로드될 때도 한 번 자동으로 돈다(바꿀 게 없으면 조용히 끝난다).
//
// 하는 일
//   1. 그림 png 의 조각 피벗을 가운데로 고친다. 자동 슬라이스는 피벗이 왼쪽 아래(0,0)라 그림이 오브젝트 위치에서
//      오른쪽 위로 밀려 그려지고 콜라이더와 어긋난다. 임포트 설정만 바꾸고 다시 임포트한다.
//   2. 그림이 고정인 프리팹(창·검·도끼)에 그림을 넣고 긴 변 item_size 유닛으로 맞춘 뒤 콜라이더를 그림에 맞춰 저장한다.
//   3. ob_normal 은 스폰 때 그림이 바뀌므로 Item_variants 만 붙인다.
// 조각이 여러 개면(자동 슬라이스 부스러기) 가장 큰 조각을 쓴다. 다시 실행해도 안전하다. 프리팹 YAML 을 손으로 고치지 않는다.
public static class Item_art_tool
{

    const string prefab_folder = "Assets/Hwang/Ob_Prefab/";
    const string resources_folder = "Assets/Hwang/Resources/";
    const float item_size = 1.5f;           // 모든 아이템은 긴 변이 1.5 유닛. 작으면 안 보여서 키웠다. 바꾸면 Item_variants 도 같이

    struct Entry
    {
        public string prefab;               // Ob_Prefab 안의 프리팹 이름
        public string art;                  // Resources 아래 경로 (확장자 없이)
        public bool box_collider;           // 콜라이더가 하나도 없으면 BoxCollider2D 를 붙인다 (길쭉한 그림용)
        public float gravity_scale;         // 0 이상이면 Crop_data.gravity_scale 을 이 값으로 맞춘다. 음수면 안 건드린다
        public bool face_velocity;          // 그림이 날아가는 방향을 보게 (Crop_data.face_velocity). 그림이 오른쪽을 본다는 전제
    }

    static readonly Entry[] entries = {
        // 창은 중력 0 → 일직선으로 날아간다. 예측선(Throw_trajectory)도 같은 값을 읽어 직선이 된다
        new Entry { prefab = "ob_Spear",      art = "Ob_Spear/spear", gravity_scale = 0f, face_velocity = true },
        new Entry { prefab = "ob_sword",      art = "Ob_Sword/sword", gravity_scale = -1f },
        new Entry { prefab = "ob_axe",        art = "Ob_axe/axe", gravity_scale = -1f },
        new Entry { prefab = "ob_holy_sword", art = "Ob_holy_sword/holysword", box_collider = true, gravity_scale = -1f },
    };

    // 피벗을 가운데로 고칠 그림 폴더. ob_normal 의 그림도 여기 포함.
    static readonly string[] art_folders = { "Ob_Spear", "Ob_Sword", "Ob_axe", "Ob_holy_sword", "Ob_normal" };

    // 스폰 때 그림이 바뀌는 프리팹과 그 그림 폴더. Item_variants 를 붙이고 폴더를 적어 둔다.
    struct Variant_entry
    {
        public string prefab;
        public string folder;               // Resources 아래 폴더 이름
    }

    static readonly Variant_entry[] variant_entries = {
        new Variant_entry { prefab = "ob_normal", folder = "Ob_normal" },
        new Variant_entry { prefab = "ob_sword",  folder = "Ob_Sword" },     // 검·종
    };

    // 스크립트 로드 직후 한 번. 플레이 중이면 건너뛴다. 바꿀 게 없으면 로그도 안 남긴다.
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

    [MenuItem("Hwang/아이템 그림 프리팹에 넣기")]
    public static void ApplyFromMenu()
    {
        Apply(true);
    }

    static void Apply(bool verbose)
    {
        int changed = 0;

        changed += FixPivots();

        foreach (Entry entry in entries) {
            if (ApplyFixedArt(entry)) {
                changed++;
            }
        }

        foreach (Variant_entry entry in variant_entries) {
            if (EnsureVariants(entry.prefab, entry.folder)) {
                changed++;
            }
        }

        if (changed > 0) {
            AssetDatabase.SaveAssets();
        }

        if (verbose || changed > 0) {
            Debug.Log("아이템 그림: " + changed + "건 바꿈. (이미 맞는 것은 건너뜀)");
        }
    }

    // ---------- 1. 피벗 ----------

    // 폴더 안 모든 png 의 조각 피벗을 가운데로. 바꾼 파일 수.
    static int FixPivots()
    {
        int fixed_count = 0;

        foreach (string folder in art_folders) {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { resources_folder + folder });
            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (FixPivot(path)) {
                    fixed_count++;
                }
            }
        }

        return fixed_count;
    }

    static bool FixPivot(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || importer.textureType != TextureImporterType.Sprite) {
            return false;
        }

        // Single 모드는 임포터 설정 하나로 끝난다.
        if (importer.spriteImportMode == SpriteImportMode.Single) {
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment == (int)SpriteAlignment.Center) {
                return false;
            }

            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            Debug.Log("아이템 그림: " + path + " 피벗을 가운데로 고쳤습니다.");
            return true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple) {
            return false;
        }

        // Multiple 은 조각마다 피벗이 있다. 스프라이트 에디터가 쓰는 데이터 프로바이더로 고친다.
        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();

        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) {
            return false;
        }

        provider.InitSpriteEditorDataProvider();
        SpriteRect[] rects = provider.GetSpriteRects();

        bool changed = false;
        foreach (SpriteRect rect in rects) {
            if (rect.alignment == SpriteAlignment.Center) {
                continue;
            }
            rect.alignment = SpriteAlignment.Center;
            rect.pivot = new Vector2(0.5f, 0.5f);
            changed = true;
        }

        if (!changed) {
            return false;
        }

        provider.SetSpriteRects(rects);
        provider.Apply();
        importer.SaveAndReimport();
        Debug.Log("아이템 그림: " + path + " 조각 " + rects.Length + "개 피벗을 가운데로 고쳤습니다.");
        return true;
    }

    // ---------- 2. 고정 그림 ----------

    static bool ApplyFixedArt(Entry entry)
    {
        string prefab_path = prefab_folder + entry.prefab + ".prefab";

        Sprite sprite = LoadLargestSprite(entry.art);
        if (sprite == null) {
            Debug.LogWarning("아이템 그림: Resources/" + entry.art + " 에서 스프라이트를 찾지 못해 " + entry.prefab + " 을 건너뜁니다.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefab_path);
        if (root == null) {
            Debug.LogWarning("아이템 그림: " + prefab_path + " 을 열지 못했습니다.");
            return false;
        }

        try {
            SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null) {
                Debug.LogWarning("아이템 그림: " + entry.prefab + " 에 SpriteRenderer 가 없습니다.");
                return false;
            }

            bool same_sprite = renderer.sprite == sprite;
            bool same_size = Mathf.Abs(LongestSide(renderer) - item_size) < 0.001f;
            if (same_sprite && same_size && ColliderMatches(renderer) && GravityMatches(root, entry) && FacingMatches(root, entry)) {
                return false;
            }

            renderer.sprite = sprite;
            Sprite_fit.FitDiameter(renderer, item_size);
            ApplyGravity(root, entry);
            ApplyFacing(root, entry);

            // 콜라이더가 없는 프리팹(성검)은 길쭉하니 그림 크기의 네모를 붙인다. 있으면 있는 걸 그림에 맞춘다.
            if (entry.box_collider && root.GetComponentInChildren<Collider2D>() == null) {
                root.AddComponent<BoxCollider2D>();
            }
            FitCollider(renderer);

            PrefabUtility.SaveAsPrefabAsset(root, prefab_path);
            Debug.Log("아이템 그림: " + entry.prefab + " ← " + entry.art + " (" + sprite.name + ", 긴 변 " + LongestSide(renderer).ToString("0.00") + ")");
            return true;
        }
        finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------- 3. ob_normal ----------

    static bool EnsureVariants(string prefab_name, string folder)
    {
        string prefab_path = prefab_folder + prefab_name + ".prefab";

        GameObject root = PrefabUtility.LoadPrefabContents(prefab_path);
        if (root == null) {
            Debug.LogWarning("아이템 그림: " + prefab_path + " 을 열지 못했습니다.");
            return false;
        }

        try {
            Item_variants variants = root.GetComponent<Item_variants>();
            if (variants != null && variants.resource_folder == folder) {
                return false;
            }

            if (variants == null) {
                variants = root.AddComponent<Item_variants>();
            }
            variants.resource_folder = folder;

            PrefabUtility.SaveAsPrefabAsset(root, prefab_path);
            Debug.Log("아이템 그림: " + prefab_name + " 에 Item_variants (" + folder + ") 를 붙였습니다.");
            return true;
        }
        finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------- 도우미 ----------

    // Multiple 모드 png 에서 가장 큰 조각. 자동 슬라이스 부스러기(몇 픽셀짜리)를 피한다.
    static Sprite LoadLargestSprite(string art)
    {
        List<Sprite> sprites = new List<Sprite>();

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(resources_folder + art + ".png")) {
            Sprite sprite = asset as Sprite;
            if (sprite != null) {
                sprites.Add(sprite);
            }
        }

        if (sprites.Count == 0) {
            sprites.AddRange(Resources.LoadAll<Sprite>(art));
        }

        Sprite largest = null;
        float largest_area = -1f;
        foreach (Sprite sprite in sprites) {
            float area = sprite.rect.width * sprite.rect.height;
            if (area > largest_area) {
                largest_area = area;
                largest = sprite;
            }
        }

        return largest;
    }

    // 항목에 중력 지정이 있으면 Crop_data 에 넣는다. 다른 Crop_data 값은 건드리지 않는다.
    static void ApplyGravity(GameObject root, Entry entry)
    {
        if (entry.gravity_scale < 0f) {
            return;
        }

        Crop_data data = root.GetComponentInChildren<Crop_data>();
        if (data != null && !Mathf.Approximately(data.gravity_scale, entry.gravity_scale)) {
            data.gravity_scale = entry.gravity_scale;
            Debug.Log("아이템 그림: " + entry.prefab + " 중력 배율 → " + entry.gravity_scale + (entry.gravity_scale == 0f ? " (일직선)" : ""));
        }
    }

    static bool GravityMatches(GameObject root, Entry entry)
    {
        if (entry.gravity_scale < 0f) {
            return true;
        }

        Crop_data data = root.GetComponentInChildren<Crop_data>();
        return data == null || Mathf.Approximately(data.gravity_scale, entry.gravity_scale);
    }

    // 항목이 face_velocity 면 Crop_data 에 켠다. 그림은 오른쪽을 본다는 전제라 art_faces_left 는 끈다.
    static void ApplyFacing(GameObject root, Entry entry)
    {
        if (!entry.face_velocity) {
            return;
        }

        Crop_data data = root.GetComponentInChildren<Crop_data>();
        if (data != null && (!data.face_velocity || data.art_faces_left)) {
            data.face_velocity = true;
            data.art_faces_left = false;
            Debug.Log("아이템 그림: " + entry.prefab + " 날아가는 방향을 보게 (face_velocity)");
        }
    }

    static bool FacingMatches(GameObject root, Entry entry)
    {
        if (!entry.face_velocity) {
            return true;
        }

        Crop_data data = root.GetComponentInChildren<Crop_data>();
        return data == null || (data.face_velocity && !data.art_faces_left);
    }

    static float LongestSide(SpriteRenderer renderer)
    {
        Vector2 world = Sprite_fit.WorldSize(renderer);
        return Mathf.Max(world.x, world.y);
    }

    // 콜라이더를 그림에 맞춘다. 원이면 반지름을 긴 변의 절반, 네모면 그림 폭·높이. 로컬 값이라 scale 로 나눈다. 피벗이 가운데라 offset 은 0.
    static void FitCollider(SpriteRenderer renderer)
    {
        CircleCollider2D circle = renderer.GetComponentInParent<CircleCollider2D>();
        if (circle != null) {
            circle.radius = TargetRadius(renderer, circle);
            circle.offset = Vector2.zero;
            return;
        }

        BoxCollider2D box = renderer.GetComponentInParent<BoxCollider2D>();
        if (box != null) {
            box.size = TargetBoxSize(renderer, box);
            box.offset = Vector2.zero;
        }
    }

    static bool ColliderMatches(SpriteRenderer renderer)
    {
        CircleCollider2D circle = renderer.GetComponentInParent<CircleCollider2D>();
        if (circle != null) {
            return Mathf.Abs(circle.radius - TargetRadius(renderer, circle)) < 0.001f && circle.offset == Vector2.zero;
        }

        BoxCollider2D box = renderer.GetComponentInParent<BoxCollider2D>();
        if (box != null) {
            return (box.size - TargetBoxSize(renderer, box)).sqrMagnitude < 0.000001f && box.offset == Vector2.zero;
        }

        return true;
    }

    static float TargetRadius(SpriteRenderer renderer, CircleCollider2D circle)
    {
        float scale = Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y));
        return LongestSide(renderer) * 0.5f / Mathf.Max(0.0001f, scale);
    }

    static Vector2 TargetBoxSize(SpriteRenderer renderer, BoxCollider2D box)
    {
        Vector2 world = Sprite_fit.WorldSize(renderer);
        Vector3 scale = box.transform.lossyScale;
        return new Vector2(world.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)), world.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
    }
}
