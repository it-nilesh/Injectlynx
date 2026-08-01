using System;
using System.Collections.Generic;

namespace Injectlynx.Core.Models;

public sealed record ModuleIdentity(string Name) : IComparable<ModuleIdentity>
{
    public int CompareTo(ModuleIdentity? other) =>
        string.Compare(Name, other?.Name, StringComparison.Ordinal);
}

public sealed record ServiceTypeIdentity(
    string Namespace,
    string MetadataName,
    string DisplayName,
    bool IsGenericTypeDefinition,
    int GenericArity) : IComparable<ServiceTypeIdentity>
{
    public string FullName => string.IsNullOrEmpty(Namespace)
        ? MetadataName
        : Namespace + "." + MetadataName;

    public int CompareTo(ServiceTypeIdentity? other) =>
        string.Compare(FullName, other?.FullName, StringComparison.Ordinal);

    public static IComparer<ServiceTypeIdentity> FullNameComparer { get; } = new Comparer();

    private sealed class Comparer : IComparer<ServiceTypeIdentity>
    {
        public int Compare(ServiceTypeIdentity? x, ServiceTypeIdentity? y) =>
            string.Compare(x?.FullName, y?.FullName, StringComparison.Ordinal);
    }
}

public sealed record KeyIdentity(string Value) : IComparable<KeyIdentity>
{
    public int CompareTo(KeyIdentity? other) =>
        string.Compare(Value, other?.Value, StringComparison.Ordinal);
}
