// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
using System.Diagnostics;
using Blazor.SourceGenerators.TypeScript.Types;
using static Blazor.SourceGenerators.TypeScript.Compiler.Core;

namespace Blazor.SourceGenerators.TypeScript.Compiler;

internal delegate void ErrorCallback(DiagnosticMessage message, int? length = null);

internal sealed class Scanner
{
    private readonly bool _skipTrivia;

    private string _text = default!;
    private int _end;
    private ScriptTarget _languageVersion;
    private LanguageVariant _languageVariant;

    private static readonly Dictionary<string, TypeScriptSyntaxKind> s_textToTokenMap = new()
    {
        ["abstract"] = TypeScriptSyntaxKind.AbstractKeyword,
        ["any"] = TypeScriptSyntaxKind.AnyKeyword,
        ["as"] = TypeScriptSyntaxKind.AsKeyword,
        ["boolean"] = TypeScriptSyntaxKind.BooleanKeyword,
        ["break"] = TypeScriptSyntaxKind.BreakKeyword,
        ["case"] = TypeScriptSyntaxKind.CaseKeyword,
        ["catch"] = TypeScriptSyntaxKind.CatchKeyword,
        ["class"] = TypeScriptSyntaxKind.ClassKeyword,
        ["continue"] = TypeScriptSyntaxKind.ContinueKeyword,
        ["const"] = TypeScriptSyntaxKind.ConstKeyword,
        ["constructor"] = TypeScriptSyntaxKind.ConstructorKeyword,
        ["debugger"] = TypeScriptSyntaxKind.DebuggerKeyword,
        ["declare"] = TypeScriptSyntaxKind.DeclareKeyword,
        ["default"] = TypeScriptSyntaxKind.DefaultKeyword,
        ["delete"] = TypeScriptSyntaxKind.DeleteKeyword,
        ["do"] = TypeScriptSyntaxKind.DoKeyword,
        ["else"] = TypeScriptSyntaxKind.ElseKeyword,
        ["enum"] = TypeScriptSyntaxKind.EnumKeyword,
        ["export"] = TypeScriptSyntaxKind.ExportKeyword,
        ["extends"] = TypeScriptSyntaxKind.ExtendsKeyword,
        ["false"] = TypeScriptSyntaxKind.FalseKeyword,
        ["finally"] = TypeScriptSyntaxKind.FinallyKeyword,
        ["for"] = TypeScriptSyntaxKind.ForKeyword,
        ["from"] = TypeScriptSyntaxKind.FromKeyword,
        ["function"] = TypeScriptSyntaxKind.FunctionKeyword,
        ["get"] = TypeScriptSyntaxKind.GetKeyword,
        ["if"] = TypeScriptSyntaxKind.IfKeyword,
        ["implements"] = TypeScriptSyntaxKind.ImplementsKeyword,
        ["import"] = TypeScriptSyntaxKind.ImportKeyword,
        ["in"] = TypeScriptSyntaxKind.InKeyword,
        ["instanceof"] = TypeScriptSyntaxKind.InstanceOfKeyword,
        ["interface"] = TypeScriptSyntaxKind.InterfaceKeyword,
        ["is"] = TypeScriptSyntaxKind.IsKeyword,
        ["keyof"] = TypeScriptSyntaxKind.KeyOfKeyword,
        ["let"] = TypeScriptSyntaxKind.LetKeyword,
        ["module"] = TypeScriptSyntaxKind.ModuleKeyword,
        ["namespace"] = TypeScriptSyntaxKind.NamespaceKeyword,
        ["never"] = TypeScriptSyntaxKind.NeverKeyword,
        ["new"] = TypeScriptSyntaxKind.NewKeyword,
        ["null"] = TypeScriptSyntaxKind.NullKeyword,
        ["number"] = TypeScriptSyntaxKind.NumberKeyword,
        ["object"] = TypeScriptSyntaxKind.ObjectKeyword,
        ["package"] = TypeScriptSyntaxKind.PackageKeyword,
        ["private"] = TypeScriptSyntaxKind.PrivateKeyword,
        ["protected"] = TypeScriptSyntaxKind.ProtectedKeyword,
        ["public"] = TypeScriptSyntaxKind.PublicKeyword,
        ["readonly"] = TypeScriptSyntaxKind.ReadonlyKeyword,
        ["require"] = TypeScriptSyntaxKind.RequireKeyword,
        ["global"] = TypeScriptSyntaxKind.GlobalKeyword,
        ["return"] = TypeScriptSyntaxKind.ReturnKeyword,
        ["set"] = TypeScriptSyntaxKind.SetKeyword,
        ["static"] = TypeScriptSyntaxKind.StaticKeyword,
        ["string"] = TypeScriptSyntaxKind.StringKeyword,
        ["super"] = TypeScriptSyntaxKind.SuperKeyword,
        ["switch"] = TypeScriptSyntaxKind.SwitchKeyword,
        ["symbol"] = TypeScriptSyntaxKind.SymbolKeyword,
        ["this"] = TypeScriptSyntaxKind.ThisKeyword,
        ["throw"] = TypeScriptSyntaxKind.ThrowKeyword,
        ["true"] = TypeScriptSyntaxKind.TrueKeyword,
        ["try"] = TypeScriptSyntaxKind.TryKeyword,
        ["type"] = TypeScriptSyntaxKind.TypeKeyword,
        ["typeof"] = TypeScriptSyntaxKind.TypeOfKeyword,
        ["undefined"] = TypeScriptSyntaxKind.UndefinedKeyword,
        ["var"] = TypeScriptSyntaxKind.VarKeyword,
        ["void"] = TypeScriptSyntaxKind.VoidKeyword,
        ["while"] = TypeScriptSyntaxKind.WhileKeyword,
        ["with"] = TypeScriptSyntaxKind.WithKeyword,
        ["yield"] = TypeScriptSyntaxKind.YieldKeyword,
        ["async"] = TypeScriptSyntaxKind.AsyncKeyword,
        ["await"] = TypeScriptSyntaxKind.AwaitKeyword,
        ["of"] = TypeScriptSyntaxKind.OfKeyword,
        ["{"] = TypeScriptSyntaxKind.OpenBraceToken,
        ["}"] = TypeScriptSyntaxKind.CloseBraceToken,
        ["("] = TypeScriptSyntaxKind.OpenParenToken,
        [")"] = TypeScriptSyntaxKind.CloseParenToken,
        ["["] = TypeScriptSyntaxKind.OpenBracketToken,
        ["]"] = TypeScriptSyntaxKind.CloseBracketToken,
        ["."] = TypeScriptSyntaxKind.DotToken,
        ["..."] = TypeScriptSyntaxKind.DotDotDotToken,
        [";"] = TypeScriptSyntaxKind.SemicolonToken,
        [","] = TypeScriptSyntaxKind.CommaToken,
        ["<"] = TypeScriptSyntaxKind.LessThanToken,
        [">"] = TypeScriptSyntaxKind.GreaterThanToken,
        ["<="] = TypeScriptSyntaxKind.LessThanEqualsToken,
        [">="] = TypeScriptSyntaxKind.GreaterThanEqualsToken,
        ["=="] = TypeScriptSyntaxKind.EqualsEqualsToken,
        ["!="] = TypeScriptSyntaxKind.ExclamationEqualsToken,
        ["==="] = TypeScriptSyntaxKind.EqualsEqualsEqualsToken,
        ["!=="] = TypeScriptSyntaxKind.ExclamationEqualsEqualsToken,
        ["=>"] = TypeScriptSyntaxKind.EqualsGreaterThanToken,
        ["+"] = TypeScriptSyntaxKind.PlusToken,
        ["-"] = TypeScriptSyntaxKind.MinusToken,
        ["**"] = TypeScriptSyntaxKind.AsteriskAsteriskToken,
        ["*"] = TypeScriptSyntaxKind.AsteriskToken,
        ["/"] = TypeScriptSyntaxKind.SlashToken,
        ["%"] = TypeScriptSyntaxKind.PercentToken,
        ["++"] = TypeScriptSyntaxKind.PlusPlusToken,
        ["--"] = TypeScriptSyntaxKind.MinusMinusToken,
        ["<<"] = TypeScriptSyntaxKind.LessThanLessThanToken,
        ["</"] = TypeScriptSyntaxKind.LessThanSlashToken,
        [">>"] = TypeScriptSyntaxKind.GreaterThanGreaterThanToken,
        [">>>"] = TypeScriptSyntaxKind.GreaterThanGreaterThanGreaterThanToken,
        ["&"] = TypeScriptSyntaxKind.AmpersandToken,
        ["|"] = TypeScriptSyntaxKind.BarToken,
        ["^"] = TypeScriptSyntaxKind.CaretToken,
        ["!"] = TypeScriptSyntaxKind.ExclamationToken,
        ["~"] = TypeScriptSyntaxKind.TildeToken,
        ["&&"] = TypeScriptSyntaxKind.AmpersandAmpersandToken,
        ["||"] = TypeScriptSyntaxKind.BarBarToken,
        [""] = TypeScriptSyntaxKind.QuestionToken,
        [":"] = TypeScriptSyntaxKind.ColonToken,
        ["="] = TypeScriptSyntaxKind.EqualsToken,
        ["+="] = TypeScriptSyntaxKind.PlusEqualsToken,
        ["-="] = TypeScriptSyntaxKind.MinusEqualsToken,
        ["*="] = TypeScriptSyntaxKind.AsteriskEqualsToken,
        ["**="] = TypeScriptSyntaxKind.AsteriskAsteriskEqualsToken,
        ["/="] = TypeScriptSyntaxKind.SlashEqualsToken,
        ["%="] = TypeScriptSyntaxKind.PercentEqualsToken,
        ["<<="] = TypeScriptSyntaxKind.LessThanLessThanEqualsToken,
        [">>="] = TypeScriptSyntaxKind.GreaterThanGreaterThanEqualsToken,
        [">>>="] = TypeScriptSyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken,
        ["&="] = TypeScriptSyntaxKind.AmpersandEqualsToken,
        ["|="] = TypeScriptSyntaxKind.BarEqualsToken,
        ["^="] = TypeScriptSyntaxKind.CaretEqualsToken,
        ["@"] = TypeScriptSyntaxKind.AtToken,
    };

    private static readonly int s_mergeConflictMarkerLength = "<<<<<<<".Length;
    private static readonly Regex s_shebangTriviaRegex = new("/^#!.*/");

