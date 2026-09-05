using UnityEngine;

// 스프라이트 몇 장을 정해진 fps 로 돌리는 가장 단순한 플립북. Animator 없이 코드로 돈다.
// 바닥 불(firefloor)처럼 "그냥 계속 도는" 그림(loop)과, 큰 화염구처럼 "한 번 돌고 마지막 프레임에 머무는" 그림(loop = false)에 쓴다.
// 상태에 따라 바뀌는 드래곤·용사는 Dragon_animation / Player_animation 이 따로 맡는다.
// 히트스톱·슬로우모션에는 같이 느려진다(deltaTime). transform 은 건드리지 않으므로 크기·배치는 부른 쪽이 정한다.
public class Sprite_flipbook : MonoBehaviour
{

    SpriteRenderer sprite_renderer;
    Sprite[] frames;
    float fps = 12f;
    float timer;
    int index;
    bool loop = true;

    // renderer 에 frames 를 fps 로 무한 반복한다. 프레임이 없으면 붙이지 않고 null. 이미 붙어 있으면 프레임만 바꾼다.
    public static Sprite_flipbook Attach(SpriteRenderer renderer, Sprite[] frames, float fps)
    {
        return Attach(renderer, frames, fps, true);
    }

    // loop 가 false 면 한 번 돌고 마지막 프레임에 머문다.
    public static Sprite_flipbook Attach(SpriteRenderer renderer, Sprite[] frames, float fps, bool loop)
    {
        if (renderer == null || frames == null || frames.Length == 0) {
            return null;
        }

        Sprite_flipbook flipbook = renderer.gameObject.GetComponent<Sprite_flipbook>();
        if (flipbook == null) {
            flipbook = renderer.gameObject.AddComponent<Sprite_flipbook>();
        }

        flipbook.sprite_renderer = renderer;
        flipbook.frames = frames;
        flipbook.fps = fps > 0f ? fps : 12f;
        flipbook.loop = loop;
        flipbook.timer = 0f;
        flipbook.index = 0;

        renderer.sprite = frames[0];
        return flipbook;
    }

    void Update()
    {
        if (sprite_renderer == null || frames == null || frames.Length < 2) {
            return;
        }

        int last = frames.Length - 1;
        if (!loop && index >= last) {
            return;   // 한 번짜리는 마지막 프레임에 머문다
        }

        timer += Time.deltaTime * fps;
        if (timer < 1f) {
            return;
        }

        // 프레임이 길어 여러 장을 건너뛰어야 하면 그만큼 넘긴다. 반복이면 끝에서 처음으로, 아니면 끝에서 멈춘다.
        int steps = Mathf.FloorToInt(timer);
        timer -= steps;
        index = loop ? (index + steps) % frames.Length : Mathf.Min(last, index + steps);
        sprite_renderer.sprite = frames[index];
    }
}
