using UnityEngine;
using UnityEngine.InputSystem;

// 용사의 체력. 맞으면 잠시 무적이 되고 깜빡인다.
public class Player_health : MonoBehaviour
{

    [Header("체력")]
    public int max_hp = 5;
    public float invincible_time = 1.3f;
    float blink_rate = 14f;

    // 피격 연출. 실제로 HP 가 깎일 때만. 빨간 플래시 + 짧은 멈춤 + 살짝 흔들림.
    const float hit_shake_amplitude = 0.12f;
    const float hit_shake_time = 0.15f;
    const float hit_hitstop_time = 0.08f;
    const float hit_flash_time = 0.2f;
    static readonly Color hit_flash_color = new Color(1f, 0.2f, 0.2f, 0.45f);

    // 피격 조명. 몸 주변에 붉은 빛이 번쩍하고 사라진다.
    static readonly Color hit_glow_color = new Color(1f, 0.25f, 0.2f);
    const float hit_glow_intensity = 1.0f;
    const float hit_glow_radius = 2.5f;
    const float hit_glow_time = 0.25f;

    const float result_jingle_delay = 0.8f;   // 사망음 뒤에 패배 징글이 이어지기까지

    // 전사 연출. Game_flow.End 직전에.
    const float death_shake_amplitude = 0.3f;
    const float death_shake_time = 0.5f;
    const float death_zoom_amount = 0.12f;
    const float death_zoom_time = 0.8f;
    const float death_flash_time = 0.3f;
    static readonly Color death_flash_color = new Color(1f, 1f, 1f, 0.6f);

    public int hp { get; private set; }
    public bool is_invincible { get { return invincible_timer > 0f; } }

    float invincible_timer;
    SpriteRenderer sprite_renderer;
    Animator animator;              // 있으면 hurt / dead 트리거. 없어도 된다
    Player player_component;        // 원래 색은 Player 가 그림에서 잰 값을 쓴다
    Color own_base_color = Color.white;

    void Awake()
    {
        hp = max_hp;
        sprite_renderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        player_component = GetComponent<Player>();

        if (sprite_renderer != null) {
            own_base_color = sprite_renderer.color;
        }

        // HUD 를 씬에 안 놓아도 알아서 생기게. 하트·드래곤 바·결과 패널을 그린다.
        Hud.Ensure();
    }

    // 깜빡임은 이 색 기준으로 알파만 바꾸고, 끝나면 정확히 이 색으로 돌아온다.
    Color BaseColor()
    {
        return player_component != null ? player_component.base_color : own_base_color;
    }

    void Update()
    {
        if (Game_flow.is_over) {
            HandleResultKeys();
        }

        if (invincible_timer <= 0f) {
            return;
        }

        invincible_timer -= Time.deltaTime;

        if (sprite_renderer == null) {
            return;
        }

        Color color = BaseColor();

        if (invincible_timer > 0f) {
            bool dim = Mathf.FloorToInt(invincible_timer * blink_rate) % 2 == 1;
            color.a *= dim ? 0.4f : 1f;
        }

        sprite_renderer.color = color;
    }

    // 결과 화면에서 R 은 같은 씬 다시, Esc 는 시작 화면.
    void HandleResultKeys()
    {
        if (Keyboard.current == null) {
            return;
        }

        if (Keyboard.current.rKey.wasPressedThisFrame) {
            Game_flow.Restart();
        }
        else if (Keyboard.current.escapeKey.wasPressedThisFrame) {
            Game_flow.GoToStart();
        }
    }

    public void TakeHit(int damage)
    {
        if (is_invincible || Game_flow.is_over || hp <= 0) {
            return;
        }

        hp -= damage;
        invincible_timer = invincible_time;

        Camera_director.Shake(hit_shake_amplitude, hit_shake_time);
        Camera_director.HitStop(hit_hitstop_time);
        Camera_director.Flash(hit_flash_color, hit_flash_time);

        Sprite_fit.Trigger(animator, hp <= 0 ? "dead" : "hurt");
        Sound_bank.Play(hp <= 0 ? "die_sound" : "hurt_sound", transform.position);

        // 맞은 자리에서 붉은 빛이 잠깐 번진다.
        Scene_lighting.Flash(transform.position, hit_glow_color, hit_glow_intensity, hit_glow_radius, hit_glow_time);

        Debug.Log("맞았다! (용사 HP " + Mathf.Max(hp, 0) + "/" + max_hp + ")");

        if (hp <= 0) {
            hp = 0;

            // 종료 연출. 흰 플래시가 피격 빨강을 덮는다. End 가 is_over 를 세우기 전에 불러야 히트스톱이 아닌 연출들이 걸린다.
            Camera_director.Shake(death_shake_amplitude, death_shake_time);
            Camera_director.ZoomPunch(death_zoom_amount, death_zoom_time);
            Camera_director.Flash(death_flash_color, death_flash_time);

            // 죽는 소리 뒤에 패배 징글. BGM 은 Game_flow.End 가 멈춘다.
            Sound_bank.Play("defeat_sound", transform.position, result_jingle_delay);

            Game_flow.End("게임 오버");
        }
    }
}
