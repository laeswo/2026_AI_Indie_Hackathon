using System.Collections.Generic;
using UnityEngine;

// 효과음 창고. Resources/sound_assets 의 파일을 id 로 부른다. 어디서든 Sound_bank.Play("hurt_sound", 위치) 한 줄.
// id → 파일 이름 표는 사운드 목록 문서와 같다. 파일이 여럿인 id(dragon_roar_sound)는 부를 때마다 하나를 랜덤으로 고른다.
// 파일이 없으면 경고를 한 번만 찍고 조용히 넘어간다. 소리가 없어도 게임은 돌아야 한다.
public static class Sound_bank
{

    const string folder = "sound_assets";

    // 효과음 볼륨. BGM 은 Music_player 가 따로 정한다.
    public const float sfx_volume = 1f;

    static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>
    {
        // 아이템이 드래곤·큰 화염구에 맞을 때. 어떤 아이템이 어떤 소리인지는 HitSoundFor 가 정한다
        { "dull_sound",        new[] { "Dull_hit" } },            // 돌·의자·벽돌·폭발통(안 터짐)
        { "metal_sound",       new[] { "Metal_hit" } },           // 냄비·종·대포알(안 터짐)
        { "water_sound",       new[] { "Water_hit" } },           // 물통
        { "chicken_sound",     new[] { "Chicken_hit" } },         // 닭
        { "crash_sound",       new[] { "Bottle_hit" } },          // 생명포션
        { "coldweapon_sound",  new[] { "Coldweapon_hit" } },      // 검·창·도끼
        { "explosion_sound",   new[] { "Explosion" } },           // 폭발
        { "holy_sound",        new[] { "Holy" } },                // 성검

        // 용사
        { "pickup_sound",      new[] { "Pickup" } },
        { "throw_sound",       new[] { "Throw" } },
        { "jump_start_sound",  new[] { "Player_jump_start" } },
        { "jump_end_sound",    new[] { "Player_jump_end" } },
        { "hurt_sound",        new[] { "Player_hurt" } },
        { "die_sound",         new[] { "Player_die" } },

        // 드래곤
        { "dragon_roar_sound", new[] { "Dragon_roar_1", "Dragon_roar_2" } },   // 공격 전조
        { "flame_sound",       new[] { "Dragon_fireball" } },                  // 브레스
        { "phase_roar_sound",  new[] { "Dragon_phase2_roar" } },
        { "last_breath_sound", new[] { "Dragon_last_breath" } },              // 마지막 패턴 레이저 발사
        { "dragon_die_sound",  new[] { "Dragon_die" } },

        // UI · 결과 · BGM
        { "click_sound",       new[] { "UI_click" } },
        { "clear_sound",       new[] { "Victory" } },
        { "defeat_sound",      new[] { "Defeat" } },
        { "main_bgm",          new[] { "Main_bgm" } },
        { "battle_bgm",        new[] { "Battle_bgm" } },
    };

    static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();
    static readonly HashSet<string> warned = new HashSet<string>();

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다. Resources 에셋은 남지만 캐시는 새로 채운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        cache.Clear();
        warned.Clear();
    }

    // id 의 클립 하나. 여럿이면 랜덤. 없으면 null (경고는 한 번만).
    public static AudioClip Get(string id)
    {
        AudioClip[] clips = Load(id);
        if (clips == null || clips.Length == 0) {
            return null;
        }

        return clips.Length == 1 ? clips[0] : clips[Random.Range(0, clips.Length)];
    }

    public static AudioSource Play(string id)
    {
        return Play(id, Vector3.zero, 0f);
    }

    public static AudioSource Play(string id, Vector3 position)
    {
        return Play(id, position, 0f);
    }

    // delay 초 뒤에 튼다. 사망음 뒤에 결과 징글을 이어 붙일 때.
    // 돌려주는 AudioSource 는 긴 소리를 중간에 끊을 때(Audio_util.FadeOut) 쓴다. 파일이 없으면 null.
    public static AudioSource Play(string id, Vector3 position, float delay)
    {
        AudioClip clip = Get(id);
        if (clip == null) {
            return null;
        }

        return Audio_util.PlayAt(clip, position, sfx_volume, delay);
    }

    // ---------- 아이템 ----------

    // 아이템이 맞았을 때. 프리팹 슬롯(hit_sound)에 직접 넣은 클립이 있으면 그걸, 없으면 아이템 종류로 고른다.
    // 폭발한 경우는 부르지 말 것 — 폭발음은 Crop_flow.Explode 가 낸다.
    public static void PlayHit(Crop_data data, Vector3 position)
    {
        if (data != null && data.hit_sound != null) {
            Audio_util.PlayAt(data.hit_sound, position, sfx_volume, 0f);
            return;
        }

        Play(HitSoundFor(data), position);
    }

    // 던질 때. 프리팹 슬롯(throw_sound)이 있으면 그걸, 없으면 공용 던지기 소리.
    public static void PlayThrow(Crop_data data, Vector3 position)
    {
        if (data != null && data.throw_sound != null) {
            Audio_util.PlayAt(data.throw_sound, position, sfx_volume, 0f);
            return;
        }

        Play("throw_sound", position);
    }

    // 아이템 종류 → 히트음 id. 변형 아이템(돌류·검·종·성검)은 dialogue_id 로, 나머지는 표시 이름으로 본다.
    // Crop_data 가 없는 예전 작물은 둔탁한 소리.
    public static string HitSoundFor(Crop_data data)
    {
        if (data == null) {
            return "dull_sound";
        }

        switch (data.dialogue_id) {
            case "pickup_stone":
            case "pickup_chair":
            case "pickup_brick":
                return "dull_sound";
            case "pickup_pot":
            case "pickup_bell":
                return "metal_sound";
            case "pickup_water":
                return "water_sound";
            case "pickup_chicken":
                return "chicken_sound";
            case "pickup_sword":
                return "coldweapon_sound";
            case Holy_sword.dialogue_id:
                return "holy_sound";
        }

        switch (data.display_name) {
            case "돌":
            case "의자":
            case "벽돌":
            case "폭발통":
            case "폭발 나무통":
                return "dull_sound";
            case "냄비":
            case "종":
            case "대포알":
                return "metal_sound";
            case "물통":
                return "water_sound";
            case "닭":
                return "chicken_sound";
            case "생명포션":
                return "crash_sound";
            case "검":
            case "창":
            case "도끼":
                return "coldweapon_sound";
            case "성검":
                return "holy_sound";
        }

        return "dull_sound";
    }

    // ---------- 내부 ----------

    static AudioClip[] Load(string id)
    {
        AudioClip[] clips;
        if (cache.TryGetValue(id, out clips)) {
            return clips;
        }

        string[] files;
        if (!table.TryGetValue(id, out files)) {
            Warn("사운드 id '" + id + "' 가 Sound_bank 표에 없습니다.");
            cache[id] = null;
            return null;
        }

        List<AudioClip> loaded = new List<AudioClip>();
        foreach (string file in files) {
            AudioClip clip = Resources.Load<AudioClip>(folder + "/" + file);
            if (clip != null) {
                loaded.Add(clip);
            }
            else {
                Warn("Resources/" + folder + "/" + file + " 을 찾지 못했습니다. (" + id + ")");
            }
        }

        clips = loaded.Count > 0 ? loaded.ToArray() : null;
        cache[id] = clips;
        return clips;
    }

    static void Warn(string message)
    {
        if (warned.Add(message)) {
            Debug.LogWarning("Sound_bank : " + message);
        }
    }
}
