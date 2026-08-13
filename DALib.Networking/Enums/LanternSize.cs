// SPDX-License-Identifier: MIT
// (C) 2026 ERISCO, LLC. See LICENSE and CONTRIBUTORS.md.

namespace DALib.Enums;

/// <summary>
///     Aisling lantern radius. Byte values match the retail wire encoding.
/// </summary>
public enum LanternSize : byte
{
    /// <summary>No lantern.</summary>
    None = 0,

    /// <summary>Small lantern radius.</summary>
    Small = 1,

    /// <summary>Large lantern radius.</summary>
    Large = 2
}
