using UnityEngine;

// 아직 그림이 없는 오브젝트에 임시로 씌울 원형 스프라이트. Dragon 이 그림 없을 때 쓴다.
public static class Placeholder_sprite
{

    static Sprite circle;

    public static Sprite Circle()
    {
        if (circle != null) {
            return circle;
        }

        int size = 64;
        float radius = size * 0.5f - 1f;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);

                // 가장자리 1픽셀만 부드럽게 해서 계단이 안 보이게 한다.
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        // pixelsPerUnit 을 size 로 두면 지름이 딱 1 유닛이 된다. 크기는 scale 로 조절한다.
        circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return circle;
    }
}
