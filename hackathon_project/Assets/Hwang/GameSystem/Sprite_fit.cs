using UnityEngine;

// 그림(스프라이트)의 크기·피벗·방향에 기대지 않고 배치하기 위한 도우미 모음. 전부 정적 메서드다.
// 원칙:
//   - 크기는 sprite.bounds(유닛)를 재서 "원하는 유닛 크기 / 실제 크기" 로 scale 을 정한다. 1×1 유닛이라는 가정은 없다.
//   - 배치는 렌더 사각형의 특정 점(밑변 가운데, 꼭짓점 등)을 원하는 월드 좌표에 맞춘다. 피벗이 어디든 같은 결과가 난다.
//   - 늘려야 하는 띠는 Tiled 그리기(drawMode)를 먼저 시도하고, 스프라이트가 Full Rect 가 아니면 scale 로 폴백한다.
//   - 그림이 없는 슬롯은 마지막 대체로 임시 원을 넣고 반드시 경고한다.
//   - 그림은 오른쪽을 본다는 전제. 반대로 그린 그림은 art_faces_left 로 알려주면 flipX 를 반대로 건다.
//   - Animator 는 있으면 트리거를 쏘고 없으면 아무것도 안 한다.
public static class Sprite_fit
{

    // ---------- 슬롯이 비었을 때 ----------

    // 스프라이트가 비어 있으면 임시 원을 넣고 경고한다. 임시 원이 들어갔으면 true.
    public static bool EnsureSprite(SpriteRenderer renderer, Color placeholder_color, string owner, string slot)
    {
        if (renderer == null || renderer.sprite != null) {
            return false;
        }

        Debug.LogWarning(owner + " : " + slot + " 에 그림이 없어서 임시 원으로 그립니다. 스프라이트를 넣어 주세요.");

        renderer.sprite = Placeholder_sprite.Circle();
        renderer.color = placeholder_color;

        // 코드로 만든 렌더러는 기본 머티리얼이 빛을 안 받는다. 조명을 받게 바꿔 준다.
        Scene_lighting.ApplyLitMaterial(renderer);
        return true;
    }

    // ---------- 크기 재기 ----------

    // 렌더 사각형(피벗 기준 로컬 좌표, scale 1). Tiled/Sliced 면 renderer.size 로 만든 사각형이고, flip 도 반영한다.
    public static Rect LocalRect(SpriteRenderer renderer)
    {
        Sprite sprite = renderer != null ? renderer.sprite : null;
        if (sprite == null) {
            return new Rect(-0.5f, -0.5f, 1f, 1f);
        }

        Rect rect;
        if (renderer.drawMode == SpriteDrawMode.Simple) {
            Bounds bounds = sprite.bounds;
            rect = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        }
        else {
            // Tiled/Sliced 는 피벗 비율을 유지한 채 size 만큼 펼쳐진다.
            Vector2 pivot_ratio = new Vector2(0.5f, 0.5f);
            if (sprite.rect.width > 0f && sprite.rect.height > 0f) {
                pivot_ratio = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            }

            Vector2 size = renderer.size;
            rect = new Rect(-pivot_ratio.x * size.x, -pivot_ratio.y * size.y, size.x, size.y);
        }

        // flip 은 피벗을 축으로 뒤집는다.
        if (renderer.flipX) {
            rect = new Rect(-rect.xMax, rect.yMin, rect.width, rect.height);
        }
        if (renderer.flipY) {
            rect = new Rect(rect.xMin, -rect.yMax, rect.width, rect.height);
        }

        return rect;
    }

    // 렌더된 월드 크기. 회전은 무시하고 scale 만 반영한다.
    public static Vector2 WorldSize(SpriteRenderer renderer)
    {
        if (renderer == null) {
            return Vector2.one;
        }

        Rect rect = LocalRect(renderer);
        Vector3 scale = renderer.transform.lossyScale;
        return new Vector2(rect.width * Mathf.Abs(scale.x), rect.height * Mathf.Abs(scale.y));
    }

    // 렌더 사각형의 (u, v) 지점의 월드 좌표. (0,0) = 왼쪽 아래, (0.5,0) = 밑변 가운데, (0.5,1) = 윗변 가운데.
    // 회전·피벗·부모 scale·자식 오프셋을 전부 반영한다.
    public static Vector3 WorldPoint(SpriteRenderer renderer, Vector2 anchor)
    {
        Rect rect = LocalRect(renderer);
        Vector3 local = new Vector3(rect.xMin + rect.width * anchor.x, rect.yMin + rect.height * anchor.y, 0f);
        return renderer.transform.TransformPoint(local);
    }