    private readonly int[] _unicodeEs3IdentifierStart = { 170, 170, 181, 181, 186, 186, 192, 214, 216, 246, 248, 543, 546, 563, 592, 685, 688, 696, 699, 705, 720, 721, 736, 740, 750, 750, 890, 890, 902, 902, 904, 906, 908, 908, 910, 929, 931, 974, 976, 983, 986, 1011, 1024, 1153, 1164, 1220, 1223, 1224, 1227, 1228, 1232, 1269, 1272, 1273, 1329, 1366, 1369, 1369, 1377, 1415, 1488, 1514, 1520, 1522, 1569, 1594, 1600, 1610, 1649, 1747, 1749, 1749, 1765, 1766, 1786, 1788, 1808, 1808, 1810, 1836, 1920, 1957, 2309, 2361, 2365, 2365, 2384, 2384, 2392, 2401, 2437, 2444, 2447, 2448, 2451, 2472, 2474, 2480, 2482, 2482, 2486, 2489, 2524, 2525, 2527, 2529, 2544, 2545, 2565, 2570, 2575, 2576, 2579, 2600, 2602, 2608, 2610, 2611, 2613, 2614, 2616, 2617, 2649, 2652, 2654, 2654, 2674, 2676, 2693, 2699, 2701, 2701, 2703, 2705, 2707, 2728, 2730, 2736, 2738, 2739, 2741, 2745, 2749, 2749, 2768, 2768, 2784, 2784, 2821, 2828, 2831, 2832, 2835, 2856, 2858, 2864, 2866, 2867, 2870, 2873, 2877, 2877, 2908, 2909, 2911, 2913, 2949, 2954, 2958, 2960, 2962, 2965, 2969, 2970, 2972, 2972, 2974, 2975, 2979, 2980, 2984, 2986, 2990, 2997, 2999, 3001, 3077, 3084, 3086, 3088, 3090, 3112, 3114, 3123, 3125, 3129, 3168, 3169, 3205, 3212, 3214, 3216, 3218, 3240, 3242, 3251, 3253, 3257, 3294, 3294, 3296, 3297, 3333, 3340, 3342, 3344, 3346, 3368, 3370, 3385, 3424, 3425, 3461, 3478, 3482, 3505, 3507, 3515, 3517, 3517, 3520, 3526, 3585, 3632, 3634, 3635, 3648, 3654, 3713, 3714, 3716, 3716, 3719, 3720, 3722, 3722, 3725, 3725, 3732, 3735, 3737, 3743, 3745, 3747, 3749, 3749, 3751, 3751, 3754, 3755, 3757, 3760, 3762, 3763, 3773, 3773, 3776, 3780, 3782, 3782, 3804, 3805, 3840, 3840, 3904, 3911, 3913, 3946, 3976, 3979, 4096, 4129, 4131, 4135, 4137, 4138, 4176, 4181, 4256, 4293, 4304, 4342, 4352, 4441, 4447, 4514, 4520, 4601, 4608, 4614, 4616, 4678, 4680, 4680, 4682, 4685, 4688, 4694, 4696, 4696, 4698, 4701, 4704, 4742, 4744, 4744, 4746, 4749, 4752, 4782, 4784, 4784, 4786, 4789, 4792, 4798, 4800, 4800, 4802, 4805, 4808, 4814, 4816, 4822, 4824, 4846, 4848, 4878, 4880, 4880, 4882, 4885, 4888, 4894, 4896, 4934, 4936, 4954, 5024, 5108, 5121, 5740, 5743, 5750, 5761, 5786, 5792, 5866, 6016, 6067, 6176, 6263, 6272, 6312, 7680, 7835, 7840, 7929, 7936, 7957, 7960, 7965, 7968, 8005, 8008, 8013, 8016, 8023, 8025, 8025, 8027, 8027, 8029, 8029, 8031, 8061, 8064, 8116, 8118, 8124, 8126, 8126, 8130, 8132, 8134, 8140, 8144, 8147, 8150, 8155, 8160, 8172, 8178, 8180, 8182, 8188, 8319, 8319, 8450, 8450, 8455, 8455, 8458, 8467, 8469, 8469, 8473, 8477, 8484, 8484, 8486, 8486, 8488, 8488, 8490, 8493, 8495, 8497, 8499, 8505, 8544, 8579, 12293, 12295, 12321, 12329, 12337, 12341, 12344, 12346, 12353, 12436, 12445, 12446, 12449, 12538, 12540, 12542, 12549, 12588, 12593, 12686, 12704, 12727, 13312, 19893, 19968, 40869, 40960, 42124, 44032, 55203, 63744, 64045, 64256, 64262, 64275, 64279, 64285, 64285, 64287, 64296, 64298, 64310, 64312, 64316, 64318, 64318, 64320, 64321, 64323, 64324, 64326, 64433, 64467, 64829, 64848, 64911, 64914, 64967, 65008, 65019, 65136, 65138, 65140, 65140, 65142, 65276, 65313, 65338, 65345, 65370, 65382, 65470, 65474, 65479, 65482, 65487, 65490, 65495, 65498, 65500, };
    private readonly int[] _unicodeEs3IdentifierPart = { 170, 170, 181, 181, 186, 186, 192, 214, 216, 246, 248, 543, 546, 563, 592, 685, 688, 696, 699, 705, 720, 721, 736, 740, 750, 750, 768, 846, 864, 866, 890, 890, 902, 902, 904, 906, 908, 908, 910, 929, 931, 974, 976, 983, 986, 1011, 1024, 1153, 1155, 1158, 1164, 1220, 1223, 1224, 1227, 1228, 1232, 1269, 1272, 1273, 1329, 1366, 1369, 1369, 1377, 1415, 1425, 1441, 1443, 1465, 1467, 1469, 1471, 1471, 1473, 1474, 1476, 1476, 1488, 1514, 1520, 1522, 1569, 1594, 1600, 1621, 1632, 1641, 1648, 1747, 1749, 1756, 1759, 1768, 1770, 1773, 1776, 1788, 1808, 1836, 1840, 1866, 1920, 1968, 2305, 2307, 2309, 2361, 2364, 2381, 2384, 2388, 2392, 2403, 2406, 2415, 2433, 2435, 2437, 2444, 2447, 2448, 2451, 2472, 2474, 2480, 2482, 2482, 2486, 2489, 2492, 2492, 2494, 2500, 2503, 2504, 2507, 2509, 2519, 2519, 2524, 2525, 2527, 2531, 2534, 2545, 2562, 2562, 2565, 2570, 2575, 2576, 2579, 2600, 2602, 2608, 2610, 2611, 2613, 2614, 2616, 2617, 2620, 2620, 2622, 2626, 2631, 2632, 2635, 2637, 2649, 2652, 2654, 2654, 2662, 2676, 2689, 2691, 2693, 2699, 2701, 2701, 2703, 2705, 2707, 2728, 2730, 2736, 2738, 2739, 2741, 2745, 2748, 2757, 2759, 2761, 2763, 2765, 2768, 2768, 2784, 2784, 2790, 2799, 2817, 2819, 2821, 2828, 2831, 2832, 2835, 2856, 2858, 2864, 2866, 2867, 2870, 2873, 2876, 2883, 2887, 2888, 2891, 2893, 2902, 2903, 2908, 2909, 2911, 2913, 2918, 2927, 2946, 2947, 2949, 2954, 2958, 2960, 2962, 2965, 2969, 2970, 2972, 2972, 2974, 2975, 2979, 2980, 2984, 2986, 2990, 2997, 2999, 3001, 3006, 3010, 3014, 3016, 3018, 3021, 3031, 3031, 3047, 3055, 3073, 3075, 3077, 3084, 3086, 3088, 3090, 3112, 3114, 3123, 3125, 3129, 3134, 3140, 3142, 3144, 3146, 3149, 3157, 3158, 3168, 3169, 3174, 3183, 3202, 3203, 3205, 3212, 3214, 3216, 3218, 3240, 3242, 3251, 3253, 3257, 3262, 3268, 3270, 3272, 3274, 3277, 3285, 3286, 3294, 3294, 3296, 3297, 3302, 3311, 3330, 3331, 3333, 3340, 3342, 3344, 3346, 3368, 3370, 3385, 3390, 3395, 3398, 3400, 3402, 3405, 3415, 3415, 3424, 3425, 3430, 3439, 3458, 3459, 3461, 3478, 3482, 3505, 3507, 3515, 3517, 3517, 3520, 3526, 3530, 3530, 3535, 3540, 3542, 3542, 3544, 3551, 3570, 3571, 3585, 3642, 3648, 3662, 3664, 3673, 3713, 3714, 3716, 3716, 3719, 3720, 3722, 3722, 3725, 3725, 3732, 3735, 3737, 3743, 3745, 3747, 3749, 3749, 3751, 3751, 3754, 3755, 3757, 3769, 3771, 3773, 3776, 3780, 3782, 3782, 3784, 3789, 3792, 3801, 3804, 3805, 3840, 3840, 3864, 3865, 3872, 3881, 3893, 3893, 3895, 3895, 3897, 3897, 3902, 3911, 3913, 3946, 3953, 3972, 3974, 3979, 3984, 3991, 3993, 4028, 4038, 4038, 4096, 4129, 4131, 4135, 4137, 4138, 4140, 4146, 4150, 4153, 4160, 4169, 4176, 4185, 4256, 4293, 4304, 4342, 4352, 4441, 4447, 4514, 4520, 4601, 4608, 4614, 4616, 4678, 4680, 4680, 4682, 4685, 4688, 4694, 4696, 4696, 4698, 4701, 4704, 4742, 4744, 4744, 4746, 4749, 4752, 4782, 4784, 4784, 4786, 4789, 4792, 4798, 4800, 4800, 4802, 4805, 4808, 4814, 4816, 4822, 4824, 4846, 4848, 4878, 4880, 4880, 4882, 4885, 4888, 4894, 4896, 4934, 4936, 4954, 4969, 4977, 5024, 5108, 5121, 5740, 5743, 5750, 5761, 5786, 5792, 5866, 6016, 6099, 6112, 6121, 6160, 6169, 6176, 6263, 6272, 6313, 7680, 7835, 7840, 7929, 7936, 7957, 7960, 7965, 7968, 8005, 8008, 8013, 8016, 8023, 8025, 8025, 8027, 8027, 8029, 8029, 8031, 8061, 8064, 8116, 8118, 8124, 8126, 8126, 8130, 8132, 8134, 8140, 8144, 8147, 8150, 8155, 8160, 8172, 8178, 8180, 8182, 8188, 8255, 8256, 8319, 8319, 8400, 8412, 8417, 8417, 8450, 8450, 8455, 8455, 8458, 8467, 8469, 8469, 8473, 8477, 8484, 8484, 8486, 8486, 8488, 8488, 8490, 8493, 8495, 8497, 8499, 8505, 8544, 8579, 12293, 12295, 12321, 12335, 12337, 12341, 12344, 12346, 12353, 12436, 12441, 12442, 12445, 12446, 12449, 12542, 12549, 12588, 12593, 12686, 12704, 12727, 13312, 19893, 19968, 40869, 40960, 42124, 44032, 55203, 63744, 64045, 64256, 64262, 64275, 64279, 64285, 64296, 64298, 64310, 64312, 64316, 64318, 64318, 64320, 64321, 64323, 64324, 64326, 64433, 64467, 64829, 64848, 64911, 64914, 64967, 65008, 65019, 65056, 65059, 65075, 65076, 65101, 65103, 65136, 65138, 65140, 65140, 65142, 65276, 65296, 65305, 65313, 65338, 65343, 65343, 65345, 65370, 65381, 65470, 65474, 65479, 65482, 65487, 65490, 65495, 65498, 65500, };
    private readonly int[] _unicodeEs5IdentifierStart = { 170, 170, 181, 181, 186, 186, 192, 214, 216, 246, 248, 705, 710, 721, 736, 740, 748, 748, 750, 750, 880, 884, 886, 887, 890, 893, 902, 902, 904, 906, 908, 908, 910, 929, 931, 1013, 1015, 1153, 1162, 1319, 1329, 1366, 1369, 1369, 1377, 1415, 1488, 1514, 1520, 1522, 1568, 1610, 1646, 1647, 1649, 1747, 1749, 1749, 1765, 1766, 1774, 1775, 1786, 1788, 1791, 1791, 1808, 1808, 1810, 1839, 1869, 1957, 1969, 1969, 1994, 2026, 2036, 2037, 2042, 2042, 2048, 2069, 2074, 2074, 2084, 2084, 2088, 2088, 2112, 2136, 2208, 2208, 2210, 2220, 2308, 2361, 2365, 2365, 2384, 2384, 2392, 2401, 2417, 2423, 2425, 2431, 2437, 2444, 2447, 2448, 2451, 2472, 2474, 2480, 2482, 2482, 2486, 2489, 2493, 2493, 2510, 2510, 2524, 2525, 2527, 2529, 2544, 2545, 2565, 2570, 2575, 2576, 2579, 2600, 2602, 2608, 2610, 2611, 2613, 2614, 2616, 2617, 2649, 2652, 2654, 2654, 2674, 2676, 2693, 2701, 2703, 2705, 2707, 2728, 2730, 2736, 2738, 2739, 2741, 2745, 2749, 2749, 2768, 2768, 2784, 2785, 2821, 2828, 2831, 2832, 2835, 2856, 2858, 2864, 2866, 2867, 2869, 2873, 2877, 2877, 2908, 2909, 2911, 2913, 2929, 2929, 2947, 2947, 2949, 2954, 2958, 2960, 2962, 2965, 2969, 2970, 2972, 2972, 2974, 2975, 2979, 2980, 2984, 2986, 2990, 3001, 3024, 3024, 3077, 3084, 3086, 3088, 3090, 3112, 3114, 3123, 3125, 3129, 3133, 3133, 3160, 3161, 3168, 3169, 3205, 3212, 3214, 3216, 3218, 3240, 3242, 3251, 3253, 3257, 3261, 3261, 3294, 3294, 3296, 3297, 3313, 3314, 3333, 3340, 3342, 3344, 3346, 3386, 3389, 3389, 3406, 3406, 3424, 3425, 3450, 3455, 3461, 3478, 3482, 3505, 3507, 3515, 3517, 3517, 3520, 3526, 3585, 3632, 3634, 3635, 3648, 3654, 3713, 3714, 3716, 3716, 3719, 3720, 3722, 3722, 3725, 3725, 3732, 3735, 3737, 3743, 3745, 3747, 3749, 3749, 3751, 3751, 3754, 3755, 3757, 3760, 3762, 3763, 3773, 3773, 3776, 3780, 3782, 3782, 3804, 3807, 3840, 3840, 3904, 3911, 3913, 3948, 3976, 3980, 4096, 4138, 4159, 4159, 4176, 4181, 4186, 4189, 4193, 4193, 4197, 4198, 4206, 4208, 4213, 4225, 4238, 4238, 4256, 4293, 4295, 4295, 4301, 4301, 4304, 4346, 4348, 4680, 4682, 4685, 4688, 4694, 4696, 4696, 4698, 4701, 4704, 4744, 4746, 4749, 4752, 4784, 4786, 4789, 4792, 4798, 4800, 4800, 4802, 4805, 4808, 4822, 4824, 4880, 4882, 4885, 4888, 4954, 4992, 5007, 5024, 5108, 5121, 5740, 5743, 5759, 5761, 5786, 5792, 5866, 5870, 5872, 5888, 5900, 5902, 5905, 5920, 5937, 5952, 5969, 5984, 5996, 5998, 6000, 6016, 6067, 6103, 6103, 6108, 6108, 6176, 6263, 6272, 6312, 6314, 6314, 6320, 6389, 6400, 6428, 6480, 6509, 6512, 6516, 6528, 6571, 6593, 6599, 6656, 6678, 6688, 6740, 6823, 6823, 6917, 6963, 6981, 6987, 7043, 7072, 7086, 7087, 7098, 7141, 7168, 7203, 7245, 7247, 7258, 7293, 7401, 7404, 7406, 7409, 7413, 7414, 7424, 7615, 7680, 7957, 7960, 7965, 7968, 8005, 8008, 8013, 8016, 8023, 8025, 8025, 8027, 8027, 8029, 8029, 8031, 8061, 8064, 8116, 8118, 8124, 8126, 8126, 8130, 8132, 8134, 8140, 8144, 8147, 8150, 8155, 8160, 8172, 8178, 8180, 8182, 8188, 8305, 8305, 8319, 8319, 8336, 8348, 8450, 8450, 8455, 8455, 8458, 8467, 8469, 8469, 8473, 8477, 8484, 8484, 8486, 8486, 8488, 8488, 8490, 8493, 8495, 8505, 8508, 8511, 8517, 8521, 8526, 8526, 8544, 8584, 11264, 11310, 11312, 11358, 11360, 11492, 11499, 11502, 11506, 11507, 11520, 11557, 11559, 11559, 11565, 11565, 11568, 11623, 11631, 11631, 11648, 11670, 11680, 11686, 11688, 11694, 11696, 11702, 11704, 11710, 11712, 11718, 11720, 11726, 11728, 11734, 11736, 11742, 11823, 11823, 12293, 12295, 12321, 12329, 12337, 12341, 12344, 12348, 12353, 12438, 12445, 12447, 12449, 12538, 12540, 12543, 12549, 12589, 12593, 12686, 12704, 12730, 12784, 12799, 13312, 19893, 19968, 40908, 40960, 42124, 42192, 42237, 42240, 42508, 42512, 42527, 42538, 42539, 42560, 42606, 42623, 42647, 42656, 42735, 42775, 42783, 42786, 42888, 42891, 42894, 42896, 42899, 42912, 42922, 43000, 43009, 43011, 43013, 43015, 43018, 43020, 43042, 43072, 43123, 43138, 43187, 43250, 43255, 43259, 43259, 43274, 43301, 43312, 43334, 43360, 43388, 43396, 43442, 43471, 43471, 43520, 43560, 43584, 43586, 43588, 43595, 43616, 43638, 43642, 43642, 43648, 43695, 43697, 43697, 43701, 43702, 43705, 43709, 43712, 43712, 43714, 43714, 43739, 43741, 43744, 43754, 43762, 43764, 43777, 43782, 43785, 43790, 43793, 43798, 43808, 43814, 43816, 43822, 43968, 44002, 44032, 55203, 55216, 55238, 55243, 55291, 63744, 64109, 64112, 64217, 64256, 64262, 64275, 64279, 64285, 64285, 64287, 64296, 64298, 64310, 64312, 64316, 64318, 64318, 64320, 64321, 64323, 64324, 64326, 64433, 64467, 64829, 64848, 64911, 64914, 64967, 65008, 65019, 65136, 65140, 65142, 65276, 65313, 65338, 65345, 65370, 65382, 65470, 65474, 65479, 65482, 65487, 65490, 65495, 65498, 65500, };
    private readonly int[] _unicodeEs5IdentifierPart = { 170, 170, 181, 181, 186, 186, 192, 214, 216, 246, 248, 705, 710, 721, 736, 740, 748, 748, 750, 750, 768, 884, 886, 887, 890, 893, 902, 902, 904, 906, 908, 908, 910, 929, 931, 1013, 1015, 1153, 1155, 1159, 1162, 1319, 1329, 1366, 1369, 1369, 1377, 1415, 1425, 1469, 1471, 1471, 1473, 1474, 1476, 1477, 1479, 1479, 1488, 1514, 1520, 1522, 1552, 1562, 1568, 1641, 1646, 1747, 1749, 1756, 1759, 1768, 1770, 1788, 1791, 1791, 1808, 1866, 1869, 1969, 1984, 2037, 2042, 2042, 2048, 2093, 2112, 2139, 2208, 2208, 2210, 2220, 2276, 2302, 2304, 2403, 2406, 2415, 2417, 2423, 2425, 2431, 2433, 2435, 2437, 2444, 2447, 2448, 2451, 2472, 2474, 2480, 2482, 2482, 2486, 2489, 2492, 2500, 2503, 2504, 2507, 2510, 2519, 2519, 2524, 2525, 2527, 2531, 2534, 2545, 2561, 2563, 2565, 2570, 2575, 2576, 2579, 2600, 2602, 2608, 2610, 2611, 2613, 2614, 2616, 2617, 2620, 2620, 2622, 2626, 2631, 2632, 2635, 2637, 2641, 2641, 2649, 2652, 2654, 2654, 2662, 2677, 2689, 2691, 2693, 2701, 2703, 2705, 2707, 2728, 2730, 2736, 2738, 2739, 2741, 2745, 2748, 2757, 2759, 2761, 2763, 2765, 2768, 2768, 2784, 2787, 2790, 2799, 2817, 2819, 2821, 2828, 2831, 2832, 2835, 2856, 2858, 2864, 2866, 2867, 2869, 2873, 2876, 2884, 2887, 2888, 2891, 2893, 2902, 2903, 2908, 2909, 2911, 2915, 2918, 2927, 2929, 2929, 2946, 2947, 2949, 2954, 2958, 2960, 2962, 2965, 2969, 2970, 2972, 2972, 2974, 2975, 2979, 2980, 2984, 2986, 2990, 3001, 3006, 3010, 3014, 3016, 3018, 3021, 3024, 3024, 3031, 3031, 3046, 3055, 3073, 3075, 3077, 3084, 3086, 3088, 3090, 3112, 3114, 3123, 3125, 3129, 3133, 3140, 3142, 3144, 3146, 3149, 3157, 3158, 3160, 3161, 3168, 3171, 3174, 3183, 3202, 3203, 3205, 3212, 3214, 3216, 3218, 3240, 3242, 3251, 3253, 3257, 3260, 3268, 3270, 3272, 3274, 3277, 3285, 3286, 3294, 3294, 3296, 3299, 3302, 3311, 3313, 3314, 3330, 3331, 3333, 3340, 3342, 3344, 3346, 3386, 3389, 3396, 3398, 3400, 3402, 3406, 3415, 3415, 3424, 3427, 3430, 3439, 3450, 3455, 3458, 3459, 3461, 3478, 3482, 3505, 3507, 3515, 3517, 3517, 3520, 3526, 3530, 3530, 3535, 3540, 3542, 3542, 3544, 3551, 3570, 3571, 3585, 3642, 3648, 3662, 3664, 3673, 3713, 3714, 3716, 3716, 3719, 3720, 3722, 3722, 3725, 3725, 3732, 3735, 3737, 3743, 3745, 3747, 3749, 3749, 3751, 3751, 3754, 3755, 3757, 3769, 3771, 3773, 3776, 3780, 3782, 3782, 3784, 3789, 3792, 3801, 3804, 3807, 3840, 3840, 3864, 3865, 3872, 3881, 3893, 3893, 3895, 3895, 3897, 3897, 3902, 3911, 3913, 3948, 3953, 3972, 3974, 3991, 3993, 4028, 4038, 4038, 4096, 4169, 4176, 4253, 4256, 4293, 4295, 4295, 4301, 4301, 4304, 4346, 4348, 4680, 4682, 4685, 4688, 4694, 4696, 4696, 4698, 4701, 4704, 4744, 4746, 4749, 4752, 4784, 4786, 4789, 4792, 4798, 4800, 4800, 4802, 4805, 4808, 4822, 4824, 4880, 4882, 4885, 4888, 4954, 4957, 4959, 4992, 5007, 5024, 5108, 5121, 5740, 5743, 5759, 5761, 5786, 5792, 5866, 5870, 5872, 5888, 5900, 5902, 5908, 5920, 5940, 5952, 5971, 5984, 5996, 5998, 6000, 6002, 6003, 6016, 6099, 6103, 6103, 6108, 6109, 6112, 6121, 6155, 6157, 6160, 6169, 6176, 6263, 6272, 6314, 6320, 6389, 6400, 6428, 6432, 6443, 6448, 6459, 6470, 6509, 6512, 6516, 6528, 6571, 6576, 6601, 6608, 6617, 6656, 6683, 6688, 6750, 6752, 6780, 6783, 6793, 6800, 6809, 6823, 6823, 6912, 6987, 6992, 7001, 7019, 7027, 7040, 7155, 7168, 7223, 7232, 7241, 7245, 7293, 7376, 7378, 7380, 7414, 7424, 7654, 7676, 7957, 7960, 7965, 7968, 8005, 8008, 8013, 8016, 8023, 8025, 8025, 8027, 8027, 8029, 8029, 8031, 8061, 8064, 8116, 8118, 8124, 8126, 8126, 8130, 8132, 8134, 8140, 8144, 8147, 8150, 8155, 8160, 8172, 8178, 8180, 8182, 8188, 8204, 8205, 8255, 8256, 8276, 8276, 8305, 8305, 8319, 8319, 8336, 8348, 8400, 8412, 8417, 8417, 8421, 8432, 8450, 8450, 8455, 8455, 8458, 8467, 8469, 8469, 8473, 8477, 8484, 8484, 8486, 8486, 8488, 8488, 8490, 8493, 8495, 8505, 8508, 8511, 8517, 8521, 8526, 8526, 8544, 8584, 11264, 11310, 11312, 11358, 11360, 11492, 11499, 11507, 11520, 11557, 11559, 11559, 11565, 11565, 11568, 11623, 11631, 11631, 11647, 11670, 11680, 11686, 11688, 11694, 11696, 11702, 11704, 11710, 11712, 11718, 11720, 11726, 11728, 11734, 11736, 11742, 11744, 11775, 11823, 11823, 12293, 12295, 12321, 12335, 12337, 12341, 12344, 12348, 12353, 12438, 12441, 12442, 12445, 12447, 12449, 12538, 12540, 12543, 12549, 12589, 12593, 12686, 12704, 12730, 12784, 12799, 13312, 19893, 19968, 40908, 40960, 42124, 42192, 42237, 42240, 42508, 42512, 42539, 42560, 42607, 42612, 42621, 42623, 42647, 42655, 42737, 42775, 42783, 42786, 42888, 42891, 42894, 42896, 42899, 42912, 42922, 43000, 43047, 43072, 43123, 43136, 43204, 43216, 43225, 43232, 43255, 43259, 43259, 43264, 43309, 43312, 43347, 43360, 43388, 43392, 43456, 43471, 43481, 43520, 43574, 43584, 43597, 43600, 43609, 43616, 43638, 43642, 43643, 43648, 43714, 43739, 43741, 43744, 43759, 43762, 43766, 43777, 43782, 43785, 43790, 43793, 43798, 43808, 43814, 43816, 43822, 43968, 44010, 44012, 44013, 44016, 44025, 44032, 55203, 55216, 55238, 55243, 55291, 63744, 64109, 64112, 64217, 64256, 64262, 64275, 64279, 64285, 64296, 64298, 64310, 64312, 64316, 64318, 64318, 64320, 64321, 64323, 64324, 64326, 64433, 64467, 64829, 64848, 64911, 64914, 64967, 65008, 65019, 65024, 65039, 65056, 65062, 65075, 65076, 65101, 65103, 65136, 65140, 65142, 65276, 65296, 65305, 65313, 65338, 65343, 65343, 65345, 65370, 65382, 65470, 65474, 65479, 65482, 65487, 65490, 65495, 65498, 65500, };

