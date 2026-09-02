using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if TMPro_PRESENT || UNITY_2019_1_OR_NEWER
using TMPro;
#endif

namespace BeepSync
{
    [RequireComponent(typeof(AudioSource))]
    public class DialogueBeepPlayer : MonoBehaviour
    {
        [Header("Data Source")]
        [SerializeField] private DialogueBeepData beepData;

        [Header("Audio Component")]
        [SerializeField] private AudioSource audioSource;

#if TMPro_PRESENT || UNITY_2019_1_OR_NEWER
        [Header("UI Text (TextMeshPro)")]
        [SerializeField] private TMP_Text targetTMPText;
#endif

        [Header("Events")]
        public UnityEvent<char> onCharacterTyped;
        public UnityEvent onDialogueCompleted;

        private Coroutine _typewriterCoroutine;
        private bool _isTyping = false;
        private string _fullText = "";
        private int _lastClipIndex = -1;

        public bool IsTyping => _isTyping;
        public DialogueBeepData BeepData { get => beepData; set => beepData = value; }

        private static readonly HashSet<char> PunctuationChars = new HashSet<char> { '.', ',', '!', '?', ';', ':', '…', '~' };

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
        }

        /// <summary>
        /// 지정된 텍스트를 타자기 연출과 함께 비프음을 재생하며 출력합니다.
        /// </summary>
        public void PlayDialogue(string text, DialogueBeepData overrideData = null)
        {
            if (overrideData != null)
            {
                beepData = overrideData;
            }

            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
            }

            _fullText = text;
            _typewriterCoroutine = StartCoroutine(TypewriterRoutine(text));
        }

        /// <summary>
        /// 타이핑을 즉시 완료하고 전체 텍스트를 한 번에 표시합니다.
        /// </summary>
        public void SkipToEnd()
        {
            if (!_isTyping) return;

            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }

            _isTyping = false;
#if TMPro_PRESENT || UNITY_2019_1_OR_NEWER
            if (targetTMPText != null)
            {
                targetTMPText.text = _fullText;
                targetTMPText.maxVisibleCharacters = _fullText.Length;
            }
#endif
            onDialogueCompleted?.Invoke();
        }

        private IEnumerator TypewriterRoutine(string text)
        {
            _isTyping = true;

#if TMPro_PRESENT || UNITY_2019_1_OR_NEWER
            if (targetTMPText != null)
            {
                targetTMPText.text = text;
                targetTMPText.maxVisibleCharacters = 0;
            }
#endif

            float basePitch = beepData != null ? beepData.basePitch : 1.0f;
            float pitchRand = beepData != null ? beepData.pitchRandomness : 0.08f;
            float charDelay = beepData != null ? beepData.charDelay : 0.045f;
            float puncPause = beepData != null ? beepData.punctuationPause : 0.2f;
            int soundFreq = (beepData != null && beepData.soundFrequency > 0) ? beepData.soundFrequency : 1;
            bool playOnSpace = beepData != null && beepData.playOnWhitespace;

            int visibleCharCount = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                visibleCharCount++;

#if TMPro_PRESENT || UNITY_2019_1_OR_NEWER
                if (targetTMPText != null)
                {
                    targetTMPText.maxVisibleCharacters = visibleCharCount;
                }
#endif
                onCharacterTyped?.Invoke(c);

                // Check sound playback condition
                bool isWhitespace = char.IsWhiteSpace(c);
                bool shouldPlaySound = (!isWhitespace || playOnSpace) && (visibleCharCount % soundFreq == 0);

                if (shouldPlaySound)
                {
                    PlayBeepSound(basePitch, pitchRand);
                }

                // Check punctuation delay
                if (PunctuationChars.Contains(c))
                {
                    yield return new WaitForSeconds(charDelay + puncPause);
                }
                else
                {
                    yield return new WaitForSeconds(charDelay);
                }
            }

            _isTyping = false;
            _typewriterCoroutine = null;
            onDialogueCompleted?.Invoke();
        }

        private void PlayBeepSound(float basePitch, float pitchRandomness)
        {
            if (beepData == null || beepData.beepClips == null || beepData.beepClips.Count == 0)
            {
                return;
            }

            // Pick a clip (avoid repeating the exact same clip consecutively if multiple are available)
            AudioClip clipToPlay;
            int count = beepData.beepClips.Count;
            if (count == 1)
            {
                clipToPlay = beepData.beepClips[0];
            }
            else
            {
                int index = UnityEngine.Random.Range(0, count);
                if (index == _lastClipIndex)
                {
                    index = (index + 1) % count;
                }
                _lastClipIndex = index;
                clipToPlay = beepData.beepClips[index];
            }

            if (clipToPlay == null) return;

            // Apply randomized pitch
            float randomOffset = UnityEngine.Random.Range(-pitchRandomness, pitchRandomness);
            audioSource.pitch = Mathf.Clamp(basePitch + randomOffset, 0.1f, 3.0f);
            audioSource.volume = beepData.volume;

            audioSource.PlayOneShot(clipToPlay);
        }
    }
}
