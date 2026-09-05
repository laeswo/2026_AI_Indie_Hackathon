using UnityEngine;

// 용사의 체력. 맞으면 잠시 무적이 되고 깜빡인다.
public class Player_health : MonoBehaviour
{

    public int max_hp = 5;
    public float invincible_time = 1.3f;
    public float blink_rate = 14f;

    public int hp { get; private set; }
    public bool is_invincible { get { return invincible_timer > 0f; } }

    float invincible_timer;
    SpriteRenderer sprite_renderer;

    void Awake()
    {
        hp = max_hp;
        sprite_renderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (invincible_timer <= 0f) {
            return;
        }

        invincible_timer -= Time.deltaTime;

        if (sprite_renderer == null) {
            return;
        }

        Color color = sprite_renderer.color;

        if (invincible_timer <= 0f) {
            color.a = 1f;
        }
        else {
            bool dim = Mathf.FloorToInt(invincible_timer * blink_rate) % 2 == 1;
            color.a = dim ? 0.4f : 1f;
        }

        sprite_renderer.color = color;
    }

    public void TakeHit(int damage)
    {
        if (is_invincible || Game_flow.is_over || hp <= 0) {
            return;
        }

        hp -= damage;
        invincible_timer = invincible_time;

        Debug.Log("맞았다! (용사 HP " + Mathf.Max(hp, 0) + "/" + max_hp + ")");

        if (hp <= 0) {
            hp = 0;
            Game_flow.End("용사 전사");
        }
    }

    void OnGUI()
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(24f, 17f, 60f, 22f), "용사");

        for (int i = 0; i < max_hp; i++) {
            GUI.color = i < hp ? new Color(0.88f, 0.34f, 0.29f) : new Color(0.23f, 0.25f, 0.28f);
            GUI.DrawTexture(new Rect(70f + i * 22f, 20f, 16f, 16f), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;

        if (!Game_flow.is_over) {
            return;
        }

        // 종료 배너. 화면 중앙에 결과를 크게 띄운다.
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 42;
        style.fontStyle = FontStyle.Bold;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 60f), Game_flow.result, style);
    }
}
