using UnityEngine;

// 오른쪽에서 흘러와 용사 앞을 지나가는 작물.
// 주워지는 순간 흐름을 멈추고 다시 평범한 물리 오브젝트가 된다.
[RequireComponent(typeof(Rigidbody2D))]
public class Crop_flow : MonoBehaviour
{

    public float despawn_margin = 3f;   // 화면 왼쪽 끝에서 이만큼 더 나가면 지운다
    public float fall_limit = -20f;     // 아래로 떨어져 사라진 것도 정리한다

    public bool is_flowing { get; private set; }

    // 던져진 뒤에만 참. 드래곤은 이 값이 참인 작물에만 맞는다.
    public bool is_thrown { get; private set; }

    Rigidbody2D body;
    Collider2D[] colliders;
    float original_gravity_scale;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        colliders = GetComponentsInChildren<Collider2D>();

        // 흐르는 동안 중력을 끄므로, 원래 값을 기억해 뒀다가 주워질 때 되돌린다.
        original_gravity_scale = body.gravityScale;

        IgnoreOtherCrops();
        StartFlowing();
    }

    // 작물끼리는 부딪히지 않는다. 던진 게 뒤따라오는 작물을 튕겨내면 흐름이 엉킨다.
    // 살아 있는 작물이 몇 개 안 되므로 생성 시점에 전부 훑어도 부담이 없다.
    void IgnoreOtherCrops()
    {
        Crop_flow[] others = FindObjectsByType<Crop_flow>(FindObjectsSortMode.None);

        foreach (Crop_flow other in others) {
            if (other == this || other.colliders == null) {
                continue;
            }

            foreach (Collider2D mine in colliders) {
                foreach (Collider2D theirs in other.colliders) {
                    Physics2D.IgnoreCollision(mine, theirs, true);
                }
            }
        }
    }

    public void StartFlowing()
    {
        is_flowing = true;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    // 용사가 주웠을 때 호출한다. 이후 위치는 Player 가 직접 옮긴다.
    public void StopFlowing()
    {
        is_flowing = false;
        body.gravityScale = original_gravity_scale;
    }

    // Player 가 손에서 놓는 순간 호출한다.
    public void MarkThrown()
    {
        is_thrown = true;
    }

    void FixedUpdate()
    {
        if (!is_flowing) {
            return;
        }

        // 배경과 같은 속도를 매 스텝 다시 읽는다. 중간에 속도가 바뀌어도 같이 따라간다.
        body.linearVelocity = new Vector2(-World_scroll.current_speed, 0f);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 던진 작물은 땅이든 뭐든 처음 닿는 순간 사라진다. 바닥에 남아 굴러다니지 않게.
        // 드래곤은 트리거라 여기로 안 들어오고, Dragon 쪽에서 직접 지운다.
        if (is_thrown) {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (transform.position.y < fall_limit) {
            Destroy(gameObject);
            return;
        }

        if (transform.position.x < World_scroll.LeftX() - despawn_margin) {
            Destroy(gameObject);
        }
    }
}
