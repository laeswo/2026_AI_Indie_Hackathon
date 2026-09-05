using UnityEngine;

// 마지막 패턴. 한 판에 한 번, 드래곤 HP 가 1 이하로 떨어지려는 순간 HP 를 1 에 묶고 여기로 온다(Dragon.TakeDamage → StartFinalPattern).
//   Charge  힘 모으기. 화면 가운데 높이로 올라가며 입에 흰빛이 커진다. 배너 "드래곤이 마지막 힘을 모은다"
//   Fire    맵 전체를 덮는 두꺼운 레이저. 줄다리기: 용사가 Y 를 누를 때마다 레이저가 드래곤 쪽으로 밀리고(Player 가 Mash 로 넘겨준다),
//           안 누르면 다시 늘어난다. 밀린 만큼 화면 끝에서부터 짧아지고 가늘고 옅어진다. 끝까지 밀면 승리
//   Win     다 채웠다. 레이저가 꺼지며 드래곤이 죽는다(Dragon.FinalKill → 게임 승리)
//   Fail    시간 안에 못 채웠다. 레이저가 용사를 덮고 게임 오버
// 이 동안 드래곤은 무적이고 먹지 않는다. 다른 화염구·충격파는 StartFinalPattern 이 치운다.
public class State_final : Dragon_state
{

    enum Phase
    {
        Charge,
        Fire,
        Win,
        Fail
    }

    // 레이저 그림. 임시 원을 길게 늘린 두 겹(바깥 흰빛 + 가운데 더 밝은 심). 조명을 안 받는 Unlit.
    const int beam_order = 55;
    const float outer_alpha = 0.85f;
    const float core_thickness_ratio = 0.35f;
    const float beam_margin = 3f;               // 화면 끝을 지나 이만큼 더
    const float layer_overlap = 0.25f;          // 심과 띠가 겹치는 길이. 이음새에 틈이 안 생기게
    const float pushed_back_min = 0.55f;        // 연타로 다 밀렸을 때 두께·밝기가 이 비율까지 줄어든다
    const float glow_intensity = 1.8f;
    const float glow_falloff = 2.5f;
    const float mouth_glow_max_intensity = 2.6f;
    const float mouth_glow_max_radius = 4f;
    const float fail_hold_time = 0.6f;          // 실패 뒤 레이저가 그대로 있는 시간
    static readonly Color resist_color = new Color(1f, 0.9f, 0.5f);   // 연타할 때 용사 쪽 금빛

    // 지시문. 유저가 뭘 해야 하는지 바로 읽히게 힘 모으기부터 띄운다.
    const string instruction_text = "Y 연타!!  레이저를 밀어내라!!";
    const float instruction_popup_time = 3f;

    Phase phase;
    float phase_timer;

    Vector2 from;
    Vector2 stand;
    float sign;

    int mash_count;
    float mash_remaining;
    float pushed_back;          // 0 → 1. 레이저가 드래곤 쪽으로 밀린 정도. 누르면 오르고 안 누르면 내려간다. 1 이면 승리
    float last_grow = 1f;       // 마지막 스텝의 뻗기 진행도. Mash 가 같은 프레임에 그림을 갱신할 때 쓴다

    AudioSource laser_sound;    // 레이저 소리. 승리·실패 때 끊는다
    const float laser_sound_fade = 0.15f;

    GameObject beam;
    SpriteRenderer beam_outer;
    SpriteRenderer beam_core;
    Glow_light beam_glow;
    Glow_light mouth_glow;
    float beam_length;

    // 프리팹 그림을 쓸 때의 색. 몸통(띠)·심(세모)의 원래 색을 프리팹에서 읽고, 빛은 심의 색을 따른다. 프리팹이 없으면 Dragon.final_color.
    Color outer_color;
    Color core_color;
    Color light_color;
    bool core_is_apex_art;      // 심이 세모 프리팹이면 꼭짓점을 입에 두고 화면 끝으로 넓어지게 놓는다

    public State_final(Dragon dragon) : base(dragon) { }

    // 무적. HP 1 에 묶여 있고 이 패턴 중에는 맞지 않는다.
    public override bool is_invincible
    {
        get { return true; }
    }

