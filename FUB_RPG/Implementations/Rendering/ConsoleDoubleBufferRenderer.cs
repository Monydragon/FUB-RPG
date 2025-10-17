using System;
using System.Text;

namespace Fub.Implementations.Rendering;

/// <summary>
/// Simple double-buffering console renderer that writes only changed segments.
/// Reduces flicker by avoiding Console.Clear and minimizing cursor movements.
/// </summary>
public class ConsoleDoubleBufferRenderer
{
    private int _width;
    private int _height;
    private char[,] _prev;
    private readonly StringBuilder _sb = new StringBuilder(1024);

    public ConsoleDoubleBufferRenderer(int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _prev = new char[_height, _width];
        ClearPrev();
        Console.CursorVisible = false;
    }

    private void ClearPrev()
    {
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                _prev[y, x] = '\0';
    }

    /// <summary>
    /// Call when console window / map size changes
    /// </summary>
    public void Resize(int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _prev = new char[_height, _width];
        ClearPrev();
    }

    /// <summary>
    /// Draw the frame by only writing changed segments.
    /// frame must be [height, width] and fully populated
    /// </summary>
    public void DrawFrame(char[,] frame, bool force = false)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        int fh = frame.GetLength(0);
        int fw = frame.GetLength(1);
        int rows = Math.Min(fh, _height);
        int cols = Math.Min(fw, _width);

        // If forced full redraw, treat prev as empty
        if (force)
        {
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    _prev[y, x] = '\0';
        }

        // For each row, find changed runs and write them in batches
        for (int y = 0; y < rows; y++)
        {
            int x = 0;
            while (x < cols)
            {
                // Skip identical characters
                if (!Changed(frame, x, y))
                {
                    x++;
                    continue;
                }

                // Start a run of changes
                int startX = x;
                _sb.Clear();
                while (x < cols && Changed(frame, x, y))
                {
                    _sb.Append(frame[y, x]);
                    // update prev as we go to avoid re-drawing in the next frame
                    _prev[y, x] = frame[y, x];
                    x++;
                }

                // Write the run with a single position + write call
                try
                {
                    Console.SetCursorPosition(startX, y);
                    Console.Write(_sb.ToString());
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Console size changed underneath us; ignore this run
                }
            }
        }
    }

    private bool Changed(char[,] frame, int x, int y)
    {
        return frame[y, x] != _prev[y, x];
    }

    /// <summary>
    /// Force a full screen clear and reset tracking
    /// </summary>
    public void Clear()
    {
        Console.Clear();
        ClearPrev();
    }
}

