using UnityEngine;

// 작물(던질 것)의 게임 수치. 프리팹마다 붙여서 값을 다르게 준다.
// 안 붙이면 Dragon 의 default_damage 를 쓴다.
public class Crop_data : MonoBehaviour
{

    public string display_name = "작물";
    public int damage = 10;
}
