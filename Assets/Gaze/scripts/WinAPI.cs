using System;
using System.Runtime.InteropServices;
using UnityEngine;

/**
 * <summary>Utilities that use WinAPI</summary>
 * */
public static class WinAPI
{
    /**
     * <summary>Get the position of either standalone game window (game running as .exe), or the game window in Unity editor</summary>
     * <returns>Game window position</returns>
     * */
    public static Vector2 GetWindowPosition()
    {
        var rect = new WinRect();
        GetWindowRect(GetUnityWindowHandle(), ref rect);

        return new Vector2(rect.left, rect.top);
    }

    /**
     * <summary>Get the rectangle of either standalone game window (game running as .exe), or the game window in Unity editor</summary>
     * <returns>Game window rectangle</returns>
     * */
    public static Rect GetWindowRect()
    {
        var rect = new WinRect();
        var gameHwnd = Application.isEditor ? GetUnityWindowHandle() : GetGameWindowHandle();
        GetWindowRect(gameHwnd, ref rect);
        return new Rect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
    }

    #region WinAPI imports

    struct WinRect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
    static extern bool GetWindowRect(IntPtr hwnd, ref WinRect rect);
    [DllImport("user32.dll", EntryPoint = "FindWindow")]
    static extern IntPtr FindWindow(string className, string windowName);
    [DllImport("user32.dll", EntryPoint = "FindWindowEx")]
    static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string windowName);

    #endregion

    #region Internal methods

    static IntPtr GetUnityWindowHandle()
    {
        IntPtr unityHwnd = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "UnityContainerWndClass", null);
        if (unityHwnd == IntPtr.Zero)
        {
            throw new Exception("Unity CONTAINER is not found");
        }

        IntPtr gameHwnd = FindWindowEx(unityHwnd, IntPtr.Zero, null, "UnityEditor.GameView");
        if (gameHwnd == IntPtr.Zero)
        {
            throw new Exception("Game window in Unity Editor is not found");
        }

        return gameHwnd;
    }

    static IntPtr GetGameWindowHandle()
    {
        IntPtr gameHwnd = FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, Application.productName);
        if (gameHwnd != IntPtr.Zero)
        {
            return gameHwnd;
        }

        gameHwnd = FindWindow(null, Application.productName);
        if (gameHwnd == IntPtr.Zero)
        {
            throw new Exception("Standalone Game window is not found");
        }

        return gameHwnd;
    }

    #endregion
}
