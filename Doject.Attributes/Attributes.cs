using System;
using System.Diagnostics;

namespace Doject.Attributes;

/// <summary>
/// Marks a system for auto-generation of OnCreate, OnUpdate, and job cache fields.
/// </summary>
[Conditional("DOJECT_KEEP_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class AutoInjectSystemAttribute : Attribute
{
}

/// <summary>
/// Marks a job for generating ComponentLookup/BufferLookup caches.
/// </summary>
[Conditional("DOJECT_KEEP_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class AutoInjectAttribute : Attribute
{
}

/// <summary>
/// Excludes a specific ComponentLookup or BufferLookup field from auto-injection.
/// </summary>
[Conditional("DOJECT_KEEP_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class IgnoreInjectAttribute : Attribute
{
}