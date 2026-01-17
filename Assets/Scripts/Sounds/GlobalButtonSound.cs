using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GlobalButtonSound : MonoBehaviour
{
    void Start()
    {
        AddSoundToAllButtons();

    }

    public void AddSoundToAllButtons()
    {
        Button[] allButtons = FindObjectsOfType<Button>(true);

        Debug.Log($"Найдено кнопок: {allButtons.Length}");

        foreach (Button button in allButtons)
        {
            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerClick;

            entry.callback.AddListener((data) => {
                PlayButtonSound();
            });

            trigger.triggers.Add(entry);
        }
    }

    void PlayButtonSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
    }
}