using UnityEngine;

// 배경 한 겹을 왼쪽으로 흘려보내고, 화면 밖으로 나간 타일을 오른쪽 끝으로 되돌린다.
// 타일 하나만 자식으로 넣어두면 화면을 채울 만큼 알아서 복제한다.
public class Scroll_layer : MonoBehaviour
{

    public Transform tile;          // 반복시킬 타일. 비워두면 첫 번째 자식을 쓴다.
    public float tile_width = 0f;   // 0 이면 SpriteRenderer 크기에서 알아낸다.

    // 0 이면 멈춰 있고, 1 이면 지면과 같은 속도로 흐른다.
    // 멀리 있는 겹일수록 작게 준다. (산 0.15 / 집 0.45 / 땅 1)
    [Range(0f, 2f)]
    public float parallax = 1f;

    public int extra_tiles = 1;     // 화면을 채우고도 남길 여유 타일 수

    Transform[] tiles;
    float span;                     // 타일 전체가 차지하는 폭. 되돌릴 때 이만큼 민다.
    float half_width;

    void Start()
    {
        Build();
    }

    void Build()
    {
        if (tile == null && transform.childCount > 0) {
            tile = transform.GetChild(0);
        }

        if (tile == null) {
            Debug.LogWarning(name + " : 반복시킬 타일이 없어서 스크롤을 끕니다.");
            enabled = false;
            return;
        }

        if (tile_width <= 0f) {
            tile_width = MeasureWidth(tile);
        }

        if (tile_width <= 0f) {
            Debug.LogWarning(name + " : 타일 폭을 알 수 없어서 스크롤을 끕니다. tile_width 를 직접 넣어주세요.");
            enabled = false;
            return;
        }

        half_width = tile_width * 0.5f;

        // 화면을 덮고도 한 장이 더 있어야 되돌리는 순간에 빈틈이 안 보인다.
        float view_width = World_scroll.ViewHalfWidth() * 2f;
        int count = Mathf.CeilToInt(view_width / tile_width) + 1 + Mathf.Max(0, extra_tiles);

        tiles = new Transform[count];
        tiles[0] = tile;

        float start_x = World_scroll.LeftX() - tile_width;
        Vector3 position = tile.position;

        for (int i = 1; i < count; i++) {
            Transform copy = Instantiate(tile, transform);
            copy.name = tile.name + "_" + i;
            tiles[i] = copy;
        }

        for (int i = 0; i < count; i++) {
            position.x = start_x + i * tile_width;
            tiles[i].position = position;
        }

        span = tile_width * count;
    }

    static float MeasureWidth(Transform target)
    {
        SpriteRenderer renderer = target.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null) {
            return 0f;
        }

        return renderer.bounds.size.x;
    }

    void Update()
    {
        if (tiles == null) {
            return;
        }

        float distance = World_scroll.current_speed * parallax * Time.deltaTime;
        if (distance == 0f) {
            return;
        }

        float left_x = World_scroll.LeftX();

        foreach (Transform t in tiles) {
            if (t == null) {
                continue;
            }

            Vector3 position = t.position;
            position.x -= distance;

            // 오른쪽 끝이 화면 왼쪽 밖으로 완전히 나가면 줄 맨 뒤로 보낸다.
            if (position.x + half_width < left_x) {
                position.x += span;
            }

            t.position = position;
        }
    }
}
