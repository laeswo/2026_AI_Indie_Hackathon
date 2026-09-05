using System.Collections.Generic;
using UnityEngine;

// 캐릭터 머리 위에 뜨는 짧은 대사 데이터. 대사표가 채워지면 여기 한 줄씩만 바꾸면 된다.
// 제약: 한 줄 12자 이내. 톤은 "진지한 판타지 문장인데 내용이 어이없는" 코미디.
//
// 줍기 대사를 고르는 순서 (PickupLine):
//   1. Crop_data.dialogue_id 가 적혀 있으면 그 id 의 대사
//   2. 아니면 프리팹 이름(인스턴스 이름에서 "(Clone)" 뗀 것)으로 id 목록을 찾아 랜덤
//   3. 없으면 null. 아무것도 안 띄운다
// 나중에 ob_normal 이 돌/냄비/닭/벽돌/물통 프리팹으로 갈라지면 각 프리팹 Crop_data 에 id 하나만 적으면 고정된다.
public static class Dialogue_table
{

    // id → 대사 후보. 후보가 여러 개면 랜덤.
    static readonly Dictionary<string, string[]> lines = new Dictionary<string, string[]>
    {
        // 튜토리얼 토스트 (상단 한 줄, 20자 이내). 조작 안내만. 농담 금지. 실제 키 기준
        { "tuto_aim",   new[] { "Y를 꾹 눌러 조준" } },
        { "tuto_throw", new[] { "Y를 떼서 던지기" } },
        { "tuto_jump",  new[] { "스페이스로 점프, 길게 누르면 높이" } },

        { "pickup_sword",   new[] { "던지기 딱 좋은 검이다" } },
        { "pickup_bell",    new[] { "누가 종을 여기에" } },      // 표에 초안 없음, 임시
        { "pickup_spear",   new[] { "관통하기 좋아보이는 창이야" } },
        { "pickup_axe",     new[] { "묵직한 도끼다" } },        // 표에 초안 없음, 임시
        { "pickup_potion",  new[] { "이건 마시는 건데" } },     // 표에 초안 없음, 임시
        { "pickup_rare",    new[] { "이건 꽤나 묵직한데…" } },
        { "pickup_pot",     new[] { "왜 바닥에 냄비가" } },
        { "pickup_chicken", new[] { "이게 뭐야!" } },
        { "pickup_brick",   new[] { "아파도 참아라!" } },
        { "pickup_stone",   new[] { "돌은 돌이다" } },          // 표에 초안 없음, 임시
        { "pickup_water",   new[] { "출렁출렁" } },             // 표에 초안 없음, 임시
        { "pickup_chair",   new[] { "앉을 시간은 없다" } },      // 표에 초안 없음, 임시

        // 성검 이벤트
        { "pickup_holysword", new[] { "손이 떨린다…" } },         // 표에 초안 없음, 임시
        { "smith_02",         new[] { "용사…!! 내 희대의 역작이…!!!" } },   // 대장장이. 성검을 던진 직후
    };

    // id → 여러 줄 대사 (순서대로 전부 보여준다). 전체화면 인트로처럼 줄이 쌓이는 것. 한 줄 25자 이내.
    static readonly Dictionary<string, string[]> sequences = new Dictionary<string, string[]>
    {
        // 용사. 게임 시작. 습격 상황 전달 + 용사가 정상이 아니라는 첫 신호
        { "intro_01", new[] {
            "잠에서 깨니 드래곤이 마을을 습격하고 있었어",
            "모두가 나에게 도와달라 하지만",
            "난 손에 쥔 걸 전부 던져버린다고",
        } },

        // 대장장이. 성검을 들고 등장할 때 (드래곤 HP 40% 이하). 한 줄 30자 이내
        { "smith_01", new[] {
            "용사… 이건 전설의 성검…",
            "절대로 던지지 말게나",
        } },
    };

    // 프리팹 이름 → id 후보. ob_normal 은 여러 물건을 한 프리팹으로 쓰므로 그중 랜덤.
    static readonly Dictionary<string, string[]> prefab_ids = new Dictionary<string, string[]>
    {
        { "ob_sword",    new[] { "pickup_sword", "pickup_bell" } },
        { "ob_drill",    new[] { "pickup_spear" } },
        { "ob_axe",      new[] { "pickup_axe" } },
        { "ob_hp_drink", new[] { "pickup_potion" } },
        { "ob_bomb",     new[] { "pickup_rare" } },
        { "ob_normal",   new[] { "pickup_pot", "pickup_chicken", "pickup_brick", "pickup_stone", "pickup_water", "pickup_chair" } },
    };

    // 아이템을 주웠을 때 띄울 대사. 없으면 null.
    public static string PickupLine(Crop_data data)
    {
        if (data == null) {
            return null;
        }

        if (!string.IsNullOrEmpty(data.dialogue_id)) {
            return Line(data.dialogue_id);
        }

        string[] ids;
        if (!prefab_ids.TryGetValue(PrefabName(data.gameObject.name), out ids) || ids.Length == 0) {
            return null;
        }

        return Line(ids[Random.Range(0, ids.Length)]);
    }

    // id 의 여러 줄 대사 전부 (순서 유지). sequences 에 없으면 lines 의 후보 배열을 그대로 돌려준다. 없는 id 면 null.
    public static string[] Lines(string id)
    {
        if (id == null) {
            return null;
        }

        string[] result;
        if (sequences.TryGetValue(id, out result) || lines.TryGetValue(id, out result)) {
            return result;
        }

        return null;
    }

    // id 의 대사 중 하나 (한 줄짜리). 없는 id 면 null.
    public static string Line(string id)
    {
        string[] candidates;
        if (id == null || !lines.TryGetValue(id, out candidates) || candidates.Length == 0) {
            return null;
        }

        return candidates[Random.Range(0, candidates.Length)];
    }

    // "ob_normal (Clone)" / "ob_normal(Clone)" → "ob_normal"
    static string PrefabName(string instance_name)
    {
        if (string.IsNullOrEmpty(instance_name)) {
            return "";
        }

        int clone = instance_name.IndexOf("(Clone)");
        if (clone >= 0) {
            instance_name = instance_name.Substring(0, clone);
        }

        return instance_name.Trim();
    }
}
