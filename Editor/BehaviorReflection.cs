using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Org.Shirousa.JsonBehavior.Editor
{
    /// <summary>
    /// Unity.Behavior（Runtime/Authoring）のinternal型・internalメンバーへの、
    /// リフレクション経由でのアクセスをまとめた薄い基盤。
    ///
    /// InternalsVisibleToには頼らない：パッケージがどのアセンブリ
    /// （Assets/Editor直下、Packages配下の独自asmdefのいずれでも）に置かれても
    /// 同じように動く必要があるため（README.md の設計方針を参照）。
    /// アクセス修飾子はコンパイル時のC#の制約であり、リフレクションはそれに
    /// 左右されないことを利用している。
    /// </summary>
    internal static class BehaviorReflection
    {
        const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>完全修飾名（例: "Unity.Behavior.NodeRegistry"）から型を解決する。</summary>
        internal static Type Resolve(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null)
            ?? throw new InvalidOperationException(
                $"Unity.Behaviorの型が見つかりません: {fullName}。パッケージのバージョンが対応範囲外の可能性があります。");

        /// <summary>ジェネリック型（例: TypedVariableModel&lt;&gt;）をvalueTypeで構築した閉じた型を返す。</summary>
        internal static Type ResolveGeneric(string fullNameWithArity, Type valueType) =>
            Resolve(fullNameWithArity).MakeGenericType(valueType);

        internal static object Construct(Type type, params object[] args) =>
            Activator.CreateInstance(type, AnyInstance, binder: null, args: args, culture: null)
            ?? throw new InvalidOperationException($"{type}のインスタンス化に失敗しました。");

        internal static object GetField(object target, string name) =>
            FindField(target.GetType(), name).GetValue(target);

        internal static object GetStaticField(Type type, string name) =>
            FindField(type, name).GetValue(null);

        internal static object GetProperty(object target, string name) =>
            FindProperty(target.GetType(), name).GetValue(target);

        internal static void SetProperty(object target, string name, object value) =>
            FindProperty(target.GetType(), name).SetValue(target, value);

        internal static void SetField(object target, string name, object value) =>
            FindField(target.GetType(), name).SetValue(target, value);

        /// <summary>プロパティ/フィールドがList&lt;T&gt;のとき、非ジェネリックIListとして操作する
        /// （TはUnity.Behaviorのinternal型のことが多く、こちらの型として名指しできないため）。</summary>
        internal static IList GetList(object target, string name) => (IList)GetProperty(target, name);

        internal static object InvokeMethod(object target, string name, params object[] args) =>
            FindMethod(target.GetType(), name, args.Length).Invoke(target, args);

        internal static object InvokeStaticMethod(Type type, string name, params object[] args) =>
            FindMethod(type, name, args.Length).Invoke(null, args);

        /// <summary>out引数を1つ持つメソッド（例: TryDefaultOutputPortModel(out PortModel)）を呼び出す。</summary>
        internal static object InvokeMethodWithOut(object target, string name, out object outArg)
        {
            var args = new object[] { null };
            var result = FindMethod(target.GetType(), name, 1).Invoke(target, args);
            outArg = args[0];
            return result;
        }

        static FieldInfo FindField(Type type, string name) =>
            type.GetField(name, AnyInstance)
            ?? throw new InvalidOperationException($"フィールドが見つかりません: {type}.{name}");

        static PropertyInfo FindProperty(Type type, string name) =>
            type.GetProperty(name, AnyInstance)
            ?? throw new InvalidOperationException($"プロパティが見つかりません: {type}.{name}");

        static MethodInfo FindMethod(Type type, string name, int parameterCount) =>
            type.GetMethods(AnyInstance | AnyStatic)
                .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount)
            ?? throw new InvalidOperationException($"メソッドが見つかりません: {type}.{name}（引数{parameterCount}個）");
    }
}
