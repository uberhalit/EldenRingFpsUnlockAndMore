using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace EldenRingFPSUnlockAndMore
{
    internal enum PointerStyle
    {
        dwRelative,
        dwAbsolute,
        qwRelative,
        qwAbsolute
    }

    internal static class PointerStyleMethods
    {
        /// <summary>
        /// Returns pointer size of pointer style.
        /// </summary>
        public static int PointerSize(this PointerStyle pointerStyle)
        {
            if (pointerStyle == PointerStyle.qwAbsolute || pointerStyle == PointerStyle.qwRelative) 
                return 8;
            return 4;
        }
    }

    class MemoryCaveGenerator
    {
        private class MemoryCave
        {
            internal readonly long InstructionAddress;
            internal readonly long CaveAddress;
            internal bool Active;
            private byte[] _originalInstruction;
            internal byte[] OriginalInstruction
            {
                get => _originalInstruction;
                set
                {
                    if (_originalInstruction == null)
                        _originalInstruction = value;
                }
            }

            internal MemoryCave(long instructionAddress, long caveAddress)
            {
                InstructionAddress = instructionAddress;
                CaveAddress = caveAddress;
                _originalInstruction = null;
                Active = false;
            }
        }

        private class DataCave : MemoryCave
        {
            internal readonly PointerStyle PointerStyle;

            internal DataCave(long instructionAddress, long codeCaveAddress, PointerStyle pointerStyle) : base(instructionAddress, codeCaveAddress)
            {
                PointerStyle = pointerStyle;
            }
        }

        private class CodeCave : MemoryCave
        {
            internal readonly int OverwriteLength;

            internal CodeCave(long instructionAddress, long codeCaveAddress, int overwriteLength) : base(instructionAddress, codeCaveAddress)
            {
                OverwriteLength = overwriteLength;
            }
        }

        private Dictionary<string, DataCave> _dataCaves;
        private Dictionary<string, CodeCave> _codeCaves;
        private static IntPtr _hProcess;
        private static long _lpBaseAddress;

        /// <summary>
        /// Initialize functionality to create and manage memory caves in given process's memory.
        /// </summary>
        /// <param name="hProcess">The handle to the process, needs all access flag.</param>
        /// <param name="lpBaseAddress">The base address of the process.</param>
        internal MemoryCaveGenerator(IntPtr hProcess, long lpBaseAddress)
        {
            _dataCaves = new Dictionary<string, DataCave>();
            _codeCaves = new Dictionary<string, CodeCave>();
            _hProcess = hProcess;
            _lpBaseAddress = lpBaseAddress;
        }

        /// <summary>
        /// Creates a new data cave with a unique name.
        /// </summary>
        /// <param name="szCaveName">The unique name of the data cave. Used to enable and disable caves.</param>
        /// <param name="lpPointerAddress">The address of the pointer that should later access the data in the cave.</param>
        /// <param name="cbDataInsert">The data to place inside the data cave.</param>
        /// <param name="pointerStyle">The type of the pointer to later replace with data cave address, default assumes a 4 byte relative pointer.</param>
        /// <returns>True if code cave has been successfully created and is ready to be activated, false otherwise.</returns>
        internal bool CreateNewDataCave(string szCaveName, long lpPointerAddress, byte[] cbDataInsert, PointerStyle pointerStyle = PointerStyle.dwRelative)
        {
            if (_dataCaves.ContainsKey(szCaveName))
                _dataCaves.Remove(szCaveName);
            long caveAddress = CreateDataCaveForPointer(_hProcess, _lpBaseAddress, lpPointerAddress, pointerStyle.PointerSize(), cbDataInsert);
            if (caveAddress < 0)
                return false;

            // read original pointer
            byte[] cbOriginalPointer = new byte[pointerStyle.PointerSize()];
            if (!ReadProcessMemory(_hProcess, lpPointerAddress, cbOriginalPointer, (ulong)pointerStyle.PointerSize(), out IntPtr lpNumberOfBytesRead) || lpNumberOfBytesRead.ToInt32() != pointerStyle.PointerSize())
            {
                MainWindow.LogToFile("Failed to read original pointer in MemoryCaveGenerator()!");
                return false;
            }
            _dataCaves.Add(szCaveName, new DataCave(lpPointerAddress, caveAddress, pointerStyle));
            _dataCaves[szCaveName].OriginalInstruction = cbOriginalPointer;
            return true;
        }

        /// <summary>
        /// Activates a data cave by name.
        /// </summary>
        /// <param name="szCaveName">The unique name of the data cave.</param>
        /// <returns>True if data cave could be activated.</returns>
        internal bool ActivateDataCaveByName(string szCaveName)
        {
            if (!_dataCaves.ContainsKey(szCaveName) || _dataCaves[szCaveName].Active)
                return false;

            // get pointer value
            byte[] cbPointer = new byte[_dataCaves[szCaveName].PointerStyle.PointerSize()];
            if (_dataCaves[szCaveName].PointerStyle == PointerStyle.dwAbsolute || _dataCaves[szCaveName].PointerStyle == PointerStyle.qwAbsolute)
                cbPointer = _dataCaves[szCaveName].PointerStyle.PointerSize() == 4 ? BitConverter.GetBytes((int)_dataCaves[szCaveName].CaveAddress) : BitConverter.GetBytes(_dataCaves[szCaveName].CaveAddress);
            else
            {
                long lpRip = _dataCaves[szCaveName].InstructionAddress + _dataCaves[szCaveName].PointerStyle.PointerSize();
                cbPointer = _dataCaves[szCaveName].PointerStyle.PointerSize() == 4 ? BitConverter.GetBytes((int)(_dataCaves[szCaveName].CaveAddress - lpRip)) : BitConverter.GetBytes(_dataCaves[szCaveName].CaveAddress - lpRip);
            }

            // overwrite pointer with new data cave address
            if (!WriteProcessMemory(_hProcess, _dataCaves[szCaveName].InstructionAddress, cbPointer, (ulong)cbPointer.Length, out IntPtr lpNumberOfBytesWritten) || lpNumberOfBytesWritten.ToInt32() != _dataCaves[szCaveName].PointerStyle.PointerSize())
            {
                MainWindow.LogToFile("Failed to overwrite target pointer in MemoryCaveGenerator()!");
                return false;
            }

            _dataCaves[szCaveName].Active = true;
            return true;
        }

        /// <summary>
        /// Deactivates a data cave by name.
        /// </summary>
        /// <param name="szCaveName">The unique name of the data cave.</param>
        /// <returns>True if data cave could be deactivated.</returns>
        internal bool DeactivateDataCaveByName(string szCaveName)
        {
            if (!_dataCaves.ContainsKey(szCaveName) || !_dataCaves[szCaveName].Active || _dataCaves[szCaveName].InstructionAddress < 0 || _dataCaves[szCaveName].OriginalInstruction == null)
                return false;

            if (!WriteProcessMemory(_hProcess, _dataCaves[szCaveName].InstructionAddress, _dataCaves[szCaveName].OriginalInstruction, (ulong)_dataCaves[szCaveName].OriginalInstruction.Length, out IntPtr lpNumberOfBytesWritten) || lpNumberOfBytesWritten.ToInt32() != _dataCaves[szCaveName].PointerStyle.PointerSize())
            {
                MainWindow.LogToFile("Could not disable data cave in MemoryCaveGenerator()!");
                return false;
            }
            _dataCaves[szCaveName].Active = false;
            return true;
        }

        /// <summary>
        /// Updates the data inside the data cave by name.
        /// </summary>
        /// <param name="szCaveName">The unique name of the data cave.</param>
        /// <param name="cbNewData">The new data to write.</param>
        /// <returns>True if data could be written.</returns>
        internal bool UpdateDataCaveValueByName(string szCaveName, byte[] cbNewData)
        {
            if (!_dataCaves.ContainsKey(szCaveName) || _dataCaves[szCaveName].CaveAddress < 0)
                return false;

            if (!WriteProcessMemory(_hProcess, _dataCaves[szCaveName].CaveAddress, cbNewData, (ulong)cbNewData.Length, out IntPtr lpNumberOfBytesWritten) || lpNumberOfBytesWritten.ToInt32() != cbNewData.Length)
            {
                MainWindow.LogToFile("Could not write data to cave in MemoryCaveGenerator()!");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Creates a new code cave with a unique name.
        /// </summary>
        /// <param name="szCaveName">The unique name of the code cave. Used to enable and disable caves.</param>
        /// <param name="lpInstructionAddress">The address of the instruction that should be overwritten with jump.</param>
        /// <param name="dwOverwriteLength">The length of the opcodes from lpInstructionAddress that should be overwritten with NOP, must be at least 5 bytes.</param>
        /// <param name="cbCodeInject">The shellcode to place inside the code cave.</param>
        /// <param name="bCopyOverwrittenInstructions">If overwritten instructions should be copied to the end of the code cave.</param>
        /// <returns>True if code cave has been successfully created and is ready to be activated, false otherwise.</returns>
        internal bool CreateNewCodeCave(string szCaveName, long lpInstructionAddress, int dwOverwriteLength, byte[] cbCodeInject, bool bCopyOverwrittenInstructions = false)
        {
            if (_codeCaves.ContainsKey(szCaveName))
            {
                DeactivateCodeCaveByName(szCaveName);
                _codeCaves.Remove(szCaveName);
            }
            long caveAddress = CreateCodeCaveForInstruction(_hProcess, _lpBaseAddress, lpInstructionAddress, dwOverwriteLength, cbCodeInject, bCopyOverwrittenInstructions);
            if (caveAddress < 0)
                return false;
            _codeCaves.Add(szCaveName, new CodeCave(lpInstructionAddress, caveAddress, dwOverwriteLength));
            return true;
        }

        /// <summary>
        /// Activates a code cave by name.
        /// </summary>
        /// <param name="szCaveName">The unique name of the code cave.</param>
        /// <returns>True if code cave could be activated.</returns>
        internal bool ActivateCodeCaveByName(string szCaveName)
        {
            if (!_codeCaves.ContainsKey(szCaveName) || _codeCaves[szCaveName].Active)
                return false;

            byte[] originalInstructionset = ActivateCodeCaveForInstruction(_hProcess, _codeCaves[szCaveName].InstructionAddress, _codeCaves[szCaveName].OverwriteLength, _codeCaves[szCaveName].CaveAddress);
            if (originalInstructionset.Length != _codeCaves[szCaveName].OverwriteLength)
                return false;
            _codeCaves[szCaveName].OriginalInstruction = originalInstructionset;
            _codeCaves[szCaveName].Active = true;
            return true;
        }

        /// <summary>
        /// Deactivates a code cave by name.
        /// </summary>
        /// <param name="szCaveName">The unique name of the code cave.</param>
        /// <returns>True if code cave could be deactivated.</returns>
        internal bool DeactivateCodeCaveByName(string szCaveName)
        {
            if (!_codeCaves.ContainsKey(szCaveName) || !_codeCaves[szCaveName].Active || _codeCaves[szCaveName].InstructionAddress < 0 || _codeCaves[szCaveName].OriginalInstruction == null)
                return false;

            if (!WriteProcessMemory(_hProcess, _codeCaves[szCaveName].InstructionAddress, _codeCaves[szCaveName].OriginalInstruction, (ulong)_codeCaves[szCaveName].OriginalInstruction.Length, out IntPtr lpNumberOfBytesWritten) || lpNumberOfBytesWritten.ToInt32() != _codeCaves[szCaveName].OriginalInstruction.Length)
            {
                MainWindow.LogToFile("Could not disable code cave in MemoryCaveGenerator()!");
                return false;
            }
            _codeCaves[szCaveName].Active = false;
            return true;
        }

        /// <summary>
        /// Gets the data cave address of an already created cave by name.
        /// </summary>
        /// <param name="szCaveName">The unique name of the data cave.</param>
        /// <returns>The address of the code cave, -1 if none found.</returns>
        internal long GetDataCaveAddressByName(string szCaveName)
        {
            if (!_dataCaves.ContainsKey(szCaveName) || _dataCaves[szCaveName].CaveAddress == 0)
                return -1;
            return _dataCaves[szCaveName].CaveAddress;
        }

        /// <summary>
        /// Gets the data cave original pointer value by name.
        /// </summary>
        /// <param name="szCaveName">The unique name of the data cave.</param>
        /// <returns>The original pointer value of the data cave, null if none found.</returns>
        internal byte[] GetDataCaveOriginalPointerByName(string szCaveName)
        {
            if (!_dataCaves.ContainsKey(szCaveName) || _dataCaves[szCaveName].OriginalInstruction == null)
                return null;
            return _dataCaves[szCaveName].OriginalInstruction;
        }

        /// <summary>
        /// Gets the code cave address of an already created cave by name.
        /// </summary>
        /// <param name="szCaveName">The unique name of the code cave.</param>
        /// <returns>The address of the code cave, -1 if none found.</returns>
        internal long GetCodeCaveAddressByName(string szCaveName)
        {
            if (!_codeCaves.ContainsKey(szCaveName) || _codeCaves[szCaveName].CaveAddress == 0)
                return -1;
            return _codeCaves[szCaveName].CaveAddress;
        }

        /// <summary>
        /// Clears all saved caves internally. Does not deactivate nor remove them from memory.
        /// </summary>
        internal void ClearCaves()
        {
            _dataCaves.Clear();
            _codeCaves.Clear();
        }

        /// <summary>
        /// Creates a data cave to link a pointer to within given process's memory in reach of given instruction address.
        /// <para>Does not activate the data cave yet.</para>
        /// </summary>
        /// <param name="hProcess">The handle to the process, needs all access flag.</param>
        /// <param name="lpBaseAddress">The base address of the process.</param>
        /// <param name="lpPointerAddress">The address of the pointer that should later access the data in the cave.</param>
        /// <param name="dwPointerLength">The lengths of the pointer to later replace with data cave address.</param>
        /// <param name="cbDataInsert">The data to place inside the data cave.</param>
        /// <remarks>Assumes a relative 4 bytes pointer, will fail if there is no free memory within signed integer range (4bytes) from lpInstructionAddress.</remarks>
        /// <returns>The address of the beginning of the data cave, -1 if operation failed.</returns>
        private static long CreateDataCaveForPointer(IntPtr hProcess, long lpBaseAddress, long lpPointerAddress, int dwPointerLength, byte[] cbDataInsert)
        {
            if (IntPtr.Size != 8)
                throw new Exception("Only x64 is supported!");

            long lpAllocationAddress = AllocateMemoryNearAddress(hProcess, lpBaseAddress, lpPointerAddress, cbDataInsert.Length, false, dwPointerLength);
            if (lpAllocationAddress < 1)
                return -1;

            // fill data cave
            if (!WriteProcessMemory(hProcess, lpAllocationAddress, cbDataInsert, (ulong)cbDataInsert.Length, out IntPtr lpNumberOfBytesWritten) || lpNumberOfBytesWritten.ToInt32() != cbDataInsert.Length)
            {
                MainWindow.LogToFile("Failed to fill data cave in MemoryCaveGenerator()!");
                return -1;
            }

            return lpAllocationAddress;
        }

        /// <summary>
        /// Creates a code cave to inject code within given process's memory in reach of given instruction address.
        /// <para>Does not activate the code cave yet.</para>
        /// </summary>
        /// <param name="hProcess">The handle to the process, needs all access flag.</param>
        /// <param name="lpBaseAddress">The base address of the process.</param>
        /// <param name="lpInstructionAddress">The address of the instruction that should be overwritten with jump.</param>
        /// <param name="dwOverwriteLength">The length of the opcodes from lpInstructionAddress that should be overwritten with NOP, must be at least 5 bytes.</param>
        /// <param name="cbCodeInject">The shellcode to place inside the code cave.</param>
        /// <param name="bCopyOverwrittenInstructions">If overwritten instructions should be copied to the end of the code cave.</param>
        /// <remarks>Uses a relative 5 bytes jump instruction, will fail if there is no free memory within signed integer range (4bytes) from lpInstructionAddress.</remarks>
        /// <returns>The address of the beginning of the code cave, -1 if operation failed.</returns>
        private static long CreateCodeCaveForInstruction(IntPtr hProcess, long lpBaseAddress, long lpInstructionAddress, int dwOverwriteLength, byte[] cbCodeInject, bool bCopyOverwrittenInstructions = false)
        {
            if (IntPtr.Size != 8)
                throw new Exception("Only x64 is supported!");

            if (dwOverwriteLength < 5)
                throw new Exception("dwOverwriteLength must be at least 5 bytes!");

            // read instructions to replace with jump
            byte[] cbOriginalInstructionset = new byte[dwOverwriteLength];
            if (!ReadProcessMemory(hProcess, lpInstructionAddress, cbOriginalInstructionset, (ulong)dwOverwriteLength, out IntPtr lpNumberOfBytesRead) || lpNumberOfBytesRead.ToInt32() != dwOverwriteLength)
            {
                MainWindow.LogToFile("Failed to read original instruction set in MemoryCaveGenerator()!");
                return -1;
            }

            long lpAllocationAddress = AllocateMemoryNearAddress(hProcess, lpBaseAddress, lpInstructionAddress, cbCodeInject.Length + dwOverwriteLength, true, 6);
            if (lpAllocationAddress < 1)
                return -1;

            // calculate jump from cave to back to where we came from
            uint lpRelativePointerFromCave = (uint)((lpInstructionAddress + dwOverwriteLength) - (lpAllocationAddress + cbCodeInject.Length + (bCopyOverwrittenInstructions ? dwOverwriteLength : 0))) - 5;

            // generate jump instruction
            byte[] cbJumpInstruction = new byte[] { 0xE9 }; // JMP relative to RIP
            byte[] cbJumpFromCaveInstruction = cbJumpInstruction.Concat(BitConverter.GetBytes(lpRelativePointerFromCave)).ToArray();

            // generate instructions for cave
            byte[] cbCaveInstructions = cbCodeInject;
            if (bCopyOverwrittenInstructions)
                cbCaveInstructions = cbCaveInstructions.Concat(cbOriginalInstructionset).ToArray();
            cbCaveInstructions = cbCaveInstructions.Concat(cbJumpFromCaveInstruction).ToArray();

            // fill code cave with instructions
            if (!WriteProcessMemory(hProcess, lpAllocationAddress, cbCaveInstructions, (ulong)cbCaveInstructions.Length, out IntPtr lpNumberOfBytesWritten) || lpNumberOfBytesWritten.ToInt32() != cbCaveInstructions.Length)
            {
                MainWindow.LogToFile("Failed to fill code cave in MemoryCaveGenerator()!");
                return -1;
            }

            return lpAllocationAddress;
        }

        /// <summary>
        /// Creates a minimal code cave to inject code within given process's memory at given instruction address.
        /// Uses a relative 5 bytes jump instruction, will fail if there is no free memory within signed integer range (4bytes) from lpInstructionAddress.
        /// </summary>
        /// <param name="hProcess">The handle to the process, needs all access flag.</param>
        /// <param name="lpInstructionAddress">The address of the instruction that will be overwritten with jump.</param>
        /// <param name="dwOverwriteLength">The length of the opcodes from lpInstructionAddress that should be overwritten with NOP, must be at least 5 bytes.</param>
        /// <param name="lpCaveAddress">The address of the previously created code cave to where the jump should lead to.</param>
        /// <returns>The overwritten instructions.</returns>
        private static byte[] ActivateCodeCaveForInstruction(IntPtr hProcess, long lpInstructionAddress, int dwOverwriteLength, long lpCaveAddress)
        {
            // read instructions to replace with jump
            byte[] cbOriginalInstructionset = new byte[dwOverwriteLength];
            if (!ReadProcessMemory(hProcess, lpInstructionAddress, cbOriginalInstructionset, (ulong)dwOverwriteLength, out IntPtr lpNumberOfBytesRead) || lpNumberOfBytesRead.ToInt32() != dwOverwriteLength)
            {
                MainWindow.LogToFile("Failed to read original instruction set in MemoryCaveGenerator()!");
                return null;
            }

            // calculate relative jump offset
            uint lpRelativePointerToCave = (uint)(lpCaveAddress - lpInstructionAddress) - 5;

            // generate jump instruction and fill rest with NOPs
            byte[] cbJumpInstruction = new byte[] { 0xE9 }; // JMP relative to RIP
            byte[] cbJumpToCaveInstruction = cbJumpInstruction.Concat(BitConverter.GetBytes(lpRelativePointerToCave)).ToArray();
            if (dwOverwriteLength > 5)
            {
                byte[] cbOverwrite = new byte[dwOverwriteLength - 5];
                for (int i = 0; i < cbOverwrite.Length; i++)
                    cbOverwrite[i] = 0x90; // NOP
                cbJumpToCaveInstruction = cbJumpToCaveInstruction.Concat(cbOverwrite).ToArray();
            }

            // write jump instruction to target address
            if (!WriteProcessMemory(hProcess, lpInstructionAddress, cbJumpToCaveInstruction, (ulong)cbJumpToCaveInstruction.Length, out IntPtr lpNumberOfBytesWritten) || lpNumberOfBytesWritten.ToInt32() != cbJumpToCaveInstruction.Length)
            {
                MainWindow.LogToFile("Failed to overwrite target instructions in MemoryCaveGenerator()!");
                return null;
            }

            return cbOriginalInstructionset;
        }

        /// <summary>
        /// Allocates memory at least the size of given sizes in DWORD range of given instruction address.
        /// </summary>
        /// <param name="hProcess">The handle to the process, needs all access flag.</param>
        /// <param name="lpBaseAddress">The base address of the process.</param>
        /// <param name="lpInstructionAddress">The address of the instruction that should later access the allocated memory.</param>
        /// <param name="dwInsertLength">The length of the insert that should be placed in allocated memory.</param>
        /// <param name="bExecuteAccess">A boolean to determine if allocated memory should be executable, default is true.</param>
        /// <param name="dwAccessInstructionLength">The length of the instruction that will jump or read from the allocated memory. Default assumes a 5 bytes JMP.</param>
        /// <returns>The address of the beginning of the memory allocated, -1 if operation failed.</returns>
        private static long AllocateMemoryNearAddress(IntPtr hProcess, long lpBaseAddress, long lpInstructionAddress, int dwInsertLength, bool bExecuteAccess = true, int dwAccessInstructionLength = 6)
        {
            SYSTEM_INFO si = new SYSTEM_INFO();
            GetSystemInfo(out si);

            // find lowest and highest possible address in jump range from lpInstructionAddress
            int iMinimalCaveSize = 8 * (int)Math.Round((dwInsertLength + dwAccessInstructionLength) / 8.0); // nearest size rounded to 8 bytes
            if (iMinimalCaveSize < 32) iMinimalCaveSize = 32;
            long lpMinimalJmpAddress = lpInstructionAddress + dwAccessInstructionLength - 0x70000000; // Int32.MinValue + a little buffer overhead
            long lpMaximumJmpAddress = lpInstructionAddress + 0x70000000 - iMinimalCaveSize; // Int32.MaxValue + a little buffer overhead
            if (lpMinimalJmpAddress < si.lpMinimumApplicationAddress) lpMinimalJmpAddress = si.lpMinimumApplicationAddress;
            if (lpMaximumJmpAddress > si.lpMaximumApplicationAddress - iMinimalCaveSize) lpMaximumJmpAddress = si.lpMaximumApplicationAddress - iMinimalCaveSize;

            // determine lowest possible memory block we could allocate for cave assuming base address lies at the beginning of a memory block
            long lpPreferredAddress = lpBaseAddress - (int)((lpBaseAddress - lpMinimalJmpAddress) / si.dwAllocationGranularity) * si.dwAllocationGranularity;

            // find lowest useable memory block and allocate it
            long lpAllocationAddress = 0;
            MEMORY_BASIC_INFORMATION64 mbi = new MEMORY_BASIC_INFORMATION64();
            while (lpPreferredAddress < lpMaximumJmpAddress)
            {
                if (VirtualQueryEx(hProcess, new IntPtr(lpPreferredAddress), out mbi, MEMORY_BASIC_INFORMATION64_LENGTH) != MEMORY_BASIC_INFORMATION64_LENGTH)
                    break;

                if (mbi.State == MEM_FREE && mbi.RegionSize > (ulong)iMinimalCaveSize)
                {
                    lpAllocationAddress = VirtualAllocEx(hProcess, new IntPtr((long)mbi.BaseAddress), (uint)iMinimalCaveSize, ALLOCATIONTYPE_RESERVE | ALLOCATIONTYPE_COMMIT, bExecuteAccess ? MEMORYPROTECTION_EXECUTEREADWRITE : MEMORYPROTECTION_READWRITE);
                    if (lpAllocationAddress > 0)
                        break;
                }
                lpPreferredAddress = (long)mbi.BaseAddress + si.dwAllocationGranularity;
            }
            if (lpAllocationAddress == 0)
            {
                MainWindow.LogToFile("No usable memory region found or failed to allocate memory for code cave in MemoryCaveGenerator()!");
                return -1;
            }

            return lpAllocationAddress;
        }

        #region WINAPI

        private const uint ALLOCATIONTYPE_COMMIT = 0x1000;
        private const uint ALLOCATIONTYPE_RESERVE = 0x2000;
        private const uint MEMORYPROTECTION_READWRITE = 0x04;
        private const uint MEMORYPROTECTION_EXECUTEREADWRITE = 0x40;
        private const int MEMORY_BASIC_INFORMATION64_LENGTH = 48;
        private const int MEM_FREE = 0x10000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            UInt32 dwDesiredAccess,
            Boolean bInheritHandle,
            UInt32 dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Boolean CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_INFO
        {
            public UInt16 wProcessorArchitecture;
            public UInt16 wReserved;
            public UInt32 dwPageSize;
            public Int64 lpMinimumApplicationAddress;
            public Int64 lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public UInt32 dwNumberOfProcessors;
            public UInt32 dwProcessorType;
            public UInt32 dwAllocationGranularity;
            public UInt16 wProcessorLevel;
            public UInt16 wProcessorRevision;
        }

        [DllImport("kernel32.dll")]
        private static extern Int32 VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION64 lpBuffer, uint dwLength);

        [StructLayout(LayoutKind.Sequential)]
        internal struct MEMORY_BASIC_INFORMATION64
        {
            public UInt64 BaseAddress;
            public UInt64 AllocationBase;
            public Int32 AllocationProtect;
            public Int32 __alignment1;
            public UInt64 RegionSize;
            public Int32 State;
            public Int32 Protect;
            public Int32 Type;
            public Int32 __alignment2;
        }

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern Int64 VirtualAllocEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            uint dwSize,
            UInt32 flAllocationType,
            UInt32 flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Boolean ReadProcessMemory(
            IntPtr hProcess,
            Int64 lpBaseAddress,
            [Out] Byte[] lpBuffer,
            UInt64 dwSize,
            out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess,
            Int64 lpBaseAddress,
            [In, Out] Byte[] lpBuffer,
            UInt64 dwSize,
            out IntPtr lpNumberOfBytesWritten);

        #endregion
    }
}
