using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance;

    [Header("Основные UI элементы")]
    public GameObject pauseMenuPanel;
    public Button continueButton;
    public Button settingsButton; 
    public Button restartButton;
    public Button menuButton;

    [Header("Панель настроек (перетащи префаб)")]
    public GameObject settingsPanel;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Text musicValueText;
    public Text sfxValueText;
    public Button settingsBackButton;

    [Header("Настройки паузы")]
    public KeyCode pauseKey = KeyCode.Escape;
    private bool isPaused = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Настройка основных кнопок
        SetupButtonWithSound(continueButton, ContinueGame);
        SetupButtonWithSound(restartButton, RestartGame);
        SetupButtonWithSound(menuButton, ReturnToMainMenu);
        SetupButtonWithSound(settingsButton, ShowSettings); 

        // Настройка кнопок в панели настроек
        if (settingsBackButton != null)
            SetupButtonWithSound(settingsBackButton, HideSettings);

        // Инициализация UI
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // Инициализация настроек звука
        InitializeAudioSettings();
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

    void InitializeAudioSettings()
    {
        // Инициализация слайдеров музыки
        if (musicSlider != null)
        {
            // Загружаем сохраненное значение или используем 0.7 по умолчанию
            float savedMusicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            musicSlider.value = savedMusicVolume;
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

            if (musicValueText != null)
                musicValueText.text = $"{Mathf.RoundToInt(savedMusicVolume * 100)}%";

            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMusicVolume(savedMusicVolume);
        }

        // Инициализация слайдеров SFX
        if (sfxSlider != null)
        {
            // Загружаем сохраненное значение или используем 0.8 по умолчанию
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

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (CanPause())
            {
                TogglePause();
            }
        }
    }

    bool CanPause()
    {
        if (GameManager.Instance == null)
            return false;

        // Нельзя ставить на паузу в главном меню
        if (SceneManager.GetActiveScene().name == "MainMenu")
            return false;

        // Нельзя ставить на паузу если игра не активна (поражение/победа)
        if (!GameManager.Instance.isGameActive)
            return false;

        // Нельзя ставить на паузу во время показа других меню
        if (GameManager.Instance.gameOverPanel != null && GameManager.Instance.gameOverPanel.activeSelf)
            return false;

        if (GameManager.Instance.victoryPanel != null && GameManager.Instance.victoryPanel.activeSelf)
            return false;

        return true;
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            PauseGame();
        }
        else
        {
            ContinueGame();
        }
    }

    public void PauseGame()
    {
        if (!CanPause()) return;

        isPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }

        // Скрываем панель настроек при открытии паузы
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        // Отключаем интерактивность игрового поля
        SetGameInteractivity(false);

        Debug.Log("Игра на паузе");
    }

    public void ContinueGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        // Скрываем обе панели
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        // Восстанавливаем интерактивность
        SetGameInteractivity(true);

        Debug.Log("Игра продолжена");
    }

    // ========== НАСТРОЙКИ ЗВУКА ==========

    void OnMusicVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);

        if (musicValueText != null)
            musicValueText.text = $"{Mathf.RoundToInt(value * 100)}%";

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);

        // Сохраняем настройки
        PlayerPrefs.SetFloat("MusicVolume", value);
        PlayerPrefs.Save();

        Debug.Log($"Громкость музыки изменена: {value:F2}");
    }

    void OnSFXVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);

        if (sfxValueText != null)
            sfxValueText.text = $"{Mathf.RoundToInt(value * 100)}%";

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSFXVolume(value);

        // Сохраняем настройки
        PlayerPrefs.SetFloat("SFXVolume", value);
        PlayerPrefs.Save();

        Debug.Log($"Громкость звуков изменена: {value:F2}");
    }


    // ========== УПРАВЛЕНИЕ ПАНЕЛЯМИ ==========

    void ShowSettings()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    void HideSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (pauseMenuPanel != null && isPaused)
            pauseMenuPanel.SetActive(true);
    }

    // ========== ОСТАЛЬНЫЕ МЕТОДЫ ==========

    void SetGameInteractivity(bool isActive)
    {
        if (BuildMenu.Instance != null)
        {
            if (!isActive)
            {
                BuildMenu.Instance.CloseMenu();
            }
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        isPaused = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartGame();
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        isPaused = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMainMenu();
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

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

    public bool IsGamePaused()
    {
        return isPaused;
    }

    void OnDestroy()
    {
        if (isPaused)
        {
            Time.timeScale = 1f;
        }
    }
}