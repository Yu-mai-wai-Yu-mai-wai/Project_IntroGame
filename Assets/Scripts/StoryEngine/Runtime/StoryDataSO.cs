using System;
using System.Collections.Generic;
using TawanOS.CardEngine;
using UnityEngine;
using UnityEngine.Video;

namespace TawanOS.StoryEngine
{
    /// <summary>
    /// A linear visual-novel story, separate from EventEngine events: a list of pages, each with a
    /// background (picture or looping video; characters are drawn into it), a speaker and a line of
    /// text. The player clicks through it.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStory", menuName = "TawanOS/StoryEngine/Story")]
    public class StoryDataSO : ScriptableObject
    {
        public string storyId;
        public List<StoryPage> pages = new List<StoryPage>();

        private void Reset()
        {
            pages = new List<StoryPage> { new StoryPage { text = "เขียนเนื้อเรื่องตรงนี้..." } };
        }
    }

    /// <summary>One click of the story. An empty background keeps what the previous page showed.</summary>
    [Serializable]
    public class StoryPage
    {
        [Header("Background")]
        [Tooltip("Looping video, played until the player clicks to a page with another background. Empty keeps the previous page's background.")]
        public VideoClip backgroundVideo;
        [Tooltip("Still picture instead of a video (used only when 'backgroundVideo' is empty).")]
        public Sprite background;
        [Tooltip("Fade the background to black on this page (narration in the dark).")]
        public bool blackScreen;

        [Header("Text")]
        [Tooltip("Name shown above the text. Empty = narration (no name plate).")]
        public string speaker;
        [TextArea(3, 8)]
        public string text;

        [Header("Tutorial Fight")]
        [Tooltip("Play a tutorial fight against this enemy after this page, with step-by-step hints; the story goes on from the " +
                 "next page whether it is won or lost. It costs no Khwan and pays nothing. Empty = no fight.")]
        public EnemyProfileSO tutorialFightEnemy;
    }
}
