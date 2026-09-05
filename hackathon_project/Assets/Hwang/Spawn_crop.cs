using UnityEngine;

// 작물을 화면 오른쪽 밖에서 일정 간격으로 내보낸다.
// 내보낸 뒤의 이동은 Crop_flow 가 맡는다.
public class Spawn_crop : MonoBehaviour
{

    public GameObject[] crop_prefabs = new GameObject[3];

    // 높이만 여기서 가져온다. x 는 화면 오른쪽 밖으로 자동 계산한다.
    public Transform spawn_point;

    public float min_interval = 0.85f;
    public float max_interval = 1.35f;
    public float spawn_margin = 2f;     // 화면 오른쪽 끝에서 이만큼 더 밖에서 만든다
    public float first_delay = 0.6f;

    float next_spawn_time;

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

    // 생성한 오브젝트를 돌려준다. 부르는 쪽에서 바로 쓸 일이 있을 수 있다.
    public GameObject Spawn()
    {
        GameObject prefab = PickRandomPrefab();
        if (prefab == null) {
            return null;
        }

        Vector3 position = spawn_point != null ? spawn_point.position : transform.position;
        position.x = World_scroll.RightX() + spawn_margin;

        GameObject crop = Instantiate(prefab, position, Quaternion.identity);

        // 프리팹에 붙여두는 걸 잊어도 흘러오도록 여기서 채워 넣는다.
        if (crop.GetComponent<Crop_flow>() == null) {
            crop.AddComponent<Crop_flow>();
        }

        return crop;
    }

    GameObject PickRandomPrefab()
    {
        if (crop_prefabs == null) {
            return null;
        }

        // 비어 있는 슬롯을 뽑으면 그 턴은 아무것도 안 나오게 되므로,
        // 채워진 것들 중에서만 고른다.
        int filled = 0;
        foreach (GameObject prefab in crop_prefabs) {
            if (prefab != null) {
                filled++;
            }
        }

        if (filled == 0) {
            return null;
        }

        int index = Random.Range(0, filled);
        foreach (GameObject prefab in crop_prefabs) {
            if (prefab == null) {
                continue;
            }

            if (index == 0) {
                return prefab;
            }

            index--;
        }

        return null;
    }
}
