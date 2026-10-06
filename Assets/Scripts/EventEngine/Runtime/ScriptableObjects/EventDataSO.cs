using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.EventEngine
{
    /// <summary>
    /// A story node ("Occurrence" in Honkai: Star Rail's Simulated Universe): an illustration,
    /// a few pages of narration and branching choices with rewards, costs, gambles or a fight.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEvent", menuName = "TawanOS/EventEngine/Event")]
    public class EventDataSO : ScriptableObject
    {
        public string eventId;
        public string title = "Event Title";
        public string speakerName;
        public Sprite illustration;
        [Tooltip("Only appears on or after this map floor.")]
        [Min(0)] public int minFloor;
        [Tooltip("Can this event show up again after it was seen in the same run?")]
        public bool repeatable;
        public List<EventPage> pages = new List<EventPage>();

        public EventPage FirstPage => pages.Count > 0 ? pages[0] : null;

        private void Reset()
        {
            InitializeDefaults();
        }

        /// <summary>A playable one-page skeleton so a freshly created event is never empty.</summary>
        public void InitializeDefaults()
        {
            title = "เหตุการณ์ใหม่";
            pages = new List<EventPage>
            {
                new EventPage
                {
                    pageId = "start",
                    body = "เขียนเนื้อเรื่องตรงนี้...",
                    choices = new List<EventChoice>
                    {
                        new EventChoice
                        {
                            label = "จากไป",
                            outcomes = new List<EventOutcome> { new EventOutcome { resultText = "คุณเดินจากไปอย่างเงียบ ๆ" } },
                        },
                    },
                },
            };
        }

        public EventPage GetPage(string pageId)
        {
            return pages.Find(p => p.pageId == pageId);
        }
    }
}