    // 월드 AABB. 회전이 있으면 감싸는 상자. renderer.bounds 와 달리 꺼져 있어도 정확하다.
    public static Bounds WorldBounds(SpriteRenderer renderer)
    {
        Vector3 a = WorldPoint(renderer, new Vector2(0f, 0f));
        Vector3 b = WorldPoint(renderer, new Vector2(1f, 0f));
        Vector3 c = WorldPoint(renderer, new Vector2(0f, 1f));
        Vector3 d = WorldPoint(renderer, new Vector2(1f, 1f));

        Bounds bounds = new Bounds(a, Vector3.zero);
        bounds.Encapsulate(b);
        bounds.Encapsulate(c);
        bounds.Encapsulate(d);
        return bounds;
    }

    // ---------- 크기 맞추기 ----------

    // 렌더 폭·높이를 유닛으로 맞춘다. Tiled/Sliced 면 size 를, 아니면 localScale 을 만진다. 기존 scale 의 부호는 유지한다.
    public static void FitSize(SpriteRenderer renderer, float width, float height)
    {
        if (renderer == null || renderer.sprite == null) {
            return;
        }

        Transform t = renderer.transform;

        if (renderer.drawMode != SpriteDrawMode.Simple) {
            Vector3 lossy = t.lossyScale;
            renderer.size = new Vector2(width / Safe(Mathf.Abs(lossy.x)), height / Safe(Mathf.Abs(lossy.y)));
            return;
        }

        Vector3 unit = renderer.sprite.bounds.size;
        Vector3 parent = ParentScale(t);
        Vector3 scale = t.localScale;

        scale.x = Mathf.Sign(scale.x) * width / Safe(unit.x * Mathf.Abs(parent.x));
        scale.y = Mathf.Sign(scale.y) * height / Safe(unit.y * Mathf.Abs(parent.y));
        t.localScale = scale;
    }

    public static void FitWidth(SpriteRenderer renderer, float width)
    {
        if (renderer == null || renderer.sprite == null) {
            return;
        }

        FitSize(renderer, width, WorldSize(renderer).y);
    }

    public static void FitHeight(SpriteRenderer renderer, float height)
    {
        if (renderer == null || renderer.sprite == null) {
            return;
        }

        FitSize(renderer, WorldSize(renderer).x, height);
    }

    // 균등 배율로 긴 쪽이 diameter 유닛이 되게 맞춘다. 원형 그림이면 지름이 곧 diameter 다.
    public static void FitDiameter(SpriteRenderer renderer, float diameter)
    {
        if (renderer == null || renderer.sprite == null) {
            return;
        }

        Transform t = renderer.transform;
        Vector3 unit = renderer.sprite.bounds.size;
        Vector3 parent = ParentScale(t);
        Vector3 scale = t.localScale;

        float longest = Mathf.Max(unit.x, unit.y);
        float k = diameter / Safe(longest * Mathf.Abs(parent.x));

        scale.x = Mathf.Sign(scale.x) * k;
        scale.y = Mathf.Sign(scale.y) * k;
        t.localScale = scale;
    }

    // 띠처럼 늘려야 하는 그림. 반복(Tiled)이 되면 size 로 늘리고 true, 안 되면 false(부른 쪽이 FitSize 로 폴백).
    // 프리팹에서 이미 Tiled/Sliced 로 두었으면 그대로 쓰고, Simple 이면 Full Rect 메시(꼭짓점 4개)일 때만 바꾼다.
    public static bool TryTile(SpriteRenderer renderer, float width, float height)
    {
        if (renderer == null || renderer.sprite == null) {
            return false;
        }

        if (renderer.drawMode == SpriteDrawMode.Simple) {
            if (!CanTile(renderer.sprite)) {
                return false;
            }

            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
        }

        FitSize(renderer, width, height);
        return true;
    }

    // Tight 메시는 타일링하면 깨진다. Full Rect 로 임포트한 스프라이트는 꼭짓점이 4개다.
    public static bool CanTile(Sprite sprite)
    {
        return sprite != null && sprite.vertices != null && sprite.vertices.Length == 4;
    }

    // ---------- 배치 ----------

    // 렌더 사각형의 (u, v) 지점이 world_point 에 오도록 root 를 옮긴다. z 는 건드리지 않는다.
    public static void AlignPoint(Transform root, SpriteRenderer renderer, Vector2 anchor, Vector2 world_point)
    {
        if (root == null || renderer == null) {
            return;
        }

        Vector3 current = WorldPoint(renderer, anchor);
        Vector3 position = root.position;
        position.x += world_point.x - current.x;
        position.y += world_point.y - current.y;
        root.position = position;
    }

