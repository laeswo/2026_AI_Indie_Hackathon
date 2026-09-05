using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 조명 총괄. URP 2D Renderer 의 Light2D 를 코드로 만든다. 씬에 라이트를 안 놓아도 된다.
//   Ensure()                     처음 부를 때 하나 생긴다. 어둑한 저녁 분위기(글로벌 라이트)와 Bloom 을 깐다
//   Attach(부모, 색, 세기, 반경[, illuminate])  점 라이트. 부모가 있으면 따라다니고, null 이면 부른 쪽이 위치를 맞춘다.
//                                illuminate 면 Multiply(진짜 조명), 아니면 Additive(스스로 빛나는 것)
//   AttachShape(부모, 색, 세기, 폭, 높이, falloff)  사각형 라이트. 띠처럼 네모난 것에
//   Flash(위치, 색, 세기, 반경, 시간)  그 자리에서 번쩍하고 사라지는 라이트
//   ApplyLitMaterial(렌더러)      코드로 만든 SpriteRenderer 가 빛을 받게 Sprite-Lit 머티리얼을 준다
//   SetPhase(페이즈)              글로벌 라이트를 페이즈 분위기로 서서히 바꾼다 (2페이즈: 붉고 더 어둡게). 순간 번쩍 포함
//   AddShadowCaster(오브젝트)     스프라이트 크기만 한 ShadowCaster2D. 바닥 불빛에 그림자가 늘어진다
//   EnableDragonRim(드래곤, on)   드래곤 뒤에서 붉게 비추는 림 라이트 (2페이즈)
//
// 분위기: 배경·작물은 글로벌 라이트로 어둑하게 깔리고, 불(화염구·브레스·충격파)과 용사·드래곤 주변만 밝다.
// 점 라이트는 Additive 블렌드(Renderer2D 의 두 번째 스타일)라 어두운 그림 위에서도 빛이 보이고, 1을 넘으면 Bloom 이 번진다.
// 씬 템플릿이 놓아둔 "Global Light 2D" 가 있으면 그걸 쓰고 값만 바꾼다. 없으면 만든다.
public class Scene_lighting : MonoBehaviour
{

    // 어둑한 저녁. 0.6 이면 배경이 읽히면서 불빛이 도드라진다. 1 이면 조명 없는 것과 같다.
    internal float ambient_intensity = 0.6f;
    internal Color ambient_color = new Color(0.72f, 0.78f, 0.96f);   // 살짝 푸른 저녁빛

    // 라이트 공통. 가운데는 진하고 가장자리로 부드럽게.
    internal float glow_falloff = 0.6f;
    const int multiply_blend_style = 0;   // Renderer2D 의 "Multiply". 진짜 조명. 밑에 있는 걸 밝힌다(가로등 원뿔)
    const int additive_blend_style = 1;   // Renderer2D 의 "Additive". 스스로 빛나는 것(불, 전구). 1을 넘으면 Bloom 이 번진다

    // Bloom. 밝은 불빛이 번진다. 카메라 포스트 프로세싱을 켠다. 과하면 뿌옇다.
    internal bool enable_bloom = true;
    internal float bloom_intensity = 0.5f;
    internal float bloom_threshold = 1.1f;
    internal float bloom_scatter = 0.6f;
    internal float vignette_intensity = 0.28f;   // 가장자리 어둡게. 집중감

    // 2페이즈 분위기. 붉고 조금 더 어둡다. 배경 틴트(Background)와 겹쳐 "저녁이 됐다".
    internal Color phase2_ambient_color = new Color(0.95f, 0.62f, 0.55f);
    internal float phase2_ambient_intensity = 0.5f;
    internal float phase_lerp_time = 1.5f;
    internal float phase_flash_strength = 1.8f;  // 진입 순간 글로벌 라이트가 이 배까지 밝아졌다 가라앉는다
    internal float phase_flash_time = 0.6f;

    // 림 라이트 (드래곤 뒤)
    internal Color rim_color = new Color(1f, 0.32f, 0.25f);
    internal float rim_intensity = 1.4f;
    internal float rim_radius = 3.2f;

    // 카메라 배경색(하늘)은 라이트를 안 받는다. 스프라이트만 어두워지면 하늘이 튀므로 같은 비율로 어둡게 한다. 끝나면 되돌린다.
    internal bool dim_camera_background = true;

    static Scene_lighting instance;
    static Material lit_material;

    Light2D global_light;
    Volume volume;
    Camera dimmed_camera;
    Color original_background;

    // 페이즈 보간
    Color ambient_from;
    Color ambient_to;
    float ambient_intensity_from;
    float ambient_intensity_to;
    float phase_lerp = 1f;          // 1 이면 보간 끝
    float flash_remaining;