    public override void Enter()
    {
        from = dragon.position;
        sign = dragon.FacingSign();

        // 화면 가운데 높이에서 쏜다. 레이저가 위아래로 화면을 거의 다 덮는다.
        // 제자리에서 용사 반대쪽(왼쪽)으로 살짝 물러나 레이저가 화면을 더 길게 가로지르게.
        stand = new Vector2(dragon.base_position.x - sign * dragon.final_stand_offset_x, Camera_director.RestPosition().y);

        phase = Phase.Charge;
        phase_timer = 0f;
        mash_count = 0;
        mash_remaining = dragon.final_mash_time;
        pushed_back = 0f;

        // 색. 심 프리팹이 있으면 그 색이 빛 색이다.
        core_color = PrefabColor(dragon.final_core_prefab, dragon.final_core_color);
        light_color = dragon.final_core_prefab != null ? core_color : dragon.final_color;
        light_color.a = 1f;

        // 몸통 띠(breath_two_last)는 심(breath)과 같은 색으로 그린다. 프리팹 색이 서로 달라도 한 줄기로 이어져 보이게.
        // 심 프리팹이 없을 때만 띠 프리팹의 색을 쓴다.
        outer_color = dragon.final_core_prefab != null ? core_color : PrefabColor(dragon.final_beam_prefab, dragon.final_color);
        core_is_apex_art = dragon.final_core_prefab != null;

        mouth_glow = Scene_lighting.Attach(null, light_color, 0f, 1f);
        mouth_glow.flicker_amount = 0.2f;

        dragon.FlashWhite(0.25f);

        // 뭘 해야 하는지 먼저 알린다. 힘 모으는 동안 배너에 크게, 용사 머리 위에도. 쏘는 동안에도 배너 지시문은 계속 남는다.
        Hud.Banner(instruction_text, dragon.final_charge_time + 1f);
        if (dragon.player != null) {
            Popup_text.ShowAbove(dragon.player, "Y 연타!!", instruction_popup_time, resist_color);
        }

        Debug.Log("마지막 패턴 - 레이저 힘 모으기 (HP 1 고정, Y 연타로 " + dragon.final_mash_time + "초 안에 밀어내기)");
    }

    // 레이저 소리를 짧게 줄여 끈다. 이미 끝났거나 없으면 아무것도 안 한다.
    void StopLaserSound()
    {
        if (laser_sound == null) {
            return;
        }

        Audio_util.FadeOut(laser_sound, laser_sound_fade);
        laser_sound = null;
    }

    public override void Exit()
    {
        dragon.StopAnimationClip();
        StopLaserSound();

        if (beam != null) {
            Object.Destroy(beam);
        }
        if (beam_glow != null) {
            Object.Destroy(beam_glow.gameObject);
        }
        if (mouth_glow != null) {
            Object.Destroy(mouth_glow.gameObject);
        }
    }

    // Player 가 Y 를 누를 때마다 부른다. 쏘는 동안만. 한 번 누르면 레이저가 드래곤 쪽으로 조금 밀린다(줄다리기).
    public void Mash()
    {
        // 힘 모으기 중에 누른 것도 버리지 않는다. 미리 밀어 둔 만큼 레이저가 짧게 나온다. 되돌아가는 건 쏘기 시작한 뒤부터.
        if (phase != Phase.Fire && phase != Phase.Charge) {
            return;
        }

        mash_count++;
        pushed_back = Mathf.Clamp01(pushed_back + dragon.final_push_per_press);

        // 누른 그 프레임에 바로 줄어든다. 다음 물리 스텝을 기다리지 않는다.
        if (phase == Phase.Fire) {
            LayoutBeam(last_grow * (1f - pushed_back));
        }
        ShowMashBanner();

        // 누른 손맛. 화면이 살짝 당겨지고 용사 쪽에 금빛이 번쩍.
        Camera_director.ZoomPunch(0.02f, 0.12f);
        if (dragon.player != null) {
            Scene_lighting.Flash(dragon.player.position, resist_color, 0.8f + pushed_back, 2f + pushed_back * 2f, 0.18f);
        }

        // 끝까지 밀어냈다. 쏘는 중일 때만. 힘 모으기 중에 1 이 되면 쏘는 순간 바로 이긴다.
        if (pushed_back >= 1f && phase == Phase.Fire) {
            BeginWin();
        }
    }

    // 지금 입 위치.
    Vector2 Mouth()
    {
        return dragon.position + new Vector2(dragon.fireball_offset_x * sign, dragon.breath_mouth_offset_y);
    }

