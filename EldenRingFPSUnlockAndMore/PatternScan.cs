using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace EldenRingFPSUnlockAndMore
{
    /// <summary>
    /// WHY OH WHY DON'T WE HAVE .NET 6 AS DEFAULT IN WINDOWS YET MICROSOFT???
    /// I AM FORCED TO USE STUPID ASS .NET 4.8 HERE CAUSE WINDOWS DOESN'T INSTALL THE LATEST .NET 6 BY DEFAULT
    /// https://github.com/uberhalit/PatternScanBench/blob/master/PatternScanBench/Implementations/PatternScanLazySIMD.cs
    /// </summary>
    internal class PatternScan
    {
        private static long dwStart = 0;
        private static byte[] bData;

        /// <summary>
        /// Initialize PatternScanner and read all memory from process.
        /// </summary>
        /// <param name="hProcess">Handle to the process in whose memory pattern will be searched for.</param>
        /// <param name="pModule">Module which will be searched for the pattern.</param>
        internal PatternScan(IntPtr hProcess, ProcessModule pModule)
        {
            if (IntPtr.Size == 4)
                dwStart = (uint)pModule.BaseAddress;
            else if (IntPtr.Size == 8)
                dwStart = (long)pModule.BaseAddress;
            int nSize = pModule.ModuleMemorySize;
            bData = new byte[nSize];

            if (!WinAPI.ReadProcessMemory(hProcess, dwStart, bData, (ulong)nSize, out IntPtr lpNumberOfBytesRead))
            {
                MainWindow.LogToFile("Could not read memory in PatternScan()!");
                return;
            }
            if (lpNumberOfBytesRead.ToInt64() != nSize || bData == null || bData.Length == 0)
            {
                MainWindow.LogToFile("ReadProcessMemory error in PatternScan()!");
                return;
            }
        }

        ~PatternScan()
        {
            bData = null;
            GC.Collect();
        }

        /// <summary>
        /// Returns address of pattern. Can match 0.
        /// </summary>
        /// <param name="cbMemory">The byte array to scan.</param>
        /// <param name="szPattern">A string that determines the pattern, '??' acts as wildcard.</param>
        /// <returns>-1 if pattern is not found.</returns>
        internal long FindPattern(string szPattern)
        {
            string[] saPattern = szPattern.Split(' ');
            string szMask = "";
            for (int i = 0; i < saPattern.Length; i++)
            {
                if (saPattern[i] == "??")
                {
                    szMask += "?";
                    saPattern[i] = "0";
                }
                else szMask += "x";
            }
            byte[] cbPattern = new byte[saPattern.Length];
            for (int i = 0; i < saPattern.Length; i++)
                cbPattern[i] = Convert.ToByte(saPattern[i], 0x10);

            if (cbPattern == null || cbPattern.Length == 0)
                throw new ArgumentException("Pattern's length is zero!");
            if (cbPattern.Length != szMask.Length)
                throw new ArgumentException("Pattern's bytes and szMask must be of the same size!");

            //if (Sse2.IsSupported && Bmi1.IsSupported && Vector.IsHardwareAccelerated)
                //return FindPattern_SIMD(ref cbMemory, ref cbPattern, szMask);
            //else
            return FindPattern_Native(ref bData, ref cbPattern, szMask);
        }

        /// <summary>
        /// Returns address of pattern using 'LazySIMD' implementation by uberhalit. Can match 0.
        /// </summary>
        /// <param name="cbMemory">The byte array to scan.</param>
        /// <param name="cbPattern">The byte pattern to look for, wildcard positions are replaced by 0.</param>
        /// <param name="szMask">A string that determines how pattern should be matched, 'x' is match, '?' acts as wildcard.</param>
        /// <returns>-1 if pattern is not found.</returns>
        //private static long FindPattern_SIMD(ref byte[] cbMemory, ref byte[] cbPattern, string szMask)
        //{
        //    ref byte pCxMemory = ref MemoryMarshal.GetArrayDataReference(cbMemory);
        //    ref byte pCxPattern = ref MemoryMarshal.GetArrayDataReference(cbPattern);
        //
        //    ReadOnlySpan<ushort> matchTable = BuildMatchIndexes(szMask, szMask.Length);
        //    int matchTableLength = matchTable.Length;
        //    Vector128<byte>[] patternVectors = PadPatternToVector128(cbPattern);
        //    ref Vector128<byte> pVec = ref patternVectors[0];
        //    int vectorLength = patternVectors.Length;
        //
        //    Vector128<byte> firstByteVec = Vector128.Create(pCxPattern);
        //    ref Vector128<byte> pFirstVec = ref firstByteVec;
        //
        //    int simdJump = 16 - 1;
        //    int searchLength = cbMemory.Length - (16 > cbPattern.Length ? 16 : cbPattern.Length);
        //    for (int position = 0; position < searchLength; position++, pCxMemory = ref Unsafe.Add(ref pCxMemory, 1))
        //    {
        //        int findFirstByte = Sse2.MoveMask(Sse2.CompareEqual(pFirstVec, Unsafe.As<byte, Vector128<byte>>(ref pCxMemory)));
        //        if (findFirstByte == 0)
        //        {
        //            position += simdJump;
        //            pCxMemory = ref Unsafe.Add(ref pCxMemory, simdJump);
        //            continue;
        //        }
        //        int offset = BitOperations.TrailingZeroCount((uint)findFirstByte);
        //
        //        position += offset;
        //        pCxMemory = ref Unsafe.Add(ref pCxMemory, offset);
        //
        //        int iMatchTableIndex = 0;
        //        bool found = true;
        //        for (int i = 0; i < vectorLength; i++)
        //        {
        //            int compareResult = Sse2.MoveMask(Sse2.CompareEqual(Unsafe.Add(ref pVec, i), Unsafe.As<byte, Vector128<byte>>(ref Unsafe.Add(ref pCxMemory, 1 + (i * 16)))));
        //
        //            for (; iMatchTableIndex < matchTableLength; iMatchTableIndex++)
        //            {
        //                int matchIndex = matchTable[iMatchTableIndex];
        //                if (i > 0) matchIndex -= i * 16;
        //                if (matchIndex >= 16)
        //                    break;
        //                if (((compareResult >> matchIndex) & 1) == 1)
        //                    continue;
        //                found = false;
        //                break;
        //            }
        //
        //            if (!found)
        //                break;
        //        }
        //
        //        if (found)
        //            return dwStart + position;
        //    }
        //
        //    return -1;
        //} 

        /// <summary>
        /// Returns address of pattern using 'BytePointerWithJIT' implementation by M i c h a e l. Can match 0.
        /// </summary>
        /// <param name="cbMemory">The byte array to scan.</param>
        /// <param name="cbPattern">The byte pattern to look for, wildcard positions are replaced by 0.</param>
        /// <param name="szMask">A string that determines how pattern should be matched, 'x' is match, '?' acts as wildcard.</param>
        /// <returns>-1 if pattern is not found.</returns>
        private static long FindPattern_Native(ref byte[] cbMemory, ref byte[] cbPattern, string szMask)
        {
            int maskLength = szMask.Length;
            int search_len = cbMemory.Length;
            ref byte region_it = ref cbMemory[0];
            ref byte pattern = ref cbPattern[0];

            for (int i = 0; i < search_len; ++i, region_it = ref Unsafe.Add(ref region_it, 1))
            {
                if (region_it == pattern)
                {
                    ref byte pattern_it = ref pattern;
                    ref byte memory_it = ref region_it;
                    bool found = true;

                    for (int j = 0; j < maskLength && (i + j) < search_len; ++j, memory_it = ref Unsafe.Add(ref memory_it, 1), pattern_it = ref Unsafe.Add(ref pattern_it, 1))
                    {
                        if (szMask[j] != 'x') continue;
                        if (memory_it != pattern_it)
                        {
                            found = false;
                            break;
                        }
                    }

                    if (found)
                        return dwStart + i;
                }
            }

            return -1;
        }
    }
}