    // 밑변이 bottom_y 에 닿게 root 를 위아래로만 옮긴다.
    public static void AlignBottomTo(Transform root, SpriteRenderer renderer, float bottom_y)
    {
        if (root == null || renderer == null) {
            return;
        }

        Vector3 current = WorldPoint(renderer, new Vector2(0.5f, 0f));
        Vector3 position = root.position;
        position.y += bottom_y - current.y;
        root.position = position;
    }

    // ---------- 색 ----------

    // 틴트는 항상 원래 색(base) 기준으로 섞는다. t = 0 이면 정확히 base 로 돌아간다.
    public static void Tint(SpriteRenderer renderer, Color base_color, Color target, float t)
    {
        if (renderer == null) {
            return;
        }

        renderer.color = Color.Lerp(base_color, target, Mathf.Clamp01(t));
    }

    // ---------- 방향 ----------

    // 진행 방향(+1 오른쪽, -1 왼쪽)을 보게 뒤집는다. 그림이 왼쪽을 보고 그려졌으면 art_faces_left 로 알려준다. 0 이면 그대로.
    public static void Face(SpriteRenderer renderer, float direction_sign, bool art_faces_left)
    {
        if (renderer == null || direction_sign == 0f) {
            return;
        }

        renderer.flipX = (direction_sign < 0f) != art_faces_left;
    }

    // 지금 그림의 "앞" 이 로컬 -x 를 보고 있는지. 속도 방향으로 회전시킬 때 180도를 더할지 정한다.
    public static bool FrontIsNegativeX(SpriteRenderer renderer, bool art_faces_left)
    {
        bool flipped = renderer != null && renderer.flipX;
        return art_faces_left != flipped;
    }

    // 속도 방향을 보게 회전시킨다. flipX 상태와 art_faces_left 를 반영해서 앞이 진행 방향을 향한다.
    public static void RotateToward(Transform target, SpriteRenderer renderer, Vector2 velocity, bool art_faces_left)
    {
        RotateToward(target, renderer, velocity, art_faces_left, 0f);
    }

    // art_angle: 그림의 앞이 그려진 방향(도). 그만큼 빼서 앞이 진행 방향을 보게 한다. art_faces_left 면 180 으로 친다.
    public static void RotateToward(Transform target, SpriteRenderer renderer, Vector2 velocity, bool art_faces_left, float art_angle)
    {
        if (target == null || velocity.sqrMagnitude < 0.0001f) {
            return;
        }

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        if (FrontIsNegativeX(renderer, art_faces_left)) {
            angle += 180f;
        }
        else {
            angle -= art_angle;
        }

        target.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    // ---------- 애니메이션 훅 ----------

    // Animator 가 없거나 그 파라미터가 없으면 조용히 넘어간다. 그림 없이도 똑같이 돌아가야 한다.
    public static void Trigger(Animator animator, string name)
    {
        if (!HasParameter(animator, name, AnimatorControllerParameterType.Trigger)) {
            return;
        }

        animator.SetTrigger(name);
    }

    public static void SetBool(Animator animator, string name, bool value)
    {
        if (!HasParameter(animator, name, AnimatorControllerParameterType.Bool)) {
            return;
        }

        animator.SetBool(name, value);
    }

    public static void SetInt(Animator animator, string name, int value)
    {
        if (!HasParameter(animator, name, AnimatorControllerParameterType.Int)) {
            return;
        }

        animator.SetInteger(name, value);
    }

    // Animator 마다 "타입:이름" 파라미터 목록을 한 번만 만들어 둔다. animator.parameters 는 부를 때마다 배열을 새로 만들어서
    // 매 프레임(grounded) 부르면 쓰레기가 쌓인다. 파라미터는 실행 중에 바뀌지 않으니 캐시해도 된다.
    static readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<string>> parameter_cache
        = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<string>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache()
    {
        parameter_cache.Clear();
    }

    static bool HasParameter(Animator animator, string name, AnimatorControllerParameterType type)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) {
            return false;
        }

        int id = animator.GetInstanceID();
        System.Collections.Generic.HashSet<string> names;

        if (!parameter_cache.TryGetValue(id, out names)) {
            names = new System.Collections.Generic.HashSet<string>();
            foreach (AnimatorControllerParameter parameter in animator.parameters) {
                names.Add(Key(parameter.type, parameter.name));
            }
            parameter_cache[id] = names;
        }

        return names.Contains(Key(type, name));
    }

    static string Key(AnimatorControllerParameterType type, string name)
    {
        return (int)type + ":" + name;
    }

    // ---------- 내부 ----------

    static Vector3 ParentScale(Transform t)
    {
        return t.parent != null ? t.parent.lossyScale : Vector3.one;
    }

    static float Safe(float value)
    {
        return Mathf.Abs(value) < 0.0001f ? 0.0001f : value;
    }
}
