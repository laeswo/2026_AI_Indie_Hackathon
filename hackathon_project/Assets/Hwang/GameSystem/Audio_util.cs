using UnityEngine;

// 소리 한 번 틀기. 어디서든 Audio_util.PlayAt(clip, position) 한 줄로 부른다.
// 임시 AudioSource 를 만들어 틀고, 다 끝나면 스스로 지운다. clip 이 비어 있으면 아무것도 안 한다.
// 씬이 바뀌어도 살아남는다(DontDestroyOnLoad). 버튼 클릭음처럼 누른 직후 씬이 바뀌는 소리가 끊기지 않게.
// 효과음 id 로 부르는 쪽은 Sound_bank, BGM 은 Music_player 가 맡는다.
public static class Audio_util
{

    public static AudioSource PlayAt(AudioClip clip, Vector3 position)
    {
        return PlayAt(clip, position, 1f, 0f);
    }

    // volume 0~1, delay 초 뒤 재생. 돌려주는 AudioSource 는 중간에 끊을 때(FadeOut) 쓴다. 안 쓰면 끝나고 알아서 사라진다.
    public static AudioSource PlayAt(AudioClip clip, Vector3 position, float volume, float delay)
    {
        if (clip == null) {
            return null;
        }

        GameObject holder = new GameObject("Audio (" + clip.name + ")");
        holder.transform.position = position;
        Object.DontDestroyOnLoad(holder);

        AudioSource source = holder.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = 0f;   // 2D 게임이라 거리 감쇠 없이 그대로 들리게

        if (delay > 0f) {
            source.PlayDelayed(delay);
        }
        else {
            source.Play();
        }

        // 재생이 끝난 직후에 지운다. 여유를 조금 둬서 끝이 잘리지 않게.
        Object.Destroy(holder, delay + clip.length + 0.1f);
        return source;
    }

    // 재생 중인 소리를 seconds 동안 줄여서 끈다. 뚝 끊기면 틱 소리가 나서 아주 짧게라도 페이드한다. 이미 끝났으면 아무것도 안 한다.
    public static void FadeOut(AudioSource source, float seconds)
    {
        if (source == null) {
            return;
        }

        Audio_fade fade = source.gameObject.GetComponent<Audio_fade>();
        if (fade == null) {
            fade = source.gameObject.AddComponent<Audio_fade>();
        }
        fade.Begin(source, seconds);
    }
}
