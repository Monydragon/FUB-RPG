using System;
using System.Text;
using System.IO;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Fub.Implementations.Rendering;

/// <summary>
/// Helper to convert Spectre.Console output to a char[,] buffer for double-buffering.
/// </summary>
public static class FrameBuffer
{
    /// <summary>
    /// Capture Spectre.Console rendered output as a character buffer.
    /// This strips ANSI codes to get plain text representation.
    /// </summary>
    public static char[,] CaptureAsCharBuffer(Action renderAction, int width, int height)
    {
        var buffer = new char[height, width];
        
        // Fill with spaces initially
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                buffer[y, x] = ' ';

        // Capture console output
        var originalOut = Console.Out;
        var sb = new StringBuilder();
        
        try
        {
            using (var writer = new StringWriter(sb))
            {
                Console.SetOut(writer);
                renderAction();
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Parse the captured output into the buffer
        var lines = sb.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        int row = 0;
        
        foreach (var line in lines)
        {
            if (row >= height) break;
            
            // Strip ANSI codes to get plain text
            var plainText = StripAnsiCodes(line);
            
            for (int col = 0; col < Math.Min(plainText.Length, width); col++)
            {
                buffer[row, col] = plainText[col];
            }
            
            row++;
        }

        return buffer;
    }

    /// <summary>
    /// Simple ANSI code stripper (removes escape sequences)
    /// </summary>
    private static string StripAnsiCodes(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        var sb = new StringBuilder(text.Length);
        bool inEscape = false;
        
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\x1b' || text[i] == '\u009b')
            {
                inEscape = true;
                continue;
            }
            
            if (inEscape)
            {
                // End of escape sequence
                if ((text[i] >= 'A' && text[i] <= 'Z') || 
                    (text[i] >= 'a' && text[i] <= 'z'))
                {
                    inEscape = false;
                }
                continue;
            }
            
            sb.Append(text[i]);
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Build a frame buffer directly from strings (for simpler cases)
    /// </summary>
    public static char[,] FromStrings(string[] lines, int width, int height)
    {
        var buffer = new char[height, width];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (y < lines.Length && x < lines[y].Length)
                {
                    buffer[y, x] = lines[y][x];
                }
                else
                {
                    buffer[y, x] = ' ';
                }
            }
        }
        
        return buffer;
    }
}

