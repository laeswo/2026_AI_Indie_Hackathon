using System.IO;
using UnityEditor;
using UnityEngine;

// 선택한 스프라이트 텍스처에서 노멀맵을 만들어 붙인다.
// 사용: Project 창에서 텍스처(여러 개 가능) 선택 → 메뉴 Hwang › 선택한 텍스처 노멀맵 만들기
// 하는 일:
//   1. 알파(가장자리 두께)와 밝기를 섞어 높이맵을 만들고 Sobel 로 노멀을 뽑아 <이름>_normal.png 로 저장
//   2. 그 파일의 임포트 타입을 Normal map 으로
//   3. 원본 텍스처의 Secondary Textures 에 "_NormalMap" 으로 연결 → Sprite-Lit-Default 가 자동으로 쓴다
// 그림이 평면 그림이라 완벽한 입체는 아니고 "가장자리가 둥글게 빛을 받는" 정도. 진짜 툴(Laigter 등)로 만든 게 있으면 그걸 연결해도 된다.
public static class Normal_map_tool
{

    const string secondary_name = "_NormalMap";
    const float strength = 2.2f;        // 기울기 세기. 클수록 굴곡이 깊어 보인다
    const int blur_passes = 2;          // 높이맵을 몇 번 뭉갤지. 픽셀아트면 1

    [MenuItem("Hwang/선택한 텍스처 노멀맵 만들기")]
    static void Generate()
    {
        int done = 0;

        foreach (Object selected in Selection.objects) {
            Texture2D texture = selected as Texture2D;
            if (texture == null) {
                continue;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) {
                continue;
            }

            if (Process(path)) {
                done++;
            }
        }

        if (done == 0) {
            Debug.LogWarning("노멀맵: Project 창에서 텍스처를 선택한 뒤 실행하세요.");
        }
        else {
            Debug.Log("노멀맵 " + done + "개 생성·연결 완료");
        }
    }

    static bool Process(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) {
            return false;
        }

        // Read/Write 설정과 무관하게 원본 파일을 직접 읽는다.
        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(File.ReadAllBytes(path))) {
            Debug.LogWarning("노멀맵: 읽을 수 없는 이미지 " + path);
            return false;
        }

        Texture2D normal = Build(source);

        string normal_path = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_normal.png").Replace('\\', '/');
        File.WriteAllBytes(normal_path, normal.EncodeToPNG());
        Object.DestroyImmediate(source);
        Object.DestroyImmediate(normal);

        AssetDatabase.ImportAsset(normal_path, ImportAssetOptions.ForceUpdate);

        TextureImporter normal_importer = AssetImporter.GetAtPath(normal_path) as TextureImporter;
        if (normal_importer != null) {
            normal_importer.textureType = TextureImporterType.NormalMap;
            normal_importer.sRGBTexture = false;
            normal_importer.filterMode = importer.filterMode;
            normal_importer.SaveAndReimport();
        }

        Texture2D normal_asset = AssetDatabase.LoadAssetAtPath<Texture2D>(normal_path);
        Attach(importer, normal_asset);
        return true;
    }

    // 원본의 Secondary Textures 에 _NormalMap 으로 넣는다. 이미 있으면 바꿔 끼운다.
    static void Attach(TextureImporter importer, Texture2D normal)
    {
        SecondarySpriteTexture[] current = importer.secondarySpriteTextures ?? new SecondarySpriteTexture[0];
        bool replaced = false;

        for (int i = 0; i < current.Length; i++) {
            if (current[i].name == secondary_name) {
                current[i].texture = normal;
                replaced = true;
            }
        }

        if (!replaced) {
            SecondarySpriteTexture[] grown = new SecondarySpriteTexture[current.Length + 1];
            current.CopyTo(grown, 0);
            grown[current.Length] = new SecondarySpriteTexture { name = secondary_name, texture = normal };
            current = grown;
        }

        importer.secondarySpriteTextures = current;
        importer.SaveAndReimport();
    }

    // 높이 = 알파 0.7 + 밝기 0.3. 알파 덕에 가장자리가 경사가 되어 둥글게 보인다.
    static Texture2D Build(Texture2D source)
    {
        int w = source.width;
        int h = source.height;
        Color32[] pixels = source.GetPixels32();

        float[] height = new float[w * h];
        for (int i = 0; i < pixels.Length; i++) {
            Color32 c = pixels[i];
            float alpha = c.a / 255f;
            float luma = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            height[i] = alpha * (0.7f + 0.3f * luma);
        }

        for (int pass = 0; pass < blur_passes; pass++) {
            height = Blur(height, w, h);
        }

        Color32[] result = new Color32[w * h];
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                float left = Sample(height, w, h, x - 1, y);
                float right = Sample(height, w, h, x + 1, y);
                float down = Sample(height, w, h, x, y - 1);
                float up = Sample(height, w, h, x, y + 1);

                Vector3 n = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;

                result[y * w + x] = new Color32(
                    (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f),
                    255);
            }
        }

        Texture2D normal = new Texture2D(w, h, TextureFormat.RGBA32, false);
        normal.SetPixels32(result);
        normal.Apply();
        return normal;
    }

    static float Sample(float[] height, int w, int h, int x, int y)
    {
        x = Mathf.Clamp(x, 0, w - 1);
        y = Mathf.Clamp(y, 0, h - 1);
        return height[y * w + x];
    }

    static float[] Blur(float[] height, int w, int h)
    {
        float[] output = new float[height.Length];
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                float sum = 0f;
                for (int dy = -1; dy <= 1; dy++) {
                    for (int dx = -1; dx <= 1; dx++) {
                        sum += Sample(height, w, h, x + dx, y + dy);
                    }
                }
                output[y * w + x] = sum / 9f;
            }
        }
        return output;
    }
}
