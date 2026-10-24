using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// The pointing-hand cursor over anything that can be clicked, the normal arrow elsewhere (Game Designer, 24 Oct).
    /// UI: a button (or anything that takes a click) under the pointer that can be pressed. The 3D scene: whatever
    /// shows its hover (a hand card, a map node, the furnace door, the graveyard, ...) calls <see cref="Hover"/> in its
    /// Update. The hand is Windows' own link cursor, read from the system at start; elsewhere the arrow stays.
    /// Created by itself at start and kept across scenes.
    /// </summary>
    [DefaultExecutionOrder(10000)] // after everything that calls Hover in its Update
    public class PointerCursor : MonoBehaviour
    {
        private static PointerCursor instance;
        private static int hoverFrame = -1;

        private Texture2D hand;
        private Vector2 handHotspot;
        private bool showingHand;
        private readonly System.Collections.Generic.List<RaycastResult> hits = new System.Collections.Generic.List<RaycastResult>();
        private PointerEventData pointer;
        private EventSystem pointerSystem;

        /// <summary>The hand cursor was read from the system (false elsewhere than Windows, or if reading it failed).</summary>
        public static bool HandAvailable => instance != null && instance.hand != null;

        /// <summary>Something clickable in the scene is under the pointer this frame.</summary>
        public static void Hover() => hoverFrame = Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("PointerCursor");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PointerCursor>();
        }

        private void Awake()
        {
            hand = SystemHandCursor(out handHotspot);
        }

        private void OnDestroy()
        {
            if (showingHand) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (hand != null) Destroy(hand);
            if (instance == this) instance = null;
        }

        private void LateUpdate()
        {
            if (hand == null) return;
            bool want = Cursor.visible && (hoverFrame == Time.frameCount || UiClickableUnderPointer());
            if (want == showingHand) return;
            showingHand = want;
            Cursor.SetCursor(want ? hand : null, want ? handHotspot : Vector2.zero, CursorMode.Auto);
        }

        // The top UI element under the pointer takes a click and can be pressed now
        private bool UiClickableUnderPointer()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (pointer == null || pointerSystem != es)
            {
                pointer = new PointerEventData(es);
                pointerSystem = es;
            }
            pointer.position = Input.mousePosition;
            hits.Clear();
            es.RaycastAll(pointer, hits);
            // Only the screen's own UI here; 3D things say so themselves (Hover), as only some of them can be clicked
            if (hits.Count == 0 || !(hits[0].module is GraphicRaycaster)) return false;

            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            if (handler == null) return false;
            var selectable = handler.GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return false;
            var group = handler.GetComponentInParent<CanvasGroup>();
            return group == null || (group.interactable && group.blocksRaycasts);
        }

        // ---- Windows' link (hand) cursor as a texture ----

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int IdcHand = 32649;

        [StructLayout(LayoutKind.Sequential)]
        private struct IconInfo
        {
            public int fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Bitmap
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public uint[] colors;
        }

        [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr instance, int name);
        [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern int GetObject(IntPtr obj, int size, out Bitmap bitmap);
        [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BitmapInfo info, uint usage);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);

        // Rows top-down as 32-bit BGRA
        private static byte[] Pixels(IntPtr dc, IntPtr bitmap, int width, int height)
        {
            var info = new BitmapInfo
            {
                biSize = 40, biWidth = width, biHeight = -height, biPlanes = 1, biBitCount = 32, colors = new uint[256]
            };
            var bits = new byte[width * height * 4];
            return GetDIBits(dc, bitmap, 0, (uint)height, bits, ref info, 0) == height ? bits : null;
        }

        private static Texture2D SystemHandCursor(out Vector2 hotspot)
        {
            hotspot = Vector2.zero;
            try
            {
                var cursor = LoadCursor(IntPtr.Zero, IdcHand);
                if (cursor == IntPtr.Zero || !GetIconInfo(cursor, out var info)) return null;
                IntPtr dc = GetDC(IntPtr.Zero);
                try
                {
                    GetObject(info.hbmMask, Marshal.SizeOf(typeof(Bitmap)), out var maskBmp);
                    int width = maskBmp.bmWidth;
                    bool colour = info.hbmColor != IntPtr.Zero;
                    // A cursor with no colour bitmap keeps both halves in the mask: AND on top, XOR below
                    int height = colour ? maskBmp.bmHeight : maskBmp.bmHeight / 2;
                    byte[] mask = Pixels(dc, info.hbmMask, width, colour ? height : height * 2);
                    byte[] rgb = colour ? Pixels(dc, info.hbmColor, width, height) : null;
                    if (mask == null || (colour && rgb == null)) return null;

                    bool hasAlpha = false;
                    if (colour) for (int i = 3; i < rgb.Length; i += 4) if (rgb[i] != 0) { hasAlpha = true; break; }

                    var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
                    var px = new Color32[width * height];
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int i = (y * width + x) * 4;
                        bool andBit = mask[i] != 0; // 1 = see-through where there is no alpha
                        Color32 c;
                        if (colour)
                            c = new Color32(rgb[i + 2], rgb[i + 1], rgb[i], hasAlpha ? rgb[i + 3] : (byte)(andBit ? 0 : 255));
                        else
                        {
                            byte xor = mask[((y + height) * width + x) * 4];
                            c = andBit ? new Color32(0, 0, 0, 0) : new Color32(xor, xor, xor, 255);
                        }
                        px[(height - 1 - y) * width + x] = c; // Unity's rows run bottom-up
                    }
                    tex.SetPixels32(px);
                    tex.Apply(false, false);
                    hotspot = new Vector2(info.xHotspot, info.yHotspot);
                    return tex;
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, dc);
                    if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
                    if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PointerCursor] Could not read the system hand cursor: {e.Message}");
                return null;
            }
        }
#else
        private static Texture2D SystemHandCursor(out Vector2 hotspot)
        {
            hotspot = Vector2.zero;
            return null; // other systems keep the arrow
        }
#endif
    }
}
