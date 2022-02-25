using System;

namespace EldenRingFPSUnlockAndMore
{
    internal class GameData
    {
        internal const string PROCESS_NAME = "eldenring";
        internal const string PROCESS_TITLE = "Elden Ring";
        internal const string PROCESS_EXE_VERSION = "1.2.0.0";
        internal static readonly string[] PROCESS_EXE_VERSION_SUPPORTED = new string[1]
        {
            "1.2.0.0"
        };

        /**
            <float>fFrameTick determines default frame rate limit in seconds.
            00007FF6AEA0EF5A | EB 4F                      | jmp eldenring.7FF6AEA0EFAB                                     |
            00007FF6AEA0EF5C | 8973 18                    | mov dword ptr ds:[rbx+18],esi                                  |
            00007FF6AEA0EF5F | C743 20 8988883C           | mov dword ptr ds:[rbx+20],3C888889                             | fFrameTick
            00007FF6AEA0EF66 | EB 43                      | jmp eldenring.7FF6AEA0EFAB                                     |
            00007FF6AEA0EF68 | 8973 18                    | mov dword ptr ds:[rbx+18],esi                                  |

            00007FF6AEA0EF5F (Version 1.2.0.0)
         */
        internal const string PATTERN_FRAMELOCK = "C7 ?? ?? ?? 88 88 3C EB"; // first byte can can be 88/90 instead of 89 due to precision loss on floating point numbers
        internal const int PATTERN_FRAMELOCK_OFFSET = 3; // offset to byte array from found position
        internal const string PATTERN_FRAMELOCK_FUZZY = "89 73 ?? C7 ?? ?? ?? ?? ?? ?? EB ?? 89 73";
        internal const int PATTERN_FRAMELOCK_OFFSET_FUZZY = 6;
    }
}
