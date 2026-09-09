using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using PixelCrushers.DialogueSystem;
using PixelCrushers;

[RequireComponent(typeof(UIDocument))]
public class LanguageSelectController : MonoBehaviour
{
    [Header("Next Scene Settings")]
    [Tooltip("언어 선택 후 전환될 메인 대화 씬 이름")]
    [SerializeField] private string nextSceneName = "SampleDialogue";

    [Header("Element Names")]
    [SerializeField] private string koreanButtonName = "KoreanButton";
    [SerializeField] private string japaneseButtonName = "JapaneseButton";

    private UIDocument _uiDocument;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (_uiDocument == null || _uiDocument.rootVisualElement == null) return;

        var root = _uiDocument.rootVisualElement;
        var koBtn = root.Q<Button>(koreanButtonName);
        var jaBtn = root.Q<Button>(japaneseButtonName);

        if (koBtn != null)
        {
            koBtn.clicked += OnClickKorean;
        }
        else
        {
            Debug.LogWarning($"[LanguageSelectController] '{koreanButtonName}' Button not found!");
        }

        if (jaBtn != null)
        {
            jaBtn.clicked += OnClickJapanese;
        }
        else
        {
            Debug.LogWarning($"[LanguageSelectController] '{japaneseButtonName}' Button not found!");
        }
    }

    private void OnDisable()
    {
        if (_uiDocument == null || _uiDocument.rootVisualElement == null) return;

        var root = _uiDocument.rootVisualElement;
        var koBtn = root.Q<Button>(koreanButtonName);
        var jaBtn = root.Q<Button>(japaneseButtonName);

        if (koBtn != null) koBtn.clicked -= OnClickKorean;
        if (jaBtn != null) jaBtn.clicked -= OnClickJapanese;
    }

    public void OnClickKorean()
    {
        SelectLanguage("ko");
    }

    public void OnClickJapanese()
    {
        SelectLanguage("ja");
    }

    public void SelectLanguage(string languageCode)
    {
        Debug.Log($"[LanguageSelectController] Selected Language: {languageCode}");

        // 1. Dialogue System 전역 언어 설정
        Localization.language = languageCode;

        // 2. DialogueManager 인스턴스가 존재할 경우 즉시 적용
        if (DialogueManager.instance != null)
        {
            DialogueManager.SetLanguage(languageCode);
        }

        // 3. UILocalizationManager가 다음 씬에서 불러올 수 있도록 PlayerPrefs에 저장
        PlayerPrefs.SetString("Language", languageCode);
        PlayerPrefs.Save();

        // 4. 다음 대화 씬 로드
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError("[LanguageSelectController] Next scene name is empty!");
        }
    }
}