    internal event ErrorCallback OnError;

    internal int StartPos { get; private set; }
    internal int TextPos { get; private set; }
    internal TypeScriptSyntaxKind Token { get; private set; }
    internal int TokenPos { get; private set; }
    internal string TokenText => _text.SubString(TokenPos, TextPos);
    internal string TokenValue { get; private set; } = default!;
    internal bool HasExtendedUnicodeEscape { get; private set; }
    internal bool HasPrecedingLineBreak { get; private set; }
    internal bool IsIdentifier => Token is TypeScriptSyntaxKind.Identifier or > TypeScriptSyntaxKind.LastReservedWord;
    internal bool IsReservedWord => Token is >= TypeScriptSyntaxKind.FirstReservedWord and <= TypeScriptSyntaxKind.LastReservedWord;
    internal bool IsUnterminated { get; private set; }

    internal Scanner(
        ScriptTarget languageVersion,
        bool skipTrivia,
        LanguageVariant languageVariant,
        string text,
        int start = 0,
        int? length = null)
    {
        _languageVersion = languageVersion;
        _languageVariant = languageVariant;
        _skipTrivia = skipTrivia;
        TextPos = 0;
        _end = 0;
        StartPos = start;
        TokenPos = 0;
        SetText(text, start, length);
    }

    internal static bool TokenIsIdentifierOrKeyword(TypeScriptSyntaxKind token) =>
        token >= TypeScriptSyntaxKind.Identifier;

