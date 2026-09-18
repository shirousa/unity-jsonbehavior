using System.Runtime.CompilerServices;

// テストアセンブリから、パッケージ内部のリフレクション補助クラス（BehaviorReflection等）を
// 直接使って生成結果の中身を検証できるようにする。
[assembly: InternalsVisibleTo("Org.Shirousa.JsonBehavior.Tests.Editor")]
