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


        /** Reference pointer to FOV table. The fovtable default constant gets used a lot so we need to overwrite the entry in the table itself.
            FOV is in radians while default is 1.0deg (0.0174533rad), to increase by 25% you'd write 1.25deg (0.0218166rad) as fFov.
            00007FF6BA8F5208 | E9 858B5600                | jmp eldenring.7FF6BAE5DD92                                     |
            00007FF6BA8F520D | 0F28F8                     | movaps xmm7,xmm0                                               |
            00007FF6BA8F5210 | F3:0F593D 249E89FE         | mulss xmm7,dword ptr ds:[7FF6B918F03C]                         | ->FOVtable
            00007FF6BA8F5218 | 0F2863 10                  | movaps xmm4,xmmword ptr ds:[rbx+10]                            |
            00007FF6BA8F521C | 0F5C23                     | subps xmm4,xmmword ptr ds:[rbx]                                |
            00007FF6BA8F521F | 0F28D4                     | movaps xmm2,xmm4                                               |

            00007FF6BA8F5210 (Version 1.2.0.0)
         */
        internal const string PATTERN_FOVTABLEPTR = "E9 ?? ?? ?? ?? 0F ?? ?? F3 ?? ?? ?? ?? ?? ?? ?? 0F ?? ?? ?? 0F ?? ?? 0F";
        internal const int PATTERN_FOVTABLEPTR_OFFSET = 12;
        internal const float PATTERN_FOVTABLEPTR_DISABLE = 0.0174533f; // Rad2Deg -> 1°
    }
}
