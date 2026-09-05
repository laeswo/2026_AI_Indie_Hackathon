using UnityEngine;

// 아이템(작물)을 화면 오른쪽 밖에서 일정 간격으로 내보낸다.
// 내보낸 뒤의 이동은 Crop_flow 가 맡는다.
// 어떤 걸 내보낼지는 프리팹의 Crop_data.spawn_weight 가중치로 고른다. Crop_data 가 없으면 가중치 10.
// 같은 프리팹이 3번 연속 나오지는 않는다 (2번 연속 뒤에는 한 번 빼고 고른다).
//
// 성검 이벤트: 드래곤 HP 가 40% 이하로 떨어진 뒤 다음 스폰 때 일반 작물 대신 성검 + 대장장이 세트를 한 판에 한 번 내보낸다.
// 그 뒤 2초는 일반 스폰을 쉬어 세트가 눈에 띄게. 두 프리팹 슬롯이 비어 있으면 건너뛰고 경고 한 번.
// "한 번만" 은 인스턴스 필드라 R 재시작(씬 재로드)이면 다시 나온다.
public class Spawn_crop : MonoBehaviour
{

    [Header("아이템 프리팹 (얼마든지 추가)")]
    public GameObject[] crop_prefabs;

    [Header("성검 이벤트 (비우면 안 나온다)")]
    public GameObject holy_sword_prefab;    // 그림만 있으면 된다. 나머지는 Holy_sword.Setup 이 붙인다
    public GameObject smith_prefab;         // 그림만. Smith_npc 는 코드가 붙인다

    const float holy_sword_hp_ratio = 0.4f; // 드래곤 HP 가 이 비율 이하가 되면
    const float holy_sword_rest_time = 2f;  // 세트가 나온 뒤 일반 스폰을 쉬는 시간
    const float smith_offset_x = 1.2f;      // 대장장이는 성검 오른쪽 이만큼 (성검이 앞서 온다)
    const string holy_sword_toast = "무언가 빛나는 것이 다가온다";
    const float holy_sword_dragon_home_range = 2f; // 드래곤이 제자리에서 이만큼 안에 있을 때만 성검을 내보낸다 (돌진 중 제외)

    bool holy_sword_spawned;                // 한 판에 한 번
    Dragon dragon;

    // 높이만 여기서 가져온다. x 는 화면 오른쪽 밖으로 자동 계산한다.
    public Transform spawn_point;

    [Header("스폰 간격 (초)")]
    public float min_interval = 0.85f;
    public float max_interval = 1.35f;
    float spawn_margin = 2f;     // 화면 오른쪽 끝에서 이만큼 더 밖에서 만든다
    float first_delay = 0.6f;

    float default_weight = 10f;  // Crop_data 가 없는 프리팹의 가중치. 예전 작물 3개가 여기에 해당한다

    float next_spawn_time;

    GameObject last_prefab;      // 직전에 내보낸 프리팹
    int same_prefab_count;       // 몇 번 연속인지

    void Start()
    {
        next_spawn_time = first_delay;
    }

    void Update()
    {
        if (Game_flow.is_over) {
            return;
        }

        next_spawn_time -= Time.deltaTime;
        if (next_spawn_time > 0f) {
            return;
        }

        // 성검 세트 차례면 일반 작물 대신 그걸 내보내고 잠깐 쉰다.
        if (!holy_sword_spawned && ShouldSpawnHolySword()) {
            holy_sword_spawned = true;
            next_spawn_time = holy_sword_rest_time;
            SpawnHolySwordSet();
            return;
        }

        next_spawn_time = Random.Range(min_interval, max_interval);
        Spawn();
    }

    // 드래곤 HP 가 기준 이하이고 아직 살아 있을 때. 단, 성검이 나오자마자 사라질 상황이면 다음 스폰 때까지 기다린다:
    //   - 페이즈 2 포효가 아직 안 왔으면(phase 1 인데 HP 는 이미 50% 아래) 포효의 BlowAwayAll 이 흐르는 작물을 전부 날려 버리므로 포효 뒤에.
    //   - 드래곤이 무적(포효·복귀 중)이거나 제자리를 떠나 돌진 중이면 바닥을 훑으며 작물을 먹으니(can_eat) 제자리에 있을 때.
    // 40% 는 페이즈 2 기준(50%)보다 낮아서 성검은 원래 페이즈 2 이벤트다. 기다려도 몇 초 차이다.
    bool ShouldSpawnHolySword()
    {
        if (dragon == null) {
            dragon = FindFirstObjectByType<Dragon>();
            if (dragon == null) {
                return false;
            }
        }

        if (dragon.max_hp <= 0 || dragon.hp <= 0) {
            return false;
        }

        if ((float)dragon.hp / dragon.max_hp > holy_sword_hp_ratio) {
            return false;
        }

        // 포효(작물 전부 날림)가 끝난 뒤에만. phase 는 포효 상태로 들어가면서 2 가 되고, 포효 동안은 무적이다.
        if (dragon.phase < 2 || dragon.is_invincible) {
            return false;
        }

        // 돌진 중이면 제자리에서 멀다. 제자리 근처(부유 중)일 때만 내보내야 스폰 지점(화면 오른쪽 밖)의 성검을 먹지 않는다.
        return Mathf.Abs(dragon.position.x - dragon.base_position.x) <= holy_sword_dragon_home_range;
    }

