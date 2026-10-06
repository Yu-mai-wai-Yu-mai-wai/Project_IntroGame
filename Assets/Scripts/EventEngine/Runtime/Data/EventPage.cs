using System;
using System.Collections.Generic;
using TawanOS.CardEngine;
using UnityEngine;
using UnityEngine.Serialization;

namespace TawanOS.EventEngine
{
    /// <summary>One screen of an event: narration text plus the choices the player can pick.</summary>
    [Serializable]
    public class EventPage
    {
        [Tooltip("Referenced by EventOutcome.nextPageId. The first page in the list is where the event starts.")]
        public string pageId = "start";
        [Tooltip("Optional override for this page's speaker; empty uses the event's speaker.")]
        public string speakerOverride;
        [TextArea(3, 8)]
        public string body;
        public List<EventChoice> choices = new List<EventChoice>();
    }

    [Serializable]
    public class EventChoice
    {
        public string label = "...";
        [Tooltip("Small grey line under the label that hints at the outcome (like the italic hint in Simulated Universe).")]
        public string hint;

        [Header("Requirements (choice is greyed out when not met)")]
        [FormerlySerializedAs("offeringCost")]
        [Min(0)] public int incenseCost;
        [Tooltip("HP paid up front. The choice is disabled if it would bring HP to 0.")]
        [Min(0)] public int hpCost;

        [Tooltip("One outcome is rolled by weight. A single entry means the choice is deterministic.")]
        public List<EventOutcome> outcomes = new List<EventOutcome>();
    }

    [Serializable]
    public class EventOutcome
    {
        [Min(0)] public int weight = 1;
        [TextArea(2, 6)]
        public string resultText;
        public List<EventEffect> effects = new List<EventEffect>();
        [Tooltip("Page to continue to. Empty ends the event after showing the result.")]
        public string nextPageId;
    }

    [Serializable]
    public class EventEffect
    {
        public EventEffectType type;
        public int amount;
        [Tooltip("GainRelic only")]
        public string relicId;
        [Tooltip("StartCombat only")]
        public EnemyProfileSO enemy;
    }
}
