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
    IReadOnlyList<SkeletonMemberInfo> Members
);

internal sealed record SkeletonMemberInfo(
    MemberKind Kind,
    string Signature,       // normalisierte Signatur, einzeilig
    string? MetaComment     // "Throws: X | Uses: IRepo" oder null
);

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
