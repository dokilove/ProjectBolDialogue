using UnityEngine;
using PixelCrushers.DialogueSystem;

public class SampleScript : MonoBehaviour
{
    public string StartDialogue;

    void Start()
    {
        DialogueManager.StartConversation(StartDialogue);
    }
}
