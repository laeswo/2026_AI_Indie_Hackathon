using UnityEngine;

// AudioSource 하나를 몇 초 동안 줄여서 끄고 오브젝트를 지운다. Audio_util.FadeOut 이 붙인다.
// 히트스톱(timeScale 0) 중에도 꺼져야 하므로 unscaled 로 잰다.
public class Audio_fade : MonoBehaviour
{

    AudioSource source;
    float start_volume;
    float duration;
    float remaining;

    public void Begin(AudioSource target, float seconds)
    {
        source = target;
        start_volume = target != null ? target.volume : 0f;
        duration = Mathf.Max(0.01f, seconds);
        remaining = duration;
    }

    void Update()
    {
        if (source == null) {
            Destroy(gameObject);
            return;
        }

        remaining -= Time.unscaledDeltaTime;
        if (remaining <= 0f) {
            source.Stop();
            Destroy(gameObject);
            return;
        }

        source.volume = start_volume * (remaining / duration);
    }
}
