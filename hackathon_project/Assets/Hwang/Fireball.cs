using UnityEngine;

// 드래곤이 쏘는 화염구. 왼쪽으로 직진하다가 용사에게 닿으면 피해를 준다.
// 용사에게 콜라이더가 없으므로 거리로 판정한다.
public class Fireball : MonoBehaviour
{

    public float speed = 6.4f;
    public int damage = 1;
    public float hit_radius = 0.7f;
    public float despawn_margin = 2f;

    public Color color = new Color(1f, 0.54f, 0.23f);
    public float diameter = 0.55f;

    Transform player;
    Player_health health;

    void Awake()
    {
        Placeholder_sprite.Ensure(gameObject, color, diameter, 60);

        GameObject player_object = GameObject.FindWithTag("Player");
        if (player_object != null) {
            player = player_object.transform;
            health = player_object.GetComponent<Player_health>();
        }
    }

    void Update()
    {
        transform.position += Vector3.left * (speed * Time.deltaTime);

        if (transform.position.x < World_scroll.LeftX() - despawn_margin) {
            Destroy(gameObject);
            return;
        }

        if (player == null) {
            return;
        }

        float distance = Vector2.Distance(player.position, transform.position);
        if (distance > hit_radius) {
            return;
        }

        // 무적 중이면 맞지 않고 그냥 지나간다.
        if (health != null && health.is_invincible) {
            return;
        }

        if (health != null) {
            health.TakeHit(damage);
        }

        Destroy(gameObject);
    }
}