    internal bool LookupInUnicodeMap(CharacterCode @char, int[] map)
    {
        var code = (int)@char;
        if (code < map[0])
        {
            return false;
        }

        var lo = 0;
        var hi = map.Length;
        while (lo + 1 < hi)
        {
            var mid = lo + ((hi - lo) / 2);
            // mid has to be even to catch a range's beginning
            mid -= mid % 2;
            if (map[mid] <= code && code <= map[mid + 1])
            {
                return true;
            }
            if (code < map[mid])
            {
                hi = mid;
            }
            else
            {
                lo = mid + 2;
            }
        }
        return false;
    }

    internal bool IsUnicodeIdentifierStart(
        CharacterCode code,
        ScriptTarget languageVersion) =>
        languageVersion >= ScriptTarget.Es5
            ? LookupInUnicodeMap(code, _unicodeEs5IdentifierStart)
            : LookupInUnicodeMap(code, _unicodeEs3IdentifierStart);


    internal bool IsUnicodeIdentifierPart(
        CharacterCode code,
        ScriptTarget languageVersion) =>
        languageVersion >= ScriptTarget.Es5
            ? LookupInUnicodeMap(code, _unicodeEs5IdentifierPart)
            : LookupInUnicodeMap(code, _unicodeEs3IdentifierPart);

    internal static string TokenToString(TypeScriptSyntaxKind syntaxKind) =>
        s_textToTokenMap.FirstOrDefault(kvp => kvp.Value == syntaxKind).Key;

    internal TypeScriptSyntaxKind StringToToken(string s) => s_textToTokenMap[s];

    internal List<int> ComputeLineStarts(string text)
    {
        List<int> result = new();
        var pos = 0;
        var lineStart = 0;
        while (pos < text.Length)
        {
            var ch = text.CharCodeAt(pos);
            pos++;
            switch (ch)
            {
                case CharacterCode.CarriageReturn:
                    if (text.CharCodeAt(pos) is CharacterCode.LineFeed)
                    {
                        pos++;
                    }
                    goto linefeed;
                case CharacterCode.LineFeed:
linefeed: result.Add(lineStart);
                    lineStart = pos;
                    break;
                default:
                    if (ch > CharacterCode.MaxAsciiCharacter && IsLineBreak(ch))
                    {
                        result.Add(lineStart);
                        lineStart = pos;
                    }
                    break;
            }
        }
        result.Add(lineStart);
        return result;
    }


    internal int GetPositionOfLineAndCharacter(SourceFile sourceFile, int line, int character) =>
        ComputePositionOfLineAndCharacter(GetLineStarts(sourceFile), line, character);

    internal int ComputePositionOfLineAndCharacter(int[] lineStarts, int line, int character) =>
        lineStarts[line] + character;

    internal int[] GetLineStarts(ISourceFileLike sourceFile) =>
        sourceFile.LineMap = ComputeLineStarts(sourceFile.Text).ToArray();

    internal LineAndCharacter ComputeLineAndCharacterOfPosition(int[] lineStarts, int position)
    {
        var lineNumber = BinarySearch(lineStarts, position);
        if (lineNumber < 0)
        {
            // If the actual position was not found,
            // the binary search returns the 2's-complement of the next line start
            // e.g. if the line starts at [5, 10, 23, 80] and the position requested was 20
            // then the search will return -2.
            //
            // We want the index of the previous line start, so we subtract 1.
            // Review 2's-complement if this is confusing.
            lineNumber = ~lineNumber - 1;

        }
        return new LineAndCharacter
        {
            Line = lineNumber,
            Character = position - lineStarts[lineNumber]
        };
    }


    internal LineAndCharacter GetLineAndCharacterOfPosition(
        SourceFile sourceFile,
        int position) =>
        ComputeLineAndCharacterOfPosition(
            GetLineStarts(sourceFile), position);

    internal static bool IsWhiteSpace(CharacterCode @char) =>
        IsWhiteSpaceSingleLine(@char) || IsLineBreak(@char);

    internal static bool IsWhiteSpaceSingleLine(CharacterCode @char) =>
        // Note: nextLine is in the Zs space, and should be considered to be a whitespace.
        // It is explicitly not a line-break as it isn't in the exact set specified by EcmaScript.
        @char is CharacterCode.Space or CharacterCode.Tab or CharacterCode.VerticalTab or CharacterCode.FormFeed or CharacterCode.NonBreakingSpace or CharacterCode.NextLine or CharacterCode.Ogham or >= CharacterCode.EnQuad and <= CharacterCode.ZeroWidthSpace or CharacterCode.NarrowNoBreakSpace or CharacterCode.MathematicalSpace or CharacterCode.IdeographicSpace or CharacterCode.ByteOrderMark;


    internal static bool IsLineBreak(CharacterCode @char) =>
        // ES5 7.3:
        // The ECMAScript line terminator characters are listed in Table 3.
        //     Table 3: Line Terminator Characters
        //     Code Unit Value     Name                    Formal Name
        //     \u000A              Line Feed               <LF>
        //     \u000D              Carriage Return         <CR>
        //     \u2028              Line separator          <LS>
        //     \u2029              Paragraph separator     <PS>
        // Only the characters in Table 3 are treated as line terminators. Other new line or line
        // breaking characters are treated as white space but not as line terminators.
        @char is CharacterCode.LineFeed or CharacterCode.CarriageReturn or CharacterCode.LineSeparator or CharacterCode.ParagraphSeparator;

    internal bool IsDigit(CharacterCode @char) =>
        @char is >= CharacterCode._0 and <= CharacterCode._9;

    internal static bool IsOctalDigit(CharacterCode @char) =>
        @char is >= CharacterCode._0 and <= CharacterCode._7;

    internal bool CouldStartTrivia(string text, int pos)
    {
        var @char = text.CharCodeAt(pos);
        return @char switch
        {
            // Start of conflict marker trivia
            CharacterCode.CarriageReturn or
            CharacterCode.LineFeed or
            CharacterCode.Tab or
            CharacterCode.VerticalTab or
            CharacterCode.FormFeed or
            CharacterCode.Space or
            CharacterCode.Slash or
            CharacterCode.LessThan or
            CharacterCode.equals or
            CharacterCode.GreaterThan => true,
            // Only if its the beginning can we have #! trivia
            CharacterCode.Hash => pos == 0,

            _ => @char > CharacterCode.MaxAsciiCharacter,
        };
    }

    internal static int SkipTriviaM(string text, int pos, bool stopAfterLineBreak = false, bool stopAtComments = false)
    {
        if (PositionIsSynthesized(pos))
        {
            return pos;
        }

        while (true)
        {
            if (pos >= text.Length) return pos;
            var ch = text.CharCodeAt(pos);
            switch (ch)
            {
                case CharacterCode.CarriageReturn:
                    if (pos + 1 >= text.Length) return pos;
                    if (text.CharCodeAt(pos + 1) is CharacterCode.LineFeed)
                    {
                        pos++;
                    }
                    goto linefeed;
                case CharacterCode.LineFeed:
linefeed: pos++;
                    if (stopAfterLineBreak)
                    {
                        return pos;
                    }
                    continue;
                case CharacterCode.Tab:
                case CharacterCode.VerticalTab:
                case CharacterCode.FormFeed:
                case CharacterCode.Space:
                    pos++;
                    continue;
                case CharacterCode.Slash:
                    if (stopAtComments)
                    {
                        break;
                    }
                    if (pos + 1 >= text.Length) return pos;
                    if (text.CharCodeAt(pos + 1) is CharacterCode.Slash)
                    {
                        pos += 2;
                        while (pos < text.Length)
                        {
                            if (IsLineBreak(text.CharCodeAt(pos)))
                            {
                                break;
                            }
                            pos++;
                        }
                        continue;
                    }
                    if (pos + 1 >= text.Length) return pos;
                    if (text.CharCodeAt(pos + 1) is CharacterCode.Asterisk)
                    {
                        pos += 2;
                        while (pos < text.Length)
                        {
                            if (pos + 1 >= text.Length) return pos;
                            if (text.CharCodeAt(pos) is CharacterCode.Asterisk &&
                                text.CharCodeAt(pos + 1) is CharacterCode.Slash)
                            {
                                pos += 2;
                                break;
                            }
                            pos++;
                        }
                        continue;
                    }
                    break;
                case CharacterCode.LessThan:
                case CharacterCode.equals:
                case CharacterCode.GreaterThan:
                    if (IsConflictMarkerTrivia(text, pos))
                    {
                        pos = ScanConflictMarkerTrivia(text, pos);
                        continue;
                    }
                    break;
                case CharacterCode.Hash:
                    if (pos == 0 && IsShebangTrivia(text, pos))
                    {
                        pos = ScanShebangTrivia(text, pos);
                        continue;
                    }
                    break;
                default:
                    if (ch > CharacterCode.MaxAsciiCharacter && IsWhiteSpace(ch))
                    {
                        pos++;
                        continue;
                    }
                    break;
            }
            return pos;
        }
    }

