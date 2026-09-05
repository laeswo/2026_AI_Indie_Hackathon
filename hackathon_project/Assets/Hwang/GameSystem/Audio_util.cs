using UnityEngine;

// 소리 한 번 틀기. 어디서든 Audio_util.PlayAt(clip, position) 한 줄로 부른다.
// 임시 AudioSource 를 만들어 틀고, 다 끝나면 스스로 지운다. clip 이 비어 있으면 아무것도 안 한다.
public static class Audio_util
{

    public static void PlayAt(AudioClip clip, Vector3 position)
    {
        if (clip == null) {
            return;
        }

        GameObject holder = new GameObject("Audio (" + clip.name + ")");
        holder.transform.position = position;

        AudioSource source = holder.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 0f;   // 2D 게임이라 거리 감쇠 없이 그대로 들리게
        source.Play();

        // 재생이 끝난 직후에 지운다. 여유를 조금 둬서 끝이 잘리지 않게.
        Object.Destroy(holder, clip.length + 0.1f);
    }
}
