using UnityEngine;
using UnityEngine.Serialization;

// 흘러오는 아이템(작물)의 게임 수치. 프리팹마다 붙여서 인스펙터에서 값을 채운다.
// 안 붙이거나 옛 필드만 있어도 예전 작물과 똑같이 동작한다 (데미지 10, 중력 1, 등장 가중치 10).
// 어떤 값이 어디서 쓰이는지:
//   damage_min/max, heal_dragon, explode_*, hit_sound  → Dragon.OnTriggerEnter2D
//   pierce, counter_hits                               → Fireball.OnTriggerEnter2D (큰 화염구)
//   gravity_scale, homing_turn_rate, explode_*         → Crop_flow
//   spin, throw_sound                                  → Player.ThrowCrop
//   spawn_weight                                       → Spawn_crop
public class Crop_data : MonoBehaviour
{

    [Header("기본")]
    public string display_name = "작물";
    public string dialogue_id = "";     // 주울 때 대사 id (Dialogue_table). 비우면 프리팹 이름으로 찾는다

    // 같으면 고정 데미지. 예전 프리팹의 damage 값은 damage_min 으로 이어받는다.
    [FormerlySerializedAs("damage")]
    public int damage_min = 10;
    public int damage_max = 10;

    [Header("물리")]
    public float gravity_scale = 1f;    // 가벼움 0.35 / 보통 1.0 / 무거움 1.8. 프리팹 Rigidbody2D 의 Gravity Scale 은 무시하고 이 값을 쓴다
    public float spin = 0f;             // 던질 때 회전 (도/초). 0 이면 안 돈다. 검·종 180, 도끼 720. 프리팹의 Freeze Rotation Z 가 꺼져 있어야 보인다
    public bool face_velocity = false;  // 손에 들고 조준할 때와 던진 뒤에 그림이 날아가는 방향을 본다. 창처럼 앞뒤가 있는 것. spin 과 같이 쓰지 않는다
    public bool art_faces_left = false; // face_velocity 용. 그림이 왼쪽을 보고 그려졌으면 켠다 (창 그림은 오른쪽을 본다)
    public float art_angle = 0f;        // face_velocity 용. 그림의 "앞"이 어느 쪽을 향해 그려졌는지 (도). 0 오른쪽, 90 위, -90 아래, 180 왼쪽. art_faces_left 가 켜져 있으면 180 으로 친다
    public float rest_angle = 0f;       // 흘러올 때·손에 들었을 때 그림 회전 (도). 창을 비스듬히 눕히고 싶으면 여기

    [Header("등장")]
    public float spawn_weight = 10f;    // 등장 가중치. 0 이면 안 나온다. 같은 종류가 여러 프리팹이면 나눠서 넣는다

    [Header("큰 화염구 상대")]
    public bool pierce = false;         // 큰 화염구를 맞혀도 안 사라지고 계속 날아간다
    public int counter_hits = 1;        // 큰 화염구에 몇 히트로 치는지. 도끼 2

    [Header("특수")]
    public int heal_dragon = 0;         // 0 보다 크면 데미지 대신 드래곤을 이만큼 회복시킨다 (함정 아이템)
    [Range(0f, 1f)]
    public float explode_chance = 0f;   // 닿았을 때 이 확률로 폭발
    public int explode_damage = 0;      // 폭발 피해. 반경 안의 드래곤이 받는다
    public float explode_radius = 0f;   // 폭발 반경 (유닛)
    public float homing_turn_rate = 0f; // 도/초. 0 보다 크면 날아가는 동안 드래곤 쪽으로 이만큼씩 휜다. 지금은 쓰는 아이템 없음

    [Header("소리 (비어도 됨)")]
    public AudioClip throw_sound;
    public AudioClip hit_sound;

    // 이번에 줄 데미지. max 가 min 보다 작게 남아 있으면(옛 프리팹) min 으로 고정.
    public int RollDamage()
    {
        if (damage_max <= damage_min) {
            return damage_min;
        }

        return Random.Range(damage_min, damage_max + 1);
    }

    public bool is_explosive
    {
        get { return explode_chance > 0f; }
    }

    // 폭발할지 굴린다. 확률이 0 이면 절대 안 터진다.
    public bool RollExplode()
    {
        return is_explosive && Random.value < explode_chance;
    }

    // 인스펙터에서 값을 고칠 때 말이 안 되는 조합을 바로잡는다.
    void OnValidate()
    {
        if (damage_max < damage_min) {
            damage_max = damage_min;
        }

        counter_hits = Mathf.Max(1, counter_hits);
        spawn_weight = Mathf.Max(0f, spawn_weight);
        explode_radius = Mathf.Max(0f, explode_radius);
        homing_turn_rate = Mathf.Max(0f, homing_turn_rate);
    }
}
