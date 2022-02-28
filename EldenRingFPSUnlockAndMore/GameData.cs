using System;

namespace EldenRingFPSUnlockAndMore
{
    internal class GameData
    {
        internal const string PROCESS_TITLE = "Elden Ring";
        internal const string PROCESS_DESCRIPTION = "elden";
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


        /**
            HARDCODED limit to 60 Hz monitor refresh rate on every resolution change. FromSoft doesn't even bother with reading the user defined Hz from driver.
            This is not just lazy, but anti-consumer as they did aknowledge user-defined Hz override in Sekiro, but not anymore in ER. Fuck you FromSoft.
            00007FF7A30CAB25 | EB 0E                      | jmp eldenring.7FF7A30CAB35                                     |
            00007FF7A30CAB27 | C745 EF 3C000000           | mov dword ptr ss:[rbp-11],3C                                   | forces monitor to 60 (0x3C) Hz
            00007FF7A30CAB2E | C745 F3 01000000           | mov dword ptr ss:[rbp-D],1                                     | 1 indicates a hertz change
            00007FF7A30CAB35 | 8B87 940E0000              | mov eax,dword ptr ds:[rdi+E94]                                 |
            00007FF7A30CAB3B | 44:8BB3 54010000           | mov r14d,dword ptr ds:[rbx+154]                                |
            
            00007FF7A30CAB27 (Version 1.2.0.0)
         */
        internal const string PATTERN_HERTZLOCK = "EB ?? C7 ?? ?? 3C 00 00 00 C7 ?? ?? 01 00 00 00";
        internal const int PATTERN_HERTZLOCK_OFFSET = 2;
        internal const int PATTERN_HERTZLOCK_OFFSET_INTEGER1 = 3;
        internal const int PATTERN_HERTZLOCK_OFFSET_INTEGER2 = 10;
        internal const int PATCH_HERTZLOCK_INSTRUCTION_LENGTH = 14;


        /**
            DATA SECTION. All resolutions are listed in memory as <int>width1 <int>height1 <int>width2 <int>height2 ...
            Overwrite an unused one with desired new one. Some glitches, 1920x1080 and 1280x720 works best.
         */
        internal const string PATTERN_RESOLUTION_DEFAULT = "80 07 00 00 38 04 00 00 00 08 00 00 80 04 00 00"; // 1920x1080
        internal const string PATTERN_RESOLUTION_DEFAULT_720 = "00 05 00 00 D0 02 00 00 A0 05 00 00 2A 03 00 00"; // 1280x720
        internal static readonly byte[] PATCH_RESOLUTION_DEFAULT_DISABLE = new byte[] { 0x80, 0x07, 0x00, 0x00, 0x38, 0x04, 0x00, 0x00 };
        internal static readonly byte[] PATCH_RESOLUTION_DEFAULT_DISABLE_720 = new byte[] { 0x00, 0x05, 0x00, 0x00, 0xD0, 0x02, 0x00, 0x00 };


        /**
            Conditional jump instruction that determines if 16/9 scaling for game is enforced or not, overwrite with non conditional JMP so widescreen won't get clinched.
            00007FF7A30C8D73 | 8B01                       | mov eax,dword ptr ds:[rcx]                                     |
            00007FF7A30C8D75 | 85C0                       | test eax,eax                                                   |
            00007FF7A30C8D77 | 74 42                      | je eldenring.7FF7A30C8DBB                                      | calculation for screen scaling
            00007FF7A30C8D79 | 44:8B59 04                 | mov r11d,dword ptr ds:[rcx+4]                                  | resolution scaling calculation...
            00007FF7A30C8D7D | 45:85DB                    | test r11d,r11d                                                 |
            00007FF7A30C8D80 | 74 39                      | je eldenring.7FF7A30C8DBB                                      |
            00007FF7A30C8D82 | 41:8BD3                    | mov edx,r11d                                                   |
         */
        internal const string PATTERN_RESOLUTION_SCALING_FIX = "8B ?? 85 ?? 74 ?? 44 8B ?? ?? 45 85 ?? 74 ?? 41 8B";
        internal const int PATTERN_RESOLUTION_SCALING_FIX_OFFSET = 4;
        internal static readonly byte[] PATCH_RESOLUTION_SCALING_FIX_ENABLE = new byte[] { 0xEB };  // jmp
        internal static readonly byte[] PATCH_RESOLUTION_SCALING_FIX_DISABLE = new byte[] { 0x74 }; // je