    // 성검이 앞, 대장장이가 오른쪽 1.2 뒤. 둘 다 일반 작물과 같은 높이·같은 속도로 흘러온다.
    void SpawnHolySwordSet()
    {
        if (holy_sword_prefab == null || smith_prefab == null) {
            Debug.LogWarning(name + " : holy_sword_prefab / smith_prefab 이 비어 있어서 성검 이벤트를 건너뜁니다. Spawn_crop 인스펙터에 두 프리팹을 연결해 주세요.");
            return;
        }

        Vector3 position = spawn_point != null ? spawn_point.position : transform.position;
        position.x = World_scroll.RightX() + spawn_margin;

        GameObject sword = Instantiate(holy_sword_prefab, position, Quaternion.identity);
        Holy_sword.Setup(sword);

        GameObject smith = Instantiate(smith_prefab, position + new Vector3(smith_offset_x, 0f, 0f), Quaternion.identity);
        if (smith.GetComponent<Smith_npc>() == null) {
            smith.AddComponent<Smith_npc>();
        }

        Toast.Show(holy_sword_toast, 2f);

        Debug.Log("스폰: 성검 + 대장장이 (드래곤 HP " + dragon.hp + "/" + dragon.max_hp + ")");
    }

    void Spawn()
    {
        GameObject prefab = PickPrefab();
        if (prefab == null) {
            return;
        }

        Vector3 position = spawn_point != null ? spawn_point.position : transform.position;
        position.x = World_scroll.RightX() + spawn_margin;

        GameObject crop = Instantiate(prefab, position, Quaternion.identity);

        // 프리팹에 붙여두는 걸 잊어도 흘러오도록 여기서 채워 넣는다.
        if (crop.GetComponent<Crop_flow>() == null) {
            crop.AddComponent<Crop_flow>();
        }

        // 연속 횟수 갱신.
        if (prefab == last_prefab) {
            same_prefab_count++;
        }
        else {
            last_prefab = prefab;
            same_prefab_count = 1;
        }

        // 인스턴스 기준. ob_normal 은 Item_variants 가 Awake 에서 이름을 바꾸므로 프리팹이 아니라 만든 것을 읽어야 맞다.
        Debug.Log("스폰: " + NameOf(crop) + (same_prefab_count >= 2 ? " (" + same_prefab_count + "연속)" : ""));
    }

    // spawn_weight 비율로 하나 고른다. 2번 연속 나온 프리팹은 이번엔 뺀다.
    // 빼고 나니 고를 게 없으면(프리팹이 하나뿐) 그냥 그걸 다시 낸다.
    GameObject PickPrefab()
    {
        if (crop_prefabs == null || crop_prefabs.Length == 0) {
            return null;
        }

        GameObject banned = same_prefab_count >= 2 ? last_prefab : null;

        float total = TotalWeight(banned);
        if (total <= 0f) {
            banned = null;
            total = TotalWeight(banned);
        }
        if (total <= 0f) {
            return null;
        }

        float roll = Random.value * total;
        GameObject last_valid = null;

        foreach (GameObject prefab in crop_prefabs) {
            float weight = WeightOf(prefab, banned);
            if (weight <= 0f) {
                continue;
            }

            last_valid = prefab;
            if (roll < weight) {
                return prefab;
            }
            roll -= weight;
        }

        // 부동소수 오차로 끝까지 못 고른 경우. 마지막 유효 프리팹으로.
        return last_valid;
    }

    float TotalWeight(GameObject banned)
    {
        float total = 0f;
        foreach (GameObject prefab in crop_prefabs) {
            total += WeightOf(prefab, banned);
        }
        return total;
    }

    float WeightOf(GameObject prefab, GameObject banned)
    {
        if (prefab == null || prefab == banned) {
            return 0f;
        }

        Crop_data data = prefab.GetComponentInChildren<Crop_data>();
        return data != null ? Mathf.Max(0f, data.spawn_weight) : default_weight;
    }

    string NameOf(GameObject prefab)
    {
        Crop_data data = prefab.GetComponentInChildren<Crop_data>();
        return data != null && !string.IsNullOrEmpty(data.display_name) ? data.display_name : prefab.name;
    }
}
