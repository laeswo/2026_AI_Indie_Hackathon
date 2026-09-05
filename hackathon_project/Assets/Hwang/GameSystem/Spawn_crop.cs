using UnityEngine;

// 아이템(작물)을 화면 오른쪽 밖에서 일정 간격으로 내보낸다.
// 내보낸 뒤의 이동은 Crop_flow 가 맡는다.
// 어떤 걸 내보낼지는 프리팹의 Crop_data.spawn_weight 가중치로 고른다. Crop_data 가 없으면 가중치 10.
// 같은 프리팹이 3번 연속 나오지는 않는다 (2번 연속 뒤에는 한 번 빼고 고른다).
public class Spawn_crop : MonoBehaviour
{

    [Header("아이템 프리팹 (얼마든지 추가)")]
    public GameObject[] crop_prefabs;

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

        next_spawn_time = Random.Range(min_interval, max_interval);
        Spawn();
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

        Debug.Log("스폰: " + NameOf(prefab) + (same_prefab_count >= 2 ? " (" + same_prefab_count + "연속)" : ""));
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
