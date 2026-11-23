using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class GameAudioHover : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (GetComponent<Button>().IsInteractable())
            GameAudio.Instance?.Play(GameSound.Hover);
    }
}
