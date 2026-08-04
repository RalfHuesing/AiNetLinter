#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Maps.Skeleton;

internal sealed record SkeletonTypeInfo(
    string Namespace,
    string TypeKind,        // "class" | "record" | "interface" | "enum" | "struct"
    string Modifiers,       // z.B. "public sealed" | "internal static"
    string Name,            // inkl. Typparameter: "Handler<TCmd>"
    string? BaseTypes,      // ": IHandler<TCmd>, IDisposable" oder null
    string RelativePath,
    IReadOnlyList<SkeletonMemberInfo> Members,
    string? Id = null);     // DocumentationCommentId fuer get_symbol_body, optional fuer Enum-Werte

internal sealed record SkeletonMemberInfo(
    MemberKind Kind,
    string Signature,       // normalisierte Signatur, einzeilig
    string? MetaComment,    // "Throws: X | Uses: IRepo" oder null
    string? Id = null);     // DocumentationCommentId fuer get_symbol_body, null wo Roslyn keinen vergibt

internal enum MemberKind
{
    Field,
    Constructor,
    Property,
    PublicMethod,
    InternalMethod,
    PrivateMethod,
    Event,
}