    public override void FixedTick(float dt)
    {
        phase_timer += dt;

        switch (phase) {
            case Phase.Charge:
                TickCharge();
                break;
            case Phase.Fire:
                TickFire(dt);
                break;
            case Phase.Win:
                TickWin();
                break;
            case Phase.Fail:
                TickFail();
                break;
        }
    }

    void TickCharge()
    {
        float total = Mathf.Max(0.01f, dragon.final_charge_time);
        float t = Mathf.Clamp01(phase_timer / total);

        dragon.MoveTo(Vector2.Lerp(from, stand, Dragon.EaseOut(t)));

        // 입에 빛이 모이고, 땅이 점점 떨린다.
        mouth_glow.transform.position = Mouth();
        mouth_glow.Set(light_color, mouth_glow_max_intensity * t, 1f + (mouth_glow_max_radius - 1f) * t);
        Camera_director.Shake(dragon.final_shake_amplitude * 0.4f * t, 0.2f);

        if (t >= 1f) {
            BeginFire();
        }
    }

    void BeginFire()
    {
        phase = Phase.Fire;
        phase_timer = 0f;

        dragon.PlayAnimationClip("breath", true);

        // 입에서 화면 반대쪽 끝을 지나도록.
        Vector2 mouth = Mouth();
        float far_x = sign > 0f ? World_scroll.RightX() : World_scroll.LeftX();
        beam_length = Mathf.Abs(far_x - mouth.x) + beam_margin;

        beam = new GameObject("Final_beam");
        beam_outer = MakeBeamLayer("Outer", dragon.final_beam_prefab, outer_color, outer_alpha, beam_order);
        beam_core = MakeBeamLayer("Core", dragon.final_core_prefab, core_color, 1f, beam_order + 1);

        beam_glow = Scene_lighting.AttachShape(null, light_color, glow_intensity, 0.01f, dragon.final_beam_thickness, glow_falloff);

        Camera_director.Flash(new Color(1f, 1f, 1f, 0.7f), 0.3f);
        Camera_director.ZoomPunch(0.08f, 0.5f);

        // 레이저 발사 소리 (Resources/sound_assets/Dragon_last_breath). 승리·실패 순간에 끊는다.
        laser_sound = Sound_bank.Play("last_breath_sound", dragon.transform.position);

        LayoutBeam(0f);

        Debug.Log("마지막 패턴 - 레이저 발사! Y 연타로 밀어내기, 제한 " + dragon.final_mash_time + "초");
    }

    // 프리팹의 SpriteRenderer 색. 없으면 fallback.
    static Color PrefabColor(GameObject prefab, Color fallback)
    {
        if (prefab == null) {
            return fallback;
        }

        SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>();
        return renderer != null ? renderer.color : fallback;
    }

    // 레이저 한 겹. 프리팹이 있으면 그걸 복제해 그림·색을 그대로 쓰고, 없으면 임시 원. 둘 다 조명을 안 받는 Unlit.
    SpriteRenderer MakeBeamLayer(string layer_name, GameObject prefab, Color color, float alpha, int order)
    {
        GameObject holder;
        SpriteRenderer renderer;

        if (prefab != null) {
            holder = Object.Instantiate(prefab, beam.transform);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            renderer = holder.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null) {
                renderer = holder.AddComponent<SpriteRenderer>();
                renderer.sprite = Placeholder_sprite.Circle();
            }
        }
        else {
            holder = new GameObject();
            holder.transform.SetParent(beam.transform, false);
            renderer = holder.AddComponent<SpriteRenderer>();
            renderer.sprite = Placeholder_sprite.Circle();
        }

        holder.name = layer_name;

