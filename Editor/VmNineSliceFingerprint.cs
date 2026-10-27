using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmNineSliceFingerprint
    {
        internal readonly string Hash;
        internal readonly int[] Columns;
        internal readonly int[] Rows;

        private VmNineSliceFingerprint(string hash, int[] columns, int[] rows)
        {
            Hash = hash;
            Columns = columns;
            Rows = rows;
        }

        internal static VmNineSliceFingerprint Create(Color32[] pixels, int width, int height,
            VmImageFingerprintBudget budget)
        {
            int[] columns = FindRuns(pixels, width, height, true);
            int[] rows = FindRuns(pixels, width, height, false);
            budget.AdmitRuns(columns.Length + rows.Length);
            using var sha = SHA256.Create();
            byte[] header = Encoding.ASCII.GetBytes($"nine-slice-rgba32:{columns.Length}x{rows.Length}:");
            sha.TransformBlock(header, 0, header.Length, header, 0);
            var buffer = new byte[65536];
            int used = 0;
            int y = 0;
            foreach (int row in rows)
            {
                int x = 0;
                foreach (int column in columns)
                {
                    if (used == buffer.Length)
                    {
                        sha.TransformBlock(buffer, 0, used, buffer, 0);
                        used = 0;
                    }
                    Color32 pixel = pixels[y * width + x];
                    buffer[used++] = pixel.a == 0 ? (byte)0 : pixel.r;
                    buffer[used++] = pixel.a == 0 ? (byte)0 : pixel.g;
                    buffer[used++] = pixel.a == 0 ? (byte)0 : pixel.b;
                    buffer[used++] = pixel.a;
                    x += column;
                }
                y += row;
            }
            sha.TransformFinalBlock(buffer, 0, used);
            return new VmNineSliceFingerprint(BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant(),
                columns, rows);
        }

        internal bool TryMatch(VmNineSliceFingerprint other, VmImageFingerprintBudget budget,
            out Dictionary<string, object> proof)
        {
            proof = null;
            if (other == null || Hash != other.Hash) return false;
            budget.AdmitComparison(Columns.Length + Rows.Length);
            if (!TryMatchAxis(Columns, other.Columns, out int column) ||
                !TryMatchAxis(Rows, other.Rows, out int row))
                return false;
            proof = new Dictionary<string, object>
            {
                { "kind", "nineSlice" }, { "stretchX", column >= 0 }, { "stretchY", row >= 0 },
                { "sourceBorder", DescribeBorder(Columns, Rows, column, row) },
                { "duplicateBorder", DescribeBorder(other.Columns, other.Rows, column, row) },
            };
            return true;
        }

        private static int[] FindRuns(Color32[] pixels, int width, int height, bool columns)
        {
            int axisLength = columns ? width : height;
            int crossLength = columns ? height : width;
            var runs = new List<int>();
            int length = 1;
            for (int axis = 1; axis < axisLength; axis++)
            {
                bool equal = true;
                for (int cross = 0; cross < crossLength; cross++)
                {
                    int index = columns ? cross * width + axis : axis * width + cross;
                    int previous = columns ? index - 1 : index - width;
                    Color32 current = pixels[index];
                    Color32 preceding = pixels[previous];
                    if (current.a == preceding.a && (current.a == 0 ||
                        current.r == preceding.r && current.g == preceding.g && current.b == preceding.b)) continue;
                    equal = false;
                    break;
                }
                if (equal) length++;
                else
                {
                    runs.Add(length);
                    length = 1;
                }
            }
            runs.Add(length);
            return runs.ToArray();
        }

        private static bool TryMatchAxis(int[] source, int[] duplicate, out int changedRun)
        {
            changedRun = -1;
            if (source.Length != duplicate.Length) return false;
            for (int index = 0; index < source.Length; index++)
            {
                if (source[index] == duplicate[index]) continue;
                if (changedRun >= 0 || source.Length > 1 && (index == 0 || index == source.Length - 1))
                    return false;
                changedRun = index;
            }
            return true;
        }

        private static Dictionary<string, object> DescribeBorder(int[] columns, int[] rows, int column, int row)
        {
            return new Dictionary<string, object>
            {
                { "left", SumBefore(columns, column) }, { "bottom", SumBefore(rows, row) },
                { "right", SumAfter(columns, column) }, { "top", SumAfter(rows, row) },
            };
        }

        private static int SumBefore(int[] runs, int index)
        {
            int sum = 0;
            for (int i = 0; i < index; i++) sum += runs[i];
            return sum;
        }

        private static int SumAfter(int[] runs, int index)
        {
            if (index < 0) return 0;
            int sum = 0;
            for (int i = index + 1; i < runs.Length; i++) sum += runs[i];
            return sum;
        }
    }
}
