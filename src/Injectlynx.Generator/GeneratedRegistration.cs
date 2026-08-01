using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using Injectlynx.Core.Models;

namespace Injectlynx.Generator;

internal sealed record GeneratedRegistration(
    string Contract,
    string Implementation,
    ServiceLifetimeModel Lifetime,
    string? Key,
    ImmutableArray<string> Decorators,
    MemberInjectionPlan? MemberInjection,
    GeneratedRegistrationReason Reason);

internal sealed record MemberInjectionPlan(
    ImmutableArray<PropertyInjectionPlan> Properties,
    ImmutableArray<MethodInjectionPlan> Methods);

internal sealed record PropertyInjectionPlan(
    string Name,
    string Type,
    bool Optional);

internal sealed record MethodInjectionPlan(
    string Name,
    ImmutableArray<MethodArgumentPlan> Arguments);

internal sealed record MethodArgumentPlan(
    string Name,
    string Type,
    bool Optional,
    string? ValueExpression);

internal sealed record GeneratedRegistrationReason(
    RegistrationReasonKind Kind,
    string Summary,
    EquatableArray<string> Details);

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    private readonly T[] _items;

    public EquatableArray(IEnumerable<T> items)
    {
        _items = items.ToArray();
    }

    public IReadOnlyList<T> Items => _items;

    public bool Equals(EquatableArray<T> other) => _items.SequenceEqual(other._items);

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in _items)
        {
            hash = (hash * 31) + item.GetHashCode();
        }

        return hash;
    }
}