    internal static bool IsConflictMarkerTrivia(string text, int pos)
    {
        if (pos == 0 || IsLineBreak(text.CharCodeAt(pos - 1)))
        {
            var ch = text.CharCodeAt(pos);
            if (pos + s_mergeConflictMarkerLength < text.Length)
            {
                for (var i = 0; i < s_mergeConflictMarkerLength; i++)
                {
                    if (text.CharCodeAt(pos + i) != ch)
                    {
                        return false;
                    }
                };
                return ch is CharacterCode.equals ||
                    text.CharCodeAt(pos + s_mergeConflictMarkerLength) is CharacterCode.Space;
            }
        }
        return false;
    }



    internal static int ScanConflictMarkerTrivia(string text, int pos, Action<DiagnosticMessage, int> error = null)
    {
        error?.Invoke(Diagnostics.Merge_conflict_marker_encountered, s_mergeConflictMarkerLength);
        var ch = text.CharCodeAt(pos);
        var len = text.Length;
        if (ch is CharacterCode.LessThan or CharacterCode.GreaterThan)
        {
            while (pos < len && !IsLineBreak(text.CharCodeAt(pos)))
            {
                pos++;
            }
        }
        else
        {
            while (pos < len)
            {
                var ch2 = text.CharCodeAt(pos);
                if (ch2 is CharacterCode.GreaterThan && IsConflictMarkerTrivia(text, pos))
                {
                    break;
                }
                pos++;
            }
        }
        return pos;
    }

    internal static bool IsShebangTrivia(string text, int pos) =>
        // Shebangs check must only be done at the start of the file
        s_shebangTriviaRegex.Test(text);

    internal static int ScanShebangTrivia(string text, int pos)
    {
        var shebang = StringExtensions.Match(s_shebangTriviaRegex, text)[0];
        pos += shebang.Length;
        return pos;
    }

    internal static U IterateCommentRanges<T, U>(
        bool reduce,
        string text,
        int pos,
        bool trailing,
        Func<(int pos, int end, TypeScriptSyntaxKind kind, bool hasTrailingNewLine, T state, U memo), U> callback,
        T state,
        U initial = default)
    {
        var pendingPos = 0;
        var pendingEnd = 0;
        var pendingKind = TypeScriptSyntaxKind.Unknown;
        var pendingHasTrailingNewLine = false;
        var hasPendingCommentRange = false;
        var collecting = trailing || pos == 0;
        var accumulator = initial;
        while (pos >= 0 && pos < text.Length)
        {
            var ch = text.CharCodeAt(pos);
            switch (ch)
            {
                case CharacterCode.CarriageReturn:
                    if (text.CharCodeAt(pos + 1) is CharacterCode.LineFeed)
                    {
                        pos++;
                    }
                    goto linefeed;
                case CharacterCode.LineFeed:
linefeed: pos++;
                    if (trailing)
                    {
                        goto breakScan;
                    }
                    collecting = true;
                    if (hasPendingCommentRange)
                    {
                        pendingHasTrailingNewLine = true;
                    }
                    continue;
                case CharacterCode.Tab:
                case CharacterCode.VerticalTab:
                case CharacterCode.FormFeed:
                case CharacterCode.Space:
                    pos++;
                    continue;
                case CharacterCode.Slash:
                    var nextChar = text.CharCodeAt(pos + 1);
                    var hasTrailingNewLine = false;
                    if (nextChar is CharacterCode.Slash or CharacterCode.Asterisk)
                    {
                        var kind = nextChar is CharacterCode.Slash ? TypeScriptSyntaxKind.SingleLineCommentTrivia : TypeScriptSyntaxKind.MultiLineCommentTrivia;
                        var startPos = pos;
                        pos += 2;
                        if (nextChar is CharacterCode.Slash)
                        {
                            while (pos < text.Length)
                            {
                                if (IsLineBreak(text.CharCodeAt(pos)))
                                {
                                    hasTrailingNewLine = true;
                                    break;
                                }
                                pos++;
                            }
                        }
                        else
                        {
                            while (pos < text.Length)
                            {
                                if (text.CharCodeAt(pos) is CharacterCode.Asterisk && text.CharCodeAt(pos + 1) is CharacterCode.Slash)
                                {
                                    pos += 2;
                                    break;
                                }
                                pos++;
                            }
                        }
                        if (collecting)
                        {
                            if (hasPendingCommentRange)
                            {
                                accumulator = callback((pendingPos, pendingEnd, pendingKind, pendingHasTrailingNewLine, state, accumulator));
                                if (!reduce && accumulator != null)
                                {
                                    // If we are not reducing and we have a truthy result, return it.
                                    return accumulator;
                                }
                            }
                            pendingPos = startPos;
                            pendingEnd = pos;
                            pendingKind = kind;
                            pendingHasTrailingNewLine = hasTrailingNewLine;
                            hasPendingCommentRange = true;
                        }
                        continue;
                    }
                    goto breakScan;
                default:
                    if (ch > CharacterCode.MaxAsciiCharacter && IsWhiteSpace(ch))
                    {
                        if (hasPendingCommentRange && IsLineBreak(ch))
                        {
                            pendingHasTrailingNewLine = true;
                        }
                        pos++;
                        continue;
                    }
                    goto breakScan;
            }
        }
breakScan:
        if (hasPendingCommentRange)
        {
            accumulator = callback((pendingPos, pendingEnd, pendingKind, pendingHasTrailingNewLine, state, accumulator));
        }
        return accumulator;
    }


    internal U ForEachLeadingCommentRange<T, U>(
        string text,
        int pos,
        Func<(int pos, int end, TypeScriptSyntaxKind kind, bool hasTrailingNewLine, T state, U memo), U> callback,
        T state) =>
        IterateCommentRanges(reduce: false, text, pos, trailing: false, callback, state);


    internal U ForEachTrailingCommentRange<T, U>(
        string text,
        int pos,
        Func<(int pos, int end, TypeScriptSyntaxKind kind, bool hasTrailingNewLine, T state, U memo), U> callback,
        T state) =>
        IterateCommentRanges(reduce: false, text, pos, trailing: true, callback, state);

    internal static U ReduceEachLeadingCommentRange<T, U>(
        string text,
        int pos,
        Func<(int pos, int end, TypeScriptSyntaxKind kind, bool hasTrailingNewLine, T state, U memo), U> callback,
        T state,
        U initial) =>
        IterateCommentRanges(reduce: true, text, pos, trailing: false, callback, state, initial);

    internal static U ReduceEachTrailingCommentRange<T, U>(
        string text,
        int pos,
        Func<(int pos, int end, TypeScriptSyntaxKind kind, bool hasTrailingNewLine, T state, U memo), U> callback,
        T state,
        U initial) =>
        IterateCommentRanges(reduce: true, text, pos, trailing: true, callback, state, initial);


    internal static List<CommentRange> AppendCommentRange(
        (int pos, int end, TypeScriptSyntaxKind kind, bool hasTrailingNewLine, object state, List<CommentRange> comments) callback)
    {
        callback.comments = new List<CommentRange>();

        var comments = new CommentRange
        {
            Kind = callback.kind,
            HasTrailingNewLine = callback.hasTrailingNewLine
        };

        ((ITextRange)comments).Pos = callback.pos;
        ((ITextRange)comments).End = callback.end;

        callback.comments.Add(comments);

        return callback.comments;
    }

    internal static List<CommentRange> GetLeadingCommentRanges(
        string text, int pos) =>
        ReduceEachLeadingCommentRange<object, List<CommentRange>>(
            text,
            pos,
            AppendCommentRange,
            null,
            null) ?? new List<CommentRange>();

    internal static List<CommentRange> GetTrailingCommentRanges(
        string text, int pos) =>
        ReduceEachTrailingCommentRange<object, List<CommentRange>>(
            text,
            pos,
            AppendCommentRange,
            null,
            null) ?? new List<CommentRange>();

    internal string GetShebang(string text) => s_shebangTriviaRegex.Test(text)
        ? StringExtensions.Match(s_shebangTriviaRegex, text)[0]
        : null;

    internal bool IsIdentifierStart(CharacterCode code, ScriptTarget languageVersion) =>
        code is >= CharacterCode.A and <= CharacterCode.Z or >= CharacterCode.a and <= CharacterCode.z or CharacterCode.Dollar or CharacterCode._ ||
        (code > CharacterCode.MaxAsciiCharacter && IsUnicodeIdentifierStart(code, languageVersion));


    internal bool IsIdentifierPart(CharacterCode code, ScriptTarget languageVersion) => code is >= CharacterCode.A and <= CharacterCode.Z or >= CharacterCode.a and <= CharacterCode.z or >= CharacterCode._0 and <= CharacterCode._9 or CharacterCode.Dollar or CharacterCode._ ||
        (code > CharacterCode.MaxAsciiCharacter && IsUnicodeIdentifierPart(code, languageVersion));


    internal bool IsIdentifierText(string name, ScriptTarget languageVersion)
    {
        if (!IsIdentifierStart(name.CharCodeAt(0), languageVersion))
        {
            return false;
        }
        for (var i = 1; i < name.Length; i++)
        {
            if (!IsIdentifierPart(name.CharCodeAt(i), languageVersion))
            {
                return false;
            }
        };
        return true;
    }

    internal void Error(DiagnosticMessage message, int length = 0) => OnError?.Invoke(message, length);

    internal string ScanNumber()
    {
        var start = TextPos;
        while (IsDigit(_text.CharCodeAt(TextPos)))
        {
            TextPos++;
        }
        if (_text.CharCodeAt(TextPos) is CharacterCode.Dot)
        {
            TextPos++;
            while (IsDigit(_text.CharCodeAt(TextPos)))
            {
                TextPos++;
            }
        }
        var end = TextPos;
        if (_text.CharCodeAt(TextPos) is CharacterCode.E || _text.CharCodeAt(TextPos) is CharacterCode.e)
        {
            TextPos++;
            if (_text.CharCodeAt(TextPos) is CharacterCode.Plus || _text.CharCodeAt(TextPos) is CharacterCode.Minus)
            {
                TextPos++;
            }
            if (IsDigit(_text.CharCodeAt(TextPos)))
            {
                TextPos++;
                while (IsDigit(_text.CharCodeAt(TextPos)))
                {
                    TextPos++;
                }
                end = TextPos;
            }
            else
            {
                Error(Diagnostics.Digit_expected);
            }
        }
        return "" + _text.SubString(start, end);
    }


    internal int ScanOctalDigits()
    {
        var start = TextPos;
        while (IsOctalDigit(_text.CharCodeAt(TextPos)))
        {
            TextPos++;
        }
        return int.Parse(_text.SubString(start, TextPos));
    }