    // 도메인 리로드를 꺼둔 에디터에서도 플레이할 때마다 초기화되게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        instance = null;
        lit_material = null;
        unlit_material = null;
        unlit_shared = false;
    }

    // ---------- 밖에서 부르는 것 ----------

    public static Scene_lighting Ensure()
    {
        if (instance != null) {
            return instance;
        }

        instance = FindFirstObjectByType<Scene_lighting>();
        if (instance != null) {
            return instance;
        }

        GameObject holder = new GameObject("Scene_lighting");
        instance = holder.AddComponent<Scene_lighting>();
        return instance;
    }

    // 점 라이트(스스로 빛나는 것)를 만든다. parent 가 있으면 그 자리에 붙어 따라다닌다(부모 scale 은 Glow_light 가 보정한다).
    public static Glow_light Attach(Transform parent, Color color, float intensity, float radius)
    {
        return Attach(parent, color, intensity, radius, false);
    }

    // illuminate 가 true 면 Multiply 블렌드. 빛 자체는 안 보이고 그 안에 있는 것들이 밝아진다(가로등 원뿔).
    public static Glow_light Attach(Transform parent, Color color, float intensity, float radius, bool illuminate)
    {
        Scene_lighting lighting = Ensure();

        Light2D light = MakeLight(parent, "Glow", illuminate);
        light.lightType = Light2D.LightType.Point;
        light.falloffIntensity = lighting.glow_falloff;
        light.pointLightInnerRadius = 0f;

        Glow_light glow = light.gameObject.AddComponent<Glow_light>();
        glow.Init(light);
        glow.Set(color, intensity, radius);
        return glow;
    }

    // 사각형 라이트. 띠처럼 네모난 것(브레스 바닥 불)은 원으로 비추면 모서리가 어두우니 이걸 쓴다.
    // 중심이 오브젝트 위치이고, 가장자리 falloff 만큼 바깥으로 부드럽게 번진다. 크기는 Glow_light.SetRect 로 바꾼다.
    public static Glow_light AttachShape(Transform parent, Color color, float intensity, float width, float height, float falloff)
    {
        Scene_lighting lighting = Ensure();

        Light2D light = MakeLight(parent, "Glow_rect", false);
        light.lightType = Light2D.LightType.Freeform;
        light.falloffIntensity = lighting.glow_falloff;
        light.shapeLightFalloffSize = Mathf.Max(0f, falloff);

        Glow_light glow = light.gameObject.AddComponent<Glow_light>();
        glow.Init(light);
        glow.SetRect(width, height);
        glow.Set(color, intensity);
        return glow;
    }

    static Light2D MakeLight(Transform parent, string holder_name, bool illuminate)
    {
        GameObject holder = new GameObject(holder_name);
        if (parent != null) {
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = Vector3.zero;
        }

        Light2D light = holder.AddComponent<Light2D>();
        light.blendStyleIndex = illuminate ? multiply_blend_style : additive_blend_style;
        light.shadowsEnabled = false;
        return light;
    }

    // 그 자리에서 번쩍하고 duration 동안 꺼지며 사라진다.
    public static Glow_light Flash(Vector3 position, Color color, float intensity, float radius, float duration)
    {
        Glow_light glow = Attach(null, color, intensity, radius);
        glow.name = "Glow_flash";
        glow.transform.position = new Vector3(position.x, position.y, 0f);
        glow.FadeOut(duration);
        return glow;
    }

    // 조명을 안 받는 머티리얼. 주울 수 있는 것(작물)은 어두운 세계에서도 선명해야 눈에 띈다.
    //
    // 반드시 URP 2D 전용 Sprite-Unlit-Default 를 쓴다. 빌트인 Sprites/Default 는 2D Renderer 에서 SRP Batcher 와 맞지 않아
    // 같은 머티리얼을 공유하는 스프라이트들이 한 배치로 묶이면서 첫 스프라이트의 텍스처로 전부 그려진다(모든 아이템이 같은 그림).
    // 2D 전용 셰이더는 스프라이트 텍스처를 렌더러마다 따로 넘기므로 공유해도 안전하다 (프로젝트 기본 Sprite-Unlit-Default 머티리얼과 같은 방식).
    // 그 셰이더를 못 찾을 때만 Sprites/Default 로 폴백하는데, 그때는 공유하지 않고 렌더러마다 인스턴스를 준다. 작물 수가 적어 배칭 손해는 없다.
    static Material unlit_material;
    static bool unlit_shared;

    public static void ApplyUnlitMaterial(SpriteRenderer renderer)
    {
        if (renderer == null) {
            return;
        }

        if (unlit_material == null) {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            unlit_shared = shader != null;

            if (shader == null) {
                shader = Shader.Find("Sprites/Default");
                if (shader == null) {
                    Debug.LogWarning("Scene_lighting : Unlit 스프라이트 셰이더를 찾지 못해 머티리얼을 그대로 둡니다.");
                    return;
                }
                Debug.LogWarning("Scene_lighting : Sprite-Unlit-Default 셰이더가 없어서 Sprites/Default 로 폴백합니다. 텍스처가 섞이지 않게 렌더러마다 따로 만듭니다.");
            }

            unlit_material = new Material(shader);
            unlit_material.name = "Sprite-Unlit (코드)";
        }

        if (unlit_shared) {
            renderer.sharedMaterial = unlit_material;
        }
        else {
            renderer.material = new Material(unlit_material);
        }
    }

    public static void ApplyLitMaterial(SpriteRenderer renderer)
    {
        if (renderer == null) {
            return;
        }

        Material lit = LitMaterial();
        if (lit != null) {
            renderer.sharedMaterial = lit;
        }
    }

    public static Material LitMaterial()
    {
        if (lit_material != null) {
            return lit_material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (shader != null) {
            lit_material = new Material(shader);
            lit_material.name = "Sprite-Lit (코드)";
            return lit_material;
        }

        // 셰이더를 못 찾으면 씬에서 이미 빛을 받는 스프라이트의 셰이더를 빌려 새 머티리얼을 만든다.
        // 그 렌더러의 머티리얼을 그대로 공유하면(인스턴스 머티리얼에 _MainTex 가 박혀 있을 수 있다) 텍스처가 섞일 수 있어서 복제한다.
        foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)) {
            Material material = renderer.sharedMaterial;
            if (material != null && material.shader != null && material.shader.name.Contains("Sprite-Lit")) {
                lit_material = new Material(material.shader);
                lit_material.name = "Sprite-Lit (코드, 빌린 셰이더)";
                return lit_material;
            }
        }

        Debug.LogWarning("Scene_lighting : Sprite-Lit-Default 셰이더를 찾지 못해 코드로 만든 임시 도형이 빛을 안 받습니다.");
        return null;
    }

    // 글로벌 라이트를 페이즈 분위기로 phase_lerp_time 에 걸쳐 바꾼다. 카메라 배경도 같이. 진입 순간 한 번 번쩍.
    public static void SetPhase(int phase)
    {
        Scene_lighting lighting = Ensure();

        lighting.ambient_from = lighting.global_light != null ? lighting.global_light.color : lighting.ambient_color;
        lighting.ambient_intensity_from = lighting.global_light != null ? lighting.global_light.intensity : lighting.ambient_intensity;
        lighting.ambient_to = phase >= 2 ? lighting.phase2_ambient_color : lighting.ambient_color;
        lighting.ambient_intensity_to = phase >= 2 ? lighting.phase2_ambient_intensity : lighting.ambient_intensity;
        lighting.phase_lerp = 0f;
        lighting.flash_remaining = lighting.phase_flash_time;
    }

    // 스프라이트 크기만 한 네모 그림자. 정확한 실루엣은 아니지만 바닥 불빛에 그림자가 늘어지는 느낌은 난다.
    public static ShadowCaster2D AddShadowCaster(GameObject target)
    {
        if (target == null) {
            return null;
        }

        ShadowCaster2D existing = target.GetComponent<ShadowCaster2D>();
        if (existing != null) {
            return existing;
        }

        SpriteRenderer renderer = target.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) {
            return null;
        }

        ShadowCaster2D caster = target.AddComponent<ShadowCaster2D>();
        caster.castsShadows = true;
        caster.selfShadows = false;

        // 모양은 공개 API 가 없어서 리플렉션으로 넣는다. 안 되면 기본 네모라도 남는다.
        Bounds local = renderer.sprite.bounds;
        Vector3 scale = renderer.transform.lossyScale;
        float half_w = local.extents.x * Mathf.Abs(scale.x);
        float half_h = local.extents.y * Mathf.Abs(scale.y);
        Vector3 offset = renderer.transform.position - target.transform.position;

        Vector3[] path = {
            offset + new Vector3(-half_w, -half_h, 0f),
            offset + new Vector3(-half_w, half_h, 0f),
            offset + new Vector3(half_w, half_h, 0f),
            offset + new Vector3(half_w, -half_h, 0f),
        };

        try {
            FieldInfo path_field = typeof(ShadowCaster2D).GetField("m_ShapePath", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo hash_field = typeof(ShadowCaster2D).GetField("m_ShapePathHash", BindingFlags.NonPublic | BindingFlags.Instance);
            if (path_field != null) {
                path_field.SetValue(caster, path);
            }
            if (hash_field != null) {
                hash_field.SetValue(caster, Random.Range(int.MinValue, int.MaxValue));
            }
        }
        catch (System.Exception e) {
            Debug.LogWarning("ShadowCaster2D 모양을 못 넣었습니다. 기본 모양을 씁니다: " + e.Message);
        }

        return caster;
    }

    // 2페이즈 림 라이트. 드래곤 뒤에서 붉게 비춰 실루엣 가장자리가 빛난다. 그림자도 드리운다.
    public static void EnableDragonRim(Transform dragon, bool on)
    {
        if (dragon == null) {
            return;
        }

        Transform existing = dragon.Find("Rim_light");

        if (!on) {
            if (existing != null) {
                Destroy(existing.gameObject);
            }
            return;
        }

        if (existing != null) {
            return;
        }

        Scene_lighting lighting = Ensure();
        Glow_light glow = Attach(dragon, lighting.rim_color, lighting.rim_intensity, lighting.rim_radius, true);
        glow.name = "Rim_light";
        glow.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        glow.flicker_amount = 0.2f;
        glow.flicker_speed = 3f;

        Light2D light = glow.GetComponent<Light2D>();
        if (light != null) {
            light.shadowsEnabled = true;
            light.shadowIntensity = 0.6f;
        }
    }

    // ---------- 준비 ----------

    void Awake()
    {
        if (instance == null) {
            instance = this;
        }

        SetupGlobalLight();
        SetupBloom();
        DimCameraBackground();

        ambient_from = ambient_to = ambient_color;
        ambient_intensity_from = ambient_intensity_to = ambient_intensity;
    }

    void Update()
    {
        if (global_light == null) {
            return;
        }

        float dt = Time.unscaledDeltaTime;

        if (phase_lerp < 1f) {
            phase_lerp = Mathf.Min(1f, phase_lerp + dt / Mathf.Max(0.01f, phase_lerp_time));
        }

        Color color = Color.Lerp(ambient_from, ambient_to, phase_lerp);
        float intensity = Mathf.Lerp(ambient_intensity_from, ambient_intensity_to, phase_lerp);

        // 진입 순간 번쩍. 선형으로 가라앉는다.
        if (flash_remaining > 0f) {
            flash_remaining = Mathf.Max(0f, flash_remaining - dt);
            float t = phase_flash_time > 0f ? flash_remaining / phase_flash_time : 0f;
            intensity *= Mathf.Lerp(1f, phase_flash_strength, t);
        }

        global_light.color = color;
        global_light.intensity = intensity;

        // 하늘(카메라 배경)도 같은 비율로.
        if (dimmed_camera != null && dim_camera_background) {
            Color dimmed = original_background * color * Mathf.Min(1f, intensity);
            dimmed.a = original_background.a;
            dimmed_camera.backgroundColor = dimmed;
        }
    }

    void DimCameraBackground()
    {
        if (!dim_camera_background) {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) {
            return;
        }

        dimmed_camera = cam;
        original_background = cam.backgroundColor;

        Color dimmed = original_background * ambient_color * ambient_intensity;
        dimmed.a = original_background.a;
        cam.backgroundColor = dimmed;
    }

    // 씬에 글로벌 라이트가 있으면(2D 템플릿이 하나 놓아둔다) 그걸 쓰고, 없으면 만든다. 값은 여기 분위기로 덮어쓴다.
    void SetupGlobalLight()
    {
        foreach (Light2D light in FindObjectsByType<Light2D>(FindObjectsSortMode.None)) {
            if (light.lightType == Light2D.LightType.Global) {
                global_light = light;
                break;
            }
        }

        if (global_light == null) {
            GameObject holder = new GameObject("Global Light 2D");
            holder.transform.SetParent(transform, false);
            global_light = holder.AddComponent<Light2D>();
            global_light.lightType = Light2D.LightType.Global;
        }

        global_light.color = ambient_color;
        global_light.intensity = ambient_intensity;
    }

    // Bloom 볼륨을 코드로 만들고 카메라 포스트 프로세싱을 켠다. 카메라가 없으면 건너뛴다.
    void SetupBloom()
    {
        if (!enable_bloom) {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) {
            return;
        }

        UniversalAdditionalCameraData camera_data = cam.GetUniversalAdditionalCameraData();
        if (camera_data != null) {
            camera_data.renderPostProcessing = true;
        }

        GameObject holder = new GameObject("Post_volume");
        holder.transform.SetParent(transform, false);

        volume = holder.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = bloom_intensity;
        bloom.threshold.value = bloom_threshold;
        bloom.scatter.value = bloom_scatter;

        // 가장자리를 어둡게. 가운데(용사·드래곤)로 시선이 모인다.
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.value = vignette_intensity;
        vignette.smoothness.value = 0.4f;

        volume.profile = profile;
    }

    void OnDestroy()
    {
        if (volume != null && volume.profile != null) {
            Destroy(volume.profile);
        }

        if (dimmed_camera != null) {
            dimmed_camera.backgroundColor = original_background;
        }

        if (instance == this) {
            instance = null;
        }
    }
}
