using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class MainMenuController : MonoBehaviour
{
    [System.Serializable]
    public class LevelEntry
    {
        public LevelData data;
        public string sceneName;
    }

    [SerializeField] TMP_Text levelNameText;
    [SerializeField] GameObject homePanel;
    [SerializeField] GameObject levelsPanel;
    [SerializeField] Transform levelList;
    [SerializeField] Button levelButtonPrefab;
    [SerializeField] LevelEntry[] levels;
    [SerializeField] Button startButton;
    InputAction backAction;

    void Awake()
    {
        backAction = new InputAction("Back", InputActionType.Button);
        backAction.AddBinding("<Keyboard>/escape");
        backAction.AddBinding("<Gamepad>/buttonEast");
    }
    void OnEnable()
    {
        backAction.performed += OnBack;
        backAction.Enable();
    }
    void OnDisable()
    {
        backAction.performed -= OnBack;
        backAction.Disable();
    }
    void OnDestroy() => backAction.Dispose();
    void OnBack(InputAction.CallbackContext context)
    {
        if (levelsPanel.activeSelf) Back();
    }

    void Start()
    {
        levelNameText.text = "Levels";
        if (levels == null) levels = System.Array.Empty<LevelEntry>();
        foreach (var entry in levels)
        {
            var button = Instantiate(levelButtonPrefab, levelList);
            button.GetComponentInChildren<TMP_Text>().text = entry.data.levelName;
            button.onClick.AddListener(() => LoadLevel(entry.sceneName));
        }
        Back();
    }

    public void Play()
    {
        homePanel.SetActive(false);
        levelsPanel.SetActive(true);
        if (levelList.childCount > 0)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(levelList.GetChild(0).gameObject);
    }

    public void Back()
    {
        levelsPanel.SetActive(false);
        homePanel.SetActive(true);
        if (startButton != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    void LoadLevel(string sceneName)
    {
        SceneNavigation.Load(sceneName);
    }

    public void Quit()
    {
        SceneNavigation.Quit();
    }
}