        Color c = color;
        c.a = alpha;
        renderer.color = c;
        renderer.sortingOrder = order;
        Scene_lighting.ApplyUnlitMaterial(renderer);
        return renderer;
    }

    void TickFire(float dt)
    {
        dragon.MoveTo(stand);
        mash_remaining -= dt;

        // 안 누르면 레이저가 다시 늘어난다. 뻗는 첫 순간(grow)이 끝난 뒤부터.
        float grow = dragon.final_beam_grow_time > 0f ? Mathf.Clamp01(phase_timer / dragon.final_beam_grow_time) : 1f;
        last_grow = grow;
        if (grow >= 1f) {
            pushed_back = Mathf.Max(0f, pushed_back - dragon.final_push_decay * dt);
        }

        // 밀린 만큼 화면 끝에서부터 드래곤 쪽으로 짧아진다.
        LayoutBeam(grow * (1f - pushed_back));

        // 쏘는 동안 계속 흔들린다. 연타로 밀릴수록 조금 약해진다.
        Camera_director.Shake(dragon.final_shake_amplitude * (1f - 0.5f * pushed_back), 0.2f);

        ShowMashBanner();

        // 힘 모으기 중에 이미 끝까지 밀어 뒀으면 쏘는 순간 이긴다.
        if (pushed_back >= 1f) {
            BeginWin();
            return;
        }

        if (mash_remaining <= 0f) {
            BeginFail();
        }
    }

    // 진행률·남은 시간 배너. 매 스텝과 누를 때마다 갱신한다.
    void ShowMashBanner()
    {
        int seconds_left = Mathf.CeilToInt(Mathf.Max(0f, mash_remaining));
        int percent = Mathf.RoundToInt(pushed_back * 100f);
        Hud.Banner(instruction_text + "   " + percent + "%   (" + seconds_left + "초)", 0.4f);
    }

    // 레이저를 입에서 화면 끝 쪽으로 factor(0→1) 비율 길이만큼. 연타로 밀리면 factor 가 줄어 드래곤 쪽으로 짧아지고, 가늘고 옅어진다.
    void LayoutBeam(float grow)
    {
        if (beam == null) {
            return;
        }

        Vector2 mouth = Mouth();
        float length = Mathf.Max(0.05f, beam_length * grow);
        float squeeze = Mathf.Lerp(1f, pushed_back_min, pushed_back);
        float thickness = dragon.final_beam_thickness * squeeze;

        Vector2 anchor = new Vector2(sign > 0f ? 0f : 1f, 0.5f);

        // 두 겹의 투명도를 같게. 프리팹 그림이면 둘 다 꽉 채우고, 밀린 만큼 같이 옅어진다.
        float layer_alpha = (core_is_apex_art ? 1f : outer_alpha) * squeeze;
        Color outer = outer_color;
        outer.a = layer_alpha;
        Color core = core_color;
        core.a = core_is_apex_art ? layer_alpha : 1f;
        beam_core.color = core;

        if (core_is_apex_art) {
            // 심(breath): 그림의 좁은 끝(final_core_art_angle 방향)이 입을 향하게 돌리고, final_cone_length 만큼 넓어진다. 끝 폭은 몸통 두께.
            float cone_length = Mathf.Max(0.05f, Mathf.Min(dragon.final_cone_length, length));
            float toward_mouth = sign > 0f ? 180f : 0f;                     // 화면 끝에서 입을 보는 방향
            float art = dragon.final_core_art_angle;
            beam_core.transform.rotation = Quaternion.Euler(0f, 0f, toward_mouth - art);

            // 그림의 어느 축이 레이저 방향인지에 따라 폭·길이를 넣는 축과 입에 붙일 가장자리가 다르다.
            bool up_or_down = Mathf.Abs(Mathf.DeltaAngle(art, 90f)) < 45f || Mathf.Abs(Mathf.DeltaAngle(art, -90f)) < 45f;
            Vector2 core_anchor;
            if (up_or_down) {
                Sprite_fit.FitSize(beam_core, thickness, cone_length);
                core_anchor = new Vector2(0.5f, Mathf.Abs(Mathf.DeltaAngle(art, 90f)) < 45f ? 1f : 0f);
            }
            else {
                Sprite_fit.FitSize(beam_core, cone_length, thickness);
                core_anchor = new Vector2(Mathf.Abs(Mathf.DeltaAngle(art, 0f)) < 45f ? 1f : 0f, 0.5f);
            }
            Sprite_fit.AlignPoint(beam_core.transform, beam_core, core_anchor, mouth);

            // 띠(breath_two_last): 세모의 밑변에서 이어져 화면 끝까지. 딱 맞닿게 두면 가장자리 안티앨리어싱 틈으로
            // 뒤의 라이트가 한 줄 새어 보이므로, 세모 밑으로 layer_overlap 만큼 겹쳐 넣는다. 세모가 위에 그려져서 겹친 부분은 안 보인다.
            float overlap = Mathf.Min(layer_overlap, cone_length);
            float band_length = Mathf.Max(0.05f, length - cone_length + overlap);
            Vector2 band_start = mouth + new Vector2(sign * (cone_length - overlap), 0f);
            beam_outer.transform.rotation = Quaternion.identity;
            Sprite_fit.FitSize(beam_outer, band_length, thickness);
            Sprite_fit.AlignPoint(beam_outer.transform, beam_outer, anchor, band_start);
            beam_outer.enabled = length > cone_length + 0.05f;
        }
        else {
            // 임시 원: 넓은 띠 위에 가늘고 밝은 심을 겹친다.
            beam_outer.transform.rotation = Quaternion.identity;
            Sprite_fit.FitSize(beam_outer, length, thickness);
            Sprite_fit.AlignPoint(beam_outer.transform, beam_outer, anchor, mouth);
            beam_outer.enabled = true;

            beam_core.transform.rotation = Quaternion.identity;
            Sprite_fit.FitSize(beam_core, length, thickness * core_thickness_ratio);
            Sprite_fit.AlignPoint(beam_core.transform, beam_core, anchor, mouth);
        }

        beam_outer.color = outer;

        if (beam_glow != null) {
            beam_glow.transform.position = mouth + new Vector2(sign * length * 0.5f, 0f);
            beam_glow.SetRect(length, thickness);
            beam_glow.Set(light_color, glow_intensity * squeeze);
        }

        if (mouth_glow != null) {
            mouth_glow.transform.position = mouth;
            mouth_glow.Set(light_color, mouth_glow_max_intensity * squeeze, mouth_glow_max_radius);
        }
    }

    void BeginWin()
    {
        phase = Phase.Win;
        phase_timer = 0f;

        // 레이저 소리를 끄고 격추 소리가 나게.
        StopLaserSound();

        Hud.Banner("!!!", 1f);
        Camera_director.Flash(new Color(1f, 1f, 1f, 0.8f), 0.4f);
        Camera_director.Shake(0.4f, 0.5f);

        Debug.Log("마지막 패턴 - 연타 성공! 드래곤 격추");
    }

    void TickWin()
    {
        dragon.MoveTo(stand);

        // 레이저가 꺼지면 죽는다. Die 가 상태를 바꾸므로 Exit 가 나머지를 치운다.
        float total = Mathf.Max(0.01f, dragon.final_beam_fade_time);
        float t = Mathf.Clamp01(phase_timer / total);
        FadeBeam(1f - t);

        if (t >= 1f) {
            dragon.FinalKill();
        }
    }

    void BeginFail()
    {
        phase = Phase.Fail;
        phase_timer = 0f;

        // 레이저 소리를 끄고 패배 소리가 나게.
        StopLaserSound();

        Hud.Banner("……", 1f);
        Camera_director.Flash(new Color(1f, 1f, 1f, 0.9f), 0.6f);

        Player_health health = dragon.PlayerHealth();
        if (health != null) {
            health.KillInstantly();
        }

        Debug.Log("마지막 패턴 - 연타 실패 (" + Mathf.RoundToInt(pushed_back * 100f) + "% 까지, " + mash_count + "번 누름). 게임 오버");
    }

    void TickFail()
    {
        dragon.MoveTo(stand);

        // 잠깐 그대로 덮고 있다가 꺼진다. 게임은 이미 끝났다.
        if (phase_timer < fail_hold_time) {
            return;
        }

        float total = Mathf.Max(0.01f, dragon.final_beam_fade_time);
        float t = Mathf.Clamp01((phase_timer - fail_hold_time) / total);
        FadeBeam(1f - t);

        if (t >= 1f) {
            dragon.ChangeState(new State_idle(dragon));
        }
    }

    // 레이저와 빛을 alpha 비율로 옅게. 0 이면 안 보인다.
    void FadeBeam(float alpha)
    {
        // 두 겹이 같은 비율로 옅어진다. 프리팹 그림이면 둘 다 1 에서, 임시 원이면 띠만 outer_alpha 에서.
        if (beam_outer != null) {
            Color c = beam_outer.color;
            c.a = (core_is_apex_art ? 1f : outer_alpha) * alpha;
            beam_outer.color = c;
        }
        if (beam_core != null) {
            Color c = beam_core.color;
            c.a = alpha;
            beam_core.color = c;
        }
        if (beam_glow != null) {
            beam_glow.Set(light_color, glow_intensity * alpha);
        }
        if (mouth_glow != null) {
            mouth_glow.Set(light_color, mouth_glow_max_intensity * alpha);
        }
    }

    public override Color GetColor(float pulse)
    {
        if (phase == Phase.Charge) {
            return dragon.Tint(light_color, pulse);
        }

        return dragon.Tint(light_color, 1f);
    }
}
