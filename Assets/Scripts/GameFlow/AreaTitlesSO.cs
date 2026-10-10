using System;
using System.Collections.Generic;
using TawanOS.MapEngine;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// The area title shown when the player enters a map node (<see cref="TawanOS.UI.SceneTransition.ShowTitle"/>):
    /// the name of the place and what to do there, one pair per node type. Lives at Resources/AreaTitles so a build
    /// finds it; made by Tools/TawanOS/Game Flow/Create Area Titles. A type missing from the asset uses
    /// <see cref="Defaults"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "AreaTitles", menuName = "TawanOS/Game Flow/Area Titles")]
    public class AreaTitlesSO : ScriptableObject
    {
        public const string ResourceName = "AreaTitles";

        [Serializable]
        public struct Entry
        {
            public NodeType nodeType;
            [Tooltip("Name of the place, shown big (e.g. ทางเปลี่ยว).")]
            public string title;
            [Tooltip("What the player does there, shown under the name (e.g. เผชิญหน้ากับศัตรู).")]
            public string subtitle;
        }

        public List<Entry> entries = new List<Entry>();

        public static readonly Entry[] Defaults =
        {
            new Entry { nodeType = NodeType.MinorEnemy, title = "ทางเปลี่ยว", subtitle = "เผชิญหน้ากับศัตรู" },
            new Entry { nodeType = NodeType.EliteEnemy, title = "วัดร้าง", subtitle = "เผชิญหน้ากับศัตรูระดับสูง" },
            new Entry { nodeType = NodeType.Boss, title = "ใจกลางป่าช้า", subtitle = "ปราบผีตายโหง เรียกขวัญกลับคืน" },
            new Entry { nodeType = NodeType.Event, title = "หมอกดำ", subtitle = "เลือกทางของเจ้าในสายหมอก" },
            new Entry { nodeType = NodeType.Treasure, title = "กองของเซ่น", subtitle = "เลือกรับของเซ่นไหว้" },
            new Entry { nodeType = NodeType.Store, title = "ศาลพระภูมิ", subtitle = "แลกธูปเป็นการ์ดและเครื่องราง" },
            new Entry { nodeType = NodeType.RestSite, title = "เมรุ", subtitle = "เผาการ์ดออกจากสำรับ" },
        };

        private static AreaTitlesSO cached;

        public static AreaTitlesSO Load()
        {
            if (cached == null) cached = Resources.Load<AreaTitlesSO>(ResourceName);
            return cached;
        }

        /// <summary>Title and subtitle for <paramref name="type"/>: the asset's entry, else the default.</summary>
        public static Entry Get(NodeType type)
        {
            var asset = Load();
            if (asset != null)
            {
                foreach (var e in asset.entries)
                    if (e.nodeType == type && !string.IsNullOrEmpty(e.title)) return e;
            }
            foreach (var e in Defaults)
                if (e.nodeType == type) return e;
            return new Entry { nodeType = type };
        }
    }
}