    internal int ScanExactNumberOfHexDigits(int count) => ScanHexDigits(minCount: count, scanAsManyAsPossible: false);


    internal int ScanMinimumNumberOfHexDigits(int count) => ScanHexDigits(minCount: count, scanAsManyAsPossible: true);


    internal int ScanHexDigits(int minCount, bool scanAsManyAsPossible)
    {
        var digits = 0;
        var value = 0;
        while (digits < minCount || scanAsManyAsPossible)
        {
            var ch = _text.CharCodeAt(TextPos);
            if (ch is >= CharacterCode._0 and <= CharacterCode._9)
            {
                value = (value * 16) + ch - CharacterCode._0;
            }
            else
        if (ch is >= CharacterCode.A and <= CharacterCode.F)
            {
                value = (value * 16) + ch - CharacterCode.A + 10;
            }
            else
        if (ch is >= CharacterCode.a and <= CharacterCode.f)
            {
                value = (value * 16) + ch - CharacterCode.a + 10;
            }
            else
            {
                break;
            }
            TextPos++;
            digits++;
        }
        if (digits < minCount)
        {
            value = -1;
        }
        return value;
    }


    internal string ScanString(bool allowEscapes = true)
    {
        var quote = _text.CharCodeAt(TextPos);
        TextPos++;
        var result = "";
        var start = TextPos;
        while (true)
        {
            if (TextPos >= _end)
            {
                result += _text.SubString(start, TextPos);
                IsUnterminated = true;
                Error(Diagnostics.Unterminated_string_literal);
                break;
            }
            var ch = _text.CharCodeAt(TextPos);
            if (ch == quote)
            {
                result += _text.SubString(start, TextPos);
                TextPos++;
                break;
            }
            if (ch is CharacterCode.Backslash && allowEscapes)
            {
                result += _text.SubString(start, TextPos);
                result += ScanEscapeSequence();
                start = TextPos;
                continue;
            }
            if (IsLineBreak(ch))
            {
                result += _text.SubString(start, TextPos);
                IsUnterminated = true;
                Error(Diagnostics.Unterminated_string_literal);
                break;
            }
            TextPos++;
        }
        return result;
    }


    internal TypeScriptSyntaxKind ScanTemplateAndSetTokenValue()
    {
        var startedWithBacktick = _text.CharCodeAt(TextPos) is CharacterCode.Backtick;
        TextPos++;
        var start = TextPos;
        var contents = "";
        TypeScriptSyntaxKind resultingToken;
        while (true)
        {
            if (TextPos >= _end)
            {
                contents += _text.SubString(start, TextPos);
                IsUnterminated = true;
                Error(Diagnostics.Unterminated_template_literal);
                resultingToken = startedWithBacktick ? TypeScriptSyntaxKind.NoSubstitutionTemplateLiteral : TypeScriptSyntaxKind.TemplateTail;
                break;
            }
            var currChar = _text.CharCodeAt(TextPos);
            if (currChar is CharacterCode.Backtick)
            {
                contents += _text.SubString(start, TextPos);
                TextPos++;
                resultingToken = startedWithBacktick ? TypeScriptSyntaxKind.NoSubstitutionTemplateLiteral : TypeScriptSyntaxKind.TemplateTail;
                break;
            }
            if (currChar is CharacterCode.Dollar && TextPos + 1 < _end && _text.CharCodeAt(TextPos + 1) is CharacterCode.OpenBrace)
            {
                contents += _text.SubString(start, TextPos);
                TextPos += 2;
                resultingToken = startedWithBacktick ? TypeScriptSyntaxKind.TemplateHead : TypeScriptSyntaxKind.TemplateMiddle;
                break;
            }
            if (currChar is CharacterCode.Backslash)
            {
                contents += _text.SubString(start, TextPos);
                contents += ScanEscapeSequence();
                start = TextPos;
                continue;
            }
            if (currChar is CharacterCode.CarriageReturn)
            {
                contents += _text.SubString(start, TextPos);
                TextPos++;
                if (TextPos < _end && _text.CharCodeAt(TextPos) is CharacterCode.LineFeed)
                {
                    TextPos++;
                }
                contents += "\n";
                start = TextPos;
                continue;
            }
            TextPos++;
        }
        //Debug.assert(resultingToken is not null);
        TokenValue = contents;
        return resultingToken;
    }


    internal string ScanEscapeSequence()
    {
        TextPos++;
        if (TextPos >= _end)
        {
            Error(Diagnostics.Unexpected_end_of_text);
            return "";
        }
        var ch = _text.CharCodeAt(TextPos);
        TextPos++;
        switch (ch)
        {
            case CharacterCode._0:
                return "\0";
            case CharacterCode.b:
                return "\b";
            case CharacterCode.t:
                return "\t";
            case CharacterCode.n:
                return "\n";
            case CharacterCode.v:
                return "\v";
            case CharacterCode.f:
                return "\f";
            case CharacterCode.r:
                return "\r";
            case CharacterCode.SingleQuote:
                return "\'";
            case CharacterCode.DoubleQuote:
                return "\"";
            case CharacterCode.u:
                if (TextPos < _end && _text.CharCodeAt(TextPos) is CharacterCode.OpenBrace)
                {
                    HasExtendedUnicodeEscape = true;
                    TextPos++;
                    return ScanExtendedUnicodeEscape();
                }
                // '\uDDDD'
                return ScanHexadecimalEscape(numDigits: 4);
            case CharacterCode.x:
                // '\xDD'
                return ScanHexadecimalEscape(numDigits: 2);
            case CharacterCode.CarriageReturn:
                if (TextPos < _end && _text.CharCodeAt(TextPos) is CharacterCode.LineFeed)
                {
                    TextPos++;
                }
                goto linefeed;
            case CharacterCode.LineFeed:
            case CharacterCode.LineSeparator:
            case CharacterCode.ParagraphSeparator:
linefeed: return "";

            default:
                return ((char)ch).ToString();
        }
    }


    internal string ScanHexadecimalEscape(int numDigits)
    {
        var escapedValue = ScanExactNumberOfHexDigits(numDigits);
        if (escapedValue >= 0)
        {
            return StringExtensions.FromCharCode(escapedValue);
        }
        else
        {
            Error(Diagnostics.Hexadecimal_digit_expected);
            return "";
        }
    }


    internal string ScanExtendedUnicodeEscape()
    {
        var escapedValue = ScanMinimumNumberOfHexDigits(1);
        var isInvalidExtendedEscape = false;
        if (escapedValue < 0)
        {
            Error(Diagnostics.Hexadecimal_digit_expected);
            isInvalidExtendedEscape = true;
        }
        else
        if (escapedValue > 0x10FFFF)
        {
            Error(Diagnostics.An_extended_Unicode_escape_value_must_be_between_0x0_and_0x10FFFF_inclusive);
            isInvalidExtendedEscape = true;
        }
        if (TextPos >= _end)
        {
            Error(Diagnostics.Unexpected_end_of_text);
            isInvalidExtendedEscape = true;
        }
        else
        if (_text.CharCodeAt(TextPos) is CharacterCode.CloseBrace)
        {
            // Only swallow the following character up if it's a '}'.
            TextPos++;
        }
        else
        {
            Error(Diagnostics.Unterminated_Unicode_escape_sequence);
            isInvalidExtendedEscape = true;
        }
        return isInvalidExtendedEscape ? "" : Utf16EncodeAsString(escapedValue);
    }


    internal string Utf16EncodeAsString(int codePoint)
    {
        Debug.Assert(codePoint is >= 0x0 and <= 0x10FFFF);
        if (codePoint <= 65535)
        {
            return StringExtensions.FromCharCode(codePoint);
        }
        var codeUnit1 = (int)Math.Floor(((double)codePoint - 65536) / 1024) + 0xD800;
        var codeUnit2 = ((codePoint - 65536) % 1024) + 0xDC00;
        return StringExtensions.FromCharCode(codeUnit1, codeUnit2);
    }


    internal CharacterCode PeekUnicodeEscape()
    {
        if (TextPos + 5 < _end && _text.CharCodeAt(TextPos + 1) is CharacterCode.u)
        {
            var start = TextPos;
            TextPos += 2;
            var value = ScanExactNumberOfHexDigits(4);
            TextPos = start;
            return (CharacterCode)value;
        }
        return (CharacterCode)(-1);
    }

    internal string ScanIdentifierParts()
    {
        var result = "";
        var start = TextPos;
        while (TextPos < _end)
        {
            var ch = _text.CharCodeAt(TextPos);
            if (IsIdentifierPart(ch, _languageVersion))
            {
                TextPos++;
            }
            else
            if (ch is CharacterCode.Backslash)
            {
                ch = PeekUnicodeEscape();
                if (!(ch >= 0 && IsIdentifierPart(ch, _languageVersion)))
                {
                    break;
                }
                result += _text.SubString(start, TextPos);
                result += StringExtensions.FromCharCode((int)ch);
                // Valid Unicode escape is always six characters
                TextPos += 6;
                start = TextPos;
            }
            else
            {
                break;
            }
        }
        result += _text.SubString(start, TextPos);
        return result;
    }

    internal TypeScriptSyntaxKind GetIdentifierToken()
    {
        var len = TokenValue.Length;
        if (len is >= 2 and <= 11)
        {
            var ch = TokenValue.CharCodeAt(0);
            if (ch is >= CharacterCode.a and <= CharacterCode.z)
            {
                if (s_textToTokenMap.ContainsKey(TokenValue))
                {
                    Token = s_textToTokenMap[TokenValue];
                    return Token;
                }
            }
        }
        Token = TypeScriptSyntaxKind.Identifier;
        return Token;
    }


    internal int ScanBinaryOrOctalDigits(int @base)
    {
        Debug.Assert(@base is 2 or 8, "Expected either @base 2 or @base 8");
        var value = 0;
        var numberOfDigits = 0;
        while (true)
        {
            var ch = _text.CharCodeAt(TextPos);
            var valueOfCh = ch - CharacterCode._0;
            if (!IsDigit(ch) || valueOfCh >= @base)
            {
                break;
            }
            value = (value * @base) + valueOfCh;
            TextPos++;
            numberOfDigits++;
        }
        return numberOfDigits == 0 ? -1 : value;
    }


