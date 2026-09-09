using System.Collections.Generic;
using UnityEngine;

namespace BeepSync
{
    [CreateAssetMenu(fileName = "NewDialogueBeepData", menuName = "BeepSync/Dialogue Beep Data", order = 1)]
    public class DialogueBeepData : ScriptableObject
    {
        [Header("Character Identity")]
        [Tooltip("캐릭터 이름")]
        public string characterName = "Character";

        [Header("Audio Clips")]
        [Tooltip("텍스트 출력 시 랜덤 또는 순차적으로 재생할 비프음 클립 목록")]
        public List<AudioClip> beepClips = new List<AudioClip>();

        [Header("Playback Settings")]
        [Range(0.1f, 3.0f)]
        [Tooltip("기본 피치 (1.0 = 원본 음정)")]
        public float basePitch = 1.0f;

        [Range(0.0f, 0.5f)]
        [Tooltip("글자마다 적용될 피치 랜덤 변화폭 (예: 0.08 = +-8% 랜덤)")]
        public float pitchRandomness = 0.08f;

        [Range(0.01f, 0.2f)]
        [Tooltip("한 글자 출력당 지연 시간(초)")]
        public float charDelay = 0.045f;

        [Range(0.0f, 1.0f)]
        [Tooltip("마침표(.), 쉼표(,), 느낌표(!), 물음표(?) 등 문장 부호에서의 추가 지연 시간(초)")]
        public float punctuationPause = 0.2f;

        [Range(0.0f, 1.0f)]
        [Tooltip("기본 볼륨")]
        public float volume = 0.8f;

        [Tooltip("공백(스페이스바)이나 줄바꿈에서도 소리를 낼지 여부")]
        public bool playOnWhitespace = false;

        [Tooltip("N글자마다 1번만 소리를 낼지 설정 (1 = 매 글자마다 재생, 2 = 2글자당 1번 재생)")]
        [Range(1, 5)]
        public int soundFrequency = 1;

        [Header("Japanese Optimization Settings")]
        [Tooltip("일본어일 때 요음(ゃ, ゅ, ょ 등), 촉음(っ), 장음(ー)을 무음으로 처리할지 여부")]
        public bool muteSmallKanaInJapanese = true;

        [Tooltip("일본어 한자(漢字) 출력 시 다중 비프음 및 체류 시간 가중치 적용 여부")]
        public bool multiBeepForKanjiInJapanese = true;

        [Range(1, 3)]
        [Tooltip("일본어 한자 1글자당 재생할 비프음 횟수 (기본 2회)")]
        public int kanjiBeepCount = 2;

        [Range(1.0f, 2.5f)]
        [Tooltip("일본어 한자 출력 시 딜레이 배율 (기본 1.6배)")]
        public float kanjiDelayMultiplier = 1.6f;
    }
}
