using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [Header("Кнопки")]
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;
    public Button backButton; 

    [Header("Панели")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;

    [Header("Настройки звука")]
    public Slider musicSlider;
    public Slider sfxSlider;
    public Text musicValueText;
    public Text sfxValueText;


    void Start()
    {
        // Инициализация кнопок с звуками
        SetupButtonWithSound(playButton, OnPlayClicked);
        SetupButtonWithSound(settingsButton, OnSettingsClicked);
        SetupButtonWithSound(quitButton, OnQuitClicked);
        SetupButtonWithSound(backButton, CloseSettings);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMenuMusic();
        }

        Debug.Log("Обработчики назначены");

        InitializeSettings();
        ShowMainMenu();

        Time.timeScale = 1f;
    }

    void SetupButtonWithSound(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
            if (AudioManager.Instance != null)
                button.onClick.AddListener(AudioManager.Instance.PlayButtonClick);
        }
    }

    void OnPlayClicked()
    {
        Debug.Log("OnPlayClicked - Загрузка игровой сцены");

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic(0.1f); 
            Debug.Log("MenuManager: Музыка меню остановлена");
        }

        NetworkNode.CurrentlySelectedNode = null;

        SceneManager.LoadScene(SceneNames.GAME_SCENE);
    }

    void OnSettingsClicked()
    {
        ShowSettings();
    }

    void OnQuitClicked()
    {
        Application.Quit();

        // Для работы в редакторе
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void InitializeSettings()
    {
        // === НАСТРОЙКИ ЗВУКА ===

        // Музыка
        if (musicSlider != null)
        {
            float savedMusicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            musicSlider.value = savedMusicVolume;
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

            if (musicValueText != null)
                musicValueText.text = $"{Mathf.RoundToInt(savedMusicVolume * 100)}%";

            // Применяем сразу
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMusicVolume(savedMusicVolume);
        }

        // Звуковые эффекты
        if (sfxSlider != null)
        {
            float savedSFXVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
            sfxSlider.value = savedSFXVolume;
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

            if (sfxValueText != null)
                sfxValueText.text = $"{Mathf.RoundToInt(savedSFXVolume * 100)}%";

            // Применяем сразу
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetSFXVolume(savedSFXVolume);
        }

    }

    // ========== ОБРАБОТЧИКИ ЗВУКА ==========

    void OnMusicVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);

        if (musicValueText != null)
            musicValueText.text = $"{Mathf.RoundToInt(value * 100)}%";

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);

        PlayerPrefs.SetFloat("MusicVolume", value);
        PlayerPrefs.Save();
    }

    void OnSFXVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);

        if (sfxValueText != null)
            sfxValueText.text = $"{Mathf.RoundToInt(value * 100)}%";

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSFXVolume(value);

        PlayerPrefs.SetFloat("SFXVolume", value);
        PlayerPrefs.Save();
    }


    // ========== УПРАВЛЕНИЕ ПАНЕЛЯМИ ==========

    public void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Включаем музыку меню
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMenuMusic();
    }

    public void ShowSettings()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);

    }

    public void CloseSettings()
    {
        ShowMainMenu();
    }

    // ========== ДОПОЛНИТЕЛЬНЫЕ МЕТОДЫ ==========

    public void ResetToDefaults()
    {
        // Сброс звука
        if (musicSlider != null)
        {
            musicSlider.value = 0.7f;
            OnMusicVolumeChanged(0.7f);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = 0.8f;
            OnSFXVolumeChanged(0.8f);
        }

    }


}