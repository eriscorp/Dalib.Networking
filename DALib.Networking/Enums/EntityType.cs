// SPDX-License-Identifier: MIT
// (C) 2026 ERISCO, LLC. See LICENSE and CONTRIBUTORS.md.

namespace DALib.Enums;

/// <summary>
///     Coarse classification of a world entity. Byte values match the retail wire
///     encoding used by entity-request packets.
/// </summary>
public enum EntityType : byte
{
    /// <summary>A creature or NPC.</summary>
    Creature = 1,

    /// <summary>A ground item or gold pile.</summary>
    Item = 2,

    /// <summary>Another player (Aisling).</summary>
    Aisling = 4
}
