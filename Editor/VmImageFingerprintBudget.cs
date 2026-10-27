using System;
using System.IO;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmImageFingerprintBudget
    {
        private long remainingPixels = 64L * 1024 * 1024;
        private long remainingBytes = 256L * 1024 * 1024;
        private long remainingRuns = 4L * 1024 * 1024;
        private long remainingComparisons = 200000;
        private long remainingRunComparisons = 16L * 1024 * 1024;

        internal void AdmitFile(long bytes)
        {
            if (bytes < 1 || bytes > 64L * 1024 * 1024 || bytes > remainingBytes)
                throw new VmImageDuplicateBudgetException("Image duplicate encoded-byte budget exceeded (64 MiB/image, 256 MiB/request).");
            remainingBytes -= bytes;
        }

        internal void AdmitImage(byte[] bytes, string path)
        {
            ReadDimensions(bytes, path, out int width, out int height);
            long pixels = (long)width * height;
            if (width < 1 || height < 1 || width > 16384 || height > 16384 ||
                pixels > 16L * 1024 * 1024 || pixels > remainingPixels)
                throw new VmImageDuplicateBudgetException($"Image duplicate pixel budget exceeded by '{path}' ({width}x{height}; 16 Mi/image, 64 Mi/request).");
            remainingPixels -= pixels;
        }

        internal void AdmitRuns(int count)
        {
            if (count > remainingRuns)
                throw new VmImageDuplicateBudgetException("Image duplicate retained-run budget exceeded (4 Mi entries/request).");
            remainingRuns -= count;
        }

        internal void AdmitComparison(int runs)
        {
            if (remainingComparisons == 0 || runs > remainingRunComparisons)
                throw new VmImageDuplicateBudgetException("Nine-slice comparison budget exceeded (200000 pairs, 16 Mi run entries/request).");
            remainingComparisons--;
            remainingRunComparisons -= runs;
        }

        private static void ReadDimensions(byte[] bytes, string path, out int width, out int height)
        {
            if (Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                if (bytes.Length < 33 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 ||
                    bytes[3] != 71 || bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R')
                    throw new InvalidDataException($"Invalid PNG header: '{path}'.");
                uint w = VmPngImage.ReadBigEndian(bytes, 16);
                uint h = VmPngImage.ReadBigEndian(bytes, 20);
                width = w <= int.MaxValue ? (int)w : int.MaxValue;
                height = h <= int.MaxValue ? (int)h : int.MaxValue;
                return;
            }
            if (bytes.Length < 4 || bytes[0] != 255 || bytes[1] != 216)
                throw new InvalidDataException($"Invalid JPEG header: '{path}'.");
            int offset = 2;
            while (offset < bytes.Length)
            {
                if (bytes[offset++] != 255)
                    throw new InvalidDataException($"Invalid JPEG marker: '{path}'.");
                while (offset < bytes.Length && bytes[offset] == 255) offset++;
                if (offset >= bytes.Length) break;
                int marker = bytes[offset++];
                if (marker == 217 || marker == 218) break;
                if (marker == 1 || marker >= 208 && marker <= 215) continue;
                if (offset + 2 > bytes.Length) break;
                int length = (bytes[offset] << 8) | bytes[offset + 1];
                if (length < 2 || offset + length > bytes.Length) break;
                if (marker >= 192 && marker <= 207 && marker != 196 && marker != 200 && marker != 204)
                {
                    if (length < 8) break;
                    height = (bytes[offset + 3] << 8) | bytes[offset + 4];
                    width = (bytes[offset + 5] << 8) | bytes[offset + 6];
                    return;
                }
                offset += length;
            }
            throw new InvalidDataException($"JPEG dimensions are missing: '{path}'.");
        }
    }
}
