using System.Collections;
using UnityEngine;

namespace TawanOS.MapEngine
{
    /// <summary>
    /// The paper under the map, laid out of the four pieces in Assets/Art/MapPaper so it fits any map length:
    /// หัว (torn start edge) → เริ่มวนรอบแรก → ต่อไปเรื่อยๆ (repeated as the map gets longer) → ท้าย (torn end edge).
    /// The paper reaches from <see cref="margin"/> before the start node to <see cref="margin"/> past the boss
    /// (ใจกลางป่าช้า), using the board columns of <see cref="MapManager.TableFloorX"/>. Built and saved into the scene
    /// by Tools/TawanOS/Map Engine/Setup Map Paper Strip; laid out again when the map loads.
    /// </summary>
    public class MapPaperStrip : MonoBehaviour
    {
        [System.Serializable]
        public class Piece
        {
            public Material material;
            [Tooltip("Width in pixels of the part of the picture that is paper (the material crops to it).")]
            public float pixelWidth = 1000f;
        }

        [Header("Pieces (materials crop each picture to its paper)")]
        public Piece head = new Piece();
        public Piece firstLoop = new Piece();
        public Piece repeat = new Piece();
        public Piece tail = new Piece();

        [Header("Size and place")]
        [Tooltip("Height in pixels of the paper band shared by all four pictures.")]
        public float pixelHeight = 982f;
        [Tooltip("World depth (Z) of the paper band.")]
        public float worldDepth = 31.6f;
        [Tooltip("World Z of the middle of the paper band.")]
        public float centerZ = -7.86f;
        [Tooltip("World height of the paper surface.")]
        public float surfaceY = 0.027f;
        [Tooltip("How far the paper reaches before the start node and past the boss.")]
        public float margin = 30.7f;
        [Tooltip("Each piece reaches this far under the next one, so no gap shows at the joins.")]
        public float overlap = 0.05f;
        [Tooltip("Map length used in the editor before a map is generated.")]
        public int editorFloors = 7;

        private void Start()
        {
            StartCoroutine(LayoutWhenMapReady());
        }

        // MapManager rolls (or loads) the map in its own Start; wait for it so the floor count is the run's
        private IEnumerator LayoutWhenMapReady()
        {
            for (int i = 0; i < 10 && (MapManager.Instance == null || MapManager.Instance.CurrentGraph == null); i++) yield return null;
            var graph = MapManager.Instance != null ? MapManager.Instance.CurrentGraph : null;
            Layout(graph != null && graph.totalFloors > 0 ? graph.totalFloors : editorFloors);
        }

        /// <summary>Lays the pieces from the start node side (+X) to the boss side (−X) for a map of this many floors.</summary>
        public void Layout(int totalFloors)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            float startX = MapManager.TableFloorX(-1, totalFloors) + margin;
            float endX = MapManager.TableFloorX(totalFloors, totalFloors) - margin;
            float length = startX - endX;
            if (length <= 0f || pixelHeight <= 0f) return;

            // One world unit per pixel both ways keeps the paper grain round; repeats fill a longer map, and the
            // whole strip is stretched a little so it ends exactly at the far margin
            float unitsPerPixel = worldDepth / pixelHeight;
            float basePixels = head.pixelWidth + firstLoop.pixelWidth + tail.pixelWidth;
            int repeats = repeat.pixelWidth > 0f
                ? Mathf.Max(0, Mathf.RoundToInt((length / unitsPerPixel - basePixels) / repeat.pixelWidth))
                : 0;
            float stretch = length / ((basePixels + repeats * repeat.pixelWidth) * unitsPerPixel);

            float x = startX;
            x = Place("Head", head, x, unitsPerPixel * stretch);
            x = Place("FirstLoop", firstLoop, x, unitsPerPixel * stretch);
            for (int i = 0; i < repeats; i++) x = Place("Repeat" + (i + 1), repeat, x, unitsPerPixel * stretch);
            Place("Tail", tail, x, unitsPerPixel * stretch);
        }

        // One flat quad lying on the table; its picture runs toward −X and its top edge toward −Z, like the old paper
        private float Place(string pieceName, Piece piece, float fromX, float unitsPerPixelX)
        {
            float width = piece.pixelWidth * unitsPerPixelX;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = pieceName;
            var collider = quad.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
            quad.transform.SetParent(transform, false);
            quad.transform.position = new Vector3(fromX - width * 0.5f, surfaceY, centerZ);
            quad.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
            quad.transform.localScale = new Vector3(width + overlap, worldDepth, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = piece.material;
            return fromX - width;
        }
    }
}
