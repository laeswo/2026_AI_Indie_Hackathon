using UnityEngine;
using UnityEngine.UI;

// 시작 화면의 UI 껍데기. Resources/Start_menu_ui.prefab 의 루트에 붙어 있고, 슬롯에 UI 요소가 연결돼 있다.
// 동작(시작·종료·키 입력)은 Start_menu 가 한다. 이 컴포넌트는 참조만 들고 있다.
// 배치·색·그림을 바꾸려면 프리팹을 열어 에디터에서 만지면 된다.
public class Start_menu_view : MonoBehaviour
{

    public Image background_image;
    public Text title_text;
    public Image title_image;       // 제목 그림. 평소 꺼둔다. Start_menu 가 title_sprite 를 받으면 켠다
    public Text subtitle_text;
    public Text hint_text;
    public Button start_button;     // OnClick 은 비워둬라. Start_menu 가 연결한다
    public Button quit_button;

    // 프리팹을 아무 씬에나 드래그해 놓아도 동작하게, 동작 담당(Start_menu)이 없으면 스스로 붙인다.
    void Start()
    {
        if (FindFirstObjectByType<Start_menu>() == null) {
            gameObject.AddComponent<Start_menu>();
        }
    }
}
