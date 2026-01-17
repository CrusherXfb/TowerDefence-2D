using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Фоновая музыка")]
    public AudioSource musicSource;
    public AudioClip menuMusic;
    public AudioClip gameMusic;
    [Range(0, 1)] public float musicVolume = 0.5f;

    [Header("Звуки UI")]
    public AudioClip buttonClick;
    public AudioClip menuOpen;
    public AudioClip menuClose;

    [Header("Звуки строительства")]
    public AudioClip buildTower;
    public AudioClip sellTower;
    public AudioClip upgradeTower;
    public AudioClip notEnoughMoney;

    [Header("Звуки башен")]
    public AudioClip towerShootNormal;
    public AudioClip towerShootAOE;
    public AudioClip towerShootSlow;
    public AudioClip towerSupportEffect;

    [Header("Звуки вирусов")]
    public AudioClip virusSpawn;
    public AudioClip virusDeath;
    public AudioClip virusDamaged;
    public AudioClip virusReachBase;

    [Header("Звуки игры")]
    public AudioClip waveStart;
    public AudioClip waveComplete;
    public AudioClip victorySound;
    public AudioClip gameOverSound;

    [Header("Настройки")]
    [Range(0, 1)] public float sfxVolume = 0.7f;
    [Range(1, 10)] public int maxSimultaneousSounds = 5;

    private List<AudioSource> sfxPool = new List<AudioSource>();
    private Queue<AudioSource> availableSources = new Queue<AudioSource>();

    void Awake()
    {


        Instance = this;
        DontDestroyOnLoad(gameObject); 

        InitializeAudioPool();
        LoadVolumeSettings();

        Debug.Log("AudioManager: Инициализирован");
    }

    void InitializeAudioPool()
    {
        for (int i = 0; i < maxSimultaneousSounds; i++)
        {
            GameObject sourceObject = new GameObject($"SFX_Source_{i}");
            sourceObject.transform.SetParent(transform);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            sfxPool.Add(source);
            availableSources.Enqueue(source);
        }

        if (musicSource == null)
        {
            GameObject musicObject = new GameObject("Music_Source");
            musicObject.transform.SetParent(transform);
            musicSource = musicObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.volume = musicVolume;
        }
    }

    public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip == null) return;

        if (availableSources.Count > 0)
        {
            AudioSource source = availableSources.Dequeue();
            source.clip = clip;
            source.volume = sfxVolume * volumeMultiplier;
            source.Play();

            StartCoroutine(ReturnToPool(source, clip.length));
        }
        else
        {
            AudioSource tempSource = gameObject.AddComponent<AudioSource>();
            tempSource.clip = clip;
            tempSource.volume = sfxVolume * volumeMultiplier;
            tempSource.Play();

            Destroy(tempSource, clip.length);
            Debug.LogWarning("AudioManager: SFX pool exhausted, using temporary source");
        }
    }

    IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        availableSources.Enqueue(source);
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null)
        {
            Debug.LogWarning("AudioManager: musicSource или clip равен null");
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            Debug.Log($"AudioManager: Музыка {clip.name} уже играет, пропускаем");
            return;
        }

        Debug.Log($"AudioManager: Начинаем играть музыку {clip.name}");

        if (musicSource.isPlaying)
        {
            musicSource.Stop();
        }

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void StopMusic(float fadeOutTime = 0f)
    {
        if (musicSource == null || !musicSource.isPlaying) return;

        Debug.Log("AudioManager: Останавливаем музыку");

        if (fadeOutTime <= 0)
        {
            musicSource.Stop();
        }
        else
        {
            StartCoroutine(FadeOutMusic(fadeOutTime));
        }
    }

    IEnumerator FadeOutMusic(float duration)
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0, elapsed / duration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = startVolume; 
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.Save();
    }


    public void PlayButtonClick() => PlaySFX(buttonClick, 0.8f);
    public void PlayBuildSound() => PlaySFX(buildTower);
    public void PlaySellSound() => PlaySFX(sellTower);
    public void PlayUpgradeSound() => PlaySFX(upgradeTower);
    public void PlayNotEnoughMoney() => PlaySFX(notEnoughMoney);
    public void PlayWaveStart() => PlaySFX(waveStart);
    public void PlayWaveComplete() => PlaySFX(waveComplete);
    public void PlayVictory() => PlaySFX(victorySound);
    public void PlayGameOver() => PlaySFX(gameOverSound);
    public void PlayVirusDeath() => PlaySFX(virusDeath);
    public void PlayVirusReachBase() => PlaySFX(virusReachBase);
    public void PlayVirusDamaged() => PlaySFX(virusDamaged);
    public void PlayMenuOpen() => PlaySFX(menuOpen);
    public void PlayMenuClose() => PlaySFX(menuClose);

    public void PlayTowerShoot(TowerType type)
    {
        switch (type)
        {
            case TowerType.Damage:
                PlaySFX(towerShootNormal);
                break;
            case TowerType.AOE:
                PlaySFX(towerShootAOE);
                break;
            case TowerType.Slow:
                PlaySFX(towerShootSlow);
                break;
            case TowerType.Support:
                PlaySFX(towerSupportEffect);
                break;
        }
    }

    void LoadVolumeSettings()
    {
        if (PlayerPrefs.HasKey("MusicVolume"))
            SetMusicVolume(PlayerPrefs.GetFloat("MusicVolume"));
        if (PlayerPrefs.HasKey("SFXVolume"))
            SetSFXVolume(PlayerPrefs.GetFloat("SFXVolume"));
    }

    public void PlayMenuMusic()
    {
        if (menuMusic != null)
        {
            Debug.Log("AudioManager: Включаем музыку меню");
            PlayMusic(menuMusic);
        }
    }

    public void PlayGameMusic()
    {
        if (gameMusic != null)
        {
            Debug.Log("AudioManager: Включаем игровую музыку");
            PlayMusic(gameMusic);
        }
    }
}