        /** Game has a function to adjust FOV by a multiplier but the multiplier never changes during ordinary gameplay.
            00007FF709FD0DF0 | 44:0F28C8                  | movaps xmm9,xmm0                                               |
            00007FF709FD0DF4 | E8 C7AC9100                | call eldenring.7FF70A8EBAC0                                    |
            00007FF709FD0DF9 | 80BB 88040000 00           | cmp byte ptr ds:[rbx+488],0                                    | -> code cave jump inject here
            00007FF709FD0E00 | 44:0F28E0                  | movaps xmm12,xmm0                                              | save FOV multiplier from xmm0 to xmm12 <- jump back here
            00007FF709FD0E04 | F344:0F1005 7BE2E102       | movss xmm8,dword ptr ds:[7FF70CDEF088]                         |
            00007FF709FD0E0D | 45:0F57D2                  | xorps xmm10,xmm10                                              | 
            00007FF709FD0E11 | F345:0F59E7                | mulss xmm12,xmm15                                              |

            00007FF709FD0E00 (Version 1.2.0.0)
         */
        internal const string PATTERN_FOV_MULTIPLIER = "80 BB ?? ?? ?? ?? 00 44 ?? ?? E0 F3 44 ?? ?? ?? ?? ?? ?? ?? 45";
        internal const int PATTERN_FOV_MULTIPLIER_OFFSET = 0;
        internal const int INJECT_FOV_MULTIPLIER_OVERWRITE_LENGTH = 7;
        internal static readonly byte[] INJECT_FOV_MULTIPLIER_SHELLCODE = new byte[]
        {
            0xF3, 0x0F, 0x59, 0x05, 0x00, 0x00, 0x00, 0x00  // mulss xmm0,dword ptr ds:[XXXXXXXXXXXX]
        };
        internal const int INJECT_FOV_MULTIPLIER_SHELLCODE_OFFSET = 4;


        /**
            Here Runes get reduced in case of death, so we nop the two instructions.
            00007FF70A1FC554 | 44:896C24 2C               | mov dword ptr ss:[rsp+2C],r13d                                 |
            00007FF70A1FC559 | 8B00                       | mov eax,dword ptr ds:[rax]                                     | prepare reduction amount
            00007FF70A1FC55B | 8945 6C                    | mov dword ptr ss:[rbp+6C],eax                                  | reduces player runes
            00007FF70A1FC55E | 48:8B0D 53646703           | mov rcx,qword ptr ds:[7FF70D8729B8]                            |
            00007FF70A1FC565 | 48:85C9                    | test rcx,rcx                                                   |

            00007FF70A1FC559 (Version 1.2.0.0)
         */
        internal const string PATTERN_DEATHPENALTY = "44 ?? ?? ?? ?? 8B 00 89 45 ?? 48 8B 0D";
        internal const int PATTERN_DEATHPENALTY_OFFSET = 5;
        internal const int PATCH_DEATHPENALTY_INSTRUCTION_LENGTH = 5;
        internal static readonly byte[] PATCH_DEATHPENALTY_ENABLE = new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 }; // nop


        /**
            Reference pointer pTimeRelated to TimescaleManager pointer, offset in struct to <float>fTimescale which acts as a global speed scale for almost all ingame calculations.
            00007FF70A98B95A | E8 B1190A01                | call eldenring.7FF70BA2D310                                    |
            00007FF70A98B95F | 48:8B05 F2737003           | mov rax,qword ptr ds:[7FF70E092D58]                            | pTimeRelated->[TimescaleManager+0x2D4]->fTimescale
            00007FF70A98B966 | F3:0F1088 D4020000         | movss xmm1,dword ptr ds:[rax+2D4]                              | offset TimescaleManager->fTimescale
            00007FF70A98B96E | F3:0F5988 70020000         | mulss xmm1,dword ptr ds:[rax+270]                              |
            00007FF70A98B976 | 48:8D1D DBE3B901           | lea rbx,qword ptr ds:[7FF70C529D58]                            |

            00007FF70A98B95F (Version 1.2.0.0)
         */
        internal const string PATTERN_TIMESCALE = "48 8B 05 ?? ?? ?? ?? F3 0F 10 88 ?? ?? ?? ?? F3 0F";
        internal const int PATTERN_TIMESCALE_OFFSET = 3;
        internal const int PATTERN_TIMESCALE_POINTER_OFFSET = 8;
    }
}
