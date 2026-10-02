using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class UISetup
{
    const string Large = "Assets/Art/Sprites/Source/Menu/Large Buttons/Large Buttons/";
    const string Square = "Assets/Art/Sprites/Source/Menu/Square Buttons/Square Buttons/";
    const string Interface = "Assets/Art/Sprites/Interface/";
    const string UIManagerPrefab = "Assets/Prefabs/UIManager.prefab";
    const string HudMaterialPath = "Assets/Art/HUDText.mat";

    [MenuItem("JumpIO/Setup/UI Screens")]
    public static void Configure()
    {
        ConfigureRestartDelay(3f);
        ConfigureLevelUI();
        ConfigureLevelCard();
        var menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        ConfigureMenu();
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        var level = EditorSceneManager.OpenScene("Assets/Scenes/Level_01.unity");
        foreach (var root in level.GetRootGameObjects()) ConfigureSelectables(root);
        EditorSceneManager.MarkSceneDirty(level);
        EditorSceneManager.SaveScene(level);
        AssetDatabase.SaveAssets();
        Debug.Log("JUMPIO_BLOCK_8_OK options, pause restart, death screen, selection feedback");
    }

    static void ConfigureRestartDelay(float seconds)
    {
        var manager = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameManager.prefab").GetComponent<GameManager>();
        var serialized = new SerializedObject(manager);
        serialized.FindProperty("restartDelay").floatValue = seconds;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
    }

    static void ConfigureMenu()
    {
        var canvas = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Canvas");
        var controller = canvas.GetComponent<MainMenuController>();
        var home = canvas.transform.Find("HomePanel");

        Place(home.Find("GameLogo"), new Vector2(0f, 270f), new Vector2(880f, 322f));
        Place(home.Find("PlayButton"), new Vector2(0f, 10f), new Vector2(540f, 180f));
        Place(home.Find("QuitButton"), new Vector2(0f, -305f), new Vector2(378f, 126f));
        Place(home.Find("Tagline"), new Vector2(0f, -415f), new Vector2(1300f, 60f));
        var existing = home.Find("OptionsButton");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var optionsButton = ImageButton(home, "OptionsButton", Sprite(Large + "Options Button.png"),
            new Vector2(0f, -160f), new Vector2(378f, 126f), controller.Options);
        optionsButton.transform.SetSiblingIndex(home.Find("QuitButton").GetSiblingIndex());

        var panel = OptionsPanel(canvas.transform, canvas.transform.Find("LevelsPanel").GetSiblingIndex() + 1,
            controller.Back, dimmed: false, out var master);

        var menu = new SerializedObject(controller);
        menu.FindProperty("optionsPanel").objectReferenceValue = panel;
        menu.FindProperty("optionsFirstSelected").objectReferenceValue = master;
        menu.ApplyModifiedPropertiesWithoutUndo();

        var events = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
            .First(g => g.GetComponent<UnityEngine.EventSystems.EventSystem>() != null).GetComponent<UnityEngine.EventSystems.EventSystem>();
        events.firstSelectedGameObject = home.Find("PlayButton").gameObject;
        ConfigureSelectables(canvas);
    }

    static GameObject OptionsPanel(Transform canvas, int siblingIndex, UnityAction onBack, bool dimmed, out Slider first)
    {
        var old = canvas.Find("OptionsPanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var panel = Ui("OptionsPanel", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        panel.transform.SetSiblingIndex(siblingIndex);
        if (dimmed)
        {
            var dim = panel.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.04f, 0.06f, 0.94f);
        }
        Label(panel.transform, "Title", "OPTIONS", new Vector2(0f, 330f), new Vector2(800f, 110f), 84f);
        var master = SliderRow(panel.transform, "Master", "MASTER", 165f, out var masterValue);
        var music = SliderRow(panel.transform, "Music", "MUSIC", 55f, out var musicValue);
        var sfx = SliderRow(panel.transform, "Sfx", "SFX", -55f, out var sfxValue);
        var jump = ToggleRow(panel.transform, "JumpSound", "JUMP SOUND", -170f);
        ImageButton(panel.transform, "BackButton", Sprite(Interface + "Back.png"), new Vector2(0f, -320f), new Vector2(330f, 110f), onBack);

        var options = panel.AddComponent<OptionsMenu>();
        var serialized = new SerializedObject(options);
        serialized.FindProperty("master").objectReferenceValue = master;
        serialized.FindProperty("music").objectReferenceValue = music;
        serialized.FindProperty("sfx").objectReferenceValue = sfx;
        serialized.FindProperty("masterValue").objectReferenceValue = masterValue;
        serialized.FindProperty("musicValue").objectReferenceValue = musicValue;
        serialized.FindProperty("sfxValue").objectReferenceValue = sfxValue;
        serialized.FindProperty("jumpSound").objectReferenceValue = jump;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        panel.SetActive(false);
        first = master;
        return panel;
    }

    static Slider SliderRow(Transform parent, string name, string text, float y, out TMP_Text value)
    {
        Label(parent, name + "Label", text, new Vector2(-440f, y), new Vector2(340f, 70f), 46f, TextAlignmentOptions.Right);
        var go = DefaultControls.CreateSlider(Resources());
        go.name = name + "Slider";
        go.transform.SetParent(parent, false);
        Place(go.transform, new Vector2(30f, y), new Vector2(480f, 36f));
        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        go.transform.Find("Background").GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.9f);
        go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = new Color(1f, 0.82f, 0.32f);
        var handle = go.transform.Find("Handle Slide Area/Handle");
        handle.GetComponent<Image>().color = Color.white;
        ((RectTransform)handle).sizeDelta = new Vector2(44f, 0f);
        value = Label(parent, name + "Value", "100%", new Vector2(400f, y), new Vector2(160f, 70f), 46f, TextAlignmentOptions.Left);
        return slider;
    }

    static Toggle ToggleRow(Transform parent, string name, string text, float y)
    {
        Label(parent, name + "Label", text, new Vector2(-440f, y), new Vector2(340f, 70f), 46f, TextAlignmentOptions.Right);
        var go = DefaultControls.CreateToggle(Resources());
        go.name = name + "Toggle";
        go.transform.SetParent(parent, false);
        Object.DestroyImmediate(go.transform.Find("Label").gameObject);
        Place(go.transform, new Vector2(-166f, y), new Vector2(88f, 88f));
        var background = (RectTransform)go.transform.Find("Background");
        Stretch(background);
        var box = background.GetComponent<Image>();
        box.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        box.type = Image.Type.Sliced;
        box.color = new Color(0.08f, 0.1f, 0.12f, 0.95f);
        var check = (RectTransform)background.Find("Checkmark");
        Stretch(check);
        check.GetComponent<Image>().sprite = Sprite(Square + "V Square Button.png");
        var toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = background.GetComponent<Image>();
        return toggle;
    }

    static DefaultControls.Resources Resources() => new DefaultControls.Resources
    {
        standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
        knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
    };

    static void ConfigureLevelCard()
    {
        const string path = "Assets/Prefabs/UI/LevelCard.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        ConfigureSelectables(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void ConfigureLevelUI()
    {
        var root = PrefabUtility.LoadPrefabContents(UIManagerPrefab);
        var manager = root.GetComponent<UIManager>();
        var canvas = root.transform.Find("Canvas");

        var pause = (RectTransform)canvas.Find("PausaPanel");
        Stretch(pause);
        var dim = pause.GetComponent<Image>();
        dim.sprite = null;
        dim.color = new Color(0.02f, 0.04f, 0.06f, 0.72f);
        var title = pause.Find("Title").GetComponent<TMP_Text>();
        title.text = "PAUSED";
        title.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(HudMaterialPath);
        Place(title.transform, new Vector2(0f, 290f), new Vector2(800f, 130f));
        var resume = pause.Find("ResumeButton");
        SetSprite(resume, Sprite(Large + "Resume Button.png"), new Vector2(0f, 110f), new Vector2(540f, 180f));
        var restart = CloneButton(pause, "RestartButton", manager.Restart, resume.GetSiblingIndex() + 1);
        var optionsButton = CloneButton(pause, "OptionsButton", manager.OpenOptions, restart.GetSiblingIndex() + 1);
        IconButton(restart, Sprite(Square + "Return Square Button.png"), new Vector2(-330f, -110f), "RESTART");
        IconButton(optionsButton, Sprite(Square + "Settings Square Button.png"), new Vector2(-110f, -110f), "OPTIONS");
        IconButton(pause.Find("MainMenuButton"), Sprite(Square + "Home Square Button.png"), new Vector2(110f, -110f), "MENU");
        IconButton(pause.Find("QuitButton"), Sprite(Square + "On Off Square Button.png"), new Vector2(330f, -110f), "QUIT");

        var options = OptionsPanel(canvas, pause.GetSiblingIndex() + 1, manager.CloseOptions, dimmed: true, out var firstSlider);

        var oldDeath = canvas.Find("DeathPanel");
        if (oldDeath != null) Object.DestroyImmediate(oldDeath.gameObject);
        var death = Ui("DeathPanel", canvas, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(700f, 320f));
        death.transform.SetSiblingIndex(pause.GetSiblingIndex());
        var card = death.AddComponent<Image>();
        card.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        card.type = Image.Type.Sliced;
        card.color = new Color(0.02f, 0.04f, 0.06f, 0.8f);
        death.AddComponent<CanvasGroup>();
        var screen = death.AddComponent<DeathScreen>();
        var attempt = Label(death.transform, "AttemptText", "ATTEMPT 1", new Vector2(0f, 110f), new Vector2(600f, 80f), 58f);
        var countdown = Label(death.transform, "CountdownText", "RESTARTING IN 3", new Vector2(0f, 55f), new Vector2(600f, 50f), 32f);
        countdown.color = new Color(1f, 0.82f, 0.4f);
        var retry = ImageButton(death.transform, "RetryButton", Sprite(Square + "Return Square Button.png"),
            new Vector2(-110f, -40f), new Vector2(120f, 120f), screen.Retry);
        AddCaption(retry.transform, "RETRY");
        var menu = ImageButton(death.transform, "MenuButton", Sprite(Square + "Home Square Button.png"),
            new Vector2(110f, -40f), new Vector2(120f, 120f), screen.Menu);
        AddCaption(menu.transform, "MENU");
        var deathSerialized = new SerializedObject(screen);
        deathSerialized.FindProperty("attemptText").objectReferenceValue = attempt;
        deathSerialized.FindProperty("countdownText").objectReferenceValue = countdown;
        deathSerialized.FindProperty("firstSelected").objectReferenceValue = retry;
        deathSerialized.ApplyModifiedPropertiesWithoutUndo();

        var serialized = new SerializedObject(manager);
        serialized.FindProperty("pauseFirstSelected").objectReferenceValue = resume.GetComponent<Selectable>();
        serialized.FindProperty("optionsPanel").objectReferenceValue = options;
        serialized.FindProperty("optionsFirstSelected").objectReferenceValue = firstSlider;
        serialized.FindProperty("pauseOptionsButton").objectReferenceValue = optionsButton.GetComponent<Selectable>();
        var completed = canvas.Find("CompletionPanel");
        serialized.FindProperty("completedFirstSelected").objectReferenceValue =
            completed.GetComponentsInChildren<Button>(true).First(b => b.name == "RetryButton");
        serialized.ApplyModifiedPropertiesWithoutUndo();

        ConfigureSelectables(root);
        PrefabUtility.SaveAsPrefabAsset(root, UIManagerPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static Transform CloneButton(Transform pause, string name, UnityAction onClick, int siblingIndex)
    {
        var old = pause.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var copy = Object.Instantiate(pause.Find("MainMenuButton").gameObject, pause).transform;
        copy.name = name;
        var button = copy.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, onClick);
        copy.SetSiblingIndex(siblingIndex);
        return copy;
    }

    static void IconButton(Transform button, Sprite sprite, Vector2 position, string caption)
    {
        SetSprite(button, sprite, position, new Vector2(140f, 140f));
        var label = button.GetComponentInChildren<TMP_Text>(true);
        label.gameObject.SetActive(true);
        label.text = caption;
        label.fontSize = 34f;
        label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(HudMaterialPath);
        label.alignment = TextAlignmentOptions.Center;
        var rect = (RectTransform)label.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -8f);
        rect.sizeDelta = new Vector2(260f, 50f);
    }

    static void AddCaption(Transform button, string caption)
    {
        var label = Label(button, "Label", caption, Vector2.zero, new Vector2(240f, 46f), 30f);
        var rect = (RectTransform)label.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -6f);
    }

    static void SetSprite(Transform button, Sprite sprite, Vector2 position, Vector2 size)
    {
        var image = button.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        Place(button, position, size);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null && sprite.name.Contains("Button") && !sprite.name.Contains("Square")) label.gameObject.SetActive(false);
    }

    static void ConfigureSelectables(GameObject root)
    {
        foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
        {
            var colors = selectable.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.75f);
            colors.selectedColor = new Color(1f, 0.86f, 0.45f);
            colors.pressedColor = new Color(0.85f, 0.7f, 0.35f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            colors.fadeDuration = 0.08f;
            selectable.colors = colors;
            selectable.transition = Selectable.Transition.ColorTint;
            var feedback = selectable.GetComponent<SelectionFeedback>();
            if (feedback == null) feedback = selectable.gameObject.AddComponent<SelectionFeedback>();
            var scale = new SerializedObject(feedback);
            scale.FindProperty("selectedScale").floatValue = selectable is Slider ? 1f : 1.08f;
            scale.ApplyModifiedPropertiesWithoutUndo();
            if (selectable.GetComponent<ButtonClickSound>() == null) selectable.gameObject.AddComponent<ButtonClickSound>();
            EditorUtility.SetDirty(selectable);
        }
    }

    static Button ImageButton(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, UnityAction onClick)
    {
        var go = Ui(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        UnityEventTools.AddPersistentListener(button.onClick, onClick);
        return button;
    }

    static TMP_Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var go = Ui(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = new Color(1f, 0.95f, 0.8f);
        label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(HudMaterialPath);
        label.raycastTarget = false;
        return label;
    }

    static GameObject Ui(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go;
    }

    static void Place(Transform transform, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    static Sprite Sprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
}