    internal TypeScriptSyntaxKind Scan()
    {
        StartPos = TextPos;
        HasExtendedUnicodeEscape = false;
        HasPrecedingLineBreak = false;
        IsUnterminated = false;
        while (true)
        {
            TokenPos = TextPos;
            if (TextPos >= _end)
            {
                Token = TypeScriptSyntaxKind.EndOfFileToken;
                return Token;
            }
            var @char = _text.CharCodeAt(TextPos);
            if (@char is CharacterCode.Hash && TextPos is 0 && IsShebangTrivia(_text, TextPos))
            {
                TextPos = ScanShebangTrivia(_text, TextPos);
                if (_skipTrivia)
                {
                    continue;
                }
                else
                {
                    Token = TypeScriptSyntaxKind.ShebangTrivia;
                    return Token;
                }
            }

            switch (@char)
            {
                case CharacterCode.LineFeed:
                case CharacterCode.CarriageReturn:
                    HasPrecedingLineBreak = true;
                    if (_skipTrivia)
                    {
                        TextPos++;
                        continue;
                    }
                    else
                    {
                        if (@char is CharacterCode.CarriageReturn && TextPos + 1 < _end &&
                            _text.CharCodeAt(TextPos + 1) is CharacterCode.LineFeed)
                        {
                            TextPos += 2;
                        }
                        else
                        {
                            TextPos++;
                        }
                        Token = TypeScriptSyntaxKind.NewLineTrivia;
                        return Token;
                    }
#pragma warning disable CS0162 // Unreachable code detected
                    goto space;
#pragma warning restore CS0162 // Unreachable code detected
                case CharacterCode.Tab:
                case CharacterCode.VerticalTab:
                case CharacterCode.FormFeed:
                case CharacterCode.Space:
space: if (_skipTrivia)
                    {
                        TextPos++;
                        continue;
                    }
                    else
                    {
                        while (TextPos < _end && IsWhiteSpaceSingleLine(_text.CharCodeAt(TextPos)))
                        {
                            TextPos++;
                        }
                        Token = TypeScriptSyntaxKind.WhitespaceTrivia;
                        return Token;
                    }
#pragma warning disable CS0162 // Unreachable code detected
                    goto exclamation;
#pragma warning restore CS0162 // Unreachable code detected
                case CharacterCode.Exclamation:
exclamation: if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                    {
                        if (_text.CharCodeAt(TextPos + 2) is CharacterCode.equals)
                        {
                            TextPos += 3;
                            Token = TypeScriptSyntaxKind.ExclamationEqualsEqualsToken;
                            return Token;
                        }
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.ExclamationEqualsToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.ExclamationToken;
                    return Token;
                case CharacterCode.DoubleQuote:
                case CharacterCode.SingleQuote:
                    TokenValue = ScanString();
                    Token = TypeScriptSyntaxKind.StringLiteral;
                    return Token;
                case CharacterCode.Backtick:
                    Token = ScanTemplateAndSetTokenValue();
                    return Token;
                case CharacterCode.Percent:
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.PercentEqualsToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.PercentToken;
                    return Token;
                case CharacterCode.Ampersand:
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.Ampersand)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.AmpersandAmpersandToken;
                        return Token;
                    }
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.AmpersandEqualsToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.AmpersandToken;
                    return Token;
                case CharacterCode.OpenParen:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.OpenParenToken;
                    return Token;
                case CharacterCode.CloseParen:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.CloseParenToken;
                    return Token;
                case CharacterCode.Asterisk:
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.AsteriskEqualsToken;
                        return Token;
                    }
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.Asterisk)
                    {
                        if (_text.CharCodeAt(TextPos + 2) is CharacterCode.equals)
                        {
                            TextPos += 3;
                            Token = TypeScriptSyntaxKind.AsteriskAsteriskEqualsToken;
                            return Token;
                        }
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.AsteriskAsteriskToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.AsteriskToken;
                    return Token;
                case CharacterCode.Plus:
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.Plus)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.PlusPlusToken;
                        return Token;
                    }
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.PlusEqualsToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.PlusToken;
                    return Token;
                case CharacterCode.Comma:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.CommaToken;
                    return Token;
                case CharacterCode.Minus:
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.Minus)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.MinusMinusToken;
                        return Token;
                    }
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.MinusEqualsToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.MinusToken;
                    return Token;
                case CharacterCode.Dot:
                    if (IsDigit(_text.CharCodeAt(TextPos + 1)))
                    {
                        TokenValue = ScanNumber();
                        Token = TypeScriptSyntaxKind.NumericLiteral;
                        return Token;
                    }
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.Dot && _text.CharCodeAt(TextPos + 2) is CharacterCode.Dot)
                    {
                        TextPos += 3;
                        Token = TypeScriptSyntaxKind.DotDotDotToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.DotToken;
                    return Token;
                case CharacterCode.Slash:
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.Slash)
                    {
                        TextPos += 2;
                        while (TextPos < _end)
                        {
                            if (IsLineBreak(_text.CharCodeAt(TextPos)))
                            {
                                break;
                            }
                            TextPos++;
                        }
                        if (_skipTrivia)
                        {
                            continue;
                        }
                        else
                        {
                            Token = TypeScriptSyntaxKind.SingleLineCommentTrivia;
                            return Token;
                        }
                    }

                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.Asterisk)
                    {
                        TextPos += 2;
                        var commentClosed = false;
                        while (TextPos < _end)
                        {
                            var ch2 = _text.CharCodeAt(TextPos);
                            if (ch2 is CharacterCode.Asterisk && _text.CharCodeAt(TextPos + 1) is CharacterCode.Slash)
                            {
                                TextPos += 2;
                                commentClosed = true;
                                break;
                            }
                            if (IsLineBreak(ch2))
                            {
                                HasPrecedingLineBreak = true;
                            }
                            TextPos++;
                        }
                        if (!commentClosed)
                        {
                            Error(Diagnostics.Asterisk_Slash_expected);
                        }
                        if (_skipTrivia)
                        {
                            continue;
                        }
                        else
                        {
                            IsUnterminated = !commentClosed;
                            Token = TypeScriptSyntaxKind.MultiLineCommentTrivia;
                            return Token;
                        }
                    }
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.SlashEqualsToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.SlashToken;
                    return Token;
                case CharacterCode._0:
                    if (TextPos + 2 < _end && (_text.CharCodeAt(TextPos + 1) is CharacterCode.X || _text.CharCodeAt(TextPos + 1) is CharacterCode.x))
                    {
                        TextPos += 2;
                        var value = ScanMinimumNumberOfHexDigits(1);
                        if (value < 0)
                        {
                            Error(Diagnostics.Hexadecimal_digit_expected);
                            value = 0;
                        }
                        TokenValue = "" + value;
                        Token = TypeScriptSyntaxKind.NumericLiteral;
                        return Token;
                    }
                    else if (TextPos + 2 < _end &&
                        (_text.CharCodeAt(TextPos + 1) is CharacterCode.B || _text.CharCodeAt(TextPos + 1) is CharacterCode.b))
                    {
                        TextPos += 2;
                        var value = ScanBinaryOrOctalDigits(/* base */ 2);
                        if (value < 0)
                        {
                            Error(Diagnostics.Binary_digit_expected);
                            value = 0;
                        }
                        TokenValue = "" + value;
                        Token = TypeScriptSyntaxKind.NumericLiteral;
                        return Token;
                    }
                    else if (TextPos + 2 < _end &&
                        (_text.CharCodeAt(TextPos + 1) is CharacterCode.O || _text.CharCodeAt(TextPos + 1) is CharacterCode.o))
                    {
                        TextPos += 2;
                        var value = ScanBinaryOrOctalDigits(/* base */ 8);
                        if (value < 0)
                        {
                            Error(Diagnostics.Octal_digit_expected);
                            value = 0;
                        }
                        TokenValue = "" + value;
                        Token = TypeScriptSyntaxKind.NumericLiteral;
                        return Token;
                    }
                    if (TextPos + 1 < _end && IsOctalDigit(_text.CharCodeAt(TextPos + 1)))
                    {
                        TokenValue = "" + ScanOctalDigits();
                        Token = TypeScriptSyntaxKind.NumericLiteral;
                        return Token;
                    }
                    goto onethroughnine;
                case CharacterCode._1:
                case CharacterCode._2:
                case CharacterCode._3:
                case CharacterCode._4:
                case CharacterCode._5:
                case CharacterCode._6:
                case CharacterCode._7:
                case CharacterCode._8:
                case CharacterCode._9:
onethroughnine: TokenValue = ScanNumber();
                    Token = TypeScriptSyntaxKind.NumericLiteral;
                    return Token;
                case CharacterCode.Colon:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.ColonToken;
                    return Token;
                case CharacterCode.Semicolon:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.SemicolonToken;
                    return Token;
                case CharacterCode.LessThan:
                    if (IsConflictMarkerTrivia(_text, TextPos))
                    {
                        TextPos = ScanConflictMarkerTrivia(_text, TextPos, Error);
                        if (_skipTrivia)
                        {
                            continue;
                        }
                        else
                        {
                            Token = TypeScriptSyntaxKind.ConflictMarkerTrivia;
                            return Token;
                        }
                    }
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.LessThan)
                    {
                        if (_text.CharCodeAt(TextPos + 2) is CharacterCode.equals)
                        {
                            TextPos += 3;
                            Token = TypeScriptSyntaxKind.LessThanLessThanEqualsToken;
                            return Token;
                        }
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.LessThanLessThanToken;
                        return Token;
                    }
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.LessThanEqualsToken;
                        return Token;
                    }
                    if (_languageVariant == LanguageVariant.Jsx &&
                        _text.CharCodeAt(TextPos + 1) is CharacterCode.Slash &&
                        _text.CharCodeAt(TextPos + 2) != CharacterCode.Asterisk)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.LessThanSlashToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.LessThanToken;
                    return Token;
                case CharacterCode.equals:
                    if (IsConflictMarkerTrivia(_text, TextPos))
                    {
                        TextPos = ScanConflictMarkerTrivia(_text, TextPos, Error);
                        if (_skipTrivia)
                        {
                            continue;
                        }
                        else
                        {
                            Token = TypeScriptSyntaxKind.ConflictMarkerTrivia;
                            return Token;
                        }
                    }
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                    {
                        if (_text.CharCodeAt(TextPos + 2) is CharacterCode.equals)
                        {
                            TextPos += 3;
                            Token = TypeScriptSyntaxKind.EqualsEqualsEqualsToken;
                            return Token;
                        }
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.EqualsEqualsToken;
                        return Token;
                    }
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.GreaterThan)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.EqualsGreaterThanToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.EqualsToken;
                    return Token;
                case CharacterCode.GreaterThan:
                    if (IsConflictMarkerTrivia(_text, TextPos))
                    {
                        TextPos = ScanConflictMarkerTrivia(_text, TextPos, Error);
                        if (_skipTrivia)
                        {
                            continue;
                        }
                        else
                        {
                            Token = TypeScriptSyntaxKind.ConflictMarkerTrivia;
                            return Token;
                        }
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.GreaterThanToken;
                    return Token;
                case CharacterCode.Question:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.QuestionToken;
                    return Token;
                case CharacterCode.OpenBracket:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.OpenBracketToken;
                    return Token;
                case CharacterCode.CloseBracket:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.CloseBracketToken;
                    return Token;
                case CharacterCode.Caret:
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.CaretEqualsToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.CaretToken;
                    return Token;
                case CharacterCode.OpenBrace:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.OpenBraceToken;
                    return Token;
                case CharacterCode.Bar:
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.Bar)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.BarBarToken;
                        return Token;
                    }
                    if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                    {
                        TextPos += 2;
                        Token = TypeScriptSyntaxKind.BarEqualsToken;
                        return Token;
                    }
                    TextPos++;
                    Token = TypeScriptSyntaxKind.BarToken;
                    return Token;
                case CharacterCode.CloseBrace:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.CloseBraceToken;
                    return Token;
                case CharacterCode.Tilde:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.TildeToken;
                    return Token;
                case CharacterCode.At:
                    TextPos++;
                    Token = TypeScriptSyntaxKind.AtToken;
                    return Token;
                case CharacterCode.Backslash:
                    var cookedChar = PeekUnicodeEscape();
                    if (cookedChar >= 0 && IsIdentifierStart(cookedChar, _languageVersion))
                    {
                        TextPos += 6;
                        TokenValue = StringExtensions.FromCharCode((int)cookedChar) + ScanIdentifierParts();
                        Token = GetIdentifierToken();
                        return Token;
                    }
                    Error(Diagnostics.Invalid_character);
                    TextPos++;
                    Token = TypeScriptSyntaxKind.Unknown;
                    return Token;
                default:
                    if (IsIdentifierStart(@char, _languageVersion))
                    {
                        TextPos++;
                        while (TextPos < _end && IsIdentifierPart(@char = _text.CharCodeAt(TextPos), _languageVersion)) TextPos++;
                        TokenValue = _text.SubString(TokenPos, TextPos);
                        if (@char is CharacterCode.Backslash)
                        {
                            TokenValue += ScanIdentifierParts();
                        }
                        return Token = GetIdentifierToken();
                    }
                    else if (IsWhiteSpaceSingleLine(@char))
                    {
                        TextPos++;
                        continue;
                    }
                    else if (IsLineBreak(@char))
                    {
                        HasPrecedingLineBreak = true;
                        TextPos++;
                        continue;
                    }
                    Error(Diagnostics.Invalid_character);
                    TextPos++;
                    Token = TypeScriptSyntaxKind.Unknown;
                    return Token;
            }
        }
    }

    internal TypeScriptSyntaxKind ReScanGreaterToken()
    {
        if (Token is TypeScriptSyntaxKind.GreaterThanToken)
        {
            if (_text.CharCodeAt(TextPos) is CharacterCode.GreaterThan)
            {
                if (_text.CharCodeAt(TextPos + 1) is CharacterCode.GreaterThan)
                {
                    if (_text.CharCodeAt(TextPos + 2) is CharacterCode.equals)
                    {
                        TextPos += 3;
                        Token = TypeScriptSyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken;
                        return Token;
                    }
                    TextPos += 2;
                    Token = TypeScriptSyntaxKind.GreaterThanGreaterThanGreaterThanToken;
                    return Token;
                }
                if (_text.CharCodeAt(TextPos + 1) is CharacterCode.equals)
                {
                    TextPos += 2;
                    Token = TypeScriptSyntaxKind.GreaterThanGreaterThanEqualsToken;
                    return Token;
                }
                TextPos++;
                Token = TypeScriptSyntaxKind.GreaterThanGreaterThanToken;
                return Token;
            }
            if (_text.CharCodeAt(TextPos) is CharacterCode.equals)
            {
                TextPos++;
                Token = TypeScriptSyntaxKind.GreaterThanEqualsToken;
                return Token;
            }
        }
        return Token;
    }


    internal TypeScriptSyntaxKind ReScanSlashToken()
    {
        if (Token is TypeScriptSyntaxKind.SlashToken or TypeScriptSyntaxKind.SlashEqualsToken)
        {
            var p = TokenPos + 1;
            var inEscape = false;
            var inCharacterClass = false;
            while (true)
            {
                if (p >= _end)
                {
                    IsUnterminated = true;
                    Error(Diagnostics.Unterminated_regular_expression_literal);
                    break;
                }
                var ch = _text.CharCodeAt(p);
                if (IsLineBreak(ch))
                {
                    IsUnterminated = true;
                    Error(Diagnostics.Unterminated_regular_expression_literal);
                    break;
                }
                if (inEscape)
                {
                    // Parsing an escape character;
                    // reset the flag and just advance to the next char.
                    inEscape = false;
                }
                else
                if (ch is CharacterCode.Slash && !inCharacterClass)
                {
                    // A slash within a character class is permissible,
                    // but in general it signals the end of the regexp literal.
                    p++;
                    break;
                }
                else
                if (ch is CharacterCode.OpenBracket)
                {
                    inCharacterClass = true;
                }
                else
                if (ch is CharacterCode.Backslash)
                {
                    inEscape = true;
                }
                else
                if (ch is CharacterCode.CloseBracket)
                {
                    inCharacterClass = false;
                }
                p++;
            }
            while (p < _end && IsIdentifierPart(_text.CharCodeAt(p), _languageVersion))
            {
                p++;
            }
            TextPos = p;
            TokenValue = _text.SubString(TokenPos, TextPos);
            Token = TypeScriptSyntaxKind.RegularExpressionLiteral;
        }
        return Token;
    }


    internal TypeScriptSyntaxKind ReScanTemplateToken()
    {
        Debug.Assert(Token is TypeScriptSyntaxKind.CloseBraceToken, "'reScanTemplateToken' should only be called on a '}'");
        TextPos = TokenPos;
        Token = ScanTemplateAndSetTokenValue();
        return Token;
    }


    internal TypeScriptSyntaxKind ReScanJsxToken()
    {
        TextPos = TokenPos = StartPos;
        Token = ScanJsxToken();
        return Token;
    }


    internal TypeScriptSyntaxKind ScanJsxToken()
    {
        StartPos = TokenPos = TextPos;
        if (TextPos >= _end)
        {
            Token = TypeScriptSyntaxKind.EndOfFileToken;
            return Token;
        }
        var @char = _text.CharCodeAt(TextPos);
        if (@char is CharacterCode.LessThan)
        {
            if (_text.CharCodeAt(TextPos + 1) is CharacterCode.Slash)
            {
                TextPos += 2;
                Token = TypeScriptSyntaxKind.LessThanSlashToken;
                return Token;
            }
            TextPos++;
            Token = TypeScriptSyntaxKind.LessThanToken;
            return Token;
        }
        if (@char is CharacterCode.OpenBrace)
        {
            TextPos++;
            Token = TypeScriptSyntaxKind.OpenBraceToken;
            return Token;
        }
        while (TextPos < _end)
        {
            TextPos++;
            @char = _text.CharCodeAt(TextPos);
            if (@char is CharacterCode.OpenBrace)
            {
                break;
            }
            if (@char is CharacterCode.LessThan)
            {
                if (IsConflictMarkerTrivia(_text, TextPos))
                {
                    TextPos = ScanConflictMarkerTrivia(_text, TextPos, Error);
                    Token = TypeScriptSyntaxKind.ConflictMarkerTrivia;
                    return Token;
                }
                break;
            }
        }
        Token = TypeScriptSyntaxKind.JsxText;
        return Token;
    }


    internal TypeScriptSyntaxKind ScanJsxIdentifier()
    {
        if (TokenIsIdentifierOrKeyword(Token))
        {
            var firstCharPosition = TextPos;
            while (TextPos < _end)
            {
                var ch = _text.CharCodeAt(TextPos);
                if (ch is CharacterCode.Minus || (firstCharPosition == TextPos ? IsIdentifierStart(ch, _languageVersion) : IsIdentifierPart(ch, _languageVersion)))
                {
                    TextPos++;
                }
                else
                {
                    break;
                }
            }
            TokenValue += _text.SubString(firstCharPosition, TextPos);
        }
        return Token;
    }


    internal TypeScriptSyntaxKind ScanJsxAttributeValue()
    {
        StartPos = TextPos;
        switch (_text.CharCodeAt(TextPos))
        {
            case CharacterCode.DoubleQuote:
            case CharacterCode.SingleQuote:
                TokenValue = ScanString(allowEscapes: false);
                Token = TypeScriptSyntaxKind.StringLiteral;
                return Token;
            default:
                // If this scans anything other than `{`, it's a parse error.
                return Scan();
        }
    }


    internal TypeScriptSyntaxKind ScanJsDocToken()
    {
        if (TextPos >= _end)
        {
            Token = TypeScriptSyntaxKind.EndOfFileToken;
            return Token;
        }
        StartPos = TextPos;
        TokenPos = TextPos;
        var ch = _text.CharCodeAt(TextPos);
        switch (ch)
        {
            case CharacterCode.Tab:
            case CharacterCode.VerticalTab:
            case CharacterCode.FormFeed:
            case CharacterCode.Space:
                while (TextPos < _end && IsWhiteSpaceSingleLine(_text.CharCodeAt(TextPos)))
                {
                    TextPos++;
                }
                Token = TypeScriptSyntaxKind.WhitespaceTrivia;
                return Token;
            case CharacterCode.At:
                TextPos++;
                Token = TypeScriptSyntaxKind.AtToken;
                return Token;
            case CharacterCode.LineFeed:
            case CharacterCode.CarriageReturn:
                TextPos++;
                Token = TypeScriptSyntaxKind.NewLineTrivia;
                return Token;
            case CharacterCode.Asterisk:
                TextPos++;
                Token = TypeScriptSyntaxKind.AsteriskToken;
                return Token;
            case CharacterCode.OpenBrace:
                TextPos++;
                Token = TypeScriptSyntaxKind.OpenBraceToken;
                return Token;
            case CharacterCode.CloseBrace:
                TextPos++;
                Token = TypeScriptSyntaxKind.CloseBraceToken;
                return Token;
            case CharacterCode.OpenBracket:
                TextPos++;
                Token = TypeScriptSyntaxKind.OpenBracketToken;
                return Token;
            case CharacterCode.CloseBracket:
                TextPos++;
                Token = TypeScriptSyntaxKind.CloseBracketToken;
                return Token;
            case CharacterCode.equals:
                TextPos++;
                Token = TypeScriptSyntaxKind.EqualsToken;
                return Token;
            case CharacterCode.Comma:
                TextPos++;
                Token = TypeScriptSyntaxKind.CommaToken;
                return Token;
            case CharacterCode.Dot:
                TextPos++;
                Token = TypeScriptSyntaxKind.DotToken;
                return Token;
        }
        if (IsIdentifierStart(ch, ScriptTarget.Latest))
        {
            TextPos++;
            while (IsIdentifierPart(_text.CharCodeAt(TextPos), ScriptTarget.Latest) && TextPos < _end)
            {
                TextPos++;
            }
            Token = TypeScriptSyntaxKind.Identifier;
            return Token;
        }
        else
        {
            TextPos += 1;
            Token = TypeScriptSyntaxKind.Unknown;
            return Token;
        }
    }


    internal T SpeculationHelper<T>(Func<T> callback, bool isLookahead)
    {
        var savePos = TextPos;
        var saveStartPos = StartPos;
        var saveTokenPos = TokenPos;
        var saveToken = Token;
        var saveTokenValue = TokenValue;
        var savePrecedingLineBreak = HasPrecedingLineBreak;
        var result = callback();
        if (result is null || (result is bool && Convert.ToBoolean(result) == false) || isLookahead)
        {
            TextPos = savePos;
            StartPos = saveStartPos;
            TokenPos = saveTokenPos;
            Token = saveToken;
            TokenValue = saveTokenValue;
            HasPrecedingLineBreak = savePrecedingLineBreak;
        }
        return result;
    }


    internal T ScanRange<T>(int start, int length, Func<T> callback)
    {
        var saveEnd = _end;
        var savePos = TextPos;
        var saveStartPos = StartPos;
        var saveTokenPos = TokenPos;
        var saveToken = Token;
        var savePrecedingLineBreak = HasPrecedingLineBreak;
        var saveTokenValue = TokenValue;
        var saveHasExtendedUnicodeEscape = HasExtendedUnicodeEscape;
        var saveTokenIsUnterminated = IsUnterminated;
        SetText(_text, start, length);
        var result = callback();
        _end = saveEnd;
        TextPos = savePos;
        StartPos = saveStartPos;
        TokenPos = saveTokenPos;
        Token = saveToken;
        HasPrecedingLineBreak = savePrecedingLineBreak;
        TokenValue = saveTokenValue;
        HasExtendedUnicodeEscape = saveHasExtendedUnicodeEscape;
        IsUnterminated = saveTokenIsUnterminated;
        return result;
    }


    internal T LookAhead<T>(Func<T> callback) =>
        SpeculationHelper(callback, isLookahead: true);


    internal T TryScan<T>(Func<T> callback) =>
        SpeculationHelper(callback, isLookahead: false);


    internal string GetText() => _text;


    internal void SetText(string newText, int? start = null, int? length = null)
    {
        _text = newText ?? "";
        _end = length is null ? _text.Length : start.GetValueOrDefault() + (int)length;
        SetTextPos(start ?? 0);
    }

    internal void SetOnError(ErrorCallback errorCallback) => OnError = errorCallback;


    internal void SetScriptTarget(ScriptTarget scriptTarget) => _languageVersion = scriptTarget;


    internal void SetLanguageVariant(LanguageVariant variant) => _languageVariant = variant;


    internal void SetTextPos(int textPos)
    {
        TextPos = textPos;
        StartPos = textPos;
        TokenPos = textPos;
        Token = TypeScriptSyntaxKind.Unknown;
        HasPrecedingLineBreak = false;
        TokenValue = null;
        HasExtendedUnicodeEscape = false;
        IsUnterminated = false;
    }
